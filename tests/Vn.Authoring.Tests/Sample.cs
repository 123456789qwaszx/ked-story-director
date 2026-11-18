using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Results;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests;

/// <summary>
/// 테스트가 공유하는 작은 프로젝트 하나.
/// 대본 하나, 챕터 조건 두 개(판의 공급 노드가 나른다), 대사 노드 하나, 그리고 이동할 대상 노드 세 개를 갖춘다.
///
/// LineId는 예측 가능한 값으로 주입한다. 무작위 Id는 동기화 계획을 테스트에서 읽을 수 없게 만든다.
/// </summary>
internal sealed class Sample
{
    private int _nextLineNumber;

    public Sample()
    {
        // 챕터 하나와 그 판 — 카드는 챕터 판에만 선다(2026-11-17 — 낙서판을 걷었다).
        var project = new StoryProject { Title = "테스트" };
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "test" });
        File = new StoryFile("sf_test", "test");
        project.Files.Add(File);

        Editor = new ProjectEditor(project, newLineId: NextLineId);

        // 조건은 챕터의 것이다 — 판의 공급 노드(설정 노드는 그것뿐이다)가 사본을 나르고, 대본의 줄 전환은 그 사본을 가리킨다.
        Editor.AddChapterStat("test", "favor", maximum: 10);
        Editor.AddChapterStat("test", "trust", maximum: 10);
        Editor.AddChapterCondition("test", "호감 높음", "favor >= 5");
        Editor.AddChapterCondition("test", "신뢰 높음", "trust >= 3");
        SetNode = File.Nodes.OfType<SetNode>().Single();
        ConditionA = SetNode.Conditions.Single(condition => condition.Name == "호감 높음");
        ConditionB = SetNode.Conditions.Single(condition => condition.Name == "신뢰 높음");

        Dialogue = Editor.AddDialogueNode(File.Id, "본문", name: "본문");

        // 본문 카드는 <b>빈 대본</b>으로 시작한다 — 시험마다 줄을 처음부터 쌓는다(<see cref="Line"/>). 카드가 태어날 때
        // 든 빈 첫 줄을 걷고 LineId 번호도 처음으로 되돌린다(대본이 카드 안으로 들어오기 전과 같은 번호가 나온다).
        Dialogue.Script.Lines.Clear();
        Dialogue.Script.RequireLocale(Dialogue.Script.PrimaryLocale).Entries.Clear();
        _nextLineNumber = 0;

        TargetA = AddEndNode("A로 간다");
        TargetB = AddEndNode("B로 간다");
        TargetC = AddEndNode("C로 간다");
    }

    public ProjectEditor Editor { get; }

    public StoryProject Project => Editor.Project;

    public StoryFile File { get; }

    /// <summary>
    /// 본문 카드의 대본 — 지금 프로젝트에서 찾는다. 되돌리기는 프로젝트를 새 객체로 갈아 끼우므로 들고 있던
    /// <see cref="Dialogue"/>의 대본은 낡는다.
    /// </summary>
    public ScriptDocument Script => Project.FindDialogue(Dialogue.Id)!.Script;

    public SetNode SetNode { get; }

    public ConditionDefinition ConditionA { get; }

    public ConditionDefinition ConditionB { get; }

    public DialogueNode Dialogue { get; }

    public DialogueNode TargetA { get; }

    public DialogueNode TargetB { get; }

    public DialogueNode TargetC { get; }

    /// <summary>대본에 줄을 하나 붙이고 그 줄의 LineId를 돌려준다.</summary>
    public string Line(string text, LineConditionTransition? transition = null)
    {
        ScriptLine line = Editor.InsertScriptLine(Dialogue.Id);
        Editor.SetScriptLineText(Dialogue.Id, line.Id, string.Empty, text);

        if (transition is not null)
        {
            Editor.SetLineTransition(Dialogue.Id, line.Id, transition);
        }

        return line.Id;
    }

    /// <summary>그 대사 노드(기본: <see cref="Dialogue"/>)를 펼친 결과 — 미리보기 합성기와 흐름 해석이 읽는 모양.</summary>
    public DialogueResult Unfold(string? nodeId = null) =>
        DialoguePublisher.AsResult(DialoguePublisher.Draft(Project, nodeId ?? Dialogue.Id));

    /// <summary>그 노드를 펼칠 때의 <b>막는</b> 문제 — 비어 있어야 노드가 성립한다.</summary>
    public IReadOnlyList<PublishProblem> BlockingProblems(string? nodeId = null) =>
        DialoguePublisher.Draft(Project, nodeId ?? Dialogue.Id).Problems
            .Where(problem => problem.IsBlocking)
            .ToList();

    /// <summary>작업지시서 6.2의 예시를 그대로 만든다.</summary>
    public (string L0, string L1, string L2, string L3, string L4, string L5, string L6) BuildSpecExample()
    {
        string l0 = Line("0");
        string l1 = Line("1", LineConditionTransition.BeginIf(ConditionA.Id));
        string l2 = Line("2");
        string l3 = Line("3", LineConditionTransition.BeginElseIf(ConditionB.Id));
        string l4 = Line("4");
        string l5 = Line("5", LineConditionTransition.EndIf());
        string l6 = Line("6");

        return (l0, l1, l2, l3, l4, l5, l6);
    }

    /// <summary>대본 한 줄짜리 종점 노드. 문제 없이 펼쳐지는 상태로 만들어 둔다.</summary>
    private DialogueNode AddEndNode(string name)
    {
        DialogueNode node = Editor.AddDialogueNode(File.Id, name, name: name);   // 빈 첫 줄을 들고 선다
        Editor.SetScriptLineText(node.Id, node.Script.ActiveLines.Single().Id, "화자", name);
        return node;
    }

    private string NextLineId() => $"ln_{++_nextLineNumber:D3}";
}
