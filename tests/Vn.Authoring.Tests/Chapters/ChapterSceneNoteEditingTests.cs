using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 장면 메모 · 에피소드 지시의 편집 (2026-11-16 · scene-notes S2). 지키는 것:
/// ① 쓰기 · 고치기 · 지우기가 되돌리기 한 칸씩이고 같은 값은 칸을 쓰지 않는다 ② 장면 이름 바꾸기가 메모를 데려간다(같은 칸)
/// ③ 마지막 에피소드 지우기 · 다른 장면으로 합치기는 메모를 지우지 않는다 — 주인 없이 남는다.
/// </summary>
public sealed class ChapterSceneNoteEditingTests
{
    private const string Ch = "ch01";

    /// <summary>복도(ep01 · ep02) · 교실(ep03) · 무대(ep04).</summary>
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter(Ch);
        editor.AddEpisode(Ch, "ep01", "", 0, 0, sceneId: "복도");
        editor.AddEpisode(Ch, "ep02", "", 0, 0, sceneId: "복도");
        editor.AddEpisode(Ch, "ep03", "", 0, 0, sceneId: "교실");
        editor.AddEpisode(Ch, "ep04", "", 0, 0, sceneId: "무대");
        return editor;
    }

    private static ChapterDocument Chapter(ProjectEditor editor) => editor.FindChapter(Ch)!;

    private static IEnumerable<(string, string)> Notes(ProjectEditor editor) =>
        Chapter(editor).SceneNotes.Select(note => (note.SceneId, note.Text)).OrderBy(pair => pair.SceneId, StringComparer.Ordinal);

    private static IEnumerable<string> Orphans(ProjectEditor editor) =>
        ChapterSceneNotes.Orphans(Chapter(editor).Episodes, Chapter(editor).SceneNotes).Select(note => note.SceneId);

    [Fact]
    public void 메모를_쓰고_고치고_지우기가_되돌리기_한_칸씩이다()
    {
        ProjectEditor editor = Editor();

        editor.SetSceneNote(Ch, "복도", "  마감 전날.\n하린: 지침 → 결심  ");
        Assert.Equal([("복도", "마감 전날.\n하린: 지침 → 결심")], Notes(editor));

        editor.SetSceneNote(Ch, "복도", "고쳤다");
        editor.SetSceneNote(Ch, "복도", "   ");
        Assert.Empty(Chapter(editor).SceneNotes);

        editor.Undo();
        Assert.Equal([("복도", "고쳤다")], Notes(editor));
        editor.Undo();
        Assert.Equal([("복도", "마감 전날.\n하린: 지침 → 결심")], Notes(editor));
        editor.Undo();
        Assert.Empty(Chapter(editor).SceneNotes);
    }

    [Fact]
    public void 같은_값이면_되돌리기_칸을_쓰지_않는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "복도", "가");

        editor.SetSceneNote(Ch, "복도", " 가 ");
        editor.SetSceneNote(Ch, "교실", "");
        editor.Undo();

        Assert.Empty(Chapter(editor).SceneNotes);
    }

    [Fact]
    public void 장면_이름을_바꾸면_메모가_같은_칸_안에서_따라간다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "복도", "복도의 사정");

        Assert.Equal(2, editor.RenameScene(Ch, "복도", " 계단 "));

        Assert.All(Chapter(editor).Episodes.Take(2), episode => Assert.Equal("계단", episode.SceneId));
        Assert.Equal([("계단", "복도의 사정")], Notes(editor));
        Assert.Empty(Orphans(editor));

        editor.Undo();
        Assert.All(Chapter(editor).Episodes.Take(2), episode => Assert.Equal("복도", episode.SceneId));
        Assert.Equal([("복도", "복도의 사정")], Notes(editor));
    }

    [Fact]
    public void 메모가_있는_장면으로_합치면_받는_쪽을_두고_보낸_쪽은_주인_없이_남는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "복도", "보낸 쪽");
        editor.SetSceneNote(Ch, "교실", "받는 쪽");

        Assert.Equal(2, editor.RenameScene(Ch, "복도", "교실"));

        Assert.Equal([("교실", "받는 쪽"), ("복도", "보낸 쪽")], Notes(editor));
        Assert.Equal(["복도"], Orphans(editor));
    }

    [Fact]
    public void 메모가_없는_장면으로_합치면_보낸_쪽_메모가_간다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "복도", "보낸 쪽");

        editor.RenameScene(Ch, "복도", "교실");

        Assert.Equal([("교실", "보낸 쪽")], Notes(editor));
        Assert.Empty(Orphans(editor));
    }

    [Fact]
    public void 장면ID는_비울_수_없다()
    {
        ProjectEditor editor = Editor();
        long before = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.RenameScene(Ch, "복도", " "));

        Assert.Equal(before, editor.Revision);
    }

    [Fact]
    public void 마지막_에피소드를_지우거나_옮겨도_메모는_남는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "교실", "교실의 사정");
        editor.SetSceneNote(Ch, "복도", "복도의 사정");

        editor.RemoveEpisode(Ch, "ep03");
        editor.UpdateEpisode(Ch, "ep01", sceneId: "옥상");
        editor.UpdateEpisode(Ch, "ep02", sceneId: "옥상");

        Assert.Equal([("교실", "교실의 사정"), ("복도", "복도의 사정")], Notes(editor));
        Assert.Equal(["교실", "복도"], Orphans(editor));

        editor.SetSceneNote(Ch, "교실", null);
        Assert.Equal(["복도"], Orphans(editor));
    }

    [Fact]
    public void 에피소드_지시를_쓰고_비우면_지워지고_되돌리기는_한_칸씩이다()
    {
        ProjectEditor editor = Editor();

        editor.UpdateEpisodeMemo(Ch, "ep03", "  하린을 먼저 세운다 ");
        Assert.Equal("하린을 먼저 세운다", Chapter(editor).Episodes[2].Memo);

        editor.UpdateEpisodeMemo(Ch, "ep03", "하린을 먼저 세운다");   // 같은 값 — 칸을 쓰지 않는다
        editor.UpdateEpisodeMemo(Ch, "ep03", " ");
        Assert.Null(Chapter(editor).Episodes[2].Memo);

        editor.Undo();
        Assert.Equal("하린을 먼저 세운다", Chapter(editor).Episodes[2].Memo);
        editor.Undo();
        Assert.Null(Chapter(editor).Episodes[2].Memo);
    }

    [Fact]
    public void 지시_카드는_비워도_남고_빼기만_지시와_함께_뗀다()
    {
        ProjectEditor editor = Editor();

        editor.AddEpisodeNoteCard(Ch, "ep01");                 // 빈 카드
        editor.UpdateEpisodeMemo(Ch, "ep02", "지시");           // 지시를 적으면 카드도 선다
        editor.UpdateEpisodeMemo(Ch, "ep02", "");

        Assert.True(Chapter(editor).Episodes[0].HasNoteCard);
        Assert.True(Chapter(editor).Episodes[1].HasNoteCard);
        Assert.Null(Chapter(editor).Episodes[1].Memo);

        editor.UpdateEpisodeMemo(Ch, "ep02", "다시");
        editor.RemoveEpisodeNoteCard(Ch, "ep02");

        Assert.False(Chapter(editor).Episodes[1].HasNoteCard);
        Assert.Null(Chapter(editor).Episodes[1].Memo);

        editor.Undo();   // 한 칸 — 카드와 지시가 함께 돌아온다
        Assert.Equal((true, "다시"), (Chapter(editor).Episodes[1].NoteCard, Chapter(editor).Episodes[1].Memo));

    }

    [Fact]
    public void 이미_붙은_카드를_또_붙이면_되돌리기_칸을_쓰지_않는다()
    {
        ProjectEditor editor = Editor();

        editor.AddEpisodeNoteCard(Ch, "ep01");
        editor.AddEpisodeNoteCard(Ch, "ep01");
        editor.Undo();

        Assert.False(Chapter(editor).Episodes[0].HasNoteCard);
    }

    [Fact]
    public void 장면을_지우면_에피소드와_장면_항목이_한_칸에_걷히고_없는_장면은_아무것도_안_한다()
    {
        // 2026-11-18 소유자 — [대본] 하이어아키 「장면 지우기」. 장면째 지우는 것이라 메모를 주인 없이 남기지 않는다.
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "복도", "복도의 사정");
        editor.SetSceneNote(Ch, "교실", "교실");
        long before = editor.Revision;

        Assert.Equal(new ProjectEditor.RemovedScene(0, 0), editor.RemoveScene(Ch, "없는 장면"));
        Assert.Equal(before, editor.Revision);

        Assert.Equal(new ProjectEditor.RemovedScene(2, 0), editor.RemoveScene(Ch, "복도"));
        Assert.Equal(before + 1, editor.Revision);
        Assert.Equal(["ep03", "ep04"], Chapter(editor).Episodes.Select(episode => episode.EpisodeId));
        Assert.Null(EpisodeNaming.CardFor(editor.Project, Ch, "ep01"));
        Assert.Equal([("교실", "교실")], Notes(editor));

        editor.Undo();
        Assert.Equal(4, Chapter(editor).Episodes.Count);
        Assert.Equal([("교실", "교실"), ("복도", "복도의 사정")], Notes(editor));
    }

    [Fact]
    public void 장면을_지우면_그_장면이_부르던_분기_카드도_한_칸에_걷히고_다른_장면도_부르는_카드는_남는다()
    {
        // 2026-11-18 소유자 — 「장면 지우기시에 분기카드까지 함께 지워주십시오.」 걷는 것은 장면을 걷은 뒤 집을 잃는 카드다.
        ProjectEditor editor = Editor();
        DialogueNode ep01 = Card(editor, "ep01");
        DialogueNode ep03 = Card(editor, "ep03");

        DialogueNode own = editor.AddBranchMarker(ep01.Id, ep01.Script.ActiveLines.First().Id);              // 복도만 부른다
        DialogueNode shared = editor.AddBranchMarker(ep01.Id, ep01.Script.ActiveLines.First().Id);          // 복도와 교실이 함께 부른다
        DialogueNode other = editor.AddBranchMarker(ep03.Id, ep03.Script.ActiveLines.First().Id);
        editor.SetExitTarget(NodeConnections.PortsOf(ep03, editor.Project).Single(port => port.TargetNodeId == other.Id), shared.Id);
        editor.RemoveNode(other.Id);

        Assert.Equal(
            [own.Id],
            ChapterBranchCards.GoingWithScene(editor.Project, Ch, "복도").Select(card => card.Id));

        long before = editor.Revision;
        Assert.Equal(new ProjectEditor.RemovedScene(2, 1), editor.RemoveScene(Ch, "복도"));
        Assert.Equal(before + 1, editor.Revision);

        Assert.Null(editor.Project.FindNode(own.Id));
        Assert.NotNull(editor.Project.FindNode(shared.Id));   // 교실이 부르고 있다 — 집이 교실로 옮겨 남는다
        Assert.Equal("ep03", ChapterBranchCards.HomesOf(editor.Project, Ch).Single(home => home.Card.Id == shared.Id).EpisodeId);

        editor.Undo();   // 한 칸 — 에피소드와 분기 카드가 함께 돌아온다
        Assert.NotNull(editor.Project.FindNode(own.Id));
        Assert.Equal(4, Chapter(editor).Episodes.Count);
    }

    private static DialogueNode Card(ProjectEditor editor, string episodeId) => EpisodeNaming.CardFor(editor.Project, Ch, episodeId)!;

    [Fact]
    public void 에피소드_지시는_장면을_옮겨도_에피소드를_따라간다()
    {
        ProjectEditor editor = Editor();
        editor.UpdateEpisodeMemo(Ch, "ep01", "지시");

        editor.UpdateEpisode(Ch, "ep01", sceneId: "교실");

        ChapterEpisode moved = Chapter(editor).Episodes[0];
        Assert.Equal(("교실", "지시"), (moved.SceneId, moved.Memo));
    }
}
