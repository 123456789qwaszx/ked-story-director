using Avalonia.Controls;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 판은 그 챕터의 에피소드만 감싼다</b> — 왼위 에피소드가 판 원점(여백 안)에 오게 그리고, 판의 크기도 그 챕터의
/// 에피소드로만 정해진다. [대본] 탭에서 카드를 세우면 그 에피소드의 [구성] 자리는 카드 자리를 그대로 받지 않고, 같은 챕터의
/// 가장 가까운 카드의 에피소드 곁에 선다(첫 카드는 원점).
/// </summary>
public sealed class ChapterBoardExtentTests : IDisposable
{
    private const double CardWidth = 190;
    private const double CardHeight = 74;
    private const double Margin = 60;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-chapter-board-extent", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 대본_탭의_먼_자리에_세운_카드도_구성_판은_그_챕터만_감싼다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        Assert.Null(session.CreateChapter("ch09"));
        string board = session.ActiveFileId!;

        // [대본] 판에서 보던 화면 한가운데 — 원점에서 먼 자리에 카드 둘을 세운다(위 · 아래).
        session.Editor.AddDialogueNode(board, 2600, 1800, name: "n1");
        session.Editor.AddDialogueNode(board, 2600, 2200, name: "n2");
        Dispatcher.UIThread.RunJobs();

        // 에피소드 자리는 카드 자리가 아니다 — 첫 카드는 원점, 둘째는 그 아래 한 칸(판의 줄 간격을 줄여서).
        ChapterGraphModel model = session.Editor.FindChapter("ch09")!.ToGraphModel("ch09");
        Assert.Equal((0, 0), (model.FindEpisode("n1")!.X, model.FindEpisode("n1")!.Y));
        Assert.Equal((0, 170), (model.FindEpisode("n2")!.X, model.FindEpisode("n2")!.Y));

        ChapterGraphView view = Show(window, "ch09");
        Canvas canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Assert.Equal(Margin + CardWidth + Margin, canvas.Width);
        Assert.Equal(Margin + 170 + CardHeight + Margin, canvas.Height);

        window.Close();
    });

    [Fact]
    public void 이미_먼_자리에_적힌_에피소드도_판_원점으로_당겨_그린다() => HeadlessUi.Run(() =>
    {
        // 고치기 전에 만든 챕터 — 에피소드가 카드 자리(먼 양수)를 그대로 지고 있다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        session.Editor.MoveEpisodes("ch02", new Dictionary<string, (double X, double Y)>
        {
            ["x"] = (2600, 1800),
            ["y"] = (2820, 1800)
        });
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView view = Show(window, "ch02");
        Canvas canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Border x = canvas.Children.OfType<Border>().Single(card => card.Tag as string == "x");

        Assert.Equal((Margin, Margin), (Canvas.GetLeft(x), Canvas.GetTop(x)));
        Assert.Equal(Margin + 220 + CardWidth + Margin, canvas.Width);
        Assert.Equal(Margin + CardHeight + Margin, canvas.Height);

        window.Close();
    });

    [Fact]
    public void 전체_보기는_큰_챕터를_한_화면에_넣고_작은_챕터는_키우지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        session.Editor.MoveEpisodes("ch02", new Dictionary<string, (double X, double Y)>
        {
            ["x"] = (0, 0),
            ["y"] = (4000, 2600)
        });
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView view = Show(window, "ch02");
        ScrollViewer scroll = view.FindControl<ScrollViewer>("GraphScroll")!;
        Canvas canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Button fit = view.FindControl<Button>("FitChapterButton")!;
        Assert.Equal("전체 보기", fit.Content);

        // 큰 챕터 — 판 전체(두 카드 + 여백)가 화면 안에 들어오고, 한쪽은 화면에 꼭 맞는다.
        fit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        double scale = Scale(view);
        Assert.True(scale < 1);
        Assert.True(canvas.Width * scale <= scroll.Viewport.Width + 0.5);
        Assert.True(canvas.Height * scale <= scroll.Viewport.Height + 0.5);
        Assert.True(Math.Abs((canvas.Width * scale) - scroll.Viewport.Width) < 0.5 ||
                    Math.Abs((canvas.Height * scale) - scroll.Viewport.Height) < 0.5);
        Assert.Equal(default, scroll.Offset);

        // 작은 챕터 — 100%보다 키우지 않는다.
        view = Show(window, "ch01");
        fit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, Scale(view), 3);

        window.Close();
    });

    /// <summary>판의 배율 — 배율은 ZoomHost의 LayoutTransform이 진다.</summary>
    private static double Scale(ChapterGraphView view) =>
        ((Avalonia.Media.ScaleTransform)view.FindControl<LayoutTransformControl>("ZoomHost")!.LayoutTransform!).ScaleX;

    private static ChapterGraphView Show(MainWindow window, string chapterId)
    {
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter(chapterId);
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        return view;
    }
}
