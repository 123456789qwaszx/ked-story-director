using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>형식 3 프로젝트는 읽어서 옮긴다</b> (2026-10-29 · <c>docs/plans/script-merge.md</c> S-D5 · S-D6).
///
/// 형식 3은 대본이 <c>script/{Id}.vnscript.json</c>에 따로 서고 카드는 그 Id를 가리켰다. 형식 4는 대본이 카드 안에
/// 든다. 옮기기는 기계적이다 — 카드마다 가리키던 대본의 내용을 그대로 넣는다(LineId · 개정 번호 그대로).
///
/// 고정 자료 <c>tests/fixtures/format3-project</c>는 견본(<c>samples/Authoring</c>)의 형식 3 시절 그대로다.
/// 그것을 열어 저장하면 지금 견본과 바이트까지 같아야 한다 — 견본은 도구가 그렇게 옮긴 것이다.
/// </summary>
public sealed class FormatThreeProjectTests : IDisposable
{
    private static readonly string FixtureDirectory =
        Path.GetFullPath("../../../../../tests/fixtures/format3-project");

    private static readonly string SampleDirectory =
        Path.GetFullPath("../../../../../samples/Authoring");

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-format3", Guid.NewGuid().ToString("N"));

    public FormatThreeProjectTests()
    {
        foreach (string source in Directory.EnumerateFiles(FixtureDirectory, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(_directory, Path.GetRelativePath(FixtureDirectory, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
    }

    private string Manifest => Path.Combine(_directory, "project.vnproject.json");

    private string ScriptFolder => Path.Combine(_directory, "script");

    private string StoryPath => Path.Combine(_directory, "story", "chapter01.vnstory.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 열면_카드마다_가리키던_대본을_그대로_든다()
    {
        // 줄 개정 번호와 은퇴한 줄도 그대로 온다 — compositor가 개정 번호를 발행 결과에 싣고, 글 반영이 은퇴한 줄을 되살린다.
        EditScriptFile("sc_chapter01", root =>
        {
            root["lines"]![1]!["revision"] = 3;
            root["lines"]!.AsArray().Add(new JsonObject { ["id"] = "ln_gone", ["revision"] = 2, ["retired"] = true });
        });

        StoryProject project = ProjectStore.Load(Manifest).Project;

        ScriptDocument scene = project.FindDialogue("nd_scene")!.Script;
        Assert.Equal(
            ["ln_a1", "ln_a2", "ln_b1", "ln_b2", "ln_c1", "ln_c2", "ln_d1", "ln_d2"],
            scene.ActiveLines.Select(line => line.Id));
        Assert.Equal(3, scene.FindLine("ln_a2")!.Revision);
        Assert.True(scene.FindLine("ln_gone")!.IsRetired);
        Assert.Equal(new LocalizedLine("라루", "3분의 1이요?"), scene.Text("ln_a2"));

        Assert.Equal(new LocalizedLine("라루", "그럼 계약서에 서명하죠."), project.FindDialogue("nd_good")!.Script.Text("ln_g1"));
        Assert.Equal(4, project.EnumerateScripts().Count());
    }

    [Fact]
    public void 열기만_해서는_아무것도_지우지_않는다()
    {
        ProjectStore.Load(Manifest);

        Assert.Equal(4, Directory.EnumerateFiles(ScriptFolder).Count());
    }

    [Fact]
    public void 저장하면_형식_4가_되고_옛_대본_파일이_지워진다()
    {
        ProjectStore.Save(Manifest, ProjectStore.Load(Manifest).Project);

        Assert.False(Directory.Exists(ScriptFolder));

        // 지금 견본은 도구가 형식 3 견본을 이렇게 옮긴 것이다 — 바이트까지 같다.
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(SampleDirectory, "project.vnproject.json")),
            File.ReadAllBytes(Manifest));
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(SampleDirectory, "story", "chapter01.vnstory.json")),
            File.ReadAllBytes(StoryPath));

        // 옮긴 프로젝트는 다시 열린다.
        Assert.Equal(4, ProjectStore.Load(Manifest).Project.EnumerateScripts().Count());
    }

    [Fact]
    public void 매니페스트가_가리키지_않던_파일은_남긴다()
    {
        // 지우는 일은 옛 매니페스트가 가리키던 대본 파일만 한다 — 사람이 둔 파일은 모른다.
        string note = Path.Combine(ScriptFolder, "메모.txt");
        File.WriteAllText(note, "남겨 둘 것");

        ProjectStore.Save(Manifest, ProjectStore.Load(Manifest).Project);

        Assert.Equal([note], Directory.EnumerateFiles(ScriptFolder));
    }

    [Fact]
    public void 가리키는_카드가_없는_대본은_버리고_그_파일도_지운다()
    {
        // 2026-10-29 전에 저장된 프로젝트 — 지운 카드의 대본이 매니페스트와 script/ 폴더에 남아 있다.
        File.Copy(Path.Combine(ScriptFolder, "sc_good.vnscript.json"), Path.Combine(ScriptFolder, "sc_leftover.vnscript.json"));
        EditScriptFile("sc_leftover", root => root["scriptId"] = "sc_leftover");
        JsonObject manifest = ReadJson(Manifest);
        manifest["scripts"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "sc_leftover",
            ["name"] = "지운 카드의 대본",
            ["path"] = "script/sc_leftover.vnscript.json"
        });
        File.WriteAllText(Manifest, manifest.ToJsonString());

        StoryProject reopened = ProjectStore.Load(Manifest).Project;

        Assert.Equal(4, reopened.EnumerateScripts().Count());

        ProjectStore.Save(Manifest, reopened);

        Assert.DoesNotContain("sc_leftover", File.ReadAllText(Manifest), StringComparison.Ordinal);
        Assert.False(Directory.Exists(ScriptFolder));
    }

    [Fact]
    public void 대본_하나를_두_카드가_가리키면_둘째는_같은_내용의_사본을_든다()
    {
        // 대본을 나눠 쓰던 옛 프로젝트 — 카드마다 제 대본을 들어야 한쪽을 고쳐도 다른 쪽이 안 바뀐다.
        JsonObject story = ReadJson(StoryPath);
        JsonObject twin = story["nodes"]!.AsArray()
            .Single(node => (string?)node!["id"] == "nd_good")!
            .DeepClone().AsObject();
        twin["id"] = "nd_twin";
        twin["name"] = "쌍둥이";
        story["nodes"]!.AsArray().Add(twin);
        File.WriteAllText(StoryPath, story.ToJsonString());

        StoryProject reopened = ProjectStore.Load(Manifest).Project;

        ScriptDocument first = reopened.FindDialogue("nd_good")!.Script;
        ScriptDocument second = reopened.FindDialogue("nd_twin")!.Script;
        Assert.NotSame(first, second);
        Assert.Equal(
            first.ActiveLines.Select(line => line.Id),
            second.ActiveLines.Select(line => line.Id));   // 줄 신원은 그대로 — 조건 · 출구가 그대로 붙는다
        Assert.Equal(first.Text("ln_g1"), second.Text("ln_g1"));
    }

    [Fact]
    public void 가리키는_대본_파일이_없으면_열지_않는다()
    {
        File.Delete(Path.Combine(ScriptFolder, "sc_trust.vnscript.json"));

        Assert.Throws<FileNotFoundException>(() => ProjectStore.Load(Manifest));
    }

    private void EditScriptFile(string scriptId, Action<JsonObject> edit)
    {
        string path = Path.Combine(ScriptFolder, scriptId + ScriptDocumentJson.LegacyFileExtension);
        JsonObject root = ReadJson(path);
        edit(root);
        File.WriteAllText(path, root.ToJsonString());
    }

    private static JsonObject ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}
