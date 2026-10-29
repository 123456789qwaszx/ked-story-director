using Vn.App.Services;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// 챕터 판에 딸리던 설정 노드 (2026-08-17 소유자 — 계층 분리).
///
/// ⛔ 세션이 챕터 스탯 키를 「A계층 어휘」로 들던 길(<c>SupplyChapterVocabulary</c>)은 2026-10-29에 걷혔다 — 작가 변수
/// 후보에서 스탯을 빼려던 것인데 작가 변수가 2026-09-17에 폐지돼 아무도 읽지 않았다(<c>legacy-cleanup.md</c> L2).
/// ⛔ 판마다 상시로 세우던 「X 설정」 노드도 같은 날(L6) 그만 세운다 — 그 안을 채우던 편집기가 먼저 걷혀
/// 눌러도 빈 칸이었다. 옛 판에 남은 것은 데이터째 두고 연출 그래프에서만 숨긴다.
/// </summary>
public sealed class ChapterVocabularyTests
{
    [Fact]
    public void 챕터_판을_열어도_설정_노드는_서지_않는다()
    {
        var session = new AuthoringSession();
        string fileId = session.EnsureChapterBoard("ch01");
        session.EnsureChapterBoard("ch01");

        Assert.Empty(session.Project.FindFile(fileId)!.Nodes.OfType<SetNode>());
    }

    [Fact]
    public void 옛_판에_남은_설정_노드는_카드가_없고_조건은_그대로_고른다()
    {
        var session = new AuthoringSession();
        string fileId = session.EnsureChapterBoard("ch01");

        SetNode settings = session.Editor.AddSetNode(fileId, name: "ch01 설정");
        ConditionDefinition condition = session.Editor.AddCondition(settings.Id, "옛 조건", "x == true");
        DialogueNode dialogue = session.Editor.AddDialogueNode(fileId, name: "장면");

        GraphProjection projection = GraphProjectionBuilder.Build(
            session.Project, new HashSet<string>([fileId]));

        Assert.DoesNotContain(
            projection.Items.OfType<ExpandedNodeProjection>(),
            item => item.NodeId == settings.Id);
        Assert.Contains(
            projection.Items.OfType<ExpandedNodeProjection>(),
            item => item.NodeId == dialogue.Id);

        Assert.Contains(
            AvailableConditionResolver.Resolve(session.Project, dialogue.Id).Conditions,
            item => item.Id == condition.Id);
    }
}
