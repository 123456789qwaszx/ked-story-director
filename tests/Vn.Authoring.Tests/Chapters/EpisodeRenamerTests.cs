using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>에피소드 개명은 되돌리기 한 칸이다</b> (2026-10-30). 챕터 쪽 개명(에피소드 · 간선)과 카드 따라가기(이름 · 표식)가 둘로
/// 나뉘어 있어서, Ctrl+Z 한 번은 카드 이름만 되돌리고 에피소드는 새 이름으로 남겼다.
/// </summary>
public sealed class EpisodeRenamerTests
{
    [Fact]
    public void 개명하면_에피소드_간선_카드가_함께_가고_되돌리기_한_번에_모두_돌아온다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        editor.AddEpisode("ch01", "b", "b", 0, 400);
        editor.AddEdge("ch01", "root", "b", optionLabel: "가자");
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, "ch01", "b")!;

        EpisodeRenamer.Result result = EpisodeRenamer.Rename(editor, "ch01", "b", "hall");

        Assert.True(result.Renamed);
        Assert.True(result.NodeRenamed);
        Assert.Equal(("hall", "hall"), (card.Name, card.MarkedEpisodeId));
        Assert.Contains(editor.FindChapter("ch01")!.Edges, edge => edge.ToEpisodeId == "hall");

        editor.Undo();

        ChapterDocument chapter = editor.FindChapter("ch01")!;
        DialogueNode restored = EpisodeNaming.CardFor(editor.Project, "ch01", "b")!;
        Assert.Contains(chapter.Episodes, episode => episode.EpisodeId == "b");
        Assert.Contains(chapter.Edges, edge => edge.ToEpisodeId == "b");
        Assert.Equal(("b", "b"), (restored.Name, restored.MarkedEpisodeId));
    }

    [Fact]
    public void 같은_Id가_있으면_아무것도_안_바꾸고_사유를_말한다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        editor.AddEpisode("ch01", "b", "b", 0, 400);
        string before = ProjectSnapshotCodec.Encode(editor.Project);

        EpisodeRenamer.Result result = EpisodeRenamer.Rename(editor, "ch01", "b", "root");

        Assert.False(result.Renamed);
        Assert.Contains("이미 있습니다", result.Failure, StringComparison.Ordinal);
        Assert.Equal(before, ProjectSnapshotCodec.Encode(editor.Project));
    }
}
