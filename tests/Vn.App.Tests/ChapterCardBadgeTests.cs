using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 카드 머리의 장면 배지는 작가의 말로 적는다</b> — 에피소드마다 장면ID, 장면이 시작하는 카드는 「장면 시작점」을 단다(2026-11-18 소유자).
/// ⛔ 「장면 · 장면ID」는 같은 날 걷었다(「장면」이 겹친다). 「장면 시작점」도 그때 「시작점」으로 줄였다가 소유자가 되돌렸다(chrome-card-header U2).
/// 영어 「SCENE」 · 「SCENE ROOT」는 쓰지 않는다.
///
/// 머리는 두 줄이다 — 위는 칩, 아래는 이름(U2). 칩은 높이가 박혀 있어 글꼴이 무엇이든(라틴 장면ID · 한글 장면 이름) 상자가 같고, 칩 · 이름은 한글 먼저
/// 글꼴 한 벌로 그린다.
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
    public void 장면_배지는_장면ID와_장면_시작점으로_적힌다() => HeadlessUi.Run(() =>
    {
        // ch01 — opening: root · a, hall: b.
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        // 2026-11-18 소유자 — 「"시작점"을 "장면 시작점"으로」(U2). 장면 칩은 장면ID 그대로(「장면 · 」 머리말 없이).
        Assert.Contains("장면 시작점", Badges(view, "root"));
        Assert.Contains("opening", Badges(view, "root"));
        Assert.DoesNotContain("장면 시작점", Badges(view, "a"));
        Assert.Contains("opening", Badges(view, "a"));
        Assert.Contains("hall", Badges(view, "b"));
        Assert.DoesNotContain(Badges(view, "root").Concat(Badges(view, "b")), badge => badge?.StartsWith("장면 ·", StringComparison.Ordinal) == true);

        Assert.DoesNotContain(
            view.FindControl<Canvas>("GraphCanvas")!.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text?.Contains("SCENE", StringComparison.Ordinal) == true);

        window.Close();
    });

    [Fact]
    public void 칩은_ID로_보든_이름으로_보든_높이가_같고_이름은_칩_아래_줄에_선다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「ID로 보기를 체크하냐 마냐에 따라서, 시작점과 장면 이름 박스의 높이가 달라져서 불편합니다」 · 「에피소드 이름이 시작점과
        // 새장면 아래쪽에」(U2). 라틴 장면ID와 한글 장면 이름은 글꼴의 줄 높이가 달라, 칩이 글을 따라 자라면 높이가 바뀐다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        session.Editor.SetSceneName("ch01", "opening", "첫 장면");
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        foreach (bool showIds in new[] { true, false })
        {
            session.SetShowIds(showIds);
            Dispatcher.UIThread.RunJobs();

            Border card = Card(view, "root");
            List<Border> chips = card.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Child is TextBlock && !ReferenceEquals(border, card)).ToList();
            Assert.Equal(2, chips.Count);   // 장면 시작점 · 장면
            Assert.All(chips, chip => Assert.Equal(ChapterGraphView.CardChipHeight, chip.Bounds.Height, 1));
            Assert.Equal(showIds ? "opening" : "첫 장면", ((TextBlock)chips[1].Child!).Text);

            // 이름은 칩 아래 줄이다 — 칩의 아래 끝보다 아래에서 시작한다.
            TextBlock name = card.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Tag as string == ChapterGraphView.CardNameTag);
            double chipBottom = chips.Max(chip => chip.TranslatePoint(new Avalonia.Point(0, chip.Bounds.Height), card)!.Value.Y);
            Assert.True(name.TranslatePoint(new Avalonia.Point(0, 0), card)!.Value.Y >= chipBottom, "이름이 칩 줄 아래에 서야 한다");

            // 칩 · 이름이 한 글꼴이다(한글 먼저).
            Assert.All(chips, chip => Assert.Equal(ChapterGraphView.CardHeadFont, ((TextBlock)chip.Child!).FontFamily));
            Assert.Equal(ChapterGraphView.CardHeadFont, name.FontFamily);
        }

        session.SetShowIds(true);
        window.Close();
    });

    private static Border Card(ChapterGraphView view, string episodeId) =>
        view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == episodeId);

    /// <summary>그 에피소드 카드 안의 글들.</summary>
    private static List<string?> Badges(ChapterGraphView view, string episodeId) =>
        view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(card => card.Tag as string == episodeId)
            .GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text)
            .ToList();
}
