using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>카드가 가면 그 대본도 간다</b> (2026-10-29 · <c>docs/plans/script-merge.md</c> S1).
///
/// 그 전에는 카드 · 에피소드 · 판을 지워도 대본을 일부러 남겼다 — "되돌리기 한 번이면 카드가 제 대본을 다시 찾는다".
/// 되돌리기는 프로젝트를 통째로 되살리므로 남길 까닭이 없었고, 남은 대본은 저장할 때마다 쌓였다.
///
/// S2부터는 카드가 대본을 든다 — 카드와 함께 가는 것이 당연해졌다. 그래도 이 약속은 붙들어 둔다.
/// 옛 파일에 남아 있던 고아 대본을 버리는 일은 <see cref="FormatThreeProjectTests"/>가 잰다.
/// </summary>
public sealed class ScriptLifecycleTests
{
    [Fact]
    public void 카드를_지우면_대본도_빠지고_되돌리면_함께_돌아온다()
    {
        var editor = new ProjectEditor(new StoryProject());
        StoryFile file = editor.AddStoryFile("판");
        DialogueNode card = editor.AddDialogueNode(file.Id, name: "장면");
        string lineId = card.Script.ActiveLines.Single().Id;

        editor.RemoveNode(card.Id);

        Assert.Empty(editor.Project.EnumerateScripts());

        editor.Undo();

        Assert.Equal(lineId, editor.Project.FindDialogue(card.Id)!.Script.ActiveLines.Single().Id);
    }

    [Fact]
    public void 에피소드를_지우면_카드의_대본도_빠진다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        editor.AddEpisode("ch01", "a", "a", 0, 0);
        int before = editor.Project.EnumerateScripts().Count();

        Assert.True(EpisodeDeleter.Delete(editor, "ch01", "a").Ok);

        Assert.Equal(before - 1, editor.Project.EnumerateScripts().Count());
        Assert.Null(EpisodeNaming.CardFor(editor.Project, "ch01", "a"));
    }

    [Fact]
    public void 챕터를_지우면_판의_대본도_빠진다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.AddStoryFile("낙서판");
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);

        Assert.True(ChapterDeleter.Delete(editor, projectManifestPath: null, "ch01").Deleted);

        Assert.Empty(editor.Project.EnumerateScripts());
    }
}
