using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 판의 범위 선택(러버밴드)</b> — [대본] 탭과 같은 규칙이다. 빈 판을 끌면 사각형이 서고, 카드가 걸리면 카드만 무리가
/// 되어(함께 걸친 선은 고르지 않는다) 끌기 시작한 자리에서 가장 가까운 카드가 인스펙터에 선다. 무리의 카드를 끌면 무리가 함께
/// 움직이고, 놓을 때 한 번 적는다(되돌리기 한 칸). 카드 없이 선(선택지 선 · 분기 점선)만 걸리면 놓은 자리(포인터)에서 가장
/// 가까운 선 하나를 고른다.
/// </summary>
public sealed class ChapterRubberBandTests : IDisposable
{
    private static readonly Color Picked = Color.Parse("#3D7BD9");

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-chapter-rubber-band", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 카드가_걸리면_카드만_무리가_되고_시작점에서_가까운_카드가_인스펙터에_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view, AuthoringSession _) = Show();

        // b 카드 아래 빈 곳에서 root 카드 안쪽까지 — 세 카드와 선택지 선 둘이 함께 걸친다. 시작점에는 b가 가깝다.
        Band(view, new Point(520, 150), new Point(200, 120));

        Assert.True(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.Equal("b", view.FindControl<TextBox>("IdBox")!.Text);
        Assert.False(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.True(IsPicked(Card(view, "root")));
        Assert.True(IsPicked(Card(view, "a")));
        Assert.True(IsPicked(Card(view, "b")));
        Assert.False(IsPicked(Card(view, "분기 1")));

        // 빈 판을 누르면 무리가 풀린다.
        Press(Canvas(view), At(view, new Point(700, 400)));
        Release(Canvas(view), At(view, new Point(700, 400)));
        Assert.False(IsPicked(Card(view, "root")));
        Assert.False(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);

        window.Close();
    });

    [Fact]
    public void 무리의_카드를_끌면_무리가_함께_움직이고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view, AuthoringSession session) = Show();
        Band(view, new Point(520, 150), new Point(200, 120));

        // 무리의 root 카드를 잡아 (40, 200)만큼 끈다.
        var grab = new Point(150, 90);
        Press(Card(view, "root"), At(view, grab));
        Move(Canvas(view), At(view, grab + new Point(40, 200)));
        Release(Canvas(view), At(view, grab + new Point(40, 200)));

        Assert.Equal((40, 200), Spot(session, "root"));
        Assert.Equal((260, 200), Spot(session, "a"));
        Assert.Equal((480, 200), Spot(session, "b"));
        Assert.Equal((220, 200), Spot(session, "분기 1"));   // 무리 밖은 그대로

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((0, 0), Spot(session, "root"));
        Assert.Equal((220, 0), Spot(session, "a"));
        Assert.Equal((440, 0), Spot(session, "b"));

        window.Close();
    });

    [Fact]
    public void 선만_걸리면_놓은_자리에서_가장_가까운_선_하나만_고른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view, AuthoringSession _) = Show();

        // root와 a 사이의 틈(x 250~280) — 선택지 선 둘(root→a는 위, root→b는 아래)만 지난다. 놓은 자리(오른쪽 아래)에는
        // root→b가 가깝다.
        Band(view, new Point(255, 95), new Point(275, 125));

        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.Equal("교무실로 간다", ChoiceText(view));
        Assert.False(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);

        // 거꾸로 그으면 놓은 자리(왼쪽 위)에 root→a가 가깝다.
        Band(view, new Point(275, 125), new Point(255, 95));

        Assert.Equal("도서관으로 간다", ChoiceText(view));

        window.Close();
    });

    [Fact]
    public void 분기_점선만_걸리면_그_분기를_고른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view, AuthoringSession _) = Show();

        // root 오른변의 분기점에서 아래 분기 카드로 내려가는 점선의 가운데쯤.
        Band(view, new Point(255, 185), new Point(275, 210));

        Assert.True(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);
        Assert.False(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// [구성] 탭의 ch01 — root(0,0) · a(220,0) · b(440,0) 위에 선택지 둘(root→b · root→a)과 root의 분기 하나를 놓는다. 분기 카드는
    /// 다른 카드와 겹치지 않게 a 아래(220,200)로 옮긴다. 판에는 여백 60을 더해 선다(카드 190×74).
    /// </summary>
    private (MainWindow Window, ChapterGraphView View, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "교무실로 간다");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "도서관으로 간다");
        DialogueNode root = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        session.Editor.MoveEpisodes("ch01", new Dictionary<string, (double X, double Y)>
        {
            ["root"] = (0, 0),
            ["a"] = (220, 0),
            ["b"] = (440, 0),
            ["분기 1"] = (220, 200)
        });
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        return (window, view, session);
    }

    private static (double X, double Y) Spot(AuthoringSession session, string episodeId)
    {
        ChapterEpisode episode = session.Editor.FindChapter("ch01")!.ToGraphModel("ch01").FindEpisode(episodeId)!;
        return (episode.X, episode.Y);
    }

    private static Canvas Canvas(ChapterGraphView view) => view.FindControl<Canvas>("GraphCanvas")!;

    private static Border Card(ChapterGraphView view, string episodeId) =>
        Canvas(view).Children.OfType<Border>().Single(card => card.Tag as string == episodeId);

    private static bool IsPicked(Border card) => card.BorderBrush is ISolidColorBrush { Color: var color } && color == Picked;

    /// <summary>간선 패널의 선택지 문구 칸 — 고른 간선의 문구가 적혀 있다.</summary>
    private static string? ChoiceText(ChapterGraphView view) => view.FindControl<TextBox>("EdgeLabelEditBox")!.Text;

    /// <summary>빈 판을 눌러 끌고 놓는다 — 사람이 범위를 긋는 길 그대로다.</summary>
    private static void Band(ChapterGraphView view, Point from, Point to)
    {
        Press(Canvas(view), At(view, from));
        Move(Canvas(view), At(view, to));
        Release(Canvas(view), At(view, to));
    }

    /// <summary>판 좌표를 최상위 좌표로 — 포인터 이벤트의 자리는 최상위 기준으로 풀린다.</summary>
    private static Point At(ChapterGraphView view, Point onCanvas) =>
        Canvas(view).TranslatePoint(onCanvas, (Visual)TopLevel.GetTopLevel(view)!)!.Value;

    private static void Press(Control control, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Move(Canvas canvas, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, canvas, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Canvas canvas, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }
}
