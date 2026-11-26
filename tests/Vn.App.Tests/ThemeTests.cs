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

        foreach (ThemeInfo theme in ThemeCatalog.All)
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
        Assert.Contains(ThemeCatalog.Classic, ThemeCatalog.All);
        Assert.Contains(ThemeCatalog.Default, ThemeCatalog.All);
        Assert.Equal(ThemeCatalog.All.Count, ThemeCatalog.All.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (ThemeInfo theme in ThemeCatalog.All)
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
            foreach (ThemeInfo theme in ThemeCatalog.All)
            {
                ThemeService.Apply(app, theme);
                Assert.Same(theme, ThemeService.Current);

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
            Assert.Equal(Color.Parse("#CFE6F5"), Assert.IsType<Color>(divider));

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
            foreach (ThemeInfo theme in ThemeCatalog.All)
            {
                ThemeService.Apply(app, theme);

                (MainWindow window, _) = HierarchyFixture.Open(Path.Combine(_directory, theme.Id), tab: MainTab.Script);
                Dispatcher.UIThread.RunJobs();
                window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Structure;
                Dispatcher.UIThread.RunJobs();

                Border edge = window.FindControl<Border>("TopBarEdge")!;
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
}
