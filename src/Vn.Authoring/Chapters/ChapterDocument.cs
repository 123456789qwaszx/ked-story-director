
namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>프로젝트가 든 챕터 하나</b>. 에피소드 구조·간선·선택지·조건·스탯의 주인은 이 타입이고,
/// 그 값들은 프로젝트 안에 산다.
///
/// <b><see cref="ChapterGraphModel"/>과 무엇이 다른가</b> — 그쪽은 <i>펴서 읽는 모양</i>이고 이쪽은
/// <i>저작한 값</i>이다. 그래서 펴기의 부산물 둘이 여기 없다:
/// <list type="bullet">
/// <item><c>SourcePath</c> — 진단이 짚을 자리. 펼 때 준다.</item>
/// <item><c>Diagnostics</c> — 값의 흠. 펼 때마다 다시 본다(<see cref="ModelDiagnostics"/>).</item>
/// </list>
///
/// <b>파생값은 담지 않는다.</b> <c>ChapterCondition.Parsed</c>·<c>IsValid</c>는 식 원문에서
/// 나오므로 <see cref="ToGraphModel"/>이 다시 푼다 — 담아 두면 식을 고쳤을 때 둘이 갈린다.
/// 원문이 정본이라는 규율(§0.5 무해석성)이 저장물에서도 그대로다.
///
/// 순서(내보내기·선택지)는 챕터에 적힌 목록 순서를 따르고, 진단은 에피소드를 Id로 짚는다.
/// </summary>
public sealed class ChapterDocument
{
    public required string ChapterId { get; set; }

    /// <summary>
    /// 사람이 읽는 챕터 이름 — 런타임 진행 JSON의 <c>DisplayName</c>이고, 끝 카드 머리에 뜬다("축제 전날").
    /// 비면 내보내기가 <see cref="ChapterId"/>를 쓴다. Id와 달리 신원이 아니라 글자다 —
    /// 바꿔도 저장 · 간선 · 판이 따라가지 않는다.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>보이는 챕터 이름 — 표시 이름, 비면 <see cref="ChapterId"/>. [대본] 탭(작가 자리)은 이것만 보인다.</summary>
    public string ShownName => ShownNameOf(ChapterId, DisplayName);

    /// <summary>보이는 이름의 규칙 한 자리 — 모델 두 층이 같은 것을 부른다.</summary>
    public static string ShownNameOf(string chapterId, string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? chapterId : displayName.Trim();

    public List<ChapterEpisode> Episodes { get; init; } = [];

    public List<ChapterEdge> Edges { get; init; } = [];

    public List<ChapterCondition> Conditions { get; init; } = [];

    public List<ChapterStat> Stats { get; init; } = [];

    /// <summary>챕터가 함께 쓰는 문구 사전 — 배선이 아니라 어휘집이다 (v9).</summary>
    public List<ChapterChoiceOption> ChoiceOptions { get; init; } = [];

    /// <summary>재생루트를 눈으로 보기 위한 테스트 데이터. 내보내기에 안 섞인다 (§3.1).</summary>
    public List<ChapterFixture> Fixtures { get; init; } = [];

    /// <summary>
    /// 장면 메모 — 장면마다 하나, 열쇠는 <see cref="ChapterEpisode.SceneId"/>. 진행 JSON에 나가지 않는다.
    /// 이 목록의 차례는 뜻이 없다 — 저장할 때 <see cref="ChapterSceneNotes.Ordered"/>가 차례를 정한다.
    /// </summary>
    public List<ChapterSceneNote> SceneNotes { get; init; } = [];

    /// <summary>
    /// 읽는 쪽이 늘 쓰던 모양으로 낸다 — 내보내기·도달성·프리뷰·그래프 뷰가 전부
    /// <see cref="ChapterGraphModel"/>을 받으므로, 주인이 바뀌어도 그쪽은 그대로다.
    /// </summary>
    /// <param name="sourcePath">진단이 짚을 자리 — 보통 챕터 Id다(엑셀 시절에는 워크북 경로였다).</param>
    public ChapterGraphModel ToGraphModel(string sourcePath) => new(
        ChapterId,
        sourcePath,
        Episodes,
        Edges,
        Conditions.Select(Reparse).ToList(),
        Stats,
        Fixtures,
        ModelDiagnostics(sourcePath),
        ChoiceOptions)
    {
        DisplayName = DisplayName,
        SceneNotes = SceneNotes,
    };

    /// <summary>
    /// 저작한 값에 대한 검사 전부 — <b>값의 흠</b>이라 어디서 왔든(툴 · 옛 프로젝트 · 손으로 고친 파일) 봐야 하는 것들이다.
    ///
    /// <b>새 검사를 더할 자리가 여기다.</b> 리더 안에만 두면 R-F 뒤에는 <b>툴이 소유한
    /// 챕터에서 조용히 안 돈다</b> — V1이 정확히 그 사고였다(자동 길 다섯이 그렇게 빠졌다).
    /// </summary>
    private IReadOnlyList<ChapterDiagnostic> ModelDiagnostics(string path) =>
    [
        .. StatDiagnostics(path),
        .. ConditionDiagnostics(path),
        .. ChapterAutoEdgeCheck.Of(Episodes, Edges, path),
        .. ChapterSceneEntryCheck.Of(Episodes, Edges, path),
        .. ChapterStructureCheck.Of(Episodes, Edges, Conditions.Select(Reparse).ToList(), Stats, path),
        .. ChapterSceneNotes.Diagnostics(Episodes, SceneNotes, path)
    ];

    /// <summary>
    /// 스탯에 대한 <b>모델 검사</b>.
    ///
    /// <b>왜 모델에 있나</b>: 파일의 성질이 아니라 <b>값</b>을 본다 — 범위가 뒤집혔는가. 값을 읽는 문 한 곳에만 두면 다른 길로 들어온
    /// 값에서는 <b>조용히 사라지는</b> 검사가 된다.
    ///
    /// ⛔ 「스탯이 game.definition.json에 없다」 경고(<c>StatMissingFromGameDefinition</c>)는 2026-11-17에 걷혔다 — 정의 파일의
    /// <c>variables</c>는 챕터 스탯의 중복이었다. 스탯의 원천은 챕터 하나다(<c>docs/work-orders/definition-split-orders.md</c> S1).
    /// </summary>
    private IReadOnlyList<ChapterDiagnostic> StatDiagnostics(string path)
    {
        var diagnostics = new List<ChapterDiagnostic>();

        foreach (ChapterStat stat in Stats)
        {
            if (stat.Minimum > stat.Maximum)
            {
                diagnostics.Add(Stat(
                    ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatRangeInvalid, path,
                    $"스탯 '{stat.Key}'의 최소({stat.Minimum})가 최대({stat.Maximum})보다 큽니다. " +
                    "이 범위는 도달성 증명(G7)의 탐색 경계라 비어 있으면 안 됩니다."));
            }
            else if (stat.Initial < stat.Minimum || stat.Initial > stat.Maximum)
            {
                diagnostics.Add(Stat(
                    ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatRangeInvalid, path,
                    $"스탯 '{stat.Key}'의 초기값({stat.Initial})이 " +
                    $"최소~최대({stat.Minimum}~{stat.Maximum}) 밖입니다."));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// 조건식에 대한 <b>모델 검사</b> — 스탯 검사와 같은 이유로 여기 있어야 한다.
    /// 여기서 안 보면 <b>툴에서 만들거나 고친 조건의 흠은 아무도 안 본다</b> — 식을 비워도,
    /// 모르는 스탯키를 적어도 검증 보고가 조용하다.
    ///
    /// 빈 식이 특히 중요하다. 관문은 <see cref="ChapterGateJudge"/>가 <c>Broken</c>으로
    /// 판정해 <b>조용히 열리지는 않지만</b>, 그 사실이 <b>도달 불가</b>로만 번져 보여서
    /// 원인이 어느 조건인지 아무 데도 안 적힌다.
    /// </summary>
    private IReadOnlyList<ChapterDiagnostic> ConditionDiagnostics(string path)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        HashSet<string> keys = Stats.Select(stat => stat.Key).ToHashSet(StringComparer.Ordinal);

        foreach (ChapterCondition condition in Conditions)
        {
            // 식 미정 — 항상 통과한다. 할 일이 남았다고 경고로 말한다(내보내기를 막지 않는다).
            if (string.IsNullOrWhiteSpace(condition.Expression))
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Warning,
                    ChapterDiagnosticCode.ConditionExpressionBlank,
                    path,
                    ChapterSheetNames.Conditions,
                    null,
                    $"조건 '{condition.Label}' — 식 미정입니다. [구성] 탭에서 식을 채울 때까지 항상 통과합니다."));
                continue;
            }

            foreach (ConditionParseProblem problem in
                     ConditionExpressionParser.Parse(condition.Expression, keys).Problems)
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error,
                    ChapterDiagnostics.CodeFor(problem.Kind),
                    path,
                    ChapterSheetNames.Conditions,
                    null,
                    $"조건 '{condition.Label}' — {problem.Message}"));
            }
        }

        return diagnostics;
    }

    private static ChapterDiagnostic Stat(
        ChapterDiagnosticSeverity severity,
        ChapterDiagnosticCode code,
        string path,
        string message) =>
        new(severity, code, path, ChapterSheetNames.Stats, EpisodeId: null, message);

    /// <summary>
    /// 식 원문에서 해석 결과를 다시 만든다 — 담아 둔 것을 믿지 않는다.
    ///
    /// <b>식이 빈 조건은 「식 미정」이고 항상 통과한다</b>(유효, 항 없음 — 관문이 없는 것과 같다). 작가가 [대본]에서 라벨만 먼저 걸고
    /// 개발자가 [구성]에서 식을 나중에 채우는 흐름이다(2026-11-16 · <c>docs/work-orders/writer-fields-orders.md</c> W1). 진단은 경고다.
    /// </summary>
    private ChapterCondition Reparse(ChapterCondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.Expression))
        {
            return condition with { Parsed = [], IsValid = true };
        }

        ConditionParseResult parsed = ConditionExpressionParser.Parse(
            condition.Expression,
            Stats.Select(stat => stat.Key).ToHashSet(StringComparer.Ordinal));

        return condition with { Parsed = parsed.Terms, IsValid = parsed.IsValid };
    }
}
