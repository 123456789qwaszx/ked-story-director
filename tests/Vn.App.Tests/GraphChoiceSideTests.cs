using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>선택지 칸은 카드 아래에 가로로 나란히 서고, 선은 제 칸 밑에서 아래로 나간다</b>(2026-11-26 소유자 — 작가들의 보드처럼 아래로 뻗는다).
///
/// 줄기는 언제나 카드 왼쪽에서 같은 자리다(<see cref="GraphEditorView.RailTrunkInset"/> · 2026-11-25) — 첫 칸의 점이 그 줄기 바로 밑이고, 다음 칸들이 칸의 차례대로
/// 그 오른쪽에 선다. 도착이 어느 쪽에 있든 칸의 모양은 같다(점이 왼쪽 · 문구가 오른쪽). 길은 칸 밑에서 곧게 내려가 도착 접점의 높이에서 건너고, 내려가는
/// 자리를 다른 카드가 막으면 그 카드의 바깥으로 비켜 내려간다.
/// ⛔ 2026-10-29 ~ 11-26에는 칸이 줄기에 세로로 쌓였고, 도착이 줄기보다 왼쪽인 칸만 거울처럼 뒤집었다(소유자 — 「좌우 뒤집기는 없애도 됩니다」).
/// 분기 포트(카드 안 실행 포트)는 전처럼 카드의 가운데에서 쪽을 잰다.
///
/// 시험마다 챕터 시작 카드(start)를 멀리 오른쪽에 둔다.
/// </summary>
public sealed class GraphChoiceSideTests : IDisposable
{
    /// <summary>카드 폭 — 시험의 카드는 기본 크기로 선다(설정 「[대본] 카드 기본 크기」).</summary>
    private static double CardWidth => CardDefaults.Width;

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
    public void 선택지_칸은_줄기에서_오른쪽으로_나란히_서고_선은_제_칸_밑에서_내려간다() => HeadlessUi.Run(() =>
    {
        // 한 카드의 두 선택지가 왼쪽 카드와 오른쪽 카드로 간다.
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("start", 2400, 0), ("scene", 800, 0), ("left", 100, 700), ("right", 1500, 700));
        session.Editor.AddEdge("ch01", "scene", "left", optionLabel: "왼쪽 길");
        session.Editor.AddEdge("ch01", "scene", "right", optionLabel: "오른쪽 길");
        Redraw(graph);

        double trunkX = CardX(graph, session, "scene") + GraphEditorView.RailTrunkInset;
        StackPanel first = Row(graph, "scene", "왼쪽 길");
        StackPanel second = Row(graph, "scene", "오른쪽 길");

        // 첫 칸의 점이 줄기 바로 밑이고, 둘째 칸은 그 오른쪽에 같은 높이로 선다 — 도착이 왼쪽이든 오른쪽이든 점이 왼쪽 · 문구가 오른쪽이다.
        Assert.Equal(trunkX - GraphEditorView.PortRadius, Canvas.GetLeft(first), precision: 3);
        Assert.True(Canvas.GetLeft(second) > Canvas.GetLeft(first) + first.DesiredSize.Width, "둘째 칸은 첫 칸의 문구 오른쪽에 선다");
        Assert.Equal(MiddleY(first), MiddleY(second), precision: 3);
        Assert.IsType<Ellipse>(first.Children[0]);
        Assert.IsType<Ellipse>(second.Children[0]);

        // 선은 제 칸의 점 바로 밑에서 나가 아래로 내려간다 — 왼쪽 도착으로는 왼쪽으로, 오른쪽 도착으로는 오른쪽으로 건넌다.
        Rect toLeft = Lane(graph, "scene>left").Data!.Bounds;
        Rect toRight = Lane(graph, "scene>right").Data!.Bounds;

        Assert.Equal(MiddleY(first) + GraphEditorView.PortRadius, toLeft.Top, precision: 3);
        Assert.Equal(trunkX, toLeft.Right, precision: 3);
        Assert.True(toLeft.Left < CardX(graph, session, "scene"));
        Assert.Equal(MiddleY(second) + GraphEditorView.PortRadius, toRight.Top, precision: 3);
        Assert.Equal(Canvas.GetLeft(second) + GraphEditorView.PortRadius, toRight.Left, precision: 3);
        Assert.True(toRight.Right > CardX(graph, session, "scene") + CardWidth);
    });

    [Fact]
    public void 어디로_가든_칸은_카드_왼쪽에서_같은_자리의_줄기에서_시작한다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("start", 2400, 0), ("east", 800, 0), ("west", 800, 1400),
            ("a", 1500, 700), ("b", 100, 2100), ("c", 300, 2100), ("d", 500, 2100));
        session.Editor.AddEdge("ch01", "east", "a", optionLabel: "동쪽");
        session.Editor.AddEdge("ch01", "west", "b", optionLabel: "서쪽 하나");
        session.Editor.AddEdge("ch01", "west", "c", optionLabel: "서쪽 둘");
        session.Editor.AddEdge("ch01", "west", "d", optionLabel: "서쪽 셋");
        Redraw(graph);

        // 오른쪽으로 가는 길 하나(나머지는 빈 칸) — 첫 칸의 점이 줄기 밑이다.
        StackPanel east = Row(graph, "east", "동쪽");
        Assert.Equal(CardX(graph, session, "east") + GraphEditorView.RailTrunkInset - GraphEditorView.PortRadius, Canvas.GetLeft(east), precision: 3);

        // 셋이 다 왼쪽으로 가도(빈 칸이 없다) 칸은 줄기에서 오른쪽으로 칸의 차례대로 선다 — 줄 하나를 이어도 줄기가 옆으로 뛰지 않고 칸이 뒤집히지 않는다.
        List<StackPanel> west = new[] { "서쪽 하나", "서쪽 둘", "서쪽 셋" }.Select(label => Row(graph, "west", label)).ToList();
        Assert.Equal(CardX(graph, session, "west") + GraphEditorView.RailTrunkInset - GraphEditorView.PortRadius, Canvas.GetLeft(west[0]), precision: 3);
        Assert.True(Canvas.GetLeft(west[1]) > Canvas.GetLeft(west[0]) + west[0].DesiredSize.Width);
        Assert.True(Canvas.GetLeft(west[2]) > Canvas.GetLeft(west[1]) + west[1].DesiredSize.Width);
        Assert.All(west, row => Assert.IsType<Ellipse>(row.Children[0]));

        // 빈 칸만 있는 새 카드도 같은 자리다.
        StackPanel blank = Rows(graph, "a").First();
        Assert.Equal(CardX(graph, session, "a") + GraphEditorView.RailTrunkInset - GraphEditorView.PortRadius, Canvas.GetLeft(blank), precision: 3);
    });

    [Fact]
    public void 내려가는_자리를_다른_카드가_막으면_그_카드의_바깥으로_비켜_내려간다() => HeadlessUi.Run(() =>
    {
        // root 아래로 mid · far가 한 줄로 서 있다 — root → far의 길이 첫 칸 밑으로 곧게 내려가면 mid 카드 뒤를 지나 보이지 않는다.
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("start", 2400, 0), ("root", 100, 0), ("mid", 100, 400), ("far", 100, 800));
        session.Editor.AddEdge("ch01", "root", "far", optionLabel: "건너뛴다");
        session.Editor.AddEdge("ch01", "root", "mid", optionLabel: "바로 아래");
        Redraw(graph);

        double midRight = CardX(graph, session, "mid") + CardWidth;

        // far의 접점(카드 가운데)은 줄기보다 오른쪽이다 — 막은 카드의 오른쪽 바깥으로 비켜 내려간다.
        Assert.True(Lane(graph, "root>far").Data!.Bounds.Right > midRight, "mid 카드의 오른쪽 바깥으로 비켜 간다");

        // 막는 카드가 없는 길(도착 카드는 막는 카드가 아니다)은 제 칸 밑에서 곧게 내려간다.
        Assert.Equal(
            Canvas.GetLeft(Row(graph, "root", "바로 아래")) + GraphEditorView.PortRadius,
            Lane(graph, "root>mid").Data!.Bounds.Right,
            precision: 3);
    });

    [Fact]
    public void 분기_포트도_출발_카드를_기준으로_쪽을_고른다() => HeadlessUi.Run(() =>
    {
        // 분기가 다녀갈 카드가 왼쪽에 있으면 포트는 카드 왼쪽에 선다.
        (GraphEditorView graph, AuthoringSession session) = Show(("start", 2400, 0), ("scene", 800, 0));
        DialogueNode scene = Card(session, "scene");
        string lineId = scene.Script.ActiveLines.First().Id;
        DialogueNode branch = session.Editor.AddBranchMarker(scene.Id, lineId);

        // 시작 카드보다는 왼쪽이지만 출발 카드보다는 오른쪽 — 오른쪽 포트다.
        session.Editor.MoveNode(branch.Id, 1300, 300);
        Redraw(graph);
        Assert.Equal(CardX(graph, session, "scene") + CardWidth, Center(graph, DetourKnob(graph, scene.Id)).X, tolerance: 1.5);

        session.Editor.MoveNode(branch.Id, 100, 300);
        graph.RefreshPositions();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Ellipse knob = DetourKnob(graph, scene.Id);
        double sceneLeft = CardX(graph, session, "scene");

        // 점이 카드 왼쪽 가장자리에 걸리고, 선은 그 왼쪽으로 나가 분기 카드의 오른쪽으로 든다.
        Assert.Equal(sceneLeft, Center(graph, knob).X, tolerance: 1.5);
        Assert.Contains(Edges(graph), path => path.Data!.Bounds.Right <= sceneLeft + 1);
    });

    [Fact]
    public void 넓힌_카드도_칸은_카드_왼쪽에서_같은_자리에_선다() => HeadlessUi.Run(() =>
    {
        // 2026-11-25 소유자 — 카드를 넓혀도 선택지가 오른쪽으로 밀려나지 않는다. 넓힌 카드(800 ~ 1800)의 줄기는 카드 왼쪽에서 같은 자리(872)다 —
        // 도착이 그 왼쪽 아래든 바로 아래든 오른쪽 아래든 칸은 거기서부터 차례대로 선다.
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("start", 2400, 0), ("scene", 800, 0), ("under-left", 500, 700), ("under-mid", 900, 700), ("under-right", 1450, 700));
        session.Editor.ResizeNode(Card(session, "scene").Id, 1000, null);
        session.Editor.AddEdge("ch01", "scene", "under-left", optionLabel: "왼쪽 아래");
        session.Editor.AddEdge("ch01", "scene", "under-mid", optionLabel: "바로 아래");
        session.Editor.AddEdge("ch01", "scene", "under-right", optionLabel: "오른쪽 아래");
        Redraw(graph);

        double trunkX = CardX(graph, session, "scene") + GraphEditorView.RailTrunkInset;
        StackPanel left = Row(graph, "scene", "왼쪽 아래");
        StackPanel mid = Row(graph, "scene", "바로 아래");
        StackPanel right = Row(graph, "scene", "오른쪽 아래");

        Assert.Equal(trunkX - GraphEditorView.PortRadius, Canvas.GetLeft(left), precision: 3);
        Assert.True(Canvas.GetLeft(mid) > Canvas.GetLeft(left) + left.DesiredSize.Width);
        Assert.True(Canvas.GetLeft(right) > Canvas.GetLeft(mid) + mid.DesiredSize.Width);
        Assert.All(new[] { left, mid, right }, row => Assert.IsType<Ellipse>(row.Children[0]));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => EpisodeNaming.EpisodeIdOf(node) == episodeId);

    /// <summary>그 카드의 「분기」 점 — 글 칸의 그 대사 높이, 카드 가장자리에 판(캔버스)에 선다.</summary>
    private static Ellipse DetourKnob(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Ellipse>()
            .Single(knob => (knob.Tag as string)?.StartsWith(
                $"{GraphEditorView.LineKnobTag}execution:{nodeId}:detour:", StringComparison.Ordinal) == true);

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
    private static IEnumerable<StackPanel> Rows(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true);

    private static StackPanel Row(GraphEditorView graph, string episodeId, string label) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Single(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true &&
                           row.Children.OfType<TextBlock>().Any(text => text.Text == label));

    /// <summary>칸의 가운데 높이 — 칸들이 매달린 줄의 높이다(문구가 접혀 칸의 키가 달라도 가운데는 같다).</summary>
    private static double MiddleY(StackPanel row) => Canvas.GetTop(row) + (row.DesiredSize.Height / 2);

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
            session.Editor.AddEpisode("ch01", id, title: id, 0, 0, sceneId: id);
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
