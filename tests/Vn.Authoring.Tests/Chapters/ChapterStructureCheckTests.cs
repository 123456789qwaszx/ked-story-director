using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>옛 워크북 리더에만 있던 값 검사가 모델로 옮겨 왔다</b> (2026-10-29 · D2, <see cref="ChapterStructureCheck"/>).
///
/// ⛔ 엑셀 연동을 걷으며 리더째 사라질 뻔했다 — 자동 길 다섯(V1)과 조건 검사가 먼저 겪은 그 사고다.
/// 여기서는 <b>편집기의 문을 지나지 않은 값</b>(들여온 옛 프로젝트 · 손으로 고친 파일)을 직접 지어
/// 검증 보고에 뜨는지 본다. 편집기가 막지 않는 것(빈 문구 · 같은 문구의 갈림)은 편집기로도 지어 본다.
/// </summary>
public sealed class ChapterStructureCheckTests
{
    // ── 에피소드 ────────────────────────────────────────────────────────────

    [Fact]
    public void 겹친_EpisodeId는_오류다()
    {
        ChapterGraphModel model = Chapter(episodes: [Episode("ep1"), Episode("ep1")]).ToGraphModel("ch01");

        Assert.Contains(model.Errors, item => item.Code == ChapterDiagnosticCode.EpisodeIdDuplicated);
    }

    [Fact]
    public void 빈_대사엔트리는_재생할_대상이_없다고_말한다()
    {
        ChapterGraphModel model = Chapter(episodes: [Episode("ep1", dialogueEntry: "")]).ToGraphModel("ch01");

        ChapterDiagnostic error = Assert.Single(model.Errors, item => item.Code == ChapterDiagnosticCode.DialogueEntryBlank);
        Assert.Contains("ep1", error.Message, StringComparison.Ordinal);
    }

    // ── 간선 ────────────────────────────────────────────────────────────────

    [Fact]
    public void 없는_에피소드로_가는_간선은_오류다()
    {
        ChapterGraphModel model = Chapter(
            episodes: [Episode("ep1")],
            edges: [Edge("ep1", "어디에도없다", "간다")]).ToGraphModel("ch01");

        ChapterDiagnostic error = Assert.Single(model.Errors, item => item.Code == ChapterDiagnosticCode.EdgeEndpointUnknown);
        Assert.Contains("어디에도없다", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 관문_라벨이_조건에_없으면_오류다()
    {
        ChapterGraphModel model = Chapter(
            episodes: [Episode("ep1"), Episode("ep2")],
            edges: [Edge("ep1", "ep2", "간다") with { ConditionLabel = "없는조건", VisibleConditionLabel = "또없는조건" }])
            .ToGraphModel("ch01");

        Assert.Equal(2, model.Errors.Count(item => item.Code == ChapterDiagnosticCode.ConditionLabelUndefined));
    }

    [Fact]
    public void 일반_길에_문구가_없으면_오류다()
    {
        // ⛔ 문구 없는 "보이지 않는 기본"은 2026-08-24에 폐지됐다 — 문구 없는 길은 명시한 자동 진행뿐이다.
        //    빈 문구를 자동으로 추측하지 않는다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep1", "복도", 0, 0);
        editor.AddEpisode("ch01", "ep2", "옥상", 220, 0);
        editor.AddEdge("ch01", "ep1", "ep2", optionLabel: string.Empty);

        ChapterGraphModel model = editor.FindChapter("ch01")!.ToGraphModel("ch01");

        Assert.Contains(model.Errors, item => item.Code == ChapterDiagnosticCode.OptionLabelBlank);
    }

    [Fact]
    public void 자동_길은_문구가_없어도_된다()
    {
        ChapterGraphModel model = Chapter(
            episodes: [Episode("ep1"), Episode("ep2")],
            edges: [Edge("ep1", "ep2", null) with { Auto = true }]).ToGraphModel("ch01");

        Assert.DoesNotContain(model.Diagnostics, item => item.Code == ChapterDiagnosticCode.OptionLabelBlank);
    }

    [Fact]
    public void 같은_에피소드에서_같은_문구가_갈리면_경고한다()
    {
        // 플레이어에게는 같은 버튼 둘이고, 문구를 열쇠로 매다는 연출도 한 벌뿐이다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep1", "복도", 0, 0);
        editor.AddEpisode("ch01", "ep2", "옥상", 220, 0);
        editor.AddEpisode("ch01", "ep3", "지하", 220, 150);
        editor.AddEdge("ch01", "ep1", "ep2", optionLabel: "간다");
        editor.AddEdge("ch01", "ep1", "ep3", optionLabel: "간다");

        ChapterGraphModel model = editor.FindChapter("ch01")!.ToGraphModel("ch01");

        ChapterDiagnostic warning = Assert.Single(model.Diagnostics, item => item.Code == ChapterDiagnosticCode.OptionEdgeMismatch);
        Assert.Equal(ChapterDiagnosticSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void 출발_도착_문구가_모두_같은_간선_둘은_오류다()
    {
        ChapterGraphModel model = Chapter(
            episodes: [Episode("ep1"), Episode("ep2")],
            edges: [Edge("ep1", "ep2", "간다"), Edge("ep1", "ep2", "간다")]).ToGraphModel("ch01");

        Assert.Contains(model.Errors, item => item.Code == ChapterDiagnosticCode.OptionEdgeMismatch);
    }

    [Fact]
    public void 챕터에_없는_스탯을_바꾸는_간선은_오류다()
    {
        ChapterGraphModel model = Chapter(
            episodes: [Episode("ep1"), Episode("ep2")],
            edges: [Edge("ep1", "ep2", "간다") with { StatChanges = [new StatDelta("trust", 1)] }]).ToGraphModel("ch01");

        Assert.Contains(model.Errors, item => item.Code == ChapterDiagnosticCode.StatKeyUnknown);
    }

    [Fact]
    public void 견본_챕터에는_구조_흠이_없다()
    {
        // 옛 리더가 이 견본에서 아무 말도 안 했다 — 옮긴 검사가 없는 흠을 지어내지 않는다.
        foreach (string sample in (string[])[Fixtures.SampleChapters.Corridor, Fixtures.SampleChapters.Contract])
        {
            ChapterDocument chapter = Fixtures.SampleChapters.Load(sample);

            Assert.Empty(ChapterStructureCheck.Of(
                chapter.Episodes, chapter.Edges,
                chapter.ToGraphModel(sample).Conditions, chapter.Stats, sample));
        }
    }

    // ── 깃발(bool) 스탯 ─────────────────────────────────────────────────────

    [Fact]
    public void 편집기는_깃발의_범위를_0에서_1로_고정한다()
    {
        // 옛 리더가 하던 일이다 — 최소·최대 칸을 읽지 않고 0·1로 고정했다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");

        ChapterStat added = editor.AddChapterStat("ch01", "met", initial: 5, maximum: 100, type: ChapterStatType.Bool);

        Assert.Equal((0, 1, 1), (added.Minimum, added.Maximum, added.Initial));

        editor.AddChapterStat("ch01", "trust", maximum: 10);
        editor.UpdateChapterStat("ch01", "trust", type: ChapterStatType.Bool);

        ChapterStat changed = editor.FindChapter("ch01")!.Stats.Single(stat => stat.Key == "trust");
        Assert.Equal((0, 1, 0), (changed.Minimum, changed.Maximum, changed.Initial));
    }

    [Fact]
    public void 범위가_어긋난_깃발은_오류다()
    {
        ChapterGraphModel model = Chapter(
            stats: [new ChapterStat("met", "만남", 0, 0, 100, ChapterStatType.Bool)]).ToGraphModel("ch01");

        Assert.Contains(model.Errors, item =>
            item.Code == ChapterDiagnosticCode.StatRangeInvalid && item.Message.Contains("bool", StringComparison.Ordinal));
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static ChapterDocument Chapter(
        IReadOnlyList<ChapterEpisode>? episodes = null,
        IReadOnlyList<ChapterEdge>? edges = null,
        IReadOnlyList<ChapterStat>? stats = null) => new()
    {
        ChapterId = "ch01",
        Episodes = [.. episodes ?? []],
        Edges = [.. edges ?? []],
        Stats = [.. stats ?? []]
    };

    private static ChapterEpisode Episode(string episodeId, string? dialogueEntry = null) =>
        new(episodeId, episodeId, Index: string.Empty, DialogueEntry: dialogueEntry ?? episodeId,
            0, 0, Memo: null);

    private static ChapterEdge Edge(string from, string to, string? label) =>
        new(from, to, label, ConditionLabel: null, LockedMessage: null);
}
