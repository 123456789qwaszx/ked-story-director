using Avalonia;
using Avalonia.Controls;

namespace Vn.App.Views;

/// <summary>
/// <b>카드 머리 줄</b> — [대본] 판 카드의 제목 줄(칸: 0 표 · 1 제목 · 2 분기 조건 표 · 3 글 맞춤 단추). 그냥 Grid와 다른 것은 하나다: 제목과
/// 분기 조건 표가 함께 넘치면 <b>제목이 적어도 남은 폭의 절반을 갖고 표가 줄어든다</b>(표는 줄임표로 잘리고, 다 읽는 길은 표의 풍선이다).
///
/// ⛔ 그 전(2026-11-25 영어 화면 E7까지)은 표가 Auto 칸이라 제 폭을 먼저 가져갔다 — 기본 폭의 분기 카드에서 조건 이름이 길면 제목이 0이 되어
/// 사라졌다. 영어(「⑂ Condition: …」)에서는 조건이 없어도 사라졌다.
/// </summary>
internal sealed class CardTitleRow : Grid
{
    internal const int IconColumn = 0;
    internal const int TitleColumn = 1;
    internal const int ChipColumn = 2;
    internal const int TogglesColumn = 3;

    public CardTitleRow()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto");
    }

    protected override Type StyleKeyOverride => typeof(Grid);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.FirstOrDefault(child => GetColumn(child) == ChipColumn) is { } chip &&
            !double.IsInfinity(availableSize.Width))
        {
            FitChip(chip, availableSize.Width);
        }

        return base.MeasureOverride(availableSize);
    }

    /// <summary>표의 최대 폭을 정한다 — 둘이 들어가면 끝이 없고, 넘치면 제목이 제 폭(많아야 절반 이상)을 먼저 갖는다.</summary>
    private void FitChip(Control chip, double width)
    {
        double others = 0;

        foreach (Control child in Children)
        {
            int column = GetColumn(child);

            if (child.IsVisible && column is IconColumn or TogglesColumn)
            {
                child.Measure(Size.Infinity);
                others += child.DesiredSize.Width;
            }
        }

        // 제목 칸의 글 — 제목을 고치는 동안은 제목 글이 숨고 칸(TextBox)이 선다. 칸도 제 글만큼만 원한다(글만큼 서는 칸 — 2026-11-25).
        // ⛔ 그 전에는 칸이 남은 폭을 다 원한다고 보아, 고치는 동안 분기 조건 표가 늘 반으로 줄었다.
        double room = Math.Max(0, width - others);
        double titleWants = Children
            .Where(child => GetColumn(child) == TitleColumn && child.IsVisible)
            .Select(Natural)
            .DefaultIfEmpty(0)
            .Max();

        chip.MaxWidth = double.PositiveInfinity;
        double chipWants = Natural(chip);
        double limit = double.PositiveInfinity;

        if (titleWants + chipWants > room)
        {
            double titleGets = Math.Min(titleWants, Math.Max(room - chipWants, room / 2));
            limit = Math.Max(0, room - titleGets - chip.Margin.Left - chip.Margin.Right);
        }

        chip.MaxWidth = limit;
    }

    private static double Natural(Control child)
    {
        child.Measure(Size.Infinity);
        return child.DesiredSize.Width;
    }
}
