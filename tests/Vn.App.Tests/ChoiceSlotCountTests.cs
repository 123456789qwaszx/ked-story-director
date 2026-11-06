using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 카드 아래 선택지 칸은 셋이다 — 인스펙터의 「빈 선택지 숨기기」를 켠 카드는 이은 선택지만</b> 선다. 다 이은 카드의
/// 안 이은 칸은 판만 어지럽힌다. 감추는 것은 안 이은 칸뿐이고(자동 길 자리도 이은 것이 없으면 감춘다), 이은 길은
/// 언제나 선다 — 이은 길을 숨기면 판이 거짓말을 한다. 켠 카드는 사용자 설정에 프로젝트별로 기억한다(화면 편의라 프로젝트
/// 파일이 아니다).
///
/// <b>[구성] 탭은 툴바의 체크 하나</b>가 판 전체에 걸린다 — 빈 선택지 점과 들어오는 길 없는 입구 점을 감추고, 앱 설정에
/// 기억한다. [대본] 탭의 카드별 칸과는 따로다.
/// </summary>
public sealed class ChoiceSlotCountTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-choice-slot-count", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        // [구성] 설정은 이 시험 프로세스가 함께 쓴다 — 중간에 실패해도 다른 시험의 판이 점을 감춘 채 서지 않게.
        AppSettingsService.SaveHideEmptyChoices(false);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 기본은_칸이_셋이고_숨기면_이은_선택지만_서며_자동_칸도_이은_것이_없으면_감춘다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = Card(session, "root").Id;

        // 기본 — 아무것도 안 이었어도 칸이 셋이다(첫 칸이 자동 길 자리).
        Assert.Equal(3, SlotRows(graph, "root").Count);

        // 숨기면 — 이은 것이 없으니 자동 칸까지 아무것도 안 선다. 숨긴 카드만 그렇다.
        session.SetHideEmptyChoices(root, hide: true);
        graph.Rebuild();
        Assert.Empty(SlotRows(graph, "root"));
        Assert.Equal(3, SlotRows(graph, "b").Count);

        // 둘을 이으면 — 이은 둘만 선다. 빈 칸은 안 붙는다.
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "옆");
        graph.Rebuild();
        Assert.Equal(["가자", "옆"], Labels(graph, "root"));

        // 다시 끄면 셋까지 빈 칸이 채워진다 — 이은 둘 + 빈 칸 하나.
        session.SetHideEmptyChoices(root, hide: false);
        graph.Rebuild();
        Assert.Equal(3, SlotRows(graph, "root").Count);
    });

    [Fact]
    public void 숨겨도_자동_길이_이어져_있으면_그_길은_선다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = Card(session, "root").Id;
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: null, auto: true);
        session.SetHideEmptyChoices(root, hide: true);
        graph.Rebuild();

        Assert.Single(SlotRows(graph, "root"));
    });

    [Fact]
    public void 인스펙터의_체크로_켜고_끄며_다음에_열어도_기억한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        session.Select(card.Id);
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Assert.Null(graph.FindControl<CheckBox>("HideEmptyChoicesCheck"));   // [대본] 툴바에는 일괄 체크가 없다

        CheckBox box = HideEmptyChoicesBox(window);
        Assert.True(box.IsVisible);
        Assert.False(box.IsChecked);
        Assert.Equal(3, SlotRows(graph, "b").Count);

        // b는 아무 데도 안 이었다 — 켜면 칸이 하나도 안 선다. 다른 카드는 그대로다.
        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(card.Id, session.HiddenChoiceNodeIds);
        Assert.Empty(SlotRows(graph, "b"));
        Assert.Equal(3, SlotRows(graph, "a").Count);

        // 다음에 열어도 — 같은 프로젝트를 다시 열면 켠 카드가 그대로다.
        var again = new AuthoringSession();
        again.Open(session.ProjectPath!);
        Assert.Contains(card.Id, again.HiddenChoiceNodeIds);

        box.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(card.Id, session.HiddenChoiceNodeIds);
        Assert.Equal(3, SlotRows(graph, "b").Count);

        window.Close();
    });

    [Fact]
    public void 구성_탭_툴바의_체크는_빈_점과_길_없는_입구_점을_감추고_대본_탭의_칸은_건드리지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        chapterGraph.SelectChapter("ch01");
        Dispatcher.UIThread.RunJobs();
        CheckBox chapterCheck = chapterGraph.FindControl<CheckBox>("HideEmptyChoicesCheck")!;

        Assert.False(chapterCheck.IsChecked);
        Assert.Equal(3, EmptySlotDots(chapterGraph, "b"));
        Assert.Equal(3, EntryDots(chapterGraph));   // 들어오는 길이 없는 root · b에도 서 있다

        // 켜면 — [구성] 판이 빈 점과 길 없는 입구 점을 감춘다.
        chapterCheck.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(session.HideEmptyChoices);
        Assert.Equal(0, EmptySlotDots(chapterGraph, "b"));
        Assert.Equal(0, EmptySlotDots(chapterGraph, "root"));   // 이은 선택지 하나만 선다
        Assert.Equal(1, EntryDots(chapterGraph));   // 들어오는 길이 있는 a만 입구 점이 선다

        // [대본] 탭의 칸은 그대로다 — 카드마다 따로 정한다.
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, SlotRows(graph, "b").Count);
        Assert.Empty(session.HiddenChoiceNodeIds);

        // 다음에 열어도 — 앱 설정이라 새 세션도 켜진 채로 시작한다.
        Assert.True(new AuthoringSession().HideEmptyChoices);

        chapterCheck.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, EmptySlotDots(chapterGraph, "b"));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>ch01: root · a · b — 이은 것 없음.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "선택지 칸" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");
        session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "root", "root", 0, 0);
        session.Editor.AddEpisode("ch01", "a", "a", 0, 500);
        session.Editor.AddEpisode("ch01", "b", "b", 400, 500);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        return (graph, session);
    }

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    /// <summary>그 에피소드의 선택지 칸 줄들 — Tag가 「{에피소드}#{칸}」이다.</summary>
    private static List<StackPanel> SlotRows(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(row => row.Tag is string tag && tag.StartsWith(episodeId + "#", StringComparison.Ordinal))
            .ToList();

    /// <summary>그 에피소드의 선택지 칸에 선 문구들 — 위에서 아래 차례.</summary>
    private static List<string> Labels(GraphEditorView graph, string episodeId) =>
        SlotRows(graph, episodeId)
            .OrderBy(Canvas.GetTop)
            .Select(row => row.Children.OfType<TextBlock>().First().Text ?? string.Empty)
            .ToList();

    /// <summary>[구성] 판에서 그 에피소드 카드 아래의 빈 선택지 점 수.</summary>
    private static int EmptySlotDots(ChapterGraphView view, string episodeId) =>
        view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Avalonia.Controls.Shapes.Ellipse>()
            .Count(dot => dot.Tag is string tag &&
                          tag.StartsWith(ChapterGraphView.EmptySlotTag + episodeId + "#", StringComparison.Ordinal));

    /// <summary>[구성] 판의 입구 점 수 — 들어오는 길이 모이는 카드 위변 가운데 점이다.</summary>
    private static int EntryDots(ChapterGraphView view) =>
        view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Avalonia.Controls.Shapes.Ellipse>()
            .Count(dot => ToolTip.GetTip(dot) as string == "이 에피소드로 들어오는 길이 여기로 모입니다.");

    private static CheckBox HideEmptyChoicesBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single()
            .FindControl<CheckBox>("HideEmptyChoicesBox")!;
}
