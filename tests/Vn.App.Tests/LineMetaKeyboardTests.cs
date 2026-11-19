using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
/// <b>대사 편집기의 줄 ＋ 차림 · 키보드로 줄 쓰기</b> (2026-11-19 소유자 · usability UX26) — 차림에서 「출구(이 갈래 끝에서 점프)」를 걷고 맨 아래에
/// [아래에 줄 추가]를 둔다. ＋를 누르고 Tab이면 그 단추로 가고, 누르면 새 줄이 서서 그 줄의 화자 칸에서 바로 쓴다. Ctrl+Tab은 앞 칸으로 —
/// 마우스 없이 이전 줄로 돌아간다.
/// </summary>
public sealed class LineMetaKeyboardTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-line-meta-keys", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 줄의_차림에_출구가_없고_맨_아래에_아래에_줄_추가가_있다() => HeadlessUi.Run(() =>
    {
        // 조건을 건 분기 카드의 그 조건 안 마지막 줄 — ⛔ 여기에 「출구 (이 갈래 끝에서 점프)」와 대상 노드 고르기가 섰다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        DialogueNode branch = GatedBranch(session);
        session.Select(branch.Id);
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = OpenPlus(window, row: 0);

        Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<TextBlock>(), block => block.Text?.Contains("출구", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<ComboBox>(), _ => true);
        Button last = Assert.IsType<Button>(panel.Children[^1]);
        Assert.Equal(DialogueNodeEditor.LineMetaAddTag, last.Tag);

        window.Close();
    });

    [Fact]
    public void 플러스를_누르고_Tab이면_줄_추가로_가고_Enter면_아래에_새_줄이_서서_그_줄의_화자_칸에서_쓴다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        string root = Card(session, "root");
        session.Editor.ApplyScenarioText(root, "라루: 첫 줄.\n윌로: 둘째 줄.");
        session.Select(root);
        Dispatcher.UIThread.RunJobs();

        // 첫 줄의 ＋를 눌러 차림을 연다(초점은 차림 안의 첫 단추로 든다).
        Button plus = Pluses(window)[0];
        plus.Focus();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Editor(window).LineMetaPanel);

        // Tab — [아래에 줄 추가]로.
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        Button add = Assert.IsType<Button>(Focused(window));
        Assert.Equal(DialogueNodeEditor.LineMetaAddTag, add.Tag);

        // Enter — 첫 줄 바로 아래에 새 줄이 서고, 그 줄의 화자 칸에 초점이 있다(곧바로 친다).
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        DialogueNode node = session.Project.FindDialogue(root)!;
        Assert.Equal(3, node.Script.ActiveLines.Count());
        string inserted = node.Script.ActiveLines.ElementAt(1).Id;
        Assert.Equal(string.Empty, Vn.Authoring.Rendering.WorkingDialoguePreview.ScenarioWriting(session.Project, root).Text.Split('\n')[1].Trim());
        AutoCompleteBox speaker = Speakers(window)[1];
        Assert.Contains(speaker, ((Visual)Focused(window)!).GetSelfAndVisualAncestors());
        Assert.Equal(1, node.Script.ActiveLines.ToList().FindIndex(line => line.Id == inserted));

        window.Close();
    });

    [Fact]
    public void 위_줄_추가도_새_줄의_화자_칸으로_간다() => HeadlessUi.Run(() =>
    {
        // 「'줄 추가'를 누르면 바로 타이핑할 수 있습니다」 — ⛔ 안내만 그랬고 초점이 가지 않았다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        string root = Card(session, "root");
        session.Editor.ApplyScenarioText(root, "라루: 첫 줄.");
        session.Select(root);
        Dispatcher.UIThread.RunJobs();

        Editor(window).FindControl<Button>("AddLineButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Speakers(window)[1], ((Visual)Focused(window)!).GetSelfAndVisualAncestors());

        window.Close();
    });

    [Fact]
    public void Ctrl_Tab이면_앞_칸으로_가_이전_줄로_돌아간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        string root = Card(session, "root");
        session.Editor.ApplyScenarioText(root, "라루: 첫 줄.\n윌로: 둘째 줄.");
        session.Select(root);
        Dispatcher.UIThread.RunJobs();

        // 둘째 줄의 대사 칸에서 — Ctrl+Tab은 Shift+Tab과 같은 앞 칸이다.
        TextBox second = Lines(window)[1];
        second.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        IInputElement? byShift = Focused(window);

        second.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Tab, RawInputModifiers.Control, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        Assert.NotSame(second, Focused(window));
        Assert.Same(byShift, Focused(window));

        // 몇 번 더 — 첫 줄의 대사 칸에 닿는다(둘째 줄의 ▾ · 화자, 첫 줄의 ＋를 지나).
        for (int step = 0; step < 6 && !ReferenceEquals(Focused(window), Lines(window)[0]); step++)
        {
            window.KeyPress(Key.Tab, RawInputModifiers.Control, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Same(Lines(window)[0], Focused(window));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static string Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static DialogueNodeEditor Editor(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();

    private static IInputElement? Focused(MainWindow window) => window.FocusManager?.GetFocusedElement();

    private static List<Button> Pluses(MainWindow window) =>
        Editor(window).LineHost.GetVisualDescendants().OfType<Button>().Where(button => Equals(button.Content, "＋")).ToList();

    private static List<AutoCompleteBox> Speakers(MainWindow window) =>
        Editor(window).LineHost.GetVisualDescendants().OfType<AutoCompleteBox>().ToList();

    /// <summary>줄마다의 대사 칸(화자 칸 안의 글 칸은 빼고).</summary>
    private static List<TextBox> Lines(MainWindow window) =>
        Editor(window).LineHost.GetVisualDescendants().OfType<TextBox>()
            .Where(box => box.PlaceholderText == "대사" && box.FindAncestorOfType<AutoCompleteBox>() is null)
            .ToList();

    private static StackPanel OpenPlus(MainWindow window, int row)
    {
        Pluses(window)[row].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        return Editor(window).LineMetaPanel!;
    }

    /// <summary>root 첫 줄 앞에 분기 카드를 세우고, 그 카드에 대사 한 줄 · 조건(「신뢰높음」)을 건다 — 조건 안의 마지막 줄이 생긴다.</summary>
    private static DialogueNode GatedBranch(AuthoringSession session)
    {
        DialogueNode root = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        Assert.True(session.Editor.ApplyScenarioText(root.Id, "하린: 첫 줄").Applied);
        DialogueNode branch = session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        session.Editor.SetScriptLineText(branch.Id, branch.Script.ActiveLines.First().Id, "안녕", "오랜만이네.");

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        Assert.Null(BranchEntryCondition.Set(session.Editor, branch.Id, "신뢰높음"));
        Dispatcher.UIThread.RunJobs();
        return (DialogueNode)session.Project.FindNode(branch.Id)!;
    }
}
