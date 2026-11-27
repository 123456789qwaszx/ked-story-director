using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vn.App.Views;

/// <summary>
/// <b>[대본] 판의 깔개 — 한 가지 색 면에 일정한 간격의 점</b>(2026-11-26 소유자 — 「챕터 바깥 배경 쪽에는 약간 점을 일정간격으로 반투명하게 박아두고,
/// 챕터쪽에서는 그 배경을 사라지게」 · themes). 판(<c>GraphScroll</c>) 뒤에 같은 칸으로 깔리고, 챕터의 테두리가 불투명한 면으로 그 위를 덮는다 — 그래서 점이
/// 보이는 곳이 곧 챕터 바깥이다.
///
/// 점은 <b>판과 함께 움직인다</b>(<see cref="Track"/> — 판을 끌거나 배율을 바꾸면 따라온다). 배율을 줄이면 점 사이가 좁아지는데, 화면에서
/// <see cref="MinStep"/>보다 촘촘해지면 한 칸씩 건너뛴다(간격을 두 배로) — 촘촘히 되풀이되는 무늬는 오래 보면 눈이 피로하다(docs/themes.md 7절).
/// 점의 크기는 화면 크기 그대로다(배율을 따르지 않는다).
///
/// 색 · 간격 · 크기는 테마가 정한다(<c>BoardBackground</c> · <c>BoardDot</c> · <c>BoardDotSpacing</c> · <c>BoardDotSize</c>). 기본 테마에는 이 이름들이 없어
/// 아무것도 그리지 않는다 — 0.5.0까지의 모양 그대로다. 누름을 받지 않는다(XAML에서 <c>IsHitTestVisible="False"</c>).
/// </summary>
internal sealed class DotGridBackdrop : Control
{
    /// <summary>화면에서 점 사이의 가장 좁은 거리 — 이보다 좁아지면 한 칸씩 건너뛴다.</summary>
    private const double MinStep = 18;

    /// <summary>바탕의 색.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<DotGridBackdrop, IBrush?>(nameof(Fill));

    /// <summary>점의 색.</summary>
    public static readonly StyledProperty<IBrush?> DotProperty =
        AvaloniaProperty.Register<DotGridBackdrop, IBrush?>(nameof(Dot));

    /// <summary>배율 100%에서 점 사이의 거리 — 0이면 점을 그리지 않는다.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<DotGridBackdrop, double>(nameof(Spacing));

    /// <summary>점의 지름(화면 크기).</summary>
    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<DotGridBackdrop, double>(nameof(DotSize));

    private Vector _offset;
    private double _zoom = 1;

    static DotGridBackdrop()
    {
        AffectsRender<DotGridBackdrop>(FillProperty, DotProperty, SpacingProperty, DotSizeProperty);
        ClipToBoundsProperty.OverrideDefaultValue<DotGridBackdrop>(true);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Dot
    {
        get => GetValue(DotProperty);
        set => SetValue(DotProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double DotSize
    {
        get => GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    /// <summary>화면에서 지금 점 사이의 거리 — 시험의 손잡이. 점을 그리지 않으면 0이다.</summary>
    internal double Step => StepFor(Spacing, _zoom);

    /// <summary>판의 스크롤 자리와 배율을 따라간다 — 판의 (0, 0)에 점 하나가 놓인 것처럼 그린다.</summary>
    public void Track(Vector offset, double zoom)
    {
        if (offset == _offset && zoom.Equals(_zoom))
        {
            return;
        }

        (_offset, _zoom) = (offset, zoom);
        InvalidateVisual();
    }

    /// <summary>화면에서 점 사이의 거리 — 판의 간격에 배율을 곱하고, <see cref="MinStep"/>보다 좁으면 넓어질 때까지 두 배씩 건너뛴다.</summary>
    internal static double StepFor(double spacing, double zoom)
    {
        double step = spacing * zoom;

        if (!(step > 0) || double.IsInfinity(step))
        {
            return 0;
        }

        while (step < MinStep)
        {
            step *= 2;
        }

        return step;
    }

    public override void Render(DrawingContext context)
    {
        var area = new Rect(Bounds.Size);

        if (Fill is { } fill)
        {
            context.DrawRectangle(fill, null, area);
        }

        double step = Step;
        double size = DotSize;

        if (Dot is not { } dot || step <= 0 || !(size > 0))
        {
            return;
        }

        // 판의 (0, 0)은 이 깔개의 (-offset)에 있다 — 거기서부터 step마다 점이다. 화면 왼위 바깥의 첫 점부터 그린다.
        double radius = size / 2;
        double left = -Wrap(_offset.X, step);
        double top = -Wrap(_offset.Y, step);

        for (double y = top; y < area.Height + radius; y += step)
        {
            for (double x = left; x < area.Width + radius; x += step)
            {
                context.DrawRectangle(dot, null, new Rect(x - radius, y - radius, size, size), radius, radius);
            }
        }
    }

    /// <summary>0 이상 step 미만으로 접는다(음수도).</summary>
    private static double Wrap(double value, double step)
    {
        double rest = value % step;
        return rest < 0 ? rest + step : rest;
    }
}
