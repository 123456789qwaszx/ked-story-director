using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>카드가 가면 그 대본도 간다</b> (2026-10-29 · <c>docs/plans/script-merge.md</c> S1).
///
/// 그 전에는 카드 · 에피소드 · 판을 지워도 대본을 일부러 남겼다 — "되돌리기 한 번이면 카드가 제 대본을 다시 찾는다".
/// 되돌리기는 프로젝트를 통째로 되살리므로 남길 까닭이 없었고, 남은 대본은 저장할 때마다 쌓였다.
/// </summary>
public sealed class ScriptLifecycleTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-script-life", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 카드를_지우면_대본도_빠지고_되돌리면_함께_돌아온다()
    {
        var editor = new ProjectEditor(new StoryProject());
        StoryFile file = editor.AddStoryFile("판");
        DialogueNode card = editor.AddDialogueNode(file.Id, name: "장면");
        string scriptId = card.ScriptId!;

        editor.RemoveNode(card.Id);

        Assert.Null(editor.Project.FindScript(scriptId));

        editor.Undo();

        Assert.NotNull(editor.Project.FindScript(scriptId));
        Assert.Equal(scriptId, editor.Project.FindDialogue(card.Id)!.ScriptId);
    }

    [Fact]
    public void 에피소드를_지우면_카드의_대본도_빠진다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        editor.AddEpisode("ch01", "a", "a", 0, 0);
        string scriptId = EpisodeNaming.CardFor(editor.Project, "ch01", "a")!.ScriptId!;

        Assert.True(EpisodeDeleter.Delete(editor, "ch01", "a").Ok);

        Assert.Null(editor.Project.FindScript(scriptId));
    }

    [Fact]
    public void 챕터를_지우면_판의_대본도_빠진다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.AddStoryFile("낙서판");
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        string scriptId = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!.ScriptId!;

        Assert.True(ChapterDeleter.Delete(editor, projectManifestPath: null, "ch01").Deleted);

        Assert.Null(editor.Project.FindScript(scriptId));
        Assert.Empty(editor.Project.Scripts);
    }

    [Fact]
    public void 열_때_가리키는_카드가_없는_대본은_버린다()
    {
        // 2026-10-29 전에 저장된 프로젝트 — 지운 카드의 대본이 매니페스트에 남아 있다.
        var editor = new ProjectEditor(new StoryProject { Title = "옛 프로젝트" });
        StoryFile file = editor.AddStoryFile("판");
        DialogueNode card = editor.AddDialogueNode(file.Id, name: "남은 카드");
        var leftover = new ScriptDocument(name: "지운 카드의 대본");
        leftover.RequireLocale(leftover.PrimaryLocale);
        editor.Project.Scripts.Add(leftover);

        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        StoryProject reopened = ProjectStore.Load(manifest).Project;

        ScriptDocument kept = Assert.Single(reopened.Scripts);
        Assert.Equal(card.ScriptId, kept.Id);
        Assert.Null(reopened.FindScript(leftover.Id));
    }
}
