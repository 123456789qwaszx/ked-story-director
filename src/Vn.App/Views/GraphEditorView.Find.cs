using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;

namespace Vn.App.Views;

/// <summary>
/// <b>[대본] 판의 찾기 (Ctrl+F)</b> — 2026-11-25 소유자 — 「대본의 그래프뷰에서 Ctrl+F 기능이 있으면 좋겠다는 의견」.
///
/// 판 오른쪽 위의 찾기 띠(<c>FindBar</c>)에 쓰는 대로 판의 모든 대사 카드(에피소드 · 분기 카드)를 찾는다 — 글 칸의 글(화자 · 대사 · 지문 · 조건 줄 —
/// 칸에 보이는 그대로, 넣지 않은 글도)과 카드 제목. 대소문자는 가리지 않는다. 찾은 것은 판의 차례(챕터 판 차례 → 위에서 아래 · 왼쪽에서 오른쪽)로
/// 세고, 첫 것으로 간다: 그 카드를 고르고, 화면 밖이면 그리로 옮기고, 글 칸에서 찾은 글자를 고른다. 접힌 장면 · 챕터 안이면 펼쳐서 간다.
/// Enter = 다음 · Shift+Enter = 앞(끝에서 처음으로 돈다), Esc = 닫고 찾은 대사에서 바로 이어 쓴다(그 칸에 초점 · 찾은 글자를 고른 채).
/// 찾기는 아무것도 바꾸지 않는다(되돌리기 칸 없음).
/// </summary>
public partial class GraphEditorView
{
    /// <summary>찾은 것 하나 — 그 카드의 글 칸 글에서 <see cref="Start"/>부터 <see cref="Length"/>글자. <see cref="Start"/>가 음수면 카드 제목에서 찾았다.</summary>
    internal sealed record FindHit(string NodeId, int Start, int Length)
    {
        public bool InTitle => Start < 0;
    }

    private List<FindHit> _findHits = [];
    private int _findIndex = -1;

    /// <summary>찾기 띠가 서 있는가.</summary>
    internal bool IsFindOpen => FindBar.IsVisible;

    /// <summary>지금 찾은 것들 — 시험이 본다.</summary>
    internal IReadOnlyList<FindHit> FindHits => _findHits;

    /// <summary>지금 가 있는 찾은 것의 차례(0부터) — 없으면 -1.</summary>
    internal int FindIndex => _findIndex;

    private void WireFind()
    {
        FindBox.TextChanged += (_, _) => UiGuard.Run(_session, "찾기", () => RunFind(fresh: true));
        FindBox.KeyDown += (_, args) => UiGuard.Run(_session, "찾기", () => FindKey(args));
        FindNextButton.Click += (_, _) => UiGuard.Run(_session, "찾기", () => StepFind(+1));
        FindPrevButton.Click += (_, _) => UiGuard.Run(_session, "찾기", () => StepFind(-1));
        FindCloseButton.Click += (_, _) => UiGuard.Run(_session, "찾기", () => CloseFind(writeAtHit: false));
    }

    /// <summary>
    /// 찾기 띠를 연다(Ctrl+F · 셸이 부른다) — 칸에 초점을 주고 글을 모두 고른다. 쓰던 글 칸에서 한 줄 안의 글을 골라 두었으면 그 글을 찾는다.
    /// 이미 열려 있으면 칸으로 돌아온다.
    /// </summary>
    internal void OpenFind()
    {
        string? seed = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox { Tag: string tag } box &&
                       tag.StartsWith(WritingTag, StringComparison.Ordinal) &&
                       box.SelectedText is { Length: > 0 } selected && !selected.Contains('\n')
            ? selected
            : null;

        FindBar.IsVisible = true;

        if (seed is not null && !string.Equals(seed, FindBox.Text, StringComparison.Ordinal))
        {
            FindBox.Text = seed;   // 찾기가 돈다(TextChanged)
        }
        else
        {
            RunFind(fresh: false);
        }

        FindBox.Focus();
        FindBox.SelectAll();
    }

    /// <summary>찾기 띠를 닫는다. <paramref name="writeAtHit"/>이면 지금 찾은 대사의 글 칸에 초점을 주고 찾은 글자를 고른다(Esc — 바로 이어 쓴다).</summary>
    internal void CloseFind(bool writeAtHit)
    {
        if (!FindBar.IsVisible)
        {
            return;
        }

        FindBar.IsVisible = false;

        if (writeAtHit && CurrentHit() is { InTitle: false } hit && FindCard(hit.NodeId)?.WritingBox is { } box)
        {
            box.Focus();
            SelectHit(box, hit);
            return;
        }

        WritingFocusSink.Focus();
    }

    private void FindKey(KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Enter:
                args.Handled = true;
                StepFind(args.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1);
                break;

            case Key.Escape:
                args.Handled = true;
                CloseFind(writeAtHit: true);
                break;
        }
    }

    private FindHit? CurrentHit() => _findIndex >= 0 && _findIndex < _findHits.Count ? _findHits[_findIndex] : null;

    /// <summary>
    /// 다시 찾는다 — <paramref name="fresh"/>(찾는 말이 바뀌었다)면 첫 것으로, 아니면 지금 가 있던 것(같은 카드 · 같은 자리)에 남는다. 찾은 것이 있으면
    /// 그리로 간다.
    /// </summary>
    private void RunFind(bool fresh)
    {
        FindHit? was = fresh ? null : CurrentHit();
        _findHits = CollectHits(FindBox.Text ?? string.Empty);
        _findIndex = _findHits.Count == 0 ? -1 : was is null ? 0 : Math.Max(0, _findHits.IndexOf(was));
        ShowFindCount();

        if (_findIndex >= 0)
        {
            GoToHit(_findIndex, unfoldTries: 2);
        }
    }

    /// <summary>다음(+1) · 앞(-1) 것으로 — 끝에서 처음으로 돈다. 그사이 글이 바뀌었을 수 있어 다시 찾고 지금 자리에서 센다.</summary>
    private void StepFind(int delta)
    {
        FindHit? was = CurrentHit();
        _findHits = CollectHits(FindBox.Text ?? string.Empty);

        if (_findHits.Count == 0)
        {
            _findIndex = -1;
            ShowFindCount();
            return;
        }

        int from = was is null ? -1 : _findHits.IndexOf(was);
        _findIndex = from < 0
            ? (delta > 0 ? 0 : _findHits.Count - 1)
            : ((from + delta) % _findHits.Count + _findHits.Count) % _findHits.Count;
        ShowFindCount();
        GoToHit(_findIndex, unfoldTries: 2);
    }

    private void ShowFindCount() =>
        FindCount.Text = string.IsNullOrEmpty(FindBox.Text)
            ? string.Empty
            : _findHits.Count == 0 ? "없음" : $"{_findIndex + 1}/{_findHits.Count}";

    /// <summary>
    /// 판의 모든 대사 카드에서 찾는다 — 챕터 판 차례, 판 안에서는 위에서 아래 · 왼쪽에서 오른쪽(줄이 같으면). 카드마다 제목이 먼저, 그다음 글 칸의 글 차례.
    /// 글 칸의 글은 판에 선 칸이면 그 칸의 글(넣지 않은 글도), 접혀 칸이 없으면 칸에 보일 글(<see cref="BoxText"/>)이다.
    /// </summary>
    private List<FindHit> CollectHits(string query)
    {
        var hits = new List<FindHit>();

        if (_session is null || string.IsNullOrWhiteSpace(query))
        {
            return hits;
        }

        foreach (StoryFile file in _session.Project.Files)
        {
            foreach (DialogueNode node in file.Nodes.OfType<DialogueNode>()
                         .OrderBy(node => node.Layout.Y)
                         .ThenBy(node => node.Layout.X))
            {
                if (ShownName(node.Id, node.Name).Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(new FindHit(node.Id, -1, 0));
                }

                string text = _writingBoxes.TryGetValue(node.Id, out TextBox? box)
                    ? box.Text ?? string.Empty
                    : BoxText(WorkingDialoguePreview.ScenarioWriting(_session.Project, node.Id));

                for (int at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                     at >= 0;
                     at = at + query.Length < text.Length ? text.IndexOf(query, at + query.Length, StringComparison.OrdinalIgnoreCase) : -1)
                {
                    hits.Add(new FindHit(node.Id, at, query.Length));
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// 찾은 것으로 간다 — 그 카드가 판에 없으면(접힌 장면 · 챕터) 펼치고 판을 다시 그린 뒤에 간다(장면이 든 챕터가 접혀 있으면 두 번 펼친다 —
    /// <paramref name="unfoldTries"/>).
    /// </summary>
    private void GoToHit(int index, int unfoldTries)
    {
        if (index < 0 || index >= _findHits.Count)
        {
            return;
        }

        FindHit hit = _findHits[index];

        if (FindCard(hit.NodeId) is null)
        {
            if (unfoldTries > 0 && UnfoldFor(hit.NodeId))
            {
                Dispatcher.UIThread.Post(() => UiGuard.Run(_session, "찾기", () =>
                {
                    if (_findIndex == index && CurrentHit() == hit)
                    {
                        GoToHit(index, unfoldTries - 1);
                    }
                }), DispatcherPriority.Background);
            }

            return;
        }

        ShowHit(hit);
    }

    /// <summary>그 카드를 접어 둔 상자를 편다 — 장면 상자면 그 장면을, 판째 접힌 상자면 그 챕터를. 상자가 없으면 false.</summary>
    private bool UnfoldFor(string nodeId)
    {
        if (_session is null ||
            _proxies.FirstOrDefault(proxy => proxy.Rows.Any(row => string.Equals(row.NodeId, nodeId, StringComparison.Ordinal))) is not { } folded)
        {
            return false;
        }

        if (folded.SceneId is { } sceneId)
        {
            _session.SetSceneCollapsed(folded.FileId, sceneId, collapsed: false);
        }
        else
        {
            _session.SetFileExpanded(folded.FileId, expanded: true);
        }

        return true;
    }

    /// <summary>그 카드를 고르고(화면 밖이면 그리로 옮긴다) 글 칸에서 찾은 글자를 고른다 — 초점은 찾기 칸에 남는다(Enter로 이어 찾는다).</summary>
    private void ShowHit(FindHit hit)
    {
        if (_session is null || FindCard(hit.NodeId) is not { } card)
        {
            return;
        }

        if (!string.Equals(_session.SelectedNodeId, hit.NodeId, StringComparison.Ordinal))
        {
            _session.Select(hit.NodeId);
        }

        RevealTarget(card.Visual);

        if (!hit.InTitle && card.WritingBox is { } box)
        {
            SelectHit(box, hit);
        }
    }

    private static void SelectHit(TextBox box, FindHit hit)
    {
        int length = (box.Text ?? string.Empty).Length;
        int start = Math.Clamp(hit.Start, 0, length);
        box.SelectionStart = start;
        box.SelectionEnd = Math.Clamp(start + hit.Length, start, length);
    }
}
