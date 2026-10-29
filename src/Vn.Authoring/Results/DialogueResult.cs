using Vn.Authoring.Model;

namespace Vn.Authoring.Results;

/// <summary>
/// 펼친 줄 하나. 그 순간의 화자·대사를 담아 대본 없이도 읽힌다 — 결과는 어디에서도 수정할 수 없다.
/// </summary>
/// <param name="Revision">펼친 순간 대본 줄의 개정 번호.</param>
/// <param name="BranchExitTargetNodeId">이 줄이 연 갈래가 끝났을 때 이동할 노드.</param>
public sealed record DialogueResultLine(
    int Index,
    string LineId,
    int Revision,
    string CharacterName,
    string Text,
    DialogueResultTransition? Transition = null,
    string? BranchExitTargetNodeId = null,
    IReadOnlyList<DialogueResultTransition>? ExtraTransitions = null,

    /// <summary>
    /// 「조건 분기」 표식이 가리키는 노드 (R7 P-5) — <b>이 줄 앞에서</b> 다녀온다.
    /// 조건이 아니다: 성립하든 말든 다녀오고, 다녀온 곳이 제 첫머리에서 판단한다.
    /// </summary>
    string? DetourTargetNodeId = null)
{
    // ⛔ 줄의 변수 변경(`SetOperations` · `<<set>>`)은 작가 변수와 함께 2026-09-17에 비었고
    //    2026-10-29(L5)에 칸째 걷혔다. 값이 변하는 자리는 챕터 간선의 스탯 변화다.

    /// <summary>
    /// 이 줄 앞에서 일어나는 전환들 — <b>순서가 곧 일어나는 순서</b>다 (2026-08-17).
    ///
    /// Yarn에는 전환만 있는 줄이 없어서(<c>&lt;&lt;endif&gt;&gt;</c>는 대사가 아니다) 블록이
    /// 겹쳐 닫히거나 닫히자마자 다음이 열리면 그 전환들이 전부 다음 대사 줄 앞에 몰린다.
    /// <see cref="Transition"/>은 그 첫 칸이고, 나머지가 <see cref="ExtraTransitions"/>다 —
    /// 옛 결과 파일이 그대로 열리도록 첫 칸의 이름과 자리를 그대로 뒀다.
    /// </summary>
    public IReadOnlyList<DialogueResultTransition> Transitions =>
        Transition is null
            ? Array.Empty<DialogueResultTransition>()
            : [Transition, .. ExtraTransitions ?? Array.Empty<DialogueResultTransition>()];
}

/// <summary>
/// 펼친 순간의 조건·선택 전환. 조건 Id만이 아니라 이름과 식까지 함께 싣는다.
/// </summary>
/// <param name="OptionId">옵션 라벨 전환에만 있다 — 선택지 옵션의 안정 정체성이다.</param>
/// <param name="ExitTargetNodeId">
/// 이 전환이 <b>여는</b> 갈래의 출구 (2026-08-24) — 조건 갈래면 detour, 옵션이면 jump.
///
/// ⚠ 줄에도 <see cref="DialogueResultLine.BranchExitTargetNodeId"/>가 있지만 그것은
/// <b>줄당 하나</b>다. 줄 없는 갈래(대본 끝의 빈 블록)에는 실을 줄 자체가 없어서, 출구가
/// <b>전환에</b> 붙어야 결과 문서가 자기 완결이 된다. 줄에 실린 쪽은 그대로 두었다 —
/// 옛 결과 파일이 그 칸으로 읽히기 때문이다.
/// </param>
public sealed record DialogueResultTransition(
    ConditionTransitionKind Kind,
    string? ConditionId,
    string? ConditionName,
    string? Expression,
    string? OptionId = null,
    string? ExitTargetNodeId = null);

/// <summary>
/// DialogueNode의 작업 상태를 펼친 <b>불변</b> 결과 — 합성기(<c>ResultDocumentComposer</c>)가 읽는
/// 모양이다(카드 글 칸 · 흐름 조각). 저장하지 않는다. (⛔ 이것을 함께 읽던 갈래 고르기 —
/// <c>BranchFlow</c> · 조건 시뮬레이션 — 는 2026-10-29 L5에 걷혔다.)
///
/// ⛔ 발행(<c>vN</c> 고정본 · 내용 해시 · 결과 저장소)은 2026-10-29에 연출과 함께 <c>ked-story-compositor</c>로
/// 갔다. "결과"라는 이름은 그 시절의 것이 남았다.
/// </summary>
public sealed class DialogueResult
{
    public DialogueResult(
        string sourceNodeId,
        string sourceNodeName,
        int sourceScriptRevision,
        string locale,
        IReadOnlyList<DialogueResultLine> lines,
        string? defaultExitTargetNodeId,
        IReadOnlyList<DialogueResultTransition>? trailingTransitions = null)
    {
        TrailingTransitions = trailingTransitions ?? Array.Empty<DialogueResultTransition>();
        SourceNodeId = sourceNodeId;
        SourceNodeName = sourceNodeName;
        SourceScriptRevision = sourceScriptRevision;
        Locale = locale;
        Lines = lines;
        DefaultExitTargetNodeId = defaultExitTargetNodeId;
    }

    /// <summary>이 결과를 낳은 DialogueNode. 추적용이며 결과의 유효성을 좌우하지 않는다.</summary>
    public string SourceNodeId { get; }

    public string SourceNodeName { get; }

    public int SourceScriptRevision { get; }

    public string Locale { get; }

    public IReadOnlyList<DialogueResultLine> Lines { get; }

    /// <summary>
    /// <b>마지막 줄 뒤의 전환들</b> (2026-08-24) — 대사 없는 조건 블록이 대본의 끝일 때.
    /// 결과에 실어야 합성기가 노드를 다시 읽지 않고 문서 끝에서 그 블록을 닫는다.
    /// </summary>
    public IReadOnlyList<DialogueResultTransition> TrailingTransitions { get; }

    public string? DefaultExitTargetNodeId { get; }

    public DialogueResultLine? FindLine(string? lineId)
    {
        return lineId is null
            ? null
            : Lines.FirstOrDefault(line => string.Equals(line.LineId, lineId, StringComparison.Ordinal));
    }

    public bool ContainsLine(string lineId) => FindLine(lineId) is not null;
}
