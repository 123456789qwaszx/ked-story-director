using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Vn.App.Services;

namespace Vn.App.Theming;

/// <summary>
/// <b>테마를 앱에 입힌다</b>(2026-11-26 · themes). 기본 테마(<see cref="ThemeCatalog.Classic"/>)의 값을 늘 깔고, 고른 테마의 값 · 컨트롤 모양을 그 위에
/// 얹는다 — 그래서 테마는 바꾸고 싶은 것만 적으면 된다. XAML은 같은 이름을 <c>{DynamicResource 이름}</c>으로, 코드는 <see cref="Skin"/>으로 꺼낸다.
///
/// 테마는 <b>켤 때 한 번</b> 입힌다(<see cref="ApplyAtStartup"/> · 화면 언어와 같다). 판의 카드 · 선은 코드가 지을 때 색을 꺼내 쓰므로, 켜 있는 동안
/// 바꾸면 이미 지은 것과 새로 짓는 것의 색이 갈린다 — 설정 창에서 고른 테마는 앱 설정에 들고 다시 켤 때 선다.
/// </summary>
internal static class ThemeService
{
    /// <summary>시험의 손잡이 — 값이 있으면 앱 설정 대신 이 이름의 테마로 켠다(화면 시험은 기본 테마의 모양을 잰다 · <c>TestProcessIsolation</c>).</summary>
    internal static string? StartupThemeOverride { get; set; }

    /// <summary>지금 입힌 테마.</summary>
    public static ThemeInfo Current { get; private set; } = ThemeCatalog.Classic;

    private static Application? _app;
    private static ResourceDictionary? _base;
    private static ResourceDictionary? _overlay;
    private static IStyle? _controls;

    private static readonly Dictionary<uint, IBrush> BrushCache = [];

    /// <summary>앱을 세울 때 — 앱 설정의 테마(없으면 <see cref="ThemeCatalog.Default"/>)를 입힌다.</summary>
    public static void ApplyAtStartup(Application app) =>
        Apply(app, ThemeCatalog.Resolve(StartupThemeOverride ?? AppSettingsService.LoadTheme()));

    /// <summary>테마를 입힌다 — 앞서 입힌 테마의 값 · 컨트롤 모양은 걷는다.</summary>
    public static void Apply(Application app, ThemeInfo theme)
    {
        if (!ReferenceEquals(_app, app))
        {
            (_app, _base, _overlay, _controls) = (app, null, null, null);
        }

        if (_overlay is not null)
        {
            app.Resources.MergedDictionaries.Remove(_overlay);
            _overlay = null;
        }

        if (_controls is not null)
        {
            app.Styles.Remove(_controls);
            _controls = null;
        }

        if (_base is null)
        {
            _base = (ResourceDictionary)AvaloniaXamlLoader.Load(ThemeCatalog.Classic.TokensUri);
            app.Resources.MergedDictionaries.Add(_base);
        }

        if (!ReferenceEquals(theme, ThemeCatalog.Classic))
        {
            // 뒤에 더한 것이 이긴다 — 고른 테마의 값이 기본 테마의 같은 이름을 덮는다.
            _overlay = (ResourceDictionary)AvaloniaXamlLoader.Load(theme.TokensUri);
            app.Resources.MergedDictionaries.Add(_overlay);
        }

        if (theme.HasControls)
        {
            _controls = (IStyle)AvaloniaXamlLoader.Load(theme.ControlsUri);
            app.Styles.Add(_controls);
        }

        lock (BrushCache)
        {
            BrushCache.Clear();
        }

        Current = theme;
    }

    /// <summary>테마가 정한 값 — 고른 테마에 없으면 기본 테마의 값. 어디에도 없으면 이름을 잘못 적은 것이다(<c>ThemeTokenTests</c>가 지킨다).</summary>
    internal static object Token(string key)
    {
        if (_overlay is not null && _overlay.TryGetResource(key, ThemeVariant.Light, out object? chosen) && chosen is not null)
        {
            return chosen;
        }

        if (_base is not null && _base.TryGetResource(key, ThemeVariant.Light, out object? fallback) && fallback is not null)
        {
            return fallback;
        }

        throw new KeyNotFoundException(
            _base is null
                ? $"Theme token '{key}' was read before a theme was applied (ThemeService.ApplyAtStartup)."
                : $"Theme token '{key}' is missing from Themes/Classic/Tokens.axaml.");
    }

    internal static Color ColorOf(string key) => Token(key) switch
    {
        Color color => color,
        ISolidColorBrush brush => brush.Color,
        object other => throw new InvalidCastException($"Theme token '{key}' is {other.GetType().Name}, not a Color.")
    };

    internal static double NumberOf(string key) => Token(key) switch
    {
        double number => number,
        object other => throw new InvalidCastException($"Theme token '{key}' is {other.GetType().Name}, not a Double.")
    };

    /// <summary>그림자 — 값은 CSS처럼 적은 글이다(<c>0 3 0 0 #B5D9EF</c>). <c>none</c>이면 그림자가 없다.</summary>
    internal static BoxShadows ShadowsOf(string key) =>
        Token(key) is string text && !string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            ? BoxShadows.Parse(text)
            : default;

    /// <summary>
    /// 색 하나에 붓 하나 — 같은 색을 다시 물으면 같은 붓이다. 시험이 「이 점의 붓이 그 붓인가」를 같은 것인지로 재고, 판은 같은 색의 붓을 카드마다
    /// 새로 만들지 않는다.
    /// </summary>
    internal static IBrush BrushOf(Color color)
    {
        lock (BrushCache)
        {
            if (!BrushCache.TryGetValue(color.ToUInt32(), out IBrush? brush))
            {
                brush = new SolidColorBrush(color);
                BrushCache[color.ToUInt32()] = brush;
            }

            return brush;
        }
    }
}
