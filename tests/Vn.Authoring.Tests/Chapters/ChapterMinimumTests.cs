using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터에는 장면 하나 · 에피소드 하나가 늘 남는다</b>(2026-11-18 소유자 · chapter-minimum M2) — 지우기는 마지막 것을
/// 받지 않고, 아무것도 바꾸지 않는다(되돌리기 칸도 쓰지 않는다). 챕터째 지우기는 그대로다.
/// </summary>
public sealed class ChapterMinimumTests
{
    private const string Ch = "ch01";

    /// <summary>복도(ep01 · ep02) · 교실(ep03).</summary>
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter(Ch);
        editor.AddEpisode(Ch, "ep01", "", 0, 0, sceneId: "복도");
        editor.AddEpisode(Ch, "ep02", "", 0, 0, sceneId: "복도");
        editor.AddEpisode(Ch, "ep03", "", 0, 0, sceneId: "교실");
        return editor;
    }

    [Fact]
    public void 마지막_에피소드는_까닭을_말하며_지우지_않는다()
    {
        ProjectEditor editor = Editor();
        Assert.True(EpisodeDeleter.Delete(editor, Ch, "ep01").Ok);
        Assert.True(EpisodeDeleter.Delete(editor, Ch, "ep02").Ok);
        long before = editor.Revision;

        EpisodeDeleter.Result last = EpisodeDeleter.Delete(editor, Ch, "ep03");

        Assert.False(last.Ok);
        Assert.Equal(ChapterMinimum.LastEpisodeReason, last.Failure);
        Assert.Equal(["ep03"], editor.FindChapter(Ch)!.Episodes.Select(episode => episode.EpisodeId));
        Assert.NotNull(EpisodeNaming.CardFor(editor.Project, Ch, "ep03"));
        Assert.Equal(before, editor.Revision);
    }

    [Fact]
    public void 마지막_장면은_지우지_않고_다른_장면이_있으면_지운다()
    {
        ProjectEditor editor = Editor();

        Assert.Equal(new ProjectEditor.RemovedScene(1, 0), editor.RemoveScene(Ch, "교실"));
        long before = editor.Revision;

        var refused = Assert.Throws<InvalidOperationException>(() => editor.RemoveScene(Ch, "복도"));

        Assert.Equal(ChapterMinimum.LastSceneReason, refused.Message);
        Assert.Equal(2, editor.FindChapter(Ch)!.Episodes.Count);
        Assert.Equal(before, editor.Revision);
    }

    [Fact]
    public void 마지막인지는_챕터의_에피소드로_잰다()
    {
        ChapterEpisode Episode(string id, string scene) => new(id, id, "", 0, 0, null) { SceneId = scene };

        Assert.True(ChapterMinimum.IsLastEpisode([Episode("a", "s")], "a"));
        Assert.False(ChapterMinimum.IsLastEpisode([Episode("a", "s"), Episode("b", "s")], "a"));
        Assert.True(ChapterMinimum.IsLastScene([Episode("a", "s"), Episode("b", "s")], "s"));
        Assert.False(ChapterMinimum.IsLastScene([Episode("a", "s"), Episode("b", "t")], "s"));
        Assert.False(ChapterMinimum.IsLastScene([], "s"));   // 옛 파일의 빈 챕터 — 지울 장면이 없다
    }
}
