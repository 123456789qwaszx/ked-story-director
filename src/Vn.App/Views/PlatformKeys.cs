using Avalonia.Input;

namespace Vn.App.Views;

/// <summary>
/// <b>그 OS의 손</b> — 단축키가 OS마다 갈리는 자리 한 벌(2026-11-19 소유자 — 「맥을 쓰는 사람이 있을 수도 있으니까 … 가볍게 최소로 지원」 ·
/// <c>docs/work-orders/writer-app-orders.md</c> WA1).
///
/// 맥에서는 <b>⌘를 Ctrl처럼 듣는다</b>. 맥의 ⌘는 Ctrl이 아니라 따로 오는 키(<see cref="KeyModifiers.Meta"/>)라, Ctrl만 보면 ⌘S · ⌘Z가 듣지 않는다.
/// Ctrl도 그대로 듣는다 — 화면 곳곳의 「되돌리기(Ctrl+Z)」가 맥에서도 참이고, 그 글들을 OS마다 가르지 않아도 된다. 보이는 글을 맥의 키로
/// 바꾸는 곳은 둘뿐이다: File 메뉴(<see cref="Gesture"/>)와 설정 창 단축키 표(<see cref="Label"/>).
/// </summary>
internal static class PlatformKeys
{
    /// <summary>맥인가 — 처음 값은 이 OS다. 시험이 제 스코프에서 바꾸고 끝에 되돌린다.</summary>
    internal static bool IsMac { get; set; } = OperatingSystem.IsMacOS();

    /// <summary>명령 키를 눌렀는가 — Ctrl, 맥에서는 ⌘도.</summary>
    internal static bool HasCommand(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || (IsMac && modifiers.HasFlag(KeyModifiers.Meta));

    /// <summary>
    /// 명령 키와 함께 누른 이 키가 <b>다시 실행</b>인가 — Y, 맥에서는 ⇧Z도(맥의 손은 ⌘⇧Z다). Windows의 Ctrl+Shift+Z는 다시 실행이 아니다 —
    /// 되돌리기 그대로다(2026-11-18 소유자 「그대로 둔다」 · 두 그래프 설계 7절 AU).
    /// </summary>
    internal static bool IsRedo(Key key, KeyModifiers modifiers) =>
        key == Key.Y || (IsMac && key == Key.Z && modifiers.HasFlag(KeyModifiers.Shift));

    /// <summary>고른 카드를 지우는 키인가 — Delete, 맥에서는 ⌫(Backspace)도. 맥북의 delete는 ⌫이고 Delete는 fn+delete라 손이 가지 않는다.</summary>
    internal static bool IsDelete(Key key) => key == Key.Delete || (IsMac && key == Key.Back);

    /// <summary>File 메뉴에 보일 단축키 — 맥은 ⌘, 그 밖은 Ctrl.</summary>
    internal static KeyGesture Gesture(Key key, KeyModifiers extra = KeyModifiers.None) =>
        new(key, (IsMac ? KeyModifiers.Meta : KeyModifiers.Control) | extra);

    /// <summary>
    /// 단축키 표(<see cref="Shortcuts"/>)의 글을 이 OS의 키로 — 표는 「Ctrl+S」 꼴로 한 벌만 적고, 맥에서는 ⌘S · ⌘⇧Z(다시 실행) · ⌫로 보인다.
    /// </summary>
    internal static string Label(string keys) => IsMac
        ? keys.Replace("Ctrl+Y", "⌘⇧Z", StringComparison.Ordinal)
            .Replace("Ctrl+", "⌘", StringComparison.Ordinal)
            .Replace("Delete", "⌫ (delete)", StringComparison.Ordinal)
        : keys;
}
