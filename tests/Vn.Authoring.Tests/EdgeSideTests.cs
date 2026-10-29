using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>선이 나가는 쪽은 그 선의 도착이 정한다</b> (H1 · 2026-10-29 소유자: *"연결되어있는 루트의 현재 연결되어 있는 하나의
/// 루트의 선택지 방향이 바뀌길 원한거야"*). 도착 카드가 출발 카드보다 왼쪽이면 왼쪽 — 챕터 시작 카드가 어디 있든 상관없다.
/// </summary>
public sealed class EdgeSideTests
{
    [Fact]
    public void 분기_포트는_도착이_출발_카드보다_왼쪽일_때만_왼쪽이다()
    {
        // 챕터 시작 카드(start)는 멀리 오른쪽이다 — 예전 규칙(시작 카드 기준)이면 scene의 포트가 늘 왼쪽이었다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "start", "start", 0, 0);
        editor.AddEpisode("ch01", "scene", "scene", 0, 0);
        editor.MoveNode(EpisodeNaming.CardFor(editor.Project, "ch01", "start")!.Id, 2000, 0);
        DialogueNode scene = EpisodeNaming.CardFor(editor.Project, "ch01", "scene")!;
        editor.MoveNode(scene.Id, 800, 0);
        string lineId = editor.Project.FindScript(scene.ScriptId)!.ActiveLines.First().Id;
        DialogueNode branch = editor.AddBranchMarker(scene.Id, lineId);

        editor.MoveNode(branch.Id, 1200, 300);
        Assert.False(DetourPort(editor, scene.Id).OnLeft);

        editor.MoveNode(branch.Id, 100, 300);
        Assert.True(DetourPort(editor, scene.Id).OnLeft);
    }

    [Fact]
    public void 낙서판에서도_같은_규칙이다()
    {
        var project = new StoryProject();
        var board = new StoryFile("sf_free", "낙서판");
        project.Files.Add(board);
        var editor = new ProjectEditor(project);
        DialogueNode node = editor.AddDialogueNode(board.Id, 800, 0, "본문");
        string lineId = editor.Project.FindScript(node.ScriptId)!.ActiveLines.First().Id;
        DialogueNode branch = editor.AddBranchMarker(node.Id, lineId);

        editor.MoveNode(branch.Id, 100, 0);

        Assert.True(DetourPort(editor, node.Id).OnLeft);
    }

    [Fact]
    public void 같은_자리면_늘_쓰던_오른쪽이다()
    {
        Assert.True(EdgeSide.GoesLeft(sourceX: 800, targetX: 100));
        Assert.False(EdgeSide.GoesLeft(sourceX: 800, targetX: 800));
        Assert.False(EdgeSide.GoesLeft(sourceX: 800, targetX: 1500));
    }

    private static GraphOutputPortProjection DetourPort(ProjectEditor editor, string nodeId) =>
        GraphProjectionBuilder
            .Build(editor.Project, new HashSet<string>(editor.Project.Files.Select(file => file.Id)))
            .Items.OfType<ExpandedNodeProjection>()
            .Single(item => item.NodeId == nodeId)
            .OutputPorts.Single(port => port.ExecutionPort?.Kind == ExitPortKind.Detour);
}
