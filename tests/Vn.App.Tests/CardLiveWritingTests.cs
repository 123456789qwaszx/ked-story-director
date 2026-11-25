using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>카드 글 칸은 쓰는 대로 들어간다</b> — 오른쪽 편집기만이 아니라 에피소드 노드 속 글 칸에서 고친 것도 바로 반영된다.
///
/// 치다가 잠깐 멈추면 칸을 나오지 않아도 넣는다(시험은 <see cref="GraphEditorView.CommitLiveWriting"/>로 그 멈춤을 대신한다).
/// 고친 줄은 처음 보인 줄의 신원을 이어서, 줄을 다 고쳐 써도 "확신할 수 없다"며 거절하지 않는다. 줄을 지우는 것만은
/// 칸을 나올 때 넣는다 — 다 지우고 새로 쓰는 도중의 모습에서 줄을 은퇴시키면 그 줄의 분기 · 연출이 떨어져 나간다.
/// </summary>
public sealed class CardLiveWritingTests
{
    private const int GraphTab = MainTab.Script;

    [Fact]
    public void 줄을_모두_고쳐_쓰고_멈추면_칸을_나오지_않아도_들어가고_곁기둥에_비친다() => HeadlessUi.Run(() =>
    {
        // 네 줄을 다 지우고 새 글로 채웠다.
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
        // "사라진 것인지 고쳐진 것인지 확신할 수 없습니다"로 통째로 거절하면 안 된다.
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

        // 다 지웠다 — 빈 칸은 넣지 않는다(칸도 붉히지 않는다).
        box.Text = string.Empty;
        Pause(window);
        Assert.Equal(["첫 줄", "둘째 줄", "셋째 줄"], Said(card));
        Assert.Null(ToolTip.GetTip(Box(window, card.Id)));

        // 첫 줄만 새로 썼다 — 아직 쓰는 중이다. 쓴 줄은 들어가고(2026-11-26 소유자 — 인스펙터에 바로 비친다), 지운 줄은 은퇴시키지 않고 지울 줄로
        // 남는다 — 신원도 분기도 그대로다.
        box.Text = "하나";
        Pause(window);

        Assert.Equal(["하나", "둘째 줄", "셋째 줄"], Said(card));
        Assert.Equal(ids, card.Script.ActiveLines.Select(line => line.Id));
        Assert.Equal([ids[1], ids[2]], session.PendingLineRemovalsOf(card.Id).Order());
        Assert.Equal([branch.Id], card.FindExtension(ids[2])!.DetourTargetNodeIds);
        Assert.Null(ToolTip.GetTip(Box(window, card.Id)));

        // 세 줄을 다 채웠다 — 지운 자리에 다시 쓴 줄은 그 줄로 이어지고, 셋째 줄의 분기도 그 줄에 남는다.
        Box(window, card.Id).Text = "하나\n둘\n셋";
        Pause(window);

        Assert.Equal(["하나", "둘", "셋"], Said(card));
        Assert.Equal(ids, card.Script.ActiveLines.Select(line => line.Id));
        Assert.Equal([branch.Id], card.FindExtension(ids[2])!.DetourTargetNodeIds);
        Assert.Empty(session.PendingLineRemovalsOf(card.Id));

        window.Close();
    });

    [Fact]
    public void 조건이_걸린_줄을_지우면_전처럼_칸을_나올_때_한꺼번에_넣는다() => HeadlessUi.Run(() =>
    {
        // 조건은 글의 조건 줄에서 다시 읽는다 — 지운 줄을 대본에 되돌려 두면 그 둘레에 조건 줄이 없어 조건이 바뀐다. 그래서 그런 줄을 지운 글은
        // 쓰는 동안 넣지 않는다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) =
            Stage("첫 줄", "둘째 줄", "셋째 줄");
        session.Project.FindDialogue(card.Id)!.RequireExtension(ids[1]).Transitions.Add(LineConditionTransition.EndIf());

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄 고침\n셋째 줄";
        Pause(window);

        Assert.Equal(["첫 줄", "둘째 줄", "셋째 줄"], Said(card));
        Assert.Empty(session.PendingLineRemovalsOf(card.Id));

        window.Close();
    });

    [Fact]
    public void 지울_줄이_있을_때_Esc는_지운_줄까지_칸에_되돌리고_표시를_걷는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) =
            Stage("첫 줄", "둘째 줄", "셋째 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n셋째 줄";
        Pause(window);
        Assert.Equal([ids[1]], session.PendingLineRemovalsOf(card.Id));

        Box(window, card.Id).RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Escape,
            Source = Box(window, card.Id)
        });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("첫 줄\n둘째 줄\n셋째 줄\n", Box(window, card.Id).Text);
        Assert.Empty(session.PendingLineRemovalsOf(card.Id));
        Assert.Empty(PendingRemovalRows(window, card.Id));

        Release(window);
        Assert.Equal(["첫 줄", "둘째 줄", "셋째 줄"], Said(card));

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

        // 멈춰도 지운 줄은 대본에 남는다 — 인스펙터가 그 줄을 「칸을 나오면 지워짐」으로 취소선을 그어 보인다(2026-11-26 소유자).
        Assert.Equal(["첫 줄", "둘째 줄", "셋째 줄"], Said(card));
        Assert.Equal([ids[1]], session.PendingLineRemovalsOf(card.Id));
        Assert.Equal([ids[1]], PendingRemovalRows(window, card.Id));

        // 지운 뒤에 고친 글 · 새 줄은 멈추면 바로 들어간다 — ⛔ 전에는 지운 줄이 있으면 이것까지 칸을 나올 때까지 미뤘다.
        box.Text = "첫 줄 고침\n셋째 줄\n넷째 줄";
        Pause(window);

        Assert.Equal(["첫 줄 고침", "둘째 줄", "셋째 줄", "넷째 줄"], Said(card));
        Assert.Equal([ids[0], ids[1], ids[2]], card.Script.ActiveLines.Take(3).Select(line => line.Id));
        Assert.Equal([ids[1]], PendingRemovalRows(window, card.Id));
        TextBox now = Box(window, card.Id);
        Assert.True(now.IsFocused);
        Assert.Equal("첫 줄 고침\n셋째 줄\n넷째 줄", now.Text);   // 칸은 쓴 그대로다 — 지운 줄이 되살아나지 않는다

        Release(window);

        Assert.Equal(["첫 줄 고침", "셋째 줄", "넷째 줄"], Said(card));
        Assert.Equal([ids[0], ids[2]], card.Script.ActiveLines.Take(2).Select(line => line.Id));
        Assert.Empty(session.PendingLineRemovalsOf(card.Id));
        Assert.Empty(PendingRemovalRows(window, card.Id));

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

    [Fact]
    public void 글자를_조합하는_중에는_멈춰도_넣지_않아_새_줄에서_조합하던_글자가_사라지지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 새 줄의 첫 단어를 쓸 때 두 번째 글자가 화면에서 사라졌다(「이런」을 쳤는데 「이」만, Enter를 치면 그제야 들어왔다).
        // 「런」을 치기 시작하면 「이」가 칸에 들어오고 멈춤 시계가 도는데, 「런」을 조합하는 도중에 시계가 다 가 넣었다 — 새 줄이라 카드를 새로 지어
        // 칸이 바뀌고, 조합하던 글자가 옛 칸과 함께 사라졌다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("첫 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n이";
        box.CaretIndex = box.Text.Length;
        Avalonia.Controls.Presenters.TextPresenter presenter = box.GetVisualDescendants()
            .OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
        presenter.SetCurrentValue(Avalonia.Controls.Presenters.TextPresenter.PreeditTextProperty, "런");   // 입력기가 「런」을 조합하는 중
        Assert.True(GraphEditorView.IsComposing(box));
        Pause(window);

        Assert.Equal(["첫 줄"], Said(card));            // 조합하는 동안은 넣지 않는다
        Assert.Same(box, Box(window, card.Id));         // 칸이 그대로다 — 조합하던 글자가 그 칸에 남는다
        Assert.Equal("런", presenter.PreeditText);

        // 조합을 마쳐 「이런」이 칸에 들어오면 멈출 때 넣는다(새 줄이라 카드를 새로 짓고 초점 · 커서는 새 칸으로 간다).
        box.Text = "첫 줄\n이런";
        box.CaretIndex = box.Text.Length;
        Assert.False(GraphEditorView.IsComposing(box));
        Pause(window);

        Assert.Equal(["첫 줄", "이런"], Said(card));
        Assert.Equal(ids[0], card.Script.ActiveLines.First().Id);
        TextBox now = Box(window, card.Id);
        Assert.True(now.IsFocused);
        Assert.Equal("첫 줄\n이런", now.Text);
        Assert.Equal(now.Text!.Length, now.CaretIndex);

        window.Close();
    });

    [Fact]
    public void 새_줄을_넣어_판이_다시_그려져도_판의_스크롤_자리는_그대로다() => HeadlessUi.Run(() =>
    {
        // 판 가운데쯤의 카드를 보며 쓴다 — 판이 왼쪽 위 구석에 있지 않아야 튐이 보인다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] _) =
            Stage("첫 줄", "둘째 줄");
        session.Editor.MoveNode(card.Id, 1500, 1200);
        Dispatcher.UIThread.RunJobs();

        ScrollViewer scroll = Graph(window).FindControl<ScrollViewer>("GraphScroll")!;
        var seen = new Vector(1300, 1000);
        scroll.Offset = seen;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(seen, scroll.Offset);

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n둘째 줄\n셋째 줄";
        box.CaretIndex = box.Text.Length;
        Pause(window);

        // 줄이 늘어 판을 다시 그렸고, 새 칸이 초점을 받았다 — 그래도 판은 보던 자리 그대로다.
        TextBox now = Box(window, card.Id);
        Assert.NotSame(box, now);
        Assert.True(now.IsFocused);
        Assert.Equal(seen, scroll.Offset);

        window.Close();
    });

    [Fact]
    public void 새_줄을_쓴_뒤_글_칸에서_Ctrl_Z는_그_줄을_되돌리고_Ctrl_Y는_다시_넣는다() => HeadlessUi.Run(() =>
    {
        // 줄이 늘면 판을 다시 그려 칸이 새로 선다 — 칸 자체의 되돌리기 기록이 비어, 전에는 Ctrl+Z가 아무것도 안 했다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] _) =
            Stage("첫 줄", "둘째 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n둘째 줄\n새로 쓰는 줄";
        box.CaretIndex = box.Text.Length;
        Pause(window);
        Assert.NotSame(box, Box(window, card.Id));
        Assert.Equal(["첫 줄", "둘째 줄", "새로 쓰는 줄"], Said(card));

        Press(window, Avalonia.Input.Key.Z);

        Assert.Equal(["첫 줄", "둘째 줄"], Said(Now(session, card)));
        TextBox now = Box(window, card.Id);
        Assert.True(now.IsFocused);
        Assert.Equal("첫 줄\n둘째 줄\n", now.Text!.Replace("\r\n", "\n", StringComparison.Ordinal));   // 끝의 빈 줄 — 이어 쓰기 자리(2026-11-25)

        Press(window, Avalonia.Input.Key.Y);

        Assert.Equal(["첫 줄", "둘째 줄", "새로 쓰는 줄"], Said(Now(session, card)));
        Assert.Equal("첫 줄\n둘째 줄\n새로 쓰는 줄\n", Box(window, card.Id).Text!.Replace("\r\n", "\n", StringComparison.Ordinal));

        window.Close();
    });

    [Fact]
    public void 줄_안을_고친_뒤_글_칸에서_Ctrl_Z는_칸과_모델을_함께_되돌린다() => HeadlessUi.Run(() =>
    {
        // 줄 수가 그대로면 판을 다시 그리지 않아 칸이 그대로다 — 되돌린 글이 그 칸에도 보여야 한다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] _) =
            Stage("첫 줄", "둘째 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n둘째 줄이다";
        box.CaretIndex = box.Text.Length;
        Pause(window);
        Assert.Equal(["첫 줄", "둘째 줄이다"], Said(card));

        Press(window, Avalonia.Input.Key.Z);

        Assert.Equal(["첫 줄", "둘째 줄"], Said(Now(session, card)));
        TextBox now = Box(window, card.Id);
        Assert.True(now.IsFocused);
        Assert.Equal("첫 줄\n둘째 줄\n", now.Text!.Replace("\r\n", "\n", StringComparison.Ordinal));   // 끝의 빈 줄 — 이어 쓰기 자리(2026-11-25)
        Assert.True(now.CaretIndex <= now.Text!.Length);

        window.Close();
    });

    [Fact]
    public void 아직_넣지_않은_글은_Ctrl_Z가_그것부터_걷는다() => HeadlessUi.Run(() =>
    {
        // 멈추기 전(넣기 전)에 누른 Ctrl+Z는 쓰던 글을 마지막으로 넣은 글로 돌린다 — 모델은 그대로다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] _) =
            Stage("첫 줄", "둘째 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n둘째 줄 쓰는 중";
        Dispatcher.UIThread.RunJobs();

        Press(window, Avalonia.Input.Key.Z);

        Assert.Equal(["첫 줄", "둘째 줄"], Said(Now(session, card)));
        Assert.Equal("첫 줄\n둘째 줄\n", Box(window, card.Id).Text!.Replace("\r\n", "\n", StringComparison.Ordinal));   // 끝의 빈 줄(2026-11-25)

        window.Close();
    });

    [Fact]
    public void 줄_사이에_빈_줄을_띄우고_멈춰도_붙지_않고_쓰던_칸이_그대로다() => HeadlessUi.Run(() =>
    {
        // 2026-11-25 소유자 — 줄과 줄 사이에 빈 공간을 두고 쓰고 싶다(⛔ 그 전에는 넣으면 붙었다). 줄 사이만 바뀌었다 — 판을 다시 그리지 않는다.
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("첫 줄", "둘째 줄");

        TextBox box = Box(window, card.Id);
        box.Focus();
        box.Text = "첫 줄\n\n둘째 줄";
        Pause(window);

        Assert.Equal("첫 줄\n\n둘째 줄", Vn.Authoring.Rendering.WorkingDialoguePreview.ScenarioText(session.Project, card.Id));
        Assert.Equal(ids, Now(session, card).Script.ActiveLines.Select(line => line.Id));
        Assert.Same(box, Box(window, card.Id));
        Assert.True(box.IsFocused);

        Release(window);

        Assert.Equal("첫 줄\n\n둘째 줄\n", Box(window, card.Id).Text);   // 칸을 나오면 끝에 이어 쓰기 자리 빈 줄이 선다

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>되돌리기는 프로젝트를 스냅샷에서 새로 세운다 — 카드는 지금 프로젝트에서 다시 찾는다.</summary>
    private static DialogueNode Now(AuthoringSession session, DialogueNode card) => session.Project.FindDialogue(card.Id)!;

    /// <summary>초점이 있는 곳에서 Ctrl+그 글쇠를 누른다 — 창의 터널 처리기부터 지난다.</summary>
    private static void Press(MainWindow window, Avalonia.Input.Key key)
    {
        Control target = (Control)window.FocusManager!.GetFocusedElement()!;
        target.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = Avalonia.Input.KeyModifiers.Control,
            Source = target
        });
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>인스펙터(대사 노드 편집기)에 「칸을 나오면 지워짐」으로 선 줄의 LineId.</summary>
    private static List<string> PendingRemovalRows(MainWindow window, string nodeId)
    {
        DialogueNodeEditor editor = window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct()
            .Single(item => item.IsVisible && item.NodeId == nodeId);
        DialogueNode node = window.SessionProbe.Project.FindDialogue(nodeId)!;
        List<string> active = node.Script.ActiveLines.Select(line => line.Id).ToList();

        // 줄 카드는 줄 차례로 선다 — 지울 줄 카드의 차례로 LineId를 읽는다.
        return editor.GetLogicalDescendants().OfType<Border>()
            .Where(border => border.Tag as string is DialogueNodeEditor.PendingRemovalTag)
            .Select(border => border.GetLogicalDescendants().OfType<TextBlock>().First().Text!)
            .Select(index => active[int.Parse(index, System.Globalization.CultureInfo.InvariantCulture)])
            .ToList();
    }

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
        DialogueNode card = session.Editor.AddDialogueNode(TestChapter.Board(session).Id, "본문", name: "본문");
        Assert.True(session.Editor.ApplyScenarioText(card.Id, string.Join('\n', texts)).Applied);
        session.Editor.MoveNode(card.Id, 60, 60);
        session.Select(card.Id);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        return (window, session, card, card.Script.ActiveLines.Select(line => line.Id).ToArray());
    }
}
