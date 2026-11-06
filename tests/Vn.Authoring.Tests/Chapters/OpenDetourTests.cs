using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>이어지지 않은 분기점</b> — 분기점을 눌러(포트를 끊어) 연결만 끊는다. 분기점은 이어지지 않은 채 그 줄에 남고
/// (<see cref="DialogueLineExtension.OpenDetours"/>), 다시 끌어 카드에 이으면 분기가 된다. 다녀갈 곳이 없으니 내보내지
/// 않는다. 분기점째 걷는 것은 따로다 — 「분기 지우기」(<see cref="ProjectEditor.RemoveBranchMarker"/>)와
/// <see cref="ProjectEditor.RemoveOpenDetour"/>.
/// </summary>
public sealed class OpenDetourTests
{
    [Fact]
    public void 포트를_끊으면_연결만_끊기고_분기점은_남는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        editor.SetExitTarget(Detours(editor, node).Single(), null);

        DialogueLineExtension extension = Node(editor, node.Id).FindExtension(lineId)!;
        Assert.Empty(extension.DetourTargetNodeIds);
        Assert.Equal(1, extension.OpenDetours);

        // 분기점이 이어지지 않은 포트로 남는다 — 다녀가던 카드(분기 노드)도 그대로다.
        ExitPort open = Assert.Single(Detours(editor, node));
        Assert.True(open.IsOpenDetour);
        Assert.False(open.IsConnected);
        Assert.NotNull(editor.Project.FindNode(made.Id));

        // 다녀갈 곳이 없으니 대본에 `<<detour>>`가 나가지 않는다.
        Assert.DoesNotContain(
            WorkingDialoguePreview.Compose(editor.Project, node.Id).Segments,
            segment => segment.Kind == RenderedSegmentKind.BranchDetour);
    }

    [Fact]
    public void 이어지지_않은_분기점을_카드에_이으면_분기가_되고_되돌리기_한_칸이다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);
        editor.SetExitTarget(Detours(editor, node).Single(), null);

        editor.SetExitTarget(Detours(editor, node).Single(), made.Id);

        DialogueLineExtension extension = Node(editor, node.Id).FindExtension(lineId)!;
        Assert.Equal([made.Id], extension.DetourTargetNodeIds);
        Assert.Equal(0, extension.OpenDetours);
        Assert.True(Detours(editor, node).Single().IsConnected);

        editor.Undo();

        DialogueLineExtension undone = Node(editor, node.Id).FindExtension(lineId)!;
        Assert.Empty(undone.DetourTargetNodeIds);
        Assert.Equal(1, undone.OpenDetours);
    }

    [Fact]
    public void 한_줄에_여럿이면_끊은_것만_분기점으로_남고_나머지는_그대로_다녀온다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode first = editor.AddBranchMarker(node.Id, lineId);
        DialogueNode second = editor.AddBranchMarker(node.Id, lineId);

        editor.SetExitTarget(Detours(editor, node).Single(port => port.TargetNodeId == first.Id), null);

        Assert.Equal([second.Id, null], Detours(editor, node).Select(port => port.TargetNodeId));
        Assert.Equal(
            [second.Id],
            WorkingDialoguePreview.Compose(editor.Project, node.Id).Segments
                .Where(segment => segment.Kind == RenderedSegmentKind.BranchDetour)
                .Select(segment => segment.TargetNodeId));

        // 같은 줄이 이미 가는 곳에는 잇지 않는다 — 분기점은 그대로 남는다.
        editor.SetExitTarget(Detours(editor, node).Single(port => port.IsOpenDetour), second.Id);
        Assert.Equal(1, Node(editor, node.Id).FindExtension(lineId)!.OpenDetours);
    }

    [Fact]
    public void 분기점은_저장하고_다시_열어도_남고_없으면_파일에_적지_않는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        editor.AddBranchMarker(node.Id, lineId);
        Assert.DoesNotContain("openDetours", ProjectSnapshotCodec.Encode(editor.Project), StringComparison.Ordinal);

        editor.SetExitTarget(Detours(editor, node).Single(), null);
        StoryProject reloaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project));

        DialogueLineExtension extension = reloaded.FindDialogue(node.Id)!.FindExtension(lineId)!;
        Assert.Empty(extension.DetourTargetNodeIds);
        Assert.Equal(1, extension.OpenDetours);
    }

    [Fact]
    public void 분기점째_걷는_것은_따로다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        // 「분기 지우기」는 분기점을 남기지 않는다.
        Assert.NotNull(editor.RemoveBranchMarker(node.Id, lineId, made.Id));
        Assert.Empty(Detours(editor, node));

        // 이어지지 않은 분기점은 RemoveOpenDetour로 걷는다.
        DialogueNode again = editor.AddBranchMarker(node.Id, lineId);
        editor.SetExitTarget(Detours(editor, node).Single(port => port.TargetNodeId == again.Id), null);
        editor.RemoveOpenDetour(node.Id, lineId);

        Assert.Empty(Detours(editor, node));
    }

    [Fact]
    public void 다녀가던_카드를_지우면_분기점이_이어지지_않은_채_남는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        editor.RemoveNode(made.Id);

        DialogueLineExtension extension = Node(editor, node.Id).FindExtension(lineId)!;
        Assert.Empty(extension.DetourTargetNodeIds);
        Assert.Equal(1, extension.OpenDetours);
        Assert.True(Assert.Single(Detours(editor, node)).IsOpenDetour);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static List<ExitPort> Detours(ProjectEditor editor, DialogueNode node) =>
        NodeConnections.PortsOf(Node(editor, node.Id), editor.Project)
            .Where(port => port.Kind == ExitPortKind.Detour)
            .ToList();

    /// <summary>되돌리기는 스냅샷을 새로 푼다 — 노드는 Id로 다시 찾는다.</summary>
    private static DialogueNode Node(ProjectEditor editor, string nodeId) => editor.Project.FindDialogue(nodeId)!;

    /// <summary>대사 한 줄짜리 에피소드 카드 하나.</summary>
    private static (ProjectEditor Editor, DialogueNode Node, string LineId) World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0);

        DialogueNode node = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        return (editor, node, node.Script.ActiveLines.First().Id);
    }
}
