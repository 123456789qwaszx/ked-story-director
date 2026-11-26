using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 카드도 장면 색이다</b>(design/scene-colors.md ⑥ · 2026-11-27 소유자 — 「권고대로」 · 「다음 단계 진행」). [구성] 판에는 장면 테두리를 그리지 않고, 카드의
/// 장면 꼬리표를 그 장면의 점 색으로 칠하고 카드 왼쪽 가장자리에 같은 색의 얇은 띠를 둔다 — [대본] 판의 장면 면 · 하이어아키의 점과 한 색이라 두 탭에서 같은
/// 장면을 색으로 찾는다. ⛔ 그 전에는 꼬리표가 모든 장면에서 회색(SceneChip)이었다.
/// </summary>
public sealed class SceneColorStructureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-scene-color-structure", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 장면_꼬리표와_카드_왼쪽_띠가_그_장면의_색이다() => HeadlessUi.Run(() =>
    {
        // ch01 — opening: root · a, hall: b.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        ChapterGraphView view = Show(window);
        IReadOnlyDictionary<string, int> colors = Colors(session);

        Assert.NotEqual(colors["opening"], colors["hall"]);   // 이야기 차례로 이웃한 장면은 다르다

        foreach ((string episodeId, string sceneId) in new[] { ("root", "opening"), ("a", "opening"), ("b", "hall") })
        {
            Color mark = Skin.SceneMarkOf(colors[sceneId]);
            Assert.Equal(mark, ((ISolidColorBrush)Chip(view, episodeId).Background!).Color);
            AssertStripe(Card(view, episodeId), mark);
        }

        window.Close();
    });

    [Fact]
    public void 색을_고르면_카드가_따라가고_되돌리면_돌아온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        ChapterGraphView view = Show(window);
        int auto = Colors(session)["hall"];
        int picked = (auto % SceneColors.Count) + 1;

        session.Editor.SetSceneColor("ch01", "hall", picked);
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Skin.SceneMarkOf(picked), ((ISolidColorBrush)Chip(view, "b").Background!).Color);
        AssertStripe(Card(view, "b"), Skin.SceneMarkOf(picked));

        session.Editor.Undo();
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Skin.SceneMarkOf(auto), ((ISolidColorBrush)Chip(view, "b").Background!).Color);
        AssertStripe(Card(view, "b"), Skin.SceneMarkOf(auto));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>카드 바탕 — 왼쪽 끝에서 띠 폭까지 그 장면 색, 그 뒤는 카드 면(끊긴 그라데이션 · 둥근 모서리를 따른다).</summary>
    private static void AssertStripe(Border card, Color mark)
    {
        LinearGradientBrush surface = Assert.IsType<LinearGradientBrush>(card.Background);
        Assert.Equal(Avalonia.RelativeUnit.Absolute, surface.EndPoint.Unit);
        Assert.Equal(0d, surface.StartPoint.Point.X);
        Assert.Equal(ChapterGraphView.SceneStripeWidth, surface.EndPoint.Point.X);
        Assert.Equal(mark, surface.GradientStops[0].Color);
        Assert.Equal(Skin.StructureCardSurface, surface.GradientStops[surface.GradientStops.Count - 1].Color);
    }

    private static ChapterGraphView Show(MainWindow window)
    {
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static IReadOnlyDictionary<string, int> Colors(AuthoringSession session)
    {
        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        return SceneColors.Of(ChapterSceneGrouping.Of(chapter.ToGraphModel("ch01")), chapter.SceneNotes);
    }

    private static Border Card(ChapterGraphView view, string episodeId) =>
        view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == episodeId);

    private static Border Chip(ChapterGraphView view, string episodeId) =>
        Card(view, episodeId).GetVisualDescendants().OfType<Border>().Single(border => Equals(border.Tag, ChapterGraphView.SceneChipTag));
}
