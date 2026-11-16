using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 장면 메모의 저장 (2026-11-16 · <c>docs/work-orders/scene-notes-orders.md</c> S1). 작가가 장면마다 자유롭게 적는
/// 사정이고, AI가 장면 하나씩 연출 초안을 낼 때 대사와 함께 읽는다. 지키는 것:
/// ① 저장을 왕복한다 ② 차례가 결정적이다(장면이 처음 나오는 차례, 주인 없는 메모는 뒤에 열쇠 차례) ③ 빈 메모는 쓰지 않는다
/// ④ 메모가 없는 옛 프로젝트는 열고 저장해도 그대로다 ⑤ 같은 장면의 메모가 둘인 파일은 읽지 않는다.
/// </summary>
public sealed class ChapterSceneNoteTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-scene-notes", Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

    public ChapterSceneNoteTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static ChapterEpisode Episode(string id, string? sceneId) =>
        new(id, "", "", $"Story_{id}", 0, 0, Memo: null) { SceneId = sceneId };

    /// <summary>장면 「복도」 · 「교실」 둘과 장면을 안 나눈 에피소드 하나 — 에피소드 차례로 복도가 먼저 나온다.</summary>
    private static ChapterDocument Chapter(params ChapterSceneNote[] notes)
    {
        var chapter = new ChapterDocument { ChapterId = "ch01" };
        chapter.Episodes.Add(Episode("ep01", "복도"));
        chapter.Episodes.Add(Episode("ep02", "교실"));
        chapter.Episodes.Add(Episode("ep03", "복도"));
        chapter.Episodes.Add(Episode("ep04", null));
        chapter.SceneNotes.AddRange(notes);
        return chapter;
    }

    private string Save(ChapterDocument chapter)
    {
        var project = new StoryProject { Title = "장면 메모" };
        project.Chapters.Add(chapter);
        ProjectStore.Save(ManifestPath, project);
        return File.ReadAllText(ManifestPath);
    }

    private static IEnumerable<string> SavedSceneIds(string manifest) =>
        JsonNode.Parse(manifest)!["chapters"]![0]!["sceneNotes"]!.AsArray()
            .Select(note => (string)note!["sceneId"]!);

    [Fact]
    public void 장면_메모는_저장을_왕복한다()
    {
        Save(Chapter(
            new ChapterSceneNote("교실", "마감 전날. 하린: 지침 → 결심\n\"좋아, 처음부터 다시.\""),
            new ChapterSceneNote("__scene_ep04", "장면을 안 나눈 에피소드에도 단다")));

        ChapterDocument read = Assert.Single(ProjectStore.Load(ManifestPath).Project.Chapters);

        Assert.Equal(
            [("교실", "마감 전날. 하린: 지침 → 결심\n\"좋아, 처음부터 다시.\""), ("__scene_ep04", "장면을 안 나눈 에피소드에도 단다")],
            read.SceneNotes.Select(note => (note.SceneId, note.Text)));
        Assert.Equal(read.SceneNotes, read.ToGraphModel("ch01").SceneNotes);
    }

    [Fact]
    public void 장면이_처음_나오는_차례로_쓰고_주인_없는_메모는_뒤에_열쇠_차례로_쓴다()
    {
        string manifest = Save(Chapter(
            new ChapterSceneNote("옥상", "주인 없음 — 장면을 풀었다"),
            new ChapterSceneNote("__scene_ep04", "미지정"),
            new ChapterSceneNote("교실", "교실"),
            new ChapterSceneNote("강당", "주인 없음"),
            new ChapterSceneNote("복도", "복도")));

        Assert.Equal(["복도", "교실", "__scene_ep04", "강당", "옥상"], SavedSceneIds(manifest));
    }

    [Fact]
    public void 목록의_차례가_달라도_같은_바이트다()
    {
        string first = Save(Chapter(new ChapterSceneNote("교실", "가"), new ChapterSceneNote("복도", "나")));
        string second = Save(Chapter(new ChapterSceneNote("복도", "나"), new ChapterSceneNote("교실", "가")));

        Assert.Equal(first, second);
    }

    [Fact]
    public void 빈_메모는_쓰지_않고_메모가_없으면_열쇠도_안_쓴다()
    {
        string manifest = Save(Chapter(new ChapterSceneNote("교실", "  \n "), new ChapterSceneNote("복도", "")));

        Assert.DoesNotContain("sceneNotes", manifest);
        Assert.Empty(Assert.Single(ProjectStore.Load(ManifestPath).Project.Chapters).SceneNotes);
    }

    [Fact]
    public void 메모가_없는_견본을_열고_저장하면_매니페스트가_한_글자도_안_바뀐다()
    {
        // 형식 4 그대로 칸만 더했다(Q2) — 옛 프로젝트는 새 칸을 새로 쓰지 않는다.
        string source = Path.GetFullPath("../../../../../samples/RuntimeSamples/FestivalEve");
        string copy = Path.Combine(_directory, "FestivalEve");
        CopyDirectory(source, copy);
        string manifest = Path.Combine(copy, "festival_eve.vnproject.json");
        string before = File.ReadAllText(manifest);

        Assert.DoesNotContain("sceneNotes", before);

        ProjectStore.Save(manifest, ProjectStore.Load(manifest).Project);

        Assert.Equal(Lf(before), Lf(File.ReadAllText(manifest)));
    }

    [Fact]
    public void 같은_장면의_메모가_둘인_파일은_읽지_않는다()
    {
        string manifest = Save(Chapter(new ChapterSceneNote("교실", "가")));
        File.WriteAllText(ManifestPath, manifest.Replace(
            "\"sceneNotes\": [",
            "\"sceneNotes\": [\n        { \"sceneId\": \"교실\", \"text\": \"나\" },",
            StringComparison.Ordinal));

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ProjectStore.Load(ManifestPath));

        Assert.Contains("교실", error.ToString());
    }

    [Fact]
    public void 주인은_실제_장면ID로_정한다()
    {
        ChapterDocument chapter = Chapter(
            new ChapterSceneNote("교실", "가"), new ChapterSceneNote("옥상", "나"), new ChapterSceneNote("__scene_ep04", "다"));

        Assert.Equal(["옥상"], ChapterSceneNotes.Orphans(chapter.Episodes, chapter.SceneNotes).Select(note => note.SceneId));
        Assert.Null(ChapterSceneNotes.Of([new ChapterSceneNote("교실", " ")], "교실"));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n");

    private static void CopyDirectory(string from, string to)
    {
        foreach (string directory in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(from, to, StringComparison.Ordinal));
        }

        Directory.CreateDirectory(to);

        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(from, to, StringComparison.Ordinal));
        }
    }
}
