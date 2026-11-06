using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// [대본] 탭 하이어아키에서 챕터를 고르면 인스펙터에 그 챕터의 스탯 표가 서고, 줄마다 대사에 그대로 붙여 쓸
/// <c>{stat("키")}</c>가 짝지어 있다 — 작가가 키를 외우거나 문법을 치지 않고 복사해 쓴다.
/// </summary>
public sealed class DirectorChapterStatsTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-chapter-stats", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 챕터를_고르면_스탯_표와_대사에_붙일_글이_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.AddChapterStat("ch01", "성실", "성실", initial: 1, minimum: 0, maximum: 5);
        session.Editor.AddChapterStat("ch01", "trust", "신뢰", initial: 0, minimum: 0, maximum: 10);
        session.Editor.AddChapterStat("ch01", "met", "만남", type: ChapterStatType.Bool, maximum: 1);
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = Pick(window, "ch01");

        Assert.Equal("{stat(\"성실\")}", Snippet(graph, "성실").Text);
        Assert.Equal("{stat(\"trust\")}", Snippet(graph, "trust").Text);   // 표시 이름이 달라도 대본에는 키가 들어간다
        Assert.Equal("{stat(\"met\")}", Snippet(graph, "met").Text);

        List<string> texts = Texts(graph);
        Assert.Contains("신뢰 (trust)", texts);
        Assert.Contains("0~5 · 처음 1", texts);
        Assert.Contains("참/거짓(1/0) · 처음 거짓", texts);

        window.Close();
    });

    [Fact]
    public void 복사를_누르면_그_글이_클립보드에_든다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.AddChapterStat("ch01", "성실", "성실", maximum: 5);
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = Pick(window, "ch01");
        Control("stat-copy:성실", graph).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("{stat(\"성실\")}", window.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult());
        Assert.Contains("{stat(\"성실\")}를 복사했습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 스탯이_없는_챕터는_없다고_말한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, _) = HierarchyFixture.Open(_directory, GraphTab);

        GraphEditorView graph = Pick(window, "ch02");

        Assert.Contains(Texts(graph), text => text.StartsWith("이 챕터에는 아직 스탯이 없습니다", StringComparison.Ordinal));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static GraphEditorView Pick(MainWindow window, string chapterId)
    {
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey(chapterId))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal($"챕터 · {chapterId}", graph.FindControl<TextBlock>("InspectorTitle")!.Text);
        return graph;
    }

    private static IEnumerable<Control> Side(GraphEditorView graph) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Control>().Distinct()
            .Where(control => control.IsEffectivelyVisible);

    private static Control Control(string tag, GraphEditorView graph) =>
        Side(graph).Single(control => (string?)control.Tag == tag);

    private static SelectableTextBlock Snippet(GraphEditorView graph, string key) =>
        (SelectableTextBlock)Control("stat-snippet:" + key, graph);

    private static List<string> Texts(GraphEditorView graph) =>
        Side(graph).OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();
}
