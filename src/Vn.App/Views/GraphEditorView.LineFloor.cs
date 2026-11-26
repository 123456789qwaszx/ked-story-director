using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Vn.App.Theming;

namespace Vn.App.Views;

/// <summary>
/// <b>판의 선 · 테두리는 화면에서 사라지지 않는다</b>(design/board-zoom.md 5절 · 2026-11-26). 판은 배율로 그려지므로 판 단위로 1.4인 선은 10%에서 화면
/// 0.14px — 길이 어디로 가는지, 무엇을 골랐는지가 판을 줄이면 사라졌다. 그래서 선마다 <b>화면 바닥</b>을 둔다: 그리는 굵기 = max(판의 굵기, 바닥 ÷ 배율).
/// 큰 배율에서는 테마의 굵기 그대로이고, 줄여서 바닥보다 가늘어질 때만 판 위에서 굵어진다.
///
/// 선은 지을 때 바닥을 적어 두고(<see cref="Floored{T}"/>), 판에 붙는 순간과 배율이 바뀔 때 굵기를 맞춘다(<see cref="FloorLines"/>). 고르면 굵어지는 선은
/// <see cref="SetLineWidth"/>로 판의 굵기를 바꾼다.
///
/// 카드 테두리는 굵기(<c>BorderThickness</c>)를 바꾸면 카드의 안쪽 치수가 바뀌어 글이 다시 흐른다 — 그래서 테두리는 그대로 두고 <b>바깥 고리</b>(번짐 없는
/// 그림자)로 모자란 만큼을 더한다(<see cref="PaintCardEdge"/>). 카드의 그림자는 글을 보이는 카드에만 둔다 — 작은 카드에 번지는 그림자는 탁하고, 카드가 많을 때
/// 그리기 값이 크다.
/// </summary>
public partial class GraphEditorView
{
    /// <summary>판의 선이 화면에서 이보다 가늘어지지 않는다(px).</summary>
    internal const double LineFloorPx = 1;

    /// <summary>고른 선 · 고른 카드 테두리 · 끄는 선이 화면에서 이보다 가늘어지지 않는다(px).</summary>
    internal const double PickedLineFloorPx = 2;

    /// <summary>선을 누르는 투명한 띠가 화면에서 이보다 좁아지지 않는다(px) — 줄인 판에서도 선을 겨눌 수 있다.</summary>
    internal const double HitLineFloorPx = 8;

    /// <summary>그 선이 판 위에서 받은 굵기(판 단위). 바닥을 건 선만 적힌다.</summary>
    private static readonly AttachedProperty<double> BaseStrokeProperty =
        AvaloniaProperty.RegisterAttached<GraphEditorView, Shape, double>("BaseStroke", double.NaN);

    /// <summary>그 선의 화면 바닥(px). NaN이면 바닥이 없다(판의 선이 아니다 — 점 · 손잡이는 카드 단계가 다룬다).</summary>
    private static readonly AttachedProperty<double> StrokeFloorProperty =
        AvaloniaProperty.RegisterAttached<GraphEditorView, Shape, double>("StrokeFloor", double.NaN);

    /// <summary>선에 화면 바닥을 적는다 — 지금 굵기가 판의 굵기다. 판에 붙으면 굵기가 맞춰진다(<see cref="OnCanvasChildrenChanged"/>).</summary>
    private static T Floored<T>(T shape, double floorPx = LineFloorPx) where T : Shape
    {
        shape.SetValue(BaseStrokeProperty, shape.StrokeThickness);
        shape.SetValue(StrokeFloorProperty, floorPx);
        return shape;
    }

    /// <summary>선의 판 굵기를 바꾼다(고르면 굵어지는 선) — 바닥을 그대로 건다.</summary>
    private void SetLineWidth(Shape shape, double width, double floorPx = LineFloorPx)
    {
        shape.SetValue(BaseStrokeProperty, width);
        shape.SetValue(StrokeFloorProperty, floorPx);
        ApplyFloor(shape);
    }

    /// <summary>바닥을 적은 선의 굵기를 지금 배율에 맞춘다.</summary>
    private void ApplyFloor(Shape shape)
    {
        double floor = shape.GetValue(StrokeFloorProperty);

        if (double.IsNaN(floor))
        {
            return;
        }

        double width = Math.Max(shape.GetValue(BaseStrokeProperty), floor / Math.Max(_zoom, 0.01));

        if (!shape.StrokeThickness.Equals(width))
        {
            shape.StrokeThickness = width;
        }
    }

    /// <summary>판의 선 모두를 지금 배율에 맞춘다 — 배율이 바뀔 때.</summary>
    private void FloorLines()
    {
        foreach (Shape shape in GraphCanvas.Children.OfType<Shape>())
        {
            ApplyFloor(shape);
        }
    }

    /// <summary>판에 붙는 선은 그 자리에서 바닥을 건다 — 철도 · 간선을 어디서 다시 긋든 따로 맞출 것이 없다.</summary>
    private void OnCanvasChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
    {
        if (args.NewItems is null)
        {
            return;
        }

        foreach (object? item in args.NewItems)
        {
            if (item is Shape shape)
            {
                ApplyFloor(shape);
            }
        }
    }

    /// <summary>카드를 지금 고른 것으로 칠하나 — 고른 카드이거나 범위로 고른 무리다.</summary>
    private bool CardIsPicked(string nodeId) =>
        string.Equals(nodeId, _session?.SelectedNodeId, StringComparison.Ordinal) || _multiSelected.Contains(nodeId);

    /// <summary>
    /// 카드의 테두리 · 그림자. 테두리의 굵기는 판 단위 그대로(1 · 고르면 2 — 카드의 안쪽 치수가 같다)이고, 화면 바닥(1 · 고르면 2px)에 모자라면 같은 색의
    /// 바깥 고리를 더한다. 그림자(고르면 파란 번짐까지)는 글을 보이는 카드에만 둔다.
    /// </summary>
    private void PaintCardEdge(NodeCard card, bool picked)
    {
        Color edge = picked ? Skin.Selection : card.Style.Border;
        double width = picked ? 2 : 1;
        double need = (picked ? PickedLineFloorPx : LineFloorPx) / Math.Max(_zoom, 0.01);

        card.Visual.BorderBrush = edge.Brush();
        card.Visual.BorderThickness = new Thickness(width);

        var shadows = new List<BoxShadow>();

        if (need > width + 0.01)
        {
            shadows.Add(new BoxShadow { Spread = need - width, Color = edge });   // 번짐 없는 고리 — 테두리 바깥으로 모자란 만큼
        }

        if (CardShowsText(card.NodeId))
        {
            BoxShadows themed = picked ? Skin.CardSelectedShadow : Skin.CardShadow;

            for (int index = 0; index < themed.Count; index++)
            {
                shadows.Add(themed[index]);
            }
        }

        card.Visual.BoxShadow = shadows.Count == 0
            ? default
            : new BoxShadows(shadows[0], shadows.Skip(1).ToArray());
    }
}
