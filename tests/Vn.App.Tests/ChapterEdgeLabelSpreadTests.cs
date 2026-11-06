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
    public void 문구를_줄여도_해금조건은_다_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        const string Long = "아주 길고 긴 선택지 문구가 여기 있어서 한 줄에 다 들어가지 않는다";
        session.Editor.AddChapterStat("ch01", "trust");
        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        session.Editor.AddEdge("ch01", "root", "b", conditionLabel: "신뢰높음", optionLabel: Long);
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<Canvas>("GraphCanvas")!;
        Border label = Assert.Single(Labels(canvas, "root", "b"));

        List<TextBlock> parts = ((StackPanel)label.Child!).Children.OfType<TextBlock>().ToList();
        Assert.Equal(TextTrimming.CharacterEllipsis, parts[0].TextTrimming);
        Assert.Equal("[신뢰높음]", parts[1].Text);
        Assert.Equal($"{Long} [신뢰높음]", ToolTip.GetTip(label));
    });

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
