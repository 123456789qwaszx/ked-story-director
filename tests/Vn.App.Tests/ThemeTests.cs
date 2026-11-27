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
/// 테마(2026-11-26 · themes) — 색 · 모양의 이름표(<see cref="Skin"/>) · 이름의 목록(<see cref="ThemeKeys"/>)과 테마 폴더(<c>Themes/</c>)가 서로
/// 맞는지, 테마를 입히고 걷는 것, 앱 설정 · 설정 창의 테마 칸. 화면 시험 전체는 기본 테마(클래식)로 돈다(<c>TestProcessIsolation</c>) — 여기서 다른 테마를 입힌 시험은
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
    [InlineData(null, "pastel-sky")]       // 처음 켜면 맑은 하늘 (2026-11-26 소유자)
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
            Assert.Equal(Color.Parse("#C9D4DC"), Assert.IsType<Color>(divider));

            // 색 이름을 붓 자리에 걸면 붓으로 든다(XAML의 {DynamicResource 이름}).
            var probe = new Border { [!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(nameof(Skin.Divider)) };
            var host = new Window { Content = probe };
            host.Show();
            Assert.Equal(Skin.Divider, Assert.IsAssignableFrom<ISolidColorBrush>(probe.Background).Color);
            host.Close();
            Assert.Same(Skin.Selection.Brush(), Skin.Selection.Brush());   // 같은 색은 같은 붓

            // 아이콘 글자(⌂ ⑂ ⚙ 📂 📄)는 본문 글꼴(Inter)에 없어 테마의 아이콘 글꼴이 그린다 — PC의 이모지 글꼴로 넘어가지 않는다.
            // ⚠은 Inter에 있어 Inter가 그린다(글자색 · 크기를 따르는 것은 같다).
            Assert.True(app.TryGetResource("UiFont", ThemeVariant.Light, out object? uiFont));
            foreach (int icon in new[] { 0x2302, 0x2442, 0x2699, 0x1F4C2, 0x1F4C4 })
            {
                Assert.True(FontManager.Current.TryMatchCharacter(
                    icon, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, (FontFamily)uiFont!, null, out Typeface drawn));
                Assert.Equal("Story Director Icons", drawn.FontFamily.Name);
            }

            // 디자인 점검 1번(2026-11-26) — 본문 · 제목의 한글과 영문은 Pretendard 한 벌이 그린다(PC의 한글 글꼴로 넘어가지 않는다 · 굵게 청해도 Pretendard다).
            Assert.True(app.TryGetResource("DisplayFont", ThemeVariant.Light, out object? displayFont));
            foreach ((FontFamily family, FontWeight weight) in new[] { ((FontFamily)uiFont!, FontWeight.Normal), ((FontFamily)displayFont!, FontWeight.SemiBold) })
            {
                using var layout = new Avalonia.Media.TextFormatting.TextLayout("대본 File", new Typeface(family, weight: weight), 20, Brushes.Black);
                List<Avalonia.Media.TextFormatting.ShapedTextRun> runs = layout.TextLines[0].TextRuns.OfType<Avalonia.Media.TextFormatting.ShapedTextRun>().ToList();
                Assert.NotEmpty(runs);
                Assert.All(runs, run => Assert.StartsWith("Pretendard", run.GlyphRun.GlyphTypeface.FamilyName, StringComparison.Ordinal));
            }

            // 장면 색(design/scene-colors.md · 2026-11-26 소유자가 견본에서 「옅게」를 골랐다) — 비치는 바탕이 챕터 바탕 위 한 겹이면 견본의 색이고,
            // 줄인 판의 덩어리는 그 위에서 대비 3:1 이상이다(design/board-zoom.md 4단계). 점은 흰 장면 이름표 위에서 3:1 이상, 겹친 자리는 한 겹 더 진하다.
            string[] swatch = ["#D5EBDC", "#F8DFE7", "#CEEBEC", "#E7E7D0", "#E9E2F8"];

            for (int slot = 1; slot <= 5; slot++)
            {
                Color tint = Skin.SceneTintOf(slot);
                Assert.InRange(tint.A, 1, 254);   // 비친다
                Color single = Over(tint, Skin.ChapterFrameFill);
                Color expected = Color.Parse(swatch[slot - 1]);
                Assert.True(Math.Abs(single.R - expected.R) <= 1 && Math.Abs(single.G - expected.G) <= 1 && Math.Abs(single.B - expected.B) <= 1,
                    $"칸 {slot}: {single} ≠ {expected}");

                foreach (Color block in new[] { Skin.BlockEpisode, Skin.BlockSceneRoot, Skin.BlockBranch, Skin.BlockOther })
                {
                    Assert.True(Contrast(block, single) >= 3, $"칸 {slot}: {block} ↔ 장면 바탕 {Contrast(block, single):0.00}:1");
                }

                Assert.True(Contrast(Skin.SceneMarkOf(slot), Skin.SceneLabel) >= 3, $"칸 {slot}의 점 ↔ 이름표 {Contrast(Skin.SceneMarkOf(slot), Skin.SceneLabel):0.00}:1");

                // [구성] 카드의 장면 꼬리표는 점 색 바탕에 흰 작은 글이다 — 4.5:1 이상(2026-11-27 세이지 · 민트를 조금 낮췄다).
                Assert.True(Contrast(Skin.OnAccent, Skin.SceneMarkOf(slot)) >= 4.5, $"칸 {slot}의 꼬리표 글 {Contrast(Skin.OnAccent, Skin.SceneMarkOf(slot)):0.00}:1");
                Assert.True(Contrast(Over(tint, single), single) > 1.08, $"칸 {slot}: 겹친 자리가 진해지지 않는다");
            }

            // RGB로 고른 장면 색(2026-11-27 소유자 — 「RGB 표에서 선택도」) — 바탕은 팔레트만큼(또는 덜) 비치고, 챕터 바탕 위 한 겹이 줄인 판의 덩어리와 3:1 이상이다
            // (짙은 색은 옅게 깔린다). 점 · [구성] 꼬리표는 흰 글과 4.5:1 이상이다(옅은 색은 같은 빛깔로 짙게).
            foreach (int rgb in new[] { 0x000000, 0xFFFFFF, 0xFF0000, 0xFFFF00, 0x00FF00, 0x3366CC, 0x8000FF, 0xF4C2C2 })
            {
                int custom = Vn.Authoring.Chapters.SceneColors.Custom(rgb);
                Color tint = Skin.SceneTintOf(custom);
                Assert.InRange(tint.A, 1, Skin.SceneTint1.A);
                Assert.Equal(((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF), ((int)tint.R, (int)tint.G, (int)tint.B));   // 고른 색 그대로 비친다
                Color single = Over(tint, Skin.ChapterFrameFill);

                foreach (Color block in new[] { Skin.BlockEpisode, Skin.BlockSceneRoot, Skin.BlockBranch, Skin.BlockOther })
                {
                    Assert.True(Contrast(block, single) >= 3, $"#{rgb:X6}: {block} ↔ 장면 바탕 {Contrast(block, single):0.00}:1");
                }

                Assert.True(Contrast(Skin.OnAccent, Skin.SceneMarkOf(custom)) >= 4.5, $"#{rgb:X6}의 꼬리표 글 {Contrast(Skin.OnAccent, Skin.SceneMarkOf(custom)):0.00}:1");
            }

            Assert.Equal(Color.Parse("#3366CC"), Skin.SceneMarkOf(Vn.Authoring.Chapters.SceneColors.Custom(0x3366CC)));   // 짙은 색은 점이 그 색 그대로

            // 틀의 글자 크기는 다섯 단계 가운데 하나다 — 꼬리표 12 · 단추 · 탭 13 · 읽는 글 15 · 제목 18 · 큰 제목 22.
            Assert.Equal(new[] { 12d, 12d, 13d, 15d, 18d, 22d }, new[] { 9d, 10d, 11d, 12d, 14d, 20d }.Select(Skin.Font));
            Assert.True(app.TryGetResource("TextSm", ThemeVariant.Light, out object? pastelSm));
            Assert.Equal(13d, Assert.IsType<double>(pastelSm));

            ThemeService.Apply(app, ThemeCatalog.Classic);
            Assert.Equal(11d, Skin.Font(11));   // 기본 테마는 단계가 없다 — 준 크기 그대로

            // 접은 장면 상자의 바탕 — 기본 테마의 챕터 바탕은 비치는 옅은 파랑이라 흰 종이 위에 얹어 셈한다. 상자는 옅다(2026-11-27 — ⛔ 챕터 바탕의 RGB를
            // 불투명하게 섞어 진한 파랑이었다).
            for (int slot = 1; slot <= 5; slot++)
            {
                Color fill = Skin.SceneProxyFillOf(slot);
                Assert.Equal(255, fill.A);
                Assert.True(Contrast(fill, Colors.White) < 1.25, $"기본 테마 칸 {slot}의 접은 상자 {fill}가 옅지 않다");
            }
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

                // 위 띠 아래의 꾸밈 줄 — 지금은 어느 테마도 쓰지 않는다(「맑은 하늘」의 구름 물결은 2026-11-26에 걷었다 — 높이 0으로 접힌다).
                Panel edge = window.FindControl<Panel>("TopBarEdge")!;
                Assert.Equal(0, edge.Bounds.Height);

                // [대본] 판의 깔개 — 테마가 점을 줄 때만 점을 그린다(기본 테마는 0.5.0까지처럼 아무것도 그리지 않는다).
                DotGridBackdrop backdrop = window.FindControl<GraphEditorView>("Graph")!.FindControl<DotGridBackdrop>("BoardBackdrop")!;
                Assert.Equal(!ReferenceEquals(theme, ThemeCatalog.Classic), backdrop.Step > 0);
                Assert.Equal(!ReferenceEquals(theme, ThemeCatalog.Classic), backdrop.Fill is not null);

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

    /// <summary>
    /// 판의 깔개의 점(2026-11-26 소유자 — 챕터 바깥에 일정한 간격의 점) — 배율을 줄이면 한 칸씩 건너뛰어, 화면에서 점 사이가 18px보다 촘촘해지지 않는다.
    /// </summary>
    [Theory]
    [InlineData(24, 1.0, 24)]
    [InlineData(24, 2.0, 48)]
    [InlineData(24, 0.61, 29.28)]   // 14.64는 촘촘하다 — 한 칸 건너뛴다
    [InlineData(24, 0.25, 24)]      // 6 → 12 → 24
    [InlineData(24, 0.1, 19.2)]
    [InlineData(0, 1.0, 0)]         // 간격이 0이면 점이 없다(기본 테마)
    public void 깔개의_점은_배율을_줄여도_화면에서_촘촘해지지_않는다(double spacing, double zoom, double expected) =>
        Assert.Equal(expected, DotGridBackdrop.StepFor(spacing, zoom), 6);

    // ── 바꿀 수 있는 이름의 목록 ─────────────────────────────────────────────

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

        // 목록의 이름은 모두 「맑은 하늘」에서 값이 있다 — 쓰는 곳 없는 이름 · 잘못 적은 이름이 없다. 설명은 두 언어 다 있다.
        Application app = Application.Current!;
        ThemeInfo before = ThemeService.Current;

        try
        {
            ThemeService.Apply(app, ThemeCatalog.PastelSky);

            foreach (ThemeKey key in ThemeKeys.All)
            {
                Assert.True(app.TryGetResource(key.Name, ThemeVariant.Light, out _), $"{key.Name}: 「맑은 하늘」에 값이 없습니다");
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
        // 색은 모두 Tokens.axaml에 이름으로 — 이름의 목록(ThemeKeys) 하나로 모든 색을 고칠 수 있게(2026-11-26).
        string controls = File.ReadAllText(Path.Combine(ThemesFolder, "PastelSky", "Controls.axaml"));
        Assert.Empty(System.Text.RegularExpressions.Regex.Matches(controls, "#[0-9A-Fa-f]{3,8}\\b"));
    }

    private static object? Resource(Application app, string key) =>
        app.TryGetResource(key, ThemeVariant.Light, out object? value) ? value : null;

    /// <summary>두 색의 대비(WCAG — 상대 휘도).</summary>
    /// <summary>비치는 색을 바탕 위에 한 겹 — 그리는 쪽(sRGB 그대로 섞는다)과 같은 셈.</summary>
    private static Color Over(Color top, Color under)
    {
        double a = top.A / 255d;
        byte Mix(byte t, byte u) => (byte)Math.Round((t * a) + (u * (1 - a)));
        return Color.FromRgb(Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
    }

    private static double Contrast(Color a, Color b)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte value)
            {
                double c = value / 255d;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
        }

        double x = Luminance(a);
        double y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
