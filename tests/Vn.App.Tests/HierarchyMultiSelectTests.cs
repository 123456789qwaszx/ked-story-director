using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>하이어아키에서 여러 줄 고르기</b> (2026-11-23 소유자 — 「여러개를 shift + 우클릭으로 한번에 선택하는게 가능할까요?」 → 고른 답 「Ctrl+클릭 · Shift+클릭」 ·
/// 「섞어 고를 수 있음」 · 「마지막 줄을 따르고 [삭제]는 끔」 · <c>docs/plans/hierarchy.md</c> §5). 마우스로 줄을 눌러(보조 키와 함께) 본다.
/// </summary>
public sealed class HierarchyMultiSelectTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-multi", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Ctrl_클릭은_더하고_빼고_판과_인스펙터는_마지막_줄을_따르며_삭제는_꺼진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph, StoryHierarchy tree) = Open();
        string a = StoryHierarchy.EpisodeKey("ch01", "a");
        string ch02 = StoryHierarchy.ChapterKey("ch02");
        string hall = StoryHierarchy.SceneKey("ch01", "hall");

        Press(window, tree, a);
        Assert.Equal([a], Keys(tree));
        Assert.Equal(Card(session, "ch01", "a"), session.SelectedNodeId);
        Assert.True(DeleteButton(graph).IsEnabled);

        // Ctrl+클릭 — 챕터를 더한다(종류가 섞여도 된다). 판 · 인스펙터는 그 챕터를 따르고 [삭제]는 꺼진다.
        Press(window, tree, ch02, RawInputModifiers.Control);
        Assert.Equal([a, ch02], Keys(tree));
        Assert.Equal(ch02, tree.Selection?.Key);
        Assert.Equal("ch02", session.ActiveFile?.Name);
        Assert.False(DeleteButton(graph).IsEnabled);
        Assert.Contains("여러 개", (string)ToolTip.GetTip(DeleteButton(graph))!, StringComparison.Ordinal);

        Press(window, tree, hall, RawInputModifiers.Control);
        Assert.Equal([a, ch02, hall], Keys(tree));
        Assert.All(new[] { a, ch02, hall }, key => Assert.True(Painted(tree, key), key));
        Assert.False(Painted(tree, StoryHierarchy.EpisodeKey("ch01", "b")));

        // 마지막 줄이 아닌 줄을 빼면 판은 그대로다. 마지막 줄을 빼면 그 앞의 줄이 마지막이 된다.
        Press(window, tree, ch02, RawInputModifiers.Control);
        Assert.Equal([a, hall], Keys(tree));
        Assert.Equal(hall, tree.Selection?.Key);
        Press(window, tree, hall, RawInputModifiers.Control);
        Assert.Equal([a], Keys(tree));
        Assert.Equal(Card(session, "ch01", "a"), session.SelectedNodeId);
        Assert.True(DeleteButton(graph).IsEnabled);

        // 하나 남은 줄은 빼지 않는다.
        Press(window, tree, a, RawInputModifiers.Control);
        Assert.Equal([a], Keys(tree));

        window.Close();
    });

    [Fact]
    public void Shift_클릭은_기준_줄부터_그_줄까지_고르고_그냥_누르면_다시_한_줄이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph, StoryHierarchy tree) = Open();
        string root = StoryHierarchy.EpisodeKey("ch01", "root");
        string b = StoryHierarchy.EpisodeKey("ch01", "b");

        Press(window, tree, root);
        Press(window, tree, b, RawInputModifiers.Shift);

        // 보이는 줄 차례로 root부터 b까지 — 사이의 장면 줄도 든다. 누른 줄이 마지막이다.
        List<string> between = tree.Rows.SkipWhile(row => row.Key != root).TakeWhile(row => row.Key != b).Select(row => row.Key).Append(b).ToList();
        Assert.Contains(StoryHierarchy.SceneKey("ch01", "hall"), between);
        Assert.Equal(between.OrderBy(key => key, StringComparer.Ordinal), Keys(tree).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(b, tree.Selection?.Key);
        Assert.Equal(Card(session, "ch01", "b"), session.SelectedNodeId);
        Assert.False(DeleteButton(graph).IsEnabled);

        // Delete 키도 지우지 않는다 — 까닭을 말한다.
        int episodes = session.Editor.FindChapter("ch01")!.Episodes.Count;
        Assert.True(graph.DeleteSelectedCard());
        Assert.Equal(episodes, session.Editor.FindChapter("ch01")!.Episodes.Count);
        Assert.Contains("여러 개", session.StatusMessage, StringComparison.Ordinal);

        // 기준 줄은 그대로 — Shift로 범위를 다시 잡는다.
        string a = StoryHierarchy.EpisodeKey("ch01", "a");
        Press(window, tree, a, RawInputModifiers.Shift);
        Assert.Equal([root, a], Keys(tree));

        // 그냥 누르면 한 줄. 판에서 다른 카드를 고르면(밖에서) 그것 한 줄.
        Press(window, tree, b);
        Assert.Equal([b], Keys(tree));
        Press(window, tree, a, RawInputModifiers.Control);
        session.Select(Card(session, "ch02", "x"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([StoryHierarchy.EpisodeKey("ch02", "x")], Keys(tree));
        Assert.True(DeleteButton(graph).IsEnabled);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph, StoryHierarchy Tree) Open()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        StoryHierarchy tree = graph.HierarchyView;

        foreach (string key in new[]
                 {
                     StoryHierarchy.ChapterKey("ch01"), StoryHierarchy.SceneKey("ch01", "opening"), StoryHierarchy.SceneKey("ch01", "hall"),
                     StoryHierarchy.ChapterKey("ch02")
                 })
        {
            tree.Fold(key, collapse: false);
        }

        Dispatcher.UIThread.RunJobs();
        return (window, session, graph, tree);
    }

    /// <summary>그 줄을 마우스로 누른다 — 보조 키와 함께.</summary>
    private static void Press(MainWindow window, StoryHierarchy tree, string key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Button button = tree.RowButton(key)!;
        button.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        Point center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left, modifiers);
        window.MouseUp(center, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Keys(StoryHierarchy tree) => tree.Selections.Select(pick => pick.Key).ToList();

    private static bool Painted(StoryHierarchy tree, string key) =>
        tree.RowButton(key)!.Background is ISolidColorBrush { Color.A: > 0 };

    private static Button DeleteButton(GraphEditorView graph) => graph.FindControl<Button>("DeleteButton")!;

    private static string Card(AuthoringSession session, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!.Id;
}
