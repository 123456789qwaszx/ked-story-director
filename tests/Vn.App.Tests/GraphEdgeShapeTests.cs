using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>선 모양을 손으로 고친다</b> (G3 · 2026-10-29 소유자: *"선이 생기는 모양새가 종종 이상할 때가 있는데, 원한다면
/// 저것도 곡선이나 그런 걸 조절할 수 있도록 해줘. 피그잼처럼"*).
///
/// 선을 누르면 손잡이와 꼴 도구줄([직각] [곡선] [직선] [자동])이 선다. 손잡이를 끄는 동안은 화면만 굽고, 놓을 때
/// 한 번 적는다(되돌리기 한 칸). 진짜 마우스로 잰다.
/// </summary>
public sealed class GraphEdgeShapeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-edge-shape", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 레인을_누르고_손잡이를_끌면_놓을_때_한_번_굽는다() => HeadlessUi.Run(() =>
    {
        (Window window, GraphEditorView graph, AuthoringSession session) = Show();
        Canvas canvas = Canvas(graph);
        int changes = 0;
        session.Changed += (_, _) => changes++;

        // 선을 누르기 전에는 손잡이가 없다.
        Assert.False(Handle(graph)?.IsVisible ?? false);

        Click(window, canvas, LanePoint(graph));

        Ellipse handle = Handle(graph)!;
        Assert.True(handle.IsVisible);
        Assert.True(Bar(graph).IsVisible);

        // 끄는 동안은 화면만 굽는다.
        Point grip = Center(window, handle);
        window.MouseDown(grip, MouseButton.Left);
        window.MouseMove(grip + new Point(60, -40), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Edge(session).Shape);
        Assert.Equal(0, changes);
        Assert.Equal(grip + new Point(60, -40), Center(window, handle));

        window.MouseUp(grip + new Point(60, -40), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // 놓으면 한 번 적고, 손잡이는 놓은 자리에 남는다.
        EdgeShape shape = Edge(session).Shape!;
        Assert.Equal(EdgeShapeKind.Elbow, shape.Kind);
        Assert.NotEqual(0, shape.BendX);
        Assert.NotEqual(0, shape.BendY);
        Assert.Equal(1, changes);
        Assert.Equal(grip + new Point(60, -40), Center(window, handle));

        // 빈 곳을 누르면 손잡이와 도구줄이 내린다.
        Click(window, canvas, new Point(900, 40));
        Assert.False(handle.IsVisible);
        Assert.False(Bar(graph).IsVisible);

        // 되돌리기 한 칸이면 자동 모양이다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(Edge(session).Shape);

        window.Close();
    });

    [Fact]
    public void 도구줄로_꼴을_고르고_자동으로_되돌린다() => HeadlessUi.Run(() =>
    {
        (Window window, GraphEditorView graph, AuthoringSession session) = Show();
        Click(window, Canvas(graph), LanePoint(graph));
        Avalonia.Media.Geometry elbow = Lane(graph).Data!;

        Press(window, BarButton(graph, "곡선"));
        Assert.Equal(new EdgeShape(EdgeShapeKind.Curve), Edge(session).Shape);

        // 곡선은 손잡이를 지난다 — 꺾은선이었으면 그 자리(굽이의 한가운데)를 지나지 않는다.
        Ellipse handle = Handle(graph)!;
        var middle = new Point(
            Avalonia.Controls.Canvas.GetLeft(handle) + 6,
            Avalonia.Controls.Canvas.GetTop(handle) + 6);
        var pen = new Avalonia.Media.Pen(Avalonia.Media.Brushes.Black, 2);
        Assert.True(Lane(graph).Data!.StrokeContains(pen, middle));
        Assert.False(elbow.StrokeContains(pen, middle));

        Press(window, BarButton(graph, "직선"));
        Assert.Equal(new EdgeShape(EdgeShapeKind.Straight), Edge(session).Shape);
        Assert.False(Handle(graph)!.IsVisible); // 직선에는 끌 굽이가 없다.

        Press(window, BarButton(graph, "자동"));
        Assert.Null(Edge(session).Shape);
        Assert.True(Handle(graph)!.IsVisible);

        window.Close();
    });

    [Fact]
    public void 분기_선도_같은_손잡이로_옆으로만_민다() => HeadlessUi.Run(() =>
    {
        (Window window, GraphEditorView graph, AuthoringSession session) = Show();
        DialogueNode a = EpisodeNaming.CardFor(session.Project, "ch01", "a")!;
        string lineId = a.Script.ActiveLines.First().Id;
        DialogueNode branch = session.Editor.AddBranchMarker(a.Id, lineId);
        session.Editor.MoveNode(branch.Id, 600, 120);
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = Canvas(graph);
        Border label = canvas.Children.OfType<Border>()
            .Single(border => border.IsVisible && border.Child is TextBlock { Text: "분기" });
        Click(window, canvas, new Point(
            Avalonia.Controls.Canvas.GetLeft(label) + 4,
            Avalonia.Controls.Canvas.GetTop(label) + 4));

        Point grip = Center(window, Handle(graph)!);
        window.MouseDown(grip, MouseButton.Left);
        window.MouseMove(grip + new Point(50, 80), RawInputModifiers.LeftMouseButton);
        window.MouseUp(grip + new Point(50, 80), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // 실행 선의 직각은 가운데 세로 마디를 옆으로만 민다 — 위아래로 끈 몫은 버린다.
        EdgeShape shape = ((DialogueNode)session.Project.FindNode(a.Id)!).EdgeShapes[$"detour:{lineId}"];
        Assert.NotEqual(0, shape.BendX);
        Assert.Equal(0, shape.BendY);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static Canvas Canvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static ChapterEdge Edge(AuthoringSession session) =>
        Assert.Single(session.Editor.FindChapter("ch01")!.Edges);

    private static Ellipse? Handle(GraphEditorView graph) =>
        Canvas(graph).Children.OfType<Ellipse>()
            .SingleOrDefault(ellipse => ToolTip.GetTip(ellipse) as string == "끌어서 선을 굽힙니다.");

    private static Border Bar(GraphEditorView graph) =>
        Canvas(graph).Children.OfType<Border>().Single(border => border.Child is StackPanel panel &&
            panel.Children.OfType<Button>().Any(button => (button.Content as string)?.EndsWith("자동") == true));

    private static Button BarButton(GraphEditorView graph, string name) =>
        ((StackPanel)Bar(graph).Child!).Children.OfType<Button>()
            .Single(button => (button.Content as string)?.EndsWith(name, StringComparison.Ordinal) == true);

    private static Avalonia.Controls.Shapes.Path Lane(GraphEditorView graph) =>
        Canvas(graph).Children.OfType<Avalonia.Controls.Shapes.Path>()
            .Single(path => path.Tag as string == "레인:a>b");

    /// <summary>레인 위의 한 점 — 내려가는 세로 줄(레인에서 가장 오른쪽)의 가운데.</summary>
    private static Point LanePoint(GraphEditorView graph)
    {
        Rect bounds = Lane(graph).Data!.Bounds;
        return new Point(bounds.Right, bounds.Y + (bounds.Height / 2));
    }

    private static Point Center(Window window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, Canvas canvas, Point onCanvas)
    {
        Point spot = canvas.TranslatePoint(onCanvas, window)!.Value;
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, Button button)
    {
        Point spot = Center(window, button);
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// ch01 판 — a(위)에서 b(아래)로 「간다」 한 길. 셸처럼 변경마다 판을 다시 그린다
    /// (<see cref="ProjectRefreshPlanner"/> — 자리만 바뀐 변경은 선만 다시 긋는다).
    /// </summary>
    private (Window Window, GraphEditorView Graph, AuthoringSession Session) Show()
    {
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "선 모양" });

        var session = new AuthoringSession();
        session.Open(manifest);
        session.Editor.EnsureChapter("ch01");
        string fileId = session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "a", "a", 0, 0);
        session.Editor.AddEpisode("ch01", "b", "b", 0, 0);
        session.Editor.MoveNode(EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id, 100, 40);
        session.Editor.MoveNode(EpisodeNaming.CardFor(session.Project, "ch01", "b")!.Id, 100, 520);
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "간다");
        session.SetFileExpanded(fileId, expanded: true);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();

        session.Changed += (_, change) =>
        {
            ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(change.Kind, null);

            if (plan.RebuildGraph)
            {
                graph.Rebuild();
            }
            else if (plan.RefreshGraphPositions)
            {
                graph.RefreshPositions();
            }
        };

        Dispatcher.UIThread.RunJobs();
        return (window, graph, session);
    }
}
