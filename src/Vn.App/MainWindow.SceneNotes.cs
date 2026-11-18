using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.Authoring.Chapters;

namespace Vn.App;

/// <summary>
/// <b>장면 메모 공간</b> — [대본] 탭 하이어아키에서 장면을 고르면 인스펙터에 선다(<c>docs/work-orders/scene-notes-orders.md</c> S4).
///
/// 둘로 이루어진다: 작가가 자유롭게 적는 <b>장면 메모</b> 한 칸과, [+ 에피소드]로 그 장면의 에피소드를 골라 붙이는 <b>에피소드 카드</b>
/// (그 에피소드에만 해당하는 지시 — 에피소드의 <see cref="ChapterEpisode.Memo"/>). 카드를 붙였다는 것은 에피소드에
/// 저장된다(<see cref="ChapterEpisode.NoteCard"/>) — 지시가 비어도 카드는 남고 [빼기]만 뗀다. 카드의 차례는 장면 안 에피소드 차례다.
///
/// 칸은 초점이 떠날 때와 Ctrl+Enter에 넣는다(Enter는 줄바꿈 — 여러 줄 글이다). Esc는 넣지 않은 글을 걷는다. 넣기는 되돌리기 한
/// 칸이고 인스펙터를 다시 세우지 않는다(<c>NodeMetadata</c>) — 다른 칸으로 옮겨 가는 누름이 사라진 칸에 떨어지지 않게.
///
/// 챕터를 고르면 <b>주인 없는 장면 메모</b>(그 장면의 에피소드가 없어진 메모)가 [지우기]와 함께 선다.
/// </summary>
public partial class MainWindow
{
    internal const string SceneNoteGuide =
        "이 장면의 사정을 자유롭게 적습니다 — 보여 줄 것 · 인물마다 감정(처음 → 끝) · 감정이 꺾이는 대사 · " +
        "무대에 있는 사람 · 장소 · 시간. 연출(움직임 · 표정 이름)은 적지 않습니다.";

    internal const string EpisodeNoteGuide = "이 에피소드에만 해당하는 사정 · 지시";

    /// <summary>장면 요약 칸의 메모 공간 — 장면 메모 칸, 에피소드 카드, [+ 에피소드].</summary>
    private void SceneNoteSpace(string chapterId, ChapterScene scene)
    {
        string sceneId = scene.SceneId;

        Heading("장면 메모");

        TextBox sceneBox = NoteBox("scene-note", SceneNoteNow(chapterId, sceneId), SceneNoteGuide, minHeight: 72);
        WireNote(sceneBox, "장면 메모",
            () => SceneNoteNow(chapterId, sceneId),
            text => _session.Editor.SetSceneNote(chapterId, sceneId, text),
            committed: null);
        DirectorSummary.Children.Add(sceneBox);

        Heading("에피소드 지시");
        Note("장면 안의 에피소드를 골라 그 에피소드에만 해당하는 지시를 적습니다. 지시가 비어도 카드는 남고, [빼기]로 뗍니다. " +
             "Enter는 줄바꿈, Ctrl+Enter로 넣고 Esc로 되돌립니다.");

        var cards = new StackPanel { Spacing = 6 };
        var shown = new List<string>();   // 카드가 선 에피소드 — 장면 안 차례로
        var add = new ComboBox
        {
            PlaceholderText = "＋ 에피소드",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = "scene-note-add"
        };
        bool refilling = false;

        int OrderOf(string episodeId) =>
            scene.Episodes.ToList().FindIndex(episode => string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal));

        void RefillAdd()
        {
            refilling = true;

            try
            {
                List<ComboBoxItem> items = scene.Episodes
                    .Where(episode => !shown.Contains(episode.EpisodeId))
                    .Select(episode => new ComboBoxItem
                    {
                        Content = episode.ShownTitle,
                        Tag = "scene-note-add:" + episode.EpisodeId
                    })
                    .ToList();

                add.SelectedIndex = -1;
                add.ItemsSource = items;
                add.IsEnabled = items.Count > 0;
                add.PlaceholderText = add.IsEnabled ? "＋ 에피소드" : "장면의 에피소드마다 카드가 있습니다";
            }
            finally
            {
                refilling = false;
            }
        }

        void Drop(string episodeId, Control card)
        {
            cards.Children.Remove(card);
            shown.Remove(episodeId);
            RefillAdd();
        }

        TextBox Card(ChapterEpisode episode)
        {
            string episodeId = episode.EpisodeId;
            int at = shown.Count(id => OrderOf(id) < OrderOf(episodeId));
            shown.Insert(at, episodeId);

            bool root = string.Equals(scene.RootEpisodeId, episodeId, StringComparison.Ordinal);
            TextBox box = NoteBox("episode-note:" + episodeId, EpisodeMemoNow(chapterId, episodeId), EpisodeNoteGuide, minHeight: 44);

            var remove = new Button
            {
                Content = "빼기",
                FontSize = 10,
                Padding = new Thickness(6, 1),
                Tag = "episode-note-remove:" + episodeId
            };
            ToolTip.SetTip(remove, "이 에피소드의 지시를 지우고 카드를 뺍니다 — 되돌리기(Ctrl+Z)로 돌아옵니다.");
            DockPanel.SetDock(remove, Dock.Right);

            var head = new DockPanel { LastChildFill = true };
            head.Children.Add(remove);
            head.Children.Add(new TextBlock
            {
                Text = (root ? "⌂ " : "· ") + episode.ShownTitle,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var card = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6),
                Tag = "episode-note-card:" + episodeId,
                Child = new StackPanel { Spacing = 4, Children = { head, box } }
            };

            WireNote(box, "에피소드 지시",
                () => EpisodeMemoNow(chapterId, episodeId),
                text => _session.Editor.UpdateEpisodeMemo(chapterId, episodeId, text),
                committed: null);   // 지시를 비워도 카드는 남는다 — [빼기]만 뗀다

            remove.Click += (_, _) => UiGuard.Run(_session, "에피소드 지시 빼기", () =>
            {
                _session.Editor.RemoveEpisodeNoteCard(chapterId, episodeId);
                Drop(episodeId, card);
                _session.SetStatus($"'{episode.ShownTitle}'의 지시 카드를 뺐습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
            });

            cards.Children.Insert(at, card);
            return box;
        }

        foreach (ChapterEpisode episode in scene.Episodes.Where(episode => HasCardNow(chapterId, episode.EpisodeId)))
        {
            Card(episode);
        }

        add.SelectionChanged += (_, _) => UiGuard.Run(_session, "에피소드 카드", () =>
        {
            if (refilling || add.SelectedItem is not ComboBoxItem { Tag: string tag } ||
                scene.Episodes.FirstOrDefault(episode => "scene-note-add:" + episode.EpisodeId == tag) is not { } picked)
            {
                return;
            }

            // 고르기가 끝난 뒤에 세운다 — 고르는 중에 목록을 바꾸면 고르기가 엉킨다.
            Dispatcher.UIThread.Post(() => UiGuard.Run(_session, "에피소드 카드", () =>
            {
                if (shown.Contains(picked.EpisodeId))
                {
                    return;
                }

                _session.Editor.AddEpisodeNoteCard(chapterId, picked.EpisodeId);   // 비어도 남는다 — 저장된다
                TextBox box = Card(picked);
                RefillAdd();
                box.Focus();
            }));
        });

        RefillAdd();
        DirectorSummary.Children.Add(cards);
        DirectorSummary.Children.Add(add);
    }

    /// <summary>
    /// 챕터 요약 칸의 <b>주인 없는 장면 메모</b> — 그 장면의 에피소드가 모두 없어지거나 다른 장면으로 옮겨져 주인이 없어진 메모. 지우는 자리가
    /// 여기다(검증 경고 <see cref="ChapterDiagnosticCode.SceneNoteOrphaned"/>가 이리로 보낸다). 없으면 아무것도 세우지 않는다.
    /// </summary>
    private void OrphanSceneNotes(string chapterId)
    {
        if (_session.Editor.FindChapter(chapterId) is not { } chapter ||
            ChapterSceneNotes.Orphans(chapter.Episodes, chapter.SceneNotes) is not { Count: > 0 } orphans)
        {
            return;
        }

        Heading("주인 없는 장면 메모");
        Note("그 장면의 에피소드가 없어진 메모입니다. 같은 이름으로 장면을 되살리면 다시 그 장면의 메모가 됩니다.");

        foreach (ChapterSceneNote orphan in orphans)
        {
            string sceneId = orphan.SceneId;

            var remove = new Button
            {
                Content = "지우기",
                FontSize = 10,
                Padding = new Thickness(6, 1),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = "orphan-note-remove:" + sceneId
            };
            remove.Click += (_, _) => UiGuard.Run(_session, "주인 없는 메모 지우기", () =>
            {
                _session.Editor.RemoveSceneNote(chapterId, sceneId);
                _session.SetStatus($"장면 '{orphan.Name?.Trim() ?? sceneId}'의 메모를 지웠습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
                ShowSelectedNode();
            });
            DockPanel.SetDock(remove, Dock.Right);

            var row = new DockPanel { LastChildFill = true, Tag = "orphan-note:" + sceneId };
            row.Children.Add(remove);
            row.Children.Add(new SelectableTextBlock
            {
                Text = $"{orphan.Name?.Trim() ?? sceneId}" +
                       (string.IsNullOrWhiteSpace(orphan.Text) ? " — (이름만)" : $" — {orphan.Text}"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });

            DirectorSummary.Children.Add(row);
        }
    }

    private string? SceneNoteNow(string chapterId, string sceneId) =>
        _session.Editor.FindChapter(chapterId) is { } chapter ? ChapterSceneNotes.TextOf(chapter.SceneNotes, sceneId) : null;

    private bool HasCardNow(string chapterId, string episodeId) =>
        _session.Editor.FindChapter(chapterId)?.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))?.HasNoteCard == true;

    private string? EpisodeMemoNow(string chapterId, string episodeId) =>
        _session.Editor.FindChapter(chapterId)?.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))?.Memo is { } memo &&
        !string.IsNullOrWhiteSpace(memo)
            ? memo
            : null;

    private static TextBox NoteBox(string tag, string? text, string guide, double minHeight) => new()
    {
        Text = text ?? string.Empty,
        PlaceholderText = guide,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = minHeight,
        FontSize = 12,
        Tag = tag
    };

    /// <summary>
    /// 메모 칸의 넣기 — 초점이 떠날 때 · Ctrl+Enter. Esc는 넣지 않은 글을 걷는다. 넣은 뒤 칸을 모델의 값(앞뒤 빈칸을 걷은 글)으로
    /// 맞추고 <paramref name="committed"/>를 부른다.
    /// </summary>
    private void WireNote(TextBox box, string what, Func<string?> now, Action<string> commit, Action? committed)
    {
        void Commit() => UiGuard.Run(_session, what, () =>
        {
            commit(box.Text ?? string.Empty);
            string stored = now() ?? string.Empty;

            if (!string.Equals(box.Text, stored, StringComparison.Ordinal))
            {
                box.Text = stored;
            }

            committed?.Invoke();
        });

        box.LostFocus += (_, _) => Commit();
        box.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Enter && args.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                args.Handled = true;
                Commit();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                box.Text = now() ?? string.Empty;
            }
        }, RoutingStrategies.Tunnel);
    }
}
