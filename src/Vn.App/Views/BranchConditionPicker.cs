using Avalonia.Controls;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.App.Strings;

namespace Vn.App.Views;

/// <summary>
/// <b>분기의 조건을 고르는 칸</b> — [대본] 탭에서 분기 카드를 고르면 대본 위쪽에 선다(2026-11-17 소유자 — 분기는 무조건 들른 뒤 그
/// 카드에서 <c>&lt;&lt;if&gt;&gt;</c>로 보므로 그 카드에서 고른다. ⛔ 같은 날 아침에는 부르는 카드의 분기 상자와 분기 선 인스펙터에 섰다).
/// [구성] 탭 칩 인스펙터의 콤보와 같은 일을 한다 — 규칙은 <see cref="BranchEntryCondition"/> 하나다.
///
/// 목록은 「(언제나)」와 그 챕터의 조건 이름이다. <b>식 미정인 조건도 고른다</b> — 식을 채울 때까지 항상 통과하고(간선과 같은 규칙),
/// [구성] 탭에서 식을 채우면 그 분기가 저절로 새 식을 따른다(사본은 이름으로 짝짓는다 — <see cref="ChapterBoardSupply"/>).
///
/// 고르는 순간 나간다(되돌리기 한 칸). 고르기가 끝난 뒤에 넣는다 — 고르는 중에 화면을 다시 세우면 고르기가 엉킨다.
/// </summary>
internal static class BranchConditionPicker
{
    /// <summary>조건을 안 건 분기 — 언제나 다녀온다.</summary>
    internal static string Always => Ui.Branch_Always;

    /// <param name="targetNodeId">분기 카드 — 조건은 그 카드의 첫머리에 선다.</param>
    /// <param name="tag">칸의 Tag — 화면마다 제 표를 단다.</param>
    /// <param name="applied">넣은 뒤(성공이든 거절이든) 화면이 할 일 — 설명 글을 다시 세운다.</param>
    internal static ComboBox Create(AuthoringSession session, string targetNodeId, string tag, Action? applied = null)
    {
        BranchEntryCondition.State state = BranchEntryCondition.Read(session.Project, targetNodeId);
        ChapterDocument? chapter = session.Project.FindNode(targetNodeId) is { } target
            ? EpisodeNaming.ChapterOf(session.Project, target)
            : null;

        var items = new List<ComboBoxItem> { new() { Content = Always, Tag = null } };

        foreach (ChapterCondition condition in chapter?.Conditions ?? [])
        {
            bool undecided = string.IsNullOrWhiteSpace(condition.Expression);
            var item = new ComboBoxItem
            {
                Content = undecided ? string.Format(Ui.Branch_Undecided, condition.Label) : condition.Label,
                Tag = condition.Label
            };

            if (undecided)
            {
                ToolTip.SetTip(item, Ui.Branch_UndecidedAlwaysPassesForNow);
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
            Ui.Branch_ChooseThisBranchSCondition);

        string? current = state.Label;

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not ComboBoxItem picked || Equals(picked.Tag, current))
            {
                return;
            }

            string? label = picked.Tag as string;

            Dispatcher.UIThread.Post(() => UiGuard.Run(session, Ui.Branch_BranchCondition, () =>
            {
                string name = session.Project.FindNode(targetNodeId) is { } node
                    ? EpisodeNaming.ShownNameOf(session.Project, node)
                    : targetNodeId;
                string? failure = BranchEntryCondition.Set(session.Editor, targetNodeId, label);

                if (failure is null)
                {
                    current = label;
                }
                else
                {
                    combo.SelectedItem = items.FirstOrDefault(item => Equals(item.Tag, current)) ?? items[0];
                }

                session.SetStatus(failure ?? (label is null
                    ? string.Format(Ui.Branch_RemovedConditionOfItS, name)
                    : BranchConditionNote.IsUndecided(session.Project, targetNodeId, label)
                        ? string.Format(Ui.Branch_SetConditionOnItS, name, label)
                        : string.Format(Ui.Branch_IsVisitedOnlyWhenCondition, name, label)));
                applied?.Invoke();
            }));
        };

        return combo;
    }
}
