using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 대사 Yarn이 <b>저절로 나가는 길</b> (<c>docs/work-orders/yarn-output-orders.md</c> Y3) — <see cref="ChapterExportService.ExportAll"/>.
/// 지키는 것: 진행 JSON과 같은 때 <c>exported/yarn/</c>에 장면마다 나간다 · <b>게임 폴더에는 쓰지 않는다</b>(연출 든 Yarn을 덮지 않게) ·
/// 막힌 장면 · 거부된 챕터의 옛 파일은 그대로 · 지운 장면 · 챕터의 파일은 걷히고 남의 챕터 파일은 안 걷는다.
/// </summary>
public sealed class ChapterYarnExportServiceTests : IDisposable
{
    private const string Ch = "festival_eve";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "vn-yarn-export", Guid.NewGuid().ToString("N"));

    public ChapterYarnExportServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string SampleManifest =>
        Path.GetFullPath("../../../../../samples/RuntimeSamples/FestivalEve/festival_eve.vnproject.json");

    /// <summary>견본 폴더를 통째로 옮겨 그 자리에서 내보낸다 — 견본의 exported/는 건드리지 않는다.</summary>
    private (string Manifest, ProjectEditor Editor) Copy()
    {
        string folder = Path.Combine(_root, "FestivalEve");
        CopyDirectory(Path.GetDirectoryName(SampleManifest)!, folder);
        string manifest = Path.Combine(folder, "festival_eve.vnproject.json");

        return (manifest, new ProjectEditor(ProjectStore.Load(manifest).Project));
    }

    private static ChapterEntry Entry(ProjectEditor editor, string chapterId = Ch) =>
        new(chapterId, editor.FindChapter(chapterId)!.ToGraphModel(chapterId));

    private static string[] YarnFiles(string manifest) =>
        Directory.Exists(ChapterYarnExporter.FolderFor(manifest))
            ? Directory.GetFiles(ChapterYarnExporter.FolderFor(manifest)).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray()
            : [];

    [Fact]
    public void 진행_JSON과_함께_장면마다_exported_yarn에_나가고_게임_폴더에는_쓰지_않는다()
    {
        (string manifest, ProjectEditor editor) = Copy();
        string game = Path.Combine(_root, "Game");
        Directory.CreateDirectory(game);
        ChapterEntry entry = Entry(editor);

        ChapterExportRun run = new ChapterExportService().ExportAll([entry], manifest, editor.Project, game);

        IReadOnlyList<ChapterYarnFile> files = ChapterYarnExporter.Export(entry.Model, editor.Project);
        Assert.Equal(files.Select(file => file.FileName).Order(StringComparer.Ordinal), YarnFiles(manifest));
        Assert.Equal(Path.Combine(Path.GetDirectoryName(manifest)!, "exported", "yarn"), ChapterYarnExporter.FolderFor(manifest));

        foreach (ChapterYarnFile file in files)
        {
            Assert.Equal(
                ChapterExportBytes.Encode(file.Text),
                File.ReadAllBytes(Path.Combine(ChapterYarnExporter.FolderFor(manifest), file.FileName)));
        }

        Assert.Equal(files.Count, run.YarnWritten!.Count);
        Assert.Empty(run.YarnProblems!);
        Assert.Null(run.Notice);
        Assert.Contains($"대사 Yarn → exported/yarn/ ({files.Count}개)", run.WhereNotice);

        // 게임 폴더에는 진행 JSON만 — .yarn은 하나도 없다.
        Assert.True(File.Exists(Path.Combine(game, Ch + ChapterExportService.ExportFileSuffix)));
        Assert.Empty(Directory.GetFiles(game, "*.yarn", SearchOption.AllDirectories));
    }

    [Fact]
    public void 막힌_장면은_쓰지_않고_옛_파일을_두며_보고에_적는다()
    {
        (string manifest, ProjectEditor editor) = Copy();
        var service = new ChapterExportService();
        service.ExportAll([Entry(editor)], manifest, editor.Project);

        ChapterEpisode first = Entry(editor).Model.Episodes[0];
        string path = Path.Combine(ChapterYarnExporter.FolderFor(manifest), ChapterYarnExporter.FileNameOf(Ch, first.EffectiveSceneId));
        byte[] before = File.ReadAllBytes(path);

        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Ch, first.EpisodeId)!;
        ScriptLine line = card.Script.ActiveLines.First();
        card.Script.RequireLocale(card.Script.PrimaryLocale).Entries[line.Id] = new LocalizedLine("하린", "값 { 끝");

        ChapterExportRun run = service.ExportAll([Entry(editor)], manifest, editor.Project);

        Assert.Equal(before, File.ReadAllBytes(path));
        string problem = Assert.Single(run.YarnProblems!);
        Assert.StartsWith($"{Ch} · {first.EffectiveSceneId}: ", problem);
        Assert.Contains("대사 Yarn이 나가지 않은 장면이 있습니다", run.Notice);

        // 다른 장면은 그대로 나간다 — 진행 JSON도.
        Assert.Equal(ChapterYarnExporter.Export(Entry(editor).Model, editor.Project).Count - 1, run.YarnWritten!.Count);
        Assert.True(run.AllExported);
    }

    [Fact]
    public void 장면을_옮기면_옛_장면_파일이_걷히고_챕터가_사라지면_그_챕터의_파일이_걷힌다()
    {
        (string manifest, ProjectEditor editor) = Copy();
        var service = new ChapterExportService();
        service.ExportAll([Entry(editor)], manifest, editor.Project);
        Assert.Contains("festival_eve_교실.yarn", YarnFiles(manifest));

        string[] classroom = Entry(editor).Model.Episodes
            .Where(episode => episode.EffectiveSceneId == "교실")
            .Select(episode => episode.EpisodeId)
            .ToArray();
        editor.UpdateEpisodeScenes(Ch, classroom, "미술실");

        ChapterExportRun moved = service.ExportAll([Entry(editor)], manifest, editor.Project);

        Assert.True(moved.AllExported, moved.Notice);
        Assert.DoesNotContain("festival_eve_교실.yarn", YarnFiles(manifest));
        Assert.Contains("festival_eve_미술실.yarn", YarnFiles(manifest));

        service.ExportAll([], manifest, editor.Project);
        Assert.Empty(YarnFiles(manifest));
    }

    [Fact]
    public void 거부된_챕터의_파일은_두고_이름이_접두로_겹치는_남의_챕터_파일을_걷지_않는다()
    {
        (string manifest, ProjectEditor editor) = Copy();

        // 「festival_eve_extra」는 「festival_eve」의 이름을 머리로 품는다 — 파일 이름만 보면 앞 챕터의 것 같다.
        const string extra = "festival_eve_extra";
        editor.EnsureChapter(extra);
        editor.AddEpisode(extra, "ep01", "", 0, 0, sceneId: "교실");
        editor.AddEpisode(extra, "ep02", "", 220, 0, sceneId: "교실");   // 들어오는 길이 없다 — 도달 불가로 거부된다

        string folder = ChapterYarnExporter.FolderFor(manifest);
        Directory.CreateDirectory(folder);
        string kept = Path.Combine(folder, ChapterYarnExporter.FileNameOf(extra, "교실"));
        File.WriteAllText(kept, "// 지난번에 나간 파일\n");

        ChapterExportRun run = new ChapterExportService().ExportAll([Entry(editor), Entry(editor, extra)], manifest, editor.Project);

        Assert.Contains(extra, run.Refused);
        Assert.Equal("// 지난번에 나간 파일\n", File.ReadAllText(kept));
        Assert.Contains("festival_eve_교실.yarn", YarnFiles(manifest));
    }

    [Fact]
    public void 진행_JSON이_거부된_챕터는_대사_Yarn도_나가지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "", 0, 0, sceneId: "교실");
        editor.AddEpisode("ch01", "ep02", "", 220, 0, sceneId: "교실");
        string manifest = Path.Combine(_root, "p" + ProjectManifestJson.FileExtension);

        ChapterExportRun run = new ChapterExportService().ExportAll([Entry(editor, "ch01")], manifest, editor.Project);

        Assert.Contains("ch01", run.Refused);
        Assert.Empty(YarnFiles(manifest));
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
