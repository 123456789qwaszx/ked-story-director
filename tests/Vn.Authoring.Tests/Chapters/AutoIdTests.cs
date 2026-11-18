using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>자동 ID에는 챕터와 장면이 든다</b> (2026-11-17 · <c>docs/work-orders/auto-id-orders.md</c> I1) — 에피소드 <c>{챕터}_{장면}_newNN</c>,
/// 분기 카드 <c>{챕터}_{장면}_branchNN</c>(장면은 집의 것). 그 ID가 곧 Yarn 노드 이름이라 프로젝트에서 겹치지 않게 짓는다.
/// ⛔ 전에는 챕터마다 <c>new01</c>부터 세어 챕터가 여럿이면 겹쳤다.
/// </summary>
public sealed class AutoIdTests
{
    [Fact]
    public void 에피소드ID에_챕터와_장면이_들고_번호는_그_자리에서_센다()
    {
        ProjectEditor editor = World();
        string board = TestChapter.Board(editor).Id;

        DialogueNode first = editor.AddDialogueNode(board, 0, 0, sceneId: "scene01");
        DialogueNode second = editor.AddDialogueNode(board, 0, 0, sceneId: "scene01");
        DialogueNode other = editor.AddDialogueNode(board, 0, 0, sceneId: "scene02");

        Assert.Equal(
            ["ch01_scene01_new01", "ch01_scene01_new02", "ch01_scene02_new01"],
            new[] { first, second, other }.Select(EpisodeNaming.EpisodeIdOf));
    }

    [Fact]
    public void 다른_챕터와는_겹치지_않는다()
    {
        // 장면ID는 챕터마다 scene01부터라 같아도, 챕터가 앞머리에 들어 Yarn 노드 이름이 갈린다.
        ProjectEditor editor = World();
        DialogueNode one = editor.AddDialogueNode(TestChapter.Board(editor, "ch01").Id, 0, 0, sceneId: "scene01");
        DialogueNode two = editor.AddDialogueNode(TestChapter.Board(editor, "ch02").Id, 0, 0, sceneId: "scene01");

        Assert.Equal("ch01_scene01_new01", one.Name);
        Assert.Equal("ch02_scene01_new01", two.Name);
    }

    [Fact]
    public void 이미_쓰인_카드_이름은_다른_챕터의_것이라도_피한다()
    {
        // 사람이 다른 챕터의 카드에 그 이름을 붙여 두었다 — Yarn 노드 이름은 프로젝트에서 하나라 건너뛴다.
        ProjectEditor editor = World();
        DialogueNode taken = editor.AddDialogueNode(TestChapter.Board(editor, "ch02").Id, 0, 0, sceneId: "scene09");
        editor.RenameEpisode("ch02", taken.Name, "ch01_scene01_new01");   // 카드(= Yarn 노드 이름)가 함께 간다

        DialogueNode created = editor.AddDialogueNode(TestChapter.Board(editor, "ch01").Id, 0, 0, sceneId: "scene01");

        Assert.Equal("ch01_scene01_new02", created.Name);
    }

    [Fact]
    public void 장면ID의_공백은_ID에_들지_않고_장면이_없으면_챕터만_든다()
    {
        Assert.Equal("ch01_교실_2_new", NewNames.EpisodePrefix("ch01", "교실 2"));
        Assert.Equal("ch01_new", NewNames.EpisodePrefix("ch01", null));
        Assert.Equal("ch01_scene01_branch", NewNames.BranchPrefix("ch01", "scene01"));
    }

    [Theory]
    [InlineData("ch01_scene01_new01", true)]
    [InlineData("ch01_scene01_new123", true)]
    [InlineData("ch01_scene01_new1", false)]       // 번호는 두 자리 이상
    [InlineData("ch01_scene01_new01a", false)]     // 숫자 밖이 붙으면 사람이 고친 것이다
    [InlineData("ch01_scene02_new01", false)]      // 다른 자리의 모양
    [InlineData("opening", false)]
    public void 자동_모양은_앞머리와_숫자뿐이다(string id, bool auto) =>
        Assert.Equal(auto, NewNames.IsAuto(id, NewNames.EpisodePrefix("ch01", "scene01")));

    [Fact]
    public void 분기_카드는_집의_장면으로_이름을_짓는다()
    {
        ProjectEditor editor = World();
        DialogueNode episode = editor.AddDialogueNode(TestChapter.Board(editor).Id, 0, 0, sceneId: "scene02");
        string lineId = episode.Script.ActiveLines.First().Id;

        DialogueNode first = editor.AddBranchMarker(episode.Id, lineId);
        DialogueNode second = editor.AddBranchMarker(episode.Id, lineId);

        // 분기 카드에서 다시 분기해도 집(처음 부르는 에피소드)의 장면이다.
        DialogueNode nested = editor.AddBranchMarker(first.Id, first.Script.ActiveLines.First().Id);

        Assert.Equal(
            ["ch01_scene02_branch01", "ch01_scene02_branch02", "ch01_scene02_branch03"],
            new[] { first, second, nested }.Select(card => card.Name));
    }

    [Fact]
    public void 빈_곳에_세운_분기_카드도_같은_규칙이다()
    {
        ProjectEditor editor = World();
        DialogueNode episode = editor.AddDialogueNode(TestChapter.Board(editor).Id, 0, 0, sceneId: "scene01");
        string lineId = episode.Script.ActiveLines.First().Id;
        DialogueNode made = editor.AddBranchMarker(episode.Id, lineId);
        editor.SetExitTarget(Ports(editor, episode).Single(port => port.TargetNodeId == made.Id), null);

        DialogueNode created = editor.AddBranchCardAt(Ports(editor, episode).Single(port => port.IsOpenDetour), 600, 0)!;

        Assert.Equal("ch01_scene01_branch02", created.Name);   // 앞의 카드(branch01)는 판에 남아 있다
    }

    [Fact]
    public void 분기_카드는_새_분기_제목으로_서고_제목은_파일을_오가며_되돌리기_한_칸이다()
    {
        // 분기 카드는 이름(= ID = Yarn 노드 이름)과 제목이 갈린다(I4) — 작가는 제목을, 개발자는 ID를 본다.
        ProjectEditor editor = World();
        DialogueNode episode = editor.AddDialogueNode(TestChapter.Board(editor).Id, 0, 0, sceneId: "scene01");
        DialogueNode branch = editor.AddBranchMarker(episode.Id, episode.Script.ActiveLines.First().Id);
        Assert.Equal((NewNames.Branch, NewNames.Branch), (branch.Title, EpisodeNaming.ShownNameOf(editor.Project, branch)));

        long before = editor.Revision;
        editor.SetBranchCardTitle(branch.Id, "  옥상 회상 ");
        editor.SetBranchCardTitle(branch.Id, "옥상 회상");   // 같은 값은 칸을 쓰지 않는다
        Assert.Equal(("옥상 회상", before + 1), (branch.Title, editor.Revision));
        Assert.Equal("「옥상 회상」(ch01_scene01_branch01)", EpisodeNaming.BranchLabelOf(branch));

        StoryFile reread = StoryFileJson.Read(StoryFileJson.Write(editor.Project.FindFileContainingNode(branch.Id)!));
        Assert.Equal("옥상 회상", reread.Nodes.OfType<DialogueNode>().Single(node => node.Id == branch.Id).Title);

        editor.SetBranchCardTitle(branch.Id, "");
        Assert.Equal("ch01_scene01_branch01", EpisodeNaming.ShownNameOf(editor.Project, branch));   // 비면 ID가 보인다
        editor.Undo();
        Assert.Equal("옥상 회상", editor.Project.FindDialogue(branch.Id)!.Title);
    }

    private static IReadOnlyList<ExitPort> Ports(ProjectEditor editor, DialogueNode node) =>
        NodeConnections.PortsOf(node, editor.Project);

    private static ProjectEditor World() => new(new StoryProject());
}
