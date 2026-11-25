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
    /// 에피소드의 대사 노드에 재생할 줄이 하나도 없다. 같은 이유로 치명적이다 —
    /// 그 노드가 YarnProject에서 빠져 재생이 시작되지 않는다.
    /// </summary>
    EpisodeSceneEmpty,

    ConditionLabelUndefined,

    /// <summary>
    /// 조건의 식이 비었다 — <b>식 미정</b>, 경고다(2026-11-16 · writer-fields W1). 항상 통과한다(관문이 없는 것과 같다). 작가가 라벨만 먼저
    /// 걸고 개발자가 식을 나중에 채운다. 글자는 있는데 읽어낼 항이 없는 식은 같은 코드의 <b>오류</b>다.
    /// </summary>
    ConditionExpressionBlank,
    ConditionExpressionMalformed,
    StatKeyUnknown,
    StatValueNotInteger,
    StatRangeInvalid,

    /// <summary>
    /// 스탯 키가 영어가 아니다(한글 · 빈칸 · 기호) — 경고다(내보내기를 막지 않는다). 키는 Yarn에 그대로 들어가는 statKey라 영어로 적는다(2026-11-23 소유자 ·
    /// <see cref="StatKeyRule"/>). 새로 만들거나 바꾸는 키는 편집기가 받지 않으므로, 이것은 옛 프로젝트 · 손으로 고친 파일에서 온다.
    /// </summary>
    StatKeyNotEnglish,
    EdgeEndpointUnknown,
    EdgeEndpointBlank,
    EpisodeUnreachable,

    /// <summary>
    /// 시작에서 <b>간선으로 이어지지 않은</b> 에피소드 — 막 만들어 아직 안 이은 것, 또는 그런 것만 부르는 섬(2026-11-17 소유자). 경고다 —
    /// 내보내기를 막지 않고 진행 JSON에도 그대로 실린다(게임에서는 거기 갈 길이 없다 · 런타임 코어도 고아 노드를 거절하지 않는다).
    /// 간선은 있는데 스탯 조건 때문에 못 닿는 것은 설계 버그라 지금처럼 <see cref="EpisodeUnreachable"/>(오류)다.
    /// </summary>
    EpisodeNotConnected,

    /// <summary>아무도 부르지 않는 분기 카드 — 경고(<see cref="ChapterBranchCards"/>).</summary>
    BranchCardOrphaned,

    /// <summary>도달할 수 없는 곳만 부르는 분기 카드 — 경고(<see cref="ChapterBranchCards"/>).</summary>
    BranchCardUnreachable,

    /// <summary>줄이 하나도 없는 분기 카드 — 오류(<see cref="ChapterBranchCards"/>).</summary>
    BranchCardEmpty,

    /// <summary>분기 카드의 Yarn 노드 이름이 에피소드 · 다른 분기 카드와 겹친다 — 오류(<see cref="ChapterBranchCards"/>).</summary>
    BranchCardNameTaken,

    /// <summary>분기 카드 안에 분기(분기점)가 있다 — 오류(<see cref="ChapterBranchCards"/> · usability UX27).</summary>
    BranchCardBranches,

    /// <summary>
    /// 이 챕터의 Yarn 노드 이름(에피소드의 대사 노드 · 분기 카드)이 <b>다른 챕터</b>의 것과 겹친다 — 오류(<see cref="ChapterYarnNames"/>,
    /// 2026-11-17). 게임은 모든 챕터의 Yarn을 한 프로젝트로 컴파일한다.
    /// </summary>
    YarnNodeNameTaken,
    OptionEdgeMismatch,


    /// <summary>
    /// 진행 코어가 이 챕터를 <b>싣지 못한다</b>. 검증은 통과했지만 산출물을
    /// 실제 소비자에게 먹여 보니 거부했다는 뜻이고, 그대로 내보내면 게임에서 그 챕터가
    /// 시작되지 않는다.
    /// </summary>
    CoreRefusedChapter,

    /// <summary>
    /// 간선에 `선택지` 문구가 없다. **모든 길은 선택지다** —
    /// 문구 없이 넘어가는 "보이지 않는 기본"은 두지 않는다.
    /// </summary>
    OptionLabelBlank,

    AutoEdgeHasSiblings,
    AutoEdgeHasConditions,
    AutoEdgeHasStatChanges,
    AutoEdgeHasChoiceLabel,

    /// <summary>
    /// 한 장면에 밖에서 들어오는 자리가 둘 이상이다.
    ///
    /// 그 자리가 롤백이 되돌아갈 곳이고 이어하기가 재개할 곳이라, 둘이면 어느 쪽으로
    /// 되돌아갈지가 정해지지 않는다. 코어의 <c>VerifySceneEntries</c>와 같은 규칙이고
    /// 내보내기 관문도 같은 이유로 거부한다 — 여기서는 <b>더 일찍</b> 말할 뿐이다.
    /// </summary>
    SceneHasManyEntries,

    /// <summary>
    /// 도달성 <b>증명이 상한에서 멈췄다</b>. 챕터에 대한 알림 하나다.
    ///
    /// 이것이 <b>보증의 성격이 바뀌었다는 신호</b>다. 이 코드가 있으면 같은 실행의
    /// <see cref="EpisodeUnreachable"/>은 「없음을 증명했다」가 아니라 「못 찾았다」다.
    /// </summary>
    ReachabilityExplorationIncomplete,

    /// <summary>
    /// <b>주인 없는 장면 메모</b> — 그 장면ID를 가진 에피소드가 없다(마지막 에피소드를 지웠거나 옮겼거나, 다른 장면으로
    /// 합쳤다). 경고다 — 내보내기를 막지 않는다. 메모를 조용히 지우지 않으므로 사람이 챕터 인스펙터에서 지우거나 장면을 되살린다.
    /// </summary>
    SceneNoteOrphaned,

    /// <summary>
    /// 그 챕터의 대사에 나오는 화자에게 <b>캐릭터 키가 없다</b> — 그 화자의 대사에는 초상화 · 표정이 붙지 않는다. 경고다 — 내보내기를 막지
    /// 않는다(<see cref="ChapterSpeakerKeys"/> · 2026-11-18 writer-comfort WC6).
    /// </summary>
    SpeakerCharacterKeyMissing,

    /// <summary>
    /// <b>나갈 길이 없는 고리</b> — 들어오면 어떤 선택지를 골라도 그 안만 돌아 챕터를 끝낼 수 없다(자동 길만으로 된 고리는 묻지도 않고 끝없이
    /// 돈다). 경고다 — 내보내기를 막지 않는다(2026-11-23 소유자 — 「네 검증 보고에 그 경고만 넣어주십시오」 · 도달성 증명이 걸은 (에피소드, 스탯)
    /// 상태에서 잰다 — <see cref="ChapterReachabilityProver"/>). 고를 수 있는 선택지가 없는 자리는 게임이 챕터를 끝내므로 막다른 곳은 여기 들지 않는다.
    /// </summary>
    LoopWithoutExit,

    /// <summary>
    /// 대사 글의 <c>{stat("키")}</c>가 <b>그 챕터에 없는 스탯</b>을 읽는다 — 게임에서 그 줄이 나오면 없는 스탯을 읽는다. 경고다 — 내보내기를 막지
    /// 않는다(<see cref="ChapterStatReads"/> · 2026-11-25 소유자 — 「🟡 경고로 짚기」 · stat-read-rename S3).
    /// </summary>
    StatReadUnknown
}

/// <summary>
/// 챕터에서 발견한 것 하나. "어딘가 잘못됐다"는 보고는 실패다 — 메시지가 <b>대상</b>(에피소드·간선·조건
/// 이름)을 짚는다(마스터 플랜 §4).
/// </summary>
/// <param name="FilePath">어느 챕터인가 — 챕터 Id.</param>
/// <param name="Sheet">챕터 안의 어느 자리인가 — 에피소드·간선·조건·스탯(<see cref="ChapterSheetNames"/>).</param>
/// <param name="EpisodeId">
/// 이 진단이 짚는 <b>에피소드</b> — 에피소드 자리의 진단에만 있다. 챕터 그래프가 카드에 오류 표식을 다는
/// 근거다(<see cref="ChapterGraphModel.EpisodeHasError"/>).
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
    /// 고치는 법 — 메시지 아래에 따로 서는 여러 줄 글(예시 포함). 없으면 null. 지금은 식이 성립하지 않는 조건만 단다
    /// (<see cref="ConditionExpressionHelp"/> · 2026-11-23 소유자 — 「검증 보고쪽에 식이 성립하지 않는 다고 띄워주고, 구체적인 예시까지 보기좋게 설명」).
    /// <see cref="Describe"/>에는 들지 않는다 — 상태줄은 한 줄이다.
    /// </summary>
    public string? Help { get; init; }

    /// <summary>
    /// "챕터 · 자리 — 메시지" 한 줄. 상태줄·목록이 그대로 쓴다.
    ///
    /// 무엇을 고칠지는 메시지가 대상(에피소드·간선·조건 이름)으로 말한다.
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
