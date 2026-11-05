using Avalonia.Controls;
using Avalonia.Interactivity;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Tests.Fixtures;

namespace Vn.App.Tests;

/// <summary>
/// 스탯 수치를 툴에서 만진다 — 챕터 그래프에서도 에피소드 노드에서도 스탯 변화를 적는다.
///
/// 값이 사는 곳은 프로젝트의 챕터 간선이다.
/// 그래서 확인은 언제나 화면이 아니라 <b>프로젝트의 챕터를 다시 읽어</b> 한다.
/// </summary>
public sealed class ChapterStatEditingTests
{
    [Fact]
    public void 에피소드_노드의_선택지_폼에서_스탯변화를_적는다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        ChapterGraphModel model = Reread(session);
        ChapterEdge target = model.Edges.First(edge => edge.FromEpisodeId == "main05.02");

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.OpenEdgeForm(target, rowIndex: 0);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        StatChangeEditor editor = Editor(view, "EdgeFormStatsHost");

        // 이 길에는 이미 변화가 걸려 있다 — 견본이 심어 둔 `복도지남 true`다. 폼은 기존 줄을 싣고 서므로 새 줄은 뒤에 선다.
        int added = Rows(editor);
        Add(editor);

        // 드롭다운은 챕터에 선언된 스탯만 담는다.
        ChapterStat first = model.Stats[0];
        StatCombo(editor, added).SelectedItem = Display(first);
        AmountBox(editor, added).Text = "2";

        view.SubmitEdgeForm();

        ChapterEdge reread = Reread(session).Edges
            .Single(edge => edge.FromEpisodeId == target.FromEpisodeId
                && edge.ToEpisodeId == target.ToEpisodeId
                && (edge.OptionLabel ?? string.Empty) == (target.OptionLabel ?? string.Empty));

        StatDelta delta = Assert.Single(reread.StatChanges, item => item.Key == first.Key);
        Assert.Equal(2, delta.Amount);

        // 원래 있던 것은 그대로다 — 새 줄을 더하는 것이 기존 줄을 지우지 않는다.
        Assert.Contains(reread.StatChanges, item => item.Key == "복도지남" && item.IsSet);

        // 배선은 건드리지 않았다 — 문구·도착이 그대로다.
        Assert.Equal(target.ToEpisodeId, reread.ToEpisodeId);
        Assert.Equal(target.OptionLabel, reread.OptionLabel);
    });

    [Fact]
    public void 새로_잇는_길에도_스탯변화가_같은_저장에_실린다() => HeadlessUi.Run(() =>
    {
        // 두 번 고치면 되돌리기가 두 걸음이 되고, 그 사이 반쪽만 적힌 길이 잠깐 진짜가 된다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        ChapterStat stat = Reread(session).Stats[0];

        view.SelectEpisode("main05.01");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.FindControl<Button>("AddNextEdgeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        StatChangeEditor editor = Editor(view, "EdgeFormStatsHost");
        Add(editor);
        StatCombo(editor, 0).SelectedItem = Display(stat);
        SignCombo(editor, 0).SelectedIndex = 1;  // －
        AmountBox(editor, 0).Text = "3";

        view.FindControl<ComboBox>("EdgeTargetCombo")!.SelectedItem = "main05.end";
        view.SubmitEdgeForm();

        ChapterEdge wired = Reread(session).Edges
            .Single(edge => edge.FromEpisodeId == "main05.01" && edge.ToEpisodeId == "main05.end");

        Assert.Equal(new StatDelta(stat.Key, -3), Assert.Single(wired.StatChanges));
    });

    [Fact]
    public void 간선_패널에서도_같은_줄_편집기로_적는다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        ChapterStat stat = Reread(session).Stats[0];

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        StatChangeEditor editor = Editor(view, "EdgeStatsHost");

        // 이 길에도 견본의 `복도지남 true`가 이미 걸려 있다 — 새 줄은 그 뒤에 선다.
        int added = Rows(editor);
        Add(editor);
        StatCombo(editor, added).SelectedItem = Display(stat);
        AmountBox(editor, added).Text = "4";

        view.ApplyEdgeFromPanel();

        ChapterEdge reread = Reread(session).Edges
            .Single(edge => edge.ToEpisodeId == "branch05.02A");

        Assert.Equal(
            new StatDelta(stat.Key, 4),
            Assert.Single(reread.StatChanges, item => item.Key == stat.Key));

        // 안 건드린 값은 그대로다 — 이 길에는 해금조건이 걸려 있었다.
        Assert.Equal("신뢰높음", reread.ConditionLabel);
    });

    [Fact]
    public void 툴에서_적은_수치가_그_자리에서_도착_스탯으로_보인다() => HeadlessUi.Run(() =>
    {
        // 두 편집 자리가 만나는 곳이다 — 여기서 수치를 적으면 저기 카드에 폭이 뜬다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        ChapterGraphModel model = Reread(session);
        ChapterStat stat = model.Stats[0];
        ChapterEdge target = model.Edges.Single(edge =>
            edge.FromEpisodeId == "main05.01" && edge.ToEpisodeId == "main05.02");

        view.SelectEpisode("main05.01");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.OpenEdgeForm(target, rowIndex: 0);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        StatChangeEditor editor = Editor(view, "EdgeFormStatsHost");
        Add(editor);
        StatCombo(editor, 0).SelectedItem = Display(stat);
        AmountBox(editor, 0).Text = "2";

        view.SubmitEdgeForm();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 줄에는 챕터의 스탯이 전부 선다 — 견본에는 `복도지남` 깃발도 있어
        // 뒤가 따라붙는다. 여기서 재는 것은 맨 앞의 그 수치다.
        Assert.StartsWith($"{stat.DisplayName} 2", StatLine(view, "main05.02"));

        // 그 길을 지나지 않은 시작 노드는 초기값 그대로다.
        Assert.StartsWith($"{stat.DisplayName} {stat.Initial}", StatLine(view, "main05.01"));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>프로젝트가 든 견본 챕터를 지금 모습 그대로 다시 편다 — 화면이 아니라 원본을 본다.</summary>
    private static ChapterGraphModel Reread(AuthoringSession session) =>
        session.Editor.FindChapter(SampleChapters.Corridor)!.ToGraphModel(SampleChapters.Corridor);

    private static string Display(ChapterStat stat) =>
        string.IsNullOrWhiteSpace(stat.DisplayName) || stat.DisplayName == stat.Key
            ? stat.Key
            : $"{stat.DisplayName} ({stat.Key})";

    private static StatChangeEditor Editor(ChapterGraphView view, string host) =>
        view.FindControl<StackPanel>(host)!.Children.OfType<StatChangeEditor>().Single();

    private static void Add(StatChangeEditor editor) =>
        editor.Children.OfType<Button>().Single()
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>지금 폼에 서 있는 줄 수 — 기존 변화가 실려 있으면 0이 아니다.</summary>
    private static int Rows(StatChangeEditor editor) =>
        editor.Children.OfType<Grid>().Count();

    private static Grid Row(StatChangeEditor editor, int index) =>
        editor.Children.OfType<Grid>().ElementAt(index);

    private static ComboBox StatCombo(StatChangeEditor editor, int index) =>
        Row(editor, index).Children.OfType<ComboBox>().First();

    private static ComboBox SignCombo(StatChangeEditor editor, int index) =>
        Row(editor, index).Children.OfType<ComboBox>().Last();

    private static TextBox AmountBox(StatChangeEditor editor, int index) =>
        Row(editor, index).Children.OfType<TextBox>().Single();

    /// <summary>그 카드에 붙은 스탯 줄 — 없으면 빈 글자.</summary>
    private static string StatLine(ChapterGraphView view, string episodeId)
    {
        Border card = view.FindControl<Canvas>("GraphCanvas")!.Children
            .OfType<Border>()
            .Single(border => border.Tag as string == episodeId);

        return ((StackPanel)card.Child!).Children
            .OfType<TextBlock>()
            .FirstOrDefault(block => block.Tag as string == ChapterGraphView.StatLineTag)
            ?.Text ?? string.Empty;
    }

    private static (ChapterGraphView View, AuthoringSession Session) Show(TempProject project)
    {
        var session = new AuthoringSession();
        session.Open(project.ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1400, Height = 800, Content = view };
        window.Show();
        view.Attach(session);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (view, session);
    }

    private sealed class TempProject : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "vn-chapter-stat", Guid.NewGuid().ToString("N"));

        public TempProject()
        {
            ManifestPath = SampleProject.Corridor(_directory, "스탯 편집 검증");
        }

        public string ManifestPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
