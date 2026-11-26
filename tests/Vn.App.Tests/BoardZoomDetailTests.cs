using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판을 줄이면 덜 그린다</b>(2026-11-26 — 디자인 점검 7번 「축소했을 때의 판」). 50% 아래면 카드의 글 칸과 머리 줄이 걷히고 제목만 화면에서
/// 14px로 서며, 선택지 칸은 점만 남는다. 25% 아래면 카드는 머리 색의 덩어리다. 챕터 · 장면 이름표는 화면에서 12px 아래로 줄지 않는다. 카드의 치수 · 자리는
/// 배율과 상관없이 같고, 다시 키우면 그대로 돌아온다.
/// </summary>
public sealed class BoardZoomDetailTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-board-zoom-detail", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 배율의_문턱() => Assert.Equal(
        [GraphEditorView.BoardDetail.Full, GraphEditorView.BoardDetail.Titles, GraphEditorView.BoardDetail.Titles, GraphEditorView.BoardDetail.Blocks],
        new[] { 0.5, 0.49, 0.25, 0.24 }.Select(GraphEditorView.DetailFor));

    [Fact]
    public void 반_아래로_줄이면_카드는_제목만_남고_선택지는_점만_남으며_키우면_돌아온다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Assert.Equal(GraphEditorView.BoardDetail.Full, graph.Detail);
        Size size = Card(graph, root).Bounds.Size;
        Assert.Null(Overview(graph, root));   // 다 그린 판에는 겹을 짓지 않는다
        Assert.NotEmpty(Chips(graph));

        graph.ZoomTo(0.4);
        Settle(graph);

        Assert.Equal(GraphEditorView.BoardDetail.Titles, graph.Detail);
        TextBox box = WritingBox(graph, root);
        Assert.Equal(0d, box.Opacity);
        Assert.False(box.IsHitTestVisible);   // 누르면 카드를 고르고 끈다 — 보이지 않는 글 칸에 쓰지 않는다
        Border overview = Overview(graph, root)!;
        TextBlock title = OverviewTitle(overview);
        Assert.True(title.IsEffectivelyVisible);
        Assert.Equal(GraphEditorView.ZoomedTitlePx / 0.4, title.FontSize, 3);   // 화면에서 14px
        Assert.Equal(HeaderTitle(graph, root).Text, title.Text);
        AssertSize(size, Card(graph, root));   // 카드의 치수는 그대로

        foreach (StackPanel chip in Chips(graph))
        {
            foreach (Control part in chip.Children)
            {
                Assert.Equal(part is Ellipse ? 1d : 0d, part.Opacity);
            }
        }

        graph.ZoomTo(0.2);
        Settle(graph);

        Assert.Equal(GraphEditorView.BoardDetail.Blocks, graph.Detail);
        Assert.False(title.IsEffectivelyVisible);
        Border fill = ((Panel)overview.Child!).Children.OfType<Border>().Single();
        Assert.True(fill.IsEffectivelyVisible);
        Assert.NotNull(fill.Background);   // 머리 색의 덩어리

        graph.ZoomTo(1);
        Settle(graph);

        Assert.Equal(GraphEditorView.BoardDetail.Full, graph.Detail);
        Assert.Equal(1d, box.Opacity);
        Assert.True(box.IsHitTestVisible);
        Assert.False(overview.IsVisible);
        AssertSize(size, Card(graph, root));
        Assert.All(Chips(graph).SelectMany(chip => chip.Children), part => Assert.True(part.Opacity > 0));
    });

    [Fact]
    public void 이름표는_화면에서_바닥_크기_아래로_줄지_않는다() => HeadlessUi.Run(() =>
    {
        // 이름표는 판 위의 화면 겹에 선다 — 그리는 배수가 곧 화면의 배수다(design/board-zoom.md 4절). 챕터 13px · 장면 12px.
        (GraphEditorView graph, _) = Show();
        Assert.Equal(1d, ScaleOf(Label(graph, GraphEditorView.SceneLabelTag + "opening")));   // 100%에서는 판에 그리던 크기 그대로

        foreach (double zoom in new[] { 0.4, 0.15 })
        {
            graph.ZoomTo(zoom);
            Settle(graph);
            AssertFloor(Label(graph, GraphEditorView.SceneLabelTag + "opening"), GraphEditorView.SceneLabelMinPx);
            AssertFloor(Label(graph, GraphEditorView.ChapterLabelTag + "ch01"), GraphEditorView.ChapterLabelMinPx);
        }

        graph.ZoomTo(1);
        Settle(graph);
        Assert.Equal(1d, ScaleOf(Label(graph, GraphEditorView.SceneLabelTag + "opening")));
    });

    private static double ScaleOf(Border label) => label.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1;

    private static void AssertFloor(Border label, double floor) =>
        Assert.True(((TextBlock)label.Child!).FontSize * ScaleOf(label) >= floor - 0.01, $"{label.Tag}: {((TextBlock)label.Child!).FontSize * ScaleOf(label)}px");

    private static Border Label(GraphEditorView graph, string tag) =>
        graph.FindControl<Canvas>("FrameLabelLayer")!.Children.OfType<Border>().Single(border => border.Tag as string == tag);

    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
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

    private static void AssertSize(Size expected, Border card)
    {
        Assert.Equal(expected.Width, card.Bounds.Width, 3);
        Assert.Equal(expected.Height, card.Bounds.Height, 3);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Border Card(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    private static TextBlock HeaderTitle(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Tag as string == GraphEditorView.CardTitleTag);

    private static Border? Overview(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Border>().SingleOrDefault(border => border.Tag as string == GraphEditorView.CardOverviewTag);

    private static TextBlock OverviewTitle(Border overview) =>
        overview.GetVisualDescendants().OfType<TextBlock>().Single();

    private static IReadOnlyList<StackPanel> Chips(GraphEditorView graph) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(chip => chip.Classes.Contains(GraphEditorView.SlotChipClass))
            .ToList();
}
