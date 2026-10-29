using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>R2 — 자동 진행은 공백 추론이 아니라 간선의 명시적 계약이다.</summary>
public sealed class ChapterAutoAuthoringTests
{
    private readonly ProjectEditor _editor = new(new StoryProject());

    private ChapterGraphModel Model() => _editor.FindChapter("auto")!.ToGraphModel("auto");

    public ChapterAutoAuthoringTests()
    {
        _editor.EnsureChapter("auto");
        _editor.AddEpisode("auto", "ep1", "첫째", 0, 0, sceneId: "scene-a");
        _editor.AddEpisode("auto", "ep2", "둘째", 200, 0, sceneId: "scene-a");
    }

    [Fact]
    public void 올바른_자동_간선은_모델에_담기고_명시적으로_내보낸다()
    {
        _editor.AddEdge("auto", "ep1", "ep2", auto: true);

        ChapterGraphModel model = Model();
        ChapterEdge edge = Assert.Single(model.Edges);
        Assert.True(edge.Auto);
        Assert.Empty(model.Errors);

        ChapterExportResult export = ChapterProgressionExporter.Export(model);
        Assert.False(export.Refused, string.Join(" / ", export.Validation.All.Select(item => item.Message)));

        using JsonDocument json = JsonDocument.Parse(export.Json!);
        JsonElement option = json.RootElement.GetProperty("Nodes")[0].GetProperty("NextOptions")[0];
        Assert.True(option.GetProperty("Auto").GetBoolean());
        Assert.Equal(string.Empty, option.GetProperty("ChoiceLabel").GetString());
    }

    [Fact]
    public void 자동_불변식은_위반마다_서로_다른_진단을_낸다()
    {
        _editor.FindChapter("auto")!.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
        _editor.AddEpisode("auto", "ep3", "셋째", 400, 0, sceneId: "scene-b");
        _editor.AddChapterCondition("auto", "항상", "trust >= 0");

        // 장면을 건너고, 관문·스탯변화·문구를 다 단 자동 길 — 그리고 곁에 다른 길.
        _editor.AddEdge("auto", "ep1", "ep3",
            conditionLabel: "항상", optionLabel: "계속", statChanges: "trust +1", auto: true);
        _editor.AddEdge("auto", "ep1", "ep2", optionLabel: "다른 길");

        ChapterDiagnosticCode[] codes = Model().Errors.Select(item => item.Code).ToArray();

        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasSiblings, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasConditions, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasStatChanges, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeCrossesScene, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasChoiceLabel, codes);
    }
}
