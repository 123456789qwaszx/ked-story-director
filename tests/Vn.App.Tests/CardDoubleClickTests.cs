using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>카드를 두 번 누르면 다른 탭의 같은 에피소드로 간다</b> — [구성] 판의 카드는 [대본] 탭의 그 카드로(그 판을 열고 고른다),
/// [대본] 판의 카드는 [구성] 탭의 그 에피소드로(그 챕터를 열고 고른다). 한 번 누르기는 고르기일 뿐이고, [대본] 카드의 글
/// 칸 안에서 두 번 누르는 것은 낱말 고르기라 탭을 바꾸지 않는다.
/// </summary>
public sealed class CardDoubleClickTests : IDisposable
{
    private const int ChapterTab = 0;
    private const int DirectorTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-double-click", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 구성_탭의_카드를_두_번_누르면_대본_탭의_그_카드로_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, ChapterTab);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch02");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();
        Border card = view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(item => item.Tag as string == "y");

        // 한 번 — 고르기만 한다.
        Press(card, clicks: 1);
        Assert.Equal(ChapterTab, Tabs(window).SelectedIndex);

        // 두 번 — [대본] 탭의 그 카드다(그 챕터의 판이 열렸다).
        Press(card, clicks: 2);
        DialogueNode target = EpisodeNaming.CardFor(session.Project, "ch02", "y")!;
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);
        Assert.Equal(target.Id, session.SelectedNodeId);
        Assert.Equal("ch02", session.ActiveFile?.Name);

        window.Close();
    });

    [Fact]
    public void 대본_탭의_카드를_두_번_누르면_구성_탭의_그_에피소드로_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, DirectorTab);
        DialogueNode node = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Border card = graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(item => item.Tag as string == node.Id);

        // 글 칸 안의 두 번 누르기는 낱말 고르기다 — 탭이 그대로다.
        TextBox writing = card.GetVisualDescendants().OfType<TextBox>().First();
        Press(writing, clicks: 2);
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);

        // 한 번 — 고르기만 한다.
        Press(card, clicks: 1);
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);

        // 두 번 — [구성] 탭의 그 에피소드다.
        Press(card, clicks: 2);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(ChapterTab, Tabs(window).SelectedIndex);
        Assert.Equal("b", view.FindControl<TextBox>("IdBox")!.Text);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "b"), view.HierarchyView.Selection?.Key);

        window.Close();
    });

    private static TabControl Tabs(MainWindow window) => window.FindControl<TabControl>("MainTabs")!;

    /// <summary>왼쪽 단추로 누른다 — <paramref name="clicks"/>가 2면 두 번 누름의 둘째 누름이다.</summary>
    private static void Press(Control target, int clicks)
    {
        target.RaiseEvent(new PointerPressedEventArgs(
            target,
            new Pointer(0, PointerType.Mouse, true),
            (Visual)TopLevel.GetTopLevel(target)!,
            target.TranslatePoint(new Point(10, 10), TopLevel.GetTopLevel(target)!) ?? default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None,
            clicks));
        target.RaiseEvent(new PointerReleasedEventArgs(
            target,
            new Pointer(0, PointerType.Mouse, true),
            (Visual)TopLevel.GetTopLevel(target)!,
            target.TranslatePoint(new Point(10, 10), TopLevel.GetTopLevel(target)!) ?? default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }
}
