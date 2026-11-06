using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 탭의 선택지 문구는 서로 겹치지 않는다</b> — 한 카드에서 나간 선택지 여럿이 같은 카드로 모이면 선 가운데가 거의 한
/// 점이라 문구가 하나로 포개졌다. 겹치면 문구가 <b>제 선을 따라</b> 위아래로 비켜 선다 — 제 선 위에 있어야 어느 문구가 어느
/// 선인지 헷갈리지 않는다. 긴 문구는 줄이고(…) 마우스를 올리면 전체가 보인다.
/// </summary>
public sealed class ChapterEdgeLabelSpreadTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-edge-label", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 같은_카드로_모이는_선택지_셋의_문구는_겹치지_않고_저마다_제_선_위에_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        session.Editor.MoveEpisode("ch01", "b", 40, 420);
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "교무실로 간다");
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "선생님께 진행 상황도 보고한다");
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "카스미에게 마무리를 부탁하고 간다");
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<Canvas>("GraphCanvas")!;
        List<Border> labels = Labels(canvas, "root", "b");
        Assert.Equal(3, labels.Count);

        List<Rect> rects = labels.Select(Rect).ToList();
        for (int i = 0; i < rects.Count; i++)
        {
            for (int j = i + 1; j < rects.Count; j++)
            {
                Assert.False(rects[i].Intersects(rects[j]), $"문구 {i}와 {j}가 겹친다: {rects[i]} / {rects[j]}");
            }
        }

        // 저마다 제 선 위다 — 문구 가운데가 어떤 선택지 선 하나 위에 있다(각자 다른 선).
        List<Line> lines = canvas.Children.OfType<Line>().Where(line => line.StrokeThickness is > 0 and < 5 && line.Stroke is not null).ToList();
        var used = new HashSet<Line>();
        foreach (Rect rect in rects)
        {
            Line? own = lines.FirstOrDefault(line => !used.Contains(line) && Distance(rect.Center, line) < 1.5);
            Assert.NotNull(own);
            used.Add(own!);
        }
    });

    [Fact]
    public void 긴_문구는_줄이고_마우스를_올리면_전체가_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        const string Long = "아주 길고 긴 선택지 문구가 여기 있어서 한 줄에 다 들어가지 않는다";
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: Long);
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<Canvas>("GraphCanvas")!;
        Border label = Assert.Single(Labels(canvas, "root", "b"));

        Assert.True(label.DesiredSize.Width <= 160 + 8 + 1, $"문구가 너무 넓다: {label.DesiredSize.Width}");
        Assert.Equal(Long, ToolTip.GetTip(label));
    });

    [Fact]
    public void 문구_위에는_표시조건과_해금조건이_아래에는_스탯변화가_선다() => HeadlessUi.Run(() =>
    {
        // 판에서 선택지 문구만 보이면 그 길이 언제 보이고 언제 고를 수 있는지, 건너면 무엇이 바뀌는지 패널을 열어야 안다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        const string Long = "아주 길고 긴 선택지 문구가 여기 있어서 한 줄에 다 들어가지 않는다";
        session.Editor.AddChapterStat("ch01", "trust");
        session.Editor.AddChapterStat("ch01", "anger");
        session.Editor.UpdateChapterStat("ch01", "trust", displayName: "신뢰");
        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        session.Editor.AddChapterCondition("ch01", "신뢰있음", "trust >= 1");
        session.Editor.AddEdge("ch01", "root", "b", conditionLabel: "신뢰높음", optionLabel: Long, statChanges: "trust +2; anger -1");
        session.Editor.UpdateEdge("ch01", "root", "b", matchOptionLabel: Long, visibleConditionLabel: "신뢰있음");
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<Canvas>("GraphCanvas")!;
        Border label = Assert.Single(Labels(canvas, "root", "b"));
        List<Control> rows = ((StackPanel)label.Child!).Children.ToList();

        Assert.Equal(
            [ChapterGraphView.EdgeLabelRow.Conditions, ChapterGraphView.EdgeLabelRow.Option, ChapterGraphView.EdgeLabelRow.Stats],
            rows.Select(row => (ChapterGraphView.EdgeLabelRow)row.Tag!));
        Assert.Equal(["표시: 신뢰있음", "해금: 신뢰높음"], Texts(rows[0]));
        Assert.Equal(["신뢰 +2", "anger -1"], Texts(rows[2]));   // 표시 이름이 있으면 그것으로

        // 줄이는 것은 문구뿐이다 — 조건과 스탯은 다 보인다.
        var option = (TextBlock)rows[1];
        Assert.Equal(Long, option.Text);
        Assert.Equal(TextTrimming.CharacterEllipsis, option.TextTrimming);
        Assert.Equal(160, option.MaxWidth);

        string tip = (string)ToolTip.GetTip(label)!;
        Assert.Contains("표시조건: 신뢰있음", tip, StringComparison.Ordinal);
        Assert.Contains("해금조건: 신뢰높음", tip, StringComparison.Ordinal);
        Assert.Contains("스탯변화: 신뢰 +2 · anger -1", tip, StringComparison.Ordinal);
    });

    [Fact]
    public void 조건도_스탯도_없으면_문구_한_줄이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "그냥 간다");
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<Canvas>("GraphCanvas")!;
        Border label = Assert.Single(Labels(canvas, "root", "b"));

        Control row = Assert.Single(((StackPanel)label.Child!).Children);
        Assert.Equal(ChapterGraphView.EdgeLabelRow.Option, row.Tag);
        Assert.Equal("그냥 간다", ToolTip.GetTip(label));
    });

    private static List<string> Texts(Control row) =>
        ((StackPanel)row).Children.OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();

    private static List<Border> Labels(Canvas canvas, string from, string to) =>
        canvas.Children.OfType<Border>()
            .Where(border => border.Tag is ChapterGraphView.EdgeLabelMark mark &&
                             mark.FromEpisodeId == from && mark.ToEpisodeId == to)
            .ToList();

    private static Rect Rect(Border label) =>
        new(Canvas.GetLeft(label), Canvas.GetTop(label), label.DesiredSize.Width, label.DesiredSize.Height);

    /// <summary>점에서 선분까지의 거리.</summary>
    private static double Distance(Point point, Line line)
    {
        Vector segment = line.EndPoint - line.StartPoint;
        double length = (segment.X * segment.X) + (segment.Y * segment.Y);
        double t = length == 0 ? 0 : Math.Clamp(Vector.Dot(point - line.StartPoint, segment) / length, 0, 1);
        Point nearest = line.StartPoint + (segment * t);
        return Point.Distance(point, nearest);
    }
}
