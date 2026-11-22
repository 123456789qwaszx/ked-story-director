using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using Vn.Authoring.Model;

namespace Vn.App.Views;

/// <summary>글 칸의 글자 자리 <c>[Start, Start + Length)</c>에 입힐 모양 — 글 칸 좌표다(화자 · 줄바꿈을 포함한 칸의 글).</summary>
internal readonly record struct BoxLook(int Start, int Length, WriterLook Look)
{
    public int End => Start + Length;
}

/// <summary>
/// <b>작가용 글 서식을 그리는 카드 글 칸</b> (2026-11-22 소유자 · <c>docs/plans/writer-formatting.md</c>) — Avalonia의 <see cref="TextBox"/> 그대로다.
/// 입력 · 커서 · 고른 범위 · 한글 조합 · 되돌리기는 손대지 않고, 글자를 배치하는 겹(<see cref="WriterTextPresenter"/>)만 바꿔 구간마다 굵게 · 색 · 글꼴 ·
/// 크기를 입힌다. Avalonia가 조합 중인 글자에 밑줄을 긋는 바로 그 길(글자 배치에 넘기는 구간별 모양)이다.
///
/// 틀(템플릿)은 App.axaml의 <c>WriterTextBoxTheme</c> — Fluent 테마의 TextBox 틀(Avalonia 12.1.1 <c>Themes.Fluent/Controls/TextBox.xaml</c>)을 XAML 그대로
/// 옮기고 글자 겹만 바꿨다. 바탕 테마(BasedOn)에서 모양(마우스 올림 · 초점 · 꺼짐)을 이어 받는다. ⛔ 첫 판(2026-11-22)은 이 틀을 코드로 지었는데 윈도에서
/// Enter 뒤 첫 한글을 칠 때 칸이 맨 위로 스크롤됐다(소유자 — dev에는 없음). 스타일 선택자가 <see cref="TextBox"/>로 보게 스타일 키는 TextBox다.
/// </summary>
internal sealed class WriterTextBox : TextBox
{
    private IReadOnlyList<BoxLook> _looks = [];

    public WriterTextBox()
    {
        // 틀은 App.axaml의 「WriterTextBoxTheme」 — Fluent TextBox 틀을 그대로 옮기고 글자 겹만 바꾼 것(코드로 지은 틀은 윈도 한글 입력에서 칸이 맨 위로 튀었다).
        if (Application.Current?.TryGetResource("WriterTextBoxTheme", null, out object? theme) == true && theme is ControlTheme writerTheme)
        {
            Theme = writerTheme;
        }
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>입힐 모양 — 칸의 글이 바뀌면 부르는 쪽이 다시 준다.</summary>
    internal IReadOnlyList<BoxLook> Looks
    {
        get => _looks;
        set
        {
            _looks = value;

            if (Presenter is { } presenter)
            {
                presenter.Looks = value;
            }
        }
    }

    /// <summary>글자 겹 — 틀이 서기 전에는 null.</summary>
    internal WriterTextPresenter? Presenter { get; private set; }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        Presenter = e.NameScope.Find<WriterTextPresenter>("PART_TextPresenter");

        if (Presenter is not null)
        {
            Presenter.Looks = _looks;
        }

        base.OnApplyTemplate(e);
    }
}

/// <summary>
/// <b>구간마다 모양을 입혀 글자를 배치하는 겹</b> — <see cref="TextPresenter.CreateTextLayout"/>만 바꾼다. 모양이 없으면 바탕 그대로다(서식 없는 카드는 지금과
/// 바이트까지 같은 배치). 모양이 있으면 같은 글 · 같은 폭 · 같은 줄 높이로 짓되, 구간마다 굵기 · 기울임 · 취소선 · 글자색 · 바탕색 · 글꼴 · 크기를 넘긴다.
/// 고른 글자의 글자색(테마가 주면)과 조합 중인 글자의 밑줄은 바탕과 같이 위에 얹는다. 커서 · 고르기 · 누른 자리는 이 배치를 그대로 쓰므로 굵은 글자도 맞는다.
/// </summary>
public sealed class WriterTextPresenter : TextPresenter
{
    private IReadOnlyList<BoxLook> _looks = [];

    internal IReadOnlyList<BoxLook> Looks
    {
        get => _looks;
        set
        {
            if (_looks.Count == 0 && value.Count == 0)
            {
                return;
            }

            _looks = value;
            InvalidateTextLayout();
        }
    }

    /// <summary>
    /// 이 칸 안에서 가장 큰 글자 차 — 줄 높이를 그만큼 키운다. 줄 높이가 고정(설정의 줄 간격)이라 큰 글자 줄만 키울 수 없어 칸 전체가 그 비율이다.
    /// </summary>
    private int MaxGrow => _looks.Count == 0 ? 0 : Math.Max(0, _looks.Max(look => look.Look.Grow));

    protected override TextLayout CreateTextLayout()
    {
        if (_looks.Count == 0 || PasswordChar != default)
        {
            return base.CreateTextLayout();
        }

        // 폭 · 높이의 끝은 바탕이 재 둔 것을 그대로 쓴다(그 값은 바탕 안에 숨어 있어 바탕의 배치에서 읽는다).
        double maxWidth;
        double maxHeight;

        using (TextLayout plain = base.CreateTextLayout())
        {
            maxWidth = plain.MaxWidth;
            maxHeight = plain.MaxHeight;
        }

        string text = Text ?? string.Empty;
        string? preedit = PreeditText;
        int caret = Math.Clamp(CaretIndex, 0, text.Length);
        int preeditLength = string.IsNullOrEmpty(preedit) ? 0 : preedit.Length;
        string shown = preeditLength > 0 ? text[..caret] + preedit + text[caret..] : text;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);

        double lineHeight = LineHeight;
        int grow = MaxGrow;

        if (grow > 0 && double.IsFinite(lineHeight) && lineHeight > 0 && FontSize > 0)
        {
            lineHeight = Math.Round(lineHeight * (FontSize + grow) / FontSize, 1);
        }

        return new TextLayout(
            shown,
            typeface,
            FontSize,
            Foreground,
            TextAlignment,
            TextWrapping,
            null,
            null,
            FlowDirection,
            maxWidth,
            maxHeight,
            lineHeight,
            LetterSpacing,
            0,
            FontFeatures,
            Overrides(text.Length, caret, preeditLength, typeface));
    }

    /// <summary>「글에 맞춤」이 잰다 — 줄을 꺾지 않고 모양을 입혀 놓았을 때 가장 긴 줄의 폭.</summary>
    internal double UnwrappedWidth()
    {
        string text = Text ?? string.Empty;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);

        using var layout = new TextLayout(
            text, typeface, FontSize, Foreground, TextAlignment.Left, TextWrapping.NoWrap, null, null, FlowDirection,
            double.PositiveInfinity, double.PositiveInfinity, LineHeight, LetterSpacing, 0, FontFeatures,
            Overrides(text.Length, 0, 0, typeface));

        return layout.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// 구간마다의 모양 — 겹치지 않고 자리 차례. 조합 중인 글자는 그 앞 글자의 모양을 잇는다(넣으면 그렇게 된다 — 구간 끝에 이어 친 글자).
    /// </summary>
    private List<ValueSpan<TextRunProperties>> Overrides(int textLength, int caret, int preeditLength, Typeface typeface)
    {
        var looks = new WriterLook[textLength];
        Array.Fill(looks, WriterLook.Plain);

        foreach (BoxLook look in _looks)
        {
            for (int at = Math.Max(0, look.Start); at < Math.Min(textLength, look.End); at++)
            {
                looks[at] = look.Look;
            }
        }

        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        IBrush? selectionForeground = preeditLength == 0 && ShowSelectionHighlight && end > start ? SelectionForegroundBrush : null;
        int shownLength = textLength + preeditLength;

        WriterLook LookAt(int shownIndex)
        {
            if (shownIndex < caret || preeditLength == 0)
            {
                return looks[shownIndex];
            }

            if (shownIndex < caret + preeditLength)
            {
                return caret > 0 ? looks[caret - 1] : WriterLook.Plain;
            }

            return looks[shownIndex - preeditLength];
        }

        bool Preedit(int shownIndex) => preeditLength > 0 && shownIndex >= caret && shownIndex < caret + preeditLength;

        bool Selected(int shownIndex) => selectionForeground is not null && shownIndex >= start && shownIndex < end;

        var spans = new List<ValueSpan<TextRunProperties>>();
        var cultures = new Dictionary<(bool, bool, string?), System.Globalization.CultureInfo?> { [(false, false, null)] = null };
        int run = 0;

        for (int at = 1; at <= shownLength; at++)
        {
            if (at < shownLength && LookAt(at) == LookAt(run) && Preedit(at) == Preedit(run) && Selected(at) == Selected(run))
            {
                continue;
            }

            WriterLook look = LookAt(run);
            bool preedit = Preedit(run);
            bool selected = Selected(run);

            if (!look.IsPlain || preedit || selected)
            {
                spans.Add(new ValueSpan<TextRunProperties>(run, at - run, Properties(look, typeface, preedit, selected ? selectionForeground : null, CultureOf(look, cultures))));
            }

            run = at;
        }

        return spans;
    }

    /// <summary>
    /// 글꼴 모양(굵기 · 기울임 · 글꼴)마다 다른 문화권 — <b>한글이 앞 구간의 글꼴을 물려받지 않게</b>. 주 글꼴(Inter)에 한글이 없어 한글은 대체 글꼴(맑은 고딕 ·
    /// Noto Sans CJK)로 그려지는데, Avalonia는 바로 앞 구간이 쓴 대체 글꼴이 그 글자를 그릴 수 있으면 그것을 다시 쓴다(글꼴이 들쭉날쭉하지 않게). 그러면
    /// 보통 글 다음의 굵은 한글이 보통 굵기로, 굵은 글 다음의 보통 한글이 굵게 나온다. 한글처럼 문화권을 타는 글자는 앞 구간과 문화권이 다르면 물려받지 않고
    /// 제 굵기 · 기울임으로 대체 글꼴을 새로 찾는다(Avalonia 12.1.1 <c>TextCharacters.CreateShapeableRun</c>). 보통 모양은 바탕과 같은 null이다.
    /// </summary>
    private static System.Globalization.CultureInfo? CultureOf(WriterLook look, Dictionary<(bool, bool, string?), System.Globalization.CultureInfo?> cultures)
    {
        (bool, bool, string?) key = (look.Bold, look.Italic, look.Font);

        if (!cultures.TryGetValue(key, out System.Globalization.CultureInfo? culture))
        {
            culture = Cultures[(cultures.Count - 1) % Cultures.Length];
            cultures[key] = culture;
        }

        return culture;
    }

    /// <summary>모양마다 줄 문화권 — 한국어를 앞에 둔다(한자도 한국 글꼴로 찾는다).</summary>
    private static readonly System.Globalization.CultureInfo[] Cultures =
        new[] { "ko-KR", "ko-KP", "ko", "en-US", "en-GB", "en" }
            .Select(name =>
            {
                try
                {
                    return System.Globalization.CultureInfo.GetCultureInfo(name);
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    return null;
                }
            })
            .OfType<System.Globalization.CultureInfo>()
            .DistinctBy(culture => culture.Name)
            .DefaultIfEmpty(System.Globalization.CultureInfo.InvariantCulture)
            .ToArray();

    private GenericTextRunProperties Properties(WriterLook look, Typeface typeface, bool preedit, IBrush? selectionForeground, System.Globalization.CultureInfo? culture)
    {
        Typeface face = look.Bold || look.Italic || look.Font is not null
            ? new Typeface(
                look.Font is { } font ? new FontFamily(font) : typeface.FontFamily,
                look.Italic ? FontStyle.Italic : typeface.Style,
                look.Bold ? FontWeight.Bold : typeface.Weight,
                typeface.Stretch)
            : typeface;

        TextDecorationCollection? decorations = (look.Strike, preedit) switch
        {
            (true, true) => new TextDecorationCollection(TextDecorations.Strikethrough.Concat(TextDecorations.Underline)),
            (true, false) => TextDecorations.Strikethrough,
            (false, true) => TextDecorations.Underline,
            _ => null
        };

        return new GenericTextRunProperties(
            face,
            Math.Max(1, FontSize + look.Grow),
            decorations,
            selectionForeground ?? (look.Color is { } color ? WriterPalette.Text(color) : Foreground),
            look.Fill is { } fill ? WriterPalette.Fill(fill) : null,
            cultureInfo: culture,
            fontFeatures: FontFeatures);
    }
}

/// <summary>
/// <b>작가용 글 서식의 색</b> — 정해 둔 목록(<see cref="WriterColor"/>)의 실제 색. 글자색은 흰 카드 바탕에서 읽히는 진한 색, 바탕색은 검은 글자가 읽히는 옅은 색이다.
/// </summary>
internal static class WriterPalette
{
    private static readonly Dictionary<WriterColor, (string Name, Color Text, Color Fill)> Colors = new()
    {
        [WriterColor.Red] = ("빨강", Color.FromRgb(0xD3, 0x2F, 0x2F), Color.FromRgb(0xFF, 0xCD, 0xD2)),
        [WriterColor.Orange] = ("주황", Color.FromRgb(0xE6, 0x5C, 0x00), Color.FromRgb(0xFF, 0xE0, 0xB2)),
        [WriterColor.Yellow] = ("노랑", Color.FromRgb(0xB2, 0x8A, 0x00), Color.FromRgb(0xFF, 0xF5, 0x9D)),
        [WriterColor.Green] = ("초록", Color.FromRgb(0x2E, 0x7D, 0x32), Color.FromRgb(0xC8, 0xE6, 0xC9)),
        [WriterColor.Teal] = ("청록", Color.FromRgb(0x00, 0x79, 0x6B), Color.FromRgb(0xB2, 0xEB, 0xF2)),
        [WriterColor.Blue] = ("파랑", Color.FromRgb(0x15, 0x65, 0xC0), Color.FromRgb(0xBB, 0xDE, 0xFB)),
        [WriterColor.Purple] = ("보라", Color.FromRgb(0x6A, 0x1B, 0x9A), Color.FromRgb(0xE1, 0xBE, 0xE7)),
        [WriterColor.Pink] = ("분홍", Color.FromRgb(0xC2, 0x18, 0x5B), Color.FromRgb(0xF8, 0xBB, 0xD0)),
        [WriterColor.Brown] = ("갈색", Color.FromRgb(0x6D, 0x4C, 0x41), Color.FromRgb(0xD7, 0xCC, 0xC8)),
        [WriterColor.Gray] = ("회색", Color.FromRgb(0x75, 0x75, 0x75), Color.FromRgb(0xE0, 0xE0, 0xE0))
    };

    private static readonly Dictionary<WriterColor, IBrush> TextBrushes = Colors.ToDictionary(pair => pair.Key, pair => (IBrush)new ImmutableSolidColorBrush(pair.Value.Text));

    private static readonly Dictionary<WriterColor, IBrush> FillBrushes = Colors.ToDictionary(pair => pair.Key, pair => (IBrush)new ImmutableSolidColorBrush(pair.Value.Fill));

    public static IReadOnlyList<WriterColor> All { get; } = Enum.GetValues<WriterColor>();

    public static string Name(WriterColor color) => Colors[color].Name;

    public static IBrush Text(WriterColor color) => TextBrushes[color];

    public static IBrush Fill(WriterColor color) => FillBrushes[color];
}
