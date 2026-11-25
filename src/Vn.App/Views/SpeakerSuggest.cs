using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Vn.Authoring.Text;

namespace Vn.App.Views;

/// <summary>
/// <b>화자 칸의 자동완성 — 첫 글자 · 자음부터</b>(2026-11-18 소유자 — 「자동완성의 도움을 받기위해서는 2글자 이상을 입력해야 … 자음만 입력하거나,
/// 혹은 처음 한 글자만 입력하더라도 바로 자동완성이 시작되도록」 · writer-comfort WC3).
///
/// 한글은 치는 글자가 <b>조합 중</b>인 동안 칸의 글(<see cref="AutoCompleteBox.Text"/>)에 들어가지 않는다 — 둘째 글자를 쳐야 첫 글자가 확정되어,
/// 칸의 글만 보는 자동완성은 둘째 글자부터 움직였다. 그래서 칸 안의 글 표시기(<see cref="TextPresenter"/>)가 들고 있는 조합 중인 글
/// (<see cref="TextPresenter.PreeditTextProperty"/>)까지 더해 이름을 거르고, 후보를 바로 연다. 거르기는 <see cref="Hangul.Matches"/> 한 벌 —
/// 초성(「ㅅ」 · 「ㅅㅌ」)도 맞는다. 칸이 스스로 거르지 않게(<see cref="AutoCompleteFilterMode.None"/>) 후보 목록을 여기서 바꿔 끼운다.
///
/// <b>후보는 칸이 거를 때(<see cref="AutoCompleteBox.Populating"/>)만 바꿔 끼운다</b>(2026-11-26 소유자 — 「마우스로 원하는 화자를 클릭했음에도 화자 이름이
/// 완성되지 않고 힌트만 꺼진다」 · 「키보드 화살표로 고르고 Enter로 넣게」). ⛔ 그 전에는 칸의 글이 바뀔 때마다 바꿔 끼웠다 — 후보를 고르면(누르기 · 화살표)
/// 칸이 그 이름을 칸에 쓰는데, 그것도 「글이 바뀜」이라 목록을 그 이름 하나로 다시 걸러 닫았고, 고른 것이 목록과 함께 사라져 칸이 치던 글로 돌아갔다.
/// 칸은 사람이 친 글에만 거르고, 고르기로 바뀐 글에는 거르지 않는다 — 그래서 화살표로 후보 사이를 오가고(칸에 그 이름이 미리 보인다) Enter · 누르기로
/// 넣고 Esc로 치던 글로 돌아간다(Avalonia 자동완성 그대로).
/// </summary>
internal static class SpeakerSuggest
{
    /// <summary>그 화자 칸에 붙인다 — <paramref name="names"/>는 부를 때마다 지금 화자 목록을 낸다.</summary>
    public static void Attach(AutoCompleteBox box, Func<IReadOnlyList<string>> names)
    {
        box.FilterMode = AutoCompleteFilterMode.None;
        box.MinimumPrefixLength = 0;
        string composing = string.Empty;

        // 그 글로 거른 후보 — 이미 그 이름 하나로 좁혀졌으면 비운다(목록을 열지 않는다 — 다 친 뒤에 목록이 떠 칸을 가리지 않게).
        List<string> Matches(string query)
        {
            List<string> shown = names().Where(name => Hangul.Matches(name, query)).ToList();
            return shown.Count == 1 && string.Equals(shown[0], query, StringComparison.Ordinal) ? [] : shown;
        }

        // 칸이 거를 때(사람이 친 글) — 후보를 바꿔 끼우고, 열고 닫기는 칸에 맡긴다.
        box.Populating += (_, populating) =>
        {
            populating.Cancel = true;
            box.ItemsSource = Matches((populating.Parameter ?? string.Empty) + composing);
            box.PopulateComplete();
        };

        // 방금 등록한 화자도 후보에 — 카드를 다시 짓지 않아도(다음에 칠 때 걸러진다).
        box.GotFocus += (_, _) =>
        {
            if (!box.IsDropDownOpen)
            {
                box.ItemsSource = names();
            }
        };

        box.TemplateApplied += (_, applied) =>
        {
            if (applied.NameScope.Find<TextBox>("PART_TextBox") is not { } text)
            {
                return;
            }

            text.TemplateApplied += (_, inner) =>
            {
                if (inner.NameScope.Find<TextPresenter>("PART_TextPresenter") is not { } presenter)
                {
                    return;
                }

                presenter.PropertyChanged += (_, change) =>
                {
                    if (change.Property != TextPresenter.PreeditTextProperty)
                    {
                        return;
                    }

                    string before = composing;
                    composing = presenter.PreeditText ?? string.Empty;

                    // 조합하던 중에 후보를 골랐다(누르기) — 칸이 그 이름을 칸에 쓰면 글자 겹이 조합 글자를 지우지만, 입력기는 아직 그 글자를 조합하고 있어
                    // 다음 키에 칸에 들어온다(「샤통ㅈ」). 그 조합을 버리게 하고, 목록은 고른 그대로 둔다(다시 거르면 고른 이름 하나로 좁혀 닫힌다).
                    if (composing.Length == 0 && before.Length > 0 &&
                        box.SelectedItem is string chosen && string.Equals(text.Text, chosen, StringComparison.Ordinal))
                    {
                        DropComposition(text);
                        return;
                    }

                    ShowComposing();
                };
            };
        };

        // 입력기에 지금 조합을 버리게 한다 — 칸의 입력기 연결을 끊었다 잇는다(초점이 다른 데로 갔다 온 것과 같은 길 · Avalonia가 그때 조합을 버린다).
        static void DropComposition(TextBox text)
        {
            InputMethod.SetIsInputMethodEnabled(text, false);
            InputMethod.SetIsInputMethodEnabled(text, true);
        }

        // 조합 중인 글자는 칸의 글이 아니라 칸이 거르지 않는다 — 여기서 거르고, 닫혀 있으면 열어 칸이 거르게 한다(그때 위의 거르기가 조합 중인 글을 더한다).
        void ShowComposing()
        {
            if (!box.IsKeyboardFocusWithin)
            {
                return;
            }

            List<string> shown = Matches((box.Text ?? string.Empty) + composing);

            if (box.IsDropDownOpen || shown.Count == 0)
            {
                box.ItemsSource = shown;

                if (shown.Count == 0)
                {
                    box.SetCurrentValue(AutoCompleteBox.IsDropDownOpenProperty, false);
                }

                return;
            }

            box.SetCurrentValue(AutoCompleteBox.IsDropDownOpenProperty, true);
        }
    }
}
