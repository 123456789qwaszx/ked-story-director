using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>분기 카드는 분기를 내지 않는다</b> (2026-11-19 소유자 · usability UX27) — 분기점도 갈래 출구도. 분기 카드는 에피소드에서 들렀다 돌아오는
/// 곳이라 그 안에서 다시 갈라지지 않는다. ⛔ 전에는 분기 카드의 분기도 사슬로 따랐고, 분기 카드의 갈래 출구는 아무 카드에나 이어졌다 — 아무도
/// 부르지 않는 분기 카드에 이으면 내보낸 Yarn이 없는 노드를 불러 게임이 멈췄고(Yarn Spinner 3.2.1 — 「No node named … has been loaded」),
/// 에피소드에 이으면 그 에피소드가 진행 기록 밖에서 통째로 재생됐다. 그 전의 파일에 남은 것은 끊고 지우기만 받는다(검증 오류 —
/// <see cref="BranchCardValidationTests"/>).
/// </summary>
public sealed class BranchCardBranchesTests
{
    [Fact]
    public void 분기_카드에서는_분기를_더하지_못한다()
    {
        (ProjectEditor editor, DialogueNode root, DialogueNode branch) = World();
        long before = editor.Revision;

        Assert.True(editor.CanBranchFrom(root.Id));
        Assert.False(editor.CanBranchFrom(branch.Id));

        var refused = Assert.Throws<InvalidOperationException>(() =>
            editor.AddBranchMarker(branch.Id, branch.Script.ActiveLines.First().Id));
        Assert.Equal(ProjectEditor.BranchCardBranchesRefusal, refused.Message);
        Assert.Equal(before, editor.Revision);

        // 에피소드에서는 그대로 — 둘째 분기 카드가 선다. 그러나 분기 카드에서 그 카드로는 잇지 못한다.
        DialogueNode other = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        Assert.True(editor.CanBranchTo(root.Id, other.Id));
        Assert.False(editor.CanBranchTo(branch.Id, other.Id));
    }

    [Fact]
    public void 분기_카드의_갈래는_판에_출구_포트로_서지_않고_잇지도_못한다()
    {
        (ProjectEditor editor, DialogueNode root, DialogueNode branch) = World();
        Assert.Null(BranchEntryCondition.Set(editor, branch.Id, "신뢰높음"));
        DialogueNode other = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        ExitPort gate = Assert.Single(NodeConnections.PortsOf(branch, editor.Project), port => port.Kind == ExitPortKind.Branch);

        Assert.DoesNotContain(PortsOnBoard(editor, branch.Id), port => port.ExecutionPort?.Kind == ExitPortKind.Branch);

        long before = editor.Revision;
        editor.SetExitTarget(gate, other.Id);
        editor.SetExitTarget(gate, root.Id);
        Assert.Equal(before, editor.Revision);
        Assert.Empty(branch.BranchExits);
    }

    [Fact]
    public void 그_전의_파일에_남은_갈래_출구는_판에_서고_끊기만_받는다()
    {
        (ProjectEditor editor, DialogueNode root, DialogueNode branch) = World();
        Assert.Null(BranchEntryCondition.Set(editor, branch.Id, "신뢰높음"));
        DialogueNode other = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        branch.BranchExits[branch.Script.ActiveLines.First().Id] = other.Id;

        // 이어진 것은 선다 — 끊을 수 있게.
        ExitPort exit = Assert.Single(NodeConnections.PortsOf(branch, editor.Project), port => port.Kind == ExitPortKind.Branch);
        Assert.Contains(PortsOnBoard(editor, branch.Id), port => port.ExecutionPort is { Kind: ExitPortKind.Branch, IsConnected: true });

        // 다른 곳으로 옮겨 잇기는 받지 않는다.
        long before = editor.Revision;
        editor.SetExitTarget(exit, root.Id);
        Assert.Equal(before, editor.Revision);
        Assert.Equal(other.Id, branch.BranchExits.Values.Single());

        // 끊기는 받는다 — 되돌리기 한 칸.
        editor.SetExitTarget(exit, null);
        Assert.Equal(before + 1, editor.Revision);
        Assert.Empty(branch.BranchExits);
        Assert.DoesNotContain(PortsOnBoard(editor, branch.Id), port => port.ExecutionPort?.Kind == ExitPortKind.Branch);
    }

    [Fact]
    public void 그_전의_파일에_남은_분기점에는_새_카드를_세우지_않고_잇지도_않는다()
    {
        (ProjectEditor editor, DialogueNode root, DialogueNode branch) = World();
        DialogueNode other = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        branch.RequireExtension(branch.Script.ActiveLines.First().Id).OpenDetourPositions.Add(0);
        ExitPort open = Assert.Single(NodeConnections.PortsOf(branch, editor.Project), port => port.IsOpenDetour);
        int cards = editor.Project.EnumerateNodes().Count();
        long before = editor.Revision;

        Assert.Null(editor.AddBranchCardAt(open, 900, 0));
        editor.SetExitTarget(open, other.Id);

        Assert.Equal(before, editor.Revision);
        Assert.Equal(cards, editor.Project.EnumerateNodes().Count());
        Assert.True(Assert.Single(NodeConnections.PortsOf(branch, editor.Project), port => port.Kind == ExitPortKind.Detour).IsOpenDetour);

        // 지우기는 받는다.
        editor.RemoveOpenDetour(branch.Id, branch.Script.ActiveLines.First().Id);
        Assert.DoesNotContain(NodeConnections.PortsOf(branch, editor.Project), port => port.Kind == ExitPortKind.Detour);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>[대본] 판이 그 카드에 세우는 출구 포트들.</summary>
    private static IReadOnlyList<GraphOutputPortProjection> PortsOnBoard(ProjectEditor editor, string nodeId) =>
        GraphProjectionBuilder.Build(editor.Project, editor.Project.Files.Select(file => file.Id).ToHashSet())
            .Items.OfType<ExpandedNodeProjection>()
            .Single(item => item.NodeId == nodeId)
            .OutputPorts;

    /// <summary>ch01 — 시작 에피소드 root, root 첫 줄 앞의 분기 카드 하나. 챕터 조건 「신뢰높음」이 판에 공급돼 있다.</summary>
    private static (ProjectEditor Editor, DialogueNode Root, DialogueNode Branch) World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        string fileId = editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");

        ChapterDocument chapter = editor.FindChapter("ch01")!;
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        ChapterBoardSupply.SupplyChapterConditionsToBoard(editor, fileId, chapter.ToGraphModel("ch01"));

        DialogueNode root = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        return (editor, root, editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id));
    }
}
