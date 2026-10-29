using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>X4 — 대사노드는 생성 즉시 편집 대상이다. 전용 대본과 첫 빈 줄이 함께 태어난다.</summary>
public class DialogueNodeCreationTests
{
    private static (ProjectEditor Editor, StoryFile File) BuildEditor()
    {
        var project = new StoryProject();
        var file = new StoryFile("sf_x4", "테스트", "story/x4.vnstory.json");
        project.Files.Add(file);
        int next = 0;
        return (new ProjectEditor(project, newLineId: () => $"ln_{++next:D3}"), file);
    }

    [Fact]
    public void 노드를_만들면_전용_대본과_첫_빈_줄이_함께_생긴다()
    {
        (ProjectEditor editor, StoryFile file) = BuildEditor();

        DialogueNode node = editor.AddDialogueNode(file.Id, name: "장면 1");

        ScriptDocument script = node.Script;
        Assert.Equal("장면 1 대본", script.Name);

        // 생성 직후 빈 라인 목록이 아니라 첫 줄이 준비되어 바로 타이핑한다.
        ScriptLine first = Assert.Single(script.ActiveLines);
        Assert.Equal("ln_001", first.Id);
        Assert.Equal(LocalizedLine.Empty, script.Text(first.Id));

        // 노드·대본·첫 줄이 한 번의 편집이다 — 되돌리기 한 번에 전부 사라진다.
        editor.Undo();
        Assert.Empty(editor.Project.EnumerateScripts());
        Assert.Empty(editor.Project.EnumerateNodes());
    }

    // ⛔ `명시한_대본을_주면_그_대본을_그대로_쓴다` · `대본_없는_옛_노드는_EnsureDialogueScript로_처음_쓸_수_있게_된다`는
    //    대본이 카드 안으로 들어오며(S2) 걷혔다 — 대본을 골라 잇는 길도, 대본 없는 카드도 없다. 대본 없이 저장된 옛 카드는
    //    읽을 때 빈 대본을 받는다(`ScriptTable`).

    [Fact]
    public void 자동_대본도_저장_형식은_기존_그대로_왕복한다()
    {
        // 로드 호환 — 새 형식 필드 없이 기존 scripts/files 구조로 저장된다.
        (ProjectEditor editor, StoryFile file) = BuildEditor();
        editor.AddDialogueNode(file.Id, name: "장면");

        StoryProject decoded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project));

        DialogueNode original = editor.Project.EnumerateNodes().OfType<DialogueNode>().Single();
        DialogueNode node = Assert.Single(decoded.EnumerateNodes().OfType<DialogueNode>());
        Assert.Equal(original.Script.Id, node.Script.Id);
        Assert.Single(node.Script.ActiveLines);
    }
}
