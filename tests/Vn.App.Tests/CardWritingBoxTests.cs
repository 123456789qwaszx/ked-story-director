using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>대사 카드 안의 글 칸</b> (<c>docs/design/two-graphs.md</c> §7) — 노드 안의 글 칸에 직접 글을 쓴다.
///
/// 한 줄이 대사 한 줄(<c>화자: 대사</c>)이고, 넣는 때는 초점을 잃을 때와 Ctrl+S다. 칸 안의 손짓(누르기 · 휠)은
/// 카드의 끌기와 판의 확대가 가로채지 않는다. 편집기 쪽 규칙(빈 첫 줄 · 되돌리기 한 칸 · 엄격한 넣기)은
/// <c>CardWritingTests</c>가 본다.
/// </summary>
public sealed class CardWritingBoxTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-writing", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 쓰고_다른_칸으로_옮기면_대본에_들어가고_초점은_옮긴_칸에_남는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        DialogueNode second = Card(session, 40, 500);
        Dispatcher.UIThread.RunJobs();

        TextBox box = Box(window, first.Id);
        box.Focus();
        box.Text = "라루: 하나\n윌로: 둘";

        // 다른 칸으로 옮긴다 — 넣기가 판을 다시 그려도(줄이 늘었다) 초점은 옮긴 칸의 새 칸에 있다.
        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("라루", "하나"), ("윌로", "둘")], Said(session, first.Id));
        Assert.Same(Box(window, second.Id), TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement());

        // 넣기 한 번 = 되돌리기 한 칸 — 새 카드의 빈 줄로 돌아간다.
        session.Editor.Undo();
        Assert.Equal([("", "")], Said(session, first.Id));

        window.Close();
    });

    [Fact]
    public void 다른_카드를_누르면_안_넣은_글이_먼저_들어간다() => HeadlessUi.Run(() =>
    {
        // 카드 머리는 초점을 받지 않는다 — 칸이 초점을 쥔 채 다른 노드가 골라지면 쓴 글이 칸에만 남는다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        DialogueNode second = Card(session, 40, 500);
        Dispatcher.UIThread.RunJobs();

        TextBox box = Box(window, first.Id);
        box.Focus();
        box.Text = "라루: 고르기 전에 넣는다";

        Border header = window.FindControl<GraphEditorView>("Graph")!
            .FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(card => (card.Tag as string) == second.Id);
        Point grip = header.TranslatePoint(new Point(header.Bounds.Width / 2, 12), window)!.Value;
        window.MouseDown(grip, MouseButton.Left);
        window.MouseUp(grip, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(second.Id, session.SelectedNodeId);
        Assert.Equal([("라루", "고르기 전에 넣는다")], Said(session, first.Id));

        window.Close();
    });

    [Fact]
    public void 못_넣는_글은_칸에_남고_칸이_사유를_말한다() => HeadlessUi.Run(() =>
    {
        // 반쯤 넣으면 못 넣은 줄이 다시 그려진 칸에서 조용히 사라진다 — 그래서 아무것도 안 넣고 글을 둔다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        DialogueNode second = Card(session, 40, 500);
        Dispatcher.UIThread.RunJobs();

        TextBox box = Box(window, first.Id);
        box.Focus();
        box.Text = "라루: 하나\n[메모] 장식은 해석 못 한다";

        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("", "")], Said(session, first.Id));
        Assert.Equal("라루: 하나\n[메모] 장식은 해석 못 한다", Box(window, first.Id).Text);
        Assert.NotNull(ToolTip.GetTip(Box(window, first.Id)));
        Assert.Contains("넣지 못했습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 넣은_뒤_정돈된_글은_다시_넣을_글이_아니다() => HeadlessUi.Run(() =>
    {
        // 칸은 넣은 뒤 모델의 모습(정돈된 표기)으로 다시 선다 — 사람이 친 모양을 기준으로 들고 있으면 정돈된
        // 칸이 "안 넣은 글"로 보여, 다음 초점 이탈이 같은 글을 또 넣는다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        DialogueNode second = Card(session, 40, 500);
        Dispatcher.UIThread.RunJobs();

        TextBox box = Box(window, first.Id);
        box.Focus();
        box.Text = "라루:하나\n\n윌로:  둘";
        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("라루: 하나\n윌로: 둘", Box(window, first.Id).Text);
        long revision = session.Editor.Revision;

        Box(window, first.Id).Focus();
        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(revision, session.Editor.Revision);

        window.Close();
    });

    [Fact]
    public void 칸을_눌렀다_나오기만_하면_아무것도_안_바뀐다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        DialogueNode second = Card(session, 40, 500);
        Dispatcher.UIThread.RunJobs();
        long revision = session.Editor.Revision;

        Box(window, first.Id).Focus();
        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(revision, session.Editor.Revision);

        window.Close();
    });

    [Fact]
    public void 칸_안에서_누르고_끌어도_카드는_안_움직이고_그_노드가_골라진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        TextBox box = Box(window, first.Id);
        Point inside = box.TranslatePoint(new Point(30, 20), window)!.Value;

        window.MouseDown(inside, MouseButton.Left);
        window.MouseMove(inside + new Point(120, 90), RawInputModifiers.LeftMouseButton);
        window.MouseUp(inside + new Point(120, 90), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        StoryNode node = session.Project.FindNode(first.Id)!;
        Assert.Equal((40, 40), (node.Layout.X, node.Layout.Y));
        Assert.Equal(first.Id, session.SelectedNodeId);   // 칸에 들어가면 그 노드를 고른다

        window.Close();
    });

    [Fact]
    public void 고르지_않은_카드의_글_칸_위에서는_휠이_판을_확대한다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 글 칸이 휠을 먹는 것은 그 카드를 골랐을 때뿐이다. 고르지 않았으면 칸 위라도 평소처럼 확대다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        session.Select(null);
        Dispatcher.UIThread.RunJobs();
        string before = ZoomOf(graph);

        TextBox box = Box(window, first.Id);
        window.MouseWheel(box.TranslatePoint(new Point(30, 20), window)!.Value, new Vector(0, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(before, ZoomOf(graph));

        // 골랐다가 놓으면 다시 확대다.
        session.Select(first.Id);
        session.Select(null);
        Dispatcher.UIThread.RunJobs();
        string again = ZoomOf(graph);
        box = Box(window, first.Id);
        window.MouseWheel(box.TranslatePoint(new Point(30, 20), window)!.Value, new Vector(0, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(again, ZoomOf(graph));

        window.Close();
    });

    [Fact]
    public void 고른_카드의_글_칸이_넘치지_않으면_휠은_판을_확대도_스크롤도_하지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 보고 — 글이 칸에 다 들어가 칸에 스크롤이 없으면, 칸이 받지 않은 휠이 판의 ScrollViewer까지
        // 올라가 판 전체가 세로로 스크롤됐다. 고른 카드의 칸 위의 휠은 칸의 것이다 — 칸이 스크롤할 것이 없으면 아무 일도 없다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        session.Select(first.Id);
        Dispatcher.UIThread.RunJobs();
        string zoom = ZoomOf(graph);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);   // 판은 세로로 스크롤할 자리가 있다
        Vector before = scroll.Offset;

        TextBox box = Box(window, first.Id);
        Point inside = box.TranslatePoint(new Point(30, 20), window)!.Value;

        for (int turn = 0; turn < 3; turn++)
        {
            window.MouseWheel(inside, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(before, scroll.Offset);
        Assert.Equal(zoom, ZoomOf(graph));

        // 칸 밖(빈 판)의 휠은 카드를 골라 두었어도 확대다.
        Point empty = scroll.TranslatePoint(
            new Point(scroll.Bounds.Width * 0.6, scroll.Bounds.Height * 0.5), window)!.Value;
        window.MouseWheel(empty, new Vector(0, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(zoom, ZoomOf(graph));

        window.Close();
    });

    [Fact]
    public void 고른_카드의_넘치는_글은_칸이_스크롤하고_끝에_닿아도_판으로_넘기지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        session.Select(first.Id);
        Dispatcher.UIThread.RunJobs();
        string zoom = ZoomOf(graph);
        Vector before = scroll.Offset;

        TextBox box = Box(window, first.Id);
        box.Text = string.Join("\n", Enumerable.Range(1, 40).Select(index => $"라루: {index}번째 줄"));
        Dispatcher.UIThread.RunJobs();

        ScrollViewer inner = box.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(inner.Extent.Height > inner.Viewport.Height);
        Point inside = box.TranslatePoint(new Point(30, 20), window)!.Value;

        // 칸의 끝을 넘도록 넉넉히 굴린다 — 끝에 닿은 뒤의 휠도 판으로 올라가지 않는다.
        for (int turn = 0; turn < 60; turn++)
        {
            window.MouseWheel(inside, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(inner.Offset.Y > 0);
        Assert.Equal(before, scroll.Offset);
        Assert.Equal(zoom, ZoomOf(graph));

        window.Close();
    });

    [Fact]
    public void 고르지_않은_카드의_넘치는_글_위에서도_휠은_확대다() => HeadlessUi.Run(() =>
    {
        // 넘치는 칸이라도 고르지 않았으면 칸은 그대로고 판이 확대된다 — 스크롤하려면 그 카드를 고른다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;

        TextBox box = Box(window, first.Id);
        box.Text = string.Join("\n", Enumerable.Range(1, 40).Select(index => $"라루: {index}번째 줄"));
        Dispatcher.UIThread.RunJobs();
        session.Select(null);
        Dispatcher.UIThread.RunJobs();
        string zoom = ZoomOf(graph);

        ScrollViewer inner = box.GetVisualDescendants().OfType<ScrollViewer>().First();
        window.MouseWheel(box.TranslatePoint(new Point(30, 20), window)!.Value, new Vector(0, -1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, inner.Offset.Y);
        Assert.NotEqual(zoom, ZoomOf(graph));

        window.Close();
    });

    private static string ZoomOf(GraphEditorView graph) => graph.FindControl<TextBlock>("ZoomText")!.Text ?? string.Empty;

    [Fact]
    public void 곁기둥에서_고친_줄이_카드의_글_칸에도_비친다() => HeadlessUi.Run(() =>
    {
        // 판은 다시 안 그린다(글자 편집이 판을 부수면 안 된다) — 그래도 칸이 낡으면 다음 넣기가 방금 고친
        // 줄을 도로 덮는다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        session.Select(first.Id);
        Dispatcher.UIThread.RunJobs();   // 곁기둥이 선다 — 사람이 고르고 나서 고치는 순서 그대로
        DialogueNode card = session.Project.FindDialogue(first.Id)!;
        string lineId = card.Script.ActiveLines.First().Id;

        session.Editor.SetScriptLineText(card.Id, lineId, "라루", "곁기둥에서 고쳤다");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("라루: 곁기둥에서 고쳤다", Box(window, first.Id).Text);

        window.Close();
    });

    [Fact]
    public void Ctrl_S는_넣지_않은_글을_먼저_넣고_저장한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage(saved: true);

        TextBox box = Box(window, first.Id);
        box.Focus();
        box.Text = "라루: 저장 전에 넣는다";

        window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("라루", "저장 전에 넣는다")], Said(session, first.Id));
        Assert.False(session.IsDirty);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static List<(string Speaker, string Text)> Said(AuthoringSession session, string nodeId)
    {
        DialogueNode node = session.Project.FindDialogue(nodeId)!;
        Vn.Authoring.Script.ScriptDocument script = node.Script;

        return script.ActiveLines
            .Select(line => (script.Text(line.Id).Speaker, script.Text(line.Id).Text))
            .ToList();
    }

    /// <summary>그 노드 카드의 글 칸 — Tag에 <c>writing:{노드}</c>를 진다. 판을 다시 그리면 새 칸이다.</summary>
    private static TextBox Box(MainWindow window, string nodeId) =>
        window.FindControl<GraphEditorView>("Graph")!
            .FindControl<Canvas>("GraphCanvas")!
            .GetLogicalDescendants().OfType<TextBox>()
            .Single(box => (box.Tag as string) == "writing:" + nodeId);

    private static DialogueNode Card(AuthoringSession session, double x, double y)
    {
        DialogueNode node = session.Editor.AddDialogueNode(TestBoard.Of(session).Id, name: $"카드 {x},{y}");
        session.Editor.MoveNode(node.Id, x, y);
        return node;
    }

    /// <summary>연출 그래프 탭에 선 창 하나 + (40, 40)에 선 새 대사 카드 하나(빈 줄 하나를 들고 태어난다).</summary>
    private (MainWindow Window, AuthoringSession Session, DialogueNode First) Stage(bool saved = false)
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;

        if (saved)
        {
            Directory.CreateDirectory(_directory);
            string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
            ProjectStore.Save(manifest, new StoryProject { Title = "글 칸" });
            session.Open(manifest);
            TestBoard.Of(session);
        }

        DialogueNode first = Card(session, 40, 40);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        return (window, session, first);
    }
}
