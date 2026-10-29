using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>선택지 줄 하나하나가 제 도착을 보고 쪽을 고른다</b> (H1 · 2026-10-29 소유자: *"선택지는 원래 것보다 왼쪽에 두더라도,
/// 여전히 오른쪽으로 점과 글자가 유지 돼 … 연결되어 있는 하나의 루트의 선택지 방향이 바뀌길 원한거야"*).
///
/// 도착 카드가 출발 카드보다 왼쪽이면 <b>그 줄만</b> 거울에 비춘 모양이다 — 점이 줄기 쪽 · 문구가 그 왼쪽에 서고 레인은 카드
/// 왼쪽으로 빠진다. 줄기는 줄들이 나가는 쪽의 반대편에 선다(섞이면 가운데). 분기 포트도 같은 규칙이다.
///
/// 시험마다 챕터 시작 카드(start)를 멀리 오른쪽에 둔다 — 예전 규칙(시작 카드 기준으로 카드째 뒤집기)이면 결과가 달랐다.
/// </summary>
public sealed class GraphChoiceSideTests : IDisposable
{
    private const double CardWidth = 210;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-choice-side", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 도착이_왼쪽인_선택지만_왼쪽으로_나간다() => HeadlessUi.Run(() =>
    {
        // 소유자 화면 그대로 — 한 카드의 두 선택지가 왼쪽 카드와 오른쪽 카드로 간다.
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("start", 2400, 0), ("scene", 800, 0), ("left", 100, 700), ("right", 1500, 700));
        session.Editor.AddEdge("ch01", "scene", "left", optionLabel: "왼쪽 길");
        session.Editor.AddEdge("ch01", "scene", "right", optionLabel: "오른쪽 길");
        Redraw(graph);

        double sceneX = CardX(graph, session, "scene");
        double trunkX = sceneX + (CardWidth / 2);
        StackPanel leftRow = Row(graph, "scene", "왼쪽 길");
        StackPanel rightRow = Row(graph, "scene", "오른쪽 길");

        // 양쪽이 섞였으니 줄기는 가운데 — 왼쪽 줄은 줄기 왼쪽에(점이 줄기 쪽), 오른쪽 줄은 줄기 오른쪽에(점이 줄기 쪽) 선다.
        Assert.Equal(trunkX - 14, Canvas.GetLeft(leftRow) + leftRow.DesiredSize.Width, precision: 3);
        Assert.IsType<Ellipse>(leftRow.Children[^1]);
        Assert.Equal(trunkX + 14, Canvas.GetLeft(rightRow), precision: 3);
        Assert.IsType<Ellipse>(rightRow.Children[0]);

        // 레인도 제 쪽으로 빠진다.
        Assert.True(Lane(graph, "scene>left").Data!.Bounds.Left < sceneX);
        Assert.True(Lane(graph, "scene>right").Data!.Bounds.Right > sceneX + CardWidth);
    });

    [Fact]
    public void 전부_오른쪽이면_늘_쓰던_모양_전부_왼쪽이면_줄기가_오른쪽_안이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("start", 2400, 0), ("east", 800, 0), ("west", 800, 1400),
            ("a", 1500, 700), ("b", 100, 2100), ("c", 300, 2100), ("d", 500, 2100));
        session.Editor.AddEdge("ch01", "east", "a", optionLabel: "동쪽");
        session.Editor.AddEdge("ch01", "west", "b", optionLabel: "서쪽 하나");
        session.Editor.AddEdge("ch01", "west", "c", optionLabel: "서쪽 둘");
        session.Editor.AddEdge("ch01", "west", "d", optionLabel: "서쪽 셋");
        Redraw(graph);

        // 오른쪽뿐(빈 칸도 오른쪽) — 줄기는 카드 왼쪽 안이다.
        StackPanel east = Row(graph, "east", "동쪽");
        Assert.Equal(CardX(graph, session, "east") + 30 + 14, Canvas.GetLeft(east), precision: 3);

        // 셋이 다 왼쪽(빈 칸이 없다) — 줄기는 카드 오른쪽 안이다.
        double westRight = CardX(graph, session, "west") + CardWidth;

        foreach (string label in new[] { "서쪽 하나", "서쪽 둘", "서쪽 셋" })
        {
            StackPanel row = Row(graph, "west", label);
            Assert.Equal(westRight - 30 - 14, Canvas.GetLeft(row) + row.DesiredSize.Width, precision: 3);
        }
    });

    [Fact]
    public void 분기_포트도_출발_카드를_기준으로_쪽을_고른다() => HeadlessUi.Run(() =>
    {
        // 2026-10-29 소유자 — "분기가 … 왼쪽에 위치하면 박스 왼쪽에 생성되도록 해줘".
        (GraphEditorView graph, AuthoringSession session) = Show(("start", 2400, 0), ("scene", 800, 0));
        DialogueNode scene = Card(session, "scene");
        string lineId = session.Project.FindScript(scene.ScriptId)!.ActiveLines.First().Id;
        DialogueNode branch = session.Editor.AddBranchMarker(scene.Id, lineId);

        // 시작 카드보다는 왼쪽이지만 출발 카드보다는 오른쪽 — 오른쪽 포트다.
        session.Editor.MoveNode(branch.Id, 1300, 300);
        Redraw(graph);
        Assert.Equal(1, Grid.GetColumn(DetourKnob(graph, scene.Id)));

        session.Editor.MoveNode(branch.Id, 100, 300);
        graph.RefreshPositions();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Ellipse knob = DetourKnob(graph, scene.Id);
        double sceneLeft = CardX(graph, session, "scene");

        // 점이 카드 왼쪽 가장자리에 걸리고, 선은 그 왼쪽으로 나가 분기 카드의 오른쪽으로 든다.
        Assert.Equal(0, Grid.GetColumn(knob));
        Assert.Equal(sceneLeft, Center(graph, knob).X, tolerance: 1.5);
        Assert.Contains(Edges(graph), path => path.Data!.Bounds.Right <= sceneLeft + 1);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => EpisodeNaming.EpisodeIdOf(node) == episodeId);

    /// <summary>그 카드의 「분기」 포트 점.</summary>
    private static Ellipse DetourKnob(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(card => card.Tag as string == nodeId)
            .GetVisualDescendants().OfType<Grid>()
            .Single(row => row.Children.OfType<TextBlock>().Any(text => text.Text == "분기"))
            .Children.OfType<Ellipse>().Single();

    private static Point Center(GraphEditorView graph, Ellipse knob) =>
        knob.TranslatePoint(
            new Point(knob.Bounds.Width / 2, knob.Bounds.Height / 2),
            graph.FindControl<Canvas>("GraphCanvas")!)!.Value;

    /// <summary>실행 선(IF 갈래 · 분기) — 레인과 그 누르는 자리는 Tag를 진다.</summary>
    private static IEnumerable<Avalonia.Controls.Shapes.Path> Edges(GraphEditorView graph) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Avalonia.Controls.Shapes.Path>()
            .Where(path => path.IsVisible && path.Data is not null && path.Tag is null);

    private static void Redraw(GraphEditorView graph)
    {
        graph.Rebuild();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static double CardX(GraphEditorView graph, AuthoringSession session, string episodeId)
    {
        string nodeId = session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => EpisodeNaming.EpisodeIdOf(node) == episodeId).Id;

        return Canvas.GetLeft(graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(card => card.Tag as string == nodeId));
    }

    /// <summary>그 에피소드의 선택지 줄 하나 — Tag가 <c>{에피소드}#{칸}</c>이고 문구를 든다.</summary>
    private static StackPanel Row(GraphEditorView graph, string episodeId, string label) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Single(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true &&
                           row.Children.OfType<TextBlock>().Any(text => text.Text == label));

    /// <summary>선택지 레인 — Tag가 <c>레인:{출발}>{도착}</c>이다.</summary>
    private static Avalonia.Controls.Shapes.Path Lane(GraphEditorView graph, string fromTo) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Avalonia.Controls.Shapes.Path>()
            .Single(path => path.Tag as string == "레인:" + fromTo);

    /// <summary>ch01 판에 에피소드들을 그 자리에 세운다 — 첫 에피소드가 챕터 시작이다.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show(params (string Id, double X, double Y)[] episodes)
    {
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "선택지 쪽" });

        var session = new AuthoringSession();
        session.Open(manifest);
        session.Editor.EnsureChapter("ch01");
        string fileId = session.EnsureChapterBoard("ch01");

        foreach ((string id, double x, double y) in episodes)
        {
            session.Editor.AddEpisode("ch01", id, title: id, 0, 0);
            DialogueNode card = session.Project.EnumerateNodes().OfType<DialogueNode>()
                .Single(node => EpisodeNaming.EpisodeIdOf(node) == id);
            session.Editor.MoveNode(card.Id, x, y);
        }

        session.SetFileExpanded(fileId, expanded: true);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        Redraw(graph);

        return (graph, session);
    }
}
