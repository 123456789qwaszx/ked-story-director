namespace Vn.Authoring.Chapters;

public enum ChapterDiagnosticSeverity
{
    /// <summary>읽지 않고 지나친 것을 알리는 기록. 데이터는 유효하다.</summary>
    Info,

    /// <summary>읽기는 했지만 사람이 봐야 한다. 모델은 만들어진다.</summary>
    Warning,

    /// <summary>규격 위반. 모델은 만들되 <see cref="ChapterGraphModel.HasErrors"/>가 선다.</summary>
    Error
}

public enum ChapterDiagnosticCode
{
    EpisodeIdBlank,
    EpisodeIdDuplicated,

    /// <summary>
    /// 에피소드의 대사 노드에 재생할 줄이 하나도 없다 (2026-08-25). 같은 이유로 치명적이다 —
    /// 그 노드가 YarnProject에서 빠져 재생이 시작되지 않는다.
    /// </summary>
    EpisodeSceneEmpty,

    DialogueEntryBlank,
    ConditionLabelUndefined,
    ConditionExpressionBlank,
    ConditionExpressionMalformed,
    StatKeyUnknown,
    StatValueNotInteger,
    StatRangeInvalid,

    StatMissingFromGameDefinition,
    EdgeEndpointUnknown,
    EdgeEndpointBlank,
    EpisodeUnreachable,
    OptionEdgeMismatch,
    ExitIntoLandingCard,

    /// <summary>
    /// `대사엔트리`가 가리키는 대사노드가 <b>그 챕터의 판에 없다</b> (2026-08-23).
    /// 이대로 내보내면 진행 JSON이 존재하지 않는 yarn 노드를 부른다.
    /// </summary>
    DialogueEntryNodeMissing,

    /// <summary>
    /// 진행 코어가 이 챕터를 <b>싣지 못한다</b> (2026-08-23). 검증은 통과했지만 산출물을
    /// 실제 소비자에게 먹여 보니 거부했다는 뜻이고, 그대로 내보내면 게임에서 그 챕터가
    /// 시작되지 않는다.
    /// </summary>
    CoreRefusedChapter,

    /// <summary>
    /// 간선에 `선택지` 문구가 없다 (2026-08-24 규격). **모든 길은 선택지다** —
    /// 문구 없이 넘어가는 "보이지 않는 기본"은 폐지됐다.
    /// </summary>
    OptionLabelBlank,

    AutoEdgeHasSiblings,
    AutoEdgeHasConditions,
    AutoEdgeHasStatChanges,
    AutoEdgeCrossesScene,
    AutoEdgeHasChoiceLabel,

    /// <summary>
    /// 한 장면에 밖에서 들어오는 자리가 둘 이상이다 (V2 · 2026-09-16).
    ///
    /// 그 자리가 롤백이 되돌아갈 곳이고 이어하기가 재개할 곳이라, 둘이면 어느 쪽으로
    /// 되돌아갈지가 정해지지 않는다. 코어의 <c>VerifySceneEntries</c>와 같은 규칙이고
    /// 내보내기 관문도 같은 이유로 거부한다 — 여기서는 <b>더 일찍</b> 말할 뿐이다.
    /// </summary>
    SceneHasManyEntries,

    /// <summary>
    /// 도달성 <b>증명이 상한에서 멈췄다</b> (2026-09-17). 챕터에 대한 알림 하나다.
    ///
    /// ⚠ 이것이 <b>보증의 성격이 바뀌었다는 신호</b>다. 이 코드가 있으면 같은 실행의
    /// <see cref="EpisodeUnreachable"/>은 「없음을 증명했다」가 아니라 「못 찾았다」다.
    /// 전에는 그 차이가 문장 한 줄과 색깔뿐이었고, 도달 불가가 없으면 아무 말도 없었다.
    /// </summary>
    ReachabilityExplorationIncomplete
}

/// <summary>
/// 챕터에서 발견한 것 하나. "어딘가 잘못됐다"는 보고는 실패다 — 메시지가 <b>대상</b>(에피소드·간선·조건
/// 이름)을 짚는다(마스터 플랜 §4).
/// </summary>
/// <param name="FilePath">어느 챕터인가 — 챕터 Id(엑셀 시절에는 워크북 경로였다).</param>
/// <param name="Sheet">챕터 안의 어느 자리인가 — 에피소드·간선·조건·스탯(<see cref="ChapterSheetNames"/>).</param>
/// <param name="EpisodeId">
/// 이 진단이 짚는 <b>에피소드</b> — 에피소드 자리의 진단에만 있다. 챕터 그래프가 카드에 오류 표식을 다는
/// 근거다(<see cref="ChapterGraphModel.EpisodeHasError"/>).
///
/// ⛔ 2026-10-29(L8)까지 이 칸은 엑셀 행 번호(<c>Row</c> = 들여온 값의 <c>SourceRow</c>)였고 표식도 행으로
/// 짝을 지었다. 툴에서 만든 에피소드는 행이 없어 <b>어떤 카드도 붉어지지 않았다</b>.
/// </param>
public sealed record ChapterDiagnostic(
    ChapterDiagnosticSeverity Severity,
    ChapterDiagnosticCode Code,
    string FilePath,
    string? Sheet,
    string? EpisodeId,
    string Message)
{
    /// <summary>
    /// "챕터 · 자리 — 메시지" 한 줄. 상태줄·목록이 그대로 쓴다.
    ///
    /// ⚠ 2026-10-29(D2)까지는 "파일 · 시트 · 4행 · I열"이었다 — 엑셀 좌표다. 엑셀 연동이 걷혀 열 칸이,
    /// L8에 행 칸이 레코드에서 빠졌다. 무엇을 고칠지는 메시지가 대상(에피소드·간선·조건 이름)으로 말한다.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string> { System.IO.Path.GetFileName(FilePath) };

        if (!string.IsNullOrEmpty(Sheet))
        {
            parts.Add(Sheet);
        }

        return $"{string.Join(" · ", parts)} — {Message}";
    }

    public override string ToString() => Describe();
}

/// <summary>
/// 조건식 해석 문제를 <b>진단 코드</b>로 옮기는 규칙 — <b>한 벌이다</b>.
///
/// ⚠ 워크북 리더와 모델 검사가 <b>둘 다</b> 쓴다. 2026-09-17까지는 리더에만 있었고,
/// 그래서 R-F로 모델이 정본이 된 뒤 <b>툴에서 만든 조건의 흠은 아무도 안 봤다</b> —
/// 같은 날 스탯 검사가 겪은 일(*"안 옮기면 조용히 사라지는 검사가 된다"*)이 조건에서
/// 한 번 더 일어나 있었다.
/// </summary>
public static class ChapterDiagnostics
{
    public static ChapterDiagnosticCode CodeFor(ConditionProblemKind kind) => kind switch
    {
        ConditionProblemKind.UnknownStatKey => ChapterDiagnosticCode.StatKeyUnknown,
        ConditionProblemKind.ValueNotInteger => ChapterDiagnosticCode.StatValueNotInteger,
        ConditionProblemKind.Empty => ChapterDiagnosticCode.ConditionExpressionBlank,
        _ => ChapterDiagnosticCode.ConditionExpressionMalformed
    };
}
