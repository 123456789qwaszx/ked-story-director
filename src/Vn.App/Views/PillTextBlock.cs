using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vn.App.Views;

/// <summary>
/// <b>알약 바탕을 스스로 그리는 글</b>(2026-11-26 · themes) — 선택지 문구를 둥근 칸에 담는 테마(<c>Skin.SlotPill</c>)가 쓴다. 글을 테두리 상자로 감싸지
/// 않고 글 자신이 바탕을 그린다 — 판의 짜임(선택지 줄 = 점 + 글 하나)이 테마와 상관없이 같아서, 누르기 · 끌기 · 줄을 찾는 코드와 시험이 한 벌로 선다.
/// 안 여백은 <see cref="TextBlock.Padding"/>으로 준다.
/// </summary>
internal sealed class PillTextBlock : TextBlock
{
    /// <summary>테마의 글 스타일(TextBlock)을 그대로 받는다.</summary>
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>알약의 바탕.</summary>
    public IBrush? Fill { get; init; }

    /// <summary>알약의 테두리(1px).</summary>
    public IBrush? Outline { get; init; }

    /// <summary>알약의 모서리.</summary>
    public double Radius { get; init; }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (Fill is not null || Outline is not null)
        {
            Rect rect = new Rect(Bounds.Size).Deflate(0.5);
            context.DrawRectangle(Fill, Outline is null ? null : new Pen(Outline, 1), rect, Radius, Radius);
        }

        base.RenderTextLayout(context, origin);
    }
}
