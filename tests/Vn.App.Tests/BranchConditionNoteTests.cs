using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
        Assert.Contains("분기 카드", session.StatusMessage, StringComparison.Ordinal);

        Assert.Equal("⑂ 조건 없음", ChipText(window, branch));
        Assert.Contains(Texts(BranchCard(window, root, branch)), text => text.StartsWith("지금은 조건 없이 늘 다녀옵니다", StringComparison.Ordinal));

        // 부르는 쪽 분기 상자에는 조건 칸이 없다 — 글로만 보인다. 고르는 곳은 분기 카드 위쪽이다(2026-11-17 소유자).
        Assert.Empty(BranchCard(window, root, branch).GetLogicalDescendants().OfType<ComboBox>());
        Assert.Equal(BranchConditionPicker.Always, Picked(Picker(window, session, branch)));

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
        Assert.Equal("신뢰높음", Picked(Picker(window, session, branch)));

        window.Close();
    });

    [Fact]
    public void 분기_카드_위쪽에서_조건을_고르고_식_미정인_조건도_고른다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 분기는 무조건 들른 뒤 그 카드에서 if로 보므로, 분기 카드를 고르면 대본 위쪽에서 조건을 고른다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode root = Card(session, "root");
        DialogueNode branch = AddBranchThroughMenu(window, session, root);
        AddCondition(session, "신뢰높음");
        session.Editor.AddChapterCondition("ch01", "아직모름", string.Empty);
        session.Select(null);
        session.Select(root.Id);
        Dispatcher.UIThread.RunJobs();

        ComboBox picker = Picker(window, session, branch);
        List<ComboBoxItem> items = picker.Items.OfType<ComboBoxItem>().ToList();
        Assert.Equal([BranchConditionPicker.Always, "신뢰높음", "아직모름 (식 미정)"], items.Select(item => item.Content));
        Assert.True(items[2].IsEnabled);

        picker.SelectedItem = items[1];
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);
        Assert.Equal("⑂ 조건: 신뢰높음", ChipText(window, branch));
        Assert.Contains("조건 '신뢰높음'일 때만 다녀옵니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("신뢰높음", Picked(Picker(window, session, branch)));   // 다시 선 카드도 그 조건을 보인다

        // 「(언제나)」로 돌리면 조건이 걷힌다. 되돌리기 한 칸이다.
        ComboBox again = Picker(window, session, branch);
        again.SelectedItem = again.Items.OfType<ComboBoxItem>().First();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);

        // 식 미정인 조건도 건다 — 지금은 늘 다녀온다고 말하고, 카드 머리의 표도 그렇게 적는다.
        ComboBox third = Picker(window, session, branch);
        third.SelectedItem = third.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, "아직모름"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("아직모름", BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);
        Assert.Contains("식 미정이라 지금은 늘 다녀옵니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("⑂ 조건: 아직모름 (식 미정)", ChipText(window, branch));
        Assert.Contains(Texts(Editor(window).BranchConditionHost), text => text.Contains("식 미정이라 지금은 늘 다녀옵니다", StringComparison.Ordinal));
        session.Select(root.Id);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Texts(BranchCard(window, root, branch)), text => text.Contains("식 미정이라 지금은 늘 다녀옵니다", StringComparison.Ordinal));

        // [구성]에서 식을 채우면 그 분기가 그대로 그 식을 따른다 — 표에서 (식 미정)이 걷힌다.
        session.Editor.UpdateChapterCondition("ch01", "아직모름", "trust >= 7");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("아직모름", BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);
        Assert.Equal("⑂ 조건: 아직모름", ChipText(window, branch));

        window.Close();
    });

    [Fact]
    public void 카드_머리의_표를_누르면_그_카드를_고르고_위쪽에_조건_칸이_선다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 조건 칸이 분기 카드 위쪽으로 왔으니 표를 누르면 [구성] 탭으로 가지 않고 그 카드를 고른다(카드를 누른 것과 같다).
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode root = Card(session, "root");
        DialogueNode branch = AddBranchThroughMenu(window, session, root);
        TabControl tabs = window.FindControl<TabControl>("MainTabs")!;

        Border chip = Chip(window, branch);
        Point at = chip.TranslatePoint(new Point(chip.Bounds.Width / 2, chip.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(GraphTab, tabs.SelectedIndex);
        Assert.Equal(branch.Id, session.SelectedNodeId);
        Assert.True(Editor(window).BranchConditionHost.IsVisible);

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
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));
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

    /// <summary>분기 카드를 골라 대본 위쪽의 조건 칸을 집는다.</summary>
    private static ComboBox Picker(MainWindow window, AuthoringSession session, DialogueNode branch)
    {
        if (session.SelectedNodeId != branch.Id)
        {
            session.Select(branch.Id);
            Dispatcher.UIThread.RunJobs();
        }

        return Editor(window).BranchConditionHost.GetLogicalDescendants().OfType<ComboBox>()
            .Single(combo => Equals(combo.Tag, DialogueNodeEditor.BranchConditionTag));
    }

    private static object? Picked(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag ?? BranchConditionPicker.Always;

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
