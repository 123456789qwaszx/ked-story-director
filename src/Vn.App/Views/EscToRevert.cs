using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Vn.App.Views;

/// <summary>
/// <b>Esc = 넣기 전 값으로</b> — 글 칸에 적던 것을 걷고 지금 값(마지막으로 넣은 값)으로 돌린다. 초점은 그대로다. [대본]의 글 칸이 하던
/// 규칙을 [구성] 인스펙터의 글 칸에도 건다(2026-11-18 소유자 — 「대본탭과 맞춰주십시오」 · <c>docs/work-orders/inspector-boxes-pan-orders.md</c> J1).
/// 칸 안의 처리보다 먼저(터널) 받는다.
/// </summary>
internal static class EscToRevert
{
    /// <param name="now">지금 값 — 누를 때 읽는다(칸을 만든 뒤에 값이 바뀌었을 수 있다).</param>
    internal static void Wire(TextBox box, Func<string?> now) =>
        box.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Escape && args.KeyModifiers == KeyModifiers.None)
            {
                args.Handled = true;
                box.Text = now() ?? string.Empty;
            }
        }, RoutingStrategies.Tunnel);
}
