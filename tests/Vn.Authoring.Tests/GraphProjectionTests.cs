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

        Assert.Collection(
            projection.Items,
            first => Assert.Equal(setA.Id, Assert.IsType<ExpandedNodeProjection>(first).NodeId),
            second => Assert.IsType<ExpandedNodeProjection>(second),
            third =>
            {
                CollapsedFileProjection proxy = Assert.IsType<CollapsedFileProjection>(third);
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

        Assert.DoesNotContain(projection.Connections,
            connection => connection.Kind == GraphConnectionKind.Settings);
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
        Assert.Equal(1, execution.Source.ProxyRowIndex);
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
    public void 펼쳐진_SetNode에는_포트가_하나도_없다()
    {
        // SetNode는 조건 공급자다 — 실행 출구 포트가 애초에 없고, 남아 있던
        // "이 챕터에 공급 중" 포트도 2026-08-22에 사라졌다 (소유자). 공급 범위가 판
        // 전체가 된 2026-08-17 이후로 이을 곳이 없어 사실 표시로만 서 있었는데,
        // 늘 켜진 포트는 "여기서 무언가를 이을 수 있다"는 거짓말이었다.
        var (project, fileA, _, setA, _, _) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileA.Id }, StringComparer.Ordinal));

        ExpandedNodeProjection node = projection.Items
            .OfType<ExpandedNodeProjection>()
            .Single(item => item.NodeId == setA.Id);

        Assert.Empty(node.OutputPorts);
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
        var script = new ScriptDocument("sc_a", "대본 A");
        script.Lines.Add(new ScriptLine("ln_a1"));

        var dialogueA = new DialogueNode("nd_a", "대사 A")
        {
            Layout = new NodeLayout { X = 340, Y = 100 },
            ScriptId = script.Id,
            LineExtensions =
            {
                new DialogueLineExtension("ln_a1")
                {
                    Transition = LineConditionTransition.BeginIf("cd_x")
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
        project.Scripts.Add(script);
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
