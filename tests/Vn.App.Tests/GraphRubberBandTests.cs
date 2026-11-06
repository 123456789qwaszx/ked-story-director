using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 범위 선택(러버밴드)은 카드와 선을 함께 잡고, 인스펙터가 따라온다</b> — 사각형에 걸친 카드 · 선택지 레인 ·
/// 분기 선이 다 무리가 되고, 그 가운데 끌기 시작한 자리에서 가장 가까운 것을 고른다. 카드면 대사 편집기, 선이면 그 선의 설명
/// (선택지: 문구 · 조건 · 스탯변화, 분기: 조건)이 인스펙터에 선다. 선을 바로 눌러도 같다.
/// </summary>
public sealed class GraphRubberBandTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-rubber-band", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 시작점에_선택지_레인이_가까우면_레인을_고르고_인스펙터가_그_선택지를_설명한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        session.Select(CardId(session, "a"));
        Dispatcher.UIThread.RunJobs();

        // 레인(x=222, root 칩 → b 앞 접점) 오른쪽 18px에서 b 카드 안까지 — 레인이 가깝고 b 카드도 걸친다.
        Band(graph, new Point(240, 700), new Point(100, 850));

        Assert.Null(session.SelectedNodeId);
        Assert.Equal("선택지 · root → b", Title(graph));
        Assert.Contains("문구: 「교무실로 간다」", Facts(graph));
        Assert.Contains("해금조건: 용기 — 아니면 잠긴 채 보입니다", Facts(graph));
        Assert.Contains("스탯변화: 신뢰 +2", Facts(graph));
        Assert.False(Editor(window).IsVisible);

        // 고른 레인이 가장 굵고, 함께 걸친 b 카드는 무리로 칠해진다.
        Assert.Equal(2.4, Lane(graph).StrokeThickness);
        Assert.Equal(2, Card(graph, CardId(session, "b")).BorderThickness.Left);
        Assert.Equal(1, Card(graph, CardId(session, "a")).BorderThickness.Left);

        // 고치는 곳은 [구성] 탭 — 단추가 그 선택지를 고른 채로 데려간다.
        Summary(graph, "summary-open").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(0, window.FindControl<TabControl>("MainTabs")!.SelectedIndex);
        Assert.True(chapterGraph.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        window.Close();
    });

    [Fact]
    public void 시작점에_카드가_가까우면_카드를_고르고_걸친_레인은_무리로_칠해진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();

        // a 카드 바로 아래에서 레인 너머까지 — a 카드(7px)가 레인(160px 남짓)보다 가깝다.
        Band(graph, new Point(60, 545), new Point(240, 520));

        Assert.Equal(CardId(session, "a"), session.SelectedNodeId);
        Assert.True(Editor(window).IsVisible);
        Assert.Equal("에피소드 · a", Title(graph));
        Assert.Equal(2, Lane(graph).StrokeThickness);

        // 빈 판을 다시 누르면 무리가 풀린다.
        Press(Canvas(graph), At(graph, new Point(400, 300)));
        Release(Canvas(graph), At(graph, new Point(400, 300)));
        Assert.Equal(1.4, Lane(graph).StrokeThickness);

        window.Close();
    });

    [Fact]
    public void 레인을_바로_눌러도_인스펙터가_바뀌고_카드를_고르면_대사_편집기로_돌아온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        session.Select(CardId(session, "root"));
        Dispatcher.UIThread.RunJobs();

        Press(LaneHit(graph), At(graph, new Point(222, 400)));

        Assert.Null(session.SelectedNodeId);
        Assert.Equal("선택지 · root → b", Title(graph));

        // 설명의 「도착」 고리 — 그 카드를 고르고 대사 편집기로 바뀐다. 고른 레인은 놓인다.
        Summary(graph, "summary-node:" + CardId(session, "b")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CardId(session, "b"), session.SelectedNodeId);
        Assert.True(Editor(window).IsVisible);
        Assert.Equal("에피소드 · b", Title(graph));
        Assert.Equal(1.4, Lane(graph).StrokeThickness);

        // 다시 레인을 고른 뒤 빈 판을 누르면 설명이 내려간다.
        Press(LaneHit(graph), At(graph, new Point(222, 400)));
        Assert.Equal("선택지 · root → b", Title(graph));
        Press(Canvas(graph), At(graph, new Point(400, 300)));
        Release(Canvas(graph), At(graph, new Point(400, 300)));

        Assert.Equal("인스펙터", Title(graph));
        Assert.Equal(1.4, Lane(graph).StrokeThickness);

        window.Close();
    });

    [Fact]
    public void 범위로_분기_선을_잡으면_인스펙터가_그_분기의_조건을_보이고_구성_탭의_그_분기로_데려간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        string root = CardId(session, "root");
        DialogueNode node = (DialogueNode)session.Project.FindNode(root)!;
        DialogueNode detour = session.Editor.AddBranchMarker(root, node.Script.ActiveLines.First().Id);
        Dispatcher.UIThread.RunJobs();

        // 분기 선의 한가운데를 작게 감싼다.
        ShapePath line = Canvas(graph).Children.OfType<ShapePath>()
            .Single(path => path.Tag is null && path.IsVisible && path.Data is not null && path.StrokeThickness >= 2);
        line.Data!.TryGetPointAtDistance(line.Data.ContourLength / 2, out Point middle);
        Band(graph, middle + new Point(6, 6), middle - new Point(6, 6));

        Assert.Equal(4, line.StrokeThickness);
        Assert.StartsWith("분기 · root → ", Title(graph), StringComparison.Ordinal);
        Assert.Contains("조건: 없음", Facts(graph));

        Summary(graph, "summary-open").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(0, window.FindControl<TabControl>("MainTabs")!.SelectedIndex);
        Assert.True(chapterGraph.FindControl<StackPanel>("BranchPanel")!.IsVisible);
        Assert.NotNull(session.Project.FindNode(detour.Id));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// [대본] 탭 — ch01에 선택지 하나(root → b, 해금조건 · 스탯변화)를 놓는다. ch02 판은 접는다: 두 판이 같은 자리에 펼쳐지면 카드가
    /// 겹친다. 카드는 위에서 아래로 root(0,0) · a(0,400) · b(0,800)이고, 레인은 root 칩에서 x=222를 타고 내려가 b 앞 접점으로 든다.
    /// </summary>
    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        session.SetFileExpanded(EpisodeNaming.BoardOf(session.Project, "ch02")!.Id, expanded: false);
        session.Editor.AddChapterStat("ch01", "trust", "신뢰");
        session.Editor.AddEdge("ch01", "root", "b", conditionLabel: "용기", optionLabel: "교무실로 간다", statChanges: "trust +2");
        Dispatcher.UIThread.RunJobs();

        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Canvas Canvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static Border Card(GraphEditorView graph, string nodeId) =>
        Canvas(graph).Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    /// <summary>보이는 레인 — 고른 것 · 무리 · 보통이 굵기로 갈린다.</summary>
    private static ShapePath Lane(GraphEditorView graph) =>
        Canvas(graph).Children.OfType<ShapePath>().Single(path => path.Tag as string == "레인:root>b");

    /// <summary>레인을 누르는 자리 — 넓고 투명한 같은 선이다.</summary>
    private static ShapePath LaneHit(GraphEditorView graph) =>
        Canvas(graph).Children.OfType<ShapePath>().Single(path => path.Tag?.GetType().Name == "RailKey");

    private static string? Title(GraphEditorView graph) => graph.FindControl<TextBlock>("InspectorTitle")!.Text;

    private static List<string?> Facts(GraphEditorView graph) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && text.Tag as string == "summary-fact")
            .Select(text => text.Text)
            .ToList();

    private static Button Summary(GraphEditorView graph, string tag) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && (string?)button.Tag == tag);

    private static DialogueNodeEditor Editor(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();

    /// <summary>빈 판을 눌러 끌고 놓는다 — 사람이 범위를 긋는 길 그대로다.</summary>
    private static void Band(GraphEditorView graph, Point from, Point to)
    {
        Press(Canvas(graph), At(graph, from));
        Move(Canvas(graph), At(graph, to));
        Release(Canvas(graph), At(graph, to));
    }

    /// <summary>판 좌표를 최상위 좌표로 — 포인터 이벤트의 자리는 최상위 기준으로 풀린다.</summary>
    private static Point At(GraphEditorView graph, Point onCanvas) =>
        Canvas(graph).TranslatePoint(onCanvas, (Visual)TopLevel.GetTopLevel(graph)!)!.Value;

    private static void Press(Control control, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Move(Canvas canvas, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, canvas, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Canvas canvas, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }
}
