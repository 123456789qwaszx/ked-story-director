using Vn.App.Services;
using Vn.App.Strings;

namespace Vn.App.Theming;

/// <summary>
/// 테마 하나. 앱에 든 테마는 <c>Themes/{Folder}/</c> 폴더 하나다 — <c>Tokens.axaml</c>(색 · 모양의 값)은 꼭 있고, <c>Controls.axaml</c>(컨트롤의 모양 ·
/// 글꼴)은 있을 때만 <paramref name="HasControls"/>를 켠다. 작가의 「내 테마」는 설정 폴더의 <c>themes/{폴더}/theme.json</c>이고(<see cref="UserFolder"/>),
/// 앱에 든 테마 하나(<see cref="Parent"/>) 위에 얹힌다(<see cref="UserThemes"/>).
/// </summary>
/// <param name="Id">앱 설정에 적히는 이름 — 바꾸면 그 테마를 골라 둔 사람이 처음 테마로 돌아간다. 「내 테마」는 <c>my:{폴더 이름}</c>.</param>
/// <param name="Folder"><c>Themes/</c> 아래 폴더 이름(앱에 든 테마).</param>
/// <param name="Name">설정 창에 보이는 이름.</param>
/// <param name="HasControls"><c>Controls.axaml</c>이 있는가.</param>
internal sealed record ThemeInfo(string Id, string Folder, Func<string> Name, bool HasControls)
{
    private static readonly string Assembly = typeof(ThemeInfo).Assembly.GetName().Name!;

    /// <summary>「내 테마」의 폴더(설정 폴더의 <c>themes/</c> 아래) — 앱에 든 테마면 null.</summary>
    public string? UserFolder { get; init; }

    /// <summary>「내 테마」가 얹히는 앱의 테마 — 앱에 든 테마면 null.</summary>
    public ThemeInfo? Parent { get; init; }

    /// <summary>값과 컨트롤 모양을 주는 앱의 테마 — 「내 테마」면 그 밑바탕, 아니면 자기.</summary>
    public ThemeInfo BuiltIn => Parent ?? this;

    /// <summary>색 · 모양의 값.</summary>
    public Uri TokensUri => new($"avares://{Assembly}/Themes/{BuiltIn.Folder}/Tokens.axaml");

    /// <summary>컨트롤의 모양 — <see cref="HasControls"/>일 때만 읽는다.</summary>
    public Uri ControlsUri => new($"avares://{Assembly}/Themes/{BuiltIn.Folder}/Controls.axaml");
}

/// <summary>
/// <b>고를 수 있는 테마의 목록</b>(2026-11-26 · themes) — 앱에 든 테마(<see cref="BuiltIns"/>)와, 설정 폴더의 <c>themes/</c>에서 찾은 작가의 「내 테마」.
/// 앱에 든 테마를 하나 더하려면:
/// <list type="number">
/// <item><c>Themes/{새 폴더}/Tokens.axaml</c>을 만든다 — <c>Themes/Classic/Tokens.axaml</c>에서 바꾸고 싶은 이름만 옮겨 값을 고친다.</item>
/// <item>(바라면) <c>Controls.axaml</c>에 컨트롤의 모양 · 글꼴을 적는다 — <c>Themes/PastelSky/Controls.axaml</c>이 본보기다.</item>
/// <item><see cref="BuiltIns"/>에 한 줄, 문구 표(<c>Ui.resx</c> · <c>Ui.en.resx</c>)에 이름 한 줄을 더한다.</item>
/// </list>
/// 설정 창의 테마 칸과 시험(<c>ThemeTests</c>)은 이 목록을 돌므로 따로 고칠 것이 없다.
/// </summary>
internal static class ThemeCatalog
{
    /// <summary>기본 테마 — 0.5.0까지의 모양. 다른 테마는 모두 이 위에 얹힌다.</summary>
    public static ThemeInfo Classic { get; } = new("classic", "Classic", () => Ui.Theme_Classic, HasControls: false);

    /// <summary>파스텔 하늘.</summary>
    public static ThemeInfo PastelSky { get; } = new("pastel-sky", "PastelSky", () => Ui.Theme_PastelSky, HasControls: true);

    /// <summary>앱에 든 테마 — 설정 창에 보이는 차례.</summary>
    public static IReadOnlyList<ThemeInfo> BuiltIns { get; } = [PastelSky, Classic];

    /// <summary>앱 설정에 테마가 없을 때(처음 켰을 때) 서는 테마 (2026-11-26 소유자 — 파스텔 하늘).</summary>
    public static ThemeInfo Default => PastelSky;

    /// <summary>작가의 「내 테마」가 사는 폴더 — 설정 파일 옆의 <c>themes/</c>(PC마다 · 2026-11-26 소유자).</summary>
    public static string UserThemesFolder =>
        Path.Combine(Path.GetDirectoryName(AppSettingsService.SettingsPath) ?? ".", "themes");

    private static IReadOnlyList<ThemeInfo>? _all;

    /// <summary>고를 수 있는 모든 테마 — 앱에 든 테마 뒤에 「내 테마」(폴더 이름 차례). 처음 물을 때 폴더를 읽는다(<see cref="Refresh"/>).</summary>
    public static IReadOnlyList<ThemeInfo> All => _all ??= [.. BuiltIns, .. UserThemes.Discover(UserThemesFolder)];

    /// <summary>「내 테마」 폴더를 다시 읽는다 — 새로 만든 뒤.</summary>
    public static void Refresh() => _all = null;

    /// <summary>앱 설정의 이름 → 테마. 모르는 이름 · 빈 값이면 <see cref="Default"/>.</summary>
    public static ThemeInfo Resolve(string? id) => Find(id) ?? Default;

    /// <summary>아는 이름이면 그 이름(목록의 표기대로), 아니면 null — 앱 설정에 모르는 이름을 남기지 않는다.</summary>
    public static string? Normalize(string? id) => Find(id)?.Id;

    /// <summary>앱에 든 테마의 이름 → 테마(「내 테마」의 밑바탕을 고를 때).</summary>
    public static ThemeInfo? FindBuiltIn(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : BuiltIns.FirstOrDefault(theme => string.Equals(theme.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    private static ThemeInfo? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : All.FirstOrDefault(theme => string.Equals(theme.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
}
