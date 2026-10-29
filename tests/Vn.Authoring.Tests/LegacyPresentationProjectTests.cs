using System.Text;
using Vn.Authoring.Definition;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>연출을 담은 옛 프로젝트</b> (2026-10-29 — 연출은 ked-story-compositor로 갔다).
///
/// 이 툴은 연출 노드 · 명령 공급 노드 · 연출 배선 · 발행 결과 · 곡선 같은 연출 데이터를 읽지 않는다. 그대로
/// 저장하면 사라지므로 <b>열 때 원본을 한 번 남긴다</b>(<see cref="ProjectStore.PresentationBackupFolder"/>) —
/// compositor는 그 폴더에서 연출을 가져간다.
/// </summary>
public sealed class LegacyPresentationProjectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vn-legacy-" + Guid.NewGuid().ToString("N"));

    public LegacyPresentationProjectTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "story"));

        Write("project.vnproject.json", """
            {
              "formatVersion": 3,
              "title": "옛 프로젝트",
              "startNode": "nd_dialogue",
              "scripts": [],
              "files": [ { "id": "sf_a", "name": "판", "path": "story/sf_a.vnstory.json" } ],
              "links": [
                { "id": "lk_settings", "kind": "settings", "source": "nd_set", "target": "nd_dialogue", "order": 0 },
                { "id": "lk_supply", "kind": "commandSupply", "source": "nd_supply", "target": "nd_pres", "order": 0 },
                { "id": "lk_pres", "kind": "presentationSupply", "source": "nd_pres", "target": "nd_dialogue", "order": 0 }
              ],
              "easeCurves": [ { "name": "부드럽게", "keys": [] } ],
              "results": "results.vnresults.json"
            }
            """);
        Write("story/sf_a.vnstory.json", """
            {
              "formatVersion": 1,
              "fileId": "sf_a",
              "name": "판",
              "nodes": [
                { "id": "nd_set", "kind": "set", "name": "설정", "layout": { "x": 0, "y": 0 } },
                { "id": "nd_dialogue", "kind": "dialogue", "name": "대사", "layout": { "x": 0, "y": 200 }, "lines": [] },
                { "id": "nd_pres", "kind": "presentation", "name": "연출", "layout": { "x": 300, "y": 200 }, "bindings": [] },
                { "id": "nd_supply", "kind": "commandSupply", "name": "공급", "layout": { "x": 600, "y": 200 } }
              ]
            }
            """);
        Write("results.vnresults.json", """{ "formatVersion": 1, "dialogueResults": [], "presentationResults": [] }""");
        Write(GameDefinition.FileName, """{ "speakers": [], "presentationCommands": [] }""");
    }

    private string ManifestPath => Path.Combine(_root, "project.vnproject.json");

    private string FirstBackup => Path.Combine(_root, ProjectStore.PresentationBackupFolder);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void 연출을_담은_옛_프로젝트는_연출_없이_열리고_원본을_한_번_남긴다()
    {
        ProjectLoadResult loaded = ProjectStore.Load(ManifestPath);

        // 원고만 들어온다 — 연출 노드 · 명령 공급 노드 · 연출 배선은 읽지 않았다.
        Assert.Equal(["nd_set", "nd_dialogue"], loaded.Project.EnumerateNodes().Select(node => node.Id));
        NodeLink settings = Assert.Single(loaded.Project.Links);
        Assert.Equal("lk_settings", settings.Id);

        // 원본은 같은 상대 경로로 남았다 — manifest · StoryFile · 발행 결과 · 게임 정의.
        Assert.Equal(FirstBackup, loaded.PresentationBackupDirectory);

        foreach (string relative in new[]
                 {
                     "project.vnproject.json",
                     "story/sf_a.vnstory.json",
                     "results.vnresults.json",
                     GameDefinition.FileName
                 })
        {
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(_root, relative)),
                File.ReadAllBytes(Path.Combine(FirstBackup, relative)));
        }
    }

    [Fact]
    public void 열기만_하고_다시_열면_같은_원본이라_같은_폴더를_쓴다()
    {
        string? first = ProjectStore.Load(ManifestPath).PresentationBackupDirectory;
        string? second = ProjectStore.Load(ManifestPath).PresentationBackupDirectory;

        Assert.Equal(first, second);
        Assert.False(Directory.Exists(FirstBackup + "-2"));
    }

    [Fact]
    public void 원본이_그사이_바뀌었으면_새_번호로_남긴다()
    {
        ProjectStore.Load(ManifestPath);

        // 옛 툴에서 그 사이 고쳤다 — 먼저 남긴 원본을 덮지 않는다.
        File.WriteAllText(
            ManifestPath,
            File.ReadAllText(ManifestPath).Replace("옛 프로젝트", "옛 프로젝트 (고침)", StringComparison.Ordinal),
            new UTF8Encoding(false));

        ProjectLoadResult again = ProjectStore.Load(ManifestPath);

        Assert.Equal(FirstBackup + "-2", again.PresentationBackupDirectory);
        Assert.Contains("(고침)", File.ReadAllText(Path.Combine(FirstBackup + "-2", "project.vnproject.json")));
        Assert.DoesNotContain("(고침)", File.ReadAllText(Path.Combine(FirstBackup, "project.vnproject.json")));
    }

    [Fact]
    public void 저장하면_연출_데이터가_빠지고_원고는_그대로다()
    {
        ProjectLoadResult loaded = ProjectStore.Load(ManifestPath);

        ProjectStore.Save(ManifestPath, loaded.Project);

        string manifest = File.ReadAllText(ManifestPath);
        Assert.DoesNotContain("easeCurves", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("\"results\"", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("commandSupply", manifest, StringComparison.Ordinal);
        Assert.Contains("lk_settings", manifest, StringComparison.Ordinal);

        string story = File.ReadAllText(Path.Combine(_root, "story", "sf_a.vnstory.json"));
        Assert.DoesNotContain("nd_pres", story, StringComparison.Ordinal);
        Assert.Contains("nd_dialogue", story, StringComparison.Ordinal);

        // 저장한 뒤에 열면 더 남길 것이 없다.
        Assert.Null(ProjectStore.Load(ManifestPath).PresentationBackupDirectory);
    }

    [Fact]
    public void 발행_결과_경로가_프로젝트_밖을_가리키면_따라가지_않는다()
    {
        // 손댄 manifest — `../`로 프로젝트 밖 파일을 가리킨다. 그대로 따라가면 밖의 파일을 읽고,
        // 백업 폴더 밖(`presentation-original/../`)에 쓴다.
        string escapeName = "escape-" + Guid.NewGuid().ToString("N") + ".vnresults.json";
        string outside = Path.Combine(Path.GetDirectoryName(_root)!, escapeName);
        File.WriteAllText(outside, "{}");

        try
        {
            File.WriteAllText(
                ManifestPath,
                File.ReadAllText(ManifestPath).Replace(
                    "\"results.vnresults.json\"", $"\"../{escapeName}\"", StringComparison.Ordinal),
                new UTF8Encoding(false));

            ProjectLoadResult loaded = ProjectStore.Load(ManifestPath);

            Assert.Equal(FirstBackup, loaded.PresentationBackupDirectory);
            Assert.False(File.Exists(Path.Combine(_root, escapeName)));
            Assert.Empty(Directory.GetFiles(FirstBackup, "escape-*", SearchOption.AllDirectories));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void 연출이_없는_프로젝트는_아무것도_남기지_않는다()
    {
        string root = Path.Combine(_root, "clean");
        string manifest = Path.Combine(root, "project.vnproject.json");
        var project = new StoryProject { Title = "새 원고" };
        project.Files.Add(new StoryFile("sf_clean", "판"));
        ProjectStore.Save(manifest, project);

        ProjectLoadResult loaded = ProjectStore.Load(manifest);

        Assert.Null(loaded.PresentationBackupDirectory);
        Assert.False(Directory.Exists(Path.Combine(root, ProjectStore.PresentationBackupFolder)));
    }

    private void Write(string relative, string text) =>
        File.WriteAllText(Path.Combine(_root, relative), text, new UTF8Encoding(false));
}
