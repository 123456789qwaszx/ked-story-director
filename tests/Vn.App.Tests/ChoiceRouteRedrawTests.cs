using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>판에서 선택지 점을 끌어 잇고 · 옮기고 · 끊으면 그 출발의 선과 도착 포트만 다시 긋는다</b> (2026-11-19 소유자 — 「선택지 선을 연결하는데
/// 멈추는 게 느껴진다」 · usability UX19). ⛔ 판을 두 번 통째로 다시 그렸다(변경 알림이 한 번, 잇기가 끝나며 또 한 번). 다시 그은 판은 통째로 그은
/// 판과 같아야 한다 — 길이 생기면 도착 포트가 새로 서고, 마지막 길이 걷히면 포트가 내려가며, 장면 시작점이 바뀐 카드는 새로 짓는다.
/// </summary>
public sealed class ChoiceRouteRedrawTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-choice-route", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 다른_카드로_옮겨_이으면_그_출발의_선만_긋고_옛_도착_포트는_내리고_새_포트를_세운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();
        Dictionary<string, Border> cards = CardVisuals(graph);
        int rails = graph.RailDrawCount;
        int choices = graph.ChoiceRedrawCount;

        Drag(graph, "root", slot: 0, onto: Card(session, "b"));

        Assert.Equal("b", Assert.Single(Edges(session)).ToEpisodeId);
        Assert.Equal(choices + 1, graph.ChoiceRedrawCount);   // 한 번 — ⛔ 알림이 한 번, 잇기가 또 한 번 다시 그렸다
        Assert.Equal(rails, graph.RailDrawCount);             // 철도를 통째로 긋지 않았다
        Assert.All(cards, pair => Assert.Same(pair.Value, CardVisuals(graph)[pair.Key]));
        Assert.Empty(graph.ArrivalsInto("a"));
        Assert.Equal([("root", "간다")], graph.ArrivalsInto("b"));
        SameAsFresh(graph, session);

        // 되돌리기 · 다시 실행도 그 출발의 선만 다시 긋는다(usability UX21 · ⛔ 판을 통째로 다시 그렸다).
        rails = graph.RailDrawCount;
        cards = CardVisuals(graph);
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("a", Assert.Single(Edges(session)).ToEpisodeId);
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.All(cards, pair => Assert.Same(pair.Value, CardVisuals(graph)[pair.Key]));
        Assert.Equal([("root", "간다")], graph.ArrivalsInto("a"));
        Assert.Empty(graph.ArrivalsInto("b"));
        SameAsFresh(graph, session);

        rails = graph.RailDrawCount;
        session.Editor.Redo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("b", Assert.Single(Edges(session)).ToEpisodeId);
        Assert.Equal(rails, graph.RailDrawCount);
        SameAsFresh(graph, session);

        window.Close();
    });

    [Fact]
    public void 빈_곳에_놓아_끊고_다시_이어도_통째로_그은_판과_같다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();
        int rails = graph.RailDrawCount;

        Drag(graph, "root", slot: 0, onto: null);
        Assert.Empty(Edges(session));
        Assert.Empty(graph.ArrivalsInto("a"));
        SameAsFresh(graph, session);

        // 문구는 칸에 남았다 — 그 칸을 다시 끌어 잇는다(새 간선).
        rails = graph.RailDrawCount;
        Drag(graph, "root", slot: 0, onto: Card(session, "a"));
        Assert.Equal(("a", "간다"), (Assert.Single(Edges(session)).ToEpisodeId, Assert.Single(Edges(session)).OptionLabel));
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Equal([("root", "간다")], graph.ArrivalsInto("a"));
        SameAsFresh(graph, session);

        window.Close();
    });

    [Fact]
    public void 장면_시작점이_바뀌면_그_카드만_새로_짓는다() => HeadlessUi.Run(() =>
    {
        // hall 장면에 b · c — 처음에는 b가 시작점이다(밖에서 오는 길이 없으면 첫 에피소드). opening의 root에서 c로 길을 옮기면 c가 시작점이 된다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEpisode("ch01", "c", title: "c", 660, 0, sceneId: "hall");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();
        Dictionary<string, Border> before = CardVisuals(graph);
        int rails = graph.RailDrawCount;

        Drag(graph, "root", slot: 0, onto: Card(session, "c"));

        Dictionary<string, Border> after = CardVisuals(graph);
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Same(before[Card(session, "root")], after[Card(session, "root")]);
        Assert.Same(before[Card(session, "a")], after[Card(session, "a")]);
        Assert.NotSame(before[Card(session, "b")], after[Card(session, "b")]);   // 시작점이 아니게 됐다
        Assert.NotSame(before[Card(session, "c")], after[Card(session, "c")]);   // 시작점이 됐다
        SameAsFresh(graph, session);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Open()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    private static string Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static IReadOnlyList<ChapterEdge> Edges(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.ToGraphModel("ch01").Edges;

    private static Dictionary<string, Border> CardVisuals(GraphEditorView graph) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Where(border => border.Tag is string && border.GetVisualDescendants().OfType<TextBlock>()
                .Any(block => block.Tag as string == GraphEditorView.CardTitleTag))
            .ToDictionary(border => (string)border.Tag!, StringComparer.Ordinal);

    /// <summary>지금 판이 카드 · 철도를 모두 새로 그은 판과 같은가 — 카드의 모습, 철도의 모양, 도착 포트의 목록.</summary>
    private static void SameAsFresh(GraphEditorView graph, AuthoringSession session)
    {
        List<string> shown = Picture(graph, session);
        graph.RebuildFresh();
        Dispatcher.UIThread.RunJobs();
        List<string> fresh = Picture(graph, session);

        Assert.True(fresh.SequenceEqual(shown),
            $"다시 그은 판이 통째로 그은 판과 다르다\n다시: {string.Join("\n", shown.Except(fresh))}\n통째: {string.Join("\n", fresh.Except(shown))}");
    }

    private static List<string> Picture(GraphEditorView graph, AuthoringSession session)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        var picture = new List<string>();

        foreach ((string nodeId, Border card) in CardVisuals(graph))
        {
            picture.Add($"카드 {nodeId} {Canvas.GetLeft(card):0.#},{Canvas.GetTop(card):0.#} w{card.Width:0.#} " +
                        string.Join(" ", card.GetVisualDescendants().OfType<TextBlock>()
                            .Where(block => block.FindAncestorOfType<TextBox>() is null)
                            .Select(block => $"{block.Text}@{ToolTip.GetTip(block)}@{block.Foreground}")) +
                        $" {card.Background} {card.BorderBrush}");
        }

        foreach (Control visual in graph.RailVisuals)
        {
            Point origin = visual.TranslatePoint(default, canvas)!.Value;
            var by = new Vector(origin.X, origin.Y);
            IEnumerable<Point> points = visual switch
            {
                Line line => [line.StartPoint + by, line.EndPoint + by],
                Polygon polygon => polygon.Points.Select(point => point + by),
                Avalonia.Controls.Shapes.Path path => [path.Data!.Bounds.TopLeft + by, path.Data.Bounds.BottomRight + by],
                _ => [origin, origin + new Vector(visual.Bounds.Width, visual.Bounds.Height)]
            };
            string texts = string.Join("/", visual.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));
            picture.Add($"철도 {visual.GetType().Name} {visual.Tag} " +
                        string.Join(",", points.Select(point => $"{Math.Round(point.X).ToString(CultureInfo.InvariantCulture)}:{Math.Round(point.Y).ToString(CultureInfo.InvariantCulture)}")) +
                        $" {texts}");
        }

        foreach (ChapterEpisode episode in session.Editor.FindChapter("ch01")!.Episodes)
        {
            picture.Add($"도착 {episode.EpisodeId} " + string.Join(",", graph.ArrivalsInto(episode.EpisodeId).Select(item => $"{item.From}:{item.Label}")));
        }

        picture.Sort(StringComparer.Ordinal);
        return picture;
    }

    /// <summary>그 카드의 선택지 칸 점을 눌러 끌고, 그 카드(null이면 판의 먼 빈 곳)에 놓는다 — 사람이 하는 길 그대로.</summary>
    private static void Drag(GraphEditorView graph, string episodeId, int slot, string? onto)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        Ellipse knob = canvas.Children.OfType<StackPanel>()
            .Single(row => row.Tag as string == $"{episodeId}#{slot}")
            .Children.OfType<Ellipse>().Single();

        knob.RaiseEvent(new PointerPressedEventArgs(
            knob, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        Point on = onto is null
            ? new Point(5000, 5000)
            : CardVisuals(graph)[onto] is { } card
                ? new Point(Canvas.GetLeft(card) + 20, Canvas.GetTop(card) + 10)
                : default;

        // 포인터 이벤트의 자리는 최상위 기준이다 — 판 좌표를 그대로 주면 엉뚱한 데에 놓여 아무 일도 안 일어난다.
        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, canvas.TranslatePoint(on, root)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }
}
