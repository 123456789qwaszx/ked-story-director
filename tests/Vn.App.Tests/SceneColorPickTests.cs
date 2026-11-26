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
using Vn.App.Strings;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

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
        Assert.Equal(SceneColors.Count + 2, buttons.Count);   // 자동 + 다섯 색 + 직접 고르기(RGB)
        Button Of(string tag) => buttons.Single(button => button.Tag as string == SceneColorMenu.ItemTag + tag);
        Assert.Equal(Skin.Selection, ((ISolidColorBrush)Of("auto").BorderBrush!).Color);   // 처음은 자동에 고리

        Click(Of("4"));

        Assert.Equal(4, Chosen(session, "hall"));
        Assert.Equal(Skin.Selection, ((ISolidColorBrush)Of("4").BorderBrush!).Color);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)Of("auto").BorderBrush!).Color);

        window.Close();
    });

    [Fact]
    public void 인스펙터의_직접_고르기는_지금_색에서_열리고_고른_RGB를_칠하며_다시_열면_쓴_색의_맨_앞이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 「장면색을 원한다면, RGB 표에서 선택도 할 수 있게 해줄래? 그게 최근 사용한 항목에 반영이 되도록 해주고, 또 특정 장면색을 선택한
        // 상태에서 열면 그 색이 선택된 상태로 RGB가 열리도록」. 서식 도구 · 화자 이름 색과 같은 색 차림이다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.SetSceneColor("ch01", "hall", 3);
        Press(window, graph.HierarchyView, StoryHierarchy.SceneKey("ch01", "hall"));

        WrapPanel row = graph.ShownSideHost.GetLogicalDescendants().OfType<WrapPanel>().Single(panel => Equals(panel.Tag, MainWindow.SceneColorRowTag));
        Button rgb = row.Children.OfType<Button>().Single(button => button.Tag as string == SceneColorMenu.ItemTag + "rgb");

        // 고른 칸(3)의 색에서 시작한다.
        rgb.Flyout!.ShowAt(rgb);
        Dispatcher.UIThread.RunJobs();
        WriterColorChooser chooser = SceneColorMenu.LastChooser!;
        Assert.Equal(SceneColorMenu.RgbOf(3).Hex, chooser.Hex.Text);

        chooser.Hex.Text = "#3366CC";
        Dispatcher.UIThread.RunJobs();
        Click(chooser.Apply);

        int blue = SceneColors.Custom(0x3366CC);
        Assert.Equal(blue, Chosen(session, "hall"));
        Assert.Contains("#3366CC", session.StatusMessage, StringComparison.Ordinal);

        // 고른 RGB의 단추가 다섯 색 뒤에 서고 고리가 그것에 간다.
        Button custom = row.Children.OfType<Button>().Single(button => button.Tag as string == SceneColorMenu.ItemTag + "custom");
        Assert.Equal(Skin.Selection, ((ISolidColorBrush)custom.BorderBrush!).Color);
        Assert.Equal(row.Children.IndexOf(rgb) - 1, row.Children.IndexOf(custom));

        // 다시 열면 — 직접 고르기는 그 색에서, 「이 프로젝트에 쓴 색」의 맨 앞이 그 색이다.
        rgb.Flyout!.ShowAt(rgb);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("#3366CC", chooser.Hex.Text);
        Assert.True(chooser.UsedTitle.IsVisible);
        Assert.Equal(new WriterRgb(0x33, 0x66, 0xCC), chooser.Used.Children.OfType<Button>().First().Tag);

        // 「자동」은 고른 색을 걷는다.
        Click(chooser.Content.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, Ui.SceneColor_Auto)));
        Assert.Null(Chosen(session, "hall"));

        window.Close();
    });

    [Fact]
    public void 장면_줄_우클릭에도_직접_고르기가_있고_고른_RGB는_메뉴에_한_줄로_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        int now = SceneColorsOf(session)["hall"];

        Click(ColorItem(graph.HierarchyView, "hall", "rgb"));
        Dispatcher.UIThread.RunJobs();
        WriterColorChooser chooser = SceneColorMenu.LastChooser!;
        Assert.Equal(SceneColorMenu.RgbOf(now).Hex, chooser.Hex.Text);   // 자동 색에서 시작한다

        chooser.Hex.Text = "#8A2BE2";
        Dispatcher.UIThread.RunJobs();
        Click(chooser.Apply);

        int violet = SceneColors.Custom(0x8A2BE2);
        Assert.Equal(violet, Chosen(session, "hall"));

        // 다시 세운 줄의 점이 그 색(점 색)이고, 메뉴에 그 색이 ✓로 한 줄 선다.
        var line = (StackPanel)graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!.Content!;
        Assert.Equal(Skin.SceneMarkOf(violet), ((ISolidColorBrush)((Avalonia.Controls.Shapes.Ellipse)line.Children[0]).Fill!).Color);
        Assert.Equal("#8A2BE2  ✓", (string)ColorItem(graph.HierarchyView, "hall", "custom").Header!);

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
