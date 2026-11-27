using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.App.Strings;

namespace Vn.App;

/// <summary>
/// <b>[대본] 탭 인스펙터의 선 설명</b> — 판에서 선(선택지 레인 · 분기 선)을 누르거나 범위 선택으로 고르면 인스펙터가 그
/// 선을 설명한다. 선택지는 문구 · 표시조건 · 해금조건 · 스탯변화를, 분기는 그 조건을 보인다. 선택지 문구는 여기서 고치고
/// (자동 길 ↔ 선택지도 여기서 바뀐다), 조건 · 스탯변화를 고치는 자리는 [구성] 탭이라 「[구성] 탭에서 …」 단추가 그리로 데려간다.
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
            Graph.ShowSideTab(SideTab.Inspector);   // 고른 선을 보인다(side-tabs V3)

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
        ChapterGraphModel? chapter = _session.Editor.FindChapter(choice.ChapterId)?.ToGraphModel(choice.ChapterId);
        ChapterEdge? edge = chapter?.Edges.FirstOrDefault(item =>
            string.Equals(item.FromEpisodeId, choice.From, StringComparison.Ordinal) &&
            string.Equals(item.ToEpisodeId, choice.To, StringComparison.Ordinal) &&
            string.Equals(item.OptionLabel, choice.Label, StringComparison.Ordinal));

        if (chapter is null || edge is null)
        {
            Graph.SetInspectorHeader(Ui.Board_Inspector, Ui.Side_SelectedChoiceIsGoneSelect);
            return;
        }

        Graph.SetInspectorHeader(
            string.Format(Ui.Side_Choice, TitleOf(chapter, edge.FromEpisodeId), TitleOf(chapter, edge.ToEpisodeId)),
            Ui.Side_EditTextHereAndConditions);

        ChoiceLabelEditor(choice, chapter, edge);

        if (edge.VisibleConditionLabel is { } visible)
        {
            Fact(Ui.Side_ShowCondition, string.Format(Ui.Side_OtherwiseItIsnTListed, visible));
        }

        if (edge.ConditionLabel is { } unlock)
        {
            Fact(Ui.Side_UnlockCondition, string.Format(Ui.Side_OtherwiseItSShownLocked, unlock));

            if (edge.LockedMessage is { Length: > 0 } locked)
            {
                Fact(Ui.Side_LockedHint, locked);
            }
        }

        if (edge.StatChanges.Count > 0)
        {
            Fact(Ui.Side_StatChange, string.Join(" · ", edge.StatChanges.Select(delta => ChapterGraphView.DeltaText(chapter.Stats, delta))));
        }

        if (!edge.HasGate && edge.StatChanges.Count == 0)
        {
            Note(Ui.Side_NoConditionsOrStatChanges);
        }

        // 출발 · 도착 고리는 두지 않는다(2026-11-17 소유자) — 머리글이 이미 말한다.
        string from = edge.FromEpisodeId;
        string to = edge.ToEpisodeId;
        string label = edge.OptionLabel?.Trim() ?? string.Empty;
        ChapterTabButton(Ui.Side_EditThisChoiceInStructure, () =>
        {
            MainTabs.SelectedItem = ChapterTabItem;
            ChapterGraph.SelectChapter(choice.ChapterId);
            ChapterGraph.SelectEdgeKey(from, to, label);
        });
    }

    /// <summary>
    /// 「문구」 칸 — 고치고 Enter(또는 딴 데를 누르면) 넣고, Esc는 되돌린다. 자동 길(AUTO)에 문구를 적으면 선택지가 되고, 그
    /// 에피소드의 하나뿐인 선택지 문구를 지우면 자동 길이 된다(<see cref="ApplyChoiceLabel"/>).
    /// </summary>
    private void ChoiceLabelEditor(GraphEditorView.PickedLine.Choice choice, ChapterGraphModel chapter, ChapterEdge edge)
    {
        string shown = edge.Auto ? string.Empty : edge.OptionLabel?.Trim() ?? string.Empty;
        var box = new TextBox
        {
            Text = shown,
            FontSize = Skin.Font(12),
            PlaceholderText = Ui.Side_LeaveEmptyForAUTOMoves,
            Tag = "summary-label"
        };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        row.Children.Add(new TextBlock
        {
            Text = Ui.Side_Text,
            FontSize = Skin.Font(12),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        Grid.SetColumn(box, 1);
        row.Children.Add(box);
        _sheet.Children.Add(row);

        if (edge.Auto)
        {
            Guide(Ui.Side_AUTOMovesOnWithoutPlayer, Ui.Side_AUTOMovesOnWithoutPlayer2);
        }
        else if (edge.HasNoOptionLabel)
        {
            Note(string.Format(Ui.Side_NoTextChoiceNeedsText, MissingChoiceText.For(edge.ToEpisodeId)));
        }

        bool applied = false;

        void Apply()
        {
            if (applied)
            {
                return;
            }

            applied = true;
            ApplyChoiceLabel(choice, chapter, edge, box.Text?.Trim() ?? string.Empty);
        }

        box.KeyDown += (_, args) => UiGuard.Run(_session, Ui.Board_ChoiceText, () =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Apply();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                box.Text = shown;
            }
        });

        // 딴 데를 누르면 Enter처럼 넣는다 — 넣기는 판 · 인스펙터를 다시 세우므로 초점 처리가 끝난 뒤로 미룬다.
        box.LostFocus += (_, _) => Dispatcher.UIThread.Post(() => UiGuard.Run(_session, Ui.Board_ChoiceText, Apply));
    }

    /// <summary>
    /// 선택지 문구를 넣는다. 같으면 아무것도 안 한다.
    /// <list type="bullet">
    /// <item>문구를 적었다 — 그 문구의 선택지다. 자동 길이었으면 선택지가 된다.</item>
    /// <item>문구를 지웠다 — 그 에피소드의 길이 이것 하나면 자동 길이 된다(장면을 넘어도 된다 — ⛔ 2026-11-18까지는 같은 장면
    /// 안에서만이었다). 길이 둘 이상이면 넣지 않는다 — 문구 없는 선택지는 오류다.</item>
    /// </list>
    /// 넣은 뒤에는 바뀐 신원(출발 · 도착 · 문구)으로 그 레인을 다시 고른다 — 판이 다시 그려지며 고른 선이 풀리기 때문이다.
    /// </summary>
    private void ApplyChoiceLabel(
        GraphEditorView.PickedLine.Choice choice, ChapterGraphModel chapter, ChapterEdge edge, string wanted)
    {
        string current = edge.Auto ? string.Empty : edge.OptionLabel?.Trim() ?? string.Empty;

        if (string.Equals(wanted, current, StringComparison.Ordinal))
        {
            return;
        }

        bool toAuto = wanted.Length == 0;

        if (toAuto)
        {
            int ways = chapter.Edges.Count(item => string.Equals(item.FromEpisodeId, edge.FromEpisodeId, StringComparison.Ordinal));

            if (ways > 1)
            {
                _session.SetStatus(Ui.Side_TextWasnTClearedChoices);
                ShowSelectedNode();
                return;
            }
        }

        _session.Editor.UpdateEdge(
            choice.ChapterId, edge.FromEpisodeId, edge.ToEpisodeId,
            matchOptionLabel: edge.OptionLabel ?? string.Empty,
            optionLabel: wanted,
            auto: toAuto);

        _session.SetStatus(toAuto
            ? Ui.Side_ClearingTextMadeItAuto
            : edge.Auto
                ? string.Format(Ui.Side_AutoPathBecameChoiceUndo, wanted)
                : string.Format(Ui.Side_ChangedChoiceTextToUndo, wanted));

        string chapterId = choice.ChapterId;
        string fromId = edge.FromEpisodeId;
        string toId = edge.ToEpisodeId;
        Dispatcher.UIThread.Post(
            () => UiGuard.Run(_session, Ui.Side_ReselectChoice, () => Graph.SelectChoiceRail(chapterId, fromId, toId, wanted)),
            DispatcherPriority.Background);
    }

    private void FillExitSummary(GraphConnectionProjection connection)
    {
        string from = CardName(connection.SourceNodeId);
        string to = CardName(connection.TargetNodeId);
        ExitPort? port = connection.ExecutionPort;

        if (port is { Kind: ExitPortKind.Detour, TargetNodeId: { } target })
        {
            Graph.SetInspectorHeader(string.Format(Ui.Side_Branch, from, to), Ui.Side_BranchCardVisitedAndReturned);
            BranchEntryCondition.State state = BranchEntryCondition.Read(_session.Project, target);

            // 조건은 여기서 고르지 않는다 — 그 분기 카드를 고르면 대본 위쪽에 칸이 선다(2026-11-17 소유자: 분기는 무조건 들른 뒤
            // 그 카드에서 if로 보므로). ⛔ 같은 날 아침에는 여기 칸이 있었다.
            string sentence = BranchConditionNote.Sentence(state, BranchConditionNote.IsUndecided(_session.Project, target, state.Label));
            Guide(sentence + string.Format(Ui.Side_ChooseConditionAtTopOf, to), sentence);
            return;
        }

        // ⛔ 「갈래 · 조건 갈래의 출구」 요약은 2026-11-19에 갈래 출구와 함께 걷혔다(usability UX28) — 실행 선은 분기뿐이다.
    }

    /// <summary>카드의 이름 — [대본] 탭에 보이는 그대로(에피소드는 제목, 비면 EpisodeId · 아니면 노드 이름).</summary>
    private string CardName(string nodeId) =>
        _session.Project.FindNode(nodeId) is { } node ? EpisodeNaming.ShownNameOf(_session.Project, node) : nodeId;

    /// <summary>그 챕터 에피소드의 보이는 제목 — 없으면 Id 그대로.</summary>
    private static string TitleOf(ChapterGraphModel chapter, string episodeId) =>
        chapter.FindEpisode(episodeId)?.ShownTitle ?? episodeId;

    /// <summary>선 설명의 한 줄 — 「이름: 값」.</summary>
    private void Fact(string name, string value) => _sheet.Children.Add(new TextBlock
    {
        Text = $"{name}: {value}",
        FontSize = Skin.Font(12),
        TextWrapping = TextWrapping.Wrap,
        Tag = "summary-fact"
    });

    /// <summary>「[구성] 탭에서 …」 단추 — 그 탭의 같은 것으로 데려간다.</summary>
    private void ChapterTabButton(string text, Action open)
    {
        var button = new Button
        {
            Content = text,
            FontSize = Skin.Font(11),
            Margin = new Thickness(0, 8, 0, 0),
            Tag = "summary-open"
        };
        button.Click += (_, _) => UiGuard.Run(_session, Ui.Side_OpenInStructureTab, open);
        _sheet.Children.Add(button);
    }
}
