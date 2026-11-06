using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 카드 머리의 장면 배지는 작가의 말로 적는다</b> — 장면ID를 적은 에피소드는 「장면 · 장면ID」, 장면이 시작하는 카드는
/// 「장면 시작점」을 단다. 영어 「SCENE」 · 「SCENE ROOT」는 쓰지 않는다.
/// </summary>
public sealed class ChapterCardBadgeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-chapter-card-badge", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 장면_배지는_장면과_장면_시작점으로_적힌다() => HeadlessUi.Run(() =>
    {
        // ch01 — opening: root · a, hall: b.
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("장면 시작점", Badges(view, "root"));
        Assert.Contains("장면 · opening", Badges(view, "root"));
        Assert.DoesNotContain("장면 시작점", Badges(view, "a"));
        Assert.Contains("장면 · opening", Badges(view, "a"));
        Assert.Contains("장면 · hall", Badges(view, "b"));

        Assert.DoesNotContain(
            view.FindControl<Canvas>("GraphCanvas")!.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text?.Contains("SCENE", StringComparison.Ordinal) == true);

        window.Close();
    });

    /// <summary>그 에피소드 카드 안의 글들.</summary>
    private static List<string?> Badges(ChapterGraphView view, string episodeId) =>
        view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(card => card.Tag as string == episodeId)
            .GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text)
            .ToList();
}
