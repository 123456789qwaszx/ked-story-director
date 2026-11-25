using Vn.App.Strings;

namespace Vn.App.Services;

/// <summary>
/// <b>앱의 이름</b> 한 벌 (2026-11-19 소유자 — 「앱 이름과 아이콘 그림을 당신이 먼저 어울리는 것으로」 · <c>docs/work-orders/writer-app-orders.md</c> WA4).
///
/// 보이는 이름은 「스토리 디렉터」다 — 저장소 이름(story-director) 그대로이고 연출 툴(compositor)과 짝이다. 실행 파일 이름 <c>StoryDirector</c>는
/// 프로젝트 파일(<c>Vn.App.csproj</c>의 <c>AssemblyName</c>)이 정하고, 꾸러미 이름은 배포 스크립트가 정한다. 솔루션 · 프로젝트 · 네임스페이스
/// (<c>VnTool.sln</c> · <c>Vn.App</c>)는 안의 이름이라 그대로다. ⛔ 그 전에 보이던 이름은 「VnTool」 · <c>Vn.App.exe</c>였다.
/// </summary>
internal static class AppIdentity
{
    /// <summary>작가에게 보이는 이름 — 창 제목 · 시작 실패 창 · 로그의 말.</summary>
    internal static string Name => Ui.App_Name;

    /// <summary>판 번호를 정하지 않고 지은 빌드의 이름 — 개발 중 실행 · 시험.</summary>
    internal static string DevelopmentVersion => Ui.App_DevBuild;

    /// <summary>
    /// <b>판 번호</b>(2026-11-19 · writer-release WR2) — 작가가 「안 돼요」라고 할 때 어느 판인지 알 수 있게 창 제목에 선다. 꾸러미를 구울 때
    /// 정한다(<c>-p:Version=0.1.0</c>). 손으로 지은 빌드(0.0.0)는 <see cref="DevelopmentVersion"/>.
    /// </summary>
    /// 화면 언어를 따르는 「개발판」이 들 수 있어 그때그때 읽는다 — 켤 때 언어를 정하기 전에 이 클래스가 먼저 실린다(english-ui E2).
    internal static string Version => VersionOf(
        typeof(AppIdentity).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion);

    /// <summary>창 제목의 앞 — 「스토리 디렉터 0.1.0」.</summary>
    internal static string TitleName => $"{Name} {Version}";

    /// <summary>
    /// 빌드가 적은 판 번호를 보이는 번호로 — 빌드 도구가 붙이는 커밋 꼬리(<c>+abc123…</c>)는 떼고, 0.0.0 · 빈 값은 <see cref="DevelopmentVersion"/>이다.
    /// </summary>
    internal static string VersionOf(string? informational)
    {
        string number = (informational ?? string.Empty).Split('+')[0].Trim();
        return number.Length == 0 || number == "0.0.0" ? DevelopmentVersion : number;
    }

    /// <summary>
    /// 이 PC의 설정(<see cref="AppSettingsService"/>) · 로그(<see cref="StartupLog"/>)가 사는 폴더의 이름.
    ///
    /// ⛔ <b>앱의 이름을 따라 바꾸지 않는다.</b> 바꾸면 이미 쓰던 작가의 최근 프로젝트 · 글꼴 · 스위치가 새 판에서 한꺼번에 사라진다 —
    /// 앱의 이름과 저장 자리의 이름은 다른 것이다. 꼭 옮겨야 한다면 옛 폴더의 것을 읽어 새 폴더로 옮기는 길부터 세운다.
    /// </summary>
    internal const string DataFolder = "VnTool";
}
