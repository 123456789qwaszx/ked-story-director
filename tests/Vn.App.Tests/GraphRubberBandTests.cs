using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 범위 선택(러버밴드)과 인스펙터</b> — 사각형에 카드가 걸리면 카드만 무리가 되고(함께 걸친 선은 고르지 않는다)
/// 끌기 시작한 자리에서 가장 가까운 카드의 대사 편집기가 인스펙터에 선다. 선만 걸리면 놓은 자리(포인터)에서 가장 가까운 선
/// 하나를 고르고, 인스펙터가 그 선을 설명한다(선택지: 문구 · 조건 · 스탯변화, 분기: 조건). 선을 바로 눌러도 같다.
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
    public void 선만_걸리면_놓은_자리에서_가장_가까운_선_하나만_고르고_인스펙터가_그_선택지를_설명한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "도서관으로 간다");
        Dispatcher.UIThread.RunJobs();
        session.Select(CardId(session, "a"));
        Dispatcher.UIThread.RunJobs();

        // 두 길의 내려가는 줄을 가로지른다(root 카드와 a 카드 사이의 띠 — 카드는 안 걸린다). root→b는 첫 칸 밑으로 곧게 내려가면 a 카드 뒤를 지나므로
        // 카드 오른쪽 바깥(x=222)으로 비켜 내려가고, root→a는 둘째 칸 밑에서 곧게 내려간다. 두 길 다 접점(카드 가운데)이 세로 줄보다 왼쪽이라 세로 줄이
        // 길에서 가장 오른쪽이다. 놓은 자리에 가까운 길을 고른다 — 시작한 자리에 가까운 길은 보지 않는다.
        double laneA = Lane(graph, "a").Data!.Bounds.Right;
        double laneB = Lane(graph, "b").Data!.Bounds.Right;
        var besideA = new Point(laneA < laneB ? laneA - 8 : laneA + 8, 345);
        var besideB = new Point(laneA < laneB ? laneB + 8 : laneB - 8, 360);
        Band(graph, besideB, besideA);

        Assert.Null(session.SelectedNodeId);
        Assert.Equal("선택지 · root → a", Title(graph));
        Assert.Equal(Picked(graph), Lane(graph, "a").StrokeThickness);
        Assert.Equal(Plain(graph), Lane(graph, "b").StrokeThickness);
        Assert.False(Editor(window).IsVisible);

        // 거꾸로 그으면 놓은 자리에 root→b가 가깝다.
        Band(graph, besideA, besideB);

        Assert.Equal("선택지 · root → b", Title(graph));
        Assert.Equal("교무실로 간다", graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<TextBox>()
            .First(box => box.IsEffectivelyVisible && box.Tag as string == "summary-label").Text);   // 문구는 고치는 칸에 선다
        Assert.Contains("해금조건: 용기 — 아니면 잠긴 채 보입니다", Facts(graph));
        Assert.Contains("스탯변화: trust +2", Facts(graph));
        Assert.Equal(Plain(graph), Lane(graph, "a").StrokeThickness);
        Assert.Equal(Picked(graph), Lane(graph, "b").StrokeThickness);

        // 고치는 곳은 [구성] 탭 — 단추가 그 선택지를 고른 채로 데려간다.
        Summary(graph, "summary-open").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(MainTab.Structure, window.FindControl<TabControl>("MainTabs")!.SelectedIndex);
        Assert.True(chapterGraph.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        window.Close();
    });

    [Fact]
    public void 카드와_선이_함께_걸리면_카드만_잡히고_시작점에서_가까운_카드가_인스펙터에_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();

        // b 카드 오른쪽 위 빈 곳에서 a 카드 아래쪽까지 — 두 카드와 레인이 함께 걸친다. 시작점에는 b가 가깝다.
        Band(graph, new Point(230, 810), new Point(100, 520));

        Assert.Equal(CardId(session, "b"), session.SelectedNodeId);
        Assert.True(Editor(window).IsVisible);
        Assert.Equal("ch01 - hall", Title(graph));
        Assert.Equal(2, Card(graph, CardId(session, "a")).BorderThickness.Left);
        Assert.Equal(2, Card(graph, CardId(session, "b")).BorderThickness.Left);
        Assert.Equal(1, Card(graph, CardId(session, "root")).BorderThickness.Left);

        // 함께 걸친 레인은 고르지도 칠하지도 않는다.
        Assert.Equal(Plain(graph), Lane(graph, "b").StrokeThickness);

        // 빈 판을 다시 누르면 무리가 풀린다.
        Press(Canvas(graph), At(graph, new Point(400, 300)));
        Release(Canvas(graph), At(graph, new Point(400, 300)));
        Assert.Equal(1, Card(graph, CardId(session, "a")).BorderThickness.Left);

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

        // 출발 · 도착 고리는 없다(2026-11-17 소유자 — 머리글이 말한다). 카드를 고르면 대사 편집기로 바뀌고 고른 레인은 놓인다.
        Assert.DoesNotContain(graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible && (button.Tag as string)?.StartsWith("summary-node:", StringComparison.Ordinal) == true);
        session.Select(CardId(session, "b"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CardId(session, "b"), session.SelectedNodeId);
        Assert.True(Editor(window).IsVisible);
        Assert.Equal("ch01 - hall", Title(graph));
        Assert.Equal(Plain(graph), Lane(graph, "b").StrokeThickness);

        // 다시 레인을 고른 뒤 빈 판을 누르면 설명이 내려간다 — 챕터 밖의 빈 판이라 프로젝트가 선다(2026-11-17 — 화자는 「화자」 탭 · side-tabs V3).
        Press(LaneHit(graph), At(graph, new Point(222, 400)));
        Assert.Equal("선택지 · root → b", Title(graph));
        Press(Canvas(graph), At(graph, new Point(3000, 3000)));
        Release(Canvas(graph), At(graph, new Point(3000, 3000)));

        Assert.Equal("프로젝트", Title(graph));
        Assert.Equal(Plain(graph), Lane(graph, "b").StrokeThickness);

        window.Close();
    });

    [Fact]
    public void 범위로_분기_선을_잡으면_인스펙터에서_그_분기의_조건을_바로_고른다() => HeadlessUi.Run(() =>
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

        // [구성] 탭으로 가는 단추 · 출발/다녀갈 곳 고리는 없다.
        List<Button> buttons = graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Tag is string tag &&
                             (tag == "summary-open" || tag.StartsWith("summary-node:", StringComparison.Ordinal)))
            .ToList();
        Assert.Empty(buttons);

        // 조건 칸도 없다 — 조건은 그 분기 카드를 골라 대본 위쪽에서 고른다(2026-11-17 소유자). 선 인스펙터는 그것을 말한다.
        Assert.DoesNotContain(graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<ComboBox>(),
            combo => combo.IsEffectivelyVisible);
        Assert.Contains(graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text?.Contains($"분기 카드 「{EpisodeNaming.BranchTitleOf(detour)}」를 고르면 대본 위쪽", StringComparison.Ordinal) == true);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// [대본] 탭 — ch01에 선택지 하나(root → b, 해금조건 · 스탯변화)를 놓는다. ch02 판은 접는다: 두 판이 같은 자리에 펼쳐지면 카드가
    /// 겹친다. 카드는 위에서 아래로 root(0,0) · a(0,400) · b(0,800)이고, 길은 root의 첫 칸 밑에서 내려와 사이의 a 카드를 비켜 x=222를 타고 b 앞 접점으로 든다.
    /// </summary>
    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        session.SetFileExpanded(EpisodeNaming.BoardOf(session.Project, "ch02")!.Id, expanded: false);
        session.Editor.AddChapterStat("ch01", "trust");
        session.Editor.AddEdge("ch01", "root", "b", conditionLabel: "용기", optionLabel: "교무실로 간다", statChanges: "trust +2");
        Dispatcher.UIThread.RunJobs();

        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    /// <summary>
    /// 고른 레인 · 그 밖 레인의 굵기 — 판의 굵기(2.4 · 1.4 × 테마의 선 굵기), 단 화면 바닥(design/board-zoom.md 5절)보다 가늘어지지 않는다. 이 판은 열 때 챕터에
    /// 맞춰 100%보다 작게 서므로(화면 맞춤) 고른 레인은 바닥 2px ÷ 배율로 굵어진다.
    /// </summary>
    private static double Picked(GraphEditorView graph) => Math.Max(2.4 * Skin.LineWeight, GraphEditorView.PickedLineFloorPx / graph.Zoom);

    private static double Plain(GraphEditorView graph) => Math.Max(1.4 * Skin.LineWeight, GraphEditorView.LineFloorPx / graph.Zoom);

    private static Canvas Canvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static Border Card(GraphEditorView graph, string nodeId) =>
        Canvas(graph).Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    /// <summary>root에서 그 에피소드로 가는 보이는 레인 — 고른 것만 굵다.</summary>
    private static ShapePath Lane(GraphEditorView graph, string to) =>
        Canvas(graph).Children.OfType<ShapePath>().Single(path => path.Tag as string == "레인:root>" + to);

    /// <summary>레인을 누르는 자리 — 넓고 투명한 같은 선이다.</summary>
    private static ShapePath LaneHit(GraphEditorView graph) =>
        Canvas(graph).Children.OfType<ShapePath>().Single(path => path.Tag?.GetType().Name == "RailKey");

    private static string? Title(GraphEditorView graph) => graph.InspectorHeaderText;

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
