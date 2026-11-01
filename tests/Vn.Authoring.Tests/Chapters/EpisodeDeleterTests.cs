using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>에피소드 지우기는 되돌리기 한 칸이다</b> (2026-11-01 소유자 보고: *"지우고 나서 ctrl+z로 복구를 하면 에피소드 -> 장면 순서로
/// 복구가 되는데 … 장면을 안 가진 에피소드라는 게 있었어"*). 챕터의 에피소드 행과 판의 카드가 따로 적혀서 Ctrl+Z 한 번은 카드만
/// 돌려놓고 행은 지운 채로 두었다 — 챕터에 없는(그래서 장면도 없는) 카드가 판에 섰다.
/// </summary>
public sealed class EpisodeDeleterTests
{
    [Fact]
    public void 지운_에피소드는_되돌리기_한_번에_행_간선_카드가_함께_돌아온다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "b", "b", 0, 400, sceneId: "opening");
        editor.AddEdge("ch01", "root", "b", optionLabel: "가자");

        EpisodeDeleter.Result result = EpisodeDeleter.Delete(editor, "ch01", "b");

        Assert.True(result.Ok);
        Assert.Null(EpisodeNaming.CardFor(editor.Project, "ch01", "b"));

        editor.Undo();

        ChapterDocument chapter = editor.FindChapter("ch01")!;
        ChapterEpisode episode = Assert.Single(chapter.Episodes, item => item.EpisodeId == "b");
        Assert.Equal("opening", episode.SceneId);
        Assert.Contains(chapter.Edges, edge => edge.ToEpisodeId == "b");
        Assert.NotNull(EpisodeNaming.CardFor(editor.Project, "ch01", "b"));
    }
}
