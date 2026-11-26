using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>두 번 누름은 짝수째 누름이다</b>(2026-11-27 소유자 — 「클릭에 대한 처리 로직 자체를 살펴볼 필요」). 운영체제의 누름 셈은 쉬지 않고 누르는 동안 계속 는다 —
/// 2 · 4 · 6이 두 번 누름, 1 · 3 · 5가 한 번 누름(윈도의 두 번 누름 · Avalonia의 DoubleTapped와 같다).
/// </summary>
public sealed class ClicksTests
{
    [Fact]
    public void 짝수째가_두_번_누름이고_홀수째가_한_번_누름이다()
    {
        Assert.Equal([false, true, false, true, false, true], Enumerable.Range(1, 6).Select(count => Clicks.IsDouble(count)));
        Assert.Equal([true, false, true, false, true, false], Enumerable.Range(1, 6).Select(count => Clicks.IsSingle(count)));
        Assert.False(Clicks.IsDouble(0));
    }
}
