using System.Text.Json.Nodes;
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
        string scriptId = card.Script.Id;

        editor.RemoveNode(card.Id);

        Assert.DoesNotContain(editor.Project.EnumerateScripts(), script => script.Id == scriptId);

        editor.Undo();

        Assert.Equal(scriptId, editor.Project.FindDialogue(card.Id)!.Script.Id);
    }

    [Fact]
    public void 에피소드를_지우면_카드의_대본도_빠진다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        editor.AddEpisode("ch01", "a", "a", 0, 0);
        string scriptId = EpisodeNaming.CardFor(editor.Project, "ch01", "a")!.Script.Id;

        Assert.True(EpisodeDeleter.Delete(editor, "ch01", "a").Ok);

        Assert.DoesNotContain(editor.Project.EnumerateScripts(), script => script.Id == scriptId);
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

    [Fact]
    public void 열_때_가리키는_카드가_없는_대본은_버린다()
    {
        // 2026-10-29 전에 저장된 프로젝트 — 지운 카드의 대본이 매니페스트와 script/ 폴더에 남아 있다.
        (string manifest, DialogueNode card) = SaveOneCard();
        AddScriptFile(manifest, "sc_leftover", "지운 카드의 대본");

        StoryProject reopened = ProjectStore.Load(manifest).Project;

        ScriptDocument kept = Assert.Single(reopened.EnumerateScripts());
        Assert.Equal(card.Script.Id, kept.Id);

        // 다음 저장은 매니페스트에서도 뺀다.
        ProjectStore.Save(manifest, reopened);
        Assert.DoesNotContain("sc_leftover", File.ReadAllText(manifest), StringComparison.Ordinal);
    }

    [Fact]
    public void 대본_하나를_두_카드가_가리키면_둘째는_같은_내용의_사본을_든다()
    {
        // 대본을 나눠 쓰던 옛 프로젝트(S2 전) — 카드마다 제 대본을 들어야 한쪽을 고쳐도 다른 쪽이 안 바뀐다.
        (string manifest, DialogueNode card) = SaveOneCard();
        string storyPath = Path.Combine(_directory, "story", "sf_board.vnstory.json");
        JsonObject story = JsonNode.Parse(File.ReadAllText(storyPath))!.AsObject();
        JsonObject twin = story["nodes"]![0]!.DeepClone().AsObject();
        twin["id"] = "nd_twin";
        twin["name"] = "쌍둥이";
        story["nodes"]!.AsArray().Add(twin);
        File.WriteAllText(storyPath, story.ToJsonString());

        StoryProject reopened = ProjectStore.Load(manifest).Project;

        DialogueNode first = reopened.FindDialogue(card.Id)!;
        DialogueNode second = reopened.FindDialogue("nd_twin")!;
        Assert.Equal(card.Script.Id, first.Script.Id);
        Assert.NotEqual(first.Script.Id, second.Script.Id);
        Assert.NotSame(first.Script, second.Script);
        Assert.Equal(
            first.Script.ActiveLines.Select(line => line.Id),
            second.Script.ActiveLines.Select(line => line.Id));   // 줄 신원은 그대로 — 조건 · 출구가 그대로 붙는다
    }

    private (string Manifest, DialogueNode Card) SaveOneCard()
    {
        var project = new StoryProject { Title = "옛 프로젝트" };
        project.Files.Add(new StoryFile("sf_board", "판", "story/sf_board.vnstory.json"));
        var editor = new ProjectEditor(project);
        DialogueNode card = editor.AddDialogueNode("sf_board", name: "남은 카드");

        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        return (manifest, card);
    }

    private void AddScriptFile(string manifest, string scriptId, string name)
    {
        var script = new ScriptDocument(scriptId, name);
        script.RequireLocale(script.PrimaryLocale);
        Directory.CreateDirectory(Path.Combine(_directory, "script"));
        File.WriteAllText(
            Path.Combine(_directory, "script", scriptId + ScriptDocumentJson.FileExtension),
            ScriptDocumentJson.Write(script));

        JsonObject root = JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
        root["scripts"]!.AsArray().Add(new JsonObject
        {
            ["id"] = scriptId,
            ["name"] = name,
            ["path"] = $"script/{scriptId}{ScriptDocumentJson.FileExtension}"
        });
        File.WriteAllText(manifest, root.ToJsonString());
    }
}
