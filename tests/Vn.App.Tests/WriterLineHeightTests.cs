using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>큰 글자가 든 줄만 높아진다</b> (2026-11-23 소유자 — 「한줄에서 특정 글자의 글자 크기를 늘렸는데도, 그 줄의 위아래가 아닌 카드 내의 전체의 줄 간격이
/// 늘어나는 게 살짝 아쉬운데」 · writer-formatting WF-D18). 글 칸의 배치를 직접 재 줄마다의 높이를 본다. ⛔ 전에는 한 글자만 키워도 칸 전체의 줄 높이가
/// 그 글자의 비율로 커졌다.
/// ⚠ Avalonia를 올렸는데 이 시험이 깨지면 줄 높이 잡기가 바뀐 것이다 — <see cref="WriterTextPresenter"/>의 받침대(StrutSize)를 다시 본다.
/// </summary>
public sealed class WriterLineHeightTests
{
    private const double Size = 14;

    [Fact]
    public void 한_줄의_글자를_키우면_그_줄만_높아지고_다른_줄은_설정의_줄_높이다() => HeadlessUi.Run(() =>
    {
        // 14 × 170 % — 한글 글꼴(맑은 고딕 · Noto Sans CJK)의 자연 높이보다 넉넉하다. 좁으면 아래 시험.
        const double lineHeight = 23.8;
        const string text = "첫 줄 가나다 abc\n둘째 줄 큰 글자\n셋째 줄 마지막";
        int big = text.IndexOf('큰');

        WriterTextBox box = Show(text, lineHeight, [new BoxLook(big, 1, new WriterLook(Grow: 10))]);
        double[] heights = Heights(box);

        Assert.Equal(3, heights.Length);
        Assert.Equal(lineHeight, heights[0], 1);
        Assert.Equal(lineHeight, heights[2], 1);
        Assert.True(heights[1] > lineHeight + 5, $"큰 글자 줄 {heights[1]:0.0}");

        // 칸 전체는 큰 줄 몫만큼만 높다 — ⛔ 전처럼 모든 줄을 키웠다면 줄마다 (14 + 10) / 14배였다.
        double scaledAll = 3 * Math.Round(lineHeight * (Size + 10) / Size, 1);
        Assert.True(box.Presenter!.TextLayout.Height < scaledAll - 10, $"칸 {box.Presenter.TextLayout.Height:0.0} · 전처럼 {scaledAll:0.0}");

        // 보통 글자는 제 크기로 그려진다(받침대 크기가 아니다).
        foreach (ShapedTextRun run in box.Presenter.TextLayout.TextLines[0].TextRuns.OfType<ShapedTextRun>())
        {
            Assert.Equal(Size, run.Properties.FontRenderingEmSize);
        }

        Assert.Contains(box.Presenter.TextLayout.TextLines[1].TextRuns.OfType<ShapedTextRun>(), run => run.Properties.FontRenderingEmSize == Size + 10);
    });

    [Fact]
    public void 크기를_바꾼_글자가_없으면_줄_높이는_전처럼_설정_그대로다() => HeadlessUi.Run(() =>
    {
        const double lineHeight = 19.6;
        const string text = "첫 줄 가나다\n둘째 줄 굵은 글자\n셋째 줄";
        int bold = text.IndexOf('굵');

        WriterTextBox box = Show(text, lineHeight, [new BoxLook(bold, 2, new WriterLook(Bold: true))]);

        Assert.All(Heights(box), height => Assert.Equal(lineHeight, height, 1));
    });

    [Fact]
    public void 줄_간격을_좁혀도_보통_줄끼리는_높이가_같고_큰_글자_줄만_높다() => HeadlessUi.Run(() =>
    {
        const double lineHeight = 14; // 100 % — 한글 글꼴의 자연 높이보다 낮다.
        const string text = "abc only\n둘째 줄 큰 글자\n셋째 줄 한글\nxyz";
        int big = text.IndexOf('큰');

        WriterTextBox box = Show(text, lineHeight, [new BoxLook(big, 1, new WriterLook(Grow: 10))]);
        double[] heights = Heights(box);

        Assert.Equal(4, heights.Length);
        Assert.Equal(heights[0], heights[2], 1);
        Assert.Equal(heights[0], heights[3], 1);

        // 보통 줄은 보통 크기 한글 한 줄의 자연 높이다 — 좁힌 줄 간격보다 조금 높다(줄 높이를 정한 칸은 한글이 위아래로 넘쳐 겹친다).
        using var natural = new TextLayout("가A", new Typeface(box.FontFamily), Size, null);
        Assert.True(natural.Height > lineHeight, $"한글 한 줄 {natural.Height:0.0}");
        Assert.Equal(natural.Height, heights[0], 1);
        Assert.True(heights[1] > heights[0] + 5, $"큰 글자 줄 {heights[1]:0.0} · 보통 줄 {heights[0]:0.0}");
    });

    private static WriterTextBox Show(string text, double lineHeight, IReadOnlyList<BoxLook> looks)
    {
        var box = new WriterTextBox
        {
            Text = text,
            FontSize = Size,
            LineHeight = lineHeight,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Width = 400,
            Height = 200,
            Looks = looks
        };

        var window = new Window { Width = 420, Height = 220, Content = box };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(box.Presenter);
        return box;
    }

    private static double[] Heights(WriterTextBox box) =>
        box.Presenter!.TextLayout.TextLines.Select(line => line.Height).ToArray();
}
