using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>[대본] 판의 카드 폭</b> — 사람이 오른쪽 가장자리를 끌어 넓힌다. 폭은 자리처럼 판의 모양이라 프로젝트에 싣고
/// (<see cref="NodeLayout.Width"/>, 기본 폭이면 <c>null</c>), 놓을 때 한 번 적어 되돌리기 한 칸이다.
/// </summary>
public sealed class CardWidthTests
{
    [Fact]
    public void 넓힌_폭은_되돌리기_한_칸이고_같은_폭은_기록하지_않는다()
    {
        var sample = new Sample();
        int notifications = 0;
        sample.Editor.Changed += (_, _) => notifications++;

        sample.Editor.ResizeNode(sample.Dialogue.Id, 480);

        Assert.Equal(480, sample.Project.FindNode(sample.Dialogue.Id)!.Layout.Width);
        Assert.Equal(1, notifications);

        // 같은 폭으로 놓은 것(눌렀다 놓기)은 적지 않는다 — 되돌리기 한 칸을 먹지 않는다.
        sample.Editor.ResizeNode(sample.Dialogue.Id, 480);
        Assert.Equal(1, notifications);

        sample.Editor.Undo();
        Assert.Null(sample.Project.FindNode(sample.Dialogue.Id)!.Layout.Width);
    }

    [Fact]
    public void 넓힌_카드만_파일에_폭을_적고_다시_읽는다()
    {
        var sample = new Sample();

        // 아무도 안 넓혔다 — 파일에 폭이 없다(기존 파일이 그대로다).
        Assert.DoesNotContain("\"width\"", ProjectSnapshotCodec.Encode(sample.Project), StringComparison.Ordinal);

        sample.Editor.ResizeNode(sample.Dialogue.Id, 432.5);
        StoryProject reloaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(sample.Project));

        Assert.Equal(432.5, reloaded.FindNode(sample.Dialogue.Id)!.Layout.Width);
        Assert.Null(reloaded.FindNode(sample.TargetA.Id)!.Layout.Width);
    }

    [Fact]
    public void 넓힌_카드의_옆자리는_넓힌_만큼_오른쪽이다()
    {
        // 분기로 다녀갈 카드는 원본의 오른쪽에 선다 — 원본을 넓혔으면 그 위에 겹쳐 서지 않는다.
        var source = new DialogueNode(name: "원본") { Layout = new NodeLayout { X = 100, Y = 0 } };
        var board = new StoryFile("sf_board", "판") { Nodes = { source } };

        (double plainX, _) = NodePlacement.BesideOf(board, source);
        source.Layout.Width = NodePlacement.CardWidth + 200;
        (double wideX, double wideY) = NodePlacement.BesideOf(board, source);

        Assert.Equal(100 + NodePlacement.Column, plainX);
        Assert.Equal(plainX + 200, wideX);
        Assert.Equal(0, wideY);

        // 넓힌 카드의 오른쪽 부분도 차 있다 — 기본 폭이면 비어 있을 x=240이지만, 0..600을 차지한 카드가 있어 아래로 비켜 선다.
        var wide = new DialogueNode(name: "넓은") { Layout = new NodeLayout { X = 0, Y = 1000, Width = 600 } };
        var other = new DialogueNode(name: "곁") { Layout = new NodeLayout { X = -80, Y = 1000 } };
        var row = new StoryFile("sf_row", "줄") { Nodes = { wide, other } };

        (double nextX, double nextY) = NodePlacement.BesideOf(row, other);

        Assert.Equal(240, nextX);
        Assert.Equal(1000 + NodePlacement.Row, nextY);
    }
}
