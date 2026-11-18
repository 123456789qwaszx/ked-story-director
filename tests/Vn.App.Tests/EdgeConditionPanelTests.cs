using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 간선 칸에서 수치를 잡는다</b> (2026-11-17 소유자). 관문 콤보 바로 아래에 고른 조건의 <b>식 칸</b>이 서서 수치를 그 자리에서
/// 고치고, [＋ 새 조건]으로 이름과 식을 한 번에 만들어 건다(조건 이름은 [대본]에서 — W4의 예외). 잠금 안내문은 해금조건을 걸었을 때만 선다.
/// </summary>
public sealed class EdgeConditionPanelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-edge-condition", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 고른_조건의_식이_콤보_아래에_서고_거기서_고친다() => HeadlessUi.Run(() =>
    {
        (Window window, ChapterGraphView view, AuthoringSession session) = Show();

        Combo(view, "EdgeConditionCombo").SelectedItem = "신뢰";
        Dispatcher.UIThread.RunJobs();

        TextBox expression = Expression(view, "EdgeUnlockExpressionHost");
        Assert.Equal("trust >= 3", expression.Text);
        Assert.Empty(Boxes(view, "EdgeVisibleExpressionHost"));   // 표시조건은 (없음) — 식 칸이 없다

        expression.Text = "trust >= 5";
        Enter(expression);

        Assert.Equal("trust >= 5", Condition(session, "신뢰").Expression);
        Assert.Equal("신뢰", Edge(session).ConditionLabel);

        // 되돌리기 한 칸.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("trust >= 3", Condition(session, "신뢰").Expression);

        window.Close();
    });

    [Fact]
    public void 다른_곳에서도_쓰는_조건이면_함께_바뀐다고_말하고_식이_틀리면_까닭을_보인다() => HeadlessUi.Run(() =>
    {
        (Window window, ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddEdge("ch01", "ch01_s1_new01", "ch01_s1_new03", optionLabel: "다른 길");
        session.Editor.UpdateEdge("ch01", "ch01_s1_new01", "ch01_s1_new03", visibleConditionLabel: "신뢰", matchOptionLabel: "다른 길");
        Dispatcher.UIThread.RunJobs();

        Combo(view, "EdgeVisibleCombo").SelectedItem = "신뢰";
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Notes(view, "EdgeVisibleExpressionHost"), text => text.Contains("다른 곳 1곳에서도", StringComparison.Ordinal));

        TextBox expression = Expression(view, "EdgeVisibleExpressionHost");
        expression.Text = "없는스탯 >= 3";
        Enter(expression);
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Notes(view, "EdgeVisibleExpressionHost"), text => text.StartsWith("✕ ", StringComparison.Ordinal));

        window.Close();
    });

    [Fact]
    public void 잠금_안내문은_해금조건을_걸었을_때만_선다() => HeadlessUi.Run(() =>
    {
        (Window window, ChapterGraphView view, _) = Show();
        TextBox message = view.FindControl<TextBox>("EdgeLockedMsgBox")!;

        Assert.False(message.IsVisible);

        Combo(view, "EdgeConditionCombo").SelectedItem = "신뢰";
        Dispatcher.UIThread.RunJobs();
        Assert.True(message.IsVisible);

        Combo(view, "EdgeConditionCombo").SelectedItem = "(없음)";
        Dispatcher.UIThread.RunJobs();
        Assert.False(message.IsVisible);

        window.Close();
    });

    [Fact]
    public void 새_조건은_이름과_식을_한_번에_만들고_그_관문에_걸며_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (Window window, ChapterGraphView view, AuthoringSession session) = Show();

        StackPanel panel = OpenNew(view, "EdgeUnlockNewButton");
        Box(panel, "new-condition-name").Text = "  하린과 친해졌을 때 ";
        Box(panel, "new-condition-expression").Text = "trust >= 4";
        Click(panel, "new-condition-create");

        Assert.Equal("trust >= 4", Condition(session, "하린과 친해졌을 때").Expression);
        Assert.Equal("하린과 친해졌을 때", Edge(session).ConditionLabel);
        Assert.Null(Edge(session).VisibleConditionLabel);
        Assert.Equal("하린과 친해졌을 때", Combo(view, "EdgeConditionCombo").SelectedItem);
        Assert.Equal("trust >= 4", Expression(view, "EdgeUnlockExpressionHost").Text);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(session.Editor.FindChapter("ch01")!.Conditions, item => item.Label == "하린과 친해졌을 때");
        Assert.Null(Edge(session).ConditionLabel);

        window.Close();
    });

    [Fact]
    public void 새_조건은_식을_비우면_식_미정이고_같은_이름과_빈_이름은_받지_않는다() => HeadlessUi.Run(() =>
    {
        (Window window, ChapterGraphView view, AuthoringSession session) = Show();

        StackPanel panel = OpenNew(view, "EdgeVisibleNewButton");
        Box(panel, "new-condition-name").Text = "신뢰";
        Click(panel, "new-condition-create");
        Assert.Contains("이미 있습니다", session.StatusMessage, StringComparison.Ordinal);

        Box(panel, "new-condition-name").Text = " ";
        Click(panel, "new-condition-create");
        Assert.Contains("적어 주세요", session.StatusMessage, StringComparison.Ordinal);
        Assert.Null(Edge(session).VisibleConditionLabel);

        Box(panel, "new-condition-name").Text = "아직 모름";
        Click(panel, "new-condition-create");

        Assert.Equal(string.Empty, Condition(session, "아직 모름").Expression);
        Assert.Equal("아직 모름", Edge(session).VisibleConditionLabel);
        Assert.Contains("식 미정", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static ComboBox Combo(ChapterGraphView view, string name) => view.FindControl<ComboBox>(name)!;

    private static List<TextBox> Boxes(ChapterGraphView view, string host) =>
        view.FindControl<StackPanel>(host)!.GetLogicalDescendants().OfType<TextBox>().ToList();

    private static TextBox Expression(ChapterGraphView view, string host) => Assert.Single(Boxes(view, host));

    private static List<string> Notes(ChapterGraphView view, string host) =>
        view.FindControl<StackPanel>(host)!.Children.OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();

    private static StackPanel OpenNew(ChapterGraphView view, string button)
    {
        view.FindControl<Button>(button)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        return view.NewConditionPanel!;
    }

    private static TextBox Box(StackPanel panel, string tag) =>
        panel.Children.OfType<TextBox>().Single(box => (string?)box.Tag == tag);

    private static void Click(StackPanel panel, string tag)
    {
        panel.Children.OfType<Button>().Single(button => (string?)button.Tag == tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Enter(TextBox box)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();
    }

    private static ChapterCondition Condition(AuthoringSession session, string label) =>
        session.Editor.FindChapter("ch01")!.Conditions.Single(item => item.Label == label);

    private static ChapterEdge Edge(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.Edges.Single(edge => edge.FromEpisodeId == "ch01_s1_new01" && edge.ToEpisodeId == "ch01_s1_new02");

    /// <summary>ch01 — 에피소드 셋(ch01_s1_new01 → ch01_s1_new02 「계속」, ch01_s1_new03), 스탯 trust, 조건 「신뢰」(trust >= 3). ch01_s1_new01 → ch01_s1_new02를 고른다.</summary>
    private (Window Window, ChapterGraphView View, AuthoringSession Session) Show()
    {
        Directory.CreateDirectory(_directory);
        var editor = new ProjectEditor(new StoryProject { Title = "간선 조건" });
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.AddChapterCondition("ch01", "신뢰", "trust >= 3");
        editor.EnsureChapterBoard("ch01");
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        var session = new AuthoringSession();
        session.Open(manifest);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1400, Height = 800, Content = view };
        window.Show();
        view.Attach(session);
        Dispatcher.UIThread.RunJobs();

        EpisodeAdder.Add(session, "ch01", "s1");
        EpisodeAdder.Add(session, "ch01", "s1");
        session.Editor.AddEdge("ch01", "ch01_s1_new01", "ch01_s1_new02", optionLabel: "계속");
        EpisodeAdder.Add(session, "ch01", "s1");
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        view.SelectEdgeKey("ch01_s1_new01", "ch01_s1_new02", "계속");
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        return (window, view, session);
    }
}
