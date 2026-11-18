using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vn.App.Services;

namespace Vn.App.Views;

/// <summary>
/// <b>지우는 일 앞의 한 걸음</b> — 차림표·단추를 누른 것이 첫 걸음이고(이름 끝의 <c>…</c>가
/// 그 뜻이다), 여기가 <b>무엇이 사라지는지 읽고 누르는</b> 두 번째다.
///
/// 주의: <b>확인 창을 띄우지 않는다.</b> 창은 방금 읽던 것을 덮는다 — 누른 자리 옆에 붙어 뜨는
/// 쪽이 <b>무엇을</b> 지우는 것인지 더 분명하다.
///
/// 두 그래프가 이 한 벌을 함께 쓴다. 화면마다 복사하면 <b>화면마다 다른 색과 다른 손잡이</b>를 갖게 된다.
///
/// <b>바깥을 누르거나 Esc면 닫힌다</b>(2026-11-18 소유자 — 「확인창을 끌 방법이 없습니다」). 플라이아웃의 바깥 누름 닫기에만 기대지 않는다 —
/// 차림표에서 띄운 칸은 차림표가 닫히며 같은 창의 바깥 누름 받이를 감춰 닫히지 않았다(데스크톱). 그래서 붙은 창의 누름 · 키를 스스로 본다.
/// </summary>
internal static class ConfirmFlyout
{
    /// <param name="anchor">붙어 뜰 자리 — 사람이 방금 누른 것 옆이어야 한다.</param>
    /// <param name="caution">무엇이 사라지는지. <b>되돌릴 수 있는지</b>까지 적는다.</param>
    /// <param name="closed">칸이 닫힐 때 한 번 — 확인을 눌렀든 바깥을 눌렀든 Esc든.</param>
    /// <returns>
    /// 뜬 확인 단추 — <b>테스트의 손잡이</b>다. 플라이아웃의 팝업은 창 밖에 살아
    /// 나무를 타고 내려가 찾을 수 없다.
    /// </returns>
    public static Button Show(
        Control anchor,
        AuthoringSession? session,
        string caution,
        string confirmText,
        Action act,
        Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(act);

        var panel = new StackPanel { Spacing = 6, MaxWidth = 260 };

        panel.Children.Add(new TextBlock
        {
            Text = caution,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85
        });

        var flyout = new Flyout { Content = panel };
        TopLevel? window = TopLevel.GetTopLevel(anchor);
        bool finished = false;

        // 붙은 창의 누름 — 칸 안(겹친 층에 뜰 때)이 아니면 닫는다. 처리된 누름도 본다(카드 · 줄이 먼저 받아 Handled로 막는다).
        void OnPress(object? sender, PointerPressedEventArgs args)
        {
            if (args.Source is Visual source && (ReferenceEquals(source, panel) || panel.IsVisualAncestorOf(source)))
            {
                return;
            }

            flyout.Hide();
        }

        void OnKey(object? sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                flyout.Hide();
            }
        }

        // 어떻게 닫히든 한 번 — 창의 손잡이를 떼고 부른 쪽에 알린다(시험 손잡이가 비워진다).
        void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            window?.RemoveHandler(InputElement.PointerPressedEvent, OnPress);
            window?.RemoveHandler(InputElement.KeyDownEvent, OnKey);
            closed?.Invoke();
        }

        flyout.Closed += (_, _) => Finish();

        var confirm = new Button
        {
            Content = confirmText,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,

            // 지우는 단추는 붉다 — 누르기 전에 색이 먼저 말한다.
            Foreground = new SolidColorBrush(Color.FromRgb(190, 60, 60))
        };

        confirm.Click += (_, _) => UiGuard.Run(session, confirmText, () =>
        {
            flyout.Hide();
            Finish();
            act();
        });

        panel.Children.Add(confirm);
        flyout.ShowAt(anchor);

        // 띄운 뒤에 단다 — 띄운 누름(단추를 누른 것)이 곧바로 닫지 않게.
        window?.AddHandler(InputElement.PointerPressedEvent, OnPress, RoutingStrategies.Tunnel, handledEventsToo: true);
        window?.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);

        return confirm;
    }
}
