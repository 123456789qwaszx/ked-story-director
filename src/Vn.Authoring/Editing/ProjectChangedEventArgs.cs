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
    ChapterLayout
}

public sealed class ProjectChangedEventArgs : EventArgs
{
    public ProjectChangedEventArgs(ProjectChangeKind kind, bool fromHistory = false)
    {
        Kind = kind;
        FromHistory = fromHistory;
    }

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
