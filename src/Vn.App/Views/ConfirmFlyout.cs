using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Strings;
using Vn.App.Theming;

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
/// <b>설명하지 않고 묻는다</b>(2026-11-26 소유자 — 「그 때는 설명을 하는 게 아니라, 정말 삭제하시겠습니까? 라고 물어야지」) — 「「이름」 에피소드를 정말
/// 삭제하시겠습니까?」 한 줄이 먼저고, 여럿이 함께 사라지는 일(장면 · 챕터)만 그 아래에 무엇이 함께 지워지는지 짧게 한 줄 붙인다. 단추는 윈도우의 확인
/// 창처럼 [삭제] [취소]다. ⛔ 그 전에는 무엇이 사라지고 되돌리기로 돌아온다는 설명문과 확인 단추 하나였다.
///
/// <b>바깥을 누르거나 Esc면 닫힌다</b>(2026-11-18 소유자 — 「확인창을 끌 방법이 없습니다」). 플라이아웃의 바깥 누름 닫기에만 기대지 않는다 —
/// 차림표에서 띄운 칸은 차림표가 닫히며 같은 창의 바깥 누름 받이를 감춰 닫히지 않았다(데스크톱). 그래서 붙은 창의 누름 · 키를 스스로 본다.
/// </summary>
internal static class ConfirmFlyout
{
    /// <param name="anchor">붙어 뜰 자리 — 사람이 방금 누른 것 옆이어야 한다.</param>
    /// <param name="question">묻는 말 — 「「이름」 에피소드를 정말 삭제하시겠습니까?」. 설명문이 아니다.</param>
    /// <param name="detail">함께 지워지는 것 한 줄(장면 · 챕터처럼 여럿이 함께 사라질 때만) — 없으면 null.</param>
    /// <param name="closed">칸이 닫힐 때 한 번 — 확인을 눌렀든 [취소] · 바깥을 눌렀든 Esc든.</param>
    /// <returns>
    /// 뜬 확인 단추 — <b>테스트의 손잡이</b>다. 플라이아웃의 팝업은 창 밖에 살아
    /// 나무를 타고 내려가 찾을 수 없다.
    /// </returns>
    public static Button Show(
        Control anchor,
        AuthoringSession? session,
        string question,
        string confirmText,
        Action act,
        string? detail = null,
        Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(act);

        var panel = new StackPanel { Spacing = 6, MaxWidth = 300 };

        panel.Children.Add(new TextBlock
        {
            Text = question,
            FontSize = Skin.Font(12),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrEmpty(detail))
        {
            panel.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = Skin.Font(11),
                TextWrapping = TextWrapping.Wrap,
                Opacity = Skin.Dim(0.7)
            });
        }

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
            FontSize = Skin.Font(11),
            MinWidth = 72,
            HorizontalContentAlignment = HorizontalAlignment.Center,

            // 지우는 단추는 붉다 — 누르기 전에 색이 먼저 말한다.
            Foreground = Skin.DangerButton.Brush()
        };

        var cancel = new Button
        {
            Content = Ui.Confirm_Cancel,
            FontSize = Skin.Font(11),
            MinWidth = 72,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        cancel.Click += (_, _) => flyout.Hide();

        confirm.Click += (_, _) => UiGuard.Run(session, confirmText, () =>
        {
            flyout.Hide();
            Finish();
            act();
        });

        // 윈도우의 확인 창처럼 [삭제] [취소] — 오른쪽에 모아 선다.
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { confirm, cancel }
        });
        flyout.ShowAt(anchor);

        // 띄운 뒤에 단다 — 띄운 누름(단추를 누른 것)이 곧바로 닫지 않게.
        window?.AddHandler(InputElement.PointerPressedEvent, OnPress, RoutingStrategies.Tunnel, handledEventsToo: true);
        window?.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);

        return confirm;
    }
}
