using System.Globalization;
using System.Text;
using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

public sealed class ChapterDeterministicExportTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "vn-deterministic-export", Guid.NewGuid().ToString("N"));
    private string ProjectPath => Path.Combine(_root, "story.vnproject.json");

    public ChapterDeterministicExportTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void 같은_객체를_반복_직렬화하면_바이트와_checksum이_같다()
    {
        ChapterGraphModel chapter = Chapter(orderReversedInMemory: true);

        ChapterExportResult first = ChapterProgressionExporter.Export(chapter, null);
        ChapterExportResult second = ChapterProgressionExporter.Export(chapter, null);

        Assert.Equal(ChapterExportBytes.Encode(first.Json!), ChapterExportBytes.Encode(second.Json!));
        Assert.Equal(first.Checksum, second.Checksum);
        Assert.Equal(64, first.Checksum!.Length);
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            ChapterExportBytes.Sha256("abc"));
    }

    [Fact]
    public void 프로젝트를_저장하고_다시_열어_export해도_바이트가_같다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("reload").Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
        editor.AddEpisode("reload", "ep1", "첫 화", 0, 0);
        editor.AddEpisode("reload", "ep2", "둘째", 200, 0);
        editor.AddEdge("reload", "ep1", "ep2", optionLabel: "다음");

        byte[] first = ChapterExportBytes.Encode(ChapterProgressionExporter.Export(
            editor.FindChapter("reload")!.ToGraphModel("reload"), null).Json!);

        ProjectStore.Save(ProjectPath, editor.Project);
        StoryProject reopened = ProjectStore.Load(ProjectPath).Project;

        byte[] again = ChapterExportBytes.Encode(ChapterProgressionExporter.Export(
            reopened.Chapters.Single(chapter => chapter.ChapterId == "reload").ToGraphModel("reload"), null).Json!);

        Assert.Equal(first, again);
    }

    [Fact]
    public void 문화권과_OS개행은_출력_바이트에_영향을_주지_않는다()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ko-KR");
            string korean = ChapterProgressionExporter.Export(Chapter(true), null).Json!;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            string french = ChapterProgressionExporter.Export(Chapter(true), null).Json!;

            Assert.Equal(korean, french);
            Assert.DoesNotContain('\r', korean);
            Assert.False(ChapterExportBytes.Encode(korean).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ⛔ 2026-10-29(D2)까지 내보내기는 가져온 엑셀 행 번호(`SourceRow`)로 정렬했다. 툴에서 만든 것은
    //    행이 0이라, 가져온 간선과 툴에서 이은 간선이 섞인 에피소드에서 <b>툴 간선이 맨 앞</b>으로 갔고
    //    (화면 순서와 게임 선택지 순서가 갈렸다), 툴 간선끼리는 도착 Id 순이었다. 이제 순서는 챕터에
    //    적힌 목록 그대로다 — 저장하고 다시 열어도 같은 순서이므로 결정성은 그대로다.

    [Fact]
    public void Nodes와_Stats는_챕터에_적힌_순서_그대로다()
    {
        using JsonDocument json = JsonDocument.Parse(
            ChapterProgressionExporter.Export(Chapter(true), null).Json!);

        Assert.Equal(["flag", "trust"], json.RootElement.GetProperty("Stats")
            .EnumerateArray().Select(x => x.GetProperty("Key").GetString()!).ToArray());
        Assert.Equal(["ep1", "ep3", "ep2"], json.RootElement.GetProperty("Nodes")
            .EnumerateArray().Select(x => x.GetProperty("EpisodeId").GetString()!).ToArray());
    }

    [Fact]
    public void NextOptions는_챕터의_간선_순서를_보존한다()
    {
        using JsonDocument json = JsonDocument.Parse(
            ChapterProgressionExporter.Export(Chapter(true), null).Json!);
        JsonElement options = json.RootElement.GetProperty("Nodes")[0].GetProperty("NextOptions");

        Assert.Equal("ep3", options[0].GetProperty("TargetEpisodeId").GetString());
        Assert.Equal("ep2", options[1].GetProperty("TargetEpisodeId").GetString());
    }

    [Fact]
    public void 가져온_간선_뒤에_툴에서_이은_간선은_뒤에_선다()
    {
        // 회귀: 행 번호로 정렬하던 시절에는 툴 간선(행 0)이 가져온 간선(행 8)보다 앞에 섰다.
        ChapterGraphModel chapter = Chapter(false);
        ChapterGraphModel mixed = new(
            chapter.ChapterId, chapter.SourcePath, chapter.Episodes,
            [new ChapterEdge("ep1", "ep2", "가져온 길", null, null),
             new ChapterEdge("ep1", "ep3", "툴에서 이은 길", null, null)],
            chapter.Conditions, chapter.Stats, chapter.Fixtures, chapter.Diagnostics);

        using JsonDocument json = JsonDocument.Parse(ChapterProgressionExporter.Export(mixed, null).Json!);
        JsonElement options = json.RootElement.GetProperty("Nodes")[0].GetProperty("NextOptions");

        Assert.Equal(["가져온 길", "툴에서 이은 길"],
            options.EnumerateArray().Select(option => option.GetProperty("ChoiceLabel").GetString()!).ToArray());
    }

    [Fact]
    public void 출시_기준선은_자동_export와_분리해_같은_바이트로_복사한다()
    {
        string export = ChapterExportService.ExportPathFor(ProjectPath, "ch");
        Directory.CreateDirectory(Path.GetDirectoryName(export)!);
        byte[] expected = ChapterExportBytes.Encode("{\r\n  \"x\": 1\r\n}");
        File.WriteAllBytes(export, expected);

        Assert.True(ChapterReleaseBaseline.Capture(ProjectPath, "ch"));
        Assert.Equal(expected, File.ReadAllBytes(ChapterReleaseBaseline.PathFor(ProjectPath, "ch")));
        Assert.NotEqual(Path.GetDirectoryName(export),
            Path.GetDirectoryName(ChapterReleaseBaseline.PathFor(ProjectPath, "ch")));
    }

    [Theory]
    [InlineData("swap")]
    [InlineData("insert-front")]
    [InlineData("delete-front")]
    public void 기존_OptionIndex를_이동시키는_변경을_찾는다(string mutation)
    {
        string baseline = JsonWithOptions("A", "B");
        string current = mutation switch
        {
            "swap" => JsonWithOptions("B", "A"),
            "insert-front" => JsonWithOptions("X", "A", "B"),
            _ => JsonWithOptions("B")
        };

        Assert.Equal(["ep1"], ChapterReleaseBaseline.FindOrderChanges(baseline, current));
    }

    [Fact]
    public void 끝_추가와_비순서_콘텐츠_수정은_막지_않는다()
    {
        Assert.Empty(ChapterReleaseBaseline.FindOrderChanges(
            JsonWithOptions("A", "B"), JsonWithOptions("A", "B", "C")));

        string changedLabel = JsonWithOptions("A*", "B");
        Assert.Empty(ChapterReleaseBaseline.FindOrderChanges(JsonWithOptions("A", "B"), changedLabel));
    }

    [Fact]
    public void 출시_기준선_뒤_순서_변경은_기존_export를_보존하고_이유를_말한다()
    {
        var service = new ChapterExportService();
        ChapterGraphModel original = Chapter(true);

        ChapterExportRun first = service.ExportAll(
            [new ChapterEntry("ch", original)], ProjectPath);
        Assert.True(first.AllExported, first.Notice);
        Assert.True(ChapterReleaseBaseline.Capture(ProjectPath, "ch"));
        byte[] released = File.ReadAllBytes(ChapterExportService.ExportPathFor(ProjectPath, "ch"));

        ChapterGraphModel swapped = new(
            original.ChapterId, original.SourcePath, original.Episodes,
            original.Edges.Reverse().ToArray(),
            original.Conditions, original.Stats, original.Fixtures, original.Diagnostics);
        ChapterExportRun run = service.ExportAll(
            [new ChapterEntry("ch", swapped)], ProjectPath);

        Assert.False(run.AllExported);
        Assert.Contains(run.Blocked, item => item.StartsWith("ch(ep1)", StringComparison.Ordinal));
        Assert.Contains("OptionIndex", run.Notice);
        Assert.Equal(released, File.ReadAllBytes(ChapterExportService.ExportPathFor(ProjectPath, "ch")));
    }

    private static string JsonWithOptions(params string[] labels) => JsonSerializer.Serialize(new
    {
        Nodes = new[] { new { EpisodeId = "ep1", NextOptions = labels.Select(label => new
        {
            TargetEpisodeId = "to-" + label.TrimEnd('*'), ChoiceLabel = label
        }) } }
    });

    private static ChapterGraphModel Chapter(bool orderReversedInMemory) => new(
        "ch", "",
        orderReversedInMemory
            // 첫 원소는 StartEpisode라는 모델 계약을 지키고, 나머지만 순서를 섞는다.
            ? [Episode("ep1"), Episode("ep3"), Episode("ep2")]
            : [Episode("ep1"), Episode("ep2"), Episode("ep3")],
        [
            new ChapterEdge("ep1", "ep3", "B", null, null),
            new ChapterEdge("ep1", "ep2", "A", null, null)
        ],
        [],
        [
            new ChapterStat("flag", "깃발", 0, 0, 1, ChapterStatType.Bool),
            new ChapterStat("trust", "신뢰", 0, 0, 5)
        ],
        [], []);

    private static ChapterEpisode Episode(string id) =>
        new(id, id, "", id, 0, 0, null);
}
