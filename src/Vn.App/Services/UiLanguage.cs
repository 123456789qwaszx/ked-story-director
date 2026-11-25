using System.Globalization;
using Vn.App.Strings;

namespace Vn.App.Services;

/// <summary>
/// <b>툴 화면의 언어</b>(2026-11-25 소유자 · <c>docs/work-orders/english-ui-orders.md</c> E1) — 한국어와 영어. 대사 · 프로젝트 데이터의 언어와는 따로다
/// (대본의 언어별 칸 · 새로 만든 것의 처음 이름 · 내보내는 파일은 언어를 따르지 않는다 — 소유자 「한국어 그대로」).
///
/// <b>처음은 PC 언어를 따른다</b> — 한국어 PC면 한국어, 그 밖은 영어(소유자 「PC 언어를 따라」). 설정 창 「기타」의 언어 칸에서 고르면 앱 설정에 들고,
/// <b>다시 켤 때</b> 바뀐다(소유자 「바꾸면 다시 켤 때」) — 켤 때 한 번 정하고(<see cref="ApplyAtStartup"/>) 켜 있는 동안은 바꾸지 않는다. 창을 그 자리에서
/// 다시 짓지 않으니 반쯤 옮겨진 화면이 생기지 않는다.
///
/// 문구는 <see cref="Ui"/>(문구 표 <c>Strings/Ui.resx</c> · <c>Ui.en.resx</c>)가 이 언어로 꺼낸다. 영어 칸이 빈 문구는 한국어로 보인다(표의 기본 언어).
/// 숫자 · 날짜의 꼴(<see cref="CultureInfo.CurrentCulture"/>)은 건드리지 않는다 — 화면의 글만이다.
/// </summary>
internal static class UiLanguage
{
    public const string Korean = "ko";
    public const string English = "en";

    /// <summary>
    /// 이 PC의 언어 — 켤 때의 <see cref="CultureInfo.CurrentUICulture"/>(Windows는 표시 언어). 시험은 한국어 PC로 고정한다
    /// (<c>Vn.App.Tests.TestProcessIsolation</c>) — 시험을 돌리는 PC의 언어에 따라 답이 갈리지 않게.
    /// </summary>
    internal static CultureInfo OsCulture { get; set; } = CultureInfo.CurrentUICulture;

    /// <summary>지금 화면의 언어 — <see cref="Korean"/> 또는 <see cref="English"/>. 켤 때 한 번 정한다.</summary>
    public static string Current { get; private set; } = Korean;

    /// <summary>언어의 이름 — 그 언어로 적는다(옮기지 않는다). 읽지 못하는 언어로 켜졌어도 제 언어를 알아본다.</summary>
    public static string NameOf(string language) => language == English ? "English" : "한국어";

    /// <summary>앱 설정의 값을 언어로 — <c>ko</c> · <c>en</c>만 받고 그 밖(비었거나 모르는 값)은 null = PC 언어 따라.</summary>
    public static string? Normalize(string? setting) => setting?.Trim().ToLowerInvariant() switch
    {
        Korean => Korean,
        English => English,
        _ => null
    };

    /// <summary>PC 언어가 정하는 화면 언어 — 한국어면 한국어, 그 밖은 영어.</summary>
    public static string OsLanguage(CultureInfo os) =>
        string.Equals(os.TwoLetterISOLanguageName, Korean, StringComparison.OrdinalIgnoreCase) ? Korean : English;

    /// <summary>앱 설정(null이면 PC 언어 따라)과 PC 언어로 화면 언어를 정한다.</summary>
    public static string Resolve(string? setting, CultureInfo os) => Normalize(setting) ?? OsLanguage(os);

    /// <summary>화면 언어를 정한다 — 문구 표가 이 언어로 꺼낸다.</summary>
    public static void Apply(string language)
    {
        Current = language == English ? English : Korean;
        Ui.Culture = CultureInfo.GetCultureInfo(Current == English ? "en" : "ko-KR");
    }

    /// <summary>켤 때 한 번 — 앱 설정과 PC 언어로 정한다(<c>Program.Main</c> · 창을 짓기 전).</summary>
    public static void ApplyAtStartup() => Apply(Resolve(AppSettingsService.LoadLanguage(), OsCulture));
}
