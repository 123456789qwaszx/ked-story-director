using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests;

public class GraphProjectionTests
{
    [Fact]
    public void 펼친_파일은_실제_노드이고_접힌_파일은_FileProxy다()
    {
        var (project, fileA, fileB, _, dialogueB) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileA.Id }, StringComparer.Ordinal));

        Assert.Collection(
            projection.Items,
            first => Assert.Equal("nd_a", Assert.IsType<ExpandedNodeProjection>(first).NodeId),
            second =>
            {
                CollapsedFileProjection proxy = Assert.IsType<CollapsedFileProjection>(second);
                Assert.Equal(fileB.Id, proxy.FileId);
                CollapsedNodeEntry row = Assert.Single(proxy.Nodes);
                Assert.Equal(dialogueB.Id, row.NodeId);
                Assert.Equal(1, row.IncomingCount); // 실행 간선 하나
            });
    }

    [Fact]
    public void 펼친_노드에서_접힌_파일로_가는_간선은_실제_NodeId를_유지한다()
    {
        var (project, fileA, _, _, dialogueB) = BuildProject();

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
    }

    [Fact]
    public void 소스_파일이_접히면_간선_시작점도_그_노드_행으로_투영된다()
    {
        var (project, _, fileB, _, dialogueB) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(new[] { fileB.Id }, StringComparer.Ordinal));

        GraphConnectionProjection execution = projection.Connections.Single(
            connection => connection.Kind == GraphConnectionKind.ExecutionBranch);

        Assert.Equal("nd_a", execution.SourceNodeId);
        Assert.Equal(dialogueB.Id, execution.TargetNodeId);
        Assert.Equal(GraphEndpointKind.CollapsedFileNodeOutput, execution.Source.Kind);
        Assert.Equal(0, execution.Source.ProxyRowIndex);
        Assert.Equal(GraphEndpointKind.ExpandedNodeInput, execution.Target.Kind);
    }

    [Fact]
    public void 두_파일이_모두_접혀도_각_FileProxy의_실제_노드_행끼리_연결된다()
    {
        var (project, _, _, _, dialogueB) = BuildProject();

        GraphProjection projection = GraphProjectionBuilder.Build(
            project,
            new HashSet<string>(StringComparer.Ordinal));

        GraphConnectionProjection execution = projection.Connections.Single(
            connection => connection.Kind == GraphConnectionKind.ExecutionBranch);

        Assert.Equal("nd_a", execution.Source.NodeId);
        Assert.Equal(dialogueB.Id, execution.Target.NodeId);
        Assert.Equal(GraphEndpointKind.CollapsedFileNodeOutput, execution.Source.Kind);
        Assert.Equal(GraphEndpointKind.CollapsedFileNodeInput, execution.Target.Kind);
    }

    [Fact]
    public void 같은_줄의_분기_선은_열쇠가_서로_다르다()
    {
        // 화면은 끄는 동안 선을 이 열쇠로 다시 찾는다 — 같은 줄의 분기들이 한 열쇠를 나눠 가지면 모두 첫 선의
        // 경로를 받아 나머지 분기가 끊긴 것처럼 보였다(2026-10-29 소유자 보고).
        var editor = new Vn.Authoring.Editing.ProjectEditor(new StoryProject());
        StoryFile file = TestChapter.Board(editor);
        DialogueNode source = editor.AddDialogueNode(file.Id, "본문", name: "본문");
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
        DialogueNode DialogueA,
        DialogueNode DialogueB) BuildProject()
    {
        var project = new StoryProject { Title = "GraphProjection" };
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "A" });
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "B" });
        var fileA = new StoryFile("sf_a", "A", "story/a.vnstory.json");
        var fileB = new StoryFile("sf_b", "B", "story/b.vnstory.json");
        // 실행 흐름은 DialogueNode만 정한다.
        // 간선의 매개는 분기다 — 카드 간선을 만드는 출구는 분기뿐이다(기본 출구는 2026-11-17에, 갈래 출구는 2026-11-19에 걷혔다).
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
                    DetourTargetNodeIds = { "nd_b" }
                }
            }
        };
        var dialogueB = new DialogueNode("nd_b", "대사 B")
        {
            Layout = new NodeLayout { X = 650, Y = 260 }
        };

        fileA.Nodes.Add(dialogueA);
        fileB.Nodes.Add(dialogueB);
        project.Files.Add(fileA);
        project.Files.Add(fileB);

        return (project, fileA, fileB, dialogueA, dialogueB);
    }
}
