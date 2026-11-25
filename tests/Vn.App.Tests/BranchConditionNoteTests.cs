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
    private const int GraphTab = MainTab.Script;

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

        Assert.Null(BranchEntryCondition.Set(session.Editor, branch.Id, "신뢰높음"));
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

        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, branch.Id).Label);
        Assert.Equal("⑂ 조건: 신뢰높음", ChipText(window, branch));
        Assert.Contains("조건 '신뢰높음'일 때만 다녀옵니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("신뢰높음", Picked(Picker(window, session, branch)));   // 다시 선 카드도 그 조건을 보인다

        // 「(언제나)」로 돌리면 조건이 걷힌다. 되돌리기 한 칸이다.
        ComboBox again = Picker(window, session, branch);
        again.SelectedItem = again.Items.OfType<ComboBoxItem>().First();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(BranchEntryCondition.Read(session.Project, branch.Id).Label);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, branch.Id).Label);

        // 식 미정인 조건도 건다 — 지금은 늘 다녀온다고 말하고, 카드 머리의 표도 그렇게 적는다.
        ComboBox third = Picker(window, session, branch);
        third.SelectedItem = third.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, "아직모름"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("아직모름", BranchEntryCondition.Read(session.Project, branch.Id).Label);
        Assert.Contains("식 미정이라 지금은 늘 다녀옵니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("⑂ 조건: 아직모름 (식 미정)", ChipText(window, branch));
        Assert.Contains(Texts(Editor(window).BranchConditionHost), text => text.Contains("식 미정이라 지금은 늘 다녀옵니다", StringComparison.Ordinal));
        session.Select(root.Id);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Texts(BranchCard(window, root, branch)), text => text.Contains("식 미정이라 지금은 늘 다녀옵니다", StringComparison.Ordinal));

        // [구성]에서 식을 채우면 그 분기가 그대로 그 식을 따른다 — 표에서 (식 미정)이 걷힌다.
        Assert.StartsWith("<<if true>>", Writing(window, branch), StringComparison.Ordinal);   // 식 미정 — 항상 통과
        session.Editor.UpdateChapterCondition("ch01", "아직모름", "trust >= 7");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("아직모름", BranchEntryCondition.Read(session.Project, branch.Id).Label);
        Assert.Equal("⑂ 조건: 아직모름", ChipText(window, branch));

        // 카드 글 칸의 조건 줄도 바로 그 식이다 — 조건을 다시 고르지 않아도(2026-11-23 소유자 보고 — ⛔ 그 전에는 <<if true>>로 남았다).
        Assert.StartsWith("<<if stat(\"trust\") >= 7>>", Writing(window, branch), StringComparison.Ordinal);
        Assert.Contains(Picker(window, session, branch).Items.OfType<ComboBoxItem>(), item => Equals(item.Content, "아직모름"));   // (식 미정)이 걷혔다

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

    [Fact]
    public void 조건_표가_길어도_카드_제목은_남은_폭의_절반을_갖고_표가_줄어든다() => HeadlessUi.Run(() =>
    {
        // 2026-11-25 영어 화면 E7 — 표가 Auto 칸이라 제 폭을 먼저 가져가, 기본 폭의 분기 카드에서 조건 이름이 길면 제목이 0이 되어 사라졌다
        // (영어는 「⑂ Condition: …」라 조건이 없어도). 이제 둘이 함께 넘치면 제목이 남은 폭의 절반 이상을 갖고 표가 줄임표로 줄어든다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode root = Card(session, "root");
        DialogueNode branch = AddBranchThroughMenu(window, session, root);
        session.Editor.SetBranchCardTitle(branch.Id, "짧게");
        Dispatcher.UIThread.RunJobs();

        // 둘 다 들면 표는 제 폭 그대로다.
        Assert.True(double.IsPositiveInfinity(Chip(window, branch).MaxWidth));

        const string Long = "하린과_아주_가까워졌을_때_열리는_조건";
        session.Editor.SetBranchCardTitle(branch.Id, "들렀다 가는 아주 긴 이름의 길");
        AddCondition(session, Long);
        Assert.Null(BranchEntryCondition.Set(session.Editor, branch.Id, Long));
        session.Select(root.Id);
        Dispatcher.UIThread.RunJobs();

        Border chip = Chip(window, branch);
        var row = Assert.IsType<CardTitleRow>(chip.Parent);
        TextBlock title = row.Children.OfType<TextBlock>().Single(block => block.Tag as string == GraphEditorView.CardTitleTag);
        Control toggles = row.Children.Single(child => Grid.GetColumn(child) == CardTitleRow.TogglesColumn);

        // 견주는 것은 제목이 받은 칸의 폭이다 — 줄임표로 줄인 글자는 칸보다 한 글자쯤 좁게 설 수 있다(글꼴마다 다르다 · Windows CI에서 글 40 · 표 45).
        double titleRoom = row.ColumnDefinitions[CardTitleRow.TitleColumn].ActualWidth;
        Assert.True(titleRoom >= chip.Bounds.Width, $"제목 칸 {titleRoom:0} · 표 {chip.Bounds.Width:0}");
        Assert.True(title.Bounds.Width > 20, $"제목 {title.Bounds.Width:0}");   // 사라지지 않고 몇 글자는 보인다
        Assert.True(chip.Bounds.Width > 0);
        Assert.True(chip.Bounds.Right <= toggles.Bounds.Left + 0.5, "표가 글 맞춤 단추를 덮지 않는다");
        Assert.Equal("⑂ 조건: " + Long, ((TextBlock)chip.Child!).Text);   // 글은 그대로 — 줄임표는 그리기만 줄인다

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
    }

    private static Border Chip(MainWindow window, DialogueNode branch) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!
            .GetVisualDescendants().OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.BranchConditionChipTag + branch.Id);

    private static string? ChipText(MainWindow window, DialogueNode branch) => ((TextBlock)Chip(window, branch).Child!).Text;

    /// <summary>[대본] 판에서 그 카드의 글 칸 글.</summary>
    private static string Writing(MainWindow window, DialogueNode card) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!.GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.Tag as string == GraphEditorView.WritingTag + card.Id).Text ?? string.Empty;

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
