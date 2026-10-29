namespace Vn.Authoring.Rendering;

/// <summary>
/// 문서의 표현 목적. 공식 저작 모델에는 저장되지 않는다.
/// (⛔ 녹음 · 번역 대본은 2026-10-29에 Script Preview 탭과 함께 걷혔다 — 그 탭이 유일한 독자였다.)
/// </summary>
public enum DocumentOutputFormat
{
    YarnRuntime,
    Scenario
}

/// <summary>도구가 기본 제공하는 문서 프리셋.</summary>
public enum OutputPresetId
{
    RuntimeFull,
    ScenarioOnly
}

/// <summary>
/// Dialogue 문서 합성 시 어떤 의미 레이어를 포함할지 결정한다.
///
/// 이 값은 Preview 요청에만 전달되며 StoryProject, Snapshot, Undo, dirty 상태에 저장되지 않는다.
/// 동일한 StoryProject에서 옵션만 바꾸어 여러 문서를 안전하게 만들 수 있다.
/// </summary>
public sealed class DocumentOutputOptions
{
    public DocumentOutputOptions(
        DocumentOutputFormat format,
        bool includeStructure,
        bool includeConditions,
        bool includeDialogueText,
        bool includeLocalizedDialogue,
        bool includeSpeaker,
        bool includeLineId,
        bool includeExecutionJumps,
        bool includeDiagnostics)
    {
        Format = format;
        IncludeStructure = includeStructure;
        IncludeConditions = includeConditions;
        IncludeDialogueText = includeDialogueText;
        IncludeLocalizedDialogue = includeLocalizedDialogue;
        IncludeSpeaker = includeSpeaker;
        IncludeLineId = includeLineId;
        IncludeExecutionJumps = includeExecutionJumps;
        IncludeDiagnostics = includeDiagnostics;
    }

    public DocumentOutputFormat Format { get; }

    public bool IncludeStructure { get; }

    public bool IncludeConditions { get; }

    public bool IncludeDialogueText { get; }

    public bool IncludeLocalizedDialogue { get; }

    public bool IncludeSpeaker { get; }

    public bool IncludeLineId { get; }

    public bool IncludeExecutionJumps { get; }

    public bool IncludeDiagnostics { get; }
}

/// <summary>사람이 고르는 이름과 합성 옵션을 묶은 불변 프리셋.</summary>
public sealed record OutputPreset(
    OutputPresetId Id,
    string DisplayName,
    string Description,
    DocumentOutputOptions Options);

/// <summary>
/// 기본 출력 프리셋 카탈로그.
///
/// 1) Runtime Full = 재생 순서 그대로(조건 · LineId · 실행 출구) — 합성의 기본값이고 흐름 조각을 재는 자리,
/// 2) Scenario Only = 대본+조건 — 카드 글 칸이 쓰는 면.
/// ⛔ Direction Sheet(LineId+연출 테이블)는 2026-10-29에 연출과 함께 <c>ked-story-compositor</c>로 갔고,
/// Recording/Localization Script(LineId+대본)는 같은 날 그것을 고르던 Script Preview 탭과 함께 걷혔다.
///
/// 프리셋 선택은 문서 합성 방법만 바꾸며 StoryProject를 수정하지 않는다.
/// </summary>
public static class OutputPresetCatalog
{
    public static OutputPreset RuntimeFull { get; } = new(
        OutputPresetId.RuntimeFull,
        "Runtime Full",
        "조건, LineId와 실행 출구를 모두 포함한 Yarn 스타일 Preview",
        new DocumentOutputOptions(
            DocumentOutputFormat.YarnRuntime,
            includeStructure: true,
            includeConditions: true,
            includeDialogueText: true,
            includeLocalizedDialogue: false,
            includeSpeaker: true,
            includeLineId: true,
            includeExecutionJumps: true,
            includeDiagnostics: true));

    public static OutputPreset ScenarioOnly { get; } = new(
        OutputPresetId.ScenarioOnly,
        "Scenario Only",
        "조건과 화자·대사만 읽기 쉽게 표시하고 실행 출구는 숨김",
        new DocumentOutputOptions(
            DocumentOutputFormat.Scenario,

            // ⛔ <b>머리글(`[장면] 이름`)을 안 낸다</b> (2026-09-18 소유자: *"이런 쓸모없는
            //    글자 치워줘"*). 이 프리셋은 보는 면이 아니라 <b>쓰는 면</b>이고, 쓴 글은
            //    그대로 파서로 되돌아간다 — 그런데 파서는 <c>[</c>로 시작하는 줄을
            //    <b>해석 못 한 줄</b>로 올린다. 그래서 저장할 때마다 자기가 낸 머리글이
            //    붉은 글씨로 되돌아오고 있었다. 어느 노드인지는 화면 위 제목이 말한다.
            includeStructure: false,
            includeConditions: true,
            includeDialogueText: true,
            includeLocalizedDialogue: false,
            includeSpeaker: true,
            includeLineId: false,
            includeExecutionJumps: false,
            includeDiagnostics: true));
}
