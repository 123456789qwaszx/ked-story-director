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
                // 고친 글이 카드의 글 칸에도 비친다 — 안 그러면 낡은 칸의 글이 다음 넣기에서 방금 고친 줄을 도로 덮는다. 어느 카드인지는
                // 알림이 싣는다(모르면 — 화자 이름 바꾸기처럼 여러 카드면 — 칸마다 · usability UX18). ⛔ 고른 카드의 칸만 다시 채웠다.
                RefreshCardWriting: true),

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

            // 한 카드의 줄이 바뀌었다 — 그 카드만 다시 짓는다(판이 다른 것도 바뀌었다고 보면 스스로 통째로 짓는다). 곁기둥의 줄 표는 줄이
            // 늘고 줄었으니 다시 세운다(usability UX13 · ⛔ 구조 — 판을 통째로 다시 지었다).
            ProjectChangeKind.DialogueLines => new ProjectRefreshPlan(
                RebuildGraph: false,
                RefreshGraphPositions: false,
                RebuildInspector: true,
                RebuildCard: true),

            // 카드 하나의 제목만 — 그 카드의 제목 글만 고친다(접은 상자 안이면 판이 스스로 통째로 짓는다 · usability UX15). 하이어아키는 [구성]의
            // 다시 읽기로 따라온다.
            // 카드를 골랐으면 그 대사 편집기의 이름 칸이 제가 고친 것이다(다시 세우면 쓰던 칸이 사라진다) — 장면 · 챕터 요약이 서 있으면 그 안의
            // 에피소드 이름이 낡지 않게 다시 세운다(가볍다).
            ProjectChangeKind.CardTitle => new ProjectRefreshPlan(
                RebuildGraph: false,
                RefreshGraphPositions: false,
                RebuildInspector: selectedNode is not DialogueNode,
                RefreshCardTitle: true),

            // 선택지 하나의 내용 — 카드는 그대로 두고 그 출발 카드에서 나가는 철도(선택지 칸)만 다시 긋는다(판이 다른 것도 바뀌었다고 보면
            // 스스로 통째로 짓는다 · usability UX16). 인스펙터는 그 선택지를 보이고 있을 수 있어 다시 세운다(구조일 때와 같다).
            ProjectChangeKind.ChoiceDetail => new ProjectRefreshPlan(
                RebuildGraph: false,
                RefreshGraphPositions: false,
                RebuildInspector: true,
                RedrawChoices: true),

            // 챕터 조건 — [대본] 판에서는 분기 카드의 조건 표만 다시 단다. 조건 요약 · 대사 편집기의 조건 고르기는 다시 세운다(usability UX16).
            ProjectChangeKind.ChapterCondition => new ProjectRefreshPlan(
                RebuildGraph: false,
                RefreshGraphPositions: false,
                RebuildInspector: true,
                RefreshBranchConditions: true),

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
    bool RefreshSceneFrames = false,
    bool RebuildCard = false,
    bool RefreshCardTitle = false,
    bool RedrawChoices = false,
    bool RefreshBranchConditions = false);
