using Vn.Authoring.Definition;
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
    public void 같은_판의_모든_설정노드가_챕터_전역으로_공급한다()
    {
        // 2026-08-17 소유자 — "시나리오 작가가 만든 조건, 변수 등은 챕터 단위로 기록이 되어서
        // 사용됐으면 해. 이거는 챕터단위로 전역에 쓰이는거야." 판 = 챕터 1:1이므로 같은 판에
        // 서 있는 것만으로 미친다. 링크를 걸어야 보이던 옛 규칙은 폐지됐다.
        var project = new StoryProject();
        var file = new StoryFile("sf_scope", "범위");
        project.Files.Add(file);
        var editor = new ProjectEditor(project);

        SetNode first = editor.AddSetNode(file.Id, name: "첫 설정");
        ConditionDefinition firstCondition = editor.AddCondition(first.Id, "첫 조건", "linked == true");
        SetNode second = editor.AddSetNode(file.Id, name: "둘째 설정");
        ConditionDefinition secondCondition = editor.AddCondition(second.Id, "둘째 조건", "other == true");
        DialogueNode dialogue = editor.AddDialogueNode(file.Id, name: "장면");
        // 링크는 걸지 않는다 — 그래도 둘 다 보여야 한다.

        // 다른 판의 설정노드는 미치지 않는다 — 챕터가 다르면 다른 어휘다.
        var otherFile = new StoryFile("sf_other", "다른 챕터");
        project.Files.Add(otherFile);
        SetNode foreign = editor.AddSetNode(otherFile.Id, name: "남의 설정");
        ConditionDefinition foreignCondition = editor.AddCondition(foreign.Id, "남의 조건", "foreign == true");

        var definition = new GameDefinition
        {
            Conditions = new List<ConditionSpec>
            {
                new()
                {
                    Id = "global_difficulty",
                    Name = "고난도",
                    Expression = "difficulty == hard"
                }
            }
        };

        AvailableConditionCatalog catalog = AvailableConditionResolver.Resolve(
            project,
            dialogue.Id,
            definition);

        Assert.Equal(
            new[] { "global_difficulty", firstCondition.Id, secondCondition.Id },
            catalog.Conditions.Select(condition => condition.Id));
        Assert.DoesNotContain(
            catalog.Conditions,
            condition => condition.Id == foreignCondition.Id);

        IReadOnlyList<ConditionChoice> choices = ConditionChoices.For(
            preceding: null,
            dialogue,
            project,
            definition);

        Assert.Contains(choices, choice => choice.ConditionId == "global_difficulty");
        Assert.Contains(choices, choice => choice.ConditionId == firstCondition.Id);
        Assert.Contains(choices, choice => choice.ConditionId == secondCondition.Id);
        Assert.DoesNotContain(choices, choice => choice.ConditionId == foreignCondition.Id);

        ScriptLine globalLine = dialogue.Script.ActiveLines.First();   // 카드는 빈 첫 줄을 들고 선다
        editor.SetLineTransition(
            dialogue.Id,
            globalLine.Id,
            LineConditionTransition.BeginIf("global_difficulty"));

        DialogueFlow flow = ConditionFlowResolver.Resolve(dialogue, project, definition);
        Assert.DoesNotContain(flow.Problems, problem =>
            problem.Kind is FlowProblemKind.UnknownCondition or FlowProblemKind.UnavailableCondition);
    }

    [Fact]
    public void 공급_순서는_판_안의_노드_순서다()
    {
        // 링크 Order가 정하던 것을 이제 판의 노드 순서가 정한다 — 두 화면이 같은 하나를 본다.
        var project = new StoryProject();
        var file = new StoryFile("sf_order", "순서");
        project.Files.Add(file);
        var editor = new ProjectEditor(project);

        SetNode first = editor.AddSetNode(file.Id, name: "첫 번째");
        ConditionDefinition firstCondition = editor.AddCondition(first.Id, "첫 조건", "a");
        SetNode second = editor.AddSetNode(file.Id, name: "두 번째");
        ConditionDefinition secondCondition = editor.AddCondition(second.Id, "둘째 조건", "b");
        editor.AddDialogueNode(file.Id, name: "장면");

        DialogueNode dialogue = project.EnumerateNodes().OfType<DialogueNode>().Single();
        AvailableConditionCatalog catalog = AvailableConditionResolver.Resolve(project, dialogue.Id);

        Assert.Equal(
            new[] { firstCondition.Id, secondCondition.Id },
            catalog.Conditions.Select(condition => condition.Id));
    }

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
        project.Files.Add(there);
        var editor = new ProjectEditor(project);

        SetNode foreign = editor.AddSetNode(there.Id, name: "저쪽 설정");
        ConditionDefinition foreignCondition = editor.AddCondition(foreign.Id, "저쪽 조건", "far == true");

        DialogueNode dialogue = editor.AddDialogueNode(here.Id, name: "장면");
        ScriptLine scriptLine = dialogue.Script.ActiveLines.First();   // 카드는 빈 첫 줄을 들고 선다
        editor.SetLineTransition(
            dialogue.Id, scriptLine.Id, LineConditionTransition.BeginIf(foreignCondition.Id));

        DialogueFlow flow = ConditionFlowResolver.Resolve(dialogue, project);
        FlowProblem problem = Assert.Single(
            flow.Problems,
            item => item.Kind == FlowProblemKind.UnavailableCondition);
        Assert.Equal(scriptLine.Id, problem.LineId);

        IReadOnlyList<ConditionChoice> choices = ConditionChoices.For(
            preceding: null,
            dialogue,
            project,
            definition: null,
            currentTransition: dialogue.FindExtension(scriptLine.Id)!.Transition);
        ConditionChoice current = ConditionChoices.Current(
            choices, dialogue.FindExtension(scriptLine.Id)!.Transition);

        Assert.False(current.IsAvailable);
        Assert.Equal(foreignCondition.Id, current.ConditionId);
        Assert.Contains("사용할 수 없음", current.Label, StringComparison.Ordinal);

        ExitPort branch = NodeConnections.PortsOf(dialogue, project)
            .Single(port => port.Kind == ExitPortKind.Branch);
        Assert.Contains("사용할 수 없음", branch.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void 옛_파일의_공급_링크는_그_노드를_지우면_함께_걷힌다()
    {
        // 링크를 만드는 편집은 2026-10-29에 걷혔다(legacy-cleanup.md L-D2) — 옛 파일의 링크 데이터만 남는다.
        // 없는 노드를 가리키는 링크가 파일에 남으면 다음에 열 때 검증이 거절한다.
        var sample = new Sample();
        sample.Project.Links.Add(new NodeLink(
            kind: NodeLinkKind.Settings, sourceNodeId: sample.SetNode.Id, targetNodeId: sample.Dialogue.Id));

        sample.Editor.RemoveNode(sample.SetNode.Id);

        Assert.Empty(sample.Project.Links);
    }
}
