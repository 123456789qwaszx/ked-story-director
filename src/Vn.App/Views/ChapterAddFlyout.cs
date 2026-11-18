using Avalonia.Controls;
using Avalonia.Layout;
using Vn.App.Services;

namespace Vn.App.Views;

/// <summary>
/// <b>[＋ 챕터]의 창구</b> — 작가가 적은 이름을 받아 <see cref="AuthoringSession.CreateNamedChapter"/>로 넘긴다(ID는 자동).
///
/// <b>창구는 이것 하나다</b>: [대본] 탭 하이어아키의 [＋] · 하이어아키 우클릭 「챕터 추가」 · [구성] 탭 빈 판
/// 한가운데 단추가 함께 쓴다. 창구가 두 벌이면 <b>어느 쪽으로 만들었느냐에 따라 챕터가 달라진다</b>.
///
/// 주의: 만든 <b>뒤</b>의 일은 화면마다 다르므로 <paramref name="created"/>로 넘긴다
/// (챕터 그래프는 제 목록을 다시 읽고 그 챕터를 고른다).
/// </summary>
internal static class ChapterAddFlyout
{
    public static void ShowAt(Control anchor, AuthoringSession session, Action<string>? created = null)
    {
        var panel = new StackPanel { Spacing = 4, MinWidth = 220 };

        var name = new TextBox
        {
            PlaceholderText = "챕터 이름 (예: 축제 전날)",
            FontSize = 11
        };

        panel.Children.Add(name);

        var flyout = new Flyout { Content = panel };

        var create = new Button
        {
            Content = "만들기",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        void Submit() => UiGuard.Run(session, "새 챕터", () =>
        {
            // 작가는 이름만 적는다 — ID(진행 JSON 파일 이름)는 자동이다(display-names N2).
            (string? chapterId, string? failure) = session.CreateNamedChapter(name.Text);

            if (failure is not null || chapterId is null)
            {
                session.SetStatus(failure ?? "챕터를 만들지 못했습니다.");
                return;
            }

            flyout.Hide();
            created?.Invoke(chapterId);
            string shown = session.Editor.FindChapter(chapterId)!.ShownName;
            session.SetStatus($"챕터 「{shown}」를 만들었습니다. 하이어아키에서 그 챕터 줄을 우클릭해 「장면 추가」로 첫 장면과 에피소드를 세웁니다.");
        });

        create.Click += (_, _) => Submit();

        // 이름을 치고 바로 Enter — 이 창구는 한 칸짜리라 마우스로 돌아갈 이유가 없다.
        name.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
            {
                args.Handled = true;
                Submit();
            }
        };

        panel.Children.Add(create);

        flyout.ShowAt(anchor);
        name.Focus();
    }
}
