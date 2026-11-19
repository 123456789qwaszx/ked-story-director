using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>선이 나가는 쪽은 그 선의 도착이 정한다</b> (H1 · 2026-10-29 소유자: *"연결되어있는 루트의 현재 연결되어 있는 하나의
/// 루트의 선택지 방향이 바뀌길 원한거야"*). 도착 카드가 출발 카드보다 왼쪽이면 왼쪽 — 챕터 시작 카드가 어디 있든 상관없다.
/// </summary>
public sealed class EdgeSideTests
{
    [Fact]
    public void 분기_포트는_도착이_출발_카드보다_왼쪽일_때만_왼쪽이다()
    {
        // 챕터 시작 카드(start)는 멀리 오른쪽이다 — 예전 규칙(시작 카드 기준)이면 scene의 포트가 늘 왼쪽이었다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "start", "start", 0, 0, sceneId: "start");
        editor.AddEpisode("ch01", "scene", "scene", 0, 0, sceneId: "scene");
        editor.MoveNode(EpisodeNaming.CardFor(editor.Project, "ch01", "start")!.Id, 2000, 0);
        DialogueNode scene = EpisodeNaming.CardFor(editor.Project, "ch01", "scene")!;
        editor.MoveNode(scene.Id, 800, 0);
        string lineId = scene.Script.ActiveLines.First().Id;
        DialogueNode branch = editor.AddBranchMarker(scene.Id, lineId);

        editor.MoveNode(branch.Id, 1200, 300);
        Assert.False(DetourPort(editor, scene.Id).OnLeft);

        editor.MoveNode(branch.Id, 100, 300);
        Assert.True(DetourPort(editor, scene.Id).OnLeft);
    }

    [Fact]
    public void 같은_자리면_늘_쓰던_오른쪽이다()
    {
        Assert.True(EdgeSide.GoesLeft(800, 210, 100, 210));
        Assert.False(EdgeSide.GoesLeft(800, 210, 800, 210));
        Assert.False(EdgeSide.GoesLeft(800, 210, 1500, 210));
    }

    [Fact]
    public void 두_카드의_지금_폭으로_잰_가운데끼리_잰다()
    {
        // 2026-11-19 소유자 — 「에피소드의 현재 가로넓이를 기준으로」(usability UX6). ⛔ 왼쪽 X끼리 재면 셋 다 「오른쪽」이었다.
        // 넓힌 출발(800 ~ 1800, 가운데 1300) 아래, 왼쪽 반의 도착(900 ~ 1110, 가운데 1005)은 왼쪽이다.
        Assert.True(EdgeSide.GoesLeft(800, 1000, 900, 210));

        // 오른쪽 반의 도착(1400 ~ 1610, 가운데 1505)은 오른쪽이다.
        Assert.False(EdgeSide.GoesLeft(800, 1000, 1400, 210));

        // 넓힌 도착은 제 가운데로 잰다 — 왼쪽 끝이 출발보다 오른쪽이어도(850) 가운데(850 + 300)가 출발의 가운데(800 + 105)보다 오른쪽이면 오른쪽,
        // 왼쪽 끝이 출발보다 왼쪽이어도(500) 가운데(500 + 600)가 오른쪽이면 오른쪽이다.
        Assert.False(EdgeSide.GoesLeft(800, 210, 850, 600));
        Assert.False(EdgeSide.GoesLeft(800, 210, 500, 1200));
        Assert.True(EdgeSide.GoesLeft(800, 210, 300, 600));
    }

    [Fact]
    public void 넓힌_카드의_분기_포트는_도착의_가운데가_카드_가운데보다_왼쪽이면_왼쪽이다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "scene", "scene", 0, 0, sceneId: "scene");
        DialogueNode scene = EpisodeNaming.CardFor(editor.Project, "ch01", "scene")!;
        editor.MoveNode(scene.Id, 800, 0);
        editor.ResizeNode(scene.Id, 1000, null);   // 800 ~ 1800, 가운데 1300
        DialogueNode branch = editor.AddBranchMarker(scene.Id, scene.Script.ActiveLines.First().Id);

        editor.MoveNode(branch.Id, 900, 300);   // 왼쪽 X는 출발보다 오른쪽 — ⛔ 예전 규칙이면 오른쪽
        Assert.True(DetourPort(editor, scene.Id).OnLeft);

        editor.MoveNode(branch.Id, 1400, 300);
        Assert.False(DetourPort(editor, scene.Id).OnLeft);
    }

    private static GraphOutputPortProjection DetourPort(ProjectEditor editor, string nodeId) =>
        GraphProjectionBuilder
            .Build(editor.Project, new HashSet<string>(editor.Project.Files.Select(file => file.Id)))
            .Items.OfType<ExpandedNodeProjection>()
            .Single(item => item.NodeId == nodeId)
            .OutputPorts.Single(port => port.ExecutionPort?.Kind == ExitPortKind.Detour);
}
