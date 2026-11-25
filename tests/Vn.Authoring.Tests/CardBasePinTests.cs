using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>새 카드의 처음 크기와 기본 크기 고정</b>(2026-11-25 소유자 — 「생성 시에 세팅해주는 기본크기는 별개여야 … 가로 세로 크기 맞춤을 무시 + 기본크기로
/// 돌려보내는 일종의 최소화 모드 / 반대로 그 상태에서 다시 더블 클릭하면 가로 세로 크기맞춤의 영향을 받도록」). 처음 크기는 앱이 대고
/// (<see cref="Editing.ProjectEditor.NewCardSize"/>) 카드에 적힌다. 기본 크기 고정은 지금 크기를 풀면 돌아갈 크기로 적어 두고 카드를 그 크기로 둔다 — 되돌리기 한 칸이고
/// 프로젝트에 싣는다.
/// </summary>
public sealed class CardBasePinTests
{
    [Fact]
    public void 새_카드는_앱이_대는_크기로_서고_가장_작은_크기와_같은_쪽은_적지_않는다()
    {
        var sample = new Sample();
        sample.Editor.NewCardSize = () => (320, 160);

        NodeLayout made = sample.Editor.AddDialogueNode(sample.File.Id, "본문", name: "새 카드").Layout;
        Assert.Equal((320, 160), (made.Width, made.WritingHeight));
        Assert.True(made.FitHeight);
        Assert.False(made.FitWidth);

        // 가장 작은 크기(210 · 88)와 같은 쪽은 적지 않는다 — 판의 기본(null)이다.
        sample.Editor.NewCardSize = () => (BoardSize.CardWidth, 200);
        NodeLayout narrow = sample.Editor.AddDialogueNode(sample.File.Id, "본문", name: "좁은 카드").Layout;
        Assert.Null(narrow.Width);
        Assert.Equal(200, narrow.WritingHeight);
    }

    [Fact]
    public void 대는_곳이_없으면_가장_작은_크기로_선다()
    {
        var sample = new Sample();
        NodeLayout made = sample.Project.FindNode(sample.Dialogue.Id)!.Layout;
        Assert.Null(made.Width);
        Assert.Null(made.WritingHeight);
    }

    [Fact]
    public void 기본_크기로_고정하면_지금_크기를_적어_두고_기본_크기로_두며_되돌리기_한_칸이다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;
        sample.Editor.ResizeNode(id, 520, 300, fitWidth: true);
        int notifications = 0;
        sample.Editor.Changed += (_, _) => notifications++;

        sample.Editor.PinToBaseSize(id, 320, 160);

        NodeLayout layout = sample.Project.FindNode(id)!.Layout;
        Assert.True(layout.PinnedToBase);
        Assert.Equal((320, 160), (layout.Width, layout.WritingHeight));
        Assert.Equal((520, 300), (layout.UnpinnedWidth, layout.UnpinnedWritingHeight));
        Assert.True(layout.FitWidth);    // 맞춤은 켜진 채 쉰다
        Assert.True(layout.FitHeight);
        Assert.Equal(1, notifications);

        // 이미 기본 크기로 고정했으면 적지 않는다.
        sample.Editor.PinToBaseSize(id, 320, 160);
        Assert.Equal(1, notifications);

        sample.Editor.Undo();
        NodeLayout restored = sample.Project.FindNode(id)!.Layout;
        Assert.False(restored.PinnedToBase);
        Assert.Equal((520, 300), (restored.Width, restored.WritingHeight));
        Assert.Null(restored.UnpinnedWidth);
    }

    [Fact]
    public void 풀면_접기_전_크기로_돌아가고_되돌리기_한_칸이다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;
        sample.Editor.ResizeNode(id, null, 240);
        sample.Editor.PinToBaseSize(id, 320, 160);

        sample.Editor.UnpinBaseSize(id);

        NodeLayout layout = sample.Project.FindNode(id)!.Layout;
        Assert.False(layout.PinnedToBase);
        Assert.Null(layout.Width);
        Assert.Equal(240, layout.WritingHeight);
        Assert.Null(layout.UnpinnedWidth);
        Assert.Null(layout.UnpinnedWritingHeight);

        // 기본 크기로 고정하지 않은 카드를 풀면 아무것도 적지 않는다.
        int notifications = 0;
        sample.Editor.Changed += (_, _) => notifications++;
        sample.Editor.UnpinBaseSize(id);
        Assert.Equal(0, notifications);

        sample.Editor.Undo();
        Assert.True(sample.Project.FindNode(id)!.Layout.PinnedToBase);
    }

    [Fact]
    public void 기본_크기로_고정한_동안_글_맞춤은_크기를_적지_않는다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;
        sample.Editor.SetNodeFit(id, fitWidth: true, fitHeight: true);
        sample.Editor.PinToBaseSize(id, 320, 160);

        sample.Editor.FitNodeSize(id, 600, 400);

        NodeLayout layout = sample.Project.FindNode(id)!.Layout;
        Assert.Equal((320, 160), (layout.Width, layout.WritingHeight));
    }

    [Fact]
    public void 손으로_크기를_정하면_기본_크기_고정이_풀린다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;
        sample.Editor.ResizeNode(id, 500, 260);
        sample.Editor.PinToBaseSize(id, 320, 160);

        // 같은 크기로 놓아도 기본 크기 고정은 풀린다 — 손으로 정한 크기다.
        sample.Editor.ResizeNode(id, 320, 160, fitWidth: false);

        NodeLayout layout = sample.Project.FindNode(id)!.Layout;
        Assert.False(layout.PinnedToBase);
        Assert.Equal((320, 160), (layout.Width, layout.WritingHeight));
        Assert.Null(layout.UnpinnedWidth);
        Assert.Null(layout.UnpinnedWritingHeight);
    }

    [Fact]
    public void 기본_크기로_고정한_카드만_파일에_적고_다시_읽는다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;

        string plain = ProjectSnapshotCodec.Encode(sample.Project);
        Assert.DoesNotContain("pinnedToBase", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("unpinned", plain, StringComparison.Ordinal);

        sample.Editor.ResizeNode(id, 480, null);
        sample.Editor.PinToBaseSize(id, 320, 160);
        string json = ProjectSnapshotCodec.Encode(sample.Project);
        Assert.Matches("\"pinnedToBase\":\\s*true", json);
        Assert.Matches("\"unpinnedWidth\":\\s*480", json);
        Assert.DoesNotContain("unpinnedWritingHeight", json, StringComparison.Ordinal);   // 풀면 가장 작은 높이다

        NodeLayout reloaded = ProjectSnapshotCodec.Decode(json).FindNode(id)!.Layout;
        Assert.True(reloaded.PinnedToBase);
        Assert.Equal((320, 160), (reloaded.Width, reloaded.WritingHeight));
        Assert.Equal(480, reloaded.UnpinnedWidth);
        Assert.Null(reloaded.UnpinnedWritingHeight);
    }

    [Fact]
    public void 기본_크기로_고정하지_않은_카드에_풀면_돌아갈_크기가_있으면_깨진_파일이다()
    {
        var sample = new Sample();
        string id = sample.Dialogue.Id;
        sample.Editor.ResizeNode(id, 480, null);
        sample.Editor.PinToBaseSize(id, 320, 160);
        string json = ProjectSnapshotCodec.Encode(sample.Project);
        string broken = System.Text.RegularExpressions.Regex.Replace(json, "\"pinnedToBase\":\\s*true,?", string.Empty);

        Assert.ThrowsAny<Exception>(() => ProjectSnapshotCodec.Decode(broken));
    }
}
