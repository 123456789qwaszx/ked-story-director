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

    private double _wantedLineHeight = double.NaN;

    /// <summary>
    /// 카드의 줄 간격 그대로의 줄 높이(px · <see cref="ScriptFont.Apply(TextBox, WriterBase?)"/>가 준다) — 칸의 줄 높이(<see cref="TextBlock.LineHeight"/>)는
    /// 카드 글꼴의 가장 낮은 줄 높이 밑으로 내려가지 않는데, 글자를 줄인 줄은 이 높이까지 내려간다(WF-D24). NaN이면 칸의 줄 높이 그대로다.
    /// </summary>
    internal double WantedLineHeight
    {
        get => _wantedLineHeight;
        set
        {
            if (_wantedLineHeight.Equals(value))
            {
                return;
            }

            _wantedLineHeight = value;

            if (Presenter is { } presenter)
            {
                presenter.WantedLineHeight = value;
            }
        }
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        Presenter = e.NameScope.Find<WriterTextPresenter>("PART_TextPresenter");

        if (Presenter is not null)
        {
            Presenter.Looks = _looks;
            Presenter.WantedLineHeight = _wantedLineHeight;
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

    /// <summary>이 칸 안에서 가장 큰 글자 차 — 0보다 크면 줄마다 높이를 따로 잡는다(<see cref="StrutSize"/>).</summary>
    private int MaxGrow => _looks.Count == 0 ? 0 : Math.Max(0, _looks.Max(look => look.Look.Grow));

    /// <summary>이 칸 안에서 가장 작은 글자 차 — 0보다 작으면 줄인 글자가 있다(WF-D24).</summary>
    private int MinGrow => _looks.Count == 0 ? 0 : Math.Min(0, _looks.Min(look => look.Look.Grow));

    private double _wantedLineHeight = double.NaN;

    /// <summary>카드의 줄 간격 그대로의 줄 높이 — <see cref="WriterTextBox.WantedLineHeight"/>.</summary>
    internal double WantedLineHeight
    {
        get => _wantedLineHeight;
        set
        {
            if (_wantedLineHeight.Equals(value))
            {
                return;
            }

            _wantedLineHeight = value;

            if (MinGrow < 0)
            {
                InvalidateTextLayout();
            }
        }
    }

    /// <summary>
    /// 이 칸에서 가장 낮게 내려갈 수 있는 줄의 높이(px · WF-D24) — 줄인 글자만 든 줄이 있으면 그 줄의 높이(<see cref="ScriptFont.NarrowestLine"/>), 없으면 null
    /// (카드 글꼴의 가장 낮은 줄 높이 그대로다). 서식 도구가 그 카드의 가장 좁은 줄 간격을 잰다. 줄 높이는 줄이 꺾이는 자리를 바꾸지 않아 지금 배치로 잰다.
    /// </summary>
    internal double? NarrowestLine()
    {
        if (MinGrow >= 0 || FontSize <= 0)
        {
            return null;
        }

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        double floor = ScriptFont.MinLineHeight(typeface, FontSize);
        return ScriptFont.NarrowestLine(TextLayout, typeface) is { } narrowest && narrowest < floor ? narrowest : null;
    }

    /// <summary>
    /// <b>큰 글자가 든 줄만 높인다</b>(2026-11-23 소유자 — 「한줄에서 특정 글자의 글자 크기를 늘렸는데도, 그 줄의 위아래가 아닌 카드 내의 전체의 줄 간격이 늘어나는
    /// 게 살짝 아쉬운데」 · writer-formatting WF-D18). Avalonia 12.1.1은 줄 높이를 정하면 모든 줄을 그 높이로 고정하고(큰 글자는 위아래 줄과 겹친다), 정하지
    /// 않으면 줄마다 「문단의 기본 글자와 그 줄의 글자들 가운데 가장 큰 것」의 자연 높이를 쓴다. 그래서 큰 글자가 있는 칸은 줄 높이를 정하지 않고, 문단의 기본
    /// 글자를 <b>자연 높이가 설정의 줄 높이와 꼭 같은 크기</b>로 둔다 — 화면에 나오지 않는 받침대다(글자는 모두 제 크기를 따로 입는다 · <see cref="Overrides"/>).
    /// 보통 줄은 받침대 높이(= 설정의 줄 높이)이고, 큰 글자가 든 줄만 그 글자의 자연 높이로 높아진다. ⛔ 그 전에는 칸 전체의 줄 높이를 가장 큰 글자의 비율로
    /// 키웠다. 받침대 크기를 못 재면(글꼴을 못 찾았다) null — 그때는 전처럼 칸 전체를 키운다.
    ///
    /// 글 칸의 줄 높이는 그 글꼴 · 크기의 가장 낮은 줄 높이(<see cref="ScriptFont.MinLineHeight"/> · WF-D19) 이상이라, 받침대가 보통 크기 한글 · 영문의 윗몫 ·
    /// 아랫몫을 다 덮는다 — 보통 줄과 빈 줄이 줄 높이 그대로이고, 키운 칸과 안 키운 칸의 보통 줄 간격이 같다(소유자가 확인한 어긋남 — 줄 간격이 한글 한 줄
    /// 높이보다 좁으면 키운 칸의 보통 줄만 높았다). 손으로 줄 높이를 그보다 낮춘 칸은 한글이 든 줄이 조금 높다.
    ///
    /// 줄인 글자가 있는 칸(WF-D24)은 받침대를 카드의 줄 간격 그대로의 높이(<see cref="WantedLineHeight"/>)로 낮춘다 — 받침대는 줄마다 「적어도 이 높이」라,
    /// 줄인 글자만 든 줄은 거기까지 내려가고 기본 크기 글자(화자 이름 · 안 줄인 글)가 든 줄은 그 글자의 자연 높이에서 멈춘다.
    /// </summary>
    private double? StrutSize(Typeface typeface, double lineHeight)
    {
        if (!double.IsFinite(lineHeight) || lineHeight <= 0 || FontSize <= 0 ||
            ScriptFont.Shares(typeface) is not { } share || share.Up + share.Down + share.Gap <= 0)
        {
            return null;
        }

        return lineHeight / (share.Up + share.Down + share.Gap);
    }

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

        TextLayout Build(double lineHeight, double paragraphSize, bool strut) => new(
            shown,
            typeface,
            paragraphSize,
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
            Overrides(shown, text.Length, caret, preeditLength, typeface, coverPlain: strut));

        int grow = MaxGrow;
        double line = LineHeight;

        // 줄인 글자가 있으면 받침대를 카드의 줄 간격 그대로까지 낮춘다(WF-D24) — 칸의 줄 높이는 카드 글꼴의 가장 낮은 줄 높이에서 멈춰 있다. 가장 낮게
        // 내려갈 수 있는 줄보다는 낮추지 않는다(그보다 낮추면 빈 줄만 낮아진다 — 줄 간격 숫자도 거기서 멈춘다). 줄 높이는 꺾이는 자리를 바꾸지 않으므로 한 번
        // 배치해 재고, 모자라면 그 높이로 다시 짓는다.
        if (MinGrow < 0 && double.IsFinite(line) && double.IsFinite(WantedLineHeight) && WantedLineHeight > 0 && WantedLineHeight < line &&
            StrutSize(typeface, WantedLineHeight) is { } lowStrut)
        {
            TextLayout low = Build(double.NaN, lowStrut, strut: true);
            double floor = Math.Min(line, ScriptFont.NarrowestLine(low, typeface) ?? line);

            if (WantedLineHeight >= floor - 1e-6)
            {
                return low;
            }

            low.Dispose();
            line = floor;
        }

        if ((grow > 0 || line < LineHeight) && StrutSize(typeface, line) is { } strutSize)
        {
            // 큰 글자가 든 줄만 높인다 · 줄인 글자만 든 줄은 낮춘다 — 줄 높이를 정하지 않고 문단의 기본 글자를 받침대로(StrutSize).
            return Build(double.NaN, strutSize, strut: true);
        }

        if (grow > 0 && double.IsFinite(line) && line > 0 && FontSize > 0)
        {
            line = Math.Round(line * (FontSize + grow) / FontSize, 1);
        }

        return Build(line, FontSize, strut: false);
    }

    /// <summary>「글에 맞춤」이 잰다 — 줄을 꺾지 않고 모양을 입혀 놓았을 때 가장 긴 줄의 폭.</summary>
    internal double UnwrappedWidth()
    {
        string text = Text ?? string.Empty;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);

        using var layout = new TextLayout(
            text, typeface, FontSize, Foreground, TextAlignment.Left, TextWrapping.NoWrap, null, null, FlowDirection,
            double.PositiveInfinity, double.PositiveInfinity, LineHeight, LetterSpacing, 0, FontFeatures,
            Overrides(text, text.Length, 0, 0, typeface));

        return layout.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// 구간마다의 모양 — 겹치지 않고 자리 차례. 조합 중인 글자는 그 앞 글자의 모양을 잇는다(넣으면 그렇게 된다 — 구간 끝에 이어 친 글자).
    /// </summary>
    /// <param name="shown">배치하는 글(조합 중인 글자 포함).</param>
    /// <param name="coverPlain">
    /// 보통 글자에도 제 크기를 입힌다 — 문단의 기본 글자가 받침대일 때(<see cref="StrutSize"/>). 줄바꿈 글자는 빼서 받침대로 둔다 — 보이지 않고, 제 크기를 입으면
    /// 줄인 글자만 든 줄도 기본 크기 높이가 된다(WF-D24 — 대사에 입힌 서식은 줄바꿈에 닿지 않는다).
    /// </param>
    private List<ValueSpan<TextRunProperties>> Overrides(string shown, int textLength, int caret, int preeditLength, Typeface typeface, bool coverPlain = false)
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

        bool Break(int shownIndex) => coverPlain && shownIndex < shown.Length && shown[shownIndex] is ('\n' or '\r');

        var spans = new List<ValueSpan<TextRunProperties>>();
        var cultures = new Dictionary<(bool, bool, string?), System.Globalization.CultureInfo?> { [(false, false, null)] = null };
        int run = 0;

        for (int at = 1; at <= shownLength; at++)
        {
            if (at < shownLength && LookAt(at) == LookAt(run) && Preedit(at) == Preedit(run) && Selected(at) == Selected(run) && Break(at) == Break(run))
            {
                continue;
            }

            WriterLook look = LookAt(run);
            bool preedit = Preedit(run);
            bool selected = Selected(run);

            if (Break(run))
            {
                // 받침대 크기 그대로 — 고른 범위의 줄바꿈도(보이지 않는다 · 고르는 동안 줄이 튀지 않게).
            }
            else if (!look.IsPlain || preedit || selected || coverPlain)
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
        // 이 PC에 없는 글꼴은 이름을 넘기지 않고 칸의 글꼴로 그린다(InstalledFonts — 넘기면 리눅스에서 메모리가 샜다 · WF-D17).
        Typeface face = look.Bold || look.Italic || look.Font is not null
            ? new Typeface(
                InstalledFonts.Named(look.Font) is { } font ? new FontFamily(font) : typeface.FontFamily,
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
            selectionForeground ?? (look.Color is { } color ? WriterPalette.Brush(color) : Foreground),
            look.Fill is { } fill ? WriterPalette.Brush(fill) : null,
            cultureInfo: culture,
            fontFeatures: FontFeatures);
    }
}

/// <summary>
/// <b>작가용 글 서식의 색</b> — RGB 값(<see cref="WriterRgb"/>)의 붓. 같은 색은 한 붓을 쓴다. 정해 둔 열 가지(이름 · 글자색 · 바탕색)는 모델의
/// <see cref="WriterSwatch"/>에 있다 — 파일에 RGB를 적으므로 옛 이름을 읽을 때도 그 값이 필요하다(writer-formatting WF-D14).
/// </summary>
internal static class WriterPalette
{
    private static readonly Dictionary<WriterRgb, IBrush> Brushes = new();

    public static Color ColorOf(WriterRgb rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);

    public static IBrush Brush(WriterRgb rgb)
    {
        if (!Brushes.TryGetValue(rgb, out IBrush? brush))
        {
            Brushes[rgb] = brush = new ImmutableSolidColorBrush(ColorOf(rgb));
        }

        return brush;
    }
}
