using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>자동 모양 그대로인 ID는 자리를 따라간다</b> (2026-11-17 · <c>docs/work-orders/auto-id-orders.md</c> I3) — 에피소드를 다른 장면으로
/// 옮기거나 장면ID · 챕터ID를 바꾸면 새 자리의 이름으로. 사람이 고친 ID는 그대로다. 분기 카드는 집을 따른다. 되돌리기 한 칸.
/// </summary>
public sealed class AutoIdFollowerTests
{
    [Fact]
    public void 다른_장면으로_옮기면_자동_ID가_그_장면의_다음_번호가_되고_분기_카드도_따라간다()
    {
        (ProjectEditor editor, string board) = World();
        editor.AddDialogueNode(board, 0, 0, sceneId: "scene02");                       // scene02의 new01은 이미 있다
        DialogueNode moving = editor.AddDialogueNode(board, 0, 0, sceneId: "scene01");
        DialogueNode branch = editor.AddBranchMarker(moving.Id, moving.Script.ActiveLines.First().Id);
        Assert.Equal(("ch01_scene01_new01", "ch01_scene01_branch01"), (moving.Name, branch.Name));
        long before = editor.Revision;

        string moved = AutoIdFollower.MoveEpisodeToScene(editor, "ch01", "ch01_scene01_new01", "scene02");

        Assert.Equal("ch01_scene02_new02", moved);
        ChapterEpisode episode = Episode(editor, moved);
        Assert.Equal("scene02", episode.SceneId);
        Assert.Equal(moved, moving.Name);                         // 카드(= Yarn 노드 이름)가 함께 간다
        Assert.Equal("ch01_scene02_branch01", branch.Name);
        Assert.Equal(before + 1, editor.Revision);

        editor.Undo();
        Assert.Equal("scene01", Episode(editor, "ch01_scene01_new01").SceneId);
        Assert.Equal("ch01_scene01_branch01", editor.Project.FindDialogue(branch.Id)!.Name);
    }

    [Fact]
    public void 사람이_고친_ID는_옮겨도_그대로다()
    {
        (ProjectEditor editor, string board) = World();
        DialogueNode card = editor.AddDialogueNode(board, 0, 0, name: "opening", sceneId: "scene01");
        DialogueNode branch = editor.AddBranchMarker(card.Id, card.Script.ActiveLines.First().Id, name: "회상");

        string moved = AutoIdFollower.MoveEpisodeToScene(editor, "ch01", "opening", "scene02");

        Assert.Equal("opening", moved);
        Assert.Equal("scene02", Episode(editor, "opening").SceneId);
        Assert.Equal("회상", branch.Name);
    }

    [Fact]
    public void 장면ID를_바꾸면_번호를_지킨_채_따라간다()
    {
        (ProjectEditor editor, string board) = World();
        DialogueNode first = editor.AddDialogueNode(board, 0, 0, sceneId: "scene01");
        editor.AddDialogueNode(board, 0, 0, sceneId: "scene01");
        editor.AddDialogueNode(board, 0, 0, name: "hand", sceneId: "scene01");
        DialogueNode branch = editor.AddBranchMarker(first.Id, first.Script.ActiveLines.First().Id);
        long before = editor.Revision;

        Assert.Equal(3, AutoIdFollower.RenameScene(editor, "ch01", "scene01", "hall"));

        Assert.Equal(
            ["ch01_hall_new01", "ch01_hall_new02", "hand"],
            editor.FindChapter("ch01")!.Episodes.Select(episode => episode.EpisodeId));
        Assert.All(editor.FindChapter("ch01")!.Episodes, episode => Assert.Equal("hall", episode.SceneId));
        Assert.Equal("ch01_hall_branch01", branch.Name);
        Assert.Equal(before + 1, editor.Revision);
    }

    [Fact]
    public void 챕터ID를_바꾸면_따라간다()
    {
        (ProjectEditor editor, string board) = World();
        DialogueNode card = editor.AddDialogueNode(board, 0, 0, sceneId: "scene01");
        DialogueNode branch = editor.AddBranchMarker(card.Id, card.Script.ActiveLines.First().Id);
        long before = editor.Revision;

        Assert.True(ChapterRenamer.Rename(editor, projectManifestPath: null, "ch01", "prologue").Renamed);

        Assert.Equal("prologue_scene01_new01", Assert.Single(editor.FindChapter("prologue")!.Episodes).EpisodeId);
        Assert.Equal("prologue_scene01_new01", card.Name);
        Assert.Equal("prologue_scene01_branch01", branch.Name);
        Assert.Equal(before + 1, editor.Revision);
    }

    [Fact]
    public void 새_이름이_이미_있으면_다음_빈_번호로_간다()
    {
        // 사람이 다른 챕터의 카드에 그 이름을 붙여 두었다 — Yarn 노드 이름은 프로젝트에서 하나다.
        (ProjectEditor editor, string board) = World();
        editor.AddDialogueNode(board, 0, 0, sceneId: "scene01");
        DialogueNode other = editor.AddDialogueNode(TestChapter.Board(editor, "ch02").Id, 0, 0, sceneId: "scene09");
        editor.RenameEpisode("ch02", other.Name, "ch01_hall_new01");   // 카드(= Yarn 노드 이름)가 함께 간다

        AutoIdFollower.RenameScene(editor, "ch01", "scene01", "hall");

        Assert.Equal("ch01_hall_new02", Assert.Single(editor.FindChapter("ch01")!.Episodes).EpisodeId);
    }

    private static ChapterEpisode Episode(ProjectEditor editor, string episodeId) =>
        editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == episodeId);

    private static (ProjectEditor Editor, string Board) World()
    {
        var editor = new ProjectEditor(new StoryProject());
        return (editor, TestChapter.Board(editor).Id);
    }
}
