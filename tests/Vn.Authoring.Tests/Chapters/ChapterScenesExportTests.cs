using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 장면 단위 내보내기 <c>exported/{챕터}.scenes.json</c> (2026-11-16 · <c>docs/work-orders/scene-notes-orders.md</c> S5). AI가 장면 하나를
/// 파일 하나로 읽는다 — 장면 메모, 에피소드마다 지시와 살아 있는 대사(LineId · 화자 · 글). 지키는 것: 모양 · 차례 · 결정성(같은 입력 → 같은
/// 바이트) · 진행 JSON과 같은 때 나가고 지운 챕터의 파일은 걷힌다 · 진행 JSON은 그대로다.
/// </summary>
public sealed class ChapterScenesExportTests : IDisposable
{
    private const string Ch = "festival_eve";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "vn-scenes-export", Guid.NewGuid().ToString("N"));

    public ChapterScenesExportTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string SampleManifest =>
        Path.GetFullPath("../../../../../samples/RuntimeSamples/FestivalEve/festival_eve.vnproject.json");

    private static ProjectEditor Sample() => new(ProjectStore.Load(SampleManifest).Project);

    private static ChapterGraphModel Model(ProjectEditor editor) => editor.FindChapter(Ch)!.ToGraphModel(Ch);

    private static JsonElement Export(ProjectEditor editor) =>
        JsonDocument.Parse(ChapterScenesExporter.Export(Model(editor), editor.Project)).RootElement.Clone();

    [Fact]
    public void 분기_카드의_대사는_부르는_에피소드_아래_branches로_든다()
    {
        // 분기 카드 대사에도 연출이 붙는다(2026-11-17 소유자 — ked-scenes/2). 분기 카드는 에피소드가 아니라 episodes에 없다.
        ProjectEditor editor = Sample();
        JsonElement root = Export(editor);
        JsonElement[] episodes = root.GetProperty("scenes").EnumerateArray()
            .SelectMany(scene => scene.GetProperty("episodes").EnumerateArray())
            .ToArray();

        Assert.Equal("ked-scenes/2", root.GetProperty("format").GetString());
        Assert.DoesNotContain(episodes, episode => episode.GetProperty("episodeId").GetString() == "Festival_Wrapup_Remade");

        JsonElement wrapup = episodes.Single(episode => episode.GetProperty("episodeId").GetString() == "Festival_Wrapup");
        JsonElement[] branches = wrapup.GetProperty("branches").EnumerateArray().ToArray();
        Assert.Equal(["Festival_Wrapup_Remade", "Festival_Wrapup_Sticker"], branches.Select(branch => branch.GetProperty("name").GetString()));

        DialogueNode remade = EpisodeNaming.BoardOf(editor.Project, Ch)!.Nodes.OfType<DialogueNode>().Single(node => node.Name == "Festival_Wrapup_Remade");
        Assert.Equal(
            remade.Script.ActiveLines.Select(line => line.Id),
            branches[0].GetProperty("lines").EnumerateArray().Select(line => line.GetProperty("lineId").GetString()));

        // 분기 카드를 부르지 않는 에피소드는 branches를 쓰지 않는다(빈 칸은 쓰지 않는다).
        Assert.False(episodes.Single(episode => episode.GetProperty("episodeId").GetString() == "Festival_Classroom")
            .TryGetProperty("branches", out _));
        Assert.Equal(5, episodes.Sum(episode => episode.TryGetProperty("branches", out JsonElement found) ? found.GetArrayLength() : 0));
    }

    [Fact]
    public void 장면마다_메모와_에피소드_지시와_살아_있는_대사가_하이어아키_차례로_든다()
    {
        ProjectEditor editor = Sample();
        ChapterGraphModel model = Model(editor);
        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(model);
        ChapterScene first = scenes[0];
        ChapterEpisode firstEpisode = first.Episodes[0];

        editor.SetSceneNote(Ch, first.SceneId, "마감 전날.\n하린: 지침 → 결심");
        editor.UpdateEpisodeMemo(Ch, firstEpisode.EpisodeId, "하린을 먼저 세운다");
        JsonElement root = Export(editor);

        Assert.Equal(ChapterScenesExporter.Format, root.GetProperty("format").GetString());
        Assert.Equal(Ch, root.GetProperty("chapterId").GetString());
        Assert.Equal(model.DisplayName ?? Ch, root.GetProperty("displayName").GetString());
        Assert.False(root.TryGetProperty("orphanNotes", out _));

        JsonElement[] exported = root.GetProperty("scenes").EnumerateArray().ToArray();
        Assert.Equal(scenes.Select(scene => scene.SceneId), exported.Select(scene => scene.GetProperty("sceneId").GetString()));

        JsonElement head = exported[0];
        Assert.Equal(first.DisplayName, head.GetProperty("displayName").GetString());
        Assert.Equal(first.RootEpisodeId, head.GetProperty("rootEpisodeId").GetString());
        Assert.Equal("마감 전날.\n하린: 지침 → 결심", head.GetProperty("note").GetString());
        Assert.False(exported[1].TryGetProperty("note", out _));   // 빈 칸은 쓰지 않는다

        JsonElement[] episodes = head.GetProperty("episodes").EnumerateArray().ToArray();
        Assert.Equal(first.Episodes.Select(episode => episode.EpisodeId), episodes.Select(item => item.GetProperty("episodeId").GetString()));
        Assert.Equal(firstEpisode.ShownTitle, episodes[0].GetProperty("title").GetString());
        Assert.Equal("하린을 먼저 세운다", episodes[0].GetProperty("note").GetString());

        // 줄은 그 카드 대본의 살아 있는 줄 그대로 — LineId · 화자 · 글, 대본 차례.
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Ch, firstEpisode.EpisodeId)!;
        JsonElement[] lines = episodes[0].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(card.Script.ActiveLines.Select(line => line.Id), lines.Select(line => line.GetProperty("lineId").GetString()));
        Assert.NotEmpty(lines);

        foreach ((JsonElement line, Vn.Authoring.Script.ScriptLine source) in lines.Zip(card.Script.ActiveLines))
        {
            Vn.Authoring.Script.LocalizedLine text = card.Script.Text(source.Id);
            Assert.Equal(text.Text, line.GetProperty("text").GetString());
            Assert.Equal(
                string.IsNullOrEmpty(text.Speaker) ? null : text.Speaker,
                line.TryGetProperty("speaker", out JsonElement speaker) ? speaker.GetString() : null);
        }
    }

    [Fact]
    public void 장면_displayName은_작가가_붙인_이름이고_비면_장면ID다()
    {
        // display-names N5 — AI가 사람이 붙인 이름으로 장면을 읽는다. 열쇠(sceneId)는 그대로 ID다.
        ProjectEditor editor = Sample();
        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(Model(editor));
        editor.SetSceneName(Ch, scenes[0].SceneId, "마감 전날의 교실");
        editor.SetSceneName(Ch, "옥상", "옥상(풀림)");   // 이름만 남은 주인 없는 항목

        JsonElement root = Export(editor);
        JsonElement[] exported = root.GetProperty("scenes").EnumerateArray().ToArray();

        Assert.Equal((scenes[0].SceneId, "마감 전날의 교실"),
            (exported[0].GetProperty("sceneId").GetString(), exported[0].GetProperty("displayName").GetString()));
        Assert.Equal(scenes[1].SceneId, exported[1].GetProperty("displayName").GetString());
        Assert.False(exported[0].TryGetProperty("note", out _));   // 이름만 — 메모 칸은 없다

        JsonElement orphan = Assert.Single(root.GetProperty("orphanNotes").EnumerateArray());
        Assert.Equal("옥상(풀림)", orphan.GetProperty("name").GetString());
        Assert.False(orphan.TryGetProperty("text", out _));
    }

    [Fact]
    public void 은퇴한_줄은_싣지_않는다()
    {
        ProjectEditor editor = Sample();
        ChapterEpisode episode = Model(editor).Episodes[0];
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Ch, episode.EpisodeId)!;
        Vn.Authoring.Script.ScriptLine retired = card.Script.ActiveLines.First();
        retired.IsRetired = true;   // 은퇴는 대본 통로가 하는 일이다 — 여기서는 결과 모양만 본다

        JsonElement scene = Export(editor).GetProperty("scenes").EnumerateArray()
            .Single(item => item.GetProperty("episodes").EnumerateArray()
                .Any(entry => entry.GetProperty("episodeId").GetString() == episode.EpisodeId));
        JsonElement lines = scene.GetProperty("episodes").EnumerateArray()
            .Single(entry => entry.GetProperty("episodeId").GetString() == episode.EpisodeId).GetProperty("lines");

        Assert.DoesNotContain(lines.EnumerateArray(), line => line.GetProperty("lineId").GetString() == retired.Id);
    }

    [Fact]
    public void 같은_입력이면_같은_바이트다()
    {
        ProjectEditor editor = Sample();
        editor.SetSceneNote(Ch, Model(editor).Episodes[0].SceneId, "메모");
        editor.SetSceneNote(Ch, "옥상", "주인 없음");

        string first = ChapterScenesExporter.Export(Model(editor), editor.Project);

        // 저장하고 다시 열어도(메모 목록의 차례가 저장 규칙대로 바뀌어도) 같다.
        string copy = Path.Combine(_root, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(copy, editor.Project);
        StoryProject reopened = ProjectStore.Load(copy).Project;
        string second = ChapterScenesExporter.Export(reopened.Chapters.Single().ToGraphModel(Ch), reopened);

        Assert.Equal(ChapterExportBytes.Encode(first), ChapterExportBytes.Encode(second));
        Assert.DoesNotContain("\r", first, StringComparison.Ordinal);
        Assert.EndsWith("}\n", first, StringComparison.Ordinal);
    }

    [Fact]
    public void 주인_없는_메모는_orphanNotes에_열쇠_차례로_든다()
    {
        ProjectEditor editor = Sample();
        editor.SetSceneNote(Ch, "옥상", "나");
        editor.SetSceneNote(Ch, "강당", "가");

        JsonElement[] orphans = Export(editor).GetProperty("orphanNotes").EnumerateArray().ToArray();

        Assert.Equal([("강당", "가"), ("옥상", "나")],
            orphans.Select(item => (item.GetProperty("sceneId").GetString(), item.GetProperty("text").GetString())));
    }

    [Fact]
    public void 진행_JSON과_같은_때_나가고_지운_챕터의_장면_파일은_걷힌다()
    {
        // 견본 폴더를 통째로 옮겨 그 자리에서 내보낸다 — 견본의 exported/는 건드리지 않는다.
        string folder = Path.Combine(_root, "FestivalEve");
        CopyDirectory(Path.GetDirectoryName(SampleManifest)!, folder);
        string manifest = Path.Combine(folder, "festival_eve.vnproject.json");
        string progression = File.ReadAllText(ChapterExportService.ExportPathFor(manifest, Ch));

        var editor = new ProjectEditor(ProjectStore.Load(manifest).Project);
        editor.SetSceneNote(Ch, Model(editor).Episodes[0].SceneId, "메모");
        var entry = new ChapterEntry(Ch, Model(editor));
        var service = new ChapterExportService();

        ChapterExportRun run = service.ExportAll([entry], manifest, editor.Project);

        string scenes = ChapterScenesExporter.PathFor(manifest, Ch);
        Assert.Equal(Path.Combine(folder, "exported", "festival_eve.scenes.json"), scenes);
        Assert.Equal(ChapterExportBytes.Encode(ChapterScenesExporter.Export(entry.Model, editor.Project)), File.ReadAllBytes(scenes));
        Assert.Empty(run.ScenesFailed!);
        Assert.Null(run.Notice);

        // 장면 메모는 진행 JSON에 나가지 않는다 — 그대로다.
        Assert.Equal(progression.Replace("\r\n", "\n"), File.ReadAllText(ChapterExportService.ExportPathFor(manifest, Ch)).Replace("\r\n", "\n"));

        // 챕터가 프로젝트에서 사라지면 그 장면 파일도 걷힌다.
        service.ExportAll([], manifest, editor.Project);
        Assert.False(File.Exists(scenes));
    }

    [Fact]
    public void 진행_JSON이_거부된_챕터는_장면_파일도_나가지_않는다()
    {
        // 같은 때 나간다 — exported/가 없으면 아직 아무것도 안 나갔다는 뜻이 그대로 선다(G8).
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "", 0, 0, sceneId: "교실");
        editor.AddEpisode("ch01", "ep02", "", 220, 0, sceneId: "교실");
        editor.AddChapterCondition("ch01", "깨짐", "없는스탯 >= 1");   // 모르는 스탯 — 검증 오류로 거부된다
        editor.SetSceneNote("ch01", "교실", "교실의 사정");
        string manifest = Path.Combine(_root, "p" + ProjectManifestJson.FileExtension);
        var entry = new ChapterEntry("ch01", editor.FindChapter("ch01")!.ToGraphModel("ch01"));

        ChapterExportRun run = new ChapterExportService().ExportAll([entry], manifest, editor.Project);

        Assert.Contains("ch01", run.Refused);
        Assert.False(File.Exists(ChapterScenesExporter.PathFor(manifest, "ch01")));
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);

        foreach (string directory in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(from, to, StringComparison.Ordinal));
        }

        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(from, to, StringComparison.Ordinal));
        }
    }
}
