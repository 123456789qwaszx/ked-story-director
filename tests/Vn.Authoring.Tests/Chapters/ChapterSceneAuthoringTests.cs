using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>R1 — 에피소드의 장면ID가 런타임의 저장·롤백 경계까지 보존되는지 고정한다.</summary>
public sealed class ChapterSceneAuthoringTests
{
    [Fact]
    public void 에피소드는_장면_없이_서지_않고_장면ID를_비울_수_없다()
    {
        // 2026-11-18 · old-shapes O7 — ⛔ 그 전에는 장면을 안 주면 에피소드마다 혼자짜리 기본 장면(`__scene_{Id}`)이 섰다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");

        Assert.Throws<InvalidOperationException>(() => editor.AddEpisode("ch01", "root", "시작", 10, 20, sceneId: " "));
        Assert.Empty(editor.FindChapter("ch01")!.Episodes);

        editor.AddEpisode("ch01", "root", "시작", 10, 20, sceneId: "hall");
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.UpdateEpisode("ch01", "root", sceneId: ""));
        Assert.Equal(revision, editor.Revision);

        editor.UpdateEpisode("ch01", "root", sceneId: "opening");

        ChapterEpisode episode = Assert.Single(editor.FindChapter("ch01")!.ToGraphModel("ch01").Episodes);
        Assert.Equal("opening", episode.SceneId);
    }

    [Fact]
    public void 같은_장면ID는_산출물에서도_같고_장면_진입점은_표시할_수_있다()
    {
        ChapterGraphModel chapter = Chapter(
            Episode("root", "A"), Episode("inside", "A"), Episode("next", "B"),
            new ChapterEdge("root", "inside", "계속", null, null),
            new ChapterEdge("inside", "next", "다음 장면", null, null));

        ChapterExportResult export = ChapterProgressionExporter.Export(chapter);
        Assert.False(export.Refused);

        using JsonDocument document = JsonDocument.Parse(export.Json!);
        JsonElement[] nodes = document.RootElement.GetProperty("Nodes").EnumerateArray().ToArray();
        Assert.Equal("A", nodes.Single(node => node.GetProperty("EpisodeId").GetString() == "root").GetProperty("SceneId").GetString());
        Assert.Equal("A", nodes.Single(node => node.GetProperty("EpisodeId").GetString() == "inside").GetProperty("SceneId").GetString());
        Assert.True(chapter.IsSceneRoot(chapter.FindEpisode("root")!));
        Assert.False(chapter.IsSceneRoot(chapter.FindEpisode("inside")!));
        Assert.True(chapter.IsSceneRoot(chapter.FindEpisode("next")!));
    }

    [Fact]
    public void 같은_장면으로_두_번_들어가면_두번째_간선으로_거부를_돌려준다()
    {
        ChapterGraphModel chapter = Chapter(
            Episode("root", "root"), Episode("a", "shared"), Episode("b", "shared"),
            new ChapterEdge("root", "a", "A로", null, null),
            new ChapterEdge("root", "b", "B로", null, null));

        ChapterExportResult export = ChapterProgressionExporter.Export(chapter);

        Assert.True(export.Refused);
        Assert.Contains(export.Validation.All, item =>
            item.Code == ChapterDiagnosticCode.CoreRefusedChapter &&
            item.Sheet == ChapterSheetNames.Edges &&
            item.Message.Contains("간선 root → b \"B로\"", StringComparison.Ordinal));
    }

    [Fact]
    public void 장면루트로의_재진입은_유효한_순환이다()
    {
        ChapterGraphModel chapter = Chapter(
            Episode("root", "A"), Episode("middle", "B"),
            new ChapterEdge("root", "middle", "나간다", null, null),
            new ChapterEdge("middle", "root", "돌아온다", null, null));

        ChapterExportResult export = ChapterProgressionExporter.Export(chapter);

        Assert.False(export.Refused, string.Join(" / ", export.Validation.All.Select(item => item.Message)));
    }

    private static ChapterEpisode Episode(string id, string sceneId) =>
        new(id, id, string.Empty, 0, 0, null) { SceneId = sceneId };

    private static ChapterGraphModel Chapter(
        ChapterEpisode first,
        ChapterEpisode second,
        params ChapterEdge[] edges) =>
        new("scenes", "scenes", [first, second], edges, [], [], [], []);

    private static ChapterGraphModel Chapter(
        ChapterEpisode first,
        ChapterEpisode second,
        ChapterEpisode third,
        params ChapterEdge[] edges) =>
        new("scenes", "scenes", [first, second, third], edges, [], [], [], []);
}
