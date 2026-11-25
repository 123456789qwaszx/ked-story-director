using Avalonia;
using Avalonia.Input;
using Vn.App.Services;

namespace Vn.App.Views;

/// <summary>
/// <b>판 위의 휠이 하는 일</b> — [구성] · [대본] 두 판의 한 벌(2026-11-19 소유자 — 「트랙패드 수정을 합시다」 ·
/// writer-app WA2 · WA3).
///
/// 휠이 하는 일은 스위치 하나가 가른다 — 설정 창 「기타」의 <b>「휠로 판 옮기기」</b>(<see cref="Pans"/> · 이 PC의 앱 설정):
/// <list type="bullet">
/// <item>꺼짐 — 휠은 배율이다. 누른 키와 무관하다(2026-08-18 미팅에서 Ctrl 요구가 빠졌다). 가로 값만은 판을 가로로 옮긴다.</item>
/// <item>켜짐 — 휠은 판을 옮기고(Shift+휠은 가로), 배율은 Ctrl(맥은 ⌘도)+휠이다. 트랙패드의 손이다 — 두 손가락 쓸기가 곧 휠이고,
/// 트랙패드 · 매직 마우스에는 판을 옮길 가운데 단추가 없다.</item>
/// </list>
/// 처음 값은 맥만 켜짐이다. 휠 마우스와 터치패드는 신호로 가를 수 없어 스위치로 둔다(Windows 노트북 작가는 켠다). 맥 트랙패드의 <b>집기</b>는
/// 스위치와 무관하게 배율이다(<see cref="Magnify"/>).
///
/// 배율은 <b>신호의 크기에 비례한다</b> — 마우스 한 칸(±1)은 ×1.15 · ÷1.15이고, 트랙패드가 한 번 쓸 때 보내는 잔 신호 수십 개는 그 크기만큼만
/// 움직인다. ⛔ 그 전에는 신호 하나마다 크기를 보지 않고 ×1.15(올림) · ÷1.15(그 밖)였다 — 트랙패드에서 배율이 튀었고, 가로로 쓸면(세로 값 0)
/// 「올림이 아니다」로 읽혀 축소됐다.
/// </summary>
internal static class BoardWheel
{
    /// <summary>휠 한 칸의 배율.</summary>
    internal const double ZoomStep = 1.15;

    /// <summary>휠 한 칸에 판이 옮겨지는 거리(화면 px) — <c>ScrollViewer</c>의 걸음과 같다.</summary>
    internal const double PanStep = 50;

    /// <summary>신호 하나가 한 번에 움직일 수 있는 칸 — 빠르게 굴린 휠이 한 신호로 판을 날려 버리지 않게.</summary>
    private const double MostNotches = 4;

    private static bool _read;
    private static bool? _chosen;

    /// <summary>
    /// 휠이 판을 옮기는가 — 고른 값(처음 읽을 때 앱 설정에서 가져온다), 고른 적이 없으면 이 OS의 손이다: 맥은 옮기기, 그 밖은 배율.
    /// </summary>
    internal static bool Pans
    {
        get
        {
            if (!_read)
            {
                _chosen = AppSettingsService.LoadWheelPansBoard();
                _read = true;
            }

            return _chosen ?? PlatformKeys.IsMac;
        }
    }

    /// <summary>고르고 기억한다 — null이면 고른 적 없는 것으로 돌린다(이 OS의 손을 따른다). 다음 휠부터 따른다.</summary>
    internal static void Set(bool? pans)
    {
        if (_read && _chosen == pans)
        {
            return;
        }

        _chosen = pans;
        _read = true;
        AppSettingsService.SaveWheelPansBoard(pans);
    }

    /// <summary>휠 신호 하나가 판에 하는 일 — 배율에 곱할 값(1이면 그대로)과 스크롤 오프셋에 더할 값.</summary>
    internal readonly record struct Move(double Zoom, Vector Pan);

    /// <summary>이 휠 신호가 판에 하는 일.</summary>
    internal static Move Read(PointerWheelEventArgs args)
    {
        Vector delta = args.Delta;

        if (!Pans || PlatformKeys.HasCommand(args.KeyModifiers))
        {
            return new Move(
                Math.Pow(ZoomStep, Math.Clamp(delta.Y, -MostNotches, MostNotches)),
                new Vector(-delta.X * PanStep, 0));
        }

        // 옮기기 — Shift+휠은 가로다(세로 휠뿐인 마우스의 손 · ScrollViewer와 같다).
        if (args.KeyModifiers.HasFlag(KeyModifiers.Shift) && delta.X == 0)
        {
            delta = new Vector(delta.Y, 0);
        }

        return new Move(1, new Vector(-delta.X * PanStep, -delta.Y * PanStep));
    }

    /// <summary>
    /// 트랙패드 집기 신호 하나가 배율에 곱할 값 — 맥이 보내는 값은 「지난 신호보다 얼마나 벌렸나」(0.02면 2% 키우기)다. 스위치와 무관하다.
    /// </summary>
    internal static double Magnify(Vector delta) => 1 + Math.Clamp(delta.X, -0.5, 0.5);
}
