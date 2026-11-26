using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판을 줄여도 선 · 카드 테두리는 화면에서 사라지지 않는다</b>(design/board-zoom.md 5절 · 2026-11-26 소유자 — 10%에서 선이 거의 안 보였다).
/// 그리는 굵기 = max(판의 굵기, 바닥 ÷ 배율) — 선 1px · 고른 선과 고른 카드 2px. 카드 테두리는 굵기를 바꾸지 않고(카드의 안쪽 치수가 같다) 바깥 고리로
/// 모자란 만큼을 더한다. 카드의 그림자는 글을 보이는 카드에만 둔다.
/// </summary>
public sealed class LineFloorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-line-floor", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 줄여도_판의_선은_화면에서_1px_아래로_가늘어지지_않고_키우면_테마의_굵기로_돌아온다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, _) = Show();
        List<double> before = BoardLines(graph).Select(line => line.StrokeThickness).ToList();

        foreach (double zoom in new[] { 0.5, 0.2, 0.1 })
        {
            graph.ZoomTo(zoom);
            Settle(graph);

            List<Shape> lines = BoardLines(graph);
            Assert.NotEmpty(lines);
            Assert.All(lines, line => Assert.True(
                line.StrokeThickness * zoom >= GraphEditorView.LineFloorPx - 0.001,
                $"{zoom:P0}: {line.GetType().Name} {line.StrokeThickness * zoom:0.00}px"));
        }

        graph.ZoomTo(1);
        Settle(graph);
        Assert.Equal(before, BoardLines(graph).Select(line => line.StrokeThickness).ToList());   // 100%에서는 테마의 굵기 그대로(1.4 · 2 …)
    });

    [Fact]
    public void 카드_테두리는_굵기를_바꾸지_않고_바깥_고리로_화면_바닥을_지킨다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        string a = CardId(session, "a");
        session.Select(root);
        Settle(graph);
        Size size = Card(graph, a).Bounds.Size;

        graph.ZoomTo(1);
        Settle(graph);
        Assert.Equal(Skin.CardShadow, Card(graph, a).BoxShadow);   // 100% — 고리 없이 테마의 그림자 그대로

        graph.ZoomTo(0.1);
        Settle(graph);

        Border plain = Card(graph, a);
        Border picked = Card(graph, root);
        Assert.Equal(1, plain.BorderThickness.Left);    // 테두리의 굵기는 그대로 — 카드의 안쪽 치수가 같다
        Assert.Equal(2, picked.BorderThickness.Left);
        Assert.Equal(size, plain.Bounds.Size);

        // 바닥 1px(고르면 2px) ÷ 10% = 판 위 10(20) — 테두리에 모자란 만큼이 번짐 없는 고리다. 글을 보이지 않는 카드라 그림자는 없다.
        Assert.Equal(1, plain.BoxShadow.Count);
        Assert.Equal(9, plain.BoxShadow[0].Spread, 3);
        Assert.Equal(0, plain.BoxShadow[0].Blur);
        Assert.Equal(1, picked.BoxShadow.Count);
        Assert.Equal(18, picked.BoxShadow[0].Spread, 3);
        Assert.Equal(Skin.Selection, picked.BoxShadow[0].Color);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>하이어아키 픽스처를 [대본] 탭으로 열고 길 둘을 잇는다 — 철도 · 레인 · 도착 선이 선다.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "교실로 간다");
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "복도로 나간다");
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Settle(graph);
        return (graph, session);
    }

    private static void Settle(GraphEditorView graph)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
        }
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Border Card(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    /// <summary>판에 보이는 선(철도 · 레인 · 간선) — 투명한 누름 띠와 점 · 화살촉은 뺀다.</summary>
    private static List<Shape> BoardLines(GraphEditorView graph) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Shape>()
            .Where(shape => shape is Line or Avalonia.Controls.Shapes.Path && shape.IsVisible &&
                            shape.Stroke is ISolidColorBrush { Color.A: > 0 })
            .ToList();
}
