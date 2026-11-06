using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 카드 크기를 끌어 바꾼다</b> — 카드 안 글 칸이 작아 보기 불편할 때 오른쪽 가장자리를 좌우로(폭), 아래 가장자리를
/// 위아래로(글 칸 높이), 오른쪽 아래 모서리를 대각으로(둘 다) 당긴다.
///
/// 끄는 동안은 화면만 키우고 놓을 때 한 번 적는다(되돌리기 한 칸). 기본보다 작게 줄이지 않고, 두 번 누르면 그 손잡이가 맡은
/// 크기가 기본으로 돌아온다. 선택지 줄기(카드 가운데 아래)와 레인도 키운 크기를 따른다.
/// </summary>
public sealed class CardResizeTests : IDisposable
{
    private const double DefaultWidth = 210;
    private const double DefaultHeight = 88;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-resize", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 오른쪽_가장자리를_끌면_카드와_글_칸이_넓어지고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        double boxBefore = WritingBox(graph, root).Bounds.Width;
        double trunkBefore = SlotLeft(graph, "root");

        DragGrip(graph, root, GraphEditorView.ResizeGripTag, dx: 150, dy: 0);

        Assert.Equal(DefaultWidth + 150, session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultWidth + 150, Card(graph, root).Width);
        Assert.Equal(boxBefore + 150, WritingBox(graph, root).Bounds.Width, precision: 0);

        // 선택지 줄기는 카드 가운데 아래다 — 넓힌 만큼의 절반을 따라간다.
        Assert.Equal(trunkBefore + 75, SlotLeft(graph, "root"), precision: 0);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);
    });

    [Fact]
    public void 기본_폭보다_좁히지_않고_두_번_누르면_기본_폭으로_돌아온다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");

        // 왼쪽으로 끌어도 기본 폭에서 멈춘다 — 적을 것이 없다.
        DragGrip(graph, root, GraphEditorView.ResizeGripTag, dx: -80, dy: 0);
        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);

        DragGrip(graph, root, GraphEditorView.ResizeGripTag, dx: 200, dy: 0);
        Assert.Equal(DefaultWidth + 200, Card(graph, root).Width);

        Press(Grip(graph, root, GraphEditorView.ResizeGripTag), clickCount: 2);

        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);
    });

    [Fact]
    public void 넓힌_폭은_저장하고_다시_열어도_그대로다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");

        DragGrip(graph, root, GraphEditorView.ResizeCornerGripTag, dx: 120, dy: 90);
        session.Save(session.ProjectPath!);

        var again = new AuthoringSession();
        again.Open(session.ProjectPath!);

        Assert.Equal(DefaultWidth + 120, again.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultHeight + 90, again.Project.FindNode(root)!.Layout.WritingHeight);
    });

    [Fact]
    public void 아래_가장자리를_끌면_글_칸이_늘고_선택지_칸이_따라_내려간다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        double slotBefore = SlotTop(graph, "root");

        DragGrip(graph, root, GraphEditorView.ResizeHeightGripTag, dx: 0, dy: 120);

        Assert.Equal(DefaultHeight + 120, session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Null(session.Project.FindNode(root)!.Layout.Width);   // 폭은 그대로다
        Assert.Equal(DefaultHeight + 120, WritingBox(graph, root).Height);

        // 손잡이를 누르며 카드가 골라져 테두리가 1px → 2px로 굵어졌다 — 그 차이만큼은 봐준다.
        Assert.InRange(SlotTop(graph, "root"), slotBefore + 117, slotBefore + 123);

        // 되돌리기 — 끌기 없이 크기만 돌아와도 판이 옛 높이에 다시 선다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal(DefaultHeight, WritingBox(graph, root).Height);

        Assert.InRange(SlotTop(graph, "root"), slotBefore - 3, slotBefore + 3);
    });

    [Fact]
    public void 모서리는_폭과_높이를_함께_바꾸고_두_번_누르면_둘_다_기본이며_가장자리는_제_것만_돌린다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");

        DragGrip(graph, root, GraphEditorView.ResizeCornerGripTag, dx: 80, dy: 60);

        Assert.Equal(DefaultWidth + 80, Card(graph, root).Width);
        Assert.Equal(DefaultHeight + 60, WritingBox(graph, root).Height);

        // 아래 가장자리를 두 번 — 높이만 기본으로, 폭은 남는다.
        Press(Grip(graph, root, GraphEditorView.ResizeHeightGripTag), clickCount: 2);

        Assert.Equal(DefaultWidth + 80, session.Project.FindNode(root)!.Layout.Width);
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal(DefaultHeight, WritingBox(graph, root).Height);

        // 다시 늘리고 모서리를 두 번 — 둘 다 기본이다.
        DragGrip(graph, root, GraphEditorView.ResizeHeightGripTag, dx: 0, dy: 50);
        Press(Grip(graph, root, GraphEditorView.ResizeCornerGripTag), clickCount: 2);

        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);
        Assert.Equal(DefaultHeight, WritingBox(graph, root).Height);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>진짜 창의 [대본] 탭 — 셸이 편집을 받아 판을 고치는 길까지 함께 돈다(<see cref="HierarchyFixture"/>).</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        return (window.FindControl<GraphEditorView>("Graph")!, session);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Canvas BoardCanvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static Border Card(GraphEditorView graph, string nodeId) =>
        BoardCanvas(graph).Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static Border Grip(GraphEditorView graph, string nodeId, string tag) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Border>()
            .Single(border => border.Tag as string == tag + nodeId);

    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    /// <summary>그 에피소드의 첫 선택지 칸 줄의 왼쪽 — 줄기(카드 가운데)에서 오른쪽으로 선다.</summary>
    private static double SlotLeft(GraphEditorView graph, string episodeId) => Canvas.GetLeft(FirstSlot(graph, episodeId));

    /// <summary>그 에피소드의 첫 선택지 칸 줄의 위 — 카드 아래 끝에서 내려간다.</summary>
    private static double SlotTop(GraphEditorView graph, string episodeId) => Canvas.GetTop(FirstSlot(graph, episodeId));

    private static StackPanel FirstSlot(GraphEditorView graph, string episodeId) =>
        BoardCanvas(graph).Children.OfType<StackPanel>()
            .First(row => row.Tag is string tag && tag.StartsWith(episodeId + "#", StringComparison.Ordinal));

    /// <summary>그 손잡이를 눌러 (<paramref name="dx"/>, <paramref name="dy"/>)만큼 끌고 놓는다 — 사람이 하는 길 그대로다.</summary>
    private static void DragGrip(GraphEditorView graph, string nodeId, string tag, double dx, double dy)
    {
        Canvas canvas = BoardCanvas(graph);
        Border card = Card(graph, nodeId);
        var start = new Point(Canvas.GetLeft(card) + card.Width - 3, Canvas.GetTop(card) + card.Bounds.Height - 3);
        var end = start + new Point(dx, dy);

        Press(Grip(graph, nodeId, tag), At(canvas, start));
        Move(canvas, At(canvas, end));
        Release(canvas, At(canvas, end));
    }

    /// <summary>판 좌표를 최상위 좌표로 — 포인터 이벤트의 자리는 최상위 기준으로 풀린다.</summary>
    private static Point At(Canvas canvas, Point onCanvas) =>
        canvas.TranslatePoint(onCanvas, (Visual)TopLevel.GetTopLevel(canvas)!)!.Value;

    private static void Press(Control control, Point at = default, int clickCount = 1)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount));
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
