using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>카드 글 칸은 쓰는 대로 들어간다</b> (2026-10-30 소유자: *"현재 텍스트 박스의 내용을 수정하는데, 기존 내용을
/// 지워버릴 경우 그게 찾을 수 없다고 표시되는 버그"* · *"굳이 꼭 오른쪽 텍스트편집만이 아니라, 에피소드 노드 속의
/// 텍스트 편집하는 게 그대로 반영이 바로바로 되기를"*).
///
/// 치다가 잠깐 멈추면 칸을 나오지 않아도 넣는다(시험은 <see cref="GraphEditorView.CommitLiveWriting"/>로 그 멈춤을 대신한다).
/// 고친 줄은 처음 보인 줄의 신원을 이어서, 줄을 다 고쳐 써도 "확신할 수 없다"며 거절하지 않는다. 줄을 지우는 것만은
/// 칸을 나올 때 넣는다 — 다 지우고 새로 쓰는 도중의 모습에서 줄을 은퇴시키면 그 줄의 분기 · 연출이 떨어져 나간다.
/// </summary>
public sealed class CardLiveWritingTests
{
    private const int GraphTab = 1;

    [Fact]
    public void 줄을_모두_고쳐_쓰고_멈추면_칸을_나오지_않아도_들어가고_곁기둥에_비친다() => HeadlessUi.Run(() =>
    {
        // 소유자 화면 그대로 — 네 줄을 다 지우고 새 글로 채웠다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) =
            Stage("asdas", "qweqwe", "rrrrr", "asd");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "점콘애\n브순\n일이\n되었더라?";
        Pause(window);

        Assert.Equal(["점콘애", "브순", "일이", "되었더라?"], Said(card));
        Assert.Equal(ids, card.Script.ActiveLines.Select(line => line.Id));

        // 글자만 바뀌었다 — 판을 다시 그리지 않아 쓰던 칸이 그대로 초점을 쥐고 있다.
        Assert.Same(box, Box(window, card.Id));
        Assert.True(box.IsFocused);
        Assert.Null(ToolTip.GetTip(box));

        // 곁기둥(대사 노드 편집기)에도 바로 비친다.
        IEnumerable<string?> side = window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct()
            .Single(editor => editor.IsVisible && editor.NodeId == card.Id)
            .GetLogicalDescendants().OfType<TextBox>().Select(text => text.Text);
        Assert.Contains("점콘애", side);
        Assert.Contains("되었더라?", side);

        window.Close();
    });

    [Fact]
    public void 줄을_모두_고쳐_쓰고_칸을_나와도_거절하지_않는다() => HeadlessUi.Run(() =>
    {
        // 예전에는 "사라진 것인지 고쳐진 것인지 확신할 수 없습니다"로 통째로 거절했다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) =
            Stage("asdas", "qweqwe", "rrrrr", "asd");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "라루: 점콘애\n브순\n일이\n되었더라?";
        Release(window);

        Assert.Equal(["점콘애", "브순", "일이", "되었더라?"], Said(card));
        Assert.Equal(ids, card.Script.ActiveLines.Select(line => line.Id));
        Assert.DoesNotContain("넣지 못했습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 다_지우고_새로_쓰는_도중에는_줄을_은퇴시키지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) =
            Stage("첫 줄", "둘째 줄", "셋째 줄");
        DialogueNode branch = session.Editor.AddBranchMarker(card.Id, ids[2]);
        Dispatcher.UIThread.RunJobs();

        TextBox box = Box(window, card.Id);
        box.Focus();

        // 다 지웠다 · 첫 줄만 새로 썼다 — 아직 쓰는 중이다. 줄이 줄어드는 넣기는 미룬다(칸도 붉히지 않는다).
        foreach (string partial in new[] { string.Empty, "하나" })
        {
            box.Text = partial;
            Pause(window);

            Assert.Equal(["첫 줄", "둘째 줄", "셋째 줄"], Said(card));
            Assert.Null(ToolTip.GetTip(Box(window, card.Id)));
        }

        // 세 줄을 다 채웠다 — 들어가고, 셋째 줄의 분기도 그 줄에 남는다.
        Box(window, card.Id).Text = "하나\n둘\n셋";
        Pause(window);

        Assert.Equal(["하나", "둘", "셋"], Said(card));
        Assert.Equal(ids, card.Script.ActiveLines.Select(line => line.Id));
        Assert.Equal([branch.Id], card.FindExtension(ids[2])!.DetourTargetNodeIds);

        window.Close();
    });

    [Fact]
    public void 줄을_지운_것은_칸을_나올_때_들어간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) =
            Stage("첫 줄", "둘째 줄", "셋째 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n셋째 줄";
        Pause(window);

        Assert.Equal(["첫 줄", "둘째 줄", "셋째 줄"], Said(card));

        Release(window);

        Assert.Equal(["첫 줄", "셋째 줄"], Said(card));
        Assert.Equal([ids[0], ids[2]], card.Script.ActiveLines.Select(line => line.Id));

        window.Close();
    });

    [Fact]
    public void 새_줄을_쓰면_판이_다시_그려져도_쓰던_글과_커서가_남는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) =
            Stage("첫 줄", "둘째 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n둘째 줄\n새로 쓰는 줄";
        box.CaretIndex = box.Text.Length;
        Pause(window);

        Assert.Equal(["첫 줄", "둘째 줄", "새로 쓰는 줄"], Said(card));
        Assert.Equal(ids, card.Script.ActiveLines.Take(2).Select(line => line.Id));

        TextBox now = Box(window, card.Id);
        Assert.True(now.IsFocused);
        Assert.Equal("첫 줄\n둘째 줄\n새로 쓰는 줄", now.Text);
        Assert.Equal(now.Text!.Length, now.CaretIndex);

        // 이어 쓴다 — 같은 줄을 고친 것이다.
        now.Text += "이다";
        Pause(window);

        Assert.Equal(["첫 줄", "둘째 줄", "새로 쓰는 줄이다"], Said(card));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static List<string> Said(DialogueNode card) =>
        card.Script.ActiveLines.Select(line => card.Script.Text(line.Id).Text).ToList();

    private static GraphEditorView Graph(MainWindow window) => window.FindControl<GraphEditorView>("Graph")!;

    private static TextBox Box(MainWindow window, string nodeId) =>
        Graph(window).FindControl<Canvas>("GraphCanvas")!
            .GetLogicalDescendants().OfType<TextBox>()
            .Single(box => (box.Tag as string) == "writing:" + nodeId);

    /// <summary>
    /// 치다가 멈춘다 — 글이 바뀌었다는 알림이 돌고(칸의 TextChanged는 미뤄서 온다) 멈춤 시계가 다 간 것처럼 넣는다.
    /// </summary>
    private static void Pause(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        Graph(window).CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>칸을 나온다 — 판의 빈 곳을 누른 것처럼(그때 초점이 가는 곳으로) 초점을 옮긴다.</summary>
    private static void Release(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        Graph(window).FindControl<Border>("WritingFocusSink")!.Focus();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>연출 그래프 탭에 대사 카드 하나 — 줄마다 글을 채우고 골라 둔다(곁기둥이 선다).</summary>
    private static (MainWindow Window, AuthoringSession Session, DialogueNode Card, string[] Ids) Stage(
        params string[] texts)
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        DialogueNode card = session.Editor.AddDialogueNode(session.ActiveFile!.Id, name: "본문");
        Assert.True(session.Editor.ApplyScenarioText(card.Id, string.Join('\n', texts), session.Definition).Applied);
        session.Editor.MoveNode(card.Id, 60, 60);
        session.Select(card.Id);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        return (window, session, card, card.Script.ActiveLines.Select(line => line.Id).ToArray());
    }
}
