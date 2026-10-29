using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 2026-08-16 소유자 개정(규격 v5)이 들여온 규칙 중 <b>값</b>에 대한 것.
///
/// ① 연산자 &lt; &gt; 개방 ② 스탯 타입(int/bool)과 bool의 true/false 조건
/// ③ 폐지된 <c>cleared:</c>는 고치는 법까지 말하며 거부 ④ 선택지는 챕터 전체의 문구 사전(v9).
///
/// 2026-10-29 · D2: 시트·열·드롭다운·구판 워크북 이행을 재던 테스트는 엑셀 연동과 함께 걷혔다.
/// </summary>
public sealed class ChapterSchemaV5Tests
{
    private readonly ProjectEditor _editor = new(new StoryProject());

    private ChapterDocument Chapter => _editor.FindChapter("ch01")!;

    private ChapterGraphModel Model() => Chapter.ToGraphModel("ch01");

    public ChapterSchemaV5Tests()
    {
        _editor.EnsureChapter("ch01");
        _editor.AddChapterStat("ch01", "trust", "신뢰", initial: 0, minimum: 0, maximum: 10);
        _editor.AddChapterStat("ch01", "anger", "분노", initial: 0, minimum: 0, maximum: 10);

        _editor.AddEpisode("ch01", "ep1", title: "", 0, 0);
        _editor.AddEpisode("ch01", "ep2", title: "", 200, 0);
        _editor.AddEdge("ch01", "ep1", "ep2", optionLabel: "계속");
    }

    // ── 조건식 ──────────────────────────────────────────────────────────────

    [Fact]
    public void 크기_비교_연산자가_조건식으로_해석된다()
    {
        _editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        _editor.AddChapterCondition("ch01", "분노초과", "anger > 5");
        _editor.AddChapterCondition("ch01", "신뢰미달", "trust < 2");

        ChapterGraphModel model = Model();

        Assert.False(model.HasErrors);
        Assert.Equal("trust >= 3", model.FindCondition("신뢰높음")!.Expression);

        ConditionTerm above = Assert.Single(model.FindCondition("분노초과")!.Parsed);
        Assert.Equal(ConditionComparison.Above, above.Comparison);

        ConditionTerm below = Assert.Single(model.FindCondition("신뢰미달")!.Parsed);
        Assert.Equal(ConditionComparison.Below, below.Comparison);
    }

    [Fact]
    public void 복합_조건식은_항마다_풀린다()
    {
        _editor.AddChapterCondition("ch01", "지쳐있음", "trust >= 4; anger <= 2");

        ChapterGraphModel model = Model();

        Assert.False(model.HasErrors);
        Assert.Equal(2, model.FindCondition("지쳐있음")!.Parsed.Count);
    }

    [Fact]
    public void 폐지된_cleared_는_고치는_법까지_말하며_거부한다()
    {
        // 옛 데이터가 들어오는 자리다. 조용히 무시하면 관문이 통째로 사라져 잠긴 길이 열린다 —
        // 짚되, 무엇으로 바꿔야 하는지까지 말한다.
        _editor.AddChapterCondition("ch01", "복도완료", "cleared:ep1");

        ChapterGraphModel model = Model();

        Assert.True(model.HasErrors);
        Assert.False(model.FindCondition("복도완료")!.IsValid);

        string message = Assert
            .Single(model.Diagnostics, item => item.Message.Contains("cleared:"))
            .Message;

        Assert.Contains("폐지", message);
        Assert.Contains("cleared_ep1", message);   // 깃발 이름 제안
        Assert.Contains("스탯변화", message);        // 켜는 자리까지 짚는다
    }

    // ── bool 스탯 ───────────────────────────────────────────────────────────

    [Fact]
    public void bool_스탯의_true_조건은_1과의_같음_비교다()
    {
        _editor.AddChapterStat(
            "ch01", "seen", "프롤로그", initial: 0, minimum: 0, maximum: 1, type: ChapterStatType.Bool);
        _editor.AddChapterCondition("ch01", "프롤로그봄", "seen == true");

        ChapterGraphModel model = Model();

        Assert.False(model.HasErrors);
        Assert.Equal(ChapterStatType.Bool, model.Stats.Single(stat => stat.Key == "seen").Type);

        ConditionTerm term = Assert.Single(model.FindCondition("프롤로그봄")!.Parsed);
        Assert.Equal(ConditionComparison.Exactly, term.Comparison);
        Assert.Equal(1, term.Value);
    }

    [Fact]
    public void bool_스탯에_크기_비교나_증감을_쓰면_오류다()
    {
        // 2026-10-29 · D2 — 이 검사는 옛 리더(`VerifyBoolStatUsage`)에만 있다가 모델 검사
        // (`ChapterStructureCheck`)로 옮겨 왔다. 내보내기 관문(코어 §G4)보다 먼저, 고치는 중에 짚는다.
        _editor.AddChapterStat("ch01", "seen", "프롤로그", type: ChapterStatType.Bool);
        _editor.AddChapterCondition("ch01", "이상한조건", "seen >= 1");
        _editor.UpdateEdge("ch01", "ep1", "ep2", statChanges: "seen +1", matchOptionLabel: "계속");

        ChapterGraphModel model = Model();

        Assert.Contains(model.Errors, item =>
            item.Sheet == ChapterSheetNames.Conditions && item.Message.Contains("bool 스탯"));
        Assert.Contains(model.Errors, item =>
            item.Sheet == ChapterSheetNames.Edges && item.Message.Contains("증감"));
    }

    // ── v9: 선택지는 전역 문구 사전, 간선이 문구를 직접 적는다 ─────────────────

    [Fact]
    public void 선택지_사전은_인덱스와_대본뿐이고_간선이_문구를_직접_적는다()
    {
        var chapter = new ChapterDocument { ChapterId = "v9" };
        chapter.Episodes.Add(new ChapterEpisode("ep1", "", "", "Story_ep1", 0, 0, null));
        chapter.Episodes.Add(new ChapterEpisode("ep2", "", "", "Story_ep2", 200, 0, null));
        chapter.Edges.Add(new ChapterEdge("ep1", "ep2", "왼쪽으로", null, null));
        chapter.Edges.Add(new ChapterEdge("ep1", "ep2", "계속", null, null));
        chapter.Edges.Add(new ChapterEdge("ep2", "ep1", "왼쪽으로", null, null));   // 같은 문구 재사용
        chapter.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
        chapter.ChoiceOptions.Add(new ChapterChoiceOption("10", "왼쪽으로", Memo: null));
        chapter.ChoiceOptions.Add(new ChapterChoiceOption("20", "오른쪽으로", "아직 아무 길도 안 쓴다"));

        ChapterGraphModel model = chapter.ToGraphModel("v9");

        Assert.False(model.HasErrors);

        // 문구는 간선의 값 그대로다 — 파생도 참조도 아니다.
        Assert.Equal(
            ["왼쪽으로", "계속"],
            model.Edges
                .Where(edge => edge.FromEpisodeId == "ep1")
                .Select(edge => edge.OptionLabel));

        // v12 — 문구 없는 길은 없다.
        Assert.DoesNotContain(model.Edges, edge => edge.HasNoOptionLabel);

        // 사전은 챕터 전체의 것이다 — 안 쓰는 낱말이 있어도, 같은 낱말을 여러 길이 써도 좋다.
        // ⚠ `계속`은 간선이 썼지만 사전에는 없다: 사전은 어휘집이지 배선이 아니라서, 거기
        //    없는 문구를 쓴 길도 그대로 산다.
        Assert.Equal(["왼쪽으로", "오른쪽으로"], model.ChoiceOptions.Select(option => option.Text));
        Assert.Equal(2, model.Edges.Count(edge => edge.OptionLabel == "왼쪽으로"));

        // 순서는 간선의 순서 — 적은 순서가 곧 화면에 뜨는 순서다.
        string json = ChapterProgressionExporter.Export(model).Json!;
        Assert.True(
            json.IndexOf("왼쪽으로", StringComparison.Ordinal) <
            json.IndexOf("계속", StringComparison.Ordinal),
            "먼저 적은 길(왼쪽으로)이 뒤 길(계속)보다 먼저 나가야 한다");
    }
}
