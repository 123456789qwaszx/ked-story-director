using System.Reflection;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// 테마(2026-11-26 · themes) — 색 · 모양의 이름표(<see cref="Skin"/>)와 테마 폴더(<c>Themes/</c>)가 서로 맞는지, 테마를 입히고 걷는 것,
/// 앱 설정 · 설정 창의 테마 칸. 화면 시험 전체는 기본 테마(클래식)로 돈다(<c>TestProcessIsolation</c>) — 여기서 다른 테마를 입힌 시험은
/// 끝날 때 처음 테마로 되돌린다.
/// </summary>
public sealed class ThemeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-themes", Guid.NewGuid().ToString("N"));

    private static readonly string ThemesFolder = Path.GetFullPath("../../../../../src/Vn.App/Themes");

    public void Dispose()
    {
        AppSettingsService.SaveTheme(null);

        if (Directory.Exists(ThemeCatalog.UserThemesFolder))
        {
            Directory.Delete(ThemeCatalog.UserThemesFolder, recursive: true);
        }

        ThemeCatalog.Refresh();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary><see cref="Skin"/>의 이름들 — 이름 → 값의 꼴.</summary>
    private static Dictionary<string, Type> SkinNames() =>
        typeof(Skin)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.Name != nameof(Skin.BranchLines))
            .ToDictionary(
                property => property.Name,
                property => property.PropertyType == typeof(BoxShadows) ? typeof(string) : property.PropertyType,
                StringComparer.Ordinal);

    /// <summary>테마 폴더의 <c>Tokens.axaml</c>에 적힌 것 — 이름 → 요소 이름(Color · Double · String · SolidColorBrush …).</summary>
    private static Dictionary<string, string> Written(ThemeInfo theme)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        return XDocument.Load(Path.Combine(ThemesFolder, theme.Folder, "Tokens.axaml")).Root!
            .Elements()
            .Where(element => element.Attribute(x + "Key") is not null)
            .ToDictionary(element => element.Attribute(x + "Key")!.Value, element => element.Name.LocalName, StringComparer.Ordinal);
    }

    private static string ElementFor(Type type) =>
        type == typeof(Color) ? "Color" : type == typeof(double) ? "Double" : type == typeof(CornerRadius) ? "CornerRadius" : "String";

    [Fact]
    public void 이름표의_이름은_모두_기본_테마에_제_꼴로_있다()
    {
        Dictionary<string, string> classic = Written(ThemeCatalog.Classic);

        foreach ((string name, Type type) in SkinNames())
        {
            Assert.True(classic.TryGetValue(name, out string? element),
                $"Skin.{name}의 값이 Themes/Classic/Tokens.axaml에 없습니다 — 같은 이름의 키를 더해 주세요.");
            Assert.True(element == ElementFor(type), $"Themes/Classic/Tokens.axaml의 {name}: <{element}>가 아니라 <{ElementFor(type)}>여야 합니다.");
        }

        // 기본 테마에는 이름표에 없는 것을 두지 않는다 — 쓰는 곳 없는 값이 쌓이지 않게(XAML만 쓰는 이름은 그 이름을 쓰는 테마가 적는다).
        Assert.Empty(classic.Keys.Except(SkinNames().Keys, StringComparer.Ordinal));
    }

    [Fact]
    public void 테마가_덮는_이름표의_값은_꼴이_같다()
    {
        Dictionary<string, Type> names = SkinNames();

        foreach (ThemeInfo theme in ThemeCatalog.BuiltIns)
        {
            foreach ((string name, string element) in Written(theme))
            {
                if (names.TryGetValue(name, out Type? type))
                {
                    Assert.True(element == ElementFor(type), $"Themes/{theme.Folder}/Tokens.axaml의 {name}: <{element}>가 아니라 <{ElementFor(type)}>여야 합니다.");
                }
            }
        }
    }

    [Fact]
    public void 목록의_테마는_폴더와_이름이_있고_이름이_겹치지_않는다()
    {
        Assert.Contains(ThemeCatalog.Classic, ThemeCatalog.BuiltIns);
        Assert.Contains(ThemeCatalog.Default, ThemeCatalog.BuiltIns);
        Assert.Equal(ThemeCatalog.BuiltIns.Count, ThemeCatalog.BuiltIns.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (ThemeInfo theme in ThemeCatalog.BuiltIns)
        {
            Assert.False(string.IsNullOrWhiteSpace(theme.Name()), $"{theme.Id}: 이름이 비었습니다");
            Assert.True(File.Exists(Path.Combine(ThemesFolder, theme.Folder, "Tokens.axaml")), $"{theme.Id}: Tokens.axaml이 없습니다");
            Assert.Equal(theme.HasControls, File.Exists(Path.Combine(ThemesFolder, theme.Folder, "Controls.axaml")));
        }
    }

    [Theory]
    [InlineData(null, "pastel-sky")]       // 처음 켜면 파스텔 하늘 (2026-11-26 소유자)
    [InlineData("", "pastel-sky")]
    [InlineData("classic", "classic")]
    [InlineData("CLASSIC", "classic")]
    [InlineData("pastel-sky", "pastel-sky")]
    [InlineData("없는 테마", "pastel-sky")]   // 모르는 이름은 처음 테마
    public void 앱_설정의_이름으로_테마를_고른다(string? setting, string expected) =>
        Assert.Equal(expected, ThemeCatalog.Resolve(setting).Id);

    [Fact]
    public void 앱_설정은_아는_테마_이름만_든다()
    {
        AppSettingsService.SaveTheme("classic");
        Assert.Equal("classic", AppSettingsService.LoadTheme());

        AppSettingsService.SaveTheme("없는 테마");
        Assert.Null(AppSettingsService.LoadTheme());

        AppSettingsService.SaveTheme(null);
        Assert.Null(AppSettingsService.LoadTheme());
    }

    [Fact]
    public void 테마마다_입히면_모든_이름이_풀리고_걷으면_기본_모양으로_돌아온다() => HeadlessUi.Run(() =>
    {
        Application app = Application.Current!;
        ThemeInfo before = ThemeService.Current;

        try
        {
            foreach (ThemeInfo theme in ThemeCatalog.BuiltIns)
            {
                ThemeService.Apply(app, theme);
                Assert.Same(theme, ThemeService.Current);

                // 글자 크기 단계 · 곁글의 진하기는 테마의 배수에서 셈한다.
                Assert.True(app.TryGetResource("TextSm", ThemeVariant.Light, out object? textSm));
                Assert.Equal(Skin.Font(11), Assert.IsType<double>(textSm));
                Assert.True(app.TryGetResource("Dim60", ThemeVariant.Light, out object? dim60));
                Assert.Equal(Skin.Dim(0.6), Assert.IsType<double>(dim60));

                // 이름표의 모든 이름이 값을 낸다(없는 이름 · 틀린 꼴이면 여기서 터진다).
                foreach (PropertyInfo property in typeof(Skin).GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    _ = property.GetValue(null);
                }

                // XAML이 같은 이름으로 꺼낸다 — 색은 붓 자리에도 그대로 든다.
                Assert.True(app.TryGetResource(nameof(Skin.Selection), ThemeVariant.Light, out object? selection));
                Assert.Equal(Skin.Selection, Assert.IsType<Color>(selection));
            }

            ThemeService.Apply(app, ThemeCatalog.PastelSky);
            Assert.NotEqual(Color.Parse("#2563EB"), Skin.Selection);
            Assert.True(app.TryGetResource("TopBarBackground", ThemeVariant.Light, out _));

            // 틀의 구분선(위 띠 · 툴바 · 기둥 사이)도 테마가 정한다 — XAML이 Divider로 부른다.
            Assert.True(app.TryGetResource(nameof(Skin.Divider), ThemeVariant.Light, out object? divider));
            Assert.Equal(Color.Parse("#D6E3EB"), Assert.IsType<Color>(divider));

            // 색 이름을 붓 자리에 걸면 붓으로 든다(XAML의 {DynamicResource 이름}).
            var probe = new Border { [!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(nameof(Skin.Divider)) };
            var host = new Window { Content = probe };
            host.Show();
            Assert.Equal(Skin.Divider, Assert.IsAssignableFrom<ISolidColorBrush>(probe.Background).Color);
            host.Close();
            Assert.Same(Skin.Selection.Brush(), Skin.Selection.Brush());   // 같은 색은 같은 붓

            // 아이콘 글자(⌂ ⑂ ⚙ 📂 📄 ⚠)는 본문 글꼴에 없어 테마의 아이콘 글꼴이 그린다 — PC의 이모지 글꼴로 넘어가지 않는다.
            Assert.True(app.TryGetResource("UiFont", ThemeVariant.Light, out object? uiFont));
            foreach (int icon in new[] { 0x2302, 0x2442, 0x2699, 0x1F4C2, 0x1F4C4, 0x26A0 })
            {
                Assert.True(FontManager.Current.TryMatchCharacter(
                    icon, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, (FontFamily)uiFont!, null, out Typeface drawn));
                Assert.Equal("Story Director Icons", drawn.FontFamily.Name);
            }

            ThemeService.Apply(app, ThemeCatalog.Classic);
            Assert.Equal(Color.Parse("#2563EB"), Skin.Selection);
            Assert.Equal(4, Skin.CardRadius);
            Assert.Equal(default, Skin.CardShadow);
            Assert.False(app.TryGetResource("TopBarBackground", ThemeVariant.Light, out _));   // 테마만의 이름은 함께 걷힌다
            Assert.False(app.TryGetResource("UiFont", ThemeVariant.Light, out _));
        }
        finally
        {
            ThemeService.Apply(app, before);
        }
    });

    [Fact]
    public void 다른_테마로도_주_창과_두_판이_선다() => HeadlessUi.Run(() =>
    {
        Application app = Application.Current!;
        ThemeInfo before = ThemeService.Current;

        try
        {
            foreach (ThemeInfo theme in ThemeCatalog.BuiltIns)
            {
                ThemeService.Apply(app, theme);

                (MainWindow window, _) = HierarchyFixture.Open(Path.Combine(_directory, theme.Id), tab: MainTab.Script);
                Dispatcher.UIThread.RunJobs();
                window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Structure;
                Dispatcher.UIThread.RunJobs();

                Panel edge = window.FindControl<Panel>("TopBarEdge")!;
                Assert.Equal(ReferenceEquals(theme, ThemeCatalog.Classic) ? 0 : 10, edge.Bounds.Height);

                SettingsWindow settings = window.OpenSettings();
                Dispatcher.UIThread.RunJobs();
                settings.Close();
                window.Close();
            }
        }
        finally
        {
            ThemeService.Apply(app, before);
        }
    });

    [Fact]
    public void 설정_창의_테마_칸은_고르면_앱_설정에_들고_화면은_다시_켤_때_바뀐다() => HeadlessUi.Run(() =>
    {
        // 앱에서는 켤 때 앱 설정의 테마를 입히므로 둘이 같다 — 시험은 기본 테마로 켜지므로(TestProcessIsolation) 앱 설정을 거기에 맞춰 둔다.
        ThemeInfo now = ThemeService.Current;
        AppSettingsService.SaveTheme(now.Id);

        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ThemeCatalog.All.Select(theme => theme.Name()), settings.ThemeCombo.Items.Cast<string>());
        Assert.Equal(ThemeCatalog.All.ToList().IndexOf(now), settings.ThemeCombo.SelectedIndex);
        Assert.False(settings.ThemeNote.IsVisible);

        ThemeInfo other = ThemeCatalog.All.First(theme => !ReferenceEquals(theme, now));

        settings.ThemeCombo.SelectedIndex = ThemeCatalog.All.ToList().IndexOf(other);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(other.Id, AppSettingsService.LoadTheme());
        Assert.True(settings.ThemeNote.IsVisible);
        Assert.Equal($"다시 켜면 「{other.Name()}」 테마로 바뀝니다.", settings.ThemeNote.Text);
        Assert.Equal(settings.ThemeNote.Text, session.StatusMessage);
        Assert.Same(now, ThemeService.Current);   // 지금 화면은 그대로

        // 다시 열면 고른 것이 보인다 — 지금 화면과 같은 테마로 되돌리면 알림이 걷힌다.
        settings.Close();
        settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ThemeCatalog.All.ToList().IndexOf(other), settings.ThemeCombo.SelectedIndex);

        settings.ThemeCombo.SelectedIndex = ThemeCatalog.All.ToList().IndexOf(now);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(now.Id, AppSettingsService.LoadTheme());
        Assert.False(settings.ThemeNote.IsVisible);

        settings.Close();
        window.Close();
    });

    // ── 작가의 「내 테마」 ─────────────────────────────────────────────

    [Fact]
    public void 바꿀_수_있는_이름의_목록은_이름표와_테마에_맞는다() => HeadlessUi.Run(() =>
    {
        Dictionary<string, Type> skin = SkinNames();

        // 이름표(Skin)의 이름은 모두 목록에 있고, 꼴이 맞는다.
        foreach ((string name, Type type) in skin)
        {
            ThemeKey key = Assert.IsType<ThemeKey>(ThemeKeys.Find(name));
            ThemeValueKind expected = type == typeof(Color) ? ThemeValueKind.Color
                : type == typeof(double) ? ThemeValueKind.Number
                : type == typeof(CornerRadius) ? ThemeValueKind.Corner
                : ThemeValueKind.Shadow;
            Assert.True(key.Kind == expected, $"{name}: 목록의 꼴 {key.Kind} ≠ {expected}");
        }

        // 목록의 이름은 모두 「파스텔 하늘」에서 값이 있다 — 쓰는 곳 없는 이름 · 잘못 적은 이름이 없다. 설명은 두 언어 다 있다.
        Application app = Application.Current!;
        ThemeInfo before = ThemeService.Current;

        try
        {
            ThemeService.Apply(app, ThemeCatalog.PastelSky);

            foreach (ThemeKey key in ThemeKeys.All)
            {
                Assert.True(app.TryGetResource(key.Name, ThemeVariant.Light, out _), $"{key.Name}: 「파스텔 하늘」에 값이 없습니다");
                Assert.False(string.IsNullOrWhiteSpace(key.Korean), key.Name);
                Assert.False(string.IsNullOrWhiteSpace(key.English), key.Name);
                Assert.DoesNotMatch("[가-힣]", key.English);
            }
        }
        finally
        {
            ThemeService.Apply(app, before);
        }
    });

    [Fact]
    public void 컨트롤_모양_파일에는_색을_숫자로_적지_않는다()
    {
        // 색은 모두 Tokens.axaml에 이름으로 — 작가가 「내 테마」로 단추 색을 바꿀 수 있게(2026-11-26).
        string controls = File.ReadAllText(Path.Combine(ThemesFolder, "PastelSky", "Controls.axaml"));
        Assert.Empty(System.Text.RegularExpressions.Regex.Matches(controls, "#[0-9A-Fa-f]{3,8}\\b"));
    }

    [Fact]
    public void 내_테마는_만들고_고치고_다시_켜면_그대로_선다() => HeadlessUi.Run(() =>
    {
        Application app = Application.Current!;
        ThemeInfo before = ThemeService.Current;

        try
        {
            ThemeService.Apply(app, ThemeCatalog.PastelSky);
            string folder = UserThemes.Create(app);

            // 만든 파일 — 쓰는 법 · 밑바탕 · 바꿀 수 있는 이름과 지금 값 · 설명. 폴더 맨 위에 쓰는 법 파일도 생긴다.
            string file = Path.Combine(folder, UserThemes.FileName);
            string text = File.ReadAllText(file);
            Assert.StartsWith("// ", text, StringComparison.Ordinal);
            Assert.Contains("\"base\": \"pastel-sky\"", text, StringComparison.Ordinal);
            Assert.Contains("\"TopBarBackground\": \"#A9D0E6\"", text, StringComparison.Ordinal);
            Assert.Contains("\"BoardDecorBrush\": \"\"", text, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(ThemeCatalog.UserThemesFolder, Strings.Ui.Theme_ReadmeFileName)));

            // 그대로 켜도 읽지 못한 것이 없다(주석 · 빈 값 · 지금 값을 모두 읽는다).
            ThemeInfo made = ThemeCatalog.All.Single(theme => theme.UserFolder == folder);
            Assert.Same(ThemeCatalog.PastelSky, made.Parent);
            ThemeService.Apply(app, made);
            Assert.Empty(ThemeService.Problems);
            Assert.Equal(Color.Parse("#A9D0E6"), ((ISolidColorBrush)Resource(app, "TopBarBackground")!).Color);

            // 작가(또는 AI)가 고친다 — 색 · 그림 · 수, 그리고 틀린 것 둘.
            SavePicture(Path.Combine(folder, "decor.png"));
            text = text
                .Replace("\"Selection\": \"#DE8AA6\"", "\"Selection\": \"#123456\"", StringComparison.Ordinal)
                .Replace("\"BoardDecorBrush\": \"\"", "\"BoardDecorBrush\": \"decor.png\"", StringComparison.Ordinal)
                .Replace("\"TopBarEdgeMask\": \"\"", "\"TopBarEdgeMask\": \"none\"", StringComparison.Ordinal)
                .Replace("\"FontScale\": 1.15", "\"FontScale\": 1.3", StringComparison.Ordinal)
                .Replace("\"CardRadius\": 14", "\"CardRadius\": \"둥글게\"", StringComparison.Ordinal)
                .Replace("\"values\": {", "\"values\": {\n    \"Sparkles\": \"#FFFFFF\",", StringComparison.Ordinal);
            File.WriteAllText(file, text);

            ThemeCatalog.Refresh();
            made = ThemeCatalog.All.Single(theme => theme.UserFolder == folder);
            ThemeService.Apply(app, made);

            Assert.Equal(Color.Parse("#123456"), Skin.Selection);
            Assert.IsType<ImageBrush>(Resource(app, "BoardDecorBrush"));
            Assert.Null(Resource(app, "TopBarEdgeMask"));
            Assert.Equal(14.5, Assert.IsType<double>(Resource(app, "TextSm")));   // 11 × 1.3 = 14.3 → 0.5 단위
            Assert.Equal(14, Skin.CardRadius);                                    // 못 읽은 값은 밑바탕 테마의 값 그대로

            // 틀린 것은 그것만 건너뛰고 알린다 — 앱은 켜진다.
            Assert.Equal(2, ThemeService.Problems.Count);
            Assert.Contains(ThemeService.Problems, problem => problem.Contains("Sparkles", StringComparison.Ordinal));
            Assert.Contains(ThemeService.Problems, problem => problem.Contains("CardRadius", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(folder, UserThemes.ProblemsFileName)));

            // 「내 테마」를 또 복사하면 고친 값과 그림이 함께 옮겨 간다.
            string copy = UserThemes.Create(app);
            Assert.True(File.Exists(Path.Combine(copy, "decor.png")));
            string copied = File.ReadAllText(Path.Combine(copy, UserThemes.FileName));
            Assert.Contains("\"Selection\": \"#123456\"", copied, StringComparison.Ordinal);
            Assert.Contains("\"BoardDecorBrush\": \"decor.png\"", copied, StringComparison.Ordinal);
            Assert.Contains("\"FontScale\": 1.3", copied, StringComparison.Ordinal);

            // 다 고치면 알림 파일이 걷힌다.
            File.WriteAllText(file, text.Replace("\"CardRadius\": \"둥글게\"", "\"CardRadius\": 20", StringComparison.Ordinal)
                .Replace("    \"Sparkles\": \"#FFFFFF\",\n", string.Empty, StringComparison.Ordinal));
            ThemeService.Apply(app, made);
            Assert.Empty(ThemeService.Problems);
            Assert.Equal(20, Skin.CardRadius);
            Assert.False(File.Exists(Path.Combine(folder, UserThemes.ProblemsFileName)));
        }
        finally
        {
            ThemeService.Apply(app, before);
        }
    });

    [Fact]
    public void 깨진_테마_파일도_앱을_막지_않는다() => HeadlessUi.Run(() =>
    {
        Application app = Application.Current!;
        ThemeInfo before = ThemeService.Current;
        string folder = Path.Combine(ThemeCatalog.UserThemesFolder, "깨진 테마");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, UserThemes.FileName), "{ \"name\": \"깨진\", \"values\": { \"Selection\": ");
        ThemeCatalog.Refresh();

        try
        {
            ThemeInfo broken = ThemeCatalog.All.Single(theme => theme.UserFolder == folder);
            Assert.Equal("깨진 테마", broken.Name());          // 이름을 못 읽으면 폴더 이름
            Assert.Same(ThemeCatalog.Default, broken.Parent);  // 밑바탕을 못 읽으면 처음 테마

            ThemeService.Apply(app, broken);
            Assert.Single(ThemeService.Problems);
            Assert.Equal(Color.Parse("#DE8AA6"), Skin.Selection);   // 밑바탕 테마 그대로
        }
        finally
        {
            ThemeService.Apply(app, before);
        }
    });

    [Fact]
    public void 설정_창에서_내_테마를_만들면_폴더가_열리고_그_테마가_골라진다() => HeadlessUi.Run(() =>
    {
        string? opened = null;
        Action<string> open = SettingsWindow.OpenFolder;
        SettingsWindow.OpenFolder = folder => opened = folder;

        try
        {
            (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();

            settings.ThemeCreateButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            ThemeInfo made = Assert.Single(ThemeCatalog.All, theme => theme.UserFolder is not null);
            Assert.Equal(made.UserFolder, opened);
            Assert.Equal(made.Id, AppSettingsService.LoadTheme());
            Assert.Equal(ThemeCatalog.All.ToList().IndexOf(made), settings.ThemeCombo.SelectedIndex);
            Assert.Contains(made.Name(), session.StatusMessage, StringComparison.Ordinal);

            settings.ThemeFolderButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(ThemeCatalog.UserThemesFolder, opened);

            settings.Close();
            window.Close();
        }
        finally
        {
            SettingsWindow.OpenFolder = open;
        }
    });

    private static object? Resource(Application app, string key) =>
        app.TryGetResource(key, ThemeVariant.Light, out object? value) ? value : null;

    /// <summary>작은 그림 파일 하나.</summary>
    private static void SavePicture(string path)
    {
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(8, 8));
        bitmap.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
