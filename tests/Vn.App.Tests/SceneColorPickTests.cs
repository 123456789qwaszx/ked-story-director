using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>장면 색을 고르는 손잡이</b>(design/scene-colors.md 5.1 · 장면 색 4단계 · 2026-11-27 소유자 — 「권고대로」). 하이어아키의 장면 줄은 이름 앞에 그 장면 색의
/// 점이 서고(두 탭 모두), [대본] 하이어아키의 장면 줄 우클릭 「장면 색」 · 판의 장면 이름표 우클릭 · 인스펙터의 장면 색 줄에서 고른다. 여러 장면 줄을 골랐으면
/// 모두에 한 번에 칠하고 되돌리기도 한 번이다.
/// </summary>
public sealed class SceneColorPickTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-scene-color-pick", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 하이어아키의_장면_줄은_두_탭_모두_이름_앞에_그_장면_색의_점이_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        int color = SceneColorsOf(session)["hall"];

        foreach (StoryHierarchy tree in new[] { graph.HierarchyView, window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView })
        {
            tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
            Dispatcher.UIThread.RunJobs();

            var line = (StackPanel)tree.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!.Content!;
            var dot = Assert.IsType<Avalonia.Controls.Shapes.Ellipse>(line.Children[0]);
            Assert.Equal(StoryHierarchy.SceneDotTag, dot.Tag);
            Assert.Equal(Skin.SceneMarkOf(color), ((ISolidColorBrush)dot.Fill!).Color);
        }

        window.Close();
    });

    [Fact]
    public void 장면_줄_우클릭_장면_색으로_고르면_칠하고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        int auto = SceneColorsOf(session)["hall"];
        int picked = (auto % SceneColors.Count) + 1;
        long revision = session.Editor.Revision;

        MenuItem item = ColorItem(graph.HierarchyView, "hall", picked.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains("✓", (string)ColorItem(graph.HierarchyView, "hall", "auto").Header!, StringComparison.Ordinal);   // 처음은 자동
        Click(item);

        Assert.Equal(picked, Chosen(session, "hall"));
        Assert.Equal(revision + 1, session.Editor.Revision);
        Assert.Contains(SceneColorMenu.NameOf(picked), session.StatusMessage, StringComparison.Ordinal);

        // 다시 세운 줄의 점 · 메뉴의 ✓가 그 색을 따른다.
        var line = (StackPanel)graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!.Content!;
        Assert.Equal(Skin.SceneMarkOf(picked), ((ISolidColorBrush)((Avalonia.Controls.Shapes.Ellipse)line.Children[0]).Fill!).Color);
        Assert.EndsWith("✓", (string)ColorItem(graph.HierarchyView, "hall", picked.ToString(System.Globalization.CultureInfo.InvariantCulture)).Header!, StringComparison.Ordinal);

        // 자동으로 돌린다.
        Click(ColorItem(graph.HierarchyView, "hall", "auto"));
        Assert.Null(Chosen(session, "hall"));

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(picked, Chosen(session, "hall"));

        window.Close();
    });

    [Fact]
    public void 여러_장면_줄을_골랐으면_모두에_한_번에_칠한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        StoryHierarchy tree = graph.HierarchyView;
        Press(window, tree, StoryHierarchy.SceneKey("ch01", "opening"));
        Press(window, tree, StoryHierarchy.SceneKey("ch01", "hall"), RawInputModifiers.Control);
        Assert.Equal(2, tree.Selections.Count);
        long revision = session.Editor.Revision;

        Click(ColorItem(tree, "hall", "5"));

        Assert.Equal(5, Chosen(session, "opening"));
        Assert.Equal(5, Chosen(session, "hall"));
        Assert.Equal(revision + 1, session.Editor.Revision);   // 함께 한 칸

        window.Close();
    });

    [Fact]
    public void 판의_장면_이름표를_우클릭해도_같은_색_메뉴다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        Border label = graph.FindControl<Canvas>("FrameLabelLayer")!.Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + "opening");
        var top = (Visual)TopLevel.GetTopLevel(label)!;
        label.RaiseEvent(new PointerPressedEventArgs(label, new Pointer(0, PointerType.Mouse, true), top, label.TranslatePoint(new Point(4, 4), top)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        ContextMenu menu = Assert.IsType<ContextMenu>(graph.SceneColorMenuShown);
        MenuItem two = menu.ItemsSource!.OfType<MenuItem>().Single(item => item.Tag as string == SceneColorMenu.ItemTag + "2");
        Click(two);

        Assert.Equal(2, Chosen(session, "opening"));

        window.Close();
    });

    [Fact]
    public void 인스펙터의_장면_색_줄에서_고르면_칠하고_고리가_옮겨_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        Press(window, graph.HierarchyView, StoryHierarchy.SceneKey("ch01", "hall"));

        WrapPanel row = graph.ShownSideHost.GetLogicalDescendants().OfType<WrapPanel>().Single(panel => Equals(panel.Tag, MainWindow.SceneColorRowTag));
        List<Button> buttons = row.Children.OfType<Button>().ToList();
        Assert.Equal(SceneColors.Count + 1, buttons.Count);   // 자동 + 다섯 색
        Button Of(string tag) => buttons.Single(button => button.Tag as string == SceneColorMenu.ItemTag + tag);
        Assert.Equal(Skin.Selection, ((ISolidColorBrush)Of("auto").BorderBrush!).Color);   // 처음은 자동에 고리

        Click(Of("4"));

        Assert.Equal(4, Chosen(session, "hall"));
        Assert.Equal(Skin.Selection, ((ISolidColorBrush)Of("4").BorderBrush!).Color);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)Of("auto").BorderBrush!).Color);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Open()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.HierarchyView.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        return (window, session, graph);
    }

    private static IReadOnlyDictionary<string, int> SceneColorsOf(AuthoringSession session)
    {
        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        return SceneColors.Of(ChapterSceneGrouping.Of(chapter.ToGraphModel("ch01")), chapter.SceneNotes);
    }

    private static int? Chosen(AuthoringSession session, string sceneId) =>
        ChapterSceneNotes.ColorOf(session.Editor.FindChapter("ch01")!.SceneNotes, sceneId);

    /// <summary>그 장면 줄의 우클릭 「장면 색」 아래 항목(칸 번호 또는 auto).</summary>
    private static MenuItem ColorItem(StoryHierarchy tree, string sceneId, string tag)
    {
        ContextMenu menu = tree.RowButton(StoryHierarchy.SceneKey("ch01", sceneId))!.ContextMenu!;
        MenuItem colors = menu.ItemsSource!.OfType<MenuItem>().Single(item => item.Tag as string == "scene-color");
        return colors.ItemsSource!.OfType<MenuItem>().Single(item => item.Tag as string == SceneColorMenu.ItemTag + tag);
    }

    private static void Click(Control control)
    {
        control.RaiseEvent(new RoutedEventArgs(control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>그 줄을 마우스로 누른다 — 보조 키와 함께.</summary>
    private static void Press(MainWindow window, StoryHierarchy tree, string key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Button button = tree.RowButton(key)!;
        button.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        Point center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left, modifiers);
        window.MouseUp(center, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }
}
