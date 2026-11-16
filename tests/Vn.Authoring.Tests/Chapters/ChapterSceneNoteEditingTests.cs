using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 장면 메모 · 에피소드 지시의 편집 (2026-11-16 · <c>docs/work-orders/scene-notes-orders.md</c> S2). 지키는 것:
/// ① 쓰기 · 고치기 · 지우기가 되돌리기 한 칸씩이고 같은 값은 칸을 쓰지 않는다 ② 장면 이름 바꾸기가 메모를 데려간다(같은 칸)
/// ③ 풀기 · 마지막 에피소드 지우기 · 다른 장면으로 합치기는 메모를 지우지 않는다 — 주인 없이 남는다
/// ④ 장면을 안 나눈 에피소드의 이름을 바꾸면 그 장면 열쇠(__scene_{Id})의 메모가 따라간다.
/// </summary>
public sealed class ChapterSceneNoteEditingTests
{
    private const string Ch = "ch01";

    /// <summary>복도(ep01 · ep02) · 교실(ep03) · 장면 없음(ep04).</summary>
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter(Ch);
        editor.AddEpisode(Ch, "ep01", "", 0, 0, sceneId: "복도");
        editor.AddEpisode(Ch, "ep02", "", 0, 0, sceneId: "복도");
        editor.AddEpisode(Ch, "ep03", "", 0, 0, sceneId: "교실");
        editor.AddEpisode(Ch, "ep04", "", 0, 0);
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
    public void 장면을_안_나눈_에피소드에도_메모를_단다()
    {
        ProjectEditor editor = Editor();

        editor.SetSceneNote(Ch, "__scene_ep04", "혼자 선 에피소드");

        Assert.Equal([("__scene_ep04", "혼자 선 에피소드")], Notes(editor));
        Assert.Empty(Orphans(editor));
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
    public void 미지정_장면에_이름을_붙여도_메모가_따라간다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "__scene_ep04", "혼자");

        editor.RenameScene(Ch, "__scene_ep04", "옥상");

        Assert.Equal([("옥상", "혼자")], Notes(editor));
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
    public void 장면을_풀면_메모는_주인_없이_남는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "복도", "풀린 장면");

        Assert.Equal(2, editor.RenameScene(Ch, "복도", ""));

        Assert.Equal([("복도", "풀린 장면")], Notes(editor));
        Assert.Equal(["복도"], Orphans(editor));
    }

    [Fact]
    public void 마지막_에피소드를_지우거나_옮겨도_메모는_남는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "교실", "교실의 사정");
        editor.SetSceneNote(Ch, "복도", "복도의 사정");

        editor.RemoveEpisode(Ch, "ep03");
        editor.UpdateEpisode(Ch, "ep01", sceneId: string.Empty);
        editor.UpdateEpisode(Ch, "ep02", sceneId: "옥상");

        Assert.Equal([("교실", "교실의 사정"), ("복도", "복도의 사정")], Notes(editor));
        Assert.Equal(["교실", "복도"], Orphans(editor));

        editor.SetSceneNote(Ch, "교실", null);
        Assert.Equal(["복도"], Orphans(editor));
    }

    [Fact]
    public void 장면을_안_나눈_에피소드의_이름을_바꾸면_그_장면의_메모가_따라간다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "__scene_ep04", "혼자");
        editor.SetSceneNote(Ch, "복도", "복도");

        editor.RenameEpisode(Ch, "ep04", "ep04_new");
        editor.RenameEpisode(Ch, "ep01", "ep01_new");

        Assert.Equal([("__scene_ep04_new", "혼자"), ("복도", "복도")], Notes(editor));
        Assert.Empty(Orphans(editor));
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
    public void 에피소드_지시는_장면을_옮겨도_에피소드를_따라간다()
    {
        ProjectEditor editor = Editor();
        editor.UpdateEpisodeMemo(Ch, "ep01", "지시");

        editor.UpdateEpisode(Ch, "ep01", sceneId: "교실");

        ChapterEpisode moved = Chapter(editor).Episodes[0];
        Assert.Equal(("교실", "지시"), (moved.SceneId, moved.Memo));
    }
}
