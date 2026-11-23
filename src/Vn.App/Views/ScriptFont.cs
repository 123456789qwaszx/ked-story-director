using Avalonia.Controls;
using Avalonia.Media;
using Vn.App.Services;
using Vn.Authoring.Model;

namespace Vn.App.Views;

/// <summary>
/// <b>[대본] 판 카드 글 칸의 글꼴 · 글자 크기 · 줄 간격</b>(2026-11-18 소유자 — 「글꼴이나, 줄 사이의 간격, 폰트 크기 같은 것들을 조정할 수 있다면
/// 작가들이 좋아하겠습니다. 이런 것들을 설정에서」 · <c>docs/work-orders/writer-comfort-orders.md</c> WC5).
///
/// 이 PC의 앱 설정이다(<see cref="AppSettingsService.LoadScriptFont"/>). <see cref="GuideMode"/>와 같은 꼴: 처음 읽을 때 앱 설정에서 가져오고,
/// 바꾸면 기억하고 알린다. 줄 간격은 글자 크기에 대한 %다(워드 프로세서의 「줄 간격 160%」와 같은 말). 칸의 높이는 그대로다 — 글이 넘치면 칸 안에서 흐른다.
///
/// ⭐ <b>글꼴 · 글자 크기는 새 카드의 처음 값이다</b>(2026-11-23 소유자 — 「설정 내부에 대본 글칸속의 글꼴과 글자 크기는 이제 처음 생성시에 기본값으로만
/// 사용하는 게 맞겠습니다. 지금처럼 즉각 갱신되는게 아니라요」 · writer-formatting WF-D13). 카드를 만들 때 이 값이 카드에 적히고
/// (<see cref="DialogueNode.WriterBase"/> — 프로젝트 파일 · 같이 쓰는 사람에게도 같다), 뒤에 바꿔도 서 있는 카드는 그대로다. 값이 안 적힌 옛 카드는 파일을
/// 열 때 이 값으로 적힌다. ⛔ 그 전에는 셋 다 모든 칸이 바로 따랐다(WC5 — 서식이 생기기 전: 글꼴 · 크기는 이 PC의 눈에 맞추는 것이라 프로젝트에 적지
/// 않았다). 굵게 · 글자색(WF-D16 — 같은 날 소유자 「Bold로써 굵게 하는 걸 기본값으로 … 글자 색깔도 같이」)도 같은 규칙이다. <b>줄 간격도</b> 같다(같은 날
/// 소유자 — 「줄 간격을 새 카드의 기본값으로 바꿉시다」 · WF-D19 — ⛔ 그 전에는 줄 간격만 모든 칸이 바로 따랐다). 서 있는 카드의 줄 간격은 서식 도구로 바꾼다.
///
/// ⭐ <b>줄 높이는 그 카드 글꼴의 한글 한 줄 높이보다 낮지 않다</b>(같은 날 소유자 — 「줄 간격 최솟값을 당신이 권고한 대로 하겠습니다」 · WF-D19). 글자를 키운
/// 카드는 줄마다 높이를 따로 잡는데(WF-D18), 그러면 Avalonia 12.1.1은 한글이 든 줄을 그 글꼴의 한 줄 높이보다 낮게 만들지 못한다. 줄 간격이 그보다 좁으면
/// 키운 카드의 보통 줄만 높아져 다른 카드와 간격이 달라 보였다(소유자가 확인). 그래서 모든 칸의 줄 높이를 그 높이 밑으로 내리지 않는다 — 그보다 좁은 줄
/// 간격은 그 높이로 보인다(⛔ 그 전에는 한글이 위아래 줄과 조금 겹쳤다).
///
/// ⭐ <b>글자를 줄인 줄은 그 글자만큼 더 좁힐 수 있다</b>(같은 날 소유자 — 「특정 에피소드내에서 글자 크기를 작게 줄여놨는데도, 줄 간격은 처음 생성될때 기준으로
/// 가장 좁은 수치가 결정되어있다보니, 엄청 답답한 감이 있네」 · WF-D24). 카드의 줄 간격은 줄마다 「적어도 이 높이」다 — 줄인 글자만 든 줄은 그 높이까지
/// 내려가고, 기본 크기 글자(화자 이름 · 안 줄인 글)가 든 줄은 그 글자의 가장 낮은 높이에서 멈춘다. 그래서 그 카드의 가장 좁은 줄 간격은 <b>그 카드에서 가장
/// 낮게 내려갈 수 있는 줄</b>(<see cref="NarrowestLine"/>)로 잰다 — 100%보다 좁을 수 있다(<see cref="LowestCardLineSpacing"/>). ⛔ 그 전에는 카드의 처음 크기로만
/// 쟀다(글자를 줄여도 줄은 처음 크기의 높이였다).
/// </summary>
internal static class ScriptFont
{
    internal const double DefaultSize = 11;
    internal const int DefaultLineSpacing = 140;
    internal const double MinSize = 9;
    /// <summary>글자 크기의 끝(pt) — 2026-11-23 소유자 「설정에서 시작 글 크기를 28로 제한되어있는데 조금 더 상한을 높혀 주시겠습니까?」로 48(⛔ 그 전에는 28).</summary>
    internal const double MaxSize = 48;
    internal const int MinLineSpacing = 100;
    internal const int MaxLineSpacing = 250;

    /// <summary>
    /// 카드의 줄 간격(%)이 내려갈 수 있는 끝 — 글자를 줄인 줄은 100%보다 좁을 수 있다(WF-D24 · 처음 크기 9pt에서 6pt 줄인 글자가 50%쯤). 설정 창(새 카드)은
    /// 그대로 <see cref="MinLineSpacing"/>부터다. 손으로 고친 파일의 값도 여기로 당긴다.
    /// </summary>
    internal const int LowestCardLineSpacing = 40;

    private static (string Family, double Size, int LineSpacing)? _now;

    private static (string Family, double Size, int LineSpacing) Now => _now ??= Clamp(AppSettingsService.LoadScriptFont());

    /// <summary>글꼴 이름 — 비면 기본 글꼴(테마의 것).</summary>
    internal static string Family => Now.Family;

    internal static double Size => Now.Size;

    /// <summary>줄 간격 — 글자 크기에 대한 %.</summary>
    internal static int LineSpacing => Now.LineSpacing;

    private static (bool Bold, WriterRgb? Color)? _look;

    private static (bool Bold, WriterRgb? Color) Look => _look ??= LoadLook();

    private static (bool Bold, WriterRgb? Color) LoadLook()
    {
        (bool bold, string? color) = AppSettingsService.LoadScriptLook();
        return (bold, WriterRgb.Parse(color));
    }

    /// <summary>새 카드 글을 굵게(writer-formatting WF-D16).</summary>
    internal static bool Bold => Look.Bold;

    /// <summary>새 카드 글의 기본 글자색 — null이면 테마의 것(WF-D16).</summary>
    internal static WriterRgb? Color => Look.Color;

    /// <summary>새 카드에 적을 처음 글꼴 · 크기 · 굵게 · 글자색 · 줄 간격 — 지금 설정.</summary>
    internal static WriterBase CardBase => new(Family, Size, Bold, Color, LineSpacing);

    internal static double LineHeightOf(double size, int lineSpacing) => Math.Round(size * lineSpacing / 100.0, 1);

    /// <summary>
    /// 그 카드에서 가장 좁은 줄 간격(%) — 이보다 좁으면 줄 높이가 그 글꼴의 가장 낮은 줄 높이(<see cref="MinLineHeight"/>)에서 멈춰 숫자만 내려가고 화면은
    /// 그대로다. 서식 도구 · 설정의 줄 간격은 여기서 멈춘다(2026-11-23 소유자 — 「줄 사이 간격을 일정 수준 이하로 줄일 떄는, 숫자만 내려가고 동작 변화가 없는데」 →
    /// 「숫자가 최소값에서 멈추게 해주십시오」 · writer-formatting WF-D23). 처음 값이 안 적힌 카드는 지금 설정의 글꼴 · 크기 · 굵게로 잰다.
    /// <paramref name="narrowestLine"/>(px — 그 카드 글 칸에서 가장 낮게 내려갈 수 있는 줄 · <see cref="WriterTextPresenter.NarrowestLine"/>)이 더 낮으면 그
    /// 높이까지다 — 글자를 줄인 줄이 있는 카드(WF-D24). 100%보다 좁을 수 있다.
    /// </summary>
    internal static int MinLineSpacingOf(WriterBase? card, double? narrowestLine = null)
    {
        (string family, double size, bool bold) = card is null
            ? (Family, Size, Bold)
            : (card.Font, Math.Clamp(Math.Round(card.Size), MinSize, MaxSize), card.Bold);
        int floor = MinLineSpacingFor(family, size, bold);

        return narrowestLine is { } lowest && double.IsFinite(lowest) && lowest > 0
            ? Math.Min(floor, LineSpacingReaching(size, lowest))
            : floor;
    }

    /// <summary>그 글꼴 · 크기 · 굵게에서 가장 좁은 줄 간격(%) — 그 %의 줄 높이가 가장 낮은 줄 높이 이상인 가장 작은 정수(설정의 범위 안).</summary>
    internal static int MinLineSpacingFor(string family, double size, bool bold)
    {
        if (!double.IsFinite(size) || size <= 0)
        {
            return MinLineSpacing;
        }

        // 칸이 입는 것과 같은 글꼴로 잰다(Apply) — 이 PC에 없는 글꼴은 기본 글꼴이다(InstalledFonts · WF-D17).
        var typeface = new Typeface(
            InstalledFonts.Named(family) is { } installed ? new FontFamily(installed) : FontFamily.Default,
            FontStyle.Normal,
            bold ? FontWeight.Bold : FontWeight.Normal);
        return Math.Max(MinLineSpacing, LineSpacingReaching(size, MinLineHeight(typeface, size)));
    }

    /// <summary>그 크기에서 줄 높이가 <paramref name="lowest"/>(px) 이상인 가장 작은 정수 줄 간격(%) — <see cref="LowestCardLineSpacing"/> ~ <see cref="MaxLineSpacing"/>.</summary>
    private static int LineSpacingReaching(double size, double lowest)
    {
        int percent = Math.Max(LowestCardLineSpacing, (int)Math.Ceiling((lowest * 100 / size) - 1e-9));

        // 줄 높이는 0.1 단위로 반올림한다 — 모자라면 한 칸 더.
        while (percent < MaxLineSpacing && LineHeightOf(size, percent) < lowest)
        {
            percent++;
        }

        return Math.Min(percent, MaxLineSpacing);
    }

    /// <summary>
    /// 카드의 줄 간격(%) — 보이는 값: 적힌 값(안 적힌 카드는 지금 설정)을 그 카드에서 가장 좁은 값(<see cref="MinLineSpacingOf"/> — 글자를 줄인 줄이 있으면
    /// <paramref name="narrowestLine"/>까지)과 설정의 끝으로 당긴다.
    /// </summary>
    internal static int ShownLineSpacing(WriterBase? card, double? narrowestLine = null) =>
        Math.Clamp(Math.Max(card?.LineSpacing ?? LineSpacing, MinLineSpacingOf(card, narrowestLine)), LowestCardLineSpacing, MaxLineSpacing);

    /// <summary>글 칸의 줄 높이 — 줄 간격의 높이와 <see cref="MinLineHeight"/> 가운데 큰 쪽(WF-D19).</summary>
    internal static double LineHeightFor(Typeface typeface, double size, int lineSpacing) =>
        Math.Max(LineHeightOf(size, lineSpacing), MinLineHeight(typeface, size));

    /// <summary>
    /// 그 글꼴 · 크기에서 가장 낮은 줄 높이 — <b>한글 · 영문 한 줄 높이</b>(<see cref="PlainLine"/>)에, 글자를 키운 칸의 받침대(그 글꼴을 줄 높이에 맞춘 크기 ·
    /// <see cref="WriterTextPresenter"/>)가 그 줄의 윗몫 · 아랫몫 · 줄 사이 몫을 <b>저마다</b> 덮는 높이. 그래야 키운 칸의 보통 줄 · 빈 줄이 받침대 높이(= 줄 높이)
    /// 그대로다 — 한글 글꼴과 주 글꼴(Inter)은 윗몫 · 아랫몫의 비가 달라, 한 줄 높이만 맞추면 한글 줄이 받침대보다 조금 높았다. 0.1 단위로 올린다(내리면 한
    /// 줄 높이보다 낮아진다). 글꼴을 못 찾으면 한 줄 높이다.
    /// </summary>
    internal static double MinLineHeight(Typeface typeface, double size) => LineNeed(typeface, typeface, size);

    /// <summary>
    /// <paramref name="face"/> · <paramref name="size"/>의 글자가 든 줄이 받침대(<paramref name="strutFace"/> — 그 칸의 글꼴)로 겹치지 않는 가장 낮은 높이 —
    /// <see cref="MinLineHeight"/>를 글자마다의 글꼴 · 크기로(WF-D24). 한글 · 영문 한 줄로 재므로 영문만 든 줄도 같은 크기의 한글 줄과 같은 값이다.
    /// </summary>
    internal static double LineNeed(Typeface strutFace, Typeface face, double size)
    {
        LineMetrics plain = PlainLine(face, size);
        double lowest = plain.Height;

        if (Shares(strutFace) is { } share && share.Up > 0 && share.Down > 0)
        {
            double strut = Math.Max(plain.Ascent / share.Up, plain.Descent / share.Down);

            if (share.Gap > 0)
            {
                strut = Math.Max(strut, plain.LineGap / share.Gap);
            }

            lowest = Math.Max(lowest, strut * (share.Up + share.Down + share.Gap));
        }

        return Math.Ceiling(lowest * 10 - 1e-6) / 10;
    }

    /// <summary>
    /// 배치된 줄들 가운데 <b>가장 낮게 내려갈 수 있는 줄</b>의 높이(px · WF-D24) — 줄마다 그 줄의 글자 조각(줄바꿈만 든 조각은 뺀다 — 보이지 않고 칸이 받침대
    /// 크기로 그린다)이 겹치지 않는 높이(<see cref="LineNeed"/>) 가운데 큰 것, 그 가운데 가장 낮은 줄. 글자가 없는 줄(빈 줄)은 세지 않는다. 없으면 null.
    /// </summary>
    internal static double? NarrowestLine(Avalonia.Media.TextFormatting.TextLayout layout, Typeface strutFace)
    {
        double? narrowest = null;

        foreach (Avalonia.Media.TextFormatting.TextLine line in layout.TextLines)
        {
            double need = 0;

            foreach (Avalonia.Media.TextFormatting.ShapedTextRun run in line.TextRuns.OfType<Avalonia.Media.TextFormatting.ShapedTextRun>())
            {
                if (!IsLineBreak(run.Text.Span))
                {
                    need = Math.Max(need, LineNeed(strutFace, run.Properties.Typeface, run.Properties.FontRenderingEmSize));
                }
            }

            if (need > 0)
            {
                narrowest = Math.Min(narrowest ?? need, need);
            }
        }

        return narrowest;
    }

    /// <summary>줄바꿈 글자만 든 조각인가.</summary>
    internal static bool IsLineBreak(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return true;
        }

        foreach (char c in text)
        {
            if (c is not ('\n' or '\r' or '\u2028' or '\u2029' or '\u0085'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>글꼴(주 글꼴 — 대체 글꼴이 아니다)의 윗몫 · 아랫몫 · 줄 사이 몫 — 글자 크기 1에 대한 비. 못 찾으면 null.</summary>
    internal static (double Up, double Down, double Gap)? Shares(Typeface typeface)
    {
        if (ShareCache.TryGetValue(typeface, out (double Up, double Down, double Gap)? known))
        {
            return known;
        }

        (double Up, double Down, double Gap)? share = null;

        if (FontManager.Current.TryGetGlyphTypeface(typeface, out GlyphTypeface? glyphs))
        {
            FontMetrics metrics = glyphs.Metrics;
            double em = metrics.DesignEmHeight;
            share = em > 0 ? (-metrics.Ascent / em, metrics.Descent / em, Math.Max(0, metrics.LineGap / em)) : null;
        }

        ShareCache[typeface] = share;
        return share;
    }

    /// <summary>글꼴마다 한 번 — 글 칸을 배치할 때마다 줄마다 묻는다(<see cref="NarrowestLine"/>).</summary>
    private static readonly Dictionary<Typeface, (double Up, double Down, double Gap)?> ShareCache = new();

    private static readonly Dictionary<(Typeface, double), LineMetrics> PlainLines = new();

    /// <summary>
    /// 그 글꼴 · 크기의 <b>한글 · 영문 한 줄</b>의 높이 몫(줄 높이를 정하지 않았을 때) — 글꼴 · 크기마다 한 번 잰다. 한글은 대체 글꼴로 그려질 수 있어
    /// 실제로 배치해 그 줄의 글자들을 본다. 줄의 높이는 글자들의 가장 높은 윗몫 + 가장 깊은 아랫몫 + 가장 큰 줄 사이 몫이다(Avalonia 12.1.1
    /// <c>TextLineImpl.CreateLineMetrics</c>).
    /// </summary>
    internal static LineMetrics PlainLine(Typeface typeface, double size)
    {
        if (!PlainLines.TryGetValue((typeface, size), out LineMetrics metrics))
        {
            using var probe = new Avalonia.Media.TextFormatting.TextLayout("가A", typeface, size, null);
            double ascent = 0;
            double descent = 0;
            double gap = 0;

            foreach (Avalonia.Media.TextFormatting.ShapedTextRun run in probe.TextLines[0].TextRuns.OfType<Avalonia.Media.TextFormatting.ShapedTextRun>())
            {
                ascent = Math.Max(ascent, -run.TextMetrics.Ascent);
                descent = Math.Max(descent, run.TextMetrics.Descent);
                gap = Math.Max(gap, run.TextMetrics.LineGap);
            }

            PlainLines[(typeface, size)] = metrics = new LineMetrics(ascent, descent, gap);
        }

        return metrics;
    }

    /// <summary>한 줄의 높이 몫 — 윗몫 · 아랫몫 · 줄 사이 몫(px · 모두 0 이상).</summary>
    internal readonly record struct LineMetrics(double Ascent, double Descent, double LineGap)
    {
        internal double Height => Ascent + Descent + LineGap;
    }

    /// <summary>바뀌었다 — 서 있는 글 칸이 제 꼴을 바꾼다.</summary>
    internal static event Action? Changed;

    /// <summary>바꾸고 기억한다 — 범위 밖은 끝으로 당긴다. 같으면 아무것도 하지 않는다.</summary>
    internal static void Set(string? family, double size, int lineSpacing)
    {
        (string Family, double Size, int LineSpacing) next = Clamp(((family ?? string.Empty).Trim(), size, lineSpacing));

        if (next == Now)
        {
            return;
        }

        _now = next;
        AppSettingsService.SaveScriptFont(next.Family, next.Size, next.LineSpacing);
        Changed?.Invoke();
    }

    /// <summary>새 카드 글의 굵게 · 기본 글자색을 바꾸고 기억한다(WF-D16). 같으면 아무것도 하지 않는다.</summary>
    internal static void SetLook(bool bold, WriterRgb? color)
    {
        if ((bold, color) == Look)
        {
            return;
        }

        _look = (bold, color);
        AppSettingsService.SaveScriptLook(bold, color?.Hex);
        Changed?.Invoke();
    }

    /// <summary>기본값으로 — 기본 글꼴 · 11 · 140% · 보통 굵기 · 테마의 글자색.</summary>
    internal static void Reset()
    {
        Set(null, DefaultSize, DefaultLineSpacing);
        SetLook(bold: false, color: null);
    }

    /// <summary>글 칸에 지금 설정을 입힌다(설정 창의 미리 보기). 기본 글꼴이면 칸의 글꼴을 비워 테마의 것을 물려받는다.</summary>
    internal static void Apply(TextBox box) => Apply(box, Family, Size, Bold, Color, LineSpacing);

    /// <summary>
    /// 카드 글 칸에 그 카드의 처음 글꼴 · 크기 · 굵게 · 글자색 · 줄 간격(<see cref="DialogueNode.WriterBase"/>)을 입힌다. 처음 값이 안 적힌 카드는 지금 설정을
    /// 따른다(줄 간격만 안 적힌 카드는 줄 간격만). 크기 · 줄 간격은 설정과 같은 범위로 당긴다(손으로 고친 파일).
    /// </summary>
    internal static void Apply(TextBox box, WriterBase? card)
    {
        if (card is null)
        {
            Apply(box);
            return;
        }

        Apply(
            box,
            card.Font,
            Math.Clamp(Math.Round(card.Size), MinSize, MaxSize),
            card.Bold,
            card.Color,
            Math.Clamp(card.LineSpacing ?? LineSpacing, LowestCardLineSpacing, MaxLineSpacing));
    }

    private static void Apply(TextBox box, string family, double size, bool bold, WriterRgb? color, int lineSpacing)
    {
        // 이 PC에 없는 글꼴(같이 쓰는 사람이 고른 것 · 지운 글꼴)은 기본 글꼴로 — 이름을 넘기지 않는다(InstalledFonts · WF-D17).
        if (InstalledFonts.Named(family) is { } installed)
        {
            box.FontFamily = new FontFamily(installed);
        }
        else
        {
            box.ClearValue(TextBox.FontFamilyProperty);
        }

        box.FontSize = size;

        // 굵게 · 글자색(WF-D16) — 없으면 값을 두지 않는다(테마의 것을 물려받는다).
        if (bold)
        {
            box.FontWeight = FontWeight.Bold;
        }
        else
        {
            box.ClearValue(TextBox.FontWeightProperty);
        }

        // 줄 높이는 굵기까지 입은 뒤 그 글꼴로 잰다 — 한 줄 높이보다 낮지 않게(WF-D19). 서식 칸에는 줄 간격 그대로의 높이도 준다 — 글자를 줄인 줄은
        // 거기까지 내려간다(WF-D24 · WriterTextPresenter).
        box.LineHeight = LineHeightFor(new Typeface(box.FontFamily, box.FontStyle, box.FontWeight), size, lineSpacing);

        if (box is WriterTextBox writer)
        {
            writer.WantedLineHeight = LineHeightOf(size, lineSpacing);
        }

        if (color is { } rgb)
        {
            box.Foreground = WriterPalette.Brush(rgb);
        }
        else
        {
            box.ClearValue(TextBox.ForegroundProperty);
        }
    }

    private static (string Family, double Size, int LineSpacing) Clamp((string Family, double Size, int LineSpacing) value) =>
        (value.Family ?? string.Empty,
            double.IsFinite(value.Size) ? Math.Clamp(Math.Round(value.Size), MinSize, MaxSize) : DefaultSize,
            Math.Clamp(value.LineSpacing, MinLineSpacing, MaxLineSpacing));
}
