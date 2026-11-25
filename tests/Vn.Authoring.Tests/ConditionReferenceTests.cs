using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Script;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>대본의 줄 전환은 챕터 조건을 이름으로 가리킨다</b> (2026-11-25 · condition-supply C1) — 그 카드가 선 판의 챕터 조건만 쓸 수 있고
/// (2026-08-17 소유자: "챕터 단위로 전역에 쓰이는거야"), 다른 챕터의 조건은 쓸 수 없다고 선다. ⛔ 그 전에는 판의 숨은 공급 노드가 든 사본의
/// Id를 가리켰다.
/// </summary>
public class ConditionReferenceTests
{
    [Fact]
    public void 줄_전환은_챕터_조건의_이름을_든다()
    {
        var sample = new Sample();
        string line = sample.Line("조건 대사", LineConditionTransition.BeginIf("호감 높음"));

        Assert.Equal("호감 높음", sample.ConditionA.Id);
        Assert.Equal("stat(\"favor\") >= 5", sample.ConditionA.Expression);
        Assert.Equal("호감 높음", sample.Dialogue.FindExtension(line)!.Transition!.ConditionId);
        Assert.Empty(ConditionFlowResolver.Resolve(sample.Dialogue, sample.Project).Problems);
    }

    [Fact]
    public void 챕터_조건이_사라지면_기존_Transition은_보존되고_삭제로_표시된다()
    {
        // 지우기는 쓰는 곳이 있으면 막힌다(RemoveChapterCondition) — 손으로 고친 파일처럼 그래도 사라진 경우다. 대본의 전환은 살아 있고
        // "찾을 수 없다"고 선다.
        var sample = new Sample();
        string conditionId = sample.ConditionA.Id;
        string line = sample.Line("조건 대사", LineConditionTransition.BeginIf(conditionId));

        sample.DropCondition(conditionId);

        Assert.Equal(ConditionTransitionKind.BeginIf, sample.Dialogue.FindExtension(line)!.Transition!.Kind);
        Assert.Equal(conditionId, sample.Dialogue.FindExtension(line)!.Transition!.ConditionId);

        DialogueFlow flow = ConditionFlowResolver.Resolve(sample.Dialogue, sample.Project);
        FlowProblem problem = Assert.Single(
            flow.Problems,
            item => item.Kind == FlowProblemKind.UnknownCondition);
        Assert.Equal(line, problem.LineId);
    }

    [Fact]
    public void 다른_챕터의_조건은_사용불가로_표시된다()
    {
        // 범위가 판(챕터)인 뒤의 "있지만 못 쓴다"는 <b>다른 챕터에 있다</b>는 뜻이다 —
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
        editor.AddChapterCondition("저 챕터", "저쪽 조건", "far == true");

        DialogueNode dialogue = editor.AddDialogueNode(here.Id, "장면", name: "장면");
        ScriptLine scriptLine = dialogue.Script.ActiveLines.First();   // 카드는 빈 첫 줄을 들고 선다
        editor.SetLineTransition(
            dialogue.Id, scriptLine.Id, LineConditionTransition.BeginIf("저쪽 조건"));

        Assert.Empty(AvailableConditionResolver.Resolve(project, dialogue.Id).Conditions);

        DialogueFlow flow = ConditionFlowResolver.Resolve(dialogue, project);
        FlowProblem problem = Assert.Single(
            flow.Problems,
            item => item.Kind == FlowProblemKind.UnavailableCondition);
        Assert.Equal(scriptLine.Id, problem.LineId);
        // (⛔ 조건 갈래의 출구 포트 라벨에도 「사용할 수 없음」이 섰다 — 포트가 갈래 출구와 함께 걷혔다 · usability UX28.)
    }

    [Fact]
    public void 조건의_식을_고치면_대사가_보는_식도_곧바로_따라간다()
    {
        // 사본이 없으므로 맞출 것도 없다 — ⛔ 2026-11-23 전에는 스탯을 나중에 더하면 공급 사본이 옛 식에 머물렀다.
        var sample = new Sample();

        sample.Editor.UpdateChapterCondition("test", "호감 높음", "favor >= 7");

        Assert.Equal("stat(\"favor\") >= 7", sample.ConditionA.Expression);
        Assert.Equal(
            "stat(\"favor\") >= 7",
            AvailableConditionResolver.Resolve(sample.Project, sample.Dialogue.Id).Find("호감 높음")!.Expression);
    }

    [Fact]
    public void 판에_카드_말고_다른_노드가_없다()
    {
        // 조건을 더하고 고치고 지워도 판에 서는 것은 카드뿐이다 — ⛔ 숨은 공급 노드가 판마다 하나 섰다.
        var sample = new Sample();

        sample.Editor.AddChapterCondition("test", "새 조건", "favor >= 1");
        sample.Editor.UpdateChapterCondition("test", "새 조건", "favor >= 2");
        Assert.True(sample.Editor.RemoveChapterCondition("test", "새 조건").Applied);

        Assert.All(sample.Project.EnumerateNodes(), node => Assert.IsType<DialogueNode>(node));
    }
}
