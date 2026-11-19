using Avalonia;
using Avalonia.Input;

namespace Vn.App.Views;

/// <summary>
/// <b>판 위의 휠이 하는 일</b> — [구성] · [대본] 두 판의 한 벌(2026-11-19 소유자 — 「트랙패드 수정을 합시다」 ·
/// <c>docs/work-orders/writer-app-orders.md</c> WA2).
///
/// 휠은 배율이다(누른 키와 무관하다 — 2026-08-18 미팅). 배율은 <b>신호의 크기에 비례한다</b> — 마우스 한 칸(±1)은 ×1.15 · ÷1.15이고, 트랙패드가
/// 한 번 쓸 때 보내는 잔 신호 수십 개는 그 크기만큼만 움직인다. <b>가로 값은 배율이 아니다</b> — 판을 가로로 옮긴다.
/// ⛔ 그 전에는 신호 하나마다 크기를 보지 않고 ×1.15(올림) · ÷1.15(그 밖)였다 — 트랙패드에서 배율이 튀었고, 가로로 쓸면(세로 값 0) 「올림이
/// 아니다」로 읽혀 축소됐다.
/// </summary>
internal static class BoardWheel
{
    /// <summary>휠 한 칸의 배율.</summary>
    internal const double ZoomStep = 1.15;

    /// <summary>휠 한 칸에 판이 옮겨지는 거리(화면 px) — <c>ScrollViewer</c>의 걸음과 같다.</summary>
    internal const double PanStep = 50;

    /// <summary>신호 하나가 한 번에 움직일 수 있는 칸 — 빠르게 굴린 휠이 한 신호로 판을 날려 버리지 않게.</summary>
    private const double MostNotches = 4;

    /// <summary>휠 신호 하나가 판에 하는 일 — 배율에 곱할 값(1이면 그대로)과 스크롤 오프셋에 더할 값.</summary>
    internal readonly record struct Move(double Zoom, Vector Pan);

    /// <summary>이 휠 신호가 판에 하는 일.</summary>
    internal static Move Read(PointerWheelEventArgs args)
    {
        Vector delta = args.Delta;

        return new Move(
            Math.Pow(ZoomStep, Math.Clamp(delta.Y, -MostNotches, MostNotches)),
            new Vector(-delta.X * PanStep, 0));
    }
}
