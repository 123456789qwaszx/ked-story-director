namespace Vn.App.Tests;

/// <summary>
/// 주 창의 두 탭 — 시험은 탭을 번호가 아니라 이 이름으로 고른다(2026-11-19 · usability UX23 — 탭 순서를 [대본] · [구성]으로 바꿀 때 번호로
/// 고르던 시험 90곳이 함께 흔들렸다). 번호는 <c>MainWindow.axaml</c>의 차례이고, <see cref="MainChromeTests"/>가 그 차례를 못 박는다.
/// </summary>
internal static class MainTab
{
    /// <summary>[대본] — 첫째 탭. 창을 열거나 새 프로젝트를 만들면 이 탭이 선다.</summary>
    public const int Script = 0;

    /// <summary>[구성].</summary>
    public const int Structure = 1;
}
