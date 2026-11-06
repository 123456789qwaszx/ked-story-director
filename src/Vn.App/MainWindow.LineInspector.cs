using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App;

/// <summary>
/// <b>[대본] 탭 인스펙터의 선 설명</b> — 판에서 선(선택지 레인 · 분기 선)을 누르거나 범위 선택으로 고르면 인스펙터가 그
/// 선을 설명한다. 선택지는 문구 · 표시조건 · 해금조건 · 스탯변화를, 분기는 그 조건을 보인다. 고치는 자리는 [구성] 탭이라
/// 「[구성] 탭에서 …」 단추가 그리로 데려간다.
/// </summary>
public partial class MainWindow
{
    /// <summary>[대본] 판에서 고른 선 — 인스펙터가 그 선을 설명하는 중이면. 카드나 하이어아키 줄을 고르면 풀린다.</summary>
    private GraphEditorView.PickedLine? _directorLine;

    /// <summary>
    /// [대본] 판에서 선을 골랐다(null이면 놓았다). 카드 선택은 놓는다 — 인스펙터는 하나만 보이고, 그래야 다음에 카드를 누르는
    /// 것이 선택의 변화가 되어 선 설명이 풀린다.
    /// </summary>
    private void OnDirectorLinePicked(GraphEditorView.PickedLine? line)
    {
        if (line is null && _directorLine is null)
        {
            return;
        }

        _directorLine = line;

        if (line is not null)
        {
            _directorSummary = null;

            if (_session.SelectedNodeId is not null)
            {
                _session.Select(null);   // 선택이 바뀐 알림이 인스펙터를 다시 세운다
                return;
            }
        }

        ShowSelectedNode();
    }

    /// <summary>고른 선의 설명 — <see cref="ShowDirectorInspector"/>가 부른다.</summary>
    private void FillLineSummary(GraphEditorView.PickedLine line)
    {
        DirectorSummary.Children.Clear();

        switch (line)
        {
            case GraphEditorView.PickedLine.Choice choice:
                FillChoiceSummary(choice);
                break;

            case GraphEditorView.PickedLine.Exit exit:
                FillExitSummary(exit.Connection);
                break;
        }
    }

    private void FillChoiceSummary(GraphEditorView.PickedLine.Choice choice)
    {
        ChapterGraphModel? chapter = _session.Editor.FindChapter(choice.ChapterId)?.ToGraphModel(choice.ChapterId, _session.Definition);
        ChapterEdge? edge = chapter?.Edges.FirstOrDefault(item =>
            string.Equals(item.FromEpisodeId, choice.From, StringComparison.Ordinal) &&
            string.Equals(item.ToEpisodeId, choice.To, StringComparison.Ordinal) &&
            string.Equals(item.OptionLabel, choice.Label, StringComparison.Ordinal));

        if (chapter is null || edge is null)
        {
            Graph.SetInspectorHeader("인스펙터", "고른 선택지가 사라졌습니다 — 판에서 다시 고르세요");
            return;
        }

        Graph.SetInspectorHeader($"선택지 · {edge.FromEpisodeId} → {edge.ToEpisodeId}", "고치는 곳은 [구성] 탭입니다");

        Fact("문구", edge.Auto ? "AUTO — 플레이어 입력 없이 넘어갑니다"
            : edge.HasNoOptionLabel ? "⚠ 문구 없음"
            : $"「{edge.OptionLabel}」");

        if (edge.VisibleConditionLabel is { } visible)
        {
            Fact("표시조건", $"{visible} — 아니면 목록에 안 보입니다");
        }

        if (edge.ConditionLabel is { } unlock)
        {
            Fact("해금조건", $"{unlock} — 아니면 잠긴 채 보입니다");

            if (edge.LockedMessage is { Length: > 0 } locked)
            {
                Fact("잠금 안내문", locked);
            }
        }

        if (edge.StatChanges.Count > 0)
        {
            Fact("스탯변화", string.Join(" · ", edge.StatChanges.Select(delta => ChapterGraphView.DeltaText(chapter.Stats, delta))));
        }

        if (!edge.HasGate && edge.StatChanges.Count == 0)
        {
            Note("조건과 스탯변화가 없습니다.");
        }

        EpisodeLink("출발", choice.ChapterId, edge.FromEpisodeId);
        EpisodeLink("도착", choice.ChapterId, edge.ToEpisodeId);

        string from = edge.FromEpisodeId;
        string to = edge.ToEpisodeId;
        string label = edge.OptionLabel?.Trim() ?? string.Empty;
        ChapterTabButton("[구성] 탭에서 이 선택지 고치기", () =>
        {
            MainTabs.SelectedItem = ChapterTabItem;
            ChapterGraph.SelectChapter(choice.ChapterId);
            ChapterGraph.SelectEdgeKey(from, to, label);
        });
    }

    private void FillExitSummary(GraphConnectionProjection connection)
    {
        string from = CardName(connection.SourceNodeId);
        string to = CardName(connection.TargetNodeId);
        ExitPort? port = connection.ExecutionPort;

        if (port is { Kind: ExitPortKind.Detour, TargetNodeId: { } target, ExitKey: { } branchKey })
        {
            Graph.SetInspectorHeader($"분기 · {from} → {to}", "조건이 맞으면 다녀오는 에피소드");
            BranchEntryCondition.State state = BranchEntryCondition.Read(_session.Project, target, _session.Definition);
            Fact("조건", state.Label ?? "없음");
            Note(BranchConditionNote.Sentence(state));
            NodeLink("출발", connection.SourceNodeId, from);
            NodeLink("다녀갈 곳", target, to);

            if (_session.Project.FindNode(connection.SourceNodeId) is { } source &&
                EpisodeNaming.ChapterOf(_session.Project, source) is { } chapter)
            {
                string chapterId = chapter.ChapterId;
                ChapterTabButton(state.Label is null ? "[구성] 탭에서 조건 정하기" : "[구성] 탭에서 조건 바꾸기",
                    () => OpenBranchCondition(chapterId, branchKey));
            }

            return;
        }

        if (connection.Kind == GraphConnectionKind.ExecutionBranch)
        {
            Graph.SetInspectorHeader($"갈래 · {from} → {to}", "조건 갈래의 출구");
            Fact("갈래", connection.Label);
            Note("이 갈래를 지나면 이리로 갑니다.");
        }
        else
        {
            Graph.SetInspectorHeader($"실행 선 · {from} → {to}", "기본 출구");
            Note("카드가 끝나면 이리로 갑니다.");
        }

        NodeLink("출발", connection.SourceNodeId, from);
        NodeLink("도착", connection.TargetNodeId, to);
    }

    /// <summary>카드의 이름 — 에피소드 카드면 에피소드 Id, 아니면 노드 이름.</summary>
    private string CardName(string nodeId) => _session.Project.FindNode(nodeId) switch
    {
        DialogueNode dialogue when EpisodeNaming.ChapterOf(_session.Project, dialogue) is not null =>
            EpisodeNaming.EpisodeIdOf(dialogue),
        { } node => node.Name,
        null => nodeId
    };

    /// <summary>선 설명의 한 줄 — 「이름: 값」.</summary>
    private void Fact(string name, string value) => DirectorSummary.Children.Add(new TextBlock
    {
        Text = $"{name}: {value}",
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Tag = "summary-fact"
    });

    /// <summary>그 에피소드의 카드로 가는 고리 — 카드가 없으면 이름만 적는다.</summary>
    private void EpisodeLink(string role, string chapterId, string episodeId)
    {
        if (EpisodeNaming.CardFor(_session.Project, chapterId, episodeId) is { } card)
        {
            NodeLink(role, card.Id, episodeId);
        }
        else
        {
            Fact(role, episodeId);
        }
    }

    /// <summary>그 카드를 고르는 고리 — 인스펙터가 그 카드의 대사 편집기로 바뀐다.</summary>
    private void NodeLink(string role, string nodeId, string name) =>
        Link($"{role} · {name}", "summary-node:" + nodeId, () => _session.Select(nodeId));

    /// <summary>「[구성] 탭에서 …」 단추 — 고치는 자리로 데려간다.</summary>
    private void ChapterTabButton(string text, Action open)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0),
            Tag = "summary-open"
        };
        button.Click += (_, _) => UiGuard.Run(_session, "[구성] 탭에서 열기", open);
        DirectorSummary.Children.Add(button);
    }
}
