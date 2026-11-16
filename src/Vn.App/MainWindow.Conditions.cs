using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;

namespace Vn.App;

/// <summary>
/// [대본] 탭의 <b>조건 이름</b> — 하이어아키의 챕터 줄을 고르면 인스펙터의 스탯 표 아래에 선다(<c>docs/work-orders/writer-fields-orders.md</c> W4).
///
/// 작가가 조건을 뜻으로 부르는 이름(「하린과 친해졌을 때」)을 더하고 고치고 지운다. 식(<c>trust &gt;= 3</c>)은 [구성] 탭이 정한다 — 식이 빈
/// 조건은 「식 미정」으로 항상 통과하고 경고만 한다. 이름을 고치면 그 이름을 가리키던 간선(표시조건 · 해금조건)이 따라간다
/// (<see cref="ProjectEditor.RenameChapterCondition"/>). 지우기는 쓰는 곳이 있으면 어디인지 말하고 거절한다.
///
/// 칸은 초점이 떠날 때와 Enter에 넣고, Esc는 넣지 않은 글을 걷는다. 넣기는 되돌리기 한 칸이고 요약이 다시 선다.
/// </summary>
public partial class MainWindow
{
    /// <summary>챕터 요약 칸의 조건 목록 — 이름 칸 · 식(읽기만) · ✕ 줄들과 맨 아래 더하기 줄.</summary>
    private void ConditionList(string chapterId)
    {
        if (_session.Editor.FindChapter(chapterId) is not { } chapter)
        {
            return;
        }

        Heading("조건");
        Note("간선의 표시조건 · 해금조건에 고를 이름입니다 — 뜻으로 적습니다(예: 하린과 친해졌을 때). 식은 [구성] 탭에서 정합니다. " +
             "식이 비면 「식 미정」으로 항상 통과합니다.");

        var rows = new StackPanel { Spacing = 3 };

        foreach (ChapterCondition condition in chapter.Conditions)
        {
            rows.Children.Add(ConditionRow(chapterId, condition));
        }

        if (chapter.Conditions.Count == 0)
        {
            Note("이 챕터에는 아직 조건이 없습니다.");
        }

        DirectorSummary.Children.Add(rows);

        var addBox = new TextBox { FontSize = 12, PlaceholderText = "새 조건 이름", Tag = "condition-add-name" };
        var addButton = new Button
        {
            Content = "＋ 추가",
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = "condition-add"
        };
        ToolTip.SetTip(addButton, "Enter로도 더합니다. 식은 비어 있습니다(식 미정 — 항상 통과) — [구성] 탭에서 채웁니다.");

        void Add() => UiGuard.Run(_session, "조건 추가", () =>
        {
            string typed = (addBox.Text ?? string.Empty).Trim();

            if (typed.Length == 0)
            {
                _session.SetStatus("조건 이름을 적어 주세요.");
                return;
            }

            if (chapter.Conditions.Any(item => string.Equals(item.Label, typed, StringComparison.Ordinal)))
            {
                _session.SetStatus($"조건 '{typed}'이 이미 있습니다.");
                return;
            }

            _session.Editor.AddChapterCondition(chapterId, typed, string.Empty);   // 요약이 다시 선다(되돌리기 한 칸)
            _session.SetStatus($"조건 '{typed}'을 더했습니다 — 식 미정(항상 통과)입니다. 식은 [구성] 탭에서 채웁니다.");
            FocusSummaryBox("condition-add-name");
        });

        addButton.Click += (_, _) => Add();
        addBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Add();
            }
        };

        var addRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetColumn(addButton, 1);
        addRow.Children.Add(addBox);
        addRow.Children.Add(addButton);
        DirectorSummary.Children.Add(addRow);
    }

    /// <summary>조건 한 줄 — `[이름][✕]`과 그 아래 식(읽기만, 비면 「식 미정」). 이름을 비우는 것은 지우기가 아니다.</summary>
    private Control ConditionRow(string chapterId, ChapterCondition condition)
    {
        string label = condition.Label;
        var name = new TextBox { Text = label, FontSize = 12, Tag = "condition-label:" + label };

        void Commit() => UiGuard.Run(_session, "조건 이름", () =>
        {
            string typed = (name.Text ?? string.Empty).Trim();

            if (string.Equals(typed, label, StringComparison.Ordinal))
            {
                name.Text = label;   // 앞뒤 공백만 걷었다
                return;
            }

            ChapterConditionRenameOutcome outcome = _session.Editor.RenameChapterCondition(chapterId, label, typed);

            if (!outcome.Applied)
            {
                name.Text = label;
                _session.SetStatus(outcome.Refusal ?? "바꾸지 못했습니다.");
                return;
            }

            _session.SetStatus(outcome.Edges == 0
                ? $"조건 '{label}' → '{typed}'."
                : $"조건 '{label}' → '{typed}' — 간선 {outcome.Edges}개가 따라갔습니다.");
        });

        name.LostFocus += (_, _) => Commit();
        name.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Commit();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                name.Text = label;
            }
        }, RoutingStrategies.Tunnel);

        var remove = new Button
        {
            Content = "✕",
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = "condition-remove:" + label
        };
        ToolTip.SetTip(remove, $"'{label}'를 지웁니다 — 쓰는 곳이 있으면 어디인지 말하고 거절합니다.");
        remove.Click += (_, _) => UiGuard.Run(_session, "조건 삭제", () =>
        {
            ChapterConditionRemoveOutcome outcome = _session.Editor.RemoveChapterCondition(chapterId, label);
            _session.SetStatus(outcome.Applied
                ? $"조건 '{label}'을 지웠습니다. 되돌리기(Ctrl+Z)로 돌아옵니다."
                : ChapterGraphView.Explain(outcome.Refusal, outcome.Uses));
        });

        bool undecided = string.IsNullOrWhiteSpace(condition.Expression);
        var expression = new TextBlock
        {
            Text = undecided ? "⚠ 식 미정 — 항상 통과합니다" : "식 " + condition.Expression.Trim(),
            FontSize = 10,
            Opacity = 0.6,
            Margin = new Thickness(4, 1, 0, 0),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Tag = "condition-expression:" + label
        };

        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(remove, 1);
        line.Children.Add(name);
        line.Children.Add(remove);

        var row = new StackPanel { Spacing = 0 };
        row.Children.Add(line);
        row.Children.Add(expression);
        return row;
    }

    /// <summary>다시 선 요약에서 그 칸에 초점을 준다 — 넣기가 끝난 뒤에(요약은 넣기 안에서 다시 선다).</summary>
    private void FocusSummaryBox(string tag) => Dispatcher.UIThread.Post(() =>
        DirectorSummary.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(box => Equals(box.Tag, tag))?.Focus());
}
