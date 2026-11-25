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
/// <b>대사 카드 안의 글 칸</b> (<c>docs/design/two-graphs.md</c> §2.4) — 노드 안의 글 칸에 직접 글을 쓴다.
///
/// 한 줄이 대사 한 줄(<c>화자: 대사</c>)이고, 넣는 때는 초점을 잃을 때와 Ctrl+S다. 칸 안의 손짓(누르기 · 휠)은
/// 카드의 끌기와 판의 확대가 가로채지 않는다. 편집기 쪽 규칙(빈 첫 줄 · 되돌리기 한 칸 · 엄격한 넣기)은
/// <c>CardWritingTests</c>가 본다.
/// </summary>
public sealed class CardWritingBoxTests : IDisposable
{
    private const int GraphTab = MainTab.Script;

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
        // 줄 사이에 띄운 빈 줄은 남는다(2026-11-25 소유자 — ⛔ 그 전에는 걷혀 붙었다). 첫 줄 앞 · 끝의 빈 줄과 화자 띄어쓰기는 정돈된다.
        box.Text = "\n라루:하나\n\n윌로:  둘\n";
        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("라루: 하나\n\n윌로: 둘\n", Box(window, first.Id).Text);   // 끝에는 이어 쓰기 자리 빈 줄 하나(2026-11-25)
        long revision = session.Editor.Revision;

        Box(window, first.Id).Focus();
        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(revision, session.Editor.Revision);

        window.Close();
    });

    [Fact]
    public void 글_칸_끝에는_늘_이어_쓸_빈_줄이_있고_아무것도_안_쓴_카드는_빈_칸이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-25 소유자 — 「가장 아래쪽에 빈 줄이 생기도록 해줄래? … 가장 아랫줄의 우측 끝에서 엔터를 눌러서 새 줄을 생성 한 뒤에 이어써야 하는 데
        // 너무 불편」. 끝의 빈 줄은 보이기만 한다 — 넣을 때 걷혀 모델 · 줄 수는 그대로다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        DialogueNode second = Card(session, 40, 500);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(string.Empty, Box(window, second.Id).Text ?? string.Empty);   // 아무것도 안 쓴 카드 — 첫 줄이 곧 쓸 줄이다

        session.Editor.ApplyScenarioText(first.Id, "라루: 하나");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("라루: 하나\n", Box(window, first.Id).Text);

        // 그 빈 줄에 이어 쓰고 나온다 — 새 줄이 들어가고, 끝에는 다시 빈 줄이 선다.
        TextBox box = Box(window, first.Id);
        box.Focus();
        box.Text = "라루: 하나\n윌로: 둘";
        Box(window, second.Id).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, session.Project.FindDialogue(first.Id)!.Script.ActiveLines.Count());
        Assert.Equal("라루: 하나\n윌로: 둘\n", Box(window, first.Id).Text);

        // 눌렀다 나오기만 하면 아무것도 적지 않는다 — 끝의 빈 줄은 넣을 글이 아니다.
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
    public void 쓰는_중인_칸_위에서도_휠은_판을_확대한다() => HeadlessUi.Run(() =>
    {
        // 2026-11-25 소유자 — 「글을 쓰다가 확대를 하려고 … 글 내부 스크롤에 걸려서 확대 축소가 안 되니 불편 … 글을 쓰는 와중이더라도 … 확대 축소가
        // 되도록」. 카드 글 칸은 휠을 먹지 않는다 — 고른 카드 · 쓰는 중인(초점을 쥔) 칸 위에서도 휠은 판의 배율이다. 판 전체가 세로로 스크롤되지도 않는다.
        // ⛔ 2026-11-17 ~ 11-25에는 고른 카드의 칸 위의 휠이 칸의 것이었다(스크롤할 것이 없으면 아무 일도 없었다).
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        session.Select(first.Id);
        Dispatcher.UIThread.RunJobs();
        TextBox box = Box(window, first.Id);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(box.IsFocused);
        string zoom = ZoomOf(graph);

        window.MouseWheel(box.TranslatePoint(new Point(30, 20), window)!.Value, new Vector(0, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(zoom, ZoomOf(graph));
        Assert.True(Box(window, first.Id).IsFocused);   // 쓰던 칸은 그대로 쓰는 중이다

        window.Close();
    });

    [Fact]
    public void 넘치는_글_칸_위에서도_휠은_확대이고_칸은_스크롤하지_않는다() => HeadlessUi.Run(() =>
    {
        // 고르고 쓰는 중인 칸이 넘쳐도 휠은 칸을 스크롤하지 않는다 — 판이 확대된다(2026-11-25 소유자 — 「스크롤 기능은 실제론 거의 사용하지 않는 경우가
        // 대다수」). 넘치는 글은 칸의 스크롤 막대를 끌거나 세로 글 맞춤으로 본다.
        (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        session.Select(first.Id);
        Dispatcher.UIThread.RunJobs();

        TextBox box = Box(window, first.Id);
        box.Focus();
        box.Text = string.Join("\n", Enumerable.Range(1, 40).Select(index => $"라루: {index}번째 줄"));
        box.CaretIndex = 0;
        Dispatcher.UIThread.RunJobs();
        string zoom = ZoomOf(graph);

        ScrollViewer inner = box.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(inner.Extent.Height > inner.Viewport.Height);
        double offset = inner.Offset.Y;
        window.MouseWheel(box.TranslatePoint(new Point(30, 20), window)!.Value, new Vector(0, -1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(offset, inner.Offset.Y);
        Assert.NotEqual(zoom, ZoomOf(graph));

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

    [Fact]
    public void 휠로_판_옮기기를_켜면_글_칸_위에서도_판이_움직인다() => HeadlessUi.Run(() =>
    {
        // 설정 「휠로 판 옮기기」(writer-app WA3)를 켜면 휠은 배율이 아니라 판 옮기기다 — 글 칸 위에서도 칸이 휠을 먹지 않고 판이 움직인다(판이 터널에서
        // 옮긴다 — 스크롤에 맡기면 넘치는 칸이 먼저 먹는다). 고른 카드도 같다(2026-11-25).
        BoardWheel.Set(true);

        try
        {
            (MainWindow window, AuthoringSession session, DialogueNode first) = Stage();
            GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
            ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;

            TextBox box = Box(window, first.Id);
            box.Text = string.Join("\n", Enumerable.Range(1, 40).Select(index => $"라루: {index}번째 줄"));
            Dispatcher.UIThread.RunJobs();
            session.Select(null);
            Dispatcher.UIThread.RunJobs();
            ScrollViewer inner = box.GetVisualDescendants().OfType<ScrollViewer>().First();
            string zoom = ZoomOf(graph);
            Vector before = scroll.Offset;

            // 고르지 않았다 — 칸은 그대로고 판이 내려간다.
            window.MouseWheel(box.TranslatePoint(new Point(30, 20), window)!.Value, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, inner.Offset.Y);
            Assert.Equal(before.Y + BoardWheel.PanStep, scroll.Offset.Y, 1);
            Assert.Equal(zoom, ZoomOf(graph));

            // 골라도 같다 — 칸은 그대로고 판이 내려간다(2026-11-25 · ⛔ 그 전에는 고른 카드의 칸이 스크롤했다).
            session.Select(first.Id);
            Dispatcher.UIThread.RunJobs();
            box = Box(window, first.Id);
            inner = box.GetVisualDescendants().OfType<ScrollViewer>().First();
            before = scroll.Offset;
            window.MouseWheel(box.TranslatePoint(new Point(30, 20), window)!.Value, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, inner.Offset.Y);
            Assert.Equal(before.Y + BoardWheel.PanStep, scroll.Offset.Y, 1);

            window.Close();
        }
        finally
        {
            BoardWheel.Set(null);   // 스위치는 이 시험 프로세스가 함께 쓴다
        }
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

        Assert.Equal("라루: 곁기둥에서 고쳤다\n", Box(window, first.Id).Text);   // 끝의 빈 줄 — 이어 쓰기 자리(2026-11-25)

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
        DialogueNode node = session.Editor.AddDialogueNode(TestChapter.Board(session).Id, $"카드 {x},{y}", name: $"카드 {x},{y}");
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
            TestChapter.Board(session);
        }

        DialogueNode first = Card(session, 40, 40);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        return (window, session, first);
    }
}
