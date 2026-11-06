using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>[대본] 판의 카드 크기</b> — 사람이 가장자리를 끌어 키운다(오른쪽 = 폭, 아래 = 글 칸 높이). 크기는 자리처럼 판의 모양이라
/// 프로젝트에 싣고(<see cref="NodeLayout.Width"/> · <see cref="NodeLayout.WritingHeight"/>, 기본이면 <c>null</c>), 놓을 때 한 번
/// 적어 되돌리기 한 칸이다.
/// </summary>
public sealed class CardSizeTests
{
    [Fact]
    public void 넓힌_폭은_되돌리기_한_칸이고_같은_폭은_기록하지_않는다()
    {
        var sample = new Sample();
        int notifications = 0;
        sample.Editor.Changed += (_, _) => notifications++;

        sample.Editor.ResizeNode(sample.Dialogue.Id, 480, 200);

        NodeLayout layout = sample.Project.FindNode(sample.Dialogue.Id)!.Layout;
        Assert.Equal((480, 200), (layout.Width, layout.WritingHeight));
        Assert.Equal(1, notifications);

        // 같은 크기로 놓은 것(눌렀다 놓기)은 적지 않는다 — 되돌리기 한 칸을 먹지 않는다.
        sample.Editor.ResizeNode(sample.Dialogue.Id, 480, 200);
        Assert.Equal(1, notifications);

        // 폭과 높이가 한 칸으로 돌아온다.
        sample.Editor.Undo();
        NodeLayout restored = sample.Project.FindNode(sample.Dialogue.Id)!.Layout;
        Assert.Null(restored.Width);
        Assert.Null(restored.WritingHeight);
    }

    [Fact]
    public void 넓힌_카드만_파일에_폭을_적고_다시_읽는다()
    {
        var sample = new Sample();

        // 아무도 안 키웠다 — 파일에 크기가 없다(기존 파일이 그대로다).
        string plain = ProjectSnapshotCodec.Encode(sample.Project);
        Assert.DoesNotContain("\"width\"", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("\"writingHeight\"", plain, StringComparison.Ordinal);

        sample.Editor.ResizeNode(sample.Dialogue.Id, 432.5, 260);
        sample.Editor.ResizeNode(sample.TargetB.Id, null, 140);
        StoryProject reloaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(sample.Project));

        Assert.Equal(432.5, reloaded.FindNode(sample.Dialogue.Id)!.Layout.Width);
        Assert.Equal(260, reloaded.FindNode(sample.Dialogue.Id)!.Layout.WritingHeight);
        Assert.Null(reloaded.FindNode(sample.TargetB.Id)!.Layout.Width);
        Assert.Equal(140, reloaded.FindNode(sample.TargetB.Id)!.Layout.WritingHeight);
        Assert.Null(reloaded.FindNode(sample.TargetA.Id)!.Layout.Width);
        Assert.Null(reloaded.FindNode(sample.TargetA.Id)!.Layout.WritingHeight);
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

    [Fact]
    public void 글_칸을_늘린_카드의_아래_자리는_늘린_만큼_아래다()
    {
        // 다음 에피소드는 부모 카드 밑에 선다 — 부모의 글 칸을 늘렸으면 그 위에 겹쳐 서지 않는다.
        var project = new StoryProject { Title = "늘린 카드" };
        var editor = new ProjectEditor(project);
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0);
        DialogueNode root = EpisodeNaming.CardFor(project, "ch01", "root")!;

        (double _, double plainY) = NodePlacement.For(project, "ch01", "next", parentEpisodeId: "root");
        editor.ResizeNode(root.Id, null, NodePlacement.WritingHeight + 300);
        (double _, double tallY) = NodePlacement.For(project, "ch01", "next", parentEpisodeId: "root");

        Assert.Equal(root.Layout.Y + NodePlacement.Row, plainY);
        Assert.Equal(plainY + 300, tallY);
    }
}
