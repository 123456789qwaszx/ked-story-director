using System.Collections;
using Avalonia.Media;

namespace Vn.App.Views;

/// <summary>
/// <b>한글은 이름으로 찾은 한글 글꼴로 그린다</b>(2026-11-23 소유자 — 「한글 글꼴이 기울여 지지 않는 건 어쩔 수 없는걸까?」 · writer-formatting WF-D17).
///
/// 앱의 기본 글꼴(Inter)에는 한글이 없어 한글은 늘 <b>대체 글꼴</b>로 그려진다. Avalonia 12.1.1은 대체 글꼴로 찾은 글자에는 가짜 기울임 · 가짜 굵게를
/// 하지 않는다(<c>FontCollectionBase.BuildTypefaceWithSynthesis</c> — .ttc 묶음 글꼴에서 다른 글꼴이 잡힐까 봐 일부러 껐다). 한글 글꼴에는 기울임꼴이
/// 없어서 기울임을 청해도 같은 줄의 영문만 기울고 한글은 서 있었다. 한글 범위를 이 글꼴들에 먼저 맡기면(<see cref="FontManagerOptions.FontFallbacks"/> —
/// 글꼴 관리자가 다른 무엇보다 먼저 본다) <b>이름으로 찾는 길</b>을 타서, 기울임을 청한 한글이 비스듬히 그려진다(Skia의 가짜 기울임 — 워드 · 한글이 하는 것).
///
/// 보통 한글은 전과 같은 글꼴이다 — 윈도는 맑은 고딕, 맥은 Apple SD 산돌고딕 Neo(그 PC의 것을 앞에 둔다). 다 없으면 전처럼 시스템이 고른다. 한자 ·
/// 문장 부호는 맡기지 않는다(전과 같다).
///
/// ⚠ <b>그 PC에 실제로 있는 글꼴만, 시스템이 쓰는 이름 그대로</b> 맡긴다(<see cref="Installed"/> — 처음 쓸 때 시스템 글꼴 목록에서 고른다). ⛔ 처음에는 후보를
/// 그대로 넘겼는데, 없는 글꼴(리눅스의 맑은 고딕)은 시스템이 비슷한 다른 글꼴을 대신 내주고 Avalonia가 그 이름 어긋남 때문에 가짜 기울임 글꼴을 캐시에서
/// 못 찾아 그릴 때마다 글꼴 파일을 통째로 다시 읽었다 — 기울인 한글을 그릴수록 메모리가 수백 MB씩 늘었다(화면 시험 전체가 메모리 부족으로 죽었다).
/// ⚠ <b>Avalonia를 올리면 다시 본다</b> — 시험(<c>HangulFontTests</c>)이 기울임을 청한 한글이 기울어 그려지는지 픽셀로 잰다.
/// </summary>
internal static class HangulFonts
{
    /// <summary>한글 — 자모 · 호환 자모 · 자모 확장 A · 음절 · 자모 확장 B.</summary>
    private static readonly UnicodeRange Hangul = new(
    [
        new UnicodeRangeSegment(0x1100, 0x11FF),
        new UnicodeRangeSegment(0x3130, 0x318F),
        new UnicodeRangeSegment(0xA960, 0xA97F),
        new UnicodeRangeSegment(0xAC00, 0xD7AF),
        new UnicodeRangeSegment(0xD7B0, 0xD7FF)
    ]);

    /// <summary>맡길 한글 글꼴의 후보 — 그 PC의 기본 한글 글꼴이 앞이다. 시스템이 한글 이름으로 알려 줄 수도 있어 두 이름을 다 둔다.</summary>
    internal static IReadOnlyList<string> Candidates { get; } = OperatingSystem.IsMacOS()
        ? ["Apple SD Gothic Neo", "Apple SD 산돌고딕 Neo", "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "Noto Sans CJK KR", "NanumGothic", "나눔고딕"]
        : ["Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Apple SD 산돌고딕 Neo", "Noto Sans KR", "Noto Sans CJK KR", "NanumGothic", "나눔고딕"];

    /// <summary>
    /// 이 PC에 있는 후보 — 시스템 글꼴 목록의 이름 그대로, 후보 차례. 처음 물을 때 한 번 고른다(글꼴 관리자가 선 뒤 — 맡기기는 글자를 처음 찾을 때 읽힌다).
    /// </summary>
    internal static IReadOnlyList<string> Installed => _installed ??= Pick();

    private static IReadOnlyList<string>? _installed;

    private static IReadOnlyList<string> Pick() =>
        Candidates
            .Select(InstalledFonts.Named)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>앱을 세울 때 넣는 글꼴 관리자 설정 — 앱(<c>Program</c>)과 화면 시험이 같은 것을 쓴다.</summary>
    internal static FontManagerOptions Options => new() { FontFallbacks = new InstalledFallbacks() };

    /// <summary>맡기기 목록 — 글꼴 관리자가 처음 읽을 때 이 PC에 있는 글꼴로 짓는다(앱을 세울 때는 아직 글꼴 관리자가 없다).</summary>
    private sealed class InstalledFallbacks : IReadOnlyList<FontFallback>
    {
        private IReadOnlyList<FontFallback>? _list;

        private IReadOnlyList<FontFallback> List => _list ??= Installed
            .Select(name => new FontFallback { FontFamily = new FontFamily(name), UnicodeRange = Hangul })
            .ToList();

        public FontFallback this[int index] => List[index];

        public int Count => List.Count;

        public IEnumerator<FontFallback> GetEnumerator() => List.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// <b>이 PC에 있는 글꼴</b> — 시스템 글꼴 목록의 이름(대소문자 무관). 서식 · 카드의 처음 글꼴(같이 쓰는 사람이 고른 것)이 이 PC에 없으면 그 이름을 글꼴
/// 관리자에 넘기지 않고 기본 글꼴로 그린다(writer-formatting WF-D17). ⛔ 넘기면 리눅스(fontconfig)는 비슷한 다른 글꼴을 대신 내주고, Avalonia 12.1.1은
/// 그 이름 어긋남 때문에 기울임 · 굵게를 번갈아 그릴 때마다 가짜 기울임 글꼴을 다시 지어 메모리가 끝없이 늘었다. 문서가 말하던 「그 글꼴이 없는 PC에서는
/// 기본 글꼴로 보인다」를 이제 앱이 직접 한다.
/// </summary>
internal static class InstalledFonts
{
    private static Dictionary<string, string>? _names;

    private static Dictionary<string, string> Names => _names ??= Load();

    private static Dictionary<string, string> Load()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (FontFamily family in FontManager.Current.SystemFonts)
            {
                names.TryAdd(family.Name, family.Name);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // 글꼴 목록을 못 읽는 환경 — 아무 글꼴도 없는 것으로 본다(기본 글꼴로 그린다).
        }

        return names;
    }

    /// <summary>이 PC에 있으면 시스템이 쓰는 그 이름, 없으면 null.</summary>
    internal static string? Named(string? family) =>
        string.IsNullOrWhiteSpace(family) ? null : Names.GetValueOrDefault(family.Trim());
}
