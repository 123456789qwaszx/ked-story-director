using Avalonia;
using Avalonia.Controls;
using Vn.App.Services;

namespace Vn.App.Tests;

/// <summary>
/// <b>앱의 이름</b> (2026-11-19 소유자 — 「앱 이름 … 당신이 먼저 어울리는 것으로」 · <c>docs/work-orders/writer-app-orders.md</c> WA4).
///
/// 보이는 이름은 「스토리 디렉터」, 실행 파일은 <c>StoryDirector</c>다. 이 PC의 설정 · 로그 폴더 이름은 앱의 이름을 따라가지 않는다.
/// </summary>
public sealed class AppIdentityTests
{
    [Fact]
    public void 창_제목은_앱_이름과_판_번호와_프로젝트_이름이다() => HeadlessUi.Run(() =>
    {
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 시험 빌드는 판 번호를 정하지 않았다(0.0.0) — 「개발판」(writer-release WR2).
        Assert.StartsWith("스토리 디렉터 개발판 — ", window.Title, StringComparison.Ordinal);

        window.Close();
    });

    [Theory]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("0.1.0+8202731abcdef", "0.1.0")]   // 빌드 도구가 붙이는 커밋 꼬리는 뗀다
    [InlineData("1.2.3-beta.1+abc", "1.2.3-beta.1")]
    [InlineData("0.0.0", "개발판")]
    [InlineData("0.0.0+abc", "개발판")]
    [InlineData("", "개발판")]
    [InlineData(null, "개발판")]
    public void 판_번호는_구울_때_정한_번호이고_정하지_않았으면_개발판이다(string? informational, string shown) =>
        Assert.Equal(shown, AppIdentity.VersionOf(informational));

    [Fact]
    public void 실행_파일_이름은_StoryDirector다()
    {
        // 배포 스크립트(publish-windows.ps1 · publish-macos.sh)와 CI가 이 이름의 실행 파일을 찾는다 — 바꾸면 그 셋을 함께 고친다.
        Assert.Equal("StoryDirector", typeof(App).Assembly.GetName().Name);
    }

    [Fact]
    public void 맥의_앱_메뉴에는_앱_이름이_서고_Avalonia가_채우는_줄이_없다() => HeadlessUi.Run(() =>
    {
        Application app = Application.Current!;

        Assert.Equal("스토리 디렉터", app.Name);
        Assert.Empty(Assert.IsType<NativeMenu>(NativeMenu.GetMenu(app)).Items);   // 비어 있지 않으면(null) Avalonia가 「About Avalonia」를 세운다
    });

    [Fact]
    public void 이_PC의_설정과_로그_폴더_이름은_VnTool_그대로다()
    {
        // ⛔ 앱의 이름을 따라 바꾸지 않는다 — 바꾸면 이미 쓰던 작가의 최근 프로젝트 · 글꼴 · 스위치가 새 판에서 사라진다(AppIdentity.DataFolder).
        Assert.Equal("VnTool", AppIdentity.DataFolder);
        Assert.Contains(Path.Combine("VnTool", "logs"), StartupLog.FilePath, StringComparison.Ordinal);
    }
}
