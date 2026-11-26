using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 판을 줄여도 선택지 문구는 화면에서 100%일 때보다 작아지지 않는다</b>(2026-11-27 소유자 — 「구성탭에서는 저 간선 중앙에 있는 선택지 문구가 중요한데,
/// 현재는 … 배율을 축소해서 넓은 화면을 볼 때, 저 선택지 문구가 잘 보이지 않습니다」). [대본] 판의 챕터 · 장면 이름표와 같은 바닥이다(판에서의 크기 × 배율, 단
/// 화면 최소). 문구 상자는 제 가운데(선 위의 그 자리)를 축으로 커져 선을 떠나지 않는다.
/// </summary>
public sealed class ChapterEdgeLabelZoomTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-edge-label-zoom", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 판을_줄여도_선택지_문구는_화면에서_100퍼센트_크기이고_제_자리를_축으로_커진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        session.Editor.MoveEpisode("ch01", "b", 40, 420);
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "교무실로 간다");
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Canvas canvas = view.FindControl<Canvas>("GraphCanvas")!;

        foreach (double zoom in new[] { 1.5, 1.0, 0.5, 0.25, 0.1 })
        {
            view.ZoomTo(zoom);
            Settle(window);

            Border label = canvas.Children.OfType<Border>()
                .Single(border => border.Tag is ChapterGraphView.EdgeLabelMark { FromEpisodeId: "root", ToEpisodeId: "b" });

            // 화면에서의 폭 = 판의 폭 × 배율, 단 100%일 때의 폭 아래로 줄지 않는다.
            Point left = label.TranslatePoint(new Point(0, 0), window)!.Value;
            Point right = label.TranslatePoint(new Point(label.Bounds.Width, 0), window)!.Value;
            Assert.Equal(label.Bounds.Width * Math.Max(zoom, 1), right.X - left.X, precision: 1);
            Assert.Equal(Math.Max(1, 1 / zoom), view.EdgeLabelScale, precision: 6);

            // 가운데는 그대로 — 판의 그 자리(선 위)가 화면에서 서는 곳이다. 그 자리는 판이 상자를 놓은 자리(Bounds)로 잰다 — Canvas.Left는 소수라
            // 놓을 때 화소에 맞춰 반올림되고, 그 차이(판에서 0.5 아래)가 배율만큼 커져 보인다(⛔ Canvas.Left로 재면 1.5배에서 0.375 어긋났다).
            Point center = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
            Point spot = canvas.TranslatePoint(
                new Point(label.Bounds.X + (label.Bounds.Width / 2), label.Bounds.Y + (label.Bounds.Height / 2)), window)!.Value;
            Assert.True(Math.Abs(label.Bounds.X - Canvas.GetLeft(label)) < 0.5, $"{zoom}: 판의 그 자리에 놓였다");
            Assert.Equal(spot.X, center.X, precision: 1);
            Assert.Equal(spot.Y, center.Y, precision: 1);
        }

        window.Close();
    });

    private static void Settle(Window window)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }
}
