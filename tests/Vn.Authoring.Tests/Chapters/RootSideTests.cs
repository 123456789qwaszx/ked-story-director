using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>뿌리</b> — 연출 그래프에서 "뿌리보다 왼쪽"을 재는 기준 (2026-10-29 소유자). 챕터 시작 에피소드의 카드다.
/// </summary>
public sealed class RootSideTests
{
    [Fact]
    public void 뿌리는_챕터_시작_에피소드의_카드다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        editor.AddEpisode("ch01", "next", "next", 0, 0);
        StoryFile board = EpisodeNaming.BoardOf(editor.Project, "ch01")!;

        DialogueNode root = Assert.IsType<DialogueNode>(RootSide.RootCard(editor.Project, board));

        Assert.Equal("root", EpisodeNaming.EpisodeIdOf(root));
    }

    [Fact]
    public void 낙서판에는_뿌리가_없다()
    {
        var project = new StoryProject();
        var board = new StoryFile("sf_free", "낙서판");
        project.Files.Add(board);

        Assert.Null(RootSide.RootCard(project, board));
    }

    [Fact]
    public void 뿌리보다_왼쪽_분기로_가는_포트는_왼쪽에_선다()
    {
        // 2026-10-29 소유자 — "분기가 뿌리 에피소드보다 왼쪽에 위치하면 박스 왼쪽에 생성되도록".
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        DialogueNode root = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        editor.MoveNode(root.Id, 800, 0);
        string lineId = editor.Project.FindScript(root.ScriptId)!.ActiveLines.First().Id;
        DialogueNode branch = editor.AddBranchMarker(root.Id, lineId);

        editor.MoveNode(branch.Id, 100, 0);
        Assert.True(DetourPort(editor, root.Id).OnLeft);

        editor.MoveNode(branch.Id, 1500, 0);
        Assert.False(DetourPort(editor, root.Id).OnLeft);
    }

    [Fact]
    public void 낙서판의_포트는_늘_오른쪽이다()
    {
        var project = new StoryProject();
        var board = new StoryFile("sf_free", "낙서판");
        project.Files.Add(board);
        var editor = new ProjectEditor(project);
        DialogueNode node = editor.AddDialogueNode(board.Id, 800, 0, "본문");
        string lineId = editor.Project.FindScript(node.ScriptId)!.ActiveLines.First().Id;
        DialogueNode branch = editor.AddBranchMarker(node.Id, lineId);

        editor.MoveNode(branch.Id, 100, 0);

        Assert.False(DetourPort(editor, node.Id).OnLeft);
    }

    private static GraphOutputPortProjection DetourPort(ProjectEditor editor, string nodeId) =>
        GraphProjectionBuilder
            .Build(editor.Project, new HashSet<string>(editor.Project.Files.Select(file => file.Id)))
            .Items.OfType<ExpandedNodeProjection>()
            .Single(item => item.NodeId == nodeId)
            .OutputPorts.Single(port => port.ExecutionPort?.Kind == ExitPortKind.Detour);

    [Fact]
    public void 뿌리_자신은_왼쪽이_아니다()
    {
        Assert.True(RootSide.IsLeftOfRoot(100, 800));
        Assert.False(RootSide.IsLeftOfRoot(800, 800));
        Assert.False(RootSide.IsLeftOfRoot(1500, 800));
    }
}
