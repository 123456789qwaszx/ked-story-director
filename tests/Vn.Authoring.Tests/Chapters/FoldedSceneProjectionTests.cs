using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>펼친 판 안에서 장면 하나만 접는다</b> (2026-10-30 — 하이어아키의 눈, <c>docs/plans/hierarchy.md</c> SF).
///
/// 소유자: *"챕터가 접히는 건 현재를 유지하되, 챕터 내의 일부 장면만 접었을 경우, 접은 장면을 간소화 시키도록 해줘"* ·
/// *"접힌 상태로 간선의 연결을 드러내줘야 해"*. 접은 장면은 줄 목록 상자(<see cref="CollapsedSceneProjection"/>)가 되고
/// 다른 장면의 카드는 그대로다. 밖과 잇는 간선은 그 줄에 붙고, 장면 안끼리의 길은 상자 머리가 센다.
/// </summary>
public sealed class FoldedSceneProjectionTests
{
    [Fact]
    public void 접은_장면만_상자가_되고_다른_장면의_카드는_그대로다()
    {
        (ProjectEditor editor, Board board) = Build();

        GraphProjection projection = Project(editor, Fold(board, "hall"));

        Assert.Equal(
            [board.Root.Id, board.A.Id],
            projection.Items.OfType<ExpandedNodeProjection>().Select(item => item.NodeId));

        CollapsedSceneProjection scene = Assert.Single(projection.Items.OfType<CollapsedSceneProjection>());
        Assert.Equal("hall", scene.SceneId);
        Assert.Equal(board.File.Id, scene.FileId);

        // 줄 순서 = 판의 노드 순서.
        Assert.Equal(
            [board.B.Id, board.C.Id, board.Visit.Id, board.Inner.Id],
            scene.Nodes.Select(entry => entry.NodeId));

        // 자리는 그 장면 카드들이 서던 자리의 왼쪽 위 — 접어도 그 자리에 선다.
        StoryNode[] nodes = [board.B, board.C, board.Visit, board.Inner];
        Assert.Equal(new GraphPosition(nodes.Min(node => node.Layout.X), nodes.Min(node => node.Layout.Y)), scene.Position);

        // 판째 접힌 것이 아니다.
        Assert.Empty(projection.Items.OfType<CollapsedFileProjection>());
    }

    [Fact]
    public void 밖과_잇는_간선은_접은_장면의_그_줄에_붙는다()
    {
        (ProjectEditor editor, Board board) = Build();

        GraphProjection projection = Project(editor, Fold(board, "hall"));

        // root(펼친 opening) → visit(접은 hall)의 분기 선.
        GraphConnectionProjection detour = Assert.Single(projection.Connections, connection =>
            connection.SourceNodeId == board.Root.Id && connection.TargetNodeId == board.Visit.Id);

        Assert.Equal(GraphEndpointKind.ExpandedNodeOutput, detour.Source.Kind);
        Assert.Equal(GraphEndpointKind.CollapsedSceneNodeInput, detour.Target.Kind);
        Assert.Equal("hall", detour.Target.SceneId);
        Assert.Equal(2, detour.Target.ProxyRowIndex);   // b · c · visit — 머리글 줄이 없다(상자 하나가 장면 하나다)
    }

    [Fact]
    public void 장면_안끼리의_길은_그리지_않고_상자_머리가_센다()
    {
        (ProjectEditor editor, Board board) = Build();

        GraphProjection projection = Project(editor, Fold(board, "hall"));
        CollapsedSceneProjection scene = projection.Items.OfType<CollapsedSceneProjection>().Single();

        // b → inner 분기 선은 장면 안이라 선이 없다.
        Assert.DoesNotContain(projection.Connections, connection =>
            connection.SourceNodeId == board.B.Id && connection.TargetNodeId == board.Inner.Id);

        // 안쪽 길 = b → inner 분기 선 하나 + b → c 선택지 길 하나.
        Assert.Equal(2, scene.InnerCount);

        // 들고 나는 수는 그려지는 것만 — b는 a에서 들어오는 선택지 길 하나, 나가는 것은 모두 안쪽이라 없다.
        CollapsedNodeEntry b = scene.Nodes.Single(entry => entry.NodeId == board.B.Id);
        Assert.Equal((1, 0), (b.IncomingCount, b.OutgoingCount));

        CollapsedNodeEntry visit = scene.Nodes.Single(entry => entry.NodeId == board.Visit.Id);
        Assert.Equal((1, 0), (visit.IncomingCount, visit.OutgoingCount));
    }

    [Fact]
    public void 판째_접힌_판과_장면ID를_안_적은_챕터에는_장면_접기가_닿지_않는다()
    {
        (ProjectEditor editor, Board board) = Build();

        // 판째 접혀 있으면 — 지금과 같은 판 상자다(소유자: "챕터가 접히는 건 현재를 유지").
        GraphProjection folded = GraphProjectionBuilder.Build(
            editor.Project, new HashSet<string>(StringComparer.Ordinal), null, Fold(board, "hall"));

        Assert.Empty(folded.Items.OfType<CollapsedSceneProjection>());
        Assert.Single(folded.Items.OfType<CollapsedFileProjection>());

        // 장면ID를 하나도 안 적은 챕터 — 하이어아키에 장면 단이 없으니 접을 장면도 없다.
        editor.EnsureChapter("ch02");
        editor.EnsureChapterBoard("ch02");
        editor.AddEpisode("ch02", "x", "x", 0, 0);
        StoryFile flat = EpisodeNaming.BoardOf(editor.Project, "ch02")!;

        GraphProjection projection = Project(editor, new HashSet<GraphSceneKey> { new(flat.Id, "__scene_x") });

        Assert.Empty(projection.Items.OfType<CollapsedSceneProjection>());
        Assert.Contains(projection.Items.OfType<ExpandedNodeProjection>(), item => item.FileId == flat.Id);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// ch01: opening(root · a), hall(b · c · visit · inner). 선택지 길 root→a · a→b · b→c. 분기 선 root→visit(hall로 옮겼다) ·
    /// b→inner(hall 안).
    /// </summary>
    private static (ProjectEditor Editor, Board Board) Build()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "a", "a", 300, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "b", "b", 0, 600, sceneId: "hall");
        editor.AddEpisode("ch01", "c", "c", 300, 600, sceneId: "hall");
        editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        editor.AddEdge("ch01", "a", "b", optionLabel: "다음");
        editor.AddEdge("ch01", "b", "c", optionLabel: "더");

        DialogueNode root = Card(editor, "root");
        DialogueNode b = Card(editor, "b");

        DialogueNode visit = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        editor.UpdateEpisode("ch01", EpisodeNaming.EpisodeIdOf(visit), sceneId: "hall");
        editor.MoveNode(visit.Id, 600, 700);

        DialogueNode inner = editor.AddBranchMarker(b.Id, b.Script.ActiveLines.First().Id);
        editor.MoveNode(inner.Id, 600, 900);

        return (editor, new Board(EpisodeNaming.BoardOf(editor.Project, "ch01")!, root, Card(editor, "a"), b, Card(editor, "c"), visit, inner));
    }

    private static DialogueNode Card(ProjectEditor editor, string episodeId) =>
        EpisodeNaming.CardFor(editor.Project, "ch01", episodeId)!;

    private static HashSet<GraphSceneKey> Fold(Board board, string sceneId) => [new(board.File.Id, sceneId)];

    private static GraphProjection Project(ProjectEditor editor, IReadOnlySet<GraphSceneKey> folded) =>
        GraphProjectionBuilder.Build(
            editor.Project,
            new HashSet<string>(editor.Project.Files.Select(file => file.Id), StringComparer.Ordinal),
            null,
            folded);

    private sealed record Board(
        StoryFile File, DialogueNode Root, DialogueNode A, DialogueNode B, DialogueNode C, DialogueNode Visit, DialogueNode Inner);
}
