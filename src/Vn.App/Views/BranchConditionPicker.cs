using Avalonia.Controls;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.Authoring.Chapters;

namespace Vn.App.Views;

/// <summary>
/// <b>분기의 조건을 고르는 칸</b> — [대본] 탭의 분기 카드(대사 편집기)와 분기 선 인스펙터가 같은 칸을 쓴다(2026-11-17 소유자: 조건 이름을
/// [대본] 탭에서 만들었으니 분기의 조건도 거기서 바로 고른다). [구성] 탭 분기 패널의 콤보와 같은 일을 한다 — 규칙은
/// <see cref="BranchEntryCondition"/> 하나다.
///
/// 목록은 「(언제나)」와 그 챕터의 조건 이름이다. <b>식 미정인 조건은 보이되 고를 수 없다</b> — 분기의 조건은 다녀갈 카드의 첫머리에
/// 식으로 서는데(<c>&lt;&lt;if&gt;&gt;</c>) 식이 없으면 세울 것이 없다. [구성] 탭에서 식을 채우면 고를 수 있다. (간선의 표시조건 ·
/// 해금조건은 식 미정이어도 건다 — 그쪽은 이름으로 가리킨다.)
///
/// 고르는 순간 나간다(되돌리기 한 칸). 고르기가 끝난 뒤에 넣는다 — 고르는 중에 화면을 다시 세우면 고르기가 엉킨다.
/// </summary>
internal static class BranchConditionPicker
{
    /// <summary>조건을 안 건 분기 — 언제나 다녀온다.</summary>
    internal const string Always = "(언제나)";

    /// <param name="targetNodeId">다녀갈 카드 — 조건은 그 카드의 첫머리에 선다.</param>
    /// <param name="tag">칸의 Tag — 화면마다 제 표를 단다.</param>
    /// <param name="applied">넣은 뒤(성공이든 거절이든) 화면이 할 일 — 설명 글을 다시 세운다.</param>
    internal static ComboBox Create(AuthoringSession session, string targetNodeId, string tag, Action? applied = null)
    {
        BranchEntryCondition.State state = BranchEntryCondition.Read(session.Project, targetNodeId, session.Definition);
        ChapterDocument? chapter = session.Project.FindNode(targetNodeId) is { } target
            ? EpisodeNaming.ChapterOf(session.Project, target)
            : null;

        var items = new List<ComboBoxItem> { new() { Content = Always, Tag = null } };

        foreach (ChapterCondition condition in chapter?.Conditions ?? [])
        {
            bool undecided = string.IsNullOrWhiteSpace(condition.Expression);
            var item = new ComboBoxItem
            {
                Content = undecided ? $"{condition.Label} (식 미정)" : condition.Label,
                Tag = condition.Label,
                IsEnabled = !undecided
            };

            if (undecided)
            {
                ToolTip.SetTip(item, "식 미정인 조건은 분기에 걸 수 없습니다 — [구성] 탭에서 식을 채우면 고를 수 있습니다.");
            }

            items.Add(item);
        }

        // 걸려 있는데 목록에 없는 조건(직접 지은 것 등)도 보이게 한다.
        if (state.Label is { } held && !items.Any(item => Equals(item.Tag, held)))
        {
            items.Add(new ComboBoxItem { Content = held, Tag = held });
        }

        var combo = new ComboBox
        {
            FontSize = 11,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            ItemsSource = items,
            SelectedItem = items.FirstOrDefault(item => Equals(item.Tag, state.Label)) ?? items[0],
            IsEnabled = state.Editable,
            Tag = tag
        };
        ToolTip.SetTip(combo, state.Blocked ??
            "이 분기의 조건을 고릅니다 — 조건이 맞을 때만 다녀옵니다. 조건 이름은 하이어아키의 챕터 줄을 눌러 더합니다.");

        string? current = state.Label;

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not ComboBoxItem picked || Equals(picked.Tag, current))
            {
                return;
            }

            string? label = picked.Tag as string;

            Dispatcher.UIThread.Post(() => UiGuard.Run(session, "분기 조건", () =>
            {
                string name = session.Project.FindNode(targetNodeId) is { } node
                    ? EpisodeNaming.ShownNameOf(session.Project, node)
                    : targetNodeId;
                string? failure = BranchEntryCondition.Set(session.Editor, targetNodeId, label, session.Definition);

                if (failure is null)
                {
                    current = label;
                }
                else
                {
                    combo.SelectedItem = items.FirstOrDefault(item => Equals(item.Tag, current)) ?? items[0];
                }

                session.SetStatus(failure ?? (label is null
                    ? $"'{name}'의 조건을 걷었습니다 — 언제나 다녀옵니다. 되돌리기(Ctrl+Z)로 돌아옵니다."
                    : $"'{name}'은 조건 '{label}'일 때만 다녀옵니다. 되돌리기(Ctrl+Z)로 돌아옵니다."));
                applied?.Invoke();
            }));
        };

        return combo;
    }
}
