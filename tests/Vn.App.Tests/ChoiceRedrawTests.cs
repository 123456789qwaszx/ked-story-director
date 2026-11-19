using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>선택지의 내용 · 챕터 조건을 고치면 카드는 그대로 두고 선 · 조건 표만 다시 그린다</b> (2026-11-19 소유자 — 속도 개선 ④ · usability UX16).
/// ⛔ 판을 통째로 다시 지었다 — 에피소드 160개에서 선택지 문구 하나 · 조건 하나에 2.5초, 판의 선택지 칸은 열 때와 닫을 때마다 2.5초.
/// </summary>
public sealed class ChoiceRedrawTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-choice-redraw", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 선택지_문구를_바꾸면_카드는_그대로_두고_그_출발의_칸과_도착_목록만_고친다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "기다린다");
        Dispatcher.UIThread.RunJobs();
        Border card = CardVisual(graph, Card(session, "a"));
        int rails = graph.RailDrawCount;
        int choices = graph.ChoiceRedrawCount;

        session.Editor.UpdateEdge("ch01", "root", "a", matchOptionLabel: "간다", optionLabel: "달린다");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(choices + 1, graph.ChoiceRedrawCount);
        Assert.Equal(rails, graph.RailDrawCount);                       // 판도 철도도 통째로 긋지 않았다
        Assert.Same(card, CardVisual(graph, Card(session, "a")));       // 카드는 그 카드 그대로다
        Assert.Equal(["달린다", "기다린다"], SlotTexts(graph, "root").Take(2));
        Assert.Equal([("root", "달린다")], graph.ArrivalsInto("a"));    // 도착 포트를 누르면 보이는 목록도 새 문구다
        Assert.Equal([("root", "기다린다")], graph.ArrivalsInto("b"));

        // 통째로 다시 지은 모습과 같다.
        List<string> slots = SlotTexts(graph, "root");
        int visuals = graph.RailVisuals.Count;
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(slots, SlotTexts(graph, "root"));
        Assert.Equal([("root", "달린다")], graph.ArrivalsInto("a"));
        Assert.Equal(visuals, graph.RailVisuals.Count);

        window.Close();
    });

    [Fact]
    public void 스탯변화_잠금_안내와_자동_길도_카드를_다시_짓지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();
        Border card = CardVisual(graph, Card(session, "root"));
        int rails = graph.RailDrawCount;
        int choices = graph.ChoiceRedrawCount;

        session.Editor.UpdateEdge("ch01", "root", "a", matchOptionLabel: "간다", statChanges: "trust +2");
        session.Editor.UpdateEdge("ch01", "root", "a", matchOptionLabel: "간다", lockedMessage: "아직은 못 간다");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(choices + 2, graph.ChoiceRedrawCount);

        // 하나뿐인 선택지의 문구를 지우면 자동 길이다 — 칸이 「자동」이 되고 도착 목록은 「(진행)」이다.
        session.Editor.UpdateEdge("ch01", "root", "a", matchOptionLabel: "간다", optionLabel: string.Empty, auto: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(choices + 3, graph.ChoiceRedrawCount);
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Same(card, CardVisual(graph, Card(session, "root")));
        Assert.Equal("자동", SlotTexts(graph, "root")[0]);
        Assert.Equal([("root", "(진행)")], graph.ArrivalsInto("a"));

        // 되돌리기도 그 출발의 선만 다시 긋는다(usability UX21 · ⛔ 판을 통째로 지었다).
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(choices + 4, graph.ChoiceRedrawCount);
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Same(card, CardVisual(graph, Card(session, "root")));
        Assert.Equal("간다", SlotTexts(graph, "root")[0]);
        Assert.Equal([("root", "간다")], graph.ArrivalsInto("a"));

        window.Close();
    });

    [Fact]
    public void 판의_선택지_칸을_열고_적고_닫아도_판을_통째로_짓지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();
        Border card = CardVisual(graph, Card(session, "root"));
        int rails = graph.RailDrawCount;

        Press(Face(graph, "root", 0));
        Assert.NotNull(graph.SlotLabelBox);
        Assert.Equal("간다", graph.SlotLabelBox!.Text);

        graph.SlotLabelBox.Text = "달린다";
        Key(graph.SlotLabelBox, Avalonia.Input.Key.Enter);

        Assert.Null(graph.SlotLabelBox);
        Assert.Equal("달린다", Edge(session, "a").OptionLabel);
        Assert.Equal("달린다", SlotTexts(graph, "root")[0]);
        Assert.Equal([("root", "달린다")], graph.ArrivalsInto("a"));
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Same(card, CardVisual(graph, Card(session, "root")));

        window.Close();
    });

    [Fact]
    public void 칸이_열린_채로_다른_칸을_누르면_앞_칸은_Enter처럼_넣는다() => HeadlessUi.Run(() =>
    {
        // 딴 데를 누르면 Enter처럼 넣는다 — 다른 선택지 칸을 누른 것도 같다. ⛔ 2026-11-19까지는 적던 글이 버려졌다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();

        Press(Face(graph, "root", 0));
        graph.SlotLabelBox!.Text = "달린다";
        Press(Face(graph, "root", 1));   // 빈 칸의 네모

        Assert.Equal("달린다", Edge(session, "a").OptionLabel);
        Assert.NotNull(graph.SlotLabelBox);
        Assert.Equal(string.Empty, graph.SlotLabelBox!.Text);   // 이제 둘째 칸이 열렸다
        Assert.Single(graph.FindControl<Canvas>("GraphCanvas")!.GetVisualDescendants().OfType<TextBox>(),
            box => (box.Parent as StackPanel)?.Tag is string tag && tag.StartsWith("root#", StringComparison.Ordinal));

        window.Close();
    });

    [Fact]
    public void 선택지만_바뀐_판인지_가린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView _) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        GraphProjection Now() => GraphProjectionBuilder.Build(session.Project, session.ExpandedFileIds, session.CollapsedScenes);

        GraphProjection before = Now();
        session.Editor.UpdateEdge("ch01", "root", "a", matchOptionLabel: "간다", optionLabel: "달린다");
        Assert.True(GraphEditorView.OnlyChoicesChanged(before, Now()));

        before = Now();
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "기다린다");
        Assert.True(GraphEditorView.OnlyChoicesChanged(before, Now()));      // 빈 칸에 길을 이었다 — 칸은 그대로 셋이다(UX19)

        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "또 간다");
        before = Now();
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "다시 기다린다");
        Assert.False(GraphEditorView.OnlyChoicesChanged(before, Now()));     // 넷째 길 — 칸이 하나 늘었다

        before = Now();
        session.Editor.MoveNode(Card(session, "a"), 900, 900);
        Assert.False(GraphEditorView.OnlyChoicesChanged(before, Now()));     // 카드가 움직였다

        window.Close();
    });

    [Fact]
    public void 챕터_조건을_더하고_식을_채우면_분기_카드의_조건_표만_다시_단다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        string root = Card(session, "root");
        session.Editor.ApplyScenarioText(root, "라루: 들렀다 가자.");
        string lineId = session.Project.FindDialogue(root)!.Script.ActiveLines.Single().Id;
        session.Editor.AddBranchMarker(root, lineId);
        string branch = session.Project.FindDialogue(root)!.FindExtension(lineId)!.DetourTargetNodeIds.Single();
        session.Editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        session.Editor.AddChapterCondition("ch01", "아직모름", string.Empty);
        Assert.Null(BranchEntryCondition.Set(session.Editor, branch, "아직모름"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("⑂ 조건: 아직모름 (식 미정)", ChipText(graph, branch));

        Border card = CardVisual(graph, branch);
        int rails = graph.RailDrawCount;
        int refreshed = graph.BranchConditionRefreshCount;

        session.Editor.UpdateChapterCondition("ch01", "아직모름", "trust >= 7");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(refreshed + 1, graph.BranchConditionRefreshCount);
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Same(card, CardVisual(graph, branch));
        Assert.Equal("⑂ 조건: 아직모름", ChipText(graph, branch));

        session.Editor.AddChapterCondition("ch01", "믿음", "trust >= 3");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(refreshed + 2, graph.BranchConditionRefreshCount);
        Assert.Equal(rails, graph.RailDrawCount);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Open()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    private static string Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Border CardVisual(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static ChapterEdge Edge(AuthoringSession session, string to) =>
        session.Editor.FindChapter("ch01")!.ToGraphModel("ch01").Edges
            .Single(edge => edge.FromEpisodeId == "root" && edge.ToEpisodeId == to);

    /// <summary>그 에피소드의 선택지 칸들 — 칸 번호 차례대로(줄은 Tag에 <c>{에피소드}#{칸}</c>을 진다).</summary>
    private static List<StackPanel> Rows(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true)
            .OrderBy(row => (row.Tag as string)![(episodeId.Length + 1)..], StringComparer.Ordinal)
            .ToList();

    /// <summary>칸의 얼굴 — 문구거나 빈 칸의 네모.</summary>
    private static Control Face(GraphEditorView graph, string episodeId, int slot) =>
        Rows(graph, episodeId)[slot].Children.First(child => child is TextBlock || child.Tag as string == GraphEditorView.SlotPlaceholderTag);

    /// <summary>칸마다 문구(빈 칸은 「빈 선택지」, 열린 칸은 그 글).</summary>
    private static List<string> SlotTexts(GraphEditorView graph, string episodeId) =>
        Rows(graph, episodeId)
            .Select(row => row.Children.Select(child => child switch
            {
                TextBlock text => text.Text,
                Border { Child: TextBlock text } => text.Text,
                TextBox box => box.Text,
                _ => null
            }).First(text => text is not null) ?? string.Empty)
            .ToList();

    private static string? ChipText(GraphEditorView graph, string nodeId) =>
        ((TextBlock)graph.FindControl<Canvas>("GraphCanvas")!.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.BranchConditionChipTag + nodeId).Child!).Text;

    private static void Press(Control control)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;

        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(Control control, Avalonia.Input.Key key)
    {
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Dispatcher.UIThread.RunJobs();
    }
}
