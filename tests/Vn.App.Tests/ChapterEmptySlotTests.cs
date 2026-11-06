using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 탭의 빈 선택지 칸도 누르면 정보가 선다</b> — 카드 아래의 빈 점은 아직 이 칸에서 나가는 길이 없다는 뜻이다. 누르면
/// 그 칸을 고르고 어느 에피소드의 몇째 칸인지, 길을 더하는 곳([대본] 탭의 카드 아래 칸)을 말한다. 이어진 선택지 점은 전처럼
/// 그 간선을 고른다.
/// </summary>
public sealed class ChapterEmptySlotTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-empty-slot", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 빈_선택지_칸을_누르면_칸_정보가_서고_길을_더하는_곳을_말한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Canvas canvas = view.FindControl<Canvas>("GraphCanvas")!;

        // root는 아직 나가는 길이 없다 — 칸 셋이 다 비었다.
        Ellipse slot = Dot(canvas, "root", 1);
        Press(slot);

        Assert.True(view.FindControl<StackPanel>("SlotPanel")!.IsVisible);
        Assert.Equal("선택지 칸 2/3 — 비어 있음", view.FindControl<TextBlock>("SlotTitleText")!.Text);
        Assert.Contains("아직 없습니다", view.FindControl<TextBlock>("SlotDetailText")!.Text, StringComparison.Ordinal);
        Assert.Contains("[대본] 탭", view.FindControl<TextBlock>("SlotDetailText")!.Text, StringComparison.Ordinal);
        Assert.True(slot.StrokeThickness > 2, "고른 칸이 강조된다");
        Assert.False(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.Null(view.FindControl<Button>("SlotAddChoiceButton"));

        window.Close();
    });

    [Fact]
    public void 다른_것을_고르거나_빈_판을_누르면_칸_선택이_풀린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Canvas canvas = view.FindControl<Canvas>("GraphCanvas")!;

        Ellipse first = Dot(canvas, "root", 0);
        Press(first);

        // 다른 칸 — 앞의 칸 강조가 풀린다.
        Ellipse second = Dot(canvas, "a", 2);
        Press(second);
        Assert.Equal(1.6, first.StrokeThickness);
        Assert.True(second.StrokeThickness > 2);
        Assert.Equal("선택지 칸 3/3 — 비어 있음", view.FindControl<TextBlock>("SlotTitleText")!.Text);

        // 빈 판 — 패널이 접힌다.
        Press(canvas);
        Assert.False(view.FindControl<StackPanel>("SlotPanel")!.IsVisible);
        Assert.Equal(1.6, second.StrokeThickness);

        window.Close();
    });

    private static Ellipse Dot(Canvas canvas, string episodeId, int index) =>
        canvas.Children.OfType<Ellipse>()
            .Single(dot => dot.Tag as string == $"{ChapterGraphView.EmptySlotTag}{episodeId}#{index}");

    private static void Press(Control control)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }
}
