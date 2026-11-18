using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// [대본] 탭은 이름만 보인다 (2026-11-16 · <c>docs/work-orders/display-names-orders.md</c> N3) — 하이어아키의 챕터 · 장면 줄 · 인스펙터 머리 ·
/// 판의 챕터 이름표가 이름(비면 ID)이다. [구성] 탭 하이어아키는 ID다.
/// </summary>
public sealed class DirectorNamesTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-names", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 대본_탭은_이름을_구성_탭은_ID를_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        session.Editor.UpdateChapterDisplayName("ch01", "축제 전날");
        session.Editor.SetSceneName("ch01", "opening", "교실");
        Dispatcher.UIThread.RunJobs();

        StoryHierarchy director = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        StoryHierarchy structure = window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;
        director.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        structure.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);

        Assert.Equal("축제 전날", Text(director, StoryHierarchy.ChapterKey("ch01")));
        Assert.Equal("교실", Text(director, StoryHierarchy.SceneKey("ch01", "opening")));
        Assert.Equal("ch02", Text(director, StoryHierarchy.ChapterKey("ch02")));   // 이름이 비면 ID

        Assert.Equal("ch01", Text(structure, StoryHierarchy.ChapterKey("ch01")));
        Assert.Equal("opening", Text(structure, StoryHierarchy.SceneKey("ch01", "opening")));

        window.Close();
    });

    [Fact]
    public void 인스펙터_머리는_이름이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        session.Editor.UpdateChapterDisplayName("ch01", "축제 전날");
        session.Editor.SetSceneName("ch01", "opening", "교실");
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        TextBlock title = graph.FindControl<TextBlock>("InspectorTitle")!;

        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("축제 전날", title.Text);   // 머리는 이름만 — 「챕터 · 」 머리말은 2026-11-18에 걷혔다

        graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "opening"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("교실", title.Text);

        graph.HierarchyView.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        graph.HierarchyView.RowButton(StoryHierarchy.EpisodeKey("ch01", "a"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("축제 전날 - 교실", title.Text);

        // 콤보의 항목은 ID 그대로(고르는 규칙), 보이는 글은 이름.
        ComboBox combo = graph.FindControl<ComboBox>("ChapterCombo")!;
        Assert.Equal("ch01", combo.SelectedItem);
        Control shown = combo.ItemTemplate!.Build("ch01")!;
        Assert.Equal("축제 전날", ((TextBlock)shown).Text);

        window.Close();
    });

    private static string Text(StoryHierarchy tree, string key) =>
        tree.Rows.Single(row => row.Key == key).Text;
}
