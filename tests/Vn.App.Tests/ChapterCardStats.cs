using Avalonia.Controls;
using Avalonia.LogicalTree;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// [구성] 카드의 도착 스탯 줄들을 읽는 손 — 시험 여럿이 함께 쓴다. 카드는 스탯 하나에 한 줄(키 · 값 두 글)을 세운다(usability UX3).
/// </summary>
internal static class ChapterCardStats
{
    /// <summary>그 카드의 스탯 줄들 — 「키 값 · 키 값」으로 이어 붙인다. 없으면 빈 글자.</summary>
    public static string Of(Border card) =>
        string.Join(" · ", Rows(card).Select(row => $"{row.Key} {row.Value}"));

    /// <summary>그 카드의 스탯 줄들 — 줄마다 키 글과 값 글.</summary>
    public static List<(string Key, string Value, TextBlock KeyBlock, TextBlock ValueBlock)> Rows(Border card)
    {
        if (card.GetLogicalDescendants().OfType<StackPanel>().FirstOrDefault(panel => panel.Tag as string == ChapterGraphView.StatLineTag)
            is not { } lines)
        {
            return [];
        }

        return lines.Children.OfType<Grid>()
            .Select(row =>
            {
                List<TextBlock> texts = row.Children.OfType<TextBlock>().ToList();
                return (texts[0].Text ?? string.Empty, texts[1].Text ?? string.Empty, texts[0], texts[1]);
            })
            .ToList();
    }
}
