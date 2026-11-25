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
        _editor.FindChapter("auto")!.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        _editor.AddEpisode("auto", "ep3", "셋째", 400, 0, sceneId: "scene-b");
        _editor.AddChapterCondition("auto", "항상", "trust >= 0");

        // 관문·스탯변화·문구를 다 단 자동 길 — 그리고 곁에 다른 길. 장면을 건너는 것은 규칙이 아니다(2026-11-18).
        // 다른 길을 먼저 잇는다 — 자동 길 뒤에 이으면 자동 길이 선택지로 바뀐다(auto-to-choice · 아래 시험).
        _editor.AddEdge("auto", "ep1", "ep2", optionLabel: "다른 길");
        _editor.AddEdge("auto", "ep1", "ep3",
            conditionLabel: "항상", optionLabel: "계속", statChanges: "trust +1", auto: true);

        ChapterDiagnosticCode[] codes = Model().Errors.Select(item => item.Code).ToArray();

        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasSiblings, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasConditions, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasStatChanges, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasChoiceLabel, codes);
    }

    [Fact]
    public void 자동_길이_있는_에피소드에_두_번째_길을_이으면_자동_길이_빈_문구_선택지가_되고_한_칸이다()
    {
        // 2026-11-23 소유자 — 「다른 칸에 두 번째 길을 이어 놓는 순간, 원래 자동 길을 지우지 않고, 문구가 빈 선택지로 바뀌는 게 맞겠네요. 도착은 그대로
        // 두고요」(auto-to-choice A1). ⛔ 그 전에는 둘이 함께 서서 검증이 AutoEdgeHasSiblings로 짚었다.
        _editor.AddEpisode("auto", "ep3", "셋째", 400, 0, sceneId: "scene-a");
        _editor.AddEdge("auto", "ep1", "ep2", auto: true);
        Assert.True(_editor.HasAutoEdge("auto", "ep1"));
        int notifications = 0;
        _editor.Changed += (_, _) => notifications++;

        _editor.AddEdge("auto", "ep1", "ep3", optionLabel: "다른 길");

        Assert.Equal(1, notifications);
        Assert.False(_editor.HasAutoEdge("auto", "ep1"));
        ChapterGraphModel model = Model();
        Assert.Equal(2, model.Edges.Count);
        ChapterEdge was = model.Edges[0];   // 자리 · 도착 그대로 — 문구가 빈 선택지다
        Assert.Equal(("ep1", "ep2", false), (was.FromEpisodeId, was.ToEpisodeId, was.Auto));
        Assert.True(was.HasNoOptionLabel);
        Assert.Equal("다른 길", model.Edges[1].OptionLabel);

        ChapterDiagnosticCode[] codes = model.Errors.Select(item => item.Code).ToArray();
        Assert.DoesNotContain(ChapterDiagnosticCode.AutoEdgeHasSiblings, codes);
        Assert.Contains(ChapterDiagnosticCode.OptionLabelBlank, codes);   // 문구를 채울 때까지 잠깐 오류다(받아들인다)

        // 문구를 적으면 오류가 걷힌다.
        _editor.SetEdgeRoute("auto", "ep1", "ep2", null, "ep2", "그대로 간다");
        Assert.DoesNotContain(Model().Errors, item => item.Code == ChapterDiagnosticCode.OptionLabelBlank);

        // 되돌리기 — 문구 적기 · 그다음 한 번에 새 길과 바꾼 것이 함께 돌아간다.
        _editor.Undo();
        _editor.Undo();
        ChapterEdge back = Assert.Single(Model().Edges);
        Assert.True(back.Auto);
        Assert.Equal("ep2", back.ToEpisodeId);
    }

    [Fact]
    public void 길이_하나만_남아도_저절로_자동으로_돌아가지_않고_자동_길을_이을_때는_바꾸지_않는다()
    {
        _editor.AddEpisode("auto", "ep3", "셋째", 400, 0, sceneId: "scene-a");
        _editor.AddEdge("auto", "ep1", "ep2", auto: true);
        _editor.AddEdge("auto", "ep1", "ep3", optionLabel: "다른 길");

        _editor.RemoveEdge("auto", "ep1", "ep3", "다른 길");

        ChapterEdge left = Assert.Single(Model().Edges);
        Assert.False(left.Auto);   // 문구를 비울 때만 자동이 된다 — 툴이 저절로 되돌리지 않는다

        // 다른 에피소드의 자동 길은 그대로다.
        _editor.AddEdge("auto", "ep2", "ep3", auto: true);
        _editor.AddEdge("auto", "ep1", "ep3", optionLabel: "또 다른 길");
        Assert.True(_editor.HasAutoEdge("auto", "ep2"));
    }

    [Fact]
    public void 자동_길과_다른_길이_함께_선_옛_프로젝트는_고치는_법을_말한다()
    {
        ChapterDocument chapter = _editor.FindChapter("auto")!;
        _editor.AddEpisode("auto", "ep3", "셋째", 400, 0, sceneId: "scene-a");
        chapter.Edges.Add(new ChapterEdge("ep1", "ep2", null, null, null) { Auto = true });
        chapter.Edges.Add(new ChapterEdge("ep1", "ep3", "다른 길", null, null));

        ChapterDiagnostic sibling = Assert.Single(Model().Errors, item => item.Code == ChapterDiagnosticCode.AutoEdgeHasSiblings);
        Assert.Contains("자동 길에 문구를 적으면 선택지가 됩니다", sibling.Message, StringComparison.Ordinal);
    }
}
