using System.Text.Json;
using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 식 미정 조건 (2026-11-16 · <c>docs/work-orders/writer-fields-orders.md</c> W1). 작가가 [대본]에서 라벨만 먼저 걸고 개발자가 [구성]에서
/// 식을 나중에 채운다 — 식이 빈 조건은 <b>항상 통과</b>하고(관문이 없는 것과 같다), 검증은 <b>경고</b>로 할 일이 남았다고 말한다.
/// 식이 틀린 것(모르는 스탯 키)은 지금처럼 오류다.
/// </summary>
public sealed class UndecidedConditionTests
{
    private static ChapterGraphModel Chapter(string expression)
    {
        var chapter = new ChapterDocument { ChapterId = "ch01" };
        chapter.Episodes.Add(new ChapterEpisode("ep01", "", "", "ep01", 0, 0, Memo: null));
        chapter.Episodes.Add(new ChapterEpisode("ep02", "", "", "ep02", 0, 0, Memo: null));
        chapter.Edges.Add(new ChapterEdge("ep01", "ep02", "같이 간다", "친해졌을 때", null));
        chapter.Conditions.Add(new ChapterCondition("친해졌을 때", expression, null, Parsed: [], IsValid: false));
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10, ChapterStatType.Int));
        return chapter.ToGraphModel("ch01");
    }

    [Fact]
    public void 식_미정_조건은_항상_열린다()
    {
        ChapterGraphModel model = Chapter("");

        Assert.Equal(ChapterGateVerdict.Open, ChapterGateJudge.Judge(model, "친해졌을 때", [0]));
        Assert.True(model.FindCondition("친해졌을 때")!.IsValid);
    }

    [Fact]
    public void 식_미정은_경고이고_도달성도_막지_않는다()
    {
        ChapterGraphModel model = Chapter("   ");

        ChapterDiagnostic warning = Assert.Single(model.Diagnostics, item => item.Code == ChapterDiagnosticCode.ConditionExpressionBlank);
        Assert.Equal(ChapterDiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("식 미정", warning.Message, StringComparison.Ordinal);
        Assert.False(model.HasErrors);

        ChapterReachabilityResult reach = ChapterReachabilityProver.Prove(model);
        Assert.Contains("ep02", reach.ReachableEpisodeIds);
        Assert.False(reach.HasErrors);
    }

    [Fact]
    public void 식_미정_조건은_관문_없이_내보내진다()
    {
        ChapterExportResult export = ChapterProgressionExporter.Export(Chapter(""));

        Assert.False(export.Refused);
        JsonElement option = JsonDocument.Parse(export.Json!).RootElement.GetProperty("Nodes")[0].GetProperty("NextOptions")[0];
        Assert.Equal(0, option.GetProperty("Conditions").GetArrayLength());
    }

    [Fact]
    public void 틀린_식은_여전히_오류다()
    {
        ChapterGraphModel model = Chapter("모르는키 >= 3");

        Assert.Contains(model.Diagnostics, item => item.Severity == ChapterDiagnosticSeverity.Error);
        Assert.Equal(ChapterGateVerdict.Broken, ChapterGateJudge.Judge(model, "친해졌을 때", [0]));
    }
}
