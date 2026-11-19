using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Vn.App.Views;

/// <summary>
/// <b>범위 선택(러버밴드)</b> — [구성] · [대본] 두 판이 같은 사각형과 같은 규칙을 쓴다. 빈 판을 왼쪽 단추로 끌면 사각형이
/// 서고, 놓으면 카드가 걸렸을 때 카드만 무리가 되어 끌기 시작한 자리에서 가장 가까운 카드를 고른다. 카드 없이 선만
/// 걸렸으면 놓은 자리(포인터)에서 가장 가까운 선 하나를 고른다. 무엇을 고르는지는 판마다 다르고, 여기는 그리기와 재기다.
/// </summary>
internal static class RubberBand
{
    /// <summary>선 위를 짚어 보는 간격(px) — 범위 사각형이 선을 건너뛰지 않을 만큼 촘촘하다.</summary>
    private const double LineSampleStep = 4;

    /// <summary>끄는 동안 보이는 점선 사각형 — 누름을 받지 않는다.</summary>
    internal static Rectangle Create(Point start)
    {
        var band = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(28, 37, 99, 235)),
            IsHitTestVisible = false
        };
        Avalonia.Controls.Canvas.SetLeft(band, start.X);
        Avalonia.Controls.Canvas.SetTop(band, start.Y);
        return band;
    }

    /// <summary>시작한 자리와 지금 자리로 사각형을 다시 맞춘다.</summary>
    internal static Rect Stretch(Rectangle band, Point start, Point current)
    {
        Rect rect = Between(start, current);
        Avalonia.Controls.Canvas.SetLeft(band, rect.X);
        Avalonia.Controls.Canvas.SetTop(band, rect.Y);
        band.Width = rect.Width;
        band.Height = rect.Height;
        return rect;
    }

    /// <summary>두 점을 맞모서리로 하는 사각형.</summary>
    internal static Rect Between(Point start, Point current) => new(
        new Point(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y)),
        new Point(Math.Max(start.X, current.X), Math.Max(start.Y, current.Y)));

    /// <summary>
    /// 선이 사각형에 걸쳤으면 걸친 부분에서 <paramref name="from"/>까지의 거리, 안 걸쳤으면 null — 선 위를
    /// <see cref="LineSampleStep"/>마다 짚어 잰다.
    /// </summary>
    internal static double? CaughtDistance(Geometry shape, Rect rect, Point from)
    {
        // 곧은 세로 · 가로 선은 테두리 상자의 폭이나 높이가 0이다 — 1px 넓혀 견준다.
        if (!shape.Bounds.Inflate(1).Intersects(rect))
        {
            return null;
        }

        double length = shape.ContourLength;
        double? nearest = null;

        for (double at = 0; ; at += LineSampleStep)
        {
            if (shape.TryGetPointAtDistance(Math.Min(at, length), out Point point) && rect.Contains(point))
            {
                double distance = Point.Distance(point, from);
                nearest = nearest is { } found ? Math.Min(found, distance) : distance;
            }

            if (at >= length)
            {
                return nearest;
            }
        }
    }

    /// <summary>점에서 사각형까지의 거리 — 안에 있으면 0.</summary>
    internal static double DistanceTo(Rect rect, Point point)
    {
        double dx = Math.Max(Math.Max(rect.Left - point.X, 0), point.X - rect.Right);
        double dy = Math.Max(Math.Max(rect.Top - point.Y, 0), point.Y - rect.Bottom);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
