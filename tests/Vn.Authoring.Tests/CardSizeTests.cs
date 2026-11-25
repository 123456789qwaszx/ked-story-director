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
    public void 새_카드는_세로_글_맞춤만_켜고_서고_켠_쪽만_파일에_적는다()
    {
        // 2026-11-25 소유자 — 가로 · 세로 글 맞춤(카드마다 · 새 카드는 「세로만 켜짐」). 칸이 없는 옛 파일의 카드는 둘 다 꺼져 있다.
        var sample = new Sample();
        NodeLayout made = sample.Project.FindNode(sample.Dialogue.Id)!.Layout;
        Assert.False(made.FitWidth);
        Assert.True(made.FitHeight);

        string json = ProjectSnapshotCodec.Encode(sample.Project);
        Assert.Matches("\"fitHeight\":\\s*true", json);
        Assert.DoesNotContain("fitWidth", json, StringComparison.Ordinal);

        sample.Editor.SetNodeFit(sample.Dialogue.Id, fitWidth: true, fitHeight: false);
        StoryProject reloaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(sample.Project));
        Assert.True(reloaded.FindNode(sample.Dialogue.Id)!.Layout.FitWidth);
        Assert.False(reloaded.FindNode(sample.Dialogue.Id)!.Layout.FitHeight);

        // 옛 파일 — 칸이 없으면 꺼져 있다(열기만 해서 크기가 바뀌지 않는다).
        StoryProject old = ProjectSnapshotCodec.Decode(
            System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\"fitHeight\":\\s*true", string.Empty));
        Assert.False(old.FindNode(sample.Dialogue.Id)!.Layout.FitHeight);
        Assert.False(old.FindNode(sample.Dialogue.Id)!.Layout.FitWidth);
    }

    [Fact]
    public void 맞춤_켜기는_되돌리기_한_칸이고_맞춘_크기는_그_칸에_든다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;
        int notifications = 0;
        sample.Editor.Changed += (_, _) => notifications++;

        sample.Editor.SetNodeFit(id, fitWidth: true, fitHeight: true);
        sample.Editor.FitNodeSize(id, 480, 200);   // 판이 곧바로 맞춰 적는다 — 제 칸이 없다

        Assert.Equal(2, notifications);
        Assert.Equal((480, 200), (sample.Project.FindNode(id)!.Layout.Width, sample.Project.FindNode(id)!.Layout.WritingHeight));

        // 같은 크기는 적지 않는다.
        sample.Editor.FitNodeSize(id, 480, 200);
        Assert.Equal(2, notifications);

        // 한 번 되돌리면 켜기 전 — 맞춤도 크기도.
        sample.Editor.Undo();
        NodeLayout restored = sample.Project.FindNode(id)!.Layout;
        Assert.False(restored.FitWidth);
        Assert.Null(restored.Width);
        Assert.Null(restored.WritingHeight);

        // 다시 실행 목록을 건드리지 않는다 — 맞춘 크기를 적어도 다시 실행이 남는다.
        Assert.True(sample.Editor.CanRedo);
        sample.Editor.FitNodeSize(id, null, 150);
        Assert.True(sample.Editor.CanRedo);
    }

    [Fact]
    public void 크기를_적으며_그_쪽_맞춤을_끌_수_있고_한_칸이다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;
        sample.Editor.SetNodeFit(id, fitWidth: true, fitHeight: true);

        sample.Editor.ResizeNode(id, null, 200, fitHeight: false);

        NodeLayout layout = sample.Project.FindNode(id)!.Layout;
        Assert.True(layout.FitWidth);
        Assert.False(layout.FitHeight);
        Assert.Equal(200, layout.WritingHeight);

        // 크기는 그대로인데 맞춤만 끄는 것도 적는다.
        sample.Editor.ResizeNode(id, null, 200, fitWidth: false);
        Assert.False(sample.Project.FindNode(id)!.Layout.FitWidth);

        sample.Editor.Undo();
        Assert.True(sample.Project.FindNode(id)!.Layout.FitWidth);
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
        // 새 에피소드는 판의 맨 아래 카드 밑에 선다 — 그 카드의 글 칸을 늘렸으면 그 위에 겹쳐 서지 않는다.
        var project = new StoryProject { Title = "늘린 카드" };
        var editor = new ProjectEditor(project);
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0, sceneId: "root");
        DialogueNode root = EpisodeNaming.CardFor(project, "ch01", "root")!;

        (double _, double plainY) = NodePlacement.For(project, "ch01", "next");
        editor.ResizeNode(root.Id, null, NodePlacement.WritingHeight + 300);
        (double _, double tallY) = NodePlacement.For(project, "ch01", "next");

        Assert.Equal(root.Layout.Y + NodePlacement.Row, plainY);
        Assert.Equal(plainY + 300, tallY);
    }
}
