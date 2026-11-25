using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Results;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests;

/// <summary>
/// 테스트가 공유하는 작은 프로젝트 하나.
/// 대본 하나, 챕터 조건 두 개, 대사 노드 하나, 그리고 이동할 대상 노드 세 개를 갖춘다.
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

        // 조건은 챕터의 것이다 — 대본의 줄 전환은 그 이름을 가리킨다.
        Editor.AddChapterStat("test", "favor", maximum: 10);
        Editor.AddChapterStat("test", "trust", maximum: 10);
        Editor.AddChapterCondition("test", "호감 높음", "favor >= 5");
        Editor.AddChapterCondition("test", "신뢰 높음", "trust >= 3");

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

    /// <summary>그 판의 대사가 쓸 수 있는 조건 — 지금 프로젝트에서 계산한다(챕터 조건에서 바로 나온다).</summary>
    public AvailableConditionCatalog Conditions =>
        AvailableConditionResolver.Of(Project.Chapters.Single(chapter => chapter.ChapterId == "test"));

    /// <summary>「호감 높음」 — <c>stat("favor") &gt;= 5</c>.</summary>
    public AvailableCondition ConditionA => Conditions.Find("호감 높음")!;

    /// <summary>「신뢰 높음」 — <c>stat("trust") &gt;= 3</c>.</summary>
    public AvailableCondition ConditionB => Conditions.Find("신뢰 높음")!;

    public DialogueNode Dialogue { get; }

    public DialogueNode TargetA { get; }

    public DialogueNode TargetB { get; }

    public DialogueNode TargetC { get; }

    /// <summary>
    /// 챕터 조건 하나를 명령을 거치지 않고 걷는다 — 손으로 고친 파일처럼 쓰는 곳이 남은 채 조건이 사라진 경우다(명령은 쓰는 곳이 있으면 거절한다).
    /// </summary>
    public void DropCondition(string label) =>
        Project.Chapters.Single(chapter => chapter.ChapterId == "test").Conditions.RemoveAll(condition => condition.Label == label);

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
