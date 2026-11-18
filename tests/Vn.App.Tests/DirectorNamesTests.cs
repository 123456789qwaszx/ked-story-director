using Avalonia.Controls;
using Avalonia.Input;
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

        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("축제 전날", graph.InspectorHeaderText);   // 머리는 이름만 — 「챕터 · 」 머리말은 2026-11-18에 걷혔다

        graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "opening"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("교실", graph.InspectorHeaderText);

        graph.HierarchyView.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        graph.HierarchyView.RowButton(StoryHierarchy.EpisodeKey("ch01", "a"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("축제 전날 - 교실", graph.InspectorHeaderText);

        // 콤보의 항목은 ID 그대로(고르는 규칙), 보이는 글은 이름.
        ComboBox combo = graph.FindControl<ComboBox>("ChapterCombo")!;
        Assert.Equal("ch01", combo.SelectedItem);
        Control shown = combo.ItemTemplate!.Build("ch01")!;
        Assert.Equal("축제 전날", ((TextBlock)shown).Text);

        // [구성] 탭의 콤보도 같다(2026-11-18 소유자 — 「대본과 동일하게 Chapter의 displayName이 드롭다운에」).
        ComboBox structure = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<ComboBox>("ChapterCombo")!;
        Assert.Equal("ch01", structure.SelectedItem);
        Assert.Equal("축제 전날", ((TextBlock)structure.ItemTemplate!.Build("ch01")!).Text);
        Assert.Equal("ch02", ((TextBlock)structure.ItemTemplate!.Build("ch02")!).Text);   // 이름이 없으면 ID

        window.Close();
    });

    [Fact]
    public void 챕터와_장면의_이름은_인스펙터_머리의_이름_칸에서_고친다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「챕터이름과 장면 이름을 우측 인스펙터에서 바꿀 수 있도록 해주십시오. 에피소드와 마찬가지로.」
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        TextBox box = graph.FindControl<TextBox>("InspectorNameBox")!;

        void Pick(string key)
        {
            graph.HierarchyView.RowButton(key)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        void Press(Key key)
        {
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
            Dispatcher.UIThread.RunJobs();
        }

        // 챕터 — 이름 칸에 지금 이름(비었으면 ID). 적고 Enter면 한 칸에 바뀌고 하이어아키도 따라간다.
        Pick(StoryHierarchy.ChapterKey("ch01"));
        Assert.True(box.IsVisible);
        Assert.Equal("ch01", box.Text);
        long before = session.Editor.Revision;
        box.Text = "축제 전날";
        Press(Key.Enter);
        Assert.Equal("축제 전날", session.Editor.FindChapter("ch01")!.DisplayName);
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Equal("축제 전날", Text(graph.HierarchyView, StoryHierarchy.ChapterKey("ch01")));

        // Esc는 그만두기 — 적던 글이 지금 이름으로 돌아가고 아무것도 안 바뀐다.
        box.Text = "안 쓸 이름";
        Press(Key.Escape);
        Assert.Equal("축제 전날", box.Text);
        Assert.Equal(before + 1, session.Editor.Revision);

        // 장면 — 초점이 떠나면 바꾼다(에피소드 이름 칸과 같다).
        Pick(StoryHierarchy.SceneKey("ch01", "opening"));
        Assert.Equal("opening", box.Text);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        box.Text = "교실";
        graph.FindControl<Button>("AddEpisodeButton")!.Focus();   // 사람이 하는 그대로 — 다른 데를 누른다
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("교실", ChapterSceneNotes.NameOf(session.Editor.FindChapter("ch01")!.SceneNotes, "opening"));

        // 비우면 ID가 보인다.
        Pick(StoryHierarchy.ChapterKey("ch01"));
        box.Text = string.Empty;
        Press(Key.Enter);
        Assert.Equal("ch01", box.Text);

        // 프로젝트 줄 · 에피소드는 이름 칸이 아니다(에피소드는 대사 편집기의 이름 칸이 고친다).
        Pick(StoryHierarchy.ProjectKey);
        Assert.False(box.IsVisible);
        Assert.Equal("화자", graph.InspectorHeaderText);

        window.Close();
    });

    private static string Text(StoryHierarchy tree, string key) =>
        tree.Rows.Single(row => row.Key == key).Text;
}
