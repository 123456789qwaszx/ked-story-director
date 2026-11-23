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

    /// <summary>
    /// 키우는 폭(pt) — 큰 글자의 줄이 넉넉한 줄 간격(170 · 200%)보다도 또렷이(5px 넘게) 높아질 만큼. 한글 한 줄 높이는 글꼴마다 다르다 — 리눅스 Noto
    /// Sans CJK는 글자 크기의 1.45배쯤, Windows 맑은 고딕은 1.33배, 맥 애플 산돌고딕은 1.2배다. ⛔ 2026-11-23까지 +10이었다 — 리눅스에서만 통과하고 Windows ·
    /// 맥 CI에서 큰 줄이 5px을 못 넘었다(24pt가 맑은 고딕 31.92 · 산돌고딕 28.8 — 줄 높이 28 · 23.8). 동작은 맞았다(큰 줄은 높아졌다).
    /// </summary>
    private const int Grow = 20;

    [Fact]
    public void 한_줄의_글자를_키우면_그_줄만_높아지고_다른_줄은_설정의_줄_높이다() => HeadlessUi.Run(() =>
    {
        // 14 × 170 % — 한글 글꼴(맑은 고딕 · 애플 산돌고딕 · Noto Sans CJK)의 한 줄 높이보다 넉넉하다. 좁으면 아래 시험.
        const double lineHeight = 23.8;
        const string text = "첫 줄 가나다 abc\n둘째 줄 큰 글자\n셋째 줄 마지막";
        int big = text.IndexOf('큰');

        WriterTextBox box = Show(text, lineHeight, [new BoxLook(big, 1, new WriterLook(Grow: Grow))]);
        double[] heights = Heights(box);

        Assert.Equal(3, heights.Length);
        Assert.Equal(lineHeight, heights[0], 1);
        Assert.Equal(lineHeight, heights[2], 1);
        Assert.True(heights[1] > lineHeight + 5, $"큰 글자 줄 {heights[1]:0.0}");

        // 칸 전체는 큰 줄 몫만큼만 높다 — ⛔ 전처럼 모든 줄을 키웠다면 줄마다 (14 + 20) / 14배였다.
        double scaledAll = 3 * Math.Round(lineHeight * (Size + Grow) / Size, 1);
        Assert.True(box.Presenter!.TextLayout.Height < scaledAll - 10, $"칸 {box.Presenter.TextLayout.Height:0.0} · 전처럼 {scaledAll:0.0}");

        // 보통 글자는 제 크기로 그려진다(받침대 크기가 아니다 — 보이지 않는 줄바꿈만 받침대 크기다 · WF-D24).
        foreach (ShapedTextRun run in box.Presenter.TextLayout.TextLines[0].TextRuns.OfType<ShapedTextRun>().Where(run => !ScriptFont.IsLineBreak(run.Text.Span)))
        {
            Assert.Equal(Size, run.Properties.FontRenderingEmSize);
        }

        Assert.Contains(box.Presenter.TextLayout.TextLines[1].TextRuns.OfType<ShapedTextRun>(), run => run.Properties.FontRenderingEmSize == Size + Grow);
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

        var grown = (WriterTextBox)Show(text, basis, [new BoxLook(text.IndexOf('큰'), 1, new WriterLook(Grow: Grow))]);
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

    /// <summary>
    /// 2026-11-23 소유자 — 「특정 에피소드내에서 글자 크기를 작게 줄여놨는데도, 줄 간격은 처음 생성될때 기준으로 가장 좁은 수치가 결정되어있다보니, 엄청 답답한
    /// 감이 있네」(writer-formatting WF-D24). 카드의 줄 간격은 줄마다 「적어도 이 높이」다 — 줄인 글자만 든 줄(꺾여 내려간 대사 · 줄인 영문 줄)과 빈 줄은 카드의
    /// 줄 간격까지 내려가고, 처음 크기 글자(화자 이름 · 안 줄인 줄)가 든 줄은 그 글자의 자연 높이에서 멈춘다. 그 카드에서 가장 낮게 내려갈 수 있는 줄은 줄인
    /// 크기의 가장 낮은 줄 높이이고, 그보다 좁은 줄 간격은 그 높이다.
    /// </summary>
    [Fact]
    public void 줄인_글자만_든_줄은_카드의_줄_간격까지_낮아지고_처음_크기_글자가_든_줄은_제_높이에서_멈춘다() => HeadlessUi.Run(() =>
    {
        const int shrink = -5;
        const string text = "하린: 작은 대사가 아주 길게 이어져서 줄이 꺾여 둘째 줄로 내려간다 작은 대사가 길게 이어진다 작은 대사가 아주 길게 이어져서 줄이 꺾여 " +
            "셋째 줄까지 내려간다 작은 대사가 길게 이어진다\n\n작은 글만 든 줄\nabc small\n보통 크기 줄";
        int body = text.IndexOf('작');
        BoxLook Small(int start, int end) => new(start, end - start, new WriterLook(Grow: shrink));
        List<BoxLook> looks =
        [
            Small(body, text.IndexOf('\n')),
            Small(text.IndexOf("작은 글만"), text.IndexOf("\nabc")),
            Small(text.IndexOf("abc"), text.IndexOf("\n보통"))
        ];
        var basis = new WriterBase(string.Empty, Size, LineSpacing: 170);
        var box = (WriterTextBox)Show(text, basis, looks, wrap: true);
        var face = new Typeface(box.FontFamily);
        double baseFloor = ScriptFont.MinLineHeight(face, Size);
        double smallFloor = ScriptFont.MinLineHeight(face, Size + shrink);

        // 줄 간격이 넉넉하면 전과 같다 — 모든 줄이 줄 간격의 높이.
        Assert.True(box.LineHeight > baseFloor);
        Assert.All(Heights(box), height => Assert.Equal(box.LineHeight, height, 1));

        // 가장 낮게 내려갈 수 있는 줄 = 줄인 크기의 가장 낮은 줄 높이.
        Assert.Equal(smallFloor, box.Presenter!.NarrowestLine()!.Value, 1);
        int lowest = ScriptFont.MinLineSpacingOf(basis, box.Presenter.NarrowestLine());
        Assert.True(lowest < ScriptFont.MinLineSpacingOf(basis) - 20, $"{lowest}% · 처음 크기 {ScriptFont.MinLineSpacingOf(basis)}%");

        // 그 값으로 — 줄인 글자만 든 줄 · 빈 줄은 그 높이, 처음 크기 글자가 든 줄은 그 글자의 자연 높이.
        ScriptFont.Apply(box, basis with { LineSpacing = lowest });
        Dispatcher.UIThread.RunJobs();
        double low = Math.Round(Size * lowest / 100.0, 1);
        TextLayout layout = box.Presenter.TextLayout;
        Assert.True(layout.TextLines.Count >= 6, $"줄 {layout.TextLines.Count}");   // 첫 줄이 꺾였다

        foreach (TextLine line in layout.TextLines)
        {
            string shown = text.Substring(line.FirstTextSourceIndex, Math.Min(line.Length, text.Length - line.FirstTextSourceIndex));

            if (shown.Contains("하린", StringComparison.Ordinal) || shown.Contains("보통", StringComparison.Ordinal))
            {
                // 처음 크기 글자의 자연 높이 — 그 줄에 놓인 글자 조각들의 가장 높은 윗몫 + 가장 깊은 아랫몫 + 줄 사이 몫(줄바꿈 조각은 뺀다 — 받침대
                // 크기다). 한글만 든 줄은 한글 글꼴만의 높이라 「가A」(영문 글꼴이 섞인 한 줄)보다 낮을 수 있다(Windows 맑은 고딕 18.62 · 「가A」
                // 18.75 — 리눅스 Noto는 영문 글꼴보다 위아래 모두 커서 같다). ⛔ 2026-11-23 CI — 처음에는 「가A」로, 다음에는 그 글을 따로 배치해
                // 쟀는데 따로 배치하면 문단의 기본 글꼴(Inter) 높이가 섞여 Windows에서 어긋났다. 줄인 줄의 높이(low)까지는 내려가지 않는다.
                double ascent = 0, descent = 0, gap = 0;

                foreach (ShapedTextRun run in line.TextRuns.OfType<ShapedTextRun>().Where(run => !ScriptFont.IsLineBreak(run.Text.Span)))
                {
                    ascent = Math.Max(ascent, -run.TextMetrics.Ascent);
                    descent = Math.Max(descent, run.TextMetrics.Descent);
                    gap = Math.Max(gap, run.TextMetrics.LineGap);
                }

                Assert.Equal(ascent + descent + gap, line.Height, 1);
                Assert.True(line.Height <= baseFloor + 0.05, $"처음 크기 줄 {line.Height:0.00} · 가장 낮은 줄 높이 {baseFloor:0.00}");
                Assert.True(line.Height > low + 1, $"처음 크기 줄 {line.Height:0.00} · 줄인 줄 {low:0.00}");
            }
            else
            {
                Assert.Equal(low, line.Height, 1);
            }
        }

        // 그보다 좁게 적힌 값은 가장 낮게 내려갈 수 있는 줄의 높이다(줄 간격 숫자도 거기서 멈춘다 — 처음 크기의 가장 낮은 줄 높이와 같은 규칙).
        ScriptFont.Apply(box, basis with { LineSpacing = ScriptFont.LowestCardLineSpacing });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(smallFloor, box.Presenter.TextLayout.TextLines[1].Height, 1);

        // 줄인 글자를 걷으면 전처럼 처음 크기의 가장 낮은 줄 높이다.
        box.Looks = [];
        Dispatcher.UIThread.RunJobs();
        Assert.All(Heights(box), height => Assert.Equal(baseFloor, height, 1));
        Assert.Null(box.Presenter.NarrowestLine());
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
    private static TextBox Show(string text, WriterBase basis, IReadOnlyList<BoxLook>? looks, bool wrap = false)
    {
        TextBox box = looks is null ? new TextBox() : new WriterTextBox { Looks = looks };
        ScriptFont.Apply(box, basis);
        Place(box, text, wrap);
        return box;
    }

    private static void Place(TextBox box, string text, bool wrap = false)
    {
        box.Text = text;
        box.AcceptsReturn = true;
        box.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        box.Width = 400;
        box.Height = 220;

        var window = new Window { Width = 420, Height = 240, Content = box };
        window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    private static double[] Heights(WriterTextBox box) => Heights(box.Presenter!.TextLayout);

    private static double[] Heights(TextLayout layout) => layout.TextLines.Select(line => line.Height).ToArray();
}
