using Vn.Authoring.Definition;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>대사 노드 카드 안의 글 칸이 쓰는 길</b> (D4 · 2026-10-29, <c>docs/design/two-graphs.md</c> §7 D4) — 편집기 수준.
///
/// 카드는 <see cref="WorkingDialoguePreview.ScenarioText"/>를 보이고, 넣을 때
/// <see cref="ProjectEditor.ApplyScenarioText"/>를 부른다. 그 길이 지켜야 할 셋: 새 카드의 빈 첫 줄이 거절을 부르지
/// 않는다 · 넣기 한 번이 되돌리기 한 칸이다 · 보인 글을 그대로 넣으면 아무 줄도 안 바뀐다(왕복).
/// </summary>
public sealed class CardWritingTests
{
    private static readonly GameDefinition Definition = GameDefinition.Parse("""
        { "speakers": [ { "name": "라루", "characterId": "laru" }, { "name": "윌로", "characterId": "willo" } ] }
        """)!;

    [Fact]
    public void 글_칸의_글은_대사마다_몇_번째_줄인가를_함께_낸다()
    {
        // 연출 그래프 카드가 분기 점을 그 대사의 높이에 세운다(2026-10-30) — 조건 줄(<<if>> · <<endif>>)이 끼어도 맞아야 한다.
        var sample = new Sample();
        (string l0, string l1, string l2, string l3, string l4, string l5, string l6) = sample.BuildSpecExample();

        (string text, IReadOnlyDictionary<string, int> rows) =
            WorkingDialoguePreview.ScenarioWriting(sample.Project, sample.Dialogue.Id);
        string[] shown = text.Split('\n');

        Assert.Equal(text, WorkingDialoguePreview.ScenarioText(sample.Project, sample.Dialogue.Id));
        Assert.Contains(shown, row => row.StartsWith("<<", StringComparison.Ordinal));   // 조건 줄이 끼어 있다

        foreach ((string lineId, string body) in new[] { (l0, "0"), (l1, "1"), (l2, "2"), (l3, "3"), (l4, "4"), (l5, "5"), (l6, "6") })
        {
            Assert.Equal(body, shown[rows[lineId]].Trim());
        }
    }

    [Fact]
    public void 새_카드에_여러_줄을_넣으면_빈_첫_줄이_첫_줄이_된다()
    {
        // ⛔ 조사에서 찾은 함정 — 새 카드는 빈 줄 하나를 들고 태어나, 여러 줄을 처음 넣으면
        //    "어느 줄이 어느 줄인지 확신할 수 없다"로 통째로 거절됐다.
        (ProjectEditor editor, DialogueNode node) = NewCard();
        string blank = Lines(editor, node).Single().Id;

        ScenarioPasteOutcome outcome = editor.ApplyScenarioText(
            node.Id, "라루: 하나\n윌로: 둘\n셋째는 지문이다.", Definition);

        Assert.True(outcome.Applied);
        Assert.Empty(outcome.Problems);

        List<ScriptLine> lines = Lines(editor, node);
        Assert.Equal(blank, lines[0].Id);
        Assert.Equal(
            [("라루", "하나"), ("윌로", "둘"), ("", "셋째는 지문이다.")],
            lines.Select(line => Said(editor, node, line.Id)));
    }

    [Fact]
    public void 넣기_한_번은_되돌리기_한_칸이다()
    {
        // 줄 동기화와 조건 전환이 따로 Mutate이던 때는 한 번 물렀을 때 반쪽이 남았다(§5-7).
        (ProjectEditor editor, DialogueNode node) = NewCard();
        ConditionDefinition condition = Condition(editor);
        int changes = 0;
        editor.Changed += (_, _) => changes++;

        ScenarioPasteOutcome outcome = editor.ApplyScenarioText(node.Id, """
            라루: 처음
            <<if favor >= 5>>
            윌로: 갈래 안
            <<endif>>
            라루: 끝
            """, Definition);

        Assert.True(outcome.Applied);
        Assert.Equal(1, changes);
        Assert.Contains(
            editor.Project.FindDialogue(node.Id)!.LineExtensions.SelectMany(extension => extension.Transitions),
            transition => transition.ConditionId == condition.Id);

        editor.Undo();

        DialogueNode restored = editor.Project.FindDialogue(node.Id)!;
        Assert.Equal([("", "")], Lines(editor, restored).Select(line => Said(editor, restored, line.Id)));
        Assert.Empty(restored.LineExtensions.SelectMany(extension => extension.Transitions));
    }

    [Fact]
    public void 카드에_보인_글을_그대로_넣으면_아무_줄도_안_바뀐다()
    {
        // 쓰는 면은 왕복해야 한다 — 보인 글을 손대지 않고 넣었는데 줄이 고쳐지거나 조건이 풀리면 안 된다.
        (ProjectEditor editor, DialogueNode node) = NewCard();
        Condition(editor);
        editor.ApplyScenarioText(node.Id, """
            라루: 처음
            <<if favor >= 5>>
            윌로: 갈래 안
            <<endif>>
            지문 한 줄.
            """, Definition);

        string shown = WorkingDialoguePreview.ScenarioText(editor.Project, node.Id, Definition);
        ScenarioPasteOutcome again = editor.ApplyScenarioText(node.Id, shown, Definition);

        Assert.DoesNotContain("[", shown, StringComparison.Ordinal);   // 머리글 · 진단 줄이 없다
        Assert.Empty(again.Problems);
        Assert.All(again.Plan!.Entries, entry => Assert.Equal(ScriptSyncKind.Unchanged, entry.Kind));
    }

    [Theory]
    [InlineData("라루: 하나\n[메모] 장식은 해석 못 한다")]
    [InlineData("라루: 하나\n<<if 없는 조건 >= 1>>\n윌로: 둘\n<<endif>>")]
    public void 엄격한_넣기는_문제가_하나라도_있으면_아무것도_넣지_않는다(string text)
    {
        // 카드의 글 칸은 넣은 뒤 모델의 모습으로 다시 그려진다 — 반쯤 넣으면 못 넣은 줄이 사람이 쓴
        // 글에서 조용히 사라진다. 그래서 카드는 strict로 넣고, 문제가 있으면 글을 칸에 둔 채 사유를 보인다.
        (ProjectEditor editor, DialogueNode node) = NewCard();
        long revision = editor.Revision;

        ScenarioPasteOutcome outcome = editor.ApplyScenarioText(node.Id, text, Definition, strict: true);

        Assert.False(outcome.Applied);
        Assert.NotEmpty(outcome.Problems);
        Assert.Equal(revision, editor.Revision);

        // 엄격하지 않으면 예전처럼 넣을 수 있는 것은 넣는다.
        Assert.True(editor.ApplyScenarioText(node.Id, text, Definition).Applied);
    }

    [Fact]
    public void 모양만_다른_글을_넣으면_아무것도_적지_않는다()
    {
        // 줄도 전환도 그대로인데 적으면 되돌리기에 빈 칸이 끼고 판이 다시 그려진다(§5-8).
        (ProjectEditor editor, DialogueNode node) = NewCard();
        editor.ApplyScenarioText(node.Id, "라루: 하나\n윌로: 둘", Definition);
        long revision = editor.Revision;

        ScenarioPasteOutcome again = editor.ApplyScenarioText(node.Id, "\n라루: 하나\n\n윌로: 둘\n\n", Definition);

        Assert.True(again.Applied);
        Assert.Equal(revision, editor.Revision);
    }

    [Fact]
    public void 새_카드의_글_칸은_비어_있다()
    {
        (ProjectEditor editor, DialogueNode node) = NewCard();

        Assert.Equal(string.Empty, WorkingDialoguePreview.ScenarioText(editor.Project, node.Id, Definition));
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static (ProjectEditor Editor, DialogueNode Node) NewCard()
    {
        var project = new StoryProject();
        var file = new StoryFile("sf_card", "카드", "story/card.vnstory.json");
        project.Files.Add(file);
        var editor = new ProjectEditor(project);

        return (editor, editor.AddDialogueNode(file.Id, name: "장면"));
    }

    /// <summary>favor >= 5 — 같은 판의 설정 노드에 두면 그 판의 노드가 쓸 수 있다.</summary>
    private static ConditionDefinition Condition(ProjectEditor editor)
    {
        SetNode set = editor.AddSetNode(editor.Project.Files[0].Id, name: "설정");
        return editor.AddCondition(set.Id, "호감 높음", "favor >= 5");
    }

    // 지금 프로젝트에서 다시 찾는다 — 되돌리기는 프로젝트를 새 객체로 갈아 끼운다.
    private static List<ScriptLine> Lines(ProjectEditor editor, DialogueNode node) =>
        editor.Project.FindDialogue(node.Id)!.Script.ActiveLines.ToList();

    private static (string Speaker, string Text) Said(ProjectEditor editor, DialogueNode node, string lineId)
    {
        LocalizedLine line = editor.Project.FindDialogue(node.Id)!.Script.Text(lineId);
        return (line.Speaker, line.Text);
    }
}
