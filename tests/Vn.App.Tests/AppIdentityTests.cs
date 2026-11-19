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
    public void 창_제목은_앱_이름과_프로젝트_이름이다() => HeadlessUi.Run(() =>
    {
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.StartsWith("스토리 디렉터 — ", window.Title, StringComparison.Ordinal);

        window.Close();
    });

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
