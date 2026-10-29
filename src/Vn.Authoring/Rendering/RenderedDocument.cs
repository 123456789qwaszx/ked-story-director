namespace Vn.Authoring.Rendering;

/// <summary>평면 문서를 이루는 의미 단위.</summary>
public enum RenderedSegmentKind
{
    NodeHeader,
    ConditionBegin,
    ConditionElseIf,
    ConditionEnd,
    DialogueLine,

    /// <summary>선택 블록의 옵션 라벨 하나. Text가 버튼 문구이고, 뒤따르는 조각들이 본문이다.</summary>
    ChoiceOption,

    /// <summary>선택 블록의 끝. 이 조각 자체는 아무 문구도 만들지 않을 수 있다.</summary>
    ChoiceEnd,

    /// <summary>선택지 옵션의 출구. <c>&lt;&lt;jump&gt;&gt;</c> — 고르면 그 씬으로 이동한다.</summary>
    BranchJump,

    /// <summary>
    /// 조건 갈래(IF)의 커스텀 씬 출구. <c>&lt;&lt;detour&gt;&gt;</c> — 씬을 재생하고
    /// 갈래로 <b>돌아와</b> 나머지 대본을 계속한다. jump면 갈래 뒤의 대사가 전부 죽는다.
    /// </summary>
    BranchDetour,
    DefaultJump,
    Warning,
    NodeFooter
}

/// <summary>
/// 같은 결과를 런타임 미리보기용, 대본용, 녹음용, 번역용으로 합성할 때 사용하는 의미 레이어.
/// </summary>
public enum DocumentLayer
{
    Structure,
    Conditions,
    Dialogue,
    ExecutionJumps,
    Diagnostics
}

/// <summary>
/// 합성된 한 Segment가 어느 결과의 어디에서 왔는지.
///
/// 화면에는 평평한 문자열만 보여도 이 참조를 잃지 않는다. Preview 줄 클릭, 로컬라이징,
/// 녹음 대본이 모두 이 연결을 사용한다.
///
/// 프리셋 필터로 다른 Segment가 빠져도 남은 Segment의 참조는 그대로다.
/// </summary>
public sealed record RenderSourceReference(
    string? NodeId = null,
    string? LineId = null,
    string? ConditionId = null);

/// <summary>
/// Formatter에 종속되지 않은 평면 문서 조각.
/// Text는 대사·표시 이름·경고처럼 사람이 읽는 내용이고, Expression/TargetNodeId는
/// Yarn뿐 아니라 다른 출력 형식도 다시 사용할 수 있는 의미 값이다.
/// </summary>
public sealed record RenderedSegment(
    string Id,
    RenderedSegmentKind Kind,
    DocumentLayer Layer,
    RenderSourceReference Source,
    int IndentLevel = 0,
    string? Text = null,
    string? LocalizedText = null,
    string? Speaker = null,
    string? Expression = null,
    string? TargetNodeId = null,
    string? TargetNodeName = null,
    string? OptionId = null,
    int? ChoiceBlockOrdinal = null,
    int? ChoiceOptionIndex = null);

/// <summary>
/// 대사 결과 하나를 평평하게 합성한 결과.
///
/// 파일 저장용 포맷이 아니다. 원본은 프로젝트(대본 + 대사 노드)이고, 이 문서는 그것을 사람이 읽는
/// 표현으로 펼친 것이다. 방향은 언제나 결과 → 문서다 — 되읽는 것은 카드 글 칸의 ScenarioOnly 하나뿐이고
/// 그것은 <c>ScenarioTextParser</c>가 따로 맡는다.
/// </summary>
public sealed class RenderedDocument
{
    public RenderedDocument(
        string sourceNodeId,
        IReadOnlyList<RenderedSegment> segments,
        DocumentOutputOptions? options = null,
        OutputPresetId? presetId = null)
    {
        ArgumentNullException.ThrowIfNull(segments);

        SourceNodeId = sourceNodeId;
        Segments = segments.ToArray();
        Options = options ?? OutputPresetCatalog.RuntimeFull.Options;
        PresetId = presetId;
    }

    /// <summary>이 문서를 낳은 결과의 원본 DialogueNode.</summary>
    public string SourceNodeId { get; }

    public IReadOnlyList<RenderedSegment> Segments { get; }

    /// <summary>이 문서 결과를 만들 때 사용한 합성 옵션. 프로젝트에는 저장되지 않는다.</summary>
    public DocumentOutputOptions Options { get; }

    /// <summary>기본 프리셋으로 합성한 경우의 식별자. 사용자 정의 옵션이면 null일 수 있다.</summary>
    public OutputPresetId? PresetId { get; }
}
