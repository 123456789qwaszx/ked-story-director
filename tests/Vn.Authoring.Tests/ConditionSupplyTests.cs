using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Script;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>조건 공급은 판 전체다</b> (2026-08-17 소유자: "챕터 단위로 전역에 쓰이는거야") — 같은 판의 설정노드가 그 판의
/// 대사 노드에 조건을 공급하고, 다른 판(챕터)의 조건은 쓸 수 없다. 노드 사이의 공급 링크는 이제 아무것도 정하지 않는다.
/// </summary>
public class ConditionSupplyTests
{
    [Fact]
    public void 설정노드를_지우면_기존_Transition은_보존되고_사용불가로_표시된다()
    {
        // 범위가 판 전체가 된 뒤로는 <b>노드를 지워야</b> 조건이 사라진다(링크를 끊는 것이
        // 아니라) — 그때도 대본의 전환은 살아 있고 "사용할 수 없음"으로 보인다.
        var sample = new Sample();
        string line = sample.Line(
            "조건 대사",
            LineConditionTransition.BeginIf(sample.ConditionA.Id));

        sample.Editor.RemoveNode(sample.SetNode.Id);

        Assert.NotNull(sample.Dialogue.FindExtension(line)!.Transition);
        Assert.Equal(ConditionTransitionKind.BeginIf, sample.Dialogue.FindExtension(line)!.Transition!.Kind);
        Assert.Equal(sample.ConditionA.Id, sample.Dialogue.FindExtension(line)!.Transition!.ConditionId);

        // 노드째 사라졌으니 프로젝트 어디에도 없다 — "삭제됐다"고 말한다.
        DialogueFlow flow = ConditionFlowResolver.Resolve(sample.Dialogue, sample.Project);
        FlowProblem problem = Assert.Single(
            flow.Problems,
            item => item.Kind == FlowProblemKind.UnknownCondition);
        Assert.Equal(line, problem.LineId);
    }

    [Fact]
    public void 다른_챕터의_조건은_사용불가로_표시된다()
    {
        // 범위가 판(챕터)이 된 뒤의 "있지만 못 쓴다"는 <b>다른 챕터에 있다</b>는 뜻이다 —
        // 대본의 전환은 보존되고 이름은 읽히되 사용할 수 없음으로 선다.
        var project = new StoryProject();
        var here = new StoryFile("sf_here", "이 챕터");
        var there = new StoryFile("sf_there", "저 챕터");
        project.Files.Add(here);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "이 챕터" });   // 카드는 챕터 판에만 선다
        project.Files.Add(there);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "저 챕터" });   // 카드는 챕터 판에만 선다
        var editor = new ProjectEditor(project);

        editor.AddChapterStat("저 챕터", "far", maximum: 1, type: global::Vn.Authoring.Chapters.ChapterStatType.Bool);
        editor.AddChapterCondition("저 챕터", "저쪽 조건", "far == true");   // 저 판의 공급 노드가 사본을 나른다
        ConditionDefinition foreignCondition = there.Nodes.OfType<SetNode>().Single().Conditions.Single();

        DialogueNode dialogue = editor.AddDialogueNode(here.Id, name: "장면");
        ScriptLine scriptLine = dialogue.Script.ActiveLines.First();   // 카드는 빈 첫 줄을 들고 선다
        editor.SetLineTransition(
            dialogue.Id, scriptLine.Id, LineConditionTransition.BeginIf(foreignCondition.Id));

        DialogueFlow flow = ConditionFlowResolver.Resolve(dialogue, project);
        FlowProblem problem = Assert.Single(
            flow.Problems,
            item => item.Kind == FlowProblemKind.UnavailableCondition);
        Assert.Equal(scriptLine.Id, problem.LineId);

        ExitPort branch = NodeConnections.PortsOf(dialogue, project)
            .Single(port => port.Kind == ExitPortKind.Branch);
        Assert.Contains("사용할 수 없음", branch.Label, StringComparison.Ordinal);
    }

}
