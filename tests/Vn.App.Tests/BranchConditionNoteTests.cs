using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>분기 조건이 연출 그래프에서 보인다</b> — 조건은 챕터 그래프의 분기 선에서 정하지만, 연출 그래프에서 안 보이면 조건 없는 분기와
/// 조건 걸린 분기가 똑같아 보여 헷갈린다. 분기를 만든 순간 「지금은 조건 없이 늘 다녀옵니다 · 조건은 챕터 그래프에서」를 말하고,
/// 다녀오는 카드 머리에 지금 조건을 늘 보이며, 누르면 챕터 그래프의 그 분기로 데려간다.
/// </summary>
public sealed class BranchConditionNoteTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-branch-condition-note", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 분기를_더하면_조건_없이_늘_다녀온다고_말하고_카드와_인스펙터가_그것을_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode root = Card(session, "root");

        DialogueNode branch = AddBranchThroughMenu(window, session, root);

        Assert.Contains("조건 없이 늘 다녀옵니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("[구성] 탭", session.StatusMessage, StringComparison.Ordinal);

        Assert.Equal("⑂ 조건 없음", ChipText(window, branch));
        Assert.Contains(Texts(BranchCard(window, root, branch)), text => text.StartsWith("지금은 조건 없이 늘 다녀옵니다", StringComparison.Ordinal));
        Assert.NotNull(OpenButton(window, root, branch));

        window.Close();
    });

    [Fact]
    public void 조건을_걸면_카드의_표와_인스펙터가_그_조건을_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode root = Card(session, "root");
        DialogueNode branch = AddBranchThroughMenu(window, session, root);
        AddCondition(session, "신뢰높음");

        Assert.Null(BranchEntryCondition.Set(session.Editor, branch.Id, "신뢰높음", session.Definition));
        session.Select(root.Id);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("⑂ 조건: 신뢰높음", ChipText(window, branch));
        Assert.Contains(Texts(BranchCard(window, root, branch)), text => text.StartsWith("조건 「신뢰높음」일 때만 재생합니다", StringComparison.Ordinal));
        Assert.Equal("[구성] 탭에서 조건 바꾸기", OpenButton(window, root, branch)!.Content);

        window.Close();
    });

    [Fact]
    public void 표나_인스펙터_단추를_누르면_챕터_그래프의_그_분기_조건_칸이_열린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode root = Card(session, "root");
        DialogueNode branch = AddBranchThroughMenu(window, session, root);
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        TabControl tabs = window.FindControl<TabControl>("MainTabs")!;

        // 연출 그래프 카드의 표.
        Press(Chip(window, branch));

        Assert.Equal(0, tabs.SelectedIndex);
        Assert.True(chapterGraph.FindControl<Control>("BranchPanel")!.IsVisible);
        Assert.True(chapterGraph.FindControl<ComboBox>("BranchConditionCombo")!.IsEnabled);

        // 인스펙터의 단추도 같은 자리로 간다.
        tabs.SelectedIndex = GraphTab;
        session.Select(root.Id);
        Dispatcher.UIThread.RunJobs();
        chapterGraph.SelectBranch(null);

        OpenButton(window, root, branch)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, tabs.SelectedIndex);
        Assert.True(chapterGraph.FindControl<Control>("BranchPanel")!.IsVisible);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    private static DialogueNodeEditor Editor(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();

    /// <summary>사람이 하는 길 그대로 — 카드를 고르고, 첫 줄의 ＋를 열고, 「＋ 분기 추가」를 누른다.</summary>
    private static DialogueNode AddBranchThroughMenu(MainWindow window, AuthoringSession session, DialogueNode caller)
    {
        session.Select(caller.Id);
        Dispatcher.UIThread.RunJobs();

        DialogueNodeEditor editor = Editor(window);
        Button plus = editor.LineHost.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "＋"));
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Button add = editor.LineMetaPanel!.Children.OfType<Button>().Single(button => Equals(button.Content, "＋ 분기 추가"));
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        string lineId = caller.Script.ActiveLines.First().Id;
        string targetId = caller.FindExtension(lineId)!.DetourTargetNodeIds.Single();
        return (DialogueNode)session.Project.FindNode(targetId)!;
    }

    private static void AddCondition(AuthoringSession session, string label)
    {
        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        chapter.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
        session.Editor.AddChapterCondition("ch01", label, "trust >= 3");
        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            session.Editor, EpisodeNaming.BoardOf(session.Project, "ch01")!.Id, chapter.ToGraphModel("ch01"));
    }

    private static Border Chip(MainWindow window, DialogueNode branch) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!
            .GetVisualDescendants().OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.BranchConditionChipTag + branch.Id);

    private static string? ChipText(MainWindow window, DialogueNode branch) => ((TextBlock)Chip(window, branch).Child!).Text;

    private static Control BranchCard(MainWindow window, DialogueNode caller, DialogueNode branch) =>
        Editor(window).LineHost.Children.Single(child =>
            Equals(child.Tag, DialogueNodeEditor.BranchMarkerTag(caller.Script.ActiveLines.First().Id, branch.Id)));

    private static Button? OpenButton(MainWindow window, DialogueNode caller, DialogueNode branch) =>
        BranchCard(window, caller, branch).GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
            Equals(button.Tag, DialogueNodeEditor.BranchConditionTag(caller.Script.ActiveLines.First().Id, branch.Id)));

    private static IEnumerable<string> Texts(Control card) =>
        card.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty);

    private static void Press(Control target)
    {
        target.RaiseEvent(new PointerPressedEventArgs(
            target, new Pointer(0, PointerType.Mouse, true), target, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }
}
