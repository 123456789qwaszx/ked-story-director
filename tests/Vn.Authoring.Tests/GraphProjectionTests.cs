using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests;

public class GraphProjectionTests
{
    [Fact]
    public void 펼친_파일은_실제_노드이고_접힌_파일은_FileProxy다()
    {
        var (project, fileA, fileB, setA, _, dialogueB) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileA.Id }, StringComparer.Ordinal));

        // 설정노드(setA)는 카드가 없다 (2026-10-29 · L6) — 펼친 판에는 대사 노드 하나만 선다.
        Assert.Collection(
            projection.Items,
            first => Assert.Equal("nd_a", Assert.IsType<ExpandedNodeProjection>(first).NodeId),
            second =>
            {
                CollapsedFileProjection proxy = Assert.IsType<CollapsedFileProjection>(second);
                Assert.Equal(fileB.Id, proxy.FileId);
                CollapsedNodeEntry row = Assert.Single(proxy.Nodes);
                Assert.Equal(dialogueB.Id, row.NodeId);
                Assert.Equal(1, row.IncomingCount); // 실행 간선 하나 — 조건 공급선은 안 그린다
            });
    }

    [Fact]
    public void 펼친_노드에서_접힌_파일로_가는_간선은_실제_NodeId를_유지한다()
    {
        var (project, fileA, _, setA, _, dialogueB) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileA.Id }, StringComparer.Ordinal));

        GraphConnectionProjection execution = projection.Connections.Single(
            connection => connection.Kind == GraphConnectionKind.ExecutionBranch);

        Assert.Equal("nd_a", execution.SourceNodeId);
        Assert.Equal(dialogueB.Id, execution.TargetNodeId);
        Assert.Equal(GraphEndpointKind.ExpandedNodeOutput, execution.Source.Kind);
        Assert.Equal(GraphEndpointKind.CollapsedFileNodeInput, execution.Target.Kind);
        Assert.Equal(0, execution.Target.ProxyRowIndex);
        _ = setA;
    }

    [Fact]
    public void 조건_공급선은_그리지_않는다()
    {
        // 2026-08-17 소유자 — 작가의 조건·변수는 챕터 단위 전역이다. 범위가 판 전체이므로
        // 선을 그리면 대사노드 수만큼 거미줄이 되고, 그 선이 무엇을 정하지도 않는다.
        // 구판 프로젝트에 남은 Settings 링크 데이터는 조용히 무시된다.
        var (project, fileA, _, _, _, _) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileA.Id }, StringComparer.Ordinal));

        // 그려지는 선은 모두 실행 출구다 — 링크에서 나오는 선은 없다.
        Assert.All(projection.Connections, connection => Assert.NotNull(connection.ExecutionPort));
    }

    [Fact]
    public void 소스_파일이_접히면_간선_시작점도_그_노드_행으로_투영된다()
    {
        var (project, _, fileB, setA, _, dialogueB) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileB.Id }, StringComparer.Ordinal));

        GraphConnectionProjection execution = projection.Connections.Single(
            connection => connection.Kind == GraphConnectionKind.ExecutionBranch);

        Assert.Equal("nd_a", execution.SourceNodeId);
        Assert.Equal(dialogueB.Id, execution.TargetNodeId);
        Assert.Equal(GraphEndpointKind.CollapsedFileNodeOutput, execution.Source.Kind);
        Assert.Equal(0, execution.Source.ProxyRowIndex); // 숨은 설정노드는 행을 차지하지 않는다
        Assert.Equal(GraphEndpointKind.ExpandedNodeInput, execution.Target.Kind);
        _ = setA;
    }

    [Fact]
    public void 두_파일이_모두_접혀도_각_FileProxy의_실제_노드_행끼리_연결된다()
    {
        var (project, _, _, setA, _, dialogueB) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(StringComparer.Ordinal));

        GraphConnectionProjection execution = projection.Connections.Single(
            connection => connection.Kind == GraphConnectionKind.ExecutionBranch);

        Assert.Equal("nd_a", execution.Source.NodeId);
        Assert.Equal(dialogueB.Id, execution.Target.NodeId);
        Assert.Equal(GraphEndpointKind.CollapsedFileNodeOutput, execution.Source.Kind);
        Assert.Equal(GraphEndpointKind.CollapsedFileNodeInput, execution.Target.Kind);
        _ = setA;
    }

    [Fact]
    public void SetNode는_카드가_없다()
    {
        // SetNode는 조건 공급자다 — 실행 출구 포트가 애초에 없고, 남아 있던
        // "이 챕터에 공급 중" 포트도 2026-08-22에 사라졌다 (소유자). 2026-10-29(L6)부터는
        // 카드째 숨는다: 그 안을 채우던 편집기가 걷혀 눌러도 빈 칸이었다. 데이터는 그대로다.
        var (project, fileA, _, setA, _, _) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileA.Id }, StringComparer.Ordinal));

        Assert.DoesNotContain(
            projection.Items.OfType<ExpandedNodeProjection>(),
            item => item.NodeId == setA.Id);
        Assert.Contains(project.EnumerateNodes(), node => node.Id == setA.Id);
    }

    [Fact]
    public void 같은_줄의_분기_선은_열쇠가_서로_다르다()
    {
        // 화면은 끄는 동안 선을 이 열쇠로 다시 찾는다 — 같은 줄의 분기들이 한 열쇠를 나눠 가지면 모두 첫 선의
        // 경로를 받아 나머지 분기가 끊긴 것처럼 보였다(2026-10-29 소유자 보고).
        var editor = new Vn.Authoring.Editing.ProjectEditor(new StoryProject());
        StoryFile file = editor.AddStoryFile("판");
        DialogueNode source = editor.AddDialogueNode(file.Id, name: "본문");
        string lineId = source.Script.ActiveLines.Single().Id;
        editor.AddBranchMarker(source.Id, lineId);
        editor.AddBranchMarker(source.Id, lineId);

        IReadOnlyList<GraphConnectionProjection> connections = GraphProjectionBuilder.Build(
            editor.Project,
            new HashSet<string>([file.Id], StringComparer.Ordinal)).Connections;

        Assert.Equal(2, connections.Count(connection => connection.ExecutionPort?.Kind == Vn.Authoring.Flow.ExitPortKind.Detour));
        Assert.Equal(connections.Count, connections.Select(connection => connection.Key).Distinct().Count());
    }

    [Fact]
    public void 직교_간선은_수평과_수직_선분만_만든다()
    {
        IReadOnlyList<GraphPosition> points = OrthogonalEdgeRouter.Route(
            new GraphPosition(100, 100),
            new GraphPosition(20, 260));

        Assert.Equal(4, points.Count);

        for (int index = 1; index < points.Count; index++)
        {
            GraphPosition previous = points[index - 1];
            GraphPosition current = points[index];
            Assert.True(previous.X == current.X || previous.Y == current.Y);
        }

        Assert.Equal(new GraphPosition(100, 100), points[0]);
        Assert.Equal(new GraphPosition(20, 260), points[^1]);
        Assert.True(points[1].X > 100);
    }

    [Fact]
    public void 왼쪽으로_나가는_간선은_거울에_비춘_모양이다()
    {
        // 대상이 왼쪽에 충분히 있으면 둘 사이 가운데에서 꺾는다.
        IReadOnlyList<GraphPosition> far = OrthogonalEdgeRouter.Route(
            new GraphPosition(800, 100),
            new GraphPosition(310, 400),
            leftward: true);

        Assert.Equal(555, far[1].X);
        Assert.Equal(555, far[2].X);

        // 대상이 오른쪽이거나 가까우면 둘 다의 왼쪽으로 돈다 — 오른쪽으로 새지 않는다.
        IReadOnlyList<GraphPosition> near = OrthogonalEdgeRouter.Route(
            new GraphPosition(100, 100),
            new GraphPosition(130, 400),
            leftward: true);

        Assert.Equal(100 - 48, near[1].X);
        Assert.All(near, point => Assert.True(point.X <= 130));
    }

    private static (
        StoryProject Project,
        StoryFile FileA,
        StoryFile FileB,
        SetNode SetA,
        DialogueNode DialogueA,
        DialogueNode DialogueB) BuildProject()
    {
        var project = new StoryProject { Title = "GraphProjection" };
        var fileA = new StoryFile("sf_a", "A", "story/a.vnstory.json");
        var fileB = new StoryFile("sf_b", "B", "story/b.vnstory.json");
        var setA = new SetNode("nd_set", "설정")
        {
            Layout = new NodeLayout { X = 80, Y = 100 }
        };
        // 실행 흐름은 DialogueNode만 정한다. SetNode는 조건 공급자일 뿐이다.
        // 간선의 매개는 조건 갈래 출구다 (2026-08-21) — 기본 출구는 커스텀 노드에서
        // 폐지됐고 엑셀노드의 것은 카드가 아니라 철도 칩에 살아, 카드 간선을 만드는
        // 출구는 갈래 하나뿐이다.
        var script = new ScriptDocument();
        script.Lines.Add(new ScriptLine("ln_a1"));

        var dialogueA = new DialogueNode("nd_a", "대사 A")
        {
            Layout = new NodeLayout { X = 340, Y = 100 },
            Script = script,
            LineExtensions =
            {
                new DialogueLineExtension("ln_a1")
                {
                    Transitions = { LineConditionTransition.BeginIf("cd_x") }
                }
            },
            BranchExits = { ["ln_a1"] = "nd_b" }
        };
        var dialogueB = new DialogueNode("nd_b", "대사 B")
        {
            Layout = new NodeLayout { X = 650, Y = 260 }
        };

        fileA.Nodes.Add(setA);
        fileA.Nodes.Add(dialogueA);
        fileB.Nodes.Add(dialogueB);
        project.Files.Add(fileA);
        project.Files.Add(fileB);
        project.Links.Add(new NodeLink(
            "lk_settings",
            NodeLinkKind.Settings,
            setA.Id,
            dialogueB.Id));

        return (project, fileA, fileB, setA, dialogueA, dialogueB);
    }
}
