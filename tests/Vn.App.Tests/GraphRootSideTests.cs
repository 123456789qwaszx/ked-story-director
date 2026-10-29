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
/// <b>뿌리보다 왼쪽에 둔 카드는 왼쪽으로 뻗는다</b> (2026-10-29 소유자: *"선택지 선이 생성되는 곳이 항상 오른쪽으로만
/// 생성 돼. … 뿌리 에피소드보다 왼쪽에 위치하면 왼쪽으로 생성되도록 해줄래?"*).
///
/// 뿌리 = 챕터 시작 에피소드의 카드. 그보다 왼쪽 카드의 선택지 칸은 거울에 비춘 모양이다 — 줄기는 카드 오른쪽 안에서
/// 내려오고, 칸의 점이 줄기 쪽에 · 문구가 그 왼쪽에 서며, 내려가는 레인은 카드 왼쪽으로 빠진다.
/// </summary>
public sealed class GraphRootSideTests : IDisposable
{
    private const double CardWidth = 210;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-root-side", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 뿌리보다_왼쪽_카드의_선택지는_왼쪽으로_나온다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("root", 800, 0), ("left", 100, 700), ("right", 1500, 700), ("below", 100, 1400));
        session.Editor.AddEdge("ch01", "left", "below", optionLabel: "내려간다");
        Redraw(graph);

        double leftX = CardX(graph, session, "left");
        StackPanel leftRow = Rows(graph, "left").First();

        // 칸은 줄기(카드 오른쪽 안) 왼쪽에 서고, 점이 줄기 쪽 · 문구가 그 왼쪽이다.
        Assert.Equal(leftX + CardWidth - 30 - 14, Canvas.GetLeft(leftRow) + leftRow.DesiredSize.Width, precision: 3);
        Assert.IsType<Ellipse>(leftRow.Children[^1]);
        Assert.IsNotType<Ellipse>(leftRow.Children[0]);

        // 이어진 길의 레인은 카드 <b>왼쪽</b>으로 내려간다.
        Avalonia.Controls.Shapes.Path lane = Assert.Single(Lanes(graph), path => (string)path.Tag! == "레인:left>below");
        Assert.True(lane.Data!.Bounds.Left < leftX);
    });

    [Fact]
    public void 뿌리와_그_오른쪽_카드는_그대로_오른쪽이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show(
            ("root", 800, 0), ("left", 100, 700), ("right", 1500, 700));

        foreach (string name in new[] { "root", "right" })
        {
            StackPanel row = Rows(graph, name).First();

            Assert.Equal(CardX(graph, session, name) + 30 + 14, Canvas.GetLeft(row), precision: 3);
            Assert.IsType<Ellipse>(row.Children[0]);
        }
    });

    [Fact]
    public void 뿌리보다_왼쪽_분기는_카드_왼쪽_포트에서_나간다() => HeadlessUi.Run(() =>
    {
        // 2026-10-29 소유자 — "분기가 뿌리 에피소드보다 왼쪽에 위치하면 박스 왼쪽에 생성되도록 해줘".
        (GraphEditorView graph, AuthoringSession session) = Show(("root", 800, 0));
        DialogueNode root = Card(session, "root");
        string lineId = session.Project.FindScript(root.ScriptId)!.ActiveLines.First().Id;
        DialogueNode branch = session.Editor.AddBranchMarker(root.Id, lineId);
        session.Editor.MoveNode(branch.Id, 100, 300);
        Redraw(graph);

        Ellipse knob = DetourKnob(graph, root.Id);
        double rootLeft = CardX(graph, session, "root");

        // 점이 카드 왼쪽 가장자리에 걸리고, 선은 그 왼쪽으로 나가 분기 카드의 오른쪽으로 든다.
        Assert.Equal(0, Grid.GetColumn(knob));
        Assert.Equal(rootLeft, Center(graph, knob).X, tolerance: 1.5);
        Assert.Contains(Edges(graph), path => path.Data!.Bounds.Right <= rootLeft + 1);

        // 분기 카드를 뿌리 오른쪽으로 옮기면(자리만 바뀐 변경) 포트도 오른쪽으로 돌아간다.
        session.Editor.MoveNode(branch.Id, 1500, 300);
        graph.RefreshPositions();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, Grid.GetColumn(DetourKnob(graph, root.Id)));
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

    /// <summary>그 에피소드의 선택지 칸 줄들 — Tag가 <c>{에피소드}#{칸}</c>이다.</summary>
    private static IReadOnlyList<StackPanel> Rows(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true)
            .OrderBy(row => row.Tag as string, StringComparer.Ordinal)
            .ToList();

    /// <summary>선택지 레인 — Tag가 <c>레인:{출발}>{도착}</c>이다(G3부터 꺾은선 하나다).</summary>
    private static IEnumerable<Avalonia.Controls.Shapes.Path> Lanes(GraphEditorView graph) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Avalonia.Controls.Shapes.Path>()
            .Where(path => (path.Tag as string)?.StartsWith("레인:", StringComparison.Ordinal) == true);

    /// <summary>ch01 판에 에피소드들을 그 자리에 세운다 — 첫 에피소드가 뿌리다.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show(params (string Id, double X, double Y)[] episodes)
    {
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "뿌리" });

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
