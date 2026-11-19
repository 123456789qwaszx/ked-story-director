using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>판을 다시 그릴 때 그대로인 카드는 판에 둔다</b> (2026-11-19 소유자 — 속도 개선 ⑤ ~ ⑦ · usability UX17). 에피소드를 더하고 지우고, 장면을
/// 접고 펴고, 되돌리기를 할 때마다 카드 160장을 떼고 새로 지었다 — 판 한 번에 1.3 ~ 2.3초. 그대로 둔 판은 <b>새로 지은 판과 똑같아야</b>
/// 한다 — 손놀림마다 통째로 새로 지은 판과 견준다(제자리에서 고친 제목 · 자리 · 크기가 되돌아오는 경우까지).
/// </summary>
public sealed class BoardCardReuseTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-reuse", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 손놀림마다_그대로_둔_판이_새로_지은_판과_같다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string root = Card(session, "root");
        string a = Card(session, "a");
        session.Editor.ApplyScenarioText(root, "라루: 첫 줄.\n윌로: 둘째 줄.\n라루: 셋째 줄.");
        string second = session.Project.FindDialogue(root)!.Script.ActiveLines.ElementAt(1).Id;
        session.Editor.AddBranchMarker(root, second);
        session.Editor.AddBranchMarker(root, second);
        session.Editor.ApplyScenarioText(a, "윌로: 골목 끝.");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();
        Same(graph, "처음");

        int cards = Cards(graph).Count;
        session.Editor.AddEpisode("ch01", "c", title: "c", 700, 0, sceneId: "hall");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(cards, graph.KeptCardCount);   // 있던 카드는 모두 그대로 — 더한 카드만 지었다
        Same(graph, "에피소드 더하기");

        session.Editor.RemoveEpisode("ch01", "c");
        Dispatcher.UIThread.RunJobs();
        Same(graph, "에피소드 지우기");

        // 제자리에서 고친 것이 되돌아온다 — 제목 · 자리 · 크기.
        session.Editor.UpdateEpisode("ch01", "a", title: "골목");
        Dispatcher.UIThread.RunJobs();
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Same(graph, "제목 되돌리기");

        session.Editor.MoveNode(a, 320, 60);
        Dispatcher.UIThread.RunJobs();
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Same(graph, "옮기기 되돌리기");

        session.Editor.ResizeNode(a, BoardSize.CardWidth + 80, 220);
        Dispatcher.UIThread.RunJobs();
        Same(graph, "크기");
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Same(graph, "크기 되돌리기");

        session.Editor.ApplyScenarioText(a, "윌로: 골목 끝.\n라루: 새 줄.");
        Dispatcher.UIThread.RunJobs();
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Same(graph, "줄 되돌리기");
        session.Editor.Redo();
        Dispatcher.UIThread.RunJobs();
        Same(graph, "줄 다시 하기");

        string board = session.EnsureChapterBoard("ch01");
        session.SetSceneCollapsed(board, "opening", true);
        Dispatcher.UIThread.RunJobs();
        Same(graph, "장면 접기");
        session.SetSceneCollapsed(board, "opening", false);
        Dispatcher.UIThread.RunJobs();
        Same(graph, "장면 펴기");

        window.Close();
    });

    [Fact]
    public void 넣지_않은_글이_든_칸은_초점이_없으면_새로_짓고_초점이_있으면_그_글을_지킨다() => HeadlessUi.Run(() =>
    {
        // 지금까지처럼 — 판을 다시 그리면 초점 없는 칸은 모델의 글로 돌아가고, 쓰던 칸은 그 글과 커서를 지킨다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string a = Card(session, "a");
        string b = Card(session, "b");
        session.Editor.ApplyScenarioText(a, "윌로: 골목 끝.");
        session.Editor.ApplyScenarioText(b, "라루: 복도.");
        graph.Rebuild();   // 칸 밖에서 쓴 글 — 고르지 않은 카드의 칸은 다시 그릴 때 따라온다
        Dispatcher.UIThread.RunJobs();

        TextBox idle = Writing(graph, a);
        idle.Text = "윌로: 넣지 않은 글.";
        TextBox busy = Writing(graph, b);
        busy.Focus();
        busy.Text = "라루: 쓰는 중.";
        busy.SelectionStart = busy.SelectionEnd = 3;
        Dispatcher.UIThread.RunJobs();

        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        Assert.NotSame(idle, Writing(graph, a));
        Assert.Equal("윌로: 골목 끝.", Writing(graph, a).Text);
        Assert.Same(busy, Writing(graph, b));
        Assert.Equal("라루: 쓰는 중.", busy.Text);
        Assert.True(busy.IsFocused);
        Assert.Equal(3, busy.SelectionStart);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>지금 판(그대로 둔 카드가 섞였다)이 카드를 모두 새로 지은 판과 같은가.</summary>
    private static void Same(GraphEditorView graph, string step)
    {
        List<string> kept = Faces(graph);
        graph.RebuildFresh();
        Dispatcher.UIThread.RunJobs();
        List<string> fresh = Faces(graph);

        Assert.True(fresh.SequenceEqual(kept), $"{step}: 그대로 둔 판이 새로 지은 판과 다르다\n그대로: {string.Join("\n", kept.Except(fresh))}\n새로: {string.Join("\n", fresh.Except(kept))}");
    }

    /// <summary>
    /// 판의 카드마다 보이는 모습 — 자리 · 크기 · 글월 · 글 칸(글 · 높이) · 풍선, 분기 점 · 공통 점의 자리와 보임. 차례 없이 견주게 정렬한다.
    /// </summary>
    private static List<string> Faces(GraphEditorView graph)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        var faces = new List<string>();

        foreach (Border card in Cards(graph))
        {
            IEnumerable<string> texts = card.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.FindAncestorOfType<TextBox>() is null)
                .Select(block => $"{block.Text}@{ToolTip.GetTip(block)}");
            IEnumerable<string> boxes = card.GetVisualDescendants().OfType<TextBox>()
                .Select(box => $"[{box.Text}|{box.Height:0.#}]");
            faces.Add($"{card.Tag} {Canvas.GetLeft(card):0.#},{Canvas.GetTop(card):0.#} w{card.Width:0.#} h{card.Bounds.Height:0.#} " +
                      $"{card.BorderBrush} {string.Join(" ", texts)} {string.Join(" ", boxes)}");
        }

        foreach (Control mark in canvas.Children.Where(child => child is Ellipse or Border &&
                                                                child.Tag is string tag &&
                                                                (tag.StartsWith(GraphEditorView.LineKnobTag, StringComparison.Ordinal) ||
                                                                 tag.StartsWith(GraphEditorView.BranchHubTag, StringComparison.Ordinal))))
        {
            faces.Add($"{mark.Tag} {Canvas.GetLeft(mark):0.#},{Canvas.GetTop(mark):0.#} {mark.IsVisible}");
        }

        faces.Sort(StringComparer.Ordinal);
        return faces;
    }

    private static List<Border> Cards(GraphEditorView graph) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Where(border => border.Tag is string tag && border.GetVisualDescendants().OfType<TextBlock>()
                .Any(block => block.Tag as string == GraphEditorView.CardTitleTag))
            .ToList();

    private static TextBox Writing(GraphEditorView graph, string nodeId) =>
        Cards(graph).Single(card => card.Tag as string == nodeId)
            .GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    private static string Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;
}
