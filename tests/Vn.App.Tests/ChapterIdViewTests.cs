using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 탭 「ID로 보기」</b>(2026-11-18 소유자 · <c>docs/work-orders/id-view-orders.md</c>) — 처음에는 켜져 있어 하이어아키 · 카드가 ID로 서고,
/// 끄면 [대본] 탭처럼 보이는 이름으로 선다. 카드 머리는 한 줄(⛔ 제목 아래 EpisodeId 둘째 줄은 걷혔다). 앱 설정에 기억한다.
/// </summary>
public sealed class ChapterIdViewTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-id-view", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ID로_보기를_끄면_하이어아키와_카드가_이름으로_서고_켜면_ID로_돌아간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);

        try
        {
            ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
            CheckBox check = view.FindControl<CheckBox>("ShowIdsCheck")!;
            session.Editor.UpdateChapterDisplayName("ch01", "축제 전날");
            session.Editor.SetSceneName("ch01", "opening", "교실");
            session.Editor.UpdateEpisode("ch01", "root", title: "첫 만남");
            view.HierarchyView.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
            view.HierarchyView.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
            view.SelectChapter("ch01");
            view.Refresh();
            Dispatcher.UIThread.RunJobs();

            HierarchyRow Row(string key) => view.HierarchyView.Rows.Single(row => row.Key == key);

            // 처음 — ID로 본다. 이름은 하이어아키 곁글에.
            Assert.True(check.IsChecked);
            Assert.Equal(("ch01", "축제 전날"), (Row(StoryHierarchy.ChapterKey("ch01")).Text, Row(StoryHierarchy.ChapterKey("ch01")).Detail));
            Assert.Equal(("opening", "교실"), (Row(StoryHierarchy.SceneKey("ch01", "opening")).Text, Row(StoryHierarchy.SceneKey("ch01", "opening")).Detail));
            Assert.Equal(("root", "첫 만남"), (Row(StoryHierarchy.EpisodeKey("ch01", "root")).Text, Row(StoryHierarchy.EpisodeKey("ch01", "root")).Detail));
            Assert.Equal("root", NameLine(view, "root"));
            Assert.Contains("opening", Texts(view, "root"));
            Assert.DoesNotContain("첫 만남", Texts(view, "root"));   // ⛔ 칩 옆의 제목 · 아래의 EpisodeId 두 줄은 걷혔다 — 한 줄이다

            // 끈다 — [대본] 탭처럼 이름으로.
            check.IsChecked = false;
            Dispatcher.UIThread.RunJobs();

            Assert.False(session.ShowIds);
            Assert.False(AppSettingsService.LoadShowIds());   // 앱 설정에 기억한다
            Assert.Equal(("축제 전날", null), (Row(StoryHierarchy.ChapterKey("ch01")).Text, Row(StoryHierarchy.ChapterKey("ch01")).Detail));
            Assert.Equal("교실", Row(StoryHierarchy.SceneKey("ch01", "opening")).Text);
            Assert.Equal(("첫 만남", null), (Row(StoryHierarchy.EpisodeKey("ch01", "root")).Text, Row(StoryHierarchy.EpisodeKey("ch01", "root")).Detail));
            Assert.Equal("첫 만남", NameLine(view, "root"));
            Assert.Contains("교실", Texts(view, "root"));
            Assert.DoesNotContain("root", Texts(view, "root"));

            // 다시 켠다.
            check.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("root", NameLine(view, "root"));
            Assert.Equal("ch01", Row(StoryHierarchy.ChapterKey("ch01")).Text);
        }
        finally
        {
            session.SetShowIds(true);   // 같은 프로세스의 다른 시험이 처음 상태로 보게
            window.Close();
        }
    });

    private static Border Card(ChapterGraphView view, string episodeId) =>
        view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == episodeId);

    private static string? NameLine(ChapterGraphView view, string episodeId) =>
        Card(view, episodeId).GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Tag as string == ChapterGraphView.CardNameTag).Text;

    private static List<string?> Texts(ChapterGraphView view, string episodeId) =>
        Card(view, episodeId).GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
}
