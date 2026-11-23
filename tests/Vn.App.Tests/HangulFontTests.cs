using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>한글도 기울고 굵어진다</b> (2026-11-23 소유자 — 「한글 글꼴이 기울여 지지 않는 건 어쩔 수 없는걸까?」 · writer-formatting WF-D17).
/// 앱의 기본 글꼴(Inter)에 한글이 없어 한글은 대체 글꼴로 그려지고, Avalonia 12.1.1은 대체 글꼴에는 가짜 기울임을 하지 않는다. 한글 범위를 한글 글꼴에
/// 먼저 맡기면(<see cref="HangulFonts"/>) 기울임을 청한 한글이 비스듬히 그려진다. 글자를 실제로 그려 잉크로 잰다 — 위 절반의 잉크가 아래 절반보다
/// 오른쪽에 있으면 기운 것이다. ⛔ 맡기기 전에는 기울임을 청해도 기울기가 보통 글자와 똑같았다.
/// ⚠ Avalonia를 올렸는데 이 시험이 깨지면 글꼴 고르기가 바뀐 것이다 — <see cref="HangulFonts"/>를 다시 본다.
/// </summary>
public sealed class HangulFontTests
{
    [Fact]
    public void 기본_글꼴에서_기울임을_청한_한글이_비스듬히_그려진다() => HeadlessUi.Run(() =>
    {
        (double lean, _) = Ink(new Typeface(FontFamily.Default), "한글");
        (double italic, _) = Ink(new Typeface(FontFamily.Default, FontStyle.Italic), "한글");

        Assert.True(italic - lean > 3, $"기울기 — 보통 {lean:0.00}, 기울임 {italic:0.00}");
    });

    [Fact]
    public void 굵게를_청한_한글은_더_굵고_보통_한글은_한글_글꼴로_그려진다() => HeadlessUi.Run(() =>
    {
        (_, double ink) = Ink(new Typeface(FontFamily.Default), "한글");
        (_, double bold) = Ink(new Typeface(FontFamily.Default, weight: FontWeight.Bold), "한글");
        Assert.True(bold > ink * 1.15, $"잉크 — 보통 {ink:0}, 굵게 {bold:0}");

        // 보통 한글은 맡긴 한글 글꼴 가운데 이 PC에 있는 것으로 그려진다.
        using var layout = new TextLayout("한글", new Typeface(FontFamily.Default), 20, Brushes.Black);
        var run = Assert.IsType<ShapedTextRun>(layout.TextLines[0].TextRuns.First());
        Assert.NotEmpty(HangulFonts.Installed);
        Assert.Equal(HangulFonts.Installed[0], run.GlyphRun.GlyphTypeface.FamilyName);
    });

    /// <summary>
    /// 기울이고 굵힌 한글을 번갈아 거듭 그려도 메모리가 늘지 않는다 — 기본 글꼴(한글 글꼴에 맡긴 길) · 이 PC의 한글 글꼴(이름으로) · <b>이 PC에 없는 글꼴을
    /// 고른 서식</b>(같이 쓰는 사람이 고른 것 — 글 칸이 기본 글꼴로 그린다). ⛔ 없는 글꼴 이름을 글꼴 관리자에 넘기면 리눅스에서는 비슷한 글꼴이 대신 오고
    /// Avalonia 12.1.1이 가짜 기울임 글꼴을 캐시에서 못 찾아 그릴 때마다 글꼴 파일을 통째로 다시 읽었다(번갈아 그리면 한 바퀴에 수십 ~ 수백 MB · 화면 시험
    /// 전체가 메모리 부족으로 죽었다).
    /// </summary>
    [Fact]
    public void 기울이고_굵힌_한글을_번갈아_거듭_그려도_메모리가_늘지_않는다() => HeadlessUi.Run(() =>
    {
        // 이 PC에 없는 한글 글꼴 이름 — 같이 쓰는 사람의 PC에서 고른 것(리눅스는 이 이름에 비슷한 한글 글꼴을 대신 내준다 — 샌 길).
        string missing = new[] { "Malgun Gothic", "Apple SD Gothic Neo", "NanumGothic", "Noto Sans KR" }.First(name => InstalledFonts.Named(name) is null);
        var box = new WriterTextBox { Text = "한글 가나다", FontSize = 14, Width = 300, Height = 60 };
        var window = new Avalonia.Controls.Window { Width = 320, Height = 100, Content = box };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        void Round(int round)
        {
            foreach (FontStyle style in new[] { FontStyle.Normal, FontStyle.Italic })
            {
                foreach (FontWeight weight in new[] { FontWeight.Normal, FontWeight.Bold })
                {
                    foreach (FontFamily family in new[] { FontFamily.Default, new FontFamily(HangulFonts.Installed[0]) })
                    {
                        using var layout = new TextLayout($"한글 {round} 가나다", new Typeface(family, style, weight), 14, Brushes.Black);
                        _ = layout.TextLines[0].TextRuns.Count;
                    }

                    box.Looks = [new BoxLook(0, 2, new WriterLook(Bold: weight == FontWeight.Bold, Italic: style == FontStyle.Italic, Font: missing))];
                    _ = box.Presenter!.TextLayout.TextLines.Count;
                }
            }
        }

        static long Megabytes()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576;
        }

        Round(0);
        Round(1);
        long before = Megabytes();

        for (int round = 2; round < 10; round++)
        {
            Round(round);
        }

        long after = Megabytes();
        window.Close();
        Assert.True(after - before < 60, $"메모리 — {before}MB → {after}MB");
    });

    /// <summary>카드 글 칸에서 서식 도구로 기울임을 입힌 한글도 기운다 — 서식을 그리는 글 칸(<see cref="WriterTextBox"/>)이 같은 길로 그린다.</summary>
    [Fact]
    public void 글_칸에서_기울임을_입힌_한글이_비스듬히_그려진다() => HeadlessUi.Run(() =>
    {
        double Lean(bool italic)
        {
            var box = new WriterTextBox
            {
                Text = "한글한글",
                FontSize = 40,
                Width = 260,
                Height = 80,
                Foreground = Brushes.Black,
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                Looks = italic ? [new BoxLook(0, 4, new WriterLook(Italic: true))] : []
            };
            var window = new Avalonia.Controls.Window { Width = 300, Height = 120, Content = box };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            (double lean, _) = Measure(260, 80, context => box.Presenter!.TextLayout.Draw(context, new Point(0, 10)));
            window.Close();
            return lean;
        }

        double plain = Lean(italic: false);
        double slanted = Lean(italic: true);
        Assert.True(slanted - plain > 3, $"기울기 — 보통 {plain:0.00}, 기울임 {slanted:0.00}");
    });

    /// <summary>글자를 그려 (위 절반 잉크의 가로 무게중심 − 아래 절반의 것, 잉크 총량).</summary>
    private static (double Lean, double Ink) Ink(Typeface face, string text)
    {
        using var layout = new TextLayout(text, face, 48, Brushes.Black);
        return Measure(200, 80, context => layout.Draw(context, new Point(10, 5)));
    }

    private static (double Lean, double Ink) Measure(int width, int height, Action<DrawingContext> draw)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));

        using (DrawingContext context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, width, height));
            draw(context);
        }

        var pixels = new byte[width * height * 4];
        nint buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(pixels.Length);

        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), buffer, pixels.Length, width * 4);
            System.Runtime.InteropServices.Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }

        (double Center, double Ink) Half(int fromY, int toY)
        {
            double sum = 0, weight = 0;

            for (int y = fromY; y < toY; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int at = ((y * width) + x) * 4;
                    double ink = 255 - ((pixels[at] + pixels[at + 1] + pixels[at + 2]) / 3.0);
                    sum += x * ink;
                    weight += ink;
                }
            }

            return (weight == 0 ? 0 : sum / weight, weight);
        }

        (double top, double topInk) = Half(0, height / 2);
        (double bottom, double bottomInk) = Half(height / 2, height);
        return (top - bottom, topInk + bottomInk);
    }
}
