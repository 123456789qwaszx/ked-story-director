using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Vn.App.Tests;

/// <summary>
/// <b>앱은 늘 밝게 뜬다</b>(2026-11-18 소유자 — 「네. 밝게 고정해주십시오.」 · <c>docs/work-orders/light-theme-orders.md</c>). 판의 색은 코드에 밝게
/// 박혀 있어, 윈도우를 따라 어둡게 뜨면 틀만 어두워지고 카드 이름이 흰 바탕에 흰 글이 됐다. 진짜 다크 모드를 넣을 때까지는 윈도우 설정과
/// 상관없이 밝게다.
///
/// 못 박는 것: ① 앱이 밝게를 청하고(윈도우를 따르지 않는다) 창이 밝게 선다 ② 어디에도 없던 <c>ThemeForegroundBrush</c>가 풀린다 — 밝게에서는
/// 정의 전에 풀리던 그 검정이다.
/// </summary>
public sealed class LightThemeTests
{
    [Fact]
    public void 앱은_윈도우_설정을_따르지_않고_늘_밝게_뜬다() => HeadlessUi.Run(() =>
    {
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);

        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);

        window.Close();
    });

    [Fact]
    public void 인스펙터_접기_제목이_부르는_글색_키가_풀린다() => HeadlessUi.Run(() =>
    {
        Assert.True(Application.Current!.TryGetResource("ThemeForegroundBrush", ThemeVariant.Light, out object? light));
        Assert.Equal(Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(light).Color);

        Assert.True(Application.Current.TryGetResource("ThemeForegroundBrush", ThemeVariant.Dark, out object? dark));
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(dark).Color);
    });
}
