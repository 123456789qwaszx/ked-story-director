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
using Stage = Vn.App.Views.GraphEditorView.CardStage;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판을 줄이면 카드마다 화면 글자 크기로 단계가 바뀐다</b>(design/board-zoom.md 3절 · 2026-11-26 소유자 — 「에피소드의 내용이 50%에서 사라지는 건
/// 너무 과하네요」). 글(화면 4px 이상) → 글줄(줄마다 막대 · 읽히는 제목) → 덩어리(1.5px 미만이거나 카드가 화면에서 20px보다 낮다). 오갈 때 문턱이 다르고,
/// 카드의 치수는 그대로다. ⛔ 1차 구현(배율 50% · 25%로 판 전체를 한 번에)은 걷혔다.
/// </summary>
public sealed class CardStageTests : IDisposable
{
    /// <summary>새 카드의 기본 글자 크기 — 시험이 카드에 적어 둔다(이 PC의 설정과 상관없게).</summary>
    private const double Base = 11;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-stage", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 문턱과_되돌아가는_문턱()
    {
        // 글 → 글줄은 4px 아래에서, 글줄 → 글은 4.4px부터. 덩어리는 1.5px 아래 · 카드가 화면에서 20px보다 낮을 때, 돌아오는 것은 1.65px · 22px부터.
        Assert.Equal(Stage.Text, GraphEditorView.NextStage(Stage.Text, 4.0, 100));
        Assert.Equal(Stage.Lines, GraphEditorView.NextStage(Stage.Text, 3.9, 100));
        Assert.Equal(Stage.Lines, GraphEditorView.NextStage(Stage.Lines, 4.3, 100));
        Assert.Equal(Stage.Text, GraphEditorView.NextStage(Stage.Lines, 4.4, 100));
        Assert.Equal(Stage.Block, GraphEditorView.NextStage(Stage.Lines, 1.4, 100));
        Assert.Equal(Stage.Block, GraphEditorView.NextStage(Stage.Text, 5, 19));
        Assert.Equal(Stage.Block, GraphEditorView.NextStage(Stage.Block, 1.6, 100));
        Assert.Equal(Stage.Lines, GraphEditorView.NextStage(Stage.Block, 1.7, 100));
        Assert.Equal(Stage.Block, GraphEditorView.NextStage(Stage.Block, 3, 21));
        Assert.Equal(Stage.Text, GraphEditorView.NextStage(Stage.Block, 5, 100));
    }

    [Fact]
    public void 줄이면_글_글줄_덩어리로_가고_키우면_그대로_돌아온다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Size size = Card(graph, root).Bounds.Size;

        // 40% — 11 × 0.4 = 4.4px. 글 그대로다(⛔ 1차 구현은 50%에서 글을 걷었다).
        graph.ZoomTo(0.4);
        Settle(graph);
        Assert.Equal(Stage.Text, graph.CardStageOf(root));
        Assert.Equal(1d, WritingBox(graph, root).Opacity);

        // 30% — 3.3px. 글줄: 글 칸은 투명하고 누름을 받지 않으며, 머리에 화면 12px 이상의 제목, 글 칸 자리에 막대(화자 머리는 따로).
        graph.ZoomTo(0.3);
        Settle(graph);
        Assert.Equal(Stage.Lines, graph.CardStageOf(root));
        TextBox box = WritingBox(graph, root);
        Assert.Equal(0d, box.Opacity);
        Assert.False(box.IsHitTestVisible);
        Border overview = Overview(graph, root)!;
        Assert.True(overview.IsEffectivelyVisible);
        TextBlock title = overview.GetVisualDescendants().OfType<TextBlock>().First(text => text.Classes.Contains("card-title") && text.IsEffectivelyVisible);
        Assert.Equal(HeaderTitle(graph, root).Text, title.Text);
        Assert.True(title.FontSize * 0.3 >= GraphEditorView.CardTitleMinPx - 0.01);
        Assert.False(Bars(overview, GraphEditorView.CardBarsTag).Data!.Bounds.Width <= 0, "본문 막대가 없다");
        Assert.False(Bars(overview, GraphEditorView.CardSpeakerBarsTag).Data!.Bounds.Width <= 0, "화자 막대가 없다");
        AssertSize(size, Card(graph, root));

        // 10% — 1.1px · 카드 높이도 화면 20px 아래. 덩어리.
        graph.ZoomTo(0.1);
        Settle(graph);
        Assert.Equal(Stage.Block, graph.CardStageOf(root));
        Assert.False(title.IsEffectivelyVisible);
        AssertSize(size, Card(graph, root));

        graph.ZoomTo(1);
        Settle(graph);
        Assert.Equal(Stage.Text, graph.CardStageOf(root));
        Assert.Equal(1d, box.Opacity);
        Assert.True(box.IsHitTestVisible);
        Assert.False(overview.IsVisible);
        AssertSize(size, Card(graph, root));
    });

    [Fact]
    public void 문턱_근처에서는_오던_단계에_머문다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        double near = 4.2 / Base;   // 4.2px — 글 → 글줄 문턱(4)과 되돌아가는 문턱(4.4) 사이

        graph.ZoomTo(near);
        Settle(graph);
        Assert.Equal(Stage.Text, graph.CardStageOf(root));   // 글에서 왔다

        graph.ZoomTo(0.3);
        Settle(graph);
        graph.ZoomTo(near);
        Settle(graph);
        Assert.Equal(Stage.Lines, graph.CardStageOf(root));   // 글줄에서 왔다
    });

    [Fact]
    public void 선택지_칸은_글줄이면_빈_칸의_더하기를_걷고_덩어리면_점까지_걷는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        IReadOnlyList<StackPanel> chips = Chips(graph, "root");
        Border plus = chips.SelectMany(chip => chip.Children).OfType<Border>().First(part => part.Tag as string == GraphEditorView.SlotPlaceholderTag);
        Ellipse knob = chips[0].Children.OfType<Ellipse>().Single();

        graph.ZoomTo(0.3);
        Settle(graph);
        Assert.Equal(0d, plus.Opacity);
        Assert.False(plus.IsHitTestVisible);
        Assert.Equal(1d, knob.Opacity);
        Assert.True((knob.RenderTransform as ScaleTransform)?.ScaleX * knob.Width * 0.3 >= GraphEditorView.MarkFloorPx - 0.01);   // 점은 화면 4px 이상

        graph.ZoomTo(0.1);
        Settle(graph);
        Assert.Equal(0d, knob.Opacity);

        graph.ZoomTo(1);
        Settle(graph);
        Assert.Equal(1d, knob.Opacity);
        Assert.True(plus.Opacity > 0);
        Assert.True(plus.IsHitTestVisible);
        Assert.Null(knob.RenderTransform);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>하이어아키 픽스처를 [대본] 탭으로 열고, root 카드의 기본 글자 크기를 11로 적은 뒤 화자 머리가 있는 대사를 쓴다.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string root = CardId(session, "root");
        session.Editor.SetWriterBase([root], basis => basis with { Size = Base });
        Settle(graph);

        TextBox box = WritingBox(graph, root);
        box.Focus();
        box.Text = "라루: 오늘따라 바람이 유난히 차갑다.\n윌로: 겉옷은?";
        Dispatcher.UIThread.RunJobs();
        graph.CommitLiveWriting();
        graph.FindControl<Border>("WritingFocusSink")!.Focus();
        Settle(graph);
        Assert.Equal(Base, graph.CardBaseFont(root));
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

    private static Avalonia.Controls.Shapes.Path Bars(Border overview, string tag) =>
        overview.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(path => path.Tag as string == tag);

    /// <summary>그 에피소드의 선택지 칸들 — 칸 번호 차례대로(칸의 Tag는 「에피소드ID#칸」이다).</summary>
    private static IReadOnlyList<StackPanel> Chips(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(chip => chip.Classes.Contains(GraphEditorView.SlotChipClass) &&
                           (chip.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true)
            .OrderBy(chip => (string)chip.Tag!, StringComparer.Ordinal)
            .ToList();
}
