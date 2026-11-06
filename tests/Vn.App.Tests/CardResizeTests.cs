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
/// <b>[대본] 판의 카드 폭을 끌어 바꾼다</b> — 카드 안 글 칸이 좁아 보기 불편할 때 오른쪽 가장자리를 좌우로 당긴다.
///
/// 끄는 동안은 화면만 넓히고 놓을 때 한 번 적는다(되돌리기 한 칸). 기본 폭보다 좁히지 않고, 두 번 누르면 기본 폭으로
/// 돌아온다. 선택지 줄기(카드 가운데 아래)와 레인도 넓힌 폭을 따른다.
/// </summary>
public sealed class CardResizeTests : IDisposable
{
    private const double DefaultWidth = 210;

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

        DragGrip(graph, root, by: 150);

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
        DragGrip(graph, root, by: -80);
        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);

        DragGrip(graph, root, by: 200);
        Assert.Equal(DefaultWidth + 200, Card(graph, root).Width);

        Press(Grip(graph, root), clickCount: 2);

        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);
    });

    [Fact]
    public void 넓힌_폭은_저장하고_다시_열어도_그대로다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");

        DragGrip(graph, root, by: 120);
        session.Save(session.ProjectPath!);

        var again = new AuthoringSession();
        again.Open(session.ProjectPath!);

        Assert.Equal(DefaultWidth + 120, again.Project.FindNode(root)!.Layout.Width);
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

    private static Border Grip(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.ResizeGripTag + nodeId);

    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    /// <summary>그 에피소드의 첫 선택지 칸 줄의 왼쪽 — 줄기(카드 가운데)에서 오른쪽으로 선다.</summary>
    private static double SlotLeft(GraphEditorView graph, string episodeId) =>
        Canvas.GetLeft(BoardCanvas(graph).Children.OfType<StackPanel>()
            .First(row => row.Tag is string tag && tag.StartsWith(episodeId + "#", StringComparison.Ordinal)));

    /// <summary>손잡이를 눌러 <paramref name="by"/>만큼 옆으로 끌고 놓는다 — 사람이 하는 길 그대로다.</summary>
    private static void DragGrip(GraphEditorView graph, string nodeId, double by)
    {
        Canvas canvas = BoardCanvas(graph);
        Border card = Card(graph, nodeId);
        var start = new Point(Canvas.GetLeft(card) + card.Width - 3, Canvas.GetTop(card) + 30);

        Press(Grip(graph, nodeId), At(canvas, start));
        Move(canvas, At(canvas, start + new Point(by, 0)));
        Release(canvas, At(canvas, start + new Point(by, 0)));
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
