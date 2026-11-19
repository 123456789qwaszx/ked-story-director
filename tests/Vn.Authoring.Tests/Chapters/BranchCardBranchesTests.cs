using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>분기 카드는 분기를 내지 않는다</b> (2026-11-19 소유자 · usability UX27). 분기 카드는 에피소드에서 들렀다 돌아오는 곳이라 그 안에서
/// 다시 갈라지지 않는다. ⛔ 전에는 분기 카드의 분기도 사슬로 따랐다. 그 전의 파일에 남은 것은 끊고 지우기만 받는다(검증 오류 —
/// <see cref="BranchCardValidationTests"/>). (분기 카드의 갈래 출구를 막던 시험 둘은 UX28에서 갈래 출구와 함께 걷혔다.)
/// </summary>
public sealed class BranchCardBranchesTests
{
    [Fact]
    public void 분기_카드에서는_분기를_더하지_못한다()
    {
        (ProjectEditor editor, DialogueNode root, DialogueNode branch) = World();
        long before = editor.Revision;

        Assert.True(editor.CanBranchFrom(root.Id));
        Assert.False(editor.CanBranchFrom(branch.Id));

        var refused = Assert.Throws<InvalidOperationException>(() =>
            editor.AddBranchMarker(branch.Id, branch.Script.ActiveLines.First().Id));
        Assert.Equal(ProjectEditor.BranchCardBranchesRefusal, refused.Message);
        Assert.Equal(before, editor.Revision);

        // 에피소드에서는 그대로 — 둘째 분기 카드가 선다. 그러나 분기 카드에서 그 카드로는 잇지 못한다.
        DialogueNode other = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        Assert.True(editor.CanBranchTo(root.Id, other.Id));
        Assert.False(editor.CanBranchTo(branch.Id, other.Id));
    }

    [Fact]
    public void 그_전의_파일에_남은_분기점에는_새_카드를_세우지_않고_잇지도_않는다()
    {
        (ProjectEditor editor, DialogueNode root, DialogueNode branch) = World();
        DialogueNode other = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        branch.RequireExtension(branch.Script.ActiveLines.First().Id).OpenDetourPositions.Add(0);
        ExitPort open = Assert.Single(NodeConnections.PortsOf(branch, editor.Project), port => port.IsOpenDetour);
        int cards = editor.Project.EnumerateNodes().Count();
        long before = editor.Revision;

        Assert.Null(editor.AddBranchCardAt(open, 900, 0));
        editor.SetExitTarget(open, other.Id);

        Assert.Equal(before, editor.Revision);
        Assert.Equal(cards, editor.Project.EnumerateNodes().Count());
        Assert.True(Assert.Single(NodeConnections.PortsOf(branch, editor.Project), port => port.Kind == ExitPortKind.Detour).IsOpenDetour);

        // 지우기는 받는다.
        editor.RemoveOpenDetour(branch.Id, branch.Script.ActiveLines.First().Id);
        Assert.DoesNotContain(NodeConnections.PortsOf(branch, editor.Project), port => port.Kind == ExitPortKind.Detour);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>ch01 — 시작 에피소드 root, root 첫 줄 앞의 분기 카드 하나.</summary>
    private static (ProjectEditor Editor, DialogueNode Root, DialogueNode Branch) World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");

        DialogueNode root = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        return (editor, root, editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id));
    }
}
