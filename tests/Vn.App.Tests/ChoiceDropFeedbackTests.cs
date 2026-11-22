using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>선택지 점을 끄는 동안 놓으면 무엇이 되는지 미리 말한다</b> (2026-11-22 소유자 — 「다른 에피소드 노드에 끌었을 때 동작이 있다는 것을 알기
/// 어렵다 · 같은 챕터라는 조건도 있다 · 이어진 선택지를 빈 곳에 끌면 끊긴다는 것도 해 보기 전까지 모른다」 · usability UX36).
///
/// 포인터 아래를 판정해 꼬리표 · 끄는 선의 색 · 아래 띠 · 놓을 카드의 테두리가 같은 말을 한다. 미리 말한 것과 놓은 결과가 같아야 한다 —
/// 그래서 시험마다 끝에 실제로 놓아 본다. 장면 입구를 둘로 만드는 잇기는 받지 않는다(같은 날 소유자).
/// </summary>
public sealed class ChoiceDropFeedbackTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-choice-drop", Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

    public ChoiceDropFeedbackTests()
    {
        Directory.CreateDirectory(_directory);
        ProjectStore.Save(ManifestPath, new StoryProject { Title = "선택지 끌기 안내" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 안_이은_칸을_에피소드_카드_위로_끌면_잇는다고_말하고_빈_곳에서는_그대로라고_말한다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        Write(graph, "root", slot: 0, "왼쪽으로");

        Press(Knob(graph, "root", 0));

        MoveOver(graph, session, "a");
        Assert.Contains("잇기 → a", graph.DropBadgeText, StringComparison.Ordinal);
        Assert.Equal(NodeId(session, "a"), graph.DropFrameNodeId);
        Assert.Same(GraphEditorView.DropJoinBrush, graph.ConnectingLineBrush);
        Assert.Contains("「왼쪽으로」를 'a'에 잇습니다", Hint(graph), StringComparison.Ordinal);

        MoveEmpty(graph);
        Assert.Equal("놓아도 그대로", graph.DropBadgeText);
        Assert.Null(graph.DropFrameNodeId);
        Assert.Same(GraphEditorView.DropIdleBrush, graph.ConnectingLineBrush);
        Assert.Contains("아무것도 바뀌지 않습니다", Hint(graph), StringComparison.Ordinal);

        // 미리 말한 대로 — 카드 위에서 놓으면 잇고, 꼬리표 · 테두리 · 띠가 걷힌다.
        Release(graph, session, "a");
        ChapterEdge joined = Assert.Single(session.Editor.FindChapter("ch01")!.Edges);
        Assert.Equal("a", joined.ToEpisodeId);
        Assert.Equal("왼쪽으로", joined.OptionLabel);
        Assert.Null(graph.DropBadgeText);
        Assert.Null(graph.DropFrameNodeId);
        Assert.False(graph.FindControl<Border>("HintBar")!.IsVisible);
    });

    [Fact]
    public void 이어진_칸을_빈_곳으로_끌면_끊긴다고_미리_말한다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        graph.Rebuild();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Press(Knob(graph, "root", 0));

        MoveEmpty(graph);
        Assert.Equal("✂ 연결 끊기", graph.DropBadgeText);
        Assert.Same(GraphEditorView.DropCutBrush, graph.ConnectingLineBrush);
        Assert.Contains("가던 연결이 끊깁니다", Hint(graph), StringComparison.Ordinal);
        Assert.Contains("「간다」는 칸에 남아", Hint(graph), StringComparison.Ordinal);

        // 제 카드 위도 빈 곳과 같다(놓을 때의 처리가 그렇게 받는다).
        MoveOver(graph, session, "root");
        Assert.Equal("✂ 연결 끊기", graph.DropBadgeText);

        MoveOver(graph, session, "a");
        Assert.Equal("이미 이어짐", graph.DropBadgeText);

        MoveOver(graph, session, "b");
        Assert.Contains("옮겨 잇기 → b", graph.DropBadgeText, StringComparison.Ordinal);
        Assert.Contains("'a' 대신 'b'", Hint(graph), StringComparison.Ordinal);

        Release(graph, session, null);
        Assert.Empty(session.Editor.FindChapter("ch01")!.Edges);
        Assert.Null(graph.DropBadgeText);
    });

    [Fact]
    public void 다른_챕터의_카드_위에서는_못_잇는다고_미리_말한다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show(secondChapter: true);
        Write(graph, "root", slot: 1, "저쪽으로");

        Press(Knob(graph, "root", 1));
        MoveOver(graph, session, "far");

        Assert.Equal("✕ 다른 챕터", graph.DropBadgeText);
        Assert.Equal(NodeId(session, "far"), graph.DropFrameNodeId);
        Assert.Same(GraphEditorView.DropCutBrush, graph.ConnectingLineBrush);
        Assert.Contains("같은 챕터 안에서만", Hint(graph), StringComparison.Ordinal);

        Release(graph, session, "far");
        Assert.Empty(session.Editor.FindChapter("ch01")!.Edges);
    });

    [Fact]
    public void 같은_문구로_이미_가는_카드_위에서는_못_잇는다고_미리_말한다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "왼쪽으로");
        graph.Rebuild();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Write(graph, "root", slot: 1, "왼쪽으로");

        Press(Knob(graph, "root", 1));
        MoveOver(graph, session, "a");

        Assert.Equal("✕ 같은 선택지가 있음", graph.DropBadgeText);

        Release(graph, session, "a");
        Assert.Single(session.Editor.FindChapter("ch01")!.Edges);
    });

    [Fact]
    public void 다른_장면의_시작점이_아닌_카드에는_못_잇는다고_미리_말하고_놓아도_잇지_않는다() => HeadlessUi.Run(() =>
    {
        // 장면 a에 a · c — root에서 a로 오는 길이 있어 a가 그 장면의 시작점이다. root에서 c로 또 이으면 장면 a에 밖에서 들어오는 자리가 둘이다
        // (검증 오류 — 2026-11-22 소유자: 「검증 오류가 나올 건데 이을 수 있다는 게 이상하다」).
        (GraphEditorView graph, AuthoringSession session) = Show();
        AddEpisode(session, "c", sceneId: "a", x: 960);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        graph.Rebuild();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Write(graph, "root", slot: 1, "저쪽으로");

        Press(Knob(graph, "root", 1));

        MoveOver(graph, session, "a");   // 그 장면의 시작점에 더 잇는 것은 괜찮다
        Assert.Contains("잇기 → a", graph.DropBadgeText, StringComparison.Ordinal);

        MoveOver(graph, session, "c");
        Assert.Equal("✕ 장면 입구가 둘이 됨", graph.DropBadgeText);
        Assert.Equal(NodeId(session, "c"), graph.DropFrameNodeId);
        Assert.Same(GraphEditorView.DropCutBrush, graph.ConnectingLineBrush);
        Assert.Contains("이미 'a'에서 시작합니다", Hint(graph), StringComparison.Ordinal);

        Release(graph, session, "c");
        Assert.DoesNotContain(session.Editor.FindChapter("ch01")!.Edges, edge => edge.ToEpisodeId == "c");
        Assert.Contains("시작점이 아닙니다", session.StatusMessage, StringComparison.Ordinal);
    });

    [Fact]
    public void 장면의_하나뿐인_입구_길은_같은_장면의_다른_카드로_옮겨_이을_수_있다() => HeadlessUi.Run(() =>
    {
        // root → a가 장면 a의 하나뿐인 입구다. 그 길을 c로 옮기면 시작점이 c로 옮겨 갈 뿐 입구는 여전히 하나다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        AddEpisode(session, "c", sceneId: "a", x: 960);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        graph.Rebuild();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Press(Knob(graph, "root", 0));
        MoveOver(graph, session, "c");
        Assert.Contains("옮겨 잇기 → c", graph.DropBadgeText, StringComparison.Ordinal);

        Release(graph, session, "c");
        Assert.Equal("c", Assert.Single(session.Editor.FindChapter("ch01")!.Edges).ToEpisodeId);
    });

    [Fact]
    public void 점에_포인터를_올리면_끌어서_할_수_있는_일을_말하고_내리면_걷는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        graph.Rebuild();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Ellipse joined = Knob(graph, "root", 0);
        Hover(joined, InputElement.PointerEnteredEvent);
        Assert.Contains("빈 곳에 놓으면 끊깁니다", graph.DropBadgeText, StringComparison.Ordinal);
        Assert.NotNull(joined.RenderTransform);

        Hover(joined, InputElement.PointerExitedEvent);
        Assert.Null(graph.DropBadgeText);
        Assert.Null(joined.RenderTransform);

        Write(graph, "root", slot: 1, "저쪽으로");
        Ellipse armed = Knob(graph, "root", 1);
        Hover(armed, InputElement.PointerEnteredEvent);
        Assert.Contains("같은 챕터의 에피소드 카드에 놓으면 잇습니다", graph.DropBadgeText, StringComparison.Ordinal);
        Hover(armed, InputElement.PointerExitedEvent);
        Assert.Null(graph.DropBadgeText);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>ch01 — root · a · b(장면은 저마다). 자리는 겹치지 않게 벌려 둔다. <paramref name="secondChapter"/>면 ch02의 far도.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show(bool secondChapter = false)
    {
        var session = new AuthoringSession();
        session.Open(ManifestPath);

        session.Editor.EnsureChapter("ch01");
        string fileId = session.EnsureChapterBoard("ch01");

        AddEpisode(session, "root", sceneId: "root", x: 0);
        AddEpisode(session, "a", sceneId: "a", x: 320);
        AddEpisode(session, "b", sceneId: "b", x: 640);

        if (secondChapter)
        {
            session.Editor.EnsureChapter("ch02");
            string second = session.EnsureChapterBoard("ch02");
            AddEpisode(session, "far", sceneId: "far", x: 1200, chapterId: "ch02");
            session.SetFileExpanded(second, expanded: true);
        }

        // 판을 펴야 카드가 선다 — 접힌 판은 표 프록시 하나로 온다.
        session.SetFileExpanded(fileId, expanded: true);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (graph, session);
    }

    /// <summary>에피소드 하나 — 카드는 `AddEpisode`가 함께 세운다(따로 세우면 같은 이름의 카드가 둘이다). 자리는 이 시험이 정한다.</summary>
    private static void AddEpisode(AuthoringSession session, string episodeId, string sceneId, double x, string chapterId = "ch01")
    {
        session.Editor.AddEpisode(chapterId, episodeId, title: episodeId, 0, 0, sceneId: sceneId);

        DialogueNode card = session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => EpisodeNaming.EpisodeIdOf(node) == episodeId);

        session.Editor.MoveNode(card.Id, x, 0);
    }

    private static string Hint(GraphEditorView graph) =>
        graph.FindControl<TextBlock>("HintText")!.Text ?? string.Empty;

    private static string NodeId(AuthoringSession session, string nodeName) =>
        session.Project.EnumerateNodes().First(node => node.Name == nodeName).Id;

    private static Border? Card(GraphEditorView graph, AuthoringSession session, string nodeName) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .FirstOrDefault(card => card.Tag as string == NodeId(session, nodeName));

    /// <summary>그 에피소드의 선택지 칸 점 — 줄은 Tag에 <c>{에피소드}#{칸}</c>을 진다.</summary>
    private static Ellipse Knob(GraphEditorView graph, string episodeId, int slot) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Single(row => row.Tag as string == $"{episodeId}#{slot}")
            .Children.OfType<Ellipse>().Single();

    /// <summary>판 좌표 — 그 카드의 왼위 안쪽.</summary>
    private static Point On(GraphEditorView graph, AuthoringSession session, string nodeName) =>
        Card(graph, session, nodeName) is { } card
            ? new Point(Canvas.GetLeft(card) + 20, Canvas.GetTop(card) + 10)
            : throw new InvalidOperationException($"카드 '{nodeName}'가 판에 없다.");

    /// <summary>빈 곳은 카드가 하나도 없는 먼 자리다.</summary>
    private static readonly Point Empty = new(5000, 5000);

    private static void MoveOver(GraphEditorView graph, AuthoringSession session, string nodeName) =>
        Move(graph, On(graph, session, nodeName));

    private static void MoveEmpty(GraphEditorView graph) => Move(graph, Empty);

    /// <summary>
    /// 판 좌표의 자리로 포인터를 옮긴다. ⚠ 포인터 이벤트의 자리는 <b>최상위 기준</b>이다 — 판 좌표를 그대로 주면 엉뚱한 데로 간다.
    /// </summary>
    private static void Move(GraphEditorView graph, Point onBoard)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;

        canvas.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, canvas, new Pointer(0, PointerType.Mouse, true), root,
            canvas.TranslatePoint(onBoard, root)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
            KeyModifiers.None));

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>그 카드(null이면 빈 곳)에서 놓는다.</summary>
    private static void Release(GraphEditorView graph, AuthoringSession session, string? onto)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        Point on = onto is null ? Empty : On(graph, session, onto);

        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, canvas.TranslatePoint(on, root)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Hover(Control control, RoutedEvent<PointerEventArgs> routedEvent)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;

        control.RaiseEvent(new PointerEventArgs(
            routedEvent, control, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None));

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>그 칸에 문구를 적어 넣는다 — 문구 칸을 누르고 적고 Enter.</summary>
    private static void Write(GraphEditorView graph, string episodeId, int slot, string text)
    {
        StackPanel row = graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Single(candidate => candidate.Tag as string == $"{episodeId}#{slot}");
        Control face = row.Children.First(child => child is TextBlock || child.Tag as string == GraphEditorView.SlotPlaceholderTag);

        Press(face);
        graph.SlotLabelBox!.Text = text;
        graph.SlotLabelBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Control control)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;

        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }
}
