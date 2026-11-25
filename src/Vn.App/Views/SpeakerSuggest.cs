using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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
/// </summary>
internal static class SpeakerSuggest
{
    /// <summary>그 화자 칸에 붙인다 — <paramref name="names"/>는 부를 때마다 지금 화자 목록을 낸다.</summary>
    public static void Attach(AutoCompleteBox box, Func<IReadOnlyList<string>> names)
    {
        box.FilterMode = AutoCompleteFilterMode.None;
        box.MinimumPrefixLength = 0;
        string composing = string.Empty;

        void Update(bool open)
        {
            string query = (box.Text ?? string.Empty) + composing;
            List<string> shown = names().Where(name => Hangul.Matches(name, query)).ToList();
            box.ItemsSource = shown;

            if (open)
            {
                // 이미 그 이름 하나로 좁혀졌으면 열지 않는다 — 고른 뒤에 목록이 다시 뜨면 칸을 가린다.
                bool settled = shown.Count == 1 && string.Equals(shown[0], query, StringComparison.Ordinal);
                box.IsDropDownOpen = box.IsKeyboardFocusWithin && shown.Count > 0 && !settled;
            }
        }

        box.TextChanged += (_, _) => Update(open: true);
        box.GotFocus += (_, _) => Update(open: false);   // 방금 등록한 화자도 후보에 — 카드를 다시 짓지 않아도

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
                    if (change.Property == TextPresenter.PreeditTextProperty)
                    {
                        composing = presenter.PreeditText ?? string.Empty;
                        Update(open: true);
                    }
                };
            };
        };
    }
}
