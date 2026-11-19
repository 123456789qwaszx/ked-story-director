using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.App.Services;

/// <summary>
/// 도메인 변경 한 번이 각 화면에 어떤 갱신을 요구하는지 결정한다.
///
/// 이 판단을 MainWindow의 switch 안에 흩어 놓으면 변경 종류를 추가할 때마다
/// SetNode 편집 컨트롤을 불필요하게 다시 만드는 회귀가 생긴다. 특히 조건 TextBox의
/// LostFocus 커밋은 다음 클릭이 도착하기 전에 컨트롤을 교체해서 두 번째 칸을 클릭할 수 없게 했다.
/// </summary>
internal static class ProjectRefreshPlanner
{
    /// <param name="fromHistory">
    /// 되돌리기 · 다시 실행에서 왔다(<see cref="ProjectChangedEventArgs.FromHistory"/>) — 프로젝트의 객체가 모두 새것이다. 그래서 가볍게 받는 것은
    /// 화면이 객체를 붙들지 않는 종류뿐이다(2026-11-18 · board-speed PF1): 글(카드의 글 칸들을 다시 채운다 — 되돌린 글이 고른 카드의 것이라는
    /// 보장이 없다) · [구성] 자리 · 화자. 그 밖은 구조로 받는다 — 예컨대 접은 장면 상자는 지을 때의 판 객체로 끌기를 잰다. ⛔ 그 전에는
    /// 되돌리기가 늘 구조로 알려 글 한 줄을 되돌려도 판을 통째로 다시 지었다.
    /// </param>
    public static ProjectRefreshPlan Plan(ProjectChangeKind kind, StoryNode? selectedNode, bool fromHistory = false)
    {
        if (fromHistory)
        {
            return kind switch
            {
                ProjectChangeKind.DialogueContent => new ProjectRefreshPlan(
                    RebuildGraph: false,
                    RefreshGraphPositions: false,
                    RebuildInspector: false,
                    RefreshAllCardWriting: true),
                ProjectChangeKind.ChapterLayout or ProjectChangeKind.Content => default,
                _ => Plan(ProjectChangeKind.Structure, selectedNode)
            };
        }

        return kind switch
        {
            ProjectChangeKind.Layout => new ProjectRefreshPlan(
                RebuildGraph: false,
                RefreshGraphPositions: true,
                RebuildInspector: false),

            ProjectChangeKind.Structure => new ProjectRefreshPlan(
                RebuildGraph: true,
                RefreshGraphPositions: false,
                RebuildInspector: true),

            ProjectChangeKind.DialogueContent => new ProjectRefreshPlan(
                RebuildGraph: false,
                RefreshGraphPositions: false,
                RebuildInspector: false,
                // 곁기둥에서 고친 글이 카드의 글 칸에도 비친다 — 안 그러면 낡은 칸의 글이
                // 다음 넣기에서 방금 고친 줄을 도로 덮는다.
                RefreshCardWriting: selectedNode is DialogueNode),

            ProjectChangeKind.ConditionDefinition => new ProjectRefreshPlan(
                RebuildGraph: true,
                RefreshGraphPositions: false,
                // 조건을 편집 중인 SetNode는 그대로 둔다. DialogueNode가 선택되어 있다면
                // 드롭다운과 갈래 라벨을 새 조건 이름으로 다시 계산한다.
                RebuildInspector: selectedNode is DialogueNode),

            ProjectChangeKind.NodeMetadata => new ProjectRefreshPlan(
                RebuildGraph: true,
                RefreshGraphPositions: false,
                RebuildInspector: false),

            // 에피소드 지시 — [대본] 판에는 서지 않는다. 장면 이름표의 「메모」 표시만 다시 단다(테두리 · 이름표 — 카드 · 철도는 그대로).
            // 장면 메모 공간은 제가 고친 것을 제자리에서 고쳐 두었다(usability UX7 · ⛔ NodeMetadata — 판을 통째로 다시 지었다).
            ProjectChangeKind.EpisodeNote => new ProjectRefreshPlan(
                RebuildGraph: false,
                RefreshGraphPositions: false,
                RebuildInspector: false,
                RefreshSceneFrames: true),

            // 셸 상태 · 상태줄 알림 — 다시 만들 화면이 없다.
            ProjectChangeKind.Content => default,

            // [구성] 판의 에피소드 자리 — [대본] 판은 대사 노드의 자리를 읽는다. [구성]은 제 다시 읽기로 그린다.
            ProjectChangeKind.ChapterLayout => default,

            _ => default
        };
    }
}

internal readonly record struct ProjectRefreshPlan(
    bool RebuildGraph,
    bool RefreshGraphPositions,
    bool RebuildInspector,
    bool RefreshCardWriting = false,
    bool RefreshAllCardWriting = false,
    bool RefreshSceneFrames = false);
