using System.Runtime.CompilerServices;
using Vn.App.Services;

namespace Vn.App.Tests;

/// <summary>
/// 테스트 프로세스를 <b>이 컴퓨터의 상태</b>에서 떼어 놓는다. 어셈블리가 실리는 순간
/// 한 번 돌므로 어떤 테스트보다 먼저다.
///
/// 떼는 것:
/// - <b>설정 파일</b> — 세션이 프로젝트를 열 때마다 최근 프로젝트를 설정에 적는데, 그
///   경로가 사용자의 진짜 AppData면 테스트가 <b>사용자의 최근 프로젝트를 임시 폴더로
///   덮어쓴다</b>(2026-08-26에 실제 settings.json이 그렇게 돼 있었다). 테스트 전용 파일로
///   돌려세우고, 지난 실행의 잔재가 이번 실행으로 새지 않게 지우고 시작한다.
/// - <b>최근 프로젝트 복원</b> — 창을 띄우면 Opened가 최근 프로젝트를 복원하고 챕터
///   감시자가 붙는다. 그 감시자의 에피소드 동기화가 <b>비동기로</b> 테스트 판을 다시
///   그려(워크북에 없는 대사 노드 솎아내기 포함), 스위트 부하에 따라 매번 다른 테스트가
///   흔들렸다 — PresentationGraphStageJumpTests가 단독으로는 늘 통과하던 이유다(그때는
///   최근 프로젝트가 이미 지워진 임시 폴더라 복원이 조용히 실패했다). 복원을 시험하고
///   싶은 테스트는 <see cref="MainWindow.RestoreRecentProjectOnOpen"/>을 제 스코프에서
///   켜고 끝에 되돌린다.
/// - <b>화면 언어</b> — 시험은 한국어로 돈다(english-ui E1). 이 PC의 언어가 영어여도 같은 답이 나오게 「한국어 PC · 한국어 화면」으로 고정한다.
///   영어 문구는 표에서 언어를 밝혀 꺼내 본다(<c>UiLanguageTests</c>) — 화면 언어를 바꾸는 것은 프로세스 전체라 다른 시험과 겹쳐 돌면 흔들린다.
/// - <b>이 OS의 손</b> — 맥에서는 ⌘가 단축키이고 휠이 처음부터 판을 옮긴다(<see cref="Views.PlatformKeys.IsMac"/>). 시험을 맥에서
///   돌려도(CI의 macOS 작업) 같은 답이 나오게 「맥이 아님」으로 고정한다. 맥의 손을 보는 시험은 제 스코프에서 켜고 끝에 되돌린다.
/// </summary>
internal static class TestProcessIsolation
{
    [ModuleInitializer]
    internal static void Isolate()
    {
        string path = Path.Combine(Path.GetTempPath(), "vntool-tests", "settings.json");

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // 다른 테스트 실행이 같은 파일을 쥐고 있다 — 같은 격리 파일이므로 그대로 쓴다.
        }

        // 화면 언어가 <b>맨 먼저</b>다 — 아래 줄이 MainWindow 같은 형식을 처음 건드리면 그 형식의 정적 필드가 그 자리에서 지어지는데, 언어를 정하기
        // 전이면 문구 표가 PC 언어(CurrentUICulture)로 꺼내진다. 한국어 PC에서는 드러나지 않고 영어 CI(GitHub Actions)에서만 창 제목이
        // 「Story Director dev build」로 나왔다(2026-11-25 · english-ui 뒤 CI).
        UiLanguage.OsCulture = System.Globalization.CultureInfo.GetCultureInfo("ko-KR");
        UiLanguage.Apply(UiLanguage.Korean);

        // 화면 시험은 기본 테마(클래식)의 모양 · 색을 잰다 — 앱의 처음 테마(맑은 하늘)와 따로다. 다른 테마로 전체를 돌려 보려면
        // 환경 변수 VN_TEST_THEME에 테마 이름을 준다(예: pastel-sky — 색을 재는 시험은 그때 어긋난다 · themes).
        Theming.ThemeService.StartupThemeOverride =
            Environment.GetEnvironmentVariable("VN_TEST_THEME") is { Length: > 0 } theme ? theme : Theming.ThemeCatalog.Classic.Id;

        AppSettingsService.SettingsPathOverride = path;
        MainWindow.RestoreRecentProjectOnOpen = false;
        Views.PlatformKeys.IsMac = false;
    }
}
