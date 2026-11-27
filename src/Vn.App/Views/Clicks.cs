using Avalonia.Input;

namespace Vn.App.Views;

/// <summary>
/// <b>두 번 누름의 셈 한 벌</b>(2026-11-27 소유자 — 「한번 더블클릭을 해서 상태를 바꾼 다음에는, 특정 시간 내에 한번만 클릭해도 바로 더블클릭을 한 듯 상태가
/// 바뀝니다」 · 「멈추지 않고 계속 클릭을 하면 그 뒤로는 더블클릭해도 아무런 반응이 없습니다」 · 「클릭에 대한 처리 로직 자체를 살펴볼 필요」).
///
/// 운영체제의 누름 셈(<see cref="PointerPressedEventArgs.ClickCount"/>)은 같은 자리를 짧은 틈으로 잇달아 누르는 동안 1 · 2 · 3 · 4 …로 계속 는다. 두 번 누름은
/// <b>짝수째</b>(2 · 4 · 6 …)이고 홀수째는 한 번 누름이다 — 윈도의 두 번 누름과 같고, Avalonia의 <c>DoubleTapped</c>도 이 셈이다. 그래서 잇달아 누르면 두 번마다
/// 한 번 오간다(셋째는 한 번 누름 — 접혔다가 곧바로 펴지지 않는다).
/// ⛔ 그 전에는 자리마다 셈이 달랐다: 「2 이상」은 셋째 · 넷째 누름도 두 번 누름이라 한 번만 눌러도 오가고 두 번 누르면 두 번 오가 제자리였고(이름표 접기 · 펴기 등),
/// 「꼭 2 · 꼭 1」은 셋째부터 다시는 맞지 않아 쉬지 않고 누르는 동안 아무 반응이 없었다(카드 머리 빈 곳의 기본 크기 고정).
/// </summary>
internal static class Clicks
{
    /// <summary>두 번 누름인가 — 짝수째 누름.</summary>
    public static bool IsDouble(int clickCount) => clickCount >= 2 && clickCount % 2 == 0;

    /// <inheritdoc cref="IsDouble(int)"/>
    public static bool IsDouble(PointerPressedEventArgs args) => IsDouble(args.ClickCount);

    /// <summary>한 번 누름인가 — 홀수째 누름(두 번 누름의 첫 누름이 될 수 있다).</summary>
    public static bool IsSingle(int clickCount) => clickCount % 2 == 1;
}
