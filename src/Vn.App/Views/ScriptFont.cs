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
/// 열 때 이 값으로 적힌다. <b>줄 간격만</b> 모든 카드 글 칸이 판을 다시 짓지 않고 바로 따른다. ⛔ 그 전에는 셋 다 모든 칸이 바로 따랐다(WC5 — 서식이
/// 생기기 전: 글꼴 · 크기는 이 PC의 눈에 맞추는 것이라 프로젝트에 적지 않았다). 굵게 · 글자색(WF-D16 — 같은 날 소유자 「Bold로써 굵게 하는 걸 기본값으로 …
/// 글자 색깔도 같이」)도 같은 규칙이다.
/// </summary>
internal static class ScriptFont
{
    internal const double DefaultSize = 11;
    internal const int DefaultLineSpacing = 140;
    internal const double MinSize = 9;
    internal const double MaxSize = 28;
    internal const int MinLineSpacing = 100;
    internal const int MaxLineSpacing = 250;

    private static (string Family, double Size, int LineSpacing)? _now;

    private static (string Family, double Size, int LineSpacing) Now => _now ??= Clamp(AppSettingsService.LoadScriptFont());

    /// <summary>글꼴 이름 — 비면 기본 글꼴(테마의 것).</summary>
    internal static string Family => Now.Family;

    internal static double Size => Now.Size;

    /// <summary>줄 간격 — 글자 크기에 대한 %.</summary>
    internal static int LineSpacing => Now.LineSpacing;

    /// <summary>한 줄의 높이(px) — 글자 크기 × 줄 간격.</summary>
    internal static double LineHeight => LineHeightOf(Size);

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

    /// <summary>새 카드에 적을 처음 글꼴 · 크기 · 굵게 · 글자색 — 지금 설정.</summary>
    internal static WriterBase CardBase => new(Family, Size, Bold, Color);

    private static double LineHeightOf(double size) => Math.Round(size * LineSpacing / 100.0, 1);

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
    internal static void Apply(TextBox box) => Apply(box, Family, Size, Bold, Color);

    /// <summary>
    /// 카드 글 칸에 그 카드의 처음 글꼴 · 크기(<see cref="DialogueNode.WriterBase"/>)와 이 PC의 줄 간격을 입힌다. 처음 값이 안 적힌 카드는 지금 설정을 따른다.
    /// 크기는 설정과 같은 범위로 당긴다(손으로 고친 파일).
    /// </summary>
    internal static void Apply(TextBox box, WriterBase? card)
    {
        if (card is null)
        {
            Apply(box);
            return;
        }

        Apply(box, card.Font, Math.Clamp(Math.Round(card.Size), MinSize, MaxSize), card.Bold, card.Color);
    }

    private static void Apply(TextBox box, string family, double size, bool bold, WriterRgb? color)
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
        box.LineHeight = LineHeightOf(size);

        // 굵게 · 글자색(WF-D16) — 없으면 값을 두지 않는다(테마의 것을 물려받는다).
        if (bold)
        {
            box.FontWeight = FontWeight.Bold;
        }
        else
        {
            box.ClearValue(TextBox.FontWeightProperty);
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
