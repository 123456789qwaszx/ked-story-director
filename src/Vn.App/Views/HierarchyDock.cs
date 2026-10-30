using Avalonia.Controls;

namespace Vn.App.Views;

/// <summary>
/// <b>하이어아키 기둥 접기</b> (2026-10-30 소유자: *"하이어아키를 접을 수 있도록 해줘"* — <c>docs/plans/hierarchy.md</c> HC).
///
/// 판 왼쪽 기둥을 좁은 세로 띠로 접고 편다. 접으면 기둥은 <see cref="FoldedWidth"/>만 남아 띠(「›」 · 하이어아키)가 서고,
/// 띠를 누르면 사람이 끌어 두었던 폭으로 돌아간다. 접힌 동안은 폭 손잡이(스플리터)도 걷는다 — 끌어 봐야 띠만 넓어진다.
///
/// ⚠ 두 그래프가 한 벌을 쓰고, 어느 쪽에서 접든 셸이 다른 쪽을 맞추고 사용자 설정에 기억한다(<see cref="Changed"/>).
/// </summary>
internal sealed class HierarchyDock
{
    /// <summary>접힌 기둥의 폭 — 띠 하나.</summary>
    internal const double FoldedWidth = 26;

    private readonly ColumnDefinition _column;
    private readonly ColumnDefinition _splitterColumn;
    private readonly GridSplitter _splitter;
    private readonly Control _open;
    private readonly Control _strip;
    private readonly double _openMinWidth;
    private readonly GridLength _splitterWidth;
    private GridLength _openWidth;

    internal HierarchyDock(
        ColumnDefinition column, ColumnDefinition splitterColumn, GridSplitter splitter, Control open, Control strip)
    {
        _column = column;
        _splitterColumn = splitterColumn;
        _splitter = splitter;
        _open = open;
        _strip = strip;
        _openMinWidth = column.MinWidth;
        _splitterWidth = splitterColumn.Width;
        _openWidth = column.Width;
    }

    /// <summary>지금 접혀 있는가.</summary>
    internal bool IsFolded { get; private set; }

    /// <summary>사람이 접거나 폈다(띠 · 단추) — 셸이 다른 탭을 맞추고 기억한다. <see cref="Fold"/>로 밖에서 바꾼 것은 알리지 않는다.</summary>
    internal event Action<bool>? Changed;

    /// <summary>사람의 손짓 — 바꾸고 알린다.</summary>
    internal void Toggle(bool folded)
    {
        if (folded == IsFolded)
        {
            return;
        }

        Fold(folded);
        Changed?.Invoke(folded);
    }

    /// <summary>밖에서 맞춘다 — 알리지 않는다.</summary>
    internal void Fold(bool folded)
    {
        if (folded == IsFolded)
        {
            return;
        }

        IsFolded = folded;

        if (folded)
        {
            _openWidth = _column.Width;   // 사람이 끌어 둔 폭 — 펼 때 돌아간다
            _column.MinWidth = FoldedWidth;
            _column.Width = new GridLength(FoldedWidth);
            _splitterColumn.Width = new GridLength(0);
        }
        else
        {
            _column.MinWidth = _openMinWidth;
            _column.Width = _openWidth;
            _splitterColumn.Width = _splitterWidth;
        }

        _splitter.IsVisible = !folded;
        _open.IsVisible = !folded;
        _strip.IsVisible = folded;
    }
}
