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
/// <b>카드 · 선택지 간선을 두 번 누르면 다른 탭의 같은 것으로 간다</b>. 선택지 간선은 [구성]의 간선 ↔ [대본]의 선택지 레인이다.
/// [구성] 판의 카드를 두 번 누르면 [대본] 탭의 그 카드로 간다(그 판을 열고 고른다). <b>[대본] 판의 카드는 두 번 누르면 그 자리에서 제목을 고치고</b>,
/// [구성] 탭의 그 에피소드로는 <b>우클릭 차림표의 「[구성] 탭에서 보기」</b>로 간다(2026-11-25 소유자 — 「구성탭에서 대본탭으로 오는 경우는 흔한데, 반대로
/// … 드물어. 오히려 에피소드의 제목을 바꾸려고 하거나 … 원하지 않게 구성탭으로 이동이 되버리는 경우가 많더라고」 · ⛔ 그 전에는 두 번 누르기가 데려갔다).
/// 한 번 누르기는 고르기일 뿐이고, [대본] 카드의 글 칸 안에서 두 번 누르는 것은 낱말 고르기다.
/// </summary>
public sealed class CardDoubleClickTests : IDisposable
{
    private const int ChapterTab = MainTab.Structure;
    private const int DirectorTab = MainTab.Script;

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
    public void 대본_탭의_카드를_두_번_누르면_그_자리에서_제목을_고치고_탭은_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, DirectorTab);
        DialogueNode node = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Border card = CardOf(graph, node.Id);

        // 글 칸 안의 두 번 누르기는 낱말 고르기다 — 제목 칸이 서지 않는다.
        TextBox writing = card.GetVisualDescendants().OfType<TextBox>().First();
        Press(writing, clicks: 2);
        Assert.False(graph.IsRenamingCard);

        // 한 번 — 고르기만 한다.
        Press(card, clicks: 1);
        Assert.False(graph.IsRenamingCard);

        // 두 번 — 제목 칸이 서고 지금 제목이 골라져 있다. 탭은 그대로다.
        Press(card, clicks: 2);
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);
        TextBox title = TitleBox(card);
        Assert.True(title.IsFocused);
        Assert.Equal(EpisodeNaming.ShownNameOf(session.Project, node), title.Text);
        Assert.Equal(title.Text!.Length, title.SelectionEnd - title.SelectionStart);

        // Enter — 바꾼다. ID는 그대로고 카드 · 인스펙터의 이름 칸이 따라온다. 되돌리기 한 칸이다.
        title.Text = "  새 제목 ";
        Key(title, Avalonia.Input.Key.Enter);
        Assert.False(graph.IsRenamingCard);
        Assert.Equal("새 제목", EpisodeNaming.ShownNameOf(session.Project, session.Project.FindDialogue(node.Id)!));
        Assert.Equal("b", EpisodeNaming.EpisodeIdOf(session.Project.FindDialogue(node.Id)!));
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Tag as string == GraphEditorView.CardTitleTag && block.Text == "새 제목" && block.IsVisible);
        Assert.Equal("새 제목", InspectorName(window, node.Id).Text);

        // Esc — 그만둔다.
        Press(CardOf(graph, node.Id), clicks: 2);
        TextBox again = TitleBox(CardOf(graph, node.Id));
        again.Text = "버릴 제목";
        Key(again, Avalonia.Input.Key.Escape);
        Assert.False(graph.IsRenamingCard);
        Assert.Equal("새 제목", EpisodeNaming.ShownNameOf(session.Project, session.Project.FindDialogue(node.Id)!));

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(EpisodeNaming.ShownNameOf(session.Project, session.Project.FindDialogue(node.Id)!), InspectorName(window, node.Id).Text);
        Assert.NotEqual("새 제목", InspectorName(window, node.Id).Text);
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);

        window.Close();
    });

    [Fact]
    public void 대본_탭의_카드를_우클릭하면_구성_탭에서_보기로_그_에피소드에_가고_제목_고치기도_있다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, DirectorTab);
        DialogueNode node = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;

        // 「제목 고치기」 — 두 번 누르기와 같은 칸이 선다.
        RightPress(CardOf(graph, node.Id));
        Assert.Equal(node.Id, session.SelectedNodeId);
        Assert.Equal(["제목 고치기", "[구성] 탭에서 보기"], MenuItems(graph).Select(item => item.Header as string));
        MenuItems(graph)[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.True(graph.IsRenamingCard);
        Key(TitleBox(CardOf(graph, node.Id)), Avalonia.Input.Key.Escape);

        // 「[구성] 탭에서 보기」 — [구성] 탭의 그 에피소드다.
        RightPress(CardOf(graph, node.Id));
        MenuItems(graph)[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(ChapterTab, Tabs(window).SelectedIndex);
        Assert.Equal("b", view.FindControl<TextBox>("IdBox")!.Text);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "b"), view.HierarchyView.Selection?.Key);

        window.Close();
    });

    [Fact]
    public void 구성_탭의_분기_칩을_두_번_누르면_대본_탭의_그_분기_카드로_간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 보고 — 칩은 두 번 눌러도 [대본]으로 가지 않았다. 에피소드 카드와 같게 간다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, ChapterTab);
        DialogueNode home = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        DialogueNode branch = session.Editor.AddBranchMarker(home.Id, home.Script.ActiveLines.First().Id);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();
        Border chip = view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(item => item.Tag is ChapterGraphView.BranchCardMark mark && mark.NodeId == branch.Id);

        // 한 번 — 고르기만 한다.
        Press(chip, clicks: 1);
        Assert.Equal(ChapterTab, Tabs(window).SelectedIndex);
        Assert.True(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);

        // 두 번 — [대본] 탭의 그 분기 카드다(그 챕터의 판이 열렸다).
        Press(chip, clicks: 2);
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);
        Assert.Equal(branch.Id, session.SelectedNodeId);
        Assert.Equal("ch01", session.ActiveFile?.Name);

        window.Close();
    });

    [Fact]
    public void 대본_탭의_분기_카드는_우클릭_구성_탭에서_보기로_그_칩에_가고_두_번_누르면_제목을_고친다() => HeadlessUi.Run(() =>
    {
        // ⛔ 2026-11-17까지는 집 에피소드를 골랐다 — 이제 그 분기 카드의 칩을 고른다. ⛔ 2026-11-25까지는 두 번 누르기가 데려갔다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, DirectorTab);
        DialogueNode home = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        DialogueNode branch = session.Editor.AddBranchMarker(home.Id, home.Script.ActiveLines.First().Id);
        Dispatcher.UIThread.RunJobs();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Border card = CardOf(graph, branch.Id);

        // 두 번 — 분기 카드의 보이는 제목을 고친다(ID는 그대로).
        Press(card, clicks: 2);
        TextBox title = TitleBox(card);
        title.Text = "들른 곳";
        Key(title, Avalonia.Input.Key.Enter);
        Assert.Equal("들른 곳", session.Project.FindDialogue(branch.Id)!.Title);
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);

        RightPress(CardOf(graph, branch.Id));
        MenuItems(graph)[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(ChapterTab, Tabs(window).SelectedIndex);
        Assert.True(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);
        Assert.Equal(branch.Name, view.FindControl<TextBox>("BranchIdBox")!.Text);
        Assert.Equal(StoryHierarchy.BranchKey("ch01", branch.Id), view.HierarchyView.Selection?.Key);

        window.Close();
    });

    [Fact]
    public void 구성_탭의_선택지_간선을_두_번_누르면_대본_탭의_그_레인으로_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, ChapterTab);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        Dispatcher.UIThread.RunJobs();
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();
        Border label = view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(item => item.Tag is ChapterGraphView.EdgeLabelMark { FromEpisodeId: "root", ToEpisodeId: "a" });

        // 한 번 — 그 간선을 고르기만 한다.
        Press(label, clicks: 1);
        Assert.Equal(ChapterTab, Tabs(window).SelectedIndex);
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        // 두 번 — [대본] 탭에서 그 선택지 레인을 골랐다(인스펙터가 그 선을 설명한다).
        Press(label, clicks: 2);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);
        Assert.Equal("ch01", session.ActiveFile?.Name);
        Assert.StartsWith("선택지 · ", graph.FindControl<TextBlock>("InspectorTitle")!.Text, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 대본_탭의_선택지_레인을_두_번_누르면_구성_탭의_그_간선으로_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, DirectorTab);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        Dispatcher.UIThread.RunJobs();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;

        // 선택지 레인의 누르는 자리 — 투명한 넓은 선이 그 레인의 열쇠(챕터 · 출발 · 도착 · 문구)를 진다.
        Control rail = graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Avalonia.Controls.Shapes.Path>()
            .Single(path => path.Tag?.GetType().Name == "RailKey" && path.Tag.ToString()!.Contains("From = root", StringComparison.Ordinal));

        Press(rail, clicks: 1);
        Assert.Equal(DirectorTab, Tabs(window).SelectedIndex);

        Press(rail, clicks: 2);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(ChapterTab, Tabs(window).SelectedIndex);
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.Equal("선택지 · 가자", view.FindControl<TextBlock>("InspectorTitle")!.Text);   // [구성] 머리글은 문구다

        window.Close();
    });

    private static TabControl Tabs(MainWindow window) => window.FindControl<TabControl>("MainTabs")!;

    private static Border CardOf(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(item => item.Tag as string == nodeId);

    private static TextBox TitleBox(Border card) =>
        card.GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.CardTitleEditTag);

    private static List<MenuItem> MenuItems(GraphEditorView graph) =>
        ((IEnumerable<object>)graph.BoardMenu!.ItemsSource!).OfType<MenuItem>().ToList();

    /// <summary>오른쪽 [대본] 기둥의 그 카드의 이름 칸(대사 노드 편집기).</summary>
    private static TextBox InspectorName(MainWindow window, string nodeId) =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(window).OfType<DialogueNodeEditor>().Distinct()
            .Single(editor => editor.IsVisible && editor.NodeId == nodeId)
            .FindControl<TextBox>("NameBox")!;

    private static void Key(Control target, Avalonia.Input.Key key)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>오른쪽 단추로 누른다.</summary>
    private static void RightPress(Control target)
    {
        target.RaiseEvent(new PointerPressedEventArgs(
            target,
            new Pointer(0, PointerType.Mouse, true),
            (Visual)TopLevel.GetTopLevel(target)!,
            target.TranslatePoint(new Point(10, 10), TopLevel.GetTopLevel(target)!) ?? default,
            0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
            KeyModifiers.None,
            1));
        Dispatcher.UIThread.RunJobs();
    }

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
