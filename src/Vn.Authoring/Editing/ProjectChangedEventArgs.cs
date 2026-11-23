namespace Vn.Authoring.Editing;

public enum ProjectChangeKind
{
    /// <summary>
    /// 노드·줄·조건·출구가 생기거나 사라지거나 순서가 바뀌었다.
    /// 계산되는 갈래와 포트, 편집 행 자체가 달라지므로 화면 구조를 다시 만들어야 한다.
    /// </summary>
    Structure,

    /// <summary>
    /// 셸 상태처럼 현재 편집 컨트롤 구조와 파생 그래프가 바뀌지 않는 내용이다.
    /// Dialogue 내용은 전용 변경 종류를 사용한다.
    /// </summary>
    Content,

    /// <summary>
    /// Dialogue LineBox의 화자나 대사 본문이 바뀌었다.
    /// Dialogue 편집기는 유지하고 Preview만 갱신한다.
    /// </summary>
    DialogueContent,

    /// <summary>
    /// 조건의 작가용 이름이나 평가식이 바뀌었다.
    /// SetNode의 편집 행 구조는 그대로지만 그래프 조건 라벨과 Dialogue 조건 선택지는 새 값을 읽어야 한다.
    /// </summary>
    ConditionDefinition,

    /// <summary>
    /// 노드의 이름처럼 그래프와 참조 표시에는 영향을 주지만 현재 편집 행을 다시 만들 필요는 없는 값이 바뀌었다.
    /// </summary>
    NodeMetadata,

    /// <summary>그래프에서의 좌표만 바뀌었다.</summary>
    Layout,

    /// <summary>
    /// [구성] 판의 에피소드 자리만 바뀌었다 — [대본] 판은 대사 노드의 자리를 읽으므로 그 판은 그대로다. ⛔ 2026-11-18까지는
    /// <see cref="NodeMetadata"/>여서 [구성]에서 카드를 놓을 때마다 [대본] 판을 통째로 다시 지었다(에피소드 300개에서 3초 · finishing FX3).
    /// </summary>
    ChapterLayout,

    /// <summary>
    /// 에피소드 지시(에피소드의 <c>Memo</c>)나 지시 카드가 바뀌었다 — [대본] 판에는 서지 않고, 장면 이름표의 「메모」 표시만 그것을 본다.
    /// ⛔ 2026-11-19까지는 <see cref="NodeMetadata"/>여서 지시 카드를 붙이고 쓰고 뗄 때마다 [대본] 판을 통째로 다시 지었다(에피소드 160개에서
    /// 한 번에 2초 · usability UX7).
    /// </summary>
    EpisodeNote,

    /// <summary>
    /// 대사 노드 하나의 줄이 생기거나 빠지거나 옮겨졌다(카드 글 칸에서 새 줄을 넣는 따위) — 어느 노드인지는
    /// <see cref="ProjectChangedEventArgs.NodeId"/>다. 판은 그 카드만 다시 지을 수 있다. ⛔ 2026-11-19까지는 <see cref="Structure"/>여서 카드에
    /// 새 줄을 넣을 때마다 [대본] 판을 통째로 다시 지었다(에피소드 160개에서 2초 · usability UX13).
    /// </summary>
    DialogueLines,

    /// <summary>
    /// 카드 하나의 보이는 제목(에피소드 제목 · 분기 카드 제목)만 바뀌었다 — 어느 카드인지는 <see cref="ProjectChangedEventArgs.NodeId"/>다. 판은 그 카드의
    /// 제목 글만 고칠 수 있다. ⛔ 2026-11-19까지는 구조 · <see cref="NodeMetadata"/>여서 제목 하나에 [대본] 판을 통째로 다시 지었다(에피소드 160개에서
    /// 2.5초 · usability UX15).
    /// </summary>
    CardTitle,

    /// <summary>
    /// 선택지(챕터 간선) 하나의 내용만 바뀌었다 — 문구 · 스탯변화 · 표시 · 해금 조건 · 잠금 안내 · 자동. 어디서 어디로 잇는지는 그대로다.
    /// 출발 에피소드의 카드가 판에 있으면 그 카드가 <see cref="ProjectChangedEventArgs.NodeId"/>다. [대본] 판에서는 그 카드에서 나가는 철도(선택지 칸)만
    /// 그것을 본다. ⛔ 2026-11-19까지는 <see cref="Structure"/>여서 선택지 문구 하나에 [대본] 판을 통째로 다시 지었다(에피소드 160개에서
    /// 2.5초 · usability UX16).
    /// </summary>
    ChoiceDetail,

    /// <summary>
    /// 선택지(챕터 간선) 하나가 생기거나 걷히거나 도착이 바뀌었다 — 출발은 그대로다. 출발 에피소드의 카드가 판에 있으면 그 카드가
    /// <see cref="ProjectChangedEventArgs.NodeId"/>다. [대본] 판에서는 그 출발의 철도와 도착 포트, 장면 시작점(밖에서 들어오는 길로 정해진다)이 바뀐
    /// 카드만 그것을 본다. ⛔ 2026-11-19까지는 <see cref="Structure"/>여서 판의 선택지 점을 끌어 이을 때마다 판을 두 번 다시 그렸다(에피소드
    /// 160개에서 한 번에 0.5 ~ 0.7초 · 카드를 그대로 두기 전에는 4초 남짓 · usability UX19).
    /// </summary>
    ChoiceRoute,

    /// <summary>
    /// 챕터 조건이 생겼거나 그 식 · 설명이 바뀌었다(라벨은 그대로) — 판에 공급한 사본과 공급 노드(판에 서지 않는 배관)도 함께. [대본] 판에서는
    /// 분기 카드 머리의 조건 표(「식 미정」)만 그것을 본다. ⛔ 2026-11-19까지는 <see cref="Structure"/>여서 조건 하나를 더하는 데 [대본] 판을
    /// 통째로 다시 지었다(에피소드 160개에서 2.5초 · usability UX16).
    /// </summary>
    ChapterCondition,

    /// <summary>
    /// 카드 하나의 <b>작가용 글 서식</b>(굵게 · 색 · 글꼴 · 크기 · 정렬)만 바뀌었다 — 어느 카드인지는 <see cref="ProjectChangedEventArgs.NodeId"/>다.
    /// 글도 줄도 그대로라 [대본] 판은 그 카드의 글 칸 꾸밈만 다시 입히고, [구성]은 다시 읽지 않는다(서식은 내보내기에 나가지 않는다 · writer-formatting).
    /// </summary>
    WriterMarks
}

public sealed class ProjectChangedEventArgs : EventArgs
{
    public ProjectChangedEventArgs(ProjectChangeKind kind, bool fromHistory = false, string? nodeId = null)
    {
        Kind = kind;
        FromHistory = fromHistory;
        NodeId = nodeId;
    }

    /// <summary>
    /// 바뀐 노드가 하나로 정해지면 그 노드 — <see cref="ProjectChangeKind.DialogueLines"/> · <see cref="ProjectChangeKind.CardTitle"/> ·
    /// <see cref="ProjectChangeKind.ChoiceDetail"/> · <see cref="ProjectChangeKind.ChoiceRoute"/>(출발 카드)가 싣는다. 모르거나 여럿이면 null. 되돌리기 ·
    /// 다시 실행도 그 편집의 노드를 싣는다(노드 Id는 스냅샷을 건너도 그대로다 · usability UX21).
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// 바뀐 것의 종류. 되돌리기 · 다시 실행이면 <b>되돌린 그 편집의 종류</b>다(2026-11-18 · board-speed PF1 — ⛔ 그 전에는 늘 <see cref="ProjectChangeKind.Structure"/>였다).
    /// </summary>
    public ProjectChangeKind Kind { get; }

    /// <summary>
    /// 되돌리기 · 다시 실행에서 왔다 — 프로젝트를 스냅샷에서 <b>통째로 새로</b> 지었다(객체가 모두 새것이다). 지은 객체를 붙들어 두는 화면은 종류가
    /// 가벼워도 다시 지어야 한다 — 어느 종류를 가볍게 받아도 되는지는 화면이 정한다.
    /// </summary>
    public bool FromHistory { get; }
}
