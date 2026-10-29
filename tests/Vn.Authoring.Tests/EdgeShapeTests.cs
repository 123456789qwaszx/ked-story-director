using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>선 모양을 사람이 고친다</b> (G3 · 2026-10-29 소유자: *"선이 생기는 모양새가 종종 이상할 때가 있는데, 원한다면 저것도
/// 곡선이나 그런 걸 조절할 수 있도록 해줘. 피그잼처럼"*).
///
/// 선택지 레인의 모양은 챕터 간선이, 실행 선(분기 · IF 갈래)의 모양은 나가는 노드가 진다. 고칠 때마다 되돌리기 한 칸이고,
/// 저장을 건너며, 안 고친 선은 파일에 아무것도 남기지 않는다.
/// </summary>
public sealed class EdgeShapeTests
{
    private static readonly EdgeShape Curved = new(EdgeShapeKind.Curve, 12, -30);

    // ── 저장 자리 · 되돌리기 ────────────────────────────────────────────────

    [Fact]
    public void 레인_모양은_간선이_지고_되돌리기_한_칸이다()
    {
        ProjectEditor editor = Chapter();

        editor.SetEdgeShape("ch01", "a", "b", "간다", Curved);

        Assert.Equal(Curved, Edge(editor).Shape);

        editor.Undo();
        Assert.Null(Edge(editor).Shape);
    }

    [Fact]
    public void 기본_모양은_적지_않고_바뀐_것이_없으면_기록도_없다()
    {
        ProjectEditor editor = Chapter();
        editor.SetEdgeShape("ch01", "a", "b", "간다", Curved);

        // 기본으로 되돌리면 칸째 걷힌다.
        editor.SetEdgeShape("ch01", "a", "b", "간다", EdgeShape.Default);
        Assert.Null(Edge(editor).Shape);

        // 같은 모양을 또 주면 아무것도 안 적는다 — 되돌리기 한 번이 곧장 그 앞으로 간다.
        editor.SetEdgeShape("ch01", "a", "b", "간다", Curved);
        editor.SetEdgeShape("ch01", "a", "b", "간다", Curved);
        editor.Undo();
        Assert.Null(Edge(editor).Shape);
    }

    [Fact]
    public void 도착_문구_이름이_바뀌어도_모양은_간선을_따라간다()
    {
        ProjectEditor editor = Chapter();
        editor.AddEpisode("ch01", "c", "c", 0, 0);
        editor.SetEdgeShape("ch01", "a", "b", "간다", Curved);

        editor.SetEdgeRoute("ch01", "a", "b", "간다", "c", "돌아간다");
        editor.RenameEpisode("ch01", "a", "a2");

        ChapterEdge edge = Assert.Single(editor.FindChapter("ch01")!.Edges);
        Assert.Equal(("a2", "c", "돌아간다"), (edge.FromEpisodeId, edge.ToEpisodeId, edge.OptionLabel));
        Assert.Equal(Curved, edge.Shape);
    }

    [Fact]
    public void 분기_선_모양은_노드가_지고_끊으면_걷힌다()
    {
        ProjectEditor editor = Chapter();
        DialogueNode a = EpisodeNaming.CardFor(editor.Project, "ch01", "a")!;
        string lineId = a.Script.ActiveLines.First().Id;
        DialogueNode made = editor.AddBranchMarker(a.Id, lineId);
        ExitPort detour = DetourOf(editor, a.Id);
        string key = $"detour:{lineId}>{made.Id}";   // 분기의 열쇠는 줄과 다녀갈 카드다 — 한 줄에 여럿일 수 있다

        editor.SetExitShape(detour, Curved);
        Assert.Equal(Curved, a.EdgeShapes[key]);

        // 선을 끊으면 모양도 걷힌다 — 같은 줄에 다시 이을 때 옛 굽이가 되살아나지 않는다.
        editor.SetExitTarget(detour, null);
        Assert.Empty(Node(editor, a.Id).EdgeShapes);

        // 끊은 것을 되돌리면 모양도 함께 돌아온다(한 칸).
        editor.Undo();
        Assert.Equal(Curved, Node(editor, a.Id).EdgeShapes[key]);
    }

    [Fact]
    public void 이어지지_않은_출구에는_모양을_적지_않는다()
    {
        ProjectEditor editor = Chapter();
        DialogueNode a = EpisodeNaming.CardFor(editor.Project, "ch01", "a")!;
        var loose = new ExitPort(ExitPortKind.Default, a.Id, null, "기본", TargetNodeId: null, PaletteIndex: 0);

        editor.SetExitShape(loose, Curved);

        Assert.Empty(a.EdgeShapes);
    }

    [Fact]
    public void 모양은_저장을_건너고_안_고친_선은_흔적이_없다()
    {
        ProjectEditor editor = Chapter();
        string untouched = ProjectSnapshotCodec.Encode(editor.Project);

        Assert.DoesNotContain("\"shape\"", untouched, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeShapes", untouched, StringComparison.Ordinal);

        DialogueNode a = EpisodeNaming.CardFor(editor.Project, "ch01", "a")!;
        string lineId = a.Script.ActiveLines.First().Id;
        DialogueNode made = editor.AddBranchMarker(a.Id, lineId);
        editor.SetExitShape(DetourOf(editor, a.Id), new EdgeShape(EdgeShapeKind.Straight));
        editor.SetEdgeShape("ch01", "a", "b", "간다", Curved);

        StoryProject loaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project));

        Assert.Equal(Curved, loaded.Chapters.Single().Edges.Single().Shape);
        Assert.Equal(
            new EdgeShape(EdgeShapeKind.Straight),
            ((DialogueNode)loaded.FindNode(a.Id)!).EdgeShapes[$"detour:{lineId}>{made.Id}"]);
    }

    // ── 선 긋기 ─────────────────────────────────────────────────────────────

    [Fact]
    public void 직각_실행_선은_가운데_세로_마디만_옆으로_민다()
    {
        var from = new GraphPosition(0, 0);
        var to = new GraphPosition(300, 100);

        ShapedEdge auto = EdgeShapeRouter.Execution(from, to, leftward: false, shape: null);
        Assert.Equal(OrthogonalEdgeRouter.Route(from, to), auto.Points);

        ShapedEdge pushed = EdgeShapeRouter.Execution(from, to, leftward: false, new EdgeShape(BendX: 40, BendY: 70));

        Assert.Equal(
            [from, new GraphPosition(190, 0), new GraphPosition(190, 100), to],
            pushed.Points);
        Assert.Equal(new GraphPosition(190, 50), pushed.Handle);
        Assert.False(pushed.FreeY);
    }

    [Fact]
    public void 직각_레인은_내려가는_줄과_건너는_줄을_민다()
    {
        var chip = new GraphPosition(100, 50);
        var junction = new GraphPosition(400, 300);

        ShapedEdge auto = EdgeShapeRouter.Lane(chip, toward: 1, laneX: 250, junction, shape: null);

        // 자동 — 레인(250)을 타고 내려가 도착 줄(300)에서 접점으로 건너간다.
        Assert.Equal(new GraphPosition(250, 50), auto.Points[1]);
        Assert.Equal(new GraphPosition(250, 300), auto.Handle);
        Assert.Equal(junction, auto.Points[^1]);

        ShapedEdge moved = EdgeShapeRouter.Lane(chip, toward: 1, laneX: 250, junction, new EdgeShape(BendX: -20, BendY: -60));

        // 레인은 230으로, 건너는 줄은 240으로 — 접점에는 위에서 곧게 든다.
        Assert.Equal(
            [chip, new GraphPosition(230, 50), new GraphPosition(230, 240), new GraphPosition(400, 240), junction],
            moved.Points);
        Assert.True(moved.FreeY);
    }

    [Fact]
    public void 곡선은_손잡이를_지나고_가운데에서_꺾이지_않는다()
    {
        var from = new GraphPosition(0, 0);
        var to = new GraphPosition(300, 200);

        ShapedEdge auto = EdgeShapeRouter.Execution(from, to, leftward: false, new EdgeShape(EdgeShapeKind.Curve));

        // 자동 곡선의 손잡이는 곡선의 한가운데(t=½)다.
        Assert.Equal(7, auto.Points.Count);
        Assert.Equal(Bezier(from, new GraphPosition(150, 0), new GraphPosition(150, 200), to, 0.5), auto.Handle);

        ShapedEdge bent = EdgeShapeRouter.Execution(from, to, leftward: false, new EdgeShape(EdgeShapeKind.Curve, 30, -40));

        // 손잡이를 옮긴 만큼 곡선의 가운데가 따라오고, 그 양옆 제어점과 한 줄에 서서 꺾이지 않는다.
        Assert.Equal(new GraphPosition(auto.Handle!.Value.X + 30, auto.Handle.Value.Y - 40), bent.Handle);
        Assert.Equal(bent.Handle, bent.Points[3]);
        Assert.Equal(0, Cross(bent.Points[2], bent.Points[3], bent.Points[4]), precision: 6);

        // 두 끝과 그 끝의 방향(포트에서 곧게)은 그대로다.
        Assert.Equal(from, bent.Points[0]);
        Assert.Equal(to, bent.Points[^1]);
        Assert.Equal(from.Y, bent.Points[1].Y);
        Assert.Equal(to.Y, bent.Points[5].Y);
    }

    [Fact]
    public void 직선은_두_끝만_잇고_끌_손잡이가_없다()
    {
        ShapedEdge line = EdgeShapeRouter.Lane(
            new GraphPosition(0, 0), toward: -1, laneX: -40, new GraphPosition(-300, 400), new EdgeShape(EdgeShapeKind.Straight));

        Assert.Equal([new GraphPosition(0, 0), new GraphPosition(-300, 400)], line.Points);
        Assert.Null(line.Handle);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>ch01 — a에서 b로 「간다」 한 길.</summary>
    private static ProjectEditor Chapter()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "a", "a", 0, 0);
        editor.AddEpisode("ch01", "b", "b", 0, 0);
        editor.AddEdge("ch01", "a", "b", optionLabel: "간다");

        return editor;
    }

    private static ChapterEdge Edge(ProjectEditor editor) => Assert.Single(editor.FindChapter("ch01")!.Edges);

    private static DialogueNode Node(ProjectEditor editor, string id) => (DialogueNode)editor.Project.FindNode(id)!;

    private static ExitPort DetourOf(ProjectEditor editor, string nodeId) =>
        NodeConnections.PortsOf(Node(editor, nodeId), editor.Project).Single(port => port.Kind == ExitPortKind.Detour);

    private static GraphPosition Bezier(GraphPosition p0, GraphPosition p1, GraphPosition p2, GraphPosition p3, double t)
    {
        double u = 1 - t;
        double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;

        return new GraphPosition(
            (a * p0.X) + (b * p1.X) + (c * p2.X) + (d * p3.X),
            (a * p0.Y) + (b * p1.Y) + (c * p2.Y) + (d * p3.Y));
    }

    private static double Cross(GraphPosition a, GraphPosition b, GraphPosition c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
}
