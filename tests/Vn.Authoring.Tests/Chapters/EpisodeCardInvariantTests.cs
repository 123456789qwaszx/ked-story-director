using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>에피소드와 카드는 한 칸에 서고 함께 지워진다</b>(2026-11-18 · <c>docs/work-orders/old-shapes-orders.md</c> O4). 에피소드마다 그 챕터 판에 표식 카드가
/// 하나, 에피소드 카드는 그 챕터의 에피소드를 가리킨다. 편집 명령이 이것을 지키고, 그렇지 않은 프로젝트는 저장 검증(읽기 · 저장 · 되돌리기)이
/// 받지 않는다 — 열 때 맞추던 길(⛔ 판 맞추기)은 걷혔다.
/// </summary>
public sealed class EpisodeCardInvariantTests
{
    [Fact]
    public void 카드_없는_에피소드_에피소드를_잃은_카드_표식이_겹친_카드는_받지_않는다()
    {
        ProjectEditor editor = World();
        JsonSupport.ValidateProject(editor.Project);

        DialogueNode card = EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!;
        StoryFile board = editor.Project.FindFileContainingNode(card.Id)!;

        board.Nodes.Remove(card);   // 카드 없는 에피소드
        Assert.Throws<InvalidDataException>(() => JsonSupport.ValidateProject(editor.Project));
        board.Nodes.Add(card);

        card.MarkedEpisodeId = "없는에피소드";   // 에피소드를 잃은 카드
        Assert.Throws<InvalidDataException>(() => JsonSupport.ValidateProject(editor.Project));
        card.MarkedEpisodeId = "ep01";

        board.Nodes.Add(new DialogueNode(name: "둘째") { MarkedEpisodeId = "ep01" });   // 표식이 겹친다
        Assert.Throws<InvalidDataException>(() => JsonSupport.ValidateProject(editor.Project));
    }

    [Fact]
    public void 에피소드를_지우면_카드도_한_칸에_걷히고_카드만은_지우지_않는다()
    {
        ProjectEditor editor = World();
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!;

        Assert.Throws<InvalidOperationException>(() => editor.RemoveNode(card.Id));

        long revision = editor.Revision;
        editor.RemoveEpisode("ch01", "ep01");

        Assert.Equal(revision + 1, editor.Revision);
        Assert.Null(editor.Project.FindNode(card.Id));
        JsonSupport.ValidateProject(editor.Project);

        editor.Undo();
        Assert.Same(editor.Project.FindNode(card.Id), EpisodeNaming.CardFor(editor.Project, "ch01", "ep01"));
    }

    [Fact]
    public void 에피소드_이름을_바꾸면_카드의_표식이_따라간다()
    {
        ProjectEditor editor = World();
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!;

        editor.RenameEpisode("ch01", "ep01", "ep01_new");

        Assert.Equal("ep01_new", editor.Project.FindDialogue(card.Id)!.MarkedEpisodeId);
        JsonSupport.ValidateProject(editor.Project);
    }

    [Fact]
    public void 이미_있는_에피소드의_이름으로_카드를_세우지_않는다()
    {
        // ⛔ 그 전에는 이미 있는 에피소드에 표식만 붙였다(열 때 판 맞추기의 뒷길).
        ProjectEditor editor = World();

        Assert.Throws<InvalidOperationException>(() =>
            editor.AddDialogueNode(editor.EnsureChapterBoard("ch01"), "ep01", name: "ep01"));
    }

    private static ProjectEditor World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "첫", 0, 0, sceneId: "scene01");
        return editor;
    }
}
