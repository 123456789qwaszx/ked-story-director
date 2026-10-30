using Vn.Authoring.Definition;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>카드 글 칸에서 고친 글은 처음 보인 줄의 신원을 잇는다</b> (2026-10-30 소유자 보고: *"현재 텍스트 박스의 내용을 수정하는데,
/// 기존 내용을 지워버릴 경우 그게 찾을 수 없다고 표시되는 버그"*).
///
/// 전에는 글 칸의 글에 신원이 없어 넣을 때 동기화가 내용만 보고 짝을 맞췄고, 한 구간에서 두 줄 이상을 고치면 "사라진 것인지
/// 고쳐진 것인지 확신할 수 없다"며 거절했다(엄격한 넣기). 이제 처음 보인 글과 고친 글을 줄 단위로 견줘 같은 자리의 줄이
/// 신원을 잇는다(<see cref="ScenarioLineIdentity.CarryLineIds"/>).
/// </summary>
public sealed class ScenarioLineIdentityTests
{
    private static readonly GameDefinition Definition = new();

    [Fact]
    public void 줄을_모두_새로_고쳐_써도_거절하지_않고_줄마다_신원을_잇는다()
    {
        // 소유자 화면 그대로 — 네 줄을 다 지우고 새 글로 채웠다. 예전에는 "확신할 수 없습니다"로 거절했다.
        (ProjectEditor editor, DialogueNode node, string[] ids) = Card("asdas", "qweqwe", "rrrrr", "asd");

        ScenarioPasteOutcome outcome = Apply(editor, node, "점콘애\n브순\n일이\n되었더라?");

        Assert.True(outcome.Applied, string.Join(" / ", outcome.Problems));
        Assert.Equal(ids, node.Script.ActiveLines.Select(line => line.Id));
        Assert.Equal(
            ["점콘애", "브순", "일이", "되었더라?"],
            node.Script.ActiveLines.Select(line => node.Script.Text(line.Id).Text));
    }

    [Fact]
    public void 줄을_지우면_그_줄만_은퇴하고_새로_쓴_줄은_새_줄이다()
    {
        (ProjectEditor editor, DialogueNode node, string[] ids) = Card("첫 줄", "둘째 줄", "셋째 줄");

        // 둘째 줄을 통째로 지운다.
        Assert.True(Apply(editor, node, "첫 줄\n셋째 줄").Applied);
        Assert.Equal([ids[0], ids[2]], node.Script.ActiveLines.Select(line => line.Id));

        // 사이에 새 줄을 쓴다 — 새 신원이다.
        Assert.True(Apply(editor, node, "첫 줄\n새로 쓴 줄\n셋째 줄").Applied);
        string[] now = node.Script.ActiveLines.Select(line => line.Id).ToArray();
        Assert.Equal(3, now.Length);
        Assert.Equal(ids[0], now[0]);
        Assert.DoesNotContain(now[1], ids);
        Assert.Equal(ids[2], now[2]);
    }

    [Fact]
    public void 줄의_글을_비우면_그_줄은_은퇴한다()
    {
        // 빈 줄은 대본 줄이 아니다 — 되읽을 때 건너뛴다.
        (ProjectEditor editor, DialogueNode node, string[] ids) = Card("첫 줄", "둘째 줄", "셋째 줄");

        Assert.True(Apply(editor, node, "첫 줄\n\n셋째 줄").Applied);

        Assert.Equal([ids[0], ids[2]], node.Script.ActiveLines.Select(line => line.Id));
    }

    [Fact]
    public void 분기가_걸린_줄을_고쳐도_분기는_그_줄에_남는다()
    {
        // 분기 표식은 그 줄의 것이다 — 줄을 고쳐도(같은 자리의 줄이 신원을 잇는다) 표식이 따라간다.
        (ProjectEditor editor, DialogueNode node, string[] ids) = Card("첫 줄", "둘째 줄");
        DialogueNode branch = editor.AddBranchMarker(node.Id, ids[1]);
        string shown = WorkingDialoguePreview.ScenarioWriting(editor.Project, node.Id, Definition).Text;

        Assert.True(Apply(editor, node, shown.Replace("둘째 줄", "고친 둘째 줄", StringComparison.Ordinal)).Applied);

        Assert.Equal(ids, node.Script.ActiveLines.Select(line => line.Id));
        Assert.Equal([branch.Id], node.FindExtension(ids[1])!.DetourTargetNodeIds);
    }

    [Fact]
    public void 표지는_대사와_선택지_줄에만_붙는다()
    {
        string carried = ScenarioLineIdentity.CarryLineIds(
            "<<if $a>>\n라루: 하나\n<<endif>>\n\n둘",
            new Dictionary<string, int> { ["ln_1"] = 1, ["ln_2"] = 4 },
            "<<if $a>>\n라루: 하나!\n<<endif>>\n\n둘");

        Assert.Equal("<<if $a>>\n라루: 하나! #line:ln_1\n<<endif>>\n\n둘 #line:ln_2", carried);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>글 칸에 보인 글을 기준으로 신원을 이어 넣는다 — 카드 글 칸이 하는 그대로(엄격하게, 지움 확인 없이).</summary>
    private static ScenarioPasteOutcome Apply(ProjectEditor editor, DialogueNode node, string edited)
    {
        ScenarioWritingLayout layout = WorkingDialoguePreview.ScenarioWriting(editor.Project, node.Id, Definition);

        return editor.ApplyScenarioText(
            node.Id,
            ScenarioLineIdentity.CarryLineIds(layout.Text, layout.RowOfLine, edited),
            Definition,
            confirmDeletes: true,
            strict: true);
    }

    private static (ProjectEditor Editor, DialogueNode Node, string[] Ids) Card(params string[] texts)
    {
        var project = new StoryProject();
        var file = new StoryFile("sf_card", "카드", "story/card.vnstory.json");
        project.Files.Add(file);
        var editor = new ProjectEditor(project);
        DialogueNode node = editor.AddDialogueNode(file.Id, name: "장면");

        Assert.True(editor.ApplyScenarioText(node.Id, string.Join('\n', texts), Definition).Applied);
        return (editor, node, node.Script.ActiveLines.Select(line => line.Id).ToArray());
    }
}
