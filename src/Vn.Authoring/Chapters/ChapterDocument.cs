using Vn.Authoring.Definition;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>프로젝트가 든 챕터 하나</b> (R-F · 2026-09-16,
/// <c>docs/work-orders/tool-owns-workbooks-orders.md</c> §1.1).
///
/// ⛔ <b>여기가 뒤집기의 나머지 절반이다.</b> R-E까지는 <i>대사 본문</i>의 주인이 옮겨
/// 왔고, 에피소드 구조·간선·선택지·조건·스탯은 아직 <c>chapters/{Id}.xlsx</c>가 쥐고
/// 있었다. 이 타입이 서면서 그 값들이 프로젝트 안으로 들어온다 — 워크북은
/// <c>exported/</c>와 같은 편, 즉 <b>산출물</b>이 된다.
///
/// <b><see cref="ChapterGraphModel"/>과 무엇이 다른가</b> — 그쪽은 <i>펴서 읽는 모양</i>이고 이쪽은
/// <i>저작한 값</i>이다. 그래서 펴기의 부산물 둘이 여기 없다:
/// <list type="bullet">
/// <item><c>SourcePath</c> — 진단이 짚을 자리. 펼 때 준다.</item>
/// <item><c>Diagnostics</c> — 값의 흠. 펼 때마다 다시 본다(<see cref="ModelDiagnostics"/>).</item>
/// </list>
///
/// (2026-10-29 · D2: 엑셀에서 들여온 모델을 받던 <c>From</c>과 화자 시트의 흔적은 엑셀 연동과 함께 걷혔다.)
///
/// ⚠ <b>파생값은 담지 않는다.</b> <c>ChapterCondition.Parsed</c>·<c>IsValid</c>는 식 원문에서
/// 나오므로 <see cref="ToGraphModel"/>이 다시 푼다 — 담아 두면 식을 고쳤을 때 둘이 갈린다.
/// 원문이 정본이라는 규율(§0.5 무해석성)이 저장물에서도 그대로다.
///
/// ⚠ <b><c>SourceRow</c>는 들여온 옛 값의 이력일 뿐이다.</b> 엑셀에서 들여온 값은 원래 행 번호를
/// 들고, 툴에서 만든 것은 0이다. 2026-10-29(D2)부터 순서(내보내기·선택지)는 이 번호가 아니라
/// 챕터에 적힌 목록 순서를 따르고, 진단 문장에도 싣지 않는다.
/// </summary>
public sealed class ChapterDocument
{
    public required string ChapterId { get; set; }

    public List<ChapterEpisode> Episodes { get; init; } = [];

    public List<ChapterEdge> Edges { get; init; } = [];

    public List<ChapterCondition> Conditions { get; init; } = [];

    public List<ChapterStat> Stats { get; init; } = [];

    /// <summary>챕터가 함께 쓰는 문구 사전 — 배선이 아니라 어휘집이다 (v9).</summary>
    public List<ChapterChoiceOption> ChoiceOptions { get; init; } = [];

    /// <summary>재생루트를 눈으로 보기 위한 테스트 데이터. 내보내기에 안 섞인다 (§3.1).</summary>
    public List<ChapterFixture> Fixtures { get; init; } = [];

    /// <summary>
    /// 읽는 쪽이 늘 쓰던 모양으로 낸다 — 내보내기·도달성·프리뷰·그래프 뷰가 전부
    /// <see cref="ChapterGraphModel"/>을 받으므로, 주인이 바뀌어도 그쪽은 그대로다.
    /// </summary>
    /// <param name="sourcePath">진단이 짚을 자리 — 보통 챕터 Id다(엑셀 시절에는 워크북 경로였다).</param>
    /// <param name="definition">
    /// 주면 <b>스탯 검사</b>도 함께 돌린다(<see cref="StatDiagnostics"/>). 리더의 불평과는
    /// 다른 것이다 — 파일의 흠이 아니라 <b>값의 흠</b>이라, 주인이 프로젝트가 된 뒤에도 남는다.
    /// 자동 길 검사(<see cref="ChapterAutoEdgeCheck"/>)는 정의 파일이 없어도 늘 돈다.
    /// </param>
    public ChapterGraphModel ToGraphModel(string sourcePath, GameDefinition? definition = null) => new(
        ChapterId,
        sourcePath,
        Episodes,
        Edges,
        Conditions.Select(Reparse).ToList(),
        Stats,
        Fixtures,
        ModelDiagnostics(sourcePath, definition),
        ChoiceOptions);

    /// <summary>
    /// 저작한 값에 대한 검사 전부 — <b>값의 흠</b>이라 어디서 왔든(툴 · 옛 프로젝트 · 손으로 고친 파일) 봐야 하는 것들이다.
    ///
    /// ⛔ <b>새 검사를 더할 자리가 여기다.</b> 리더 안에만 두면 R-F 뒤에는 <b>툴이 소유한
    /// 챕터에서 조용히 안 돈다</b> — V1이 정확히 그 사고였다(자동 길 다섯이 그렇게 빠졌다).
    /// </summary>
    private IReadOnlyList<ChapterDiagnostic> ModelDiagnostics(string path, GameDefinition? definition) =>
    [
        .. StatDiagnostics(path, definition),
        .. ConditionDiagnostics(path),
        .. ChapterAutoEdgeCheck.Of(Episodes, Edges, path),
        .. ChapterSceneEntryCheck.Of(Episodes, Edges, path),
        .. ChapterStructureCheck.Of(Episodes, Edges, Conditions.Select(Reparse).ToList(), Stats, path)
    ];

    /// <summary>
    /// 스탯에 대한 <b>모델 검사</b> — 옛 <c>ChapterWorkbookReader</c>가 `스탯` 시트를
    /// 읽으며 하던 그 검사다 (R-F · 2026-09-16에 여기로 옮겨 왔다).
    ///
    /// ⚠ <b>왜 따라와야 했나</b>: 리더에 있었던 것은 <i>거기가 유일한 문이어서</i>였지 파일의
    /// 성질을 봐서가 아니다. 둘 다 <b>값</b>을 본다 — 범위가 뒤집혔는가, 정의 파일이 그 스탯을
    /// 아는가. 안 옮기면 뒤집기와 함께 <b>조용히 사라지는</b> 검사가 된다(실제로 그랬다:
    /// 검증 보고의 경고 4건이 통째로 없어졌고 테스트가 그것을 잡았다).
    ///
    /// ⚠ 행 번호는 <see cref="ChapterStat.SourceRow"/>다 — 들여온 값이면 원래 자리를 짚고,
    /// 툴에서 만든 것이면 0이다(짚을 행이 아직 없다).
    /// </summary>
    private IReadOnlyList<ChapterDiagnostic> StatDiagnostics(string path, GameDefinition? definition)
    {
        var diagnostics = new List<ChapterDiagnostic>();

        foreach (ChapterStat stat in Stats)
        {
            if (stat.Minimum > stat.Maximum)
            {
                diagnostics.Add(Stat(
                    ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatRangeInvalid, path, stat,
                    $"스탯 '{stat.Key}'의 최소({stat.Minimum})가 최대({stat.Maximum})보다 큽니다. " +
                    "이 범위는 도달성 증명(G7)의 탐색 경계라 비어 있으면 안 됩니다."));
            }
            else if (stat.Initial < stat.Minimum || stat.Initial > stat.Maximum)
            {
                diagnostics.Add(Stat(
                    ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatRangeInvalid, path, stat,
                    $"스탯 '{stat.Key}'의 초기값({stat.Initial})이 " +
                    $"최소~최대({stat.Minimum}~{stat.Maximum}) 밖입니다."));
            }

            if (definition is not null &&
                !definition.Variables.Any(variable =>
                    string.Equals(variable.Name, stat.Key, StringComparison.Ordinal)))
            {
                diagnostics.Add(Stat(
                    ChapterDiagnosticSeverity.Warning,
                    ChapterDiagnosticCode.StatMissingFromGameDefinition, path, stat,
                    $"스탯 '{stat.Key}'가 game.definition.json에 없습니다. " +
                    "스탯의 원천은 정의 파일입니다(§3.1)."));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// 조건식에 대한 <b>모델 검사</b> — 스탯 검사와 같은 이유로 여기 있어야 한다
    /// (2026-09-17에 뒤늦게 옮겨 왔다).
    ///
    /// ⛔ <b>R-F가 스탯에서 겪은 일이 조건에서 한 번 더 있었다.</b> 이 검사는
    /// <c>ChapterWorkbookReader</c>에만 있었고, 거기 있었던 것은 <i>그때 그것이 유일한
    /// 문이어서</i>였다. 모델이 정본이 된 뒤로 <b>툴에서 만들거나 고친 조건의 흠은 아무도
    /// 안 봤다</b> — 식을 비워도, 모르는 스탯키를 적어도 검증 보고가 조용했다.
    ///
    /// ⚠ 빈 식이 특히 중요하다. 관문은 <see cref="ChapterGateJudge"/>가 <c>Broken</c>으로
    /// 판정해 <b>조용히 열리지는 않지만</b>, 그 사실이 <b>도달 불가</b>로만 번져 보여서
    /// 원인이 어느 조건인지 아무 데도 안 적힌다.
    /// </summary>
    private IReadOnlyList<ChapterDiagnostic> ConditionDiagnostics(string path)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        HashSet<string> keys = Stats.Select(stat => stat.Key).ToHashSet(StringComparer.Ordinal);

        foreach (ChapterCondition condition in Conditions)
        {
            foreach (ConditionParseProblem problem in
                     ConditionExpressionParser.Parse(condition.Expression, keys).Problems)
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error,
                    ChapterDiagnostics.CodeFor(problem.Kind),
                    path,
                    ChapterSheetNames.Conditions,
                    condition.SourceRow > 0 ? condition.SourceRow : null,
                    $"조건 '{condition.Label}' — {problem.Message}"));
            }
        }

        return diagnostics;
    }

    private static ChapterDiagnostic Stat(
        ChapterDiagnosticSeverity severity,
        ChapterDiagnosticCode code,
        string path,
        ChapterStat stat,
        string message) =>
        new(severity, code, path, ChapterSheetNames.Stats,
            stat.SourceRow > 0 ? stat.SourceRow : null, message);

    /// <summary>식 원문에서 해석 결과를 다시 만든다 — 담아 둔 것을 믿지 않는다.</summary>
    private ChapterCondition Reparse(ChapterCondition condition)
    {
        if (condition.Expression.Length == 0)
        {
            return condition with { Parsed = [], IsValid = false };
        }

        ConditionParseResult parsed = ConditionExpressionParser.Parse(
            condition.Expression,
            Stats.Select(stat => stat.Key).ToHashSet(StringComparer.Ordinal));

        return condition with { Parsed = parsed.Terms, IsValid = parsed.IsValid };
    }
}
