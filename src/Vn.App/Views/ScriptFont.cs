using Avalonia.Controls;
using Avalonia.Media;
using Vn.App.Services;

namespace Vn.App.Views;

/// <summary>
/// <b>[대본] 판 카드 글 칸의 글꼴 · 글자 크기 · 줄 간격</b>(2026-11-18 소유자 — 「글꼴이나, 줄 사이의 간격, 폰트 크기 같은 것들을 조정할 수 있다면
/// 작가들이 좋아하겠습니다. 이런 것들을 설정에서」 · <c>docs/work-orders/writer-comfort-orders.md</c> WC5).
///
/// 이 PC의 앱 설정이다(<see cref="AppSettingsService.LoadScriptFont"/>) — 내 눈에 맞춘 글자가 팀의 프로젝트 파일을 바꾸면 안 된다.
/// <see cref="GuideMode"/>와 같은 꼴: 처음 읽을 때 앱 설정에서 가져오고, 바꾸면 기억하고 알린다 — 서 있는 글 칸이 판을 다시 짓지 않고 따른다.
/// 줄 간격은 글자 크기에 대한 %다(워드 프로세서의 「줄 간격 160%」와 같은 말). 칸의 높이는 그대로다 — 글이 넘치면 칸 안에서 흐른다.
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
    internal static double LineHeight => Math.Round(Size * LineSpacing / 100.0, 1);

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

    /// <summary>기본값으로 — 기본 글꼴 · 11 · 140%.</summary>
    internal static void Reset() => Set(null, DefaultSize, DefaultLineSpacing);

    /// <summary>글 칸에 지금 설정을 입힌다. 기본 글꼴이면 칸의 글꼴을 비워 테마의 것을 물려받는다.</summary>
    internal static void Apply(TextBox box)
    {
        if (Family.Length == 0)
        {
            box.ClearValue(TextBox.FontFamilyProperty);
        }
        else
        {
            box.FontFamily = new FontFamily(Family);
        }

        box.FontSize = Size;
        box.LineHeight = LineHeight;
    }

    private static (string Family, double Size, int LineSpacing) Clamp((string Family, double Size, int LineSpacing) value) =>
        (value.Family ?? string.Empty,
            double.IsFinite(value.Size) ? Math.Clamp(Math.Round(value.Size), MinSize, MaxSize) : DefaultSize,
            Math.Clamp(value.LineSpacing, MinLineSpacing, MaxLineSpacing));
}
