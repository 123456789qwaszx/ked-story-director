using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>큰 글자가 든 줄만 높아진다</b> (2026-11-23 소유자 — 「한줄에서 특정 글자의 글자 크기를 늘렸는데도, 그 줄의 위아래가 아닌 카드 내의 전체의 줄 간격이
/// 늘어나는 게 살짝 아쉬운데」 · writer-formatting WF-D18). 글 칸의 배치를 직접 재 줄마다의 높이를 본다. ⛔ 전에는 한 글자만 키워도 칸 전체의 줄 높이가
/// 그 글자의 비율로 커졌다. 줄 간격이 좁아도 키운 카드와 아닌 카드의 보통 줄 간격이 같다(WF-D19 — 줄 높이는 그 글꼴의 가장 낮은 줄 높이 밑으로 내리지
/// 않는다).
/// ⚠ Avalonia를 올렸는데 이 시험이 깨지면 줄 높이 잡기가 바뀐 것이다 — <see cref="WriterTextPresenter"/>의 받침대(StrutSize)와
/// <see cref="ScriptFont.MinLineHeight"/>를 다시 본다.
/// </summary>
public sealed class WriterLineHeightTests
{
    private const double Size = 14;

    [Fact]
    public void 한_줄의_글자를_키우면_그_줄만_높아지고_다른_줄은_설정의_줄_높이다() => HeadlessUi.Run(() =>
    {
        // 14 × 170 % — 한글 글꼴(맑은 고딕 · Noto Sans CJK)의 한 줄 높이보다 넉넉하다. 좁으면 아래 시험.
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

    /// <summary>
    /// 2026-11-23 소유자 — 「그런 카드에서 글자를 키우면, 순간적으로 전체 카드의 줄이 설정보다 높아지는 문제가 있습니다. 이 탓에 키운 글자가 있는 카드와 없는
    /// 카드의 보통 줄 간격이 달라보이네요」 → 「줄 간격 최솟값을 당신이 권고한 대로」(WF-D19). 카드의 처음 값을 입는 길(<see cref="ScriptFont.Apply(TextBox, WriterBase?)"/>)
    /// 그대로 세 칸을 세운다 — 글자를 키운 칸 · 굵게만 입힌 칸 · 서식 없는 칸. 영문만 든 줄 · 빈 줄 · 한글 줄 모두 같은 높이이고 큰 글자 줄만 높다.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(140)]
    [InlineData(200)]
    public void 줄_간격이_좁아도_글자를_키운_카드와_아닌_카드의_보통_줄_간격이_같다(int lineSpacing) => HeadlessUi.Run(() =>
    {
        const string text = "abc only\n\n둘째 줄 큰 글자\n셋째 줄 한글\nxyz";
        var basis = new WriterBase(string.Empty, Size, LineSpacing: lineSpacing);

        var grown = (WriterTextBox)Show(text, basis, [new BoxLook(text.IndexOf('큰'), 1, new WriterLook(Grow: 10))]);
        var bolded = (WriterTextBox)Show(text, basis, [new BoxLook(text.IndexOf('셋'), 2, new WriterLook(Bold: true))]);
        TextBox plain = Show(text, basis, looks: null);

        // 줄 높이 — 줄 간격의 높이 이상이고 한글 한 줄 높이 이상. 100 %(한글 한 줄보다 좁다)이면 그 글꼴의 가장 낮은 줄 높이다.
        double line = grown.LineHeight;
        Assert.Equal(line, plain.LineHeight);
        Assert.True(line >= Math.Round(Size * lineSpacing / 100.0, 1));

        using (var natural = new TextLayout("가A", new Typeface(grown.FontFamily), Size, null))
        {
            Assert.True(line >= natural.Height, $"줄 높이 {line:0.00} · 한글 한 줄 {natural.Height:0.00}");
        }

        if (lineSpacing == 100)
        {
            Assert.Equal(ScriptFont.MinLineHeight(new Typeface(grown.FontFamily), Size), line);
        }

        double[] heights = Heights(grown);
        Assert.Equal(5, heights.Length);
        Assert.All(new[] { 0, 1, 3, 4 }, row => Assert.Equal(line, heights[row], 2));
        Assert.True(heights[2] > line + 5, $"큰 글자 줄 {heights[2]:0.00} · 줄 높이 {line:0.00}");

        Assert.All(Heights(bolded), height => Assert.Equal(line, height, 2));
        Assert.All(Heights(plain.GetVisualDescendants().OfType<TextPresenter>().Single().TextLayout), height => Assert.Equal(line, height, 2));
    });

    /// <summary>줄 높이를 손으로 정한 서식 칸.</summary>
    private static WriterTextBox Show(string text, double lineHeight, IReadOnlyList<BoxLook> looks)
    {
        var box = new WriterTextBox { LineHeight = lineHeight, FontSize = Size, Looks = looks };
        Place(box, text);
        Assert.NotNull(box.Presenter);
        return box;
    }

    /// <summary>카드의 처음 값을 입은 칸 — 모양이 있으면 서식 칸, null이면 보통 TextBox(서식 없는 카드).</summary>
    private static TextBox Show(string text, WriterBase basis, IReadOnlyList<BoxLook>? looks)
    {
        TextBox box = looks is null ? new TextBox() : new WriterTextBox { Looks = looks };
        ScriptFont.Apply(box, basis);
        Place(box, text);
        return box;
    }

    private static void Place(TextBox box, string text)
    {
        box.Text = text;
        box.AcceptsReturn = true;
        box.TextWrapping = TextWrapping.NoWrap;
        box.Width = 400;
        box.Height = 220;

        var window = new Window { Width = 420, Height = 240, Content = box };
        window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    private static double[] Heights(WriterTextBox box) => Heights(box.Presenter!.TextLayout);

    private static double[] Heights(TextLayout layout) => layout.TextLines.Select(line => line.Height).ToArray();
}
