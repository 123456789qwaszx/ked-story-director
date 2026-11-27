using Vn.App.Strings;

namespace Vn.App.Theming;

/// <summary>
/// 테마 하나 — <c>Themes/{Folder}/</c> 폴더 하나다. <c>Tokens.axaml</c>(색 · 모양의 값)은 꼭 있고, <c>Controls.axaml</c>(단추 · 탭 같은 컨트롤의
/// 모양 · 글꼴)은 있을 때만 <paramref name="HasControls"/>를 켠다.
/// </summary>
/// <param name="Id">앱 설정에 적히는 이름 — 바꾸면 그 테마를 골라 둔 사람이 처음 테마로 돌아간다.</param>
/// <param name="Folder"><c>Themes/</c> 아래 폴더 이름.</param>
/// <param name="Name">설정 창에 보이는 이름 — 화면 언어를 따른다(<c>Strings/Ui.resx</c>).</param>
/// <param name="HasControls"><c>Controls.axaml</c>이 있는가.</param>
internal sealed record ThemeInfo(string Id, string Folder, Func<string> Name, bool HasControls)
{
    private static readonly string Assembly = typeof(ThemeInfo).Assembly.GetName().Name!;

    /// <summary>색 · 모양의 값.</summary>
    public Uri TokensUri => new($"avares://{Assembly}/Themes/{Folder}/Tokens.axaml");

    /// <summary>컨트롤의 모양 — <see cref="HasControls"/>일 때만 읽는다.</summary>
    public Uri ControlsUri => new($"avares://{Assembly}/Themes/{Folder}/Controls.axaml");
}

/// <summary>
/// <b>고를 수 있는 테마의 목록</b>(2026-11-26 · themes). 테마를 하나 더하려면:
/// <list type="number">
/// <item><c>Themes/{새 폴더}/Tokens.axaml</c>을 만든다 — <c>Themes/Classic/Tokens.axaml</c>에서 바꾸고 싶은 이름만 옮겨 값을 고친다.</item>
/// <item>(바라면) <c>Controls.axaml</c>에 컨트롤의 모양 · 글꼴을 적는다 — <c>Themes/ClearSky/Controls.axaml</c>이 본보기다.</item>
/// <item><see cref="All"/>에 한 줄, 문구 표(<c>Ui.resx</c> · <c>Ui.en.resx</c>)에 이름 한 줄을 더한다.</item>
/// </list>
/// 설정 창의 테마 칸과 시험(<c>ThemeTests</c>)은 이 목록을 돌므로 따로 고칠 것이 없다. 바꿀 수 있는 이름의 목록은 <see cref="ThemeKeys"/>다.
/// </summary>
internal static class ThemeCatalog
{
    /// <summary>기본 테마 — 0.5.0까지의 모양. 다른 테마는 모두 이 위에 얹힌다.</summary>
    public static ThemeInfo Classic { get; } = new("classic", "Classic", () => Ui.Theme_Classic, HasControls: false);

    /// <summary>맑은 하늘.</summary>
    public static ThemeInfo ClearSky { get; } = new("clear-sky", "ClearSky", () => Ui.Theme_ClearSky, HasControls: true);

    /// <summary>설정 창에 보이는 차례.</summary>
    public static IReadOnlyList<ThemeInfo> All { get; } = [ClearSky, Classic];

    /// <summary>앱 설정에 테마가 없을 때(처음 켰을 때) 서는 테마 (2026-11-26 소유자 — 맑은 하늘).</summary>
    public static ThemeInfo Default => ClearSky;

    /// <summary>앱 설정의 이름 → 테마. 모르는 이름 · 빈 값이면 <see cref="Default"/>.</summary>
    public static ThemeInfo Resolve(string? id) => Find(id) ?? Default;

    /// <summary>아는 이름이면 그 이름(목록의 표기대로), 아니면 null — 앱 설정에 모르는 이름을 남기지 않는다.</summary>
    public static string? Normalize(string? id) => Find(id)?.Id;

    /// <summary>
    /// 예전 이름 → 지금 이름. 「맑은 하늘」은 처음에 「파스텔 하늘」(<c>pastel-sky</c>)이었다(2026-11-27 소유자 — 이름 · 폴더 · 코드의 키까지 바꿨다). 그 이름을
    /// 골라 둔 설정도 같은 테마로 열고, <see cref="Normalize"/>가 지금 이름으로 고쳐 적는다.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Renamed =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["pastel-sky"] = "clear-sky" };

    private static ThemeInfo? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string name = Renamed.TryGetValue(id.Trim(), out string? now) ? now : id.Trim();
        return All.FirstOrDefault(theme => string.Equals(theme.Id, name, StringComparison.OrdinalIgnoreCase));
    }
}
