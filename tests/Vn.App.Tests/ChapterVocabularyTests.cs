using Vn.App.Services;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// 챕터 판의 설정 노드 (계층 분리).
///
/// 판마다 「X 설정」 노드를 세우지 않는다 — 그 안을 채울 편집기가 없어 눌러도 빈 칸이 된다.
/// 옛 판에 남은 것은 데이터째 두고 연출 그래프에서만 숨긴다.
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
