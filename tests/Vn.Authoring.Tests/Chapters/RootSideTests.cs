using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
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
    public void 뿌리_자신은_왼쪽이_아니다()
    {
        Assert.True(RootSide.IsLeftOfRoot(100, 800));
        Assert.False(RootSide.IsLeftOfRoot(800, 800));
        Assert.False(RootSide.IsLeftOfRoot(1500, 800));
    }
}
