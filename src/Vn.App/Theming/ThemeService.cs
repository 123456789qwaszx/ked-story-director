using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Vn.App.Services;

namespace Vn.App.Theming;

/// <summary>
/// <b>테마를 앱에 입힌다</b>(2026-11-26 · themes). 겹이 넷이다 — 뒤의 겹이 앞의 같은 이름을 덮는다:
/// <list type="number">
/// <item>기본 테마(<see cref="ThemeCatalog.Classic"/>)의 값 — 늘 깔린다. 그래서 테마는 바꾸고 싶은 것만 적으면 된다.</item>
/// <item>고른 앱의 테마(「내 테마」면 그 밑바탕)의 값과 컨트롤 모양.</item>
/// <item>작가의 「내 테마」 파일(theme.json)에 적힌 값(<see cref="UserThemes"/>).</item>
/// <item>셈한 값 — 글자 크기 단계(<c>TextXxs</c> ~ <c>TextLg</c>)와 곁글의 진하기(<c>Dim45</c> ~ <c>Dim70</c>)를 <see cref="Skin.FontScale"/> · <see cref="Skin.DimLift"/>에서.</item>
/// </list>
/// XAML은 같은 이름을 <c>{DynamicResource 이름}</c>으로, 코드는 <see cref="Skin"/>으로 꺼낸다.
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

    /// <summary>「내 테마」 파일에서 읽지 못한 것 — 켤 때 상태줄과 테마 폴더의 <c>theme-problems.txt</c>로 알린다. 앱에 든 테마면 비어 있다.</summary>
    public static IReadOnlyList<string> Problems { get; private set; } = [];

    /// <summary>「내 테마」 파일에 적힌 그대로의 값 — 이름 → 글(「내 테마」를 복사할 때 그대로 옮긴다 · 그림 파일 이름 · 글꼴 이름도).</summary>
    public static IReadOnlyDictionary<string, string> UserWritten { get; private set; } = new Dictionary<string, string>();

    /// <summary>글자 크기 단계 — 기본 테마의 크기.</summary>
    private static readonly (string Key, double Size)[] TextSteps =
        [("TextXxs", 9), ("TextXs", 10), ("TextSm", 11), ("TextMd", 12), ("TextLg", 14)];

    /// <summary>곁글의 진하기 단계 — 기본 테마의 투명도.</summary>
    private static readonly (string Key, double Opacity)[] DimSteps =
        [("Dim45", 0.45), ("Dim50", 0.5), ("Dim55", 0.55), ("Dim60", 0.6), ("Dim65", 0.65), ("Dim70", 0.7)];

    private static Application? _app;
    private static ResourceDictionary? _base;
    private static ResourceDictionary? _overlay;
    private static ResourceDictionary? _user;
    private static ResourceDictionary? _computed;
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
            (_app, _base, _overlay, _user, _computed, _controls) = (app, null, null, null, null, null);
        }

        foreach (ResourceDictionary? layer in new[] { _overlay, _user, _computed })
        {
            if (layer is not null)
            {
                app.Resources.MergedDictionaries.Remove(layer);
            }
        }

        (_overlay, _user, _computed) = (null, null, null);

        if (_controls is not null)
        {
            app.Styles.Remove(_controls);
            _controls = null;
        }

        if (_base is null)
        {
            _base = (ResourceDictionary)AvaloniaXamlLoader.Load(ThemeCatalog.Classic.TokensUri);
            ParseShadows(_base);
            app.Resources.MergedDictionaries.Add(_base);
        }

        ThemeInfo builtIn = theme.BuiltIn;

        if (!ReferenceEquals(builtIn, ThemeCatalog.Classic))
        {
            _overlay = (ResourceDictionary)AvaloniaXamlLoader.Load(builtIn.TokensUri);
            ParseShadows(_overlay);
            app.Resources.MergedDictionaries.Add(_overlay);
        }

        if (builtIn.HasControls)
        {
            _controls = (IStyle)AvaloniaXamlLoader.Load(builtIn.ControlsUri);
            app.Styles.Add(_controls);
        }

        Problems = [];
        UserWritten = new Dictionary<string, string>();

        if (theme.UserFolder is not null)
        {
            UserThemes.Loaded loaded = UserThemes.Load(theme, key => Existing(app, key));
            _user = loaded.Resources;
            Problems = loaded.Problems;
            UserWritten = loaded.Written;
            app.Resources.MergedDictionaries.Add(_user);
            UserThemes.WriteProblems(theme, loaded.Problems);
        }

        lock (BrushCache)
        {
            BrushCache.Clear();
        }

        _computed = new ResourceDictionary();
        foreach ((string key, double size) in TextSteps)
        {
            _computed[key] = Skin.Font(size);
        }

        foreach ((string key, double opacity) in DimSteps)
        {
            _computed[key] = Skin.Dim(opacity);
        }

        app.Resources.MergedDictionaries.Add(_computed);
        Current = theme;
    }

    /// <summary>지금까지 깐 겹(앱 · 테마)에서 그 이름의 값 — 「내 테마」가 그림의 깔기(되풀이 · 늘이기)를 앱의 그림에서 이어 받는다.</summary>
    private static object? Existing(Application app, string key) =>
        app.TryGetResource(key, ThemeVariant.Light, out object? value) ? value : null;

    /// <summary>그림자는 글로 적는다(<c>0 3 0 0 #C9DCE8</c>) — XAML이 쓰도록 그림자 값으로 바꿔 둔다. <c>none</c>이면 그림자가 없다.</summary>
    private static void ParseShadows(ResourceDictionary layer)
    {
        foreach (ThemeKey key in ThemeKeys.All.Where(key => key.Kind == ThemeValueKind.Shadow))
        {
            if (layer.TryGetValue(key.Name, out object? value) && value is string text)
            {
                layer[key.Name] = ParseShadow(text);
            }
        }
    }

    /// <summary>그림자 글 → 그림자. <c>none</c> · 빈 글이면 그림자가 없다.</summary>
    internal static BoxShadows ParseShadow(string text) =>
        string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            ? default
            : BoxShadows.Parse(text);

    /// <summary>테마가 정한 값 — 뒤의 겹부터 찾는다. 어디에도 없으면 이름을 잘못 적은 것이다(<c>ThemeTests</c>가 지킨다).</summary>
    internal static object Token(string key)
    {
        foreach (ResourceDictionary? layer in new[] { _computed, _user, _overlay, _base })
        {
            if (layer is not null && layer.TryGetValue(key, out object? value) && value is not null)
            {
                return value;
            }
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

    internal static CornerRadius CornerOf(string key) => Token(key) switch
    {
        CornerRadius corner => corner,
        double all => new CornerRadius(all),
        object other => throw new InvalidCastException($"Theme token '{key}' is {other.GetType().Name}, not a CornerRadius.")
    };

    /// <summary>그림자 — 값은 CSS처럼 적은 글이다(<c>0 3 0 0 #B5D9EF</c>). <c>none</c>이면 그림자가 없다.</summary>
    internal static BoxShadows ShadowsOf(string key) => Token(key) switch
    {
        BoxShadows shadows => shadows,
        string text => ParseShadow(text),
        object other => throw new InvalidCastException($"Theme token '{key}' is {other.GetType().Name}, not a shadow.")
    };

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
