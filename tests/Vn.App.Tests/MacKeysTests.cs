using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>맥의 손</b> (2026-11-19 소유자 — 「맥을 쓰는 사람이 있을 수도 있으니까 … 가볍게 최소로 지원」 · writer-app WA1).
///
/// 맥에서는 ⌘를 Ctrl처럼 듣고(Ctrl도 그대로), ⌘⇧Z가 다시 실행이고, ⌫도 고른 카드를 지운다. 맥이 아니면 하나도 달라지지 않는다 —
/// 그래서 시험마다 두 쪽을 함께 본다. 맥인지는 <see cref="PlatformKeys.IsMac"/>가 정하고, 시험은 제 스코프에서 바꾸고 되돌린다.
/// </summary>
public sealed class MacKeysTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-mac-keys", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 맥에서는_커맨드_Z가_되돌리고_커맨드_시프트_Z와_커맨드_Y가_다시_실행한다() => OnMac(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory);
        session.Editor.AddEpisode("ch01", "c", title: "c", 660, 0, sceneId: "hall");
        Assert.Equal(4, Chapter(session).Episodes.Count);

        Press(window, Key.Z, KeyModifiers.Meta);
        Assert.Equal(3, Chapter(session).Episodes.Count);

        Press(window, Key.Z, KeyModifiers.Meta | KeyModifiers.Shift);
        Assert.Equal(4, Chapter(session).Episodes.Count);

        Press(window, Key.Z, KeyModifiers.Meta);
        Press(window, Key.Y, KeyModifiers.Meta);
        Assert.Equal(4, Chapter(session).Episodes.Count);

        // Ctrl도 그대로 듣는다 — 화면의 「되돌리기(Ctrl+Z)」가 맥에서도 참이다.
        Press(window, Key.Z, KeyModifiers.Control);
        Assert.Equal(3, Chapter(session).Episodes.Count);

        window.Close();
    });

    [Fact]
    public void 맥이_아니면_커맨드는_단축키가_아니고_Ctrl_Shift_Z는_되돌리기_그대로다() => HeadlessUi.Run(() =>
    {
        Assert.False(PlatformKeys.IsMac);   // 시험 기계는 맥이 아니다 — 맥에서 돌리면 이 시험이 맥의 규칙을 본다
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory);
        session.Editor.AddEpisode("ch01", "c", title: "c", 660, 0, sceneId: "hall");
        session.Editor.AddEpisode("ch01", "d", title: "d", 880, 0, sceneId: "hall");

        // Windows 키(Meta)+Z · S는 이 창의 것이 아니다.
        Press(window, Key.Z, KeyModifiers.Meta);
        Press(window, Key.S, KeyModifiers.Meta);
        Assert.Equal(5, Chapter(session).Episodes.Count);
        Assert.True(session.IsDirty);

        // Ctrl+Shift+Z는 되돌리기다(2026-11-18 소유자 「그대로 둔다」 · 7절 AU) — 맥의 ⌘⇧Z(다시 실행)가 이것을 바꾸지 않는다.
        Press(window, Key.Z, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal(4, Chapter(session).Episodes.Count);

        window.Close();
    });

    [Fact]
    public void 맥에서는_커맨드_S가_저장한다() => OnMac(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory);
        session.Editor.AddEpisode("ch01", "c", title: "c", 660, 0, sceneId: "hall");
        Assert.True(session.IsDirty);

        Press(window, Key.S, KeyModifiers.Meta);

        Assert.False(session.IsDirty);

        window.Close();
    });

    [Fact]
    public void 맥에서는_백스페이스도_고른_카드를_지우고_글_칸에서는_글자_지우기다() => OnMac(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        StoryHierarchy tree = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);

        // 글 칸에 초점 — 거기서 ⌫는 글자 지우기다.
        Pick(tree, StoryHierarchy.EpisodeKey("ch01", "a"));
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().First().FindControl<TextBox>("NameBox")!.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, Chapter(session).Episodes.Count);

        // 글 칸 밖 · 에피소드를 고른 채 — 지운다(Delete와 같다: 묻지 않고 한 칸).
        Pick(tree, StoryHierarchy.EpisodeKey("ch01", "a"));
        long before = session.Editor.Revision;
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["root", "b"], Chapter(session).Episodes.Select(episode => episode.EpisodeId));
        Assert.Equal(before + 1, session.Editor.Revision);

        window.Close();
    });

    [Fact]
    public void 맥이_아니면_백스페이스는_카드를_지우지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        StoryHierarchy tree = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);

        Pick(tree, StoryHierarchy.EpisodeKey("ch01", "a"));
        long before = session.Editor.Revision;
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, Chapter(session).Episodes.Count);
        Assert.Equal(before, session.Editor.Revision);

        window.Close();
    });

    [Fact]
    public void 맥에서는_장면_메모를_커맨드_Enter로도_넣는다() => OnMac(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Pick(graph.HierarchyView, StoryHierarchy.SceneKey("ch01", "opening"));

        TextBox note = graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<TextBox>().Distinct()
            .Single(box => box.IsEffectivelyVisible && (string?)box.Tag == "scene-note");
        note.Text = "마감 전날.";
        note.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Enter,
            KeyModifiers = KeyModifiers.Meta,
            Source = note
        });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("마감 전날.", ChapterSceneNotes.TextOf(Chapter(session).SceneNotes, "opening"));

        window.Close();
    });

    [Fact]
    public void File_메뉴의_단축키_글은_맥에서_커맨드이고_그_밖에서는_Ctrl이다()
    {
        HeadlessUi.Run(() =>
        {
            var window = new MainWindow();

            Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Control), Item(window, "SaveMenuItem").InputGesture);
            Assert.Equal(new KeyGesture(Key.Z, KeyModifiers.Control), Item(window, "UndoMenuItem").InputGesture);
            Assert.Equal(new KeyGesture(Key.Y, KeyModifiers.Control), Item(window, "RedoMenuItem").InputGesture);
        });

        OnMac(() =>
        {
            var window = new MainWindow();

            Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Meta), Item(window, "SaveMenuItem").InputGesture);
            Assert.Equal(new KeyGesture(Key.Z, KeyModifiers.Meta), Item(window, "UndoMenuItem").InputGesture);
            Assert.Equal(new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift), Item(window, "RedoMenuItem").InputGesture);
        });
    }

    [Fact]
    public void 단축키_표의_글은_맥에서_커맨드와_백스페이스로_보이고_그_밖에서는_그대로다()
    {
        Assert.Equal("Ctrl+S", PlatformKeys.Label("Ctrl+S"));
        Assert.Equal("Delete", PlatformKeys.Label("Delete"));

        OnMac(() =>
        {
            Assert.Equal("⌘S", PlatformKeys.Label("Ctrl+S"));
            Assert.Equal("⌘Z · ⌘⇧Z", PlatformKeys.Label("Ctrl+Z · Ctrl+Y"));
            Assert.Equal("⌘Enter", PlatformKeys.Label("Ctrl+Enter"));
            Assert.Equal("⌫ (delete)", PlatformKeys.Label("Delete"));
            Assert.Equal("마우스 휠", PlatformKeys.Label("마우스 휠"));

            // 설정 창의 표가 그 글로 선다.
            (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory);
            var settings = new SettingsWindow(session);
            settings.Show(window);
            settings.ShortcutGroup.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();

            List<string?> keys = settings.ShortcutGroup.GetLogicalDescendants().OfType<Grid>()
                .Where(grid => (grid.Tag as string)?.StartsWith("shortcuts:", StringComparison.Ordinal) == true)
                .SelectMany(grid => grid.Children.OfType<TextBlock>().Where(block => Grid.GetColumn(block) == 0))
                .Select(block => block.Text).ToList();

            Assert.Contains("⌘S", keys);
            Assert.Contains("⌫ (delete)", keys);
            Assert.DoesNotContain(keys, text => text!.Contains("Ctrl", StringComparison.Ordinal));

            settings.Close();
            window.Close();
        });
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>맥인 것처럼 본문을 돌리고 되돌린다 — UI 스레드 하나에서 차례로 도니 다른 시험으로 새지 않는다.</summary>
    private static void OnMac(Action body) => HeadlessUi.Run(() =>
    {
        bool was = PlatformKeys.IsMac;
        PlatformKeys.IsMac = true;

        try
        {
            body();
        }
        finally
        {
            PlatformKeys.IsMac = was;
        }
    });

    private static ChapterDocument Chapter(AuthoringSession session) => session.Editor.FindChapter("ch01")!;

    private static MenuItem Item(MainWindow window, string name) => window.FindControl<MenuItem>(name)!;

    private static void Pick(StoryHierarchy tree, string key)
    {
        tree.RowButton(key)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>창이 Tunnel로 받는 그 키 — 어디에 포커스가 있어도 단축키는 창이 먼저 본다.</summary>
    private static void Press(Window window, Key key, KeyModifiers modifiers)
    {
        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers
        });
        Dispatcher.UIThread.RunJobs();
    }
}
