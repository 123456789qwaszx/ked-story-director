using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vn.Authoring.Graph;

namespace Vn.App.Views;

/// <summary>
/// <b>챕터 · 장면 이름표의 화면 겹</b>(design/board-zoom.md 4절 · 2026-11-26 소유자 — 「챕터와 장면 이름표가 축소되면서 겹치는데 … 챕터는 경계 줄 대신,
/// 경계 바깥 아웃라인 위쪽에 얹고, 장면의 경우 최소 축소로 축소하더라도, 장면 자체의 영역을 벗어나지는 않도록」).
///
/// 이름표는 판(<c>GraphCanvas</c>)이 아니라 판 위에 겹친 화면 겹(<c>FrameLabelLayer</c>)에 선다. 판의 여백은 판 단위로 고정인데 이름표는 화면에서 읽히는
/// 크기를 지켜야 해서, 판 위에서 키우면 줄일수록 이름표가 판을 1/배율만큼 차지해 서로 · 카드와 겹쳤다(⛔ 1차 구현 `135a508b`).
///
/// <b>장면 이름표는 옮기지도 숨기지도 않는다</b>(같은 날 소유자 — 「장면 이름표가 숨는 타이밍이 있다는 것 자체가 어색하고 … 원래라면 위쪽이름표를 토대로 무슨
/// 장면인지 구분하던 게, 갑자기 중앙에 장면 이름이 생기니 저게 도대체 뭔지 헷갈린다」). 이름표는 손잡이이고 작가가 자리로 기억하는 표지다 — 자리는 늘
/// 장면의 왼위이고, 자리가 모자라면 그 자리에서 글자를 줄이다가(<see cref="SceneLabelShrinkPx"/>까지) 이름이 빠진 작은 표(장면 색 점 + ⌄ · 이름은 풍선)가 된다.
/// 위로는 챕터 테두리 안까지만 자란다 — 테두리 밖의 챕터 이름표와 부딪힐 수 없다. 아주 멀리서(장면의 카드가 모두 덩어리)는 장면 한가운데에 장면 이름이
/// 옅게 깔린다 — 이름표와 다른 모양으로 카드 뒤에(판 위의 글), 구석의 이름표 · 작은 표는 그대로. ⛔ 같은 날 처음에는 자리가 없으면 장면 이름표가 한가운데로
/// 옮겨 가거나 물러났다(35%에서 첫 장면의 이름표가 사라졌다).
///
/// 크기는 「판에서의 크기 × 배율」, 단 화면 최소(챕터 <see cref="ChapterLabelMinPx"/> · 장면 <see cref="SceneLabelMinPx"/>)다 — 큰 배율에서는 판과 함께 커지고
/// 작아질 때만 바닥에서 멈춘다. 이름표는 판 단위로 지어 두고 그 배수로 그린다(100%에서는 판에 그리던 것과 같은 모양이다).
///
/// 이름표는 그대로 손잡이다(<see cref="OnFrameLabelPressed"/>) — 겹 밖의 판이 끌기를 받도록 포인터를 판에 잡아 둔다. 휠 · 집기 · 가운데 단추의 판 옮기기도
/// 이름표 위에서 그대로 된다(생성자에서 겹에 같은 처리를 단다).
/// </summary>
public partial class GraphEditorView
{
    /// <summary>챕터 이름표가 화면에서 이보다 작아지지 않는다(px).</summary>
    internal const double ChapterLabelMinPx = 13;

    /// <summary>장면 이름표가 화면에서 이보다 작아지지 않는다(px) — 자리가 넉넉하면.</summary>
    internal const double SceneLabelMinPx = 12;

    /// <summary>자리가 모자라면 장면 이름표의 글자가 여기까지 줄어든다(px). 그래도 모자라면 작은 표가 된다.</summary>
    internal const double SceneLabelShrinkPx = 10;

    /// <summary>장면 이름표를 이보다 좁게 말줄임해야 하면 작은 표가 된다(px) — 「장면 …」만 남은 이름표는 읽히지 않는다.</summary>
    internal const double LabelShortestPx = 36;

    /// <summary>장면 이름표의 아래 끝과 장면의 첫 카드 사이(판 단위).</summary>
    private const double SceneLabelGap = 4;

    /// <summary>멀리서 장면 한가운데에 깔리는 장면 이름의 화면 크기(px) — 장면이 좁으면 <see cref="SceneLabelMinPx"/>까지 줄고, 그보다 좁으면 쓰지 않는다.</summary>
    internal const double SceneWatermarkPx = 18;

    /// <summary>챕터 이름표의 Tag 머리 — 뒤에 챕터 ID가 붙는다.</summary>
    internal const string ChapterLabelTag = "chapter-label:";

    /// <summary>장면 이름표 대신 서는 작은 표의 Tag 머리 — 뒤에 장면ID가 붙는다.</summary>
    internal const string SceneChipTag = "scene-chip:";

    /// <summary>멀리서 장면 한가운데에 깔리는 장면 이름(판 위의 글)의 Tag 머리 — 뒤에 장면ID가 붙는다.</summary>
    internal const string SceneWatermarkTag = "scene-watermark:";

    /// <summary>이름표가 선 자리(<see cref="PlaceFrameLabels"/>).</summary>
    internal enum FrameLabelPlace
    {
        /// <summary>챕터 — 테두리 바깥 위(위 선에 아래 끝을 붙인 탭).</summary>
        Above,

        /// <summary>챕터 — 판의 맨 위라 바깥에 자리가 없어 테두리 안쪽 왼위.</summary>
        Inside,

        /// <summary>장면 — 왼위, 장면 위 여백의 아래 끝에 아래 끝을 붙여 위로 자란 이름표(100%에서는 위 테두리 선에 걸터앉는다 · 자리가 모자라면 글자가 준다).</summary>
        Corner,

        /// <summary>장면 — 같은 자리의 작은 표(장면 색 점 + ⌄ · 이름은 풍선).</summary>
        Chip
    }

    /// <summary>화면 겹의 이름표 하나 — 판의 테두리 자리와 그 테두리의 카드를 안다.</summary>
    private sealed class FrameLabel
    {
        public FrameLabel(Border visual, bool chapter, Rect area, double inset, IReadOnlyCollection<string> members)
        {
            Visual = visual;
            Chapter = chapter;
            Area = area;
            Inset = inset;
            Members = new HashSet<string>(members, StringComparer.Ordinal);
        }

        public Border Visual { get; }

        public bool Chapter { get; }

        /// <summary>그 테두리(판 단위).</summary>
        public Rect Area { get; }

        /// <summary>테두리 왼쪽 선에서 이름표까지(판 단위).</summary>
        public double Inset { get; }

        /// <summary>그 테두리의 카드 — 장면 이름표가 위로 자랄 때 비켜 갈 것에서 빠지고, 멀리서 장면 이름을 깔지 본다.</summary>
        public HashSet<string> Members { get; }

        /// <summary>장면 — 자리가 모자랄 때 같은 자리에 서는 작은 표(화면 겹 · 배수 없이 화면 크기 그대로).</summary>
        public Border? Chip { get; init; }

        /// <summary>장면 — 멀리서 장면 한가운데에 깔리는 장면 이름(판 위의 글).</summary>
        public TextBlock? Watermark { get; init; }

        /// <summary>장면의 이름(보이는 이름).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>장면 — 그 장면이 든 챕터의 테두리(판 단위). 이름표는 이 위로 나가지 않는다.</summary>
        public Rect Container { get; init; }

        /// <summary>판 단위의 제 크기(말줄임 전).</summary>
        public Size Natural { get; set; }

        /// <summary>제 크기를 창에 붙은 채 쟀나 — 떨어져 잰 크기는 테마의 글꼴이 안 든 것이라 붙으면 다시 잰다.</summary>
        public bool MeasuredAttached { get; set; }

        /// <summary>지금 그리는 배수.</summary>
        public double Scale { get; set; } = 1;

        public FrameLabelPlace Place { get; set; }

        /// <summary>선 자리(화면 겹의 좌표).</summary>
        public Rect Screen { get; set; }
    }

    /// <summary>챕터 · 장면 이름표 — 화면 겹(<c>FrameLabelLayer</c>)에 선다. 테두리를 그을 때마다 다시 짓는다(<see cref="DrawChapterFrames"/>).</summary>
    private readonly List<FrameLabel> _frameLabels = new();

    /// <summary>
    /// 장면 이름표가 비켜 갈 것 — 카드(그 아래 선택지 칸까지 · <see cref="FrameRectOf"/>)와 접은 상자의 판 자리. 카드면 그 카드의 Id를 든다. 테두리를 그을 때
    /// 모은다(<see cref="CollectLabelObstacles"/>).
    /// </summary>
    private readonly List<(Rect Area, string? NodeId)> _labelObstacles = new();

    /// <summary>이름표 하나를 화면 겹에 세운다 — 자리는 <see cref="PlaceFrameLabels"/>가 정한다.</summary>
    private void AddFrameLabel(FrameLabel label)
    {
        label.Visual.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        _frameLabels.Add(label);
        FrameLabelLayer.Children.Add(label.Visual);

        if (label.Chip is { } chip)
        {
            FrameLabelLayer.Children.Add(chip);
        }
    }

    /// <summary>화면 겹의 이름표를 모두 걷는다(멀리서 깔리는 장면 이름은 판의 테두리와 함께 걷힌다).</summary>
    private void ClearFrameLabels()
    {
        foreach (FrameLabel label in _frameLabels)
        {
            FrameLabelLayer.Children.Remove(label.Visual);

            if (label.Chip is { } chip)
            {
                FrameLabelLayer.Children.Remove(chip);
            }
        }

        _frameLabels.Clear();
    }

    /// <summary>장면 이름표가 비켜 갈 카드 · 접은 상자의 자리를 모은다.</summary>
    private void CollectLabelObstacles()
    {
        _labelObstacles.Clear();

        if (_projection is null)
        {
            return;
        }

        foreach (ExpandedNodeProjection node in _projection.Items.OfType<ExpandedNodeProjection>())
        {
            if (FindCard(node.NodeId) is { } card)
            {
                _labelObstacles.Add((FrameRectOf(node, card), node.NodeId));
            }
        }

        foreach (FileProxyVisual proxy in _proxies)
        {
            _labelObstacles.Add((ProxyRect(proxy), null));
        }
    }

    /// <summary>
    /// 그 카드가 지금 글을 보이나(글 단계 · <see cref="CardStage"/>) — 카드의 그림자가 이것을 따른다. 판에 없는 카드는 글을 보이는 것으로 친다.
    /// </summary>
    private bool CardShowsText(string nodeId) => FindCard(nodeId) is not { } card || card.Stage == CardStage.Text;

    /// <summary>
    /// 이름표의 자리와 크기를 지금 배율 · 스크롤에 맞춘다(design/board-zoom.md 4.2절). 챕터는 테두리 바깥 위(판 맨 위면 안쪽). 장면은 늘 왼위 —
    /// 위로 자랄 자리(챕터 테두리 안 · 위쪽의 다른 장면의 카드 · 선택지 칸 아래까지)에 맞춰 글자를 줄이고, 그래도 모자라면 같은 자리의 작은 표다. 멀리서는 장면
    /// 한가운데에 장면 이름을 깐다. 배율 · 스크롤 · 테두리가 바뀔 때마다 부른다.
    /// </summary>
    internal void PlaceFrameLabels()
    {
        if (_frameLabels.Count == 0)
        {
            return;
        }

        double zoom = _zoom;
        Vector offset = GraphScroll.Offset;
        double boardTop = -offset.Y;   // 판의 맨 위(화면) — 그 위로는 스크롤할 수 없다
        bool attached = TopLevel.GetTopLevel(this) is not null;

        Rect Screen(Rect board) => new(
            (board.X * zoom) - offset.X,
            (board.Y * zoom) - offset.Y,
            board.Width * zoom,
            board.Height * zoom);

        List<(Rect Screen, string? NodeId)> obstacles = _labelObstacles
            .Select(item => (Screen(item.Area), item.NodeId))
            .ToList();
        var placed = new List<Rect>();

        foreach (FrameLabel label in _frameLabels.Where(label => label.Chapter))
        {
            MeasureLabel(label, attached);
            double scale = ScaleOf(label, zoom);
            Rect frame = Screen(label.Area);
            double inset = Math.Max(4, label.Inset * zoom);
            double width = Math.Min(label.Natural.Width * scale, Math.Max(frame.Width - inset - 4, LabelShortestPx));
            double height = label.Natural.Height * scale;

            // 테두리 바깥 위 — 위 선에 아래 끝을 붙인다. 판의 맨 위라 바깥에 자리가 없으면 테두리 안쪽.
            var above = new Rect(frame.X + inset, frame.Y - height, width, height);
            bool outside = above.Y >= boardTop;
            Rect rect = outside
                ? above
                : new Rect(above.X, Math.Max(boardTop, frame.Y + Math.Max(4, 8 * zoom)), width, height);

            ShowLabel(label, rect, scale, outside ? FrameLabelPlace.Above : FrameLabelPlace.Inside);
            placed.Add(rect);
        }

        foreach (FrameLabel label in _frameLabels.Where(label => !label.Chapter).OrderBy(label => label.Area.Y).ThenBy(label => label.Area.X))
        {
            MeasureLabel(label, attached);
            PlaceWatermark(label, zoom);

            double full = ScaleOf(label, zoom);
            double font = FontOf(label);
            Rect frame = Screen(label.Area);
            double inset = Math.Max(4, label.Inset * zoom);
            double left = frame.X + inset;
            double room = frame.Width - inset - 4;

            // 아래 끝 — 장면 위 여백의 아래 끝(첫 카드 바로 위). 위 끝의 천장 — 챕터 테두리 안 · 판의 맨 위 · 위쪽에 놓인 남의 카드(선택지 칸까지) · 접은 상자.
            double bottom = ((label.Area.Y + SceneFramePad.Top - SceneLabelGap) * zoom) - offset.Y;
            double ceiling = Math.Max(boardTop, Screen(label.Container).Y + 2);

            foreach ((Rect area, string? nodeId) in obstacles)
            {
                bool mine = nodeId is not null && label.Members.Contains(nodeId);

                if (!mine && area.Bottom <= bottom && area.Bottom > ceiling && area.Right > left && area.X < left + Math.Max(room, 0))
                {
                    ceiling = area.Bottom + 2;
                }
            }

            // 자리에 맞춰 글자를 줄인다 — 화면 SceneLabelShrinkPx까지. 장면의 폭에서는 말줄임한다.
            double scale = Math.Min(full, (bottom - ceiling) / Math.Max(label.Natural.Height, 1));
            double width = Math.Min(label.Natural.Width * scale, room);
            bool fits = font * scale >= SceneLabelShrinkPx - 0.01 && room >= Math.Min(LabelShortestPx, label.Natural.Width * scale);

            if (fits)
            {
                double height = label.Natural.Height * scale;
                var corner = new Rect(left, bottom - height, width, height);

                if (!placed.Any(other => other.Intersects(corner)))
                {
                    ShowLabel(label, corner, scale, FrameLabelPlace.Corner);
                    placed.Add(corner);
                    continue;
                }
            }

            // 같은 자리의 작은 표 — 화면 크기 그대로. 천장 아래로만(넘치면 제 장면 안쪽으로 내려온다 — 멀리서는 카드가 글을 보이지 않는다).
            Size chip = ChipSize(label);
            var spot = new Rect(left, Math.Max(ceiling, bottom - chip.Height), chip.Width, chip.Height);
            ShowLabel(label, spot, 1, FrameLabelPlace.Chip);
            placed.Add(spot);
        }
    }

    /// <summary>
    /// 멀리서 장면 한가운데에 장면 이름을 깐다 — 장면의 카드가 모두 덩어리일 때만, 화면 <see cref="SceneWatermarkPx"/>(장면이 좁으면 <see cref="SceneLabelMinPx"/>까지
    /// 줄고, 그래도 넘치면 쓰지 않는다). 판 위의 글이라 카드 뒤에 선다.
    /// </summary>
    private void PlaceWatermark(FrameLabel label, double zoom)
    {
        if (label.Watermark is not { } mark)
        {
            return;
        }

        bool far = label.Members.Count > 0 && label.Members.All(nodeId => CardStageOf(nodeId) == CardStage.Block);

        if (!far)
        {
            mark.IsVisible = false;
            mark.Text = string.Empty;   // 글 그대로의 판에는 글자가 남지 않는다
            return;
        }

        double scale = Math.Max(zoom, 0.01);
        mark.Text = label.Name;
        mark.FontSize = SceneWatermarkPx / scale;
        mark.Measure(Size.Infinity);
        double room = label.Area.Width - (16 / scale);

        if (mark.DesiredSize.Width > room)
        {
            mark.FontSize = Math.Max(SceneLabelMinPx / scale, mark.FontSize * room / Math.Max(mark.DesiredSize.Width, 1));
            mark.Measure(Size.Infinity);
        }

        mark.IsVisible = mark.DesiredSize.Width <= room && mark.DesiredSize.Height <= label.Area.Height;
        Canvas.SetLeft(mark, label.Area.Center.X - (mark.DesiredSize.Width / 2));
        Canvas.SetTop(mark, label.Area.Center.Y - (mark.DesiredSize.Height / 2));
    }

    /// <summary>작은 표의 화면 크기.</summary>
    private static Size ChipSize(FrameLabel label)
    {
        if (label.Chip is not { } chip)
        {
            return new Size(24, 16);
        }

        bool shown = chip.IsVisible;
        chip.IsVisible = true;   // 감춘 컨트롤은 크기가 0으로 재인다
        chip.Measure(Size.Infinity);
        Size size = chip.DesiredSize;
        chip.IsVisible = shown;
        return size;
    }

    private static double FontOf(FrameLabel label) =>
        label.Visual.Child is TextBlock name && name.FontSize > 0 ? name.FontSize : 12;

    /// <summary>이름표의 제 크기(판 단위)를 잰다 — 처음 한 번, 창에서 떨어져 쟀으면 붙은 뒤에 다시.</summary>
    private static void MeasureLabel(FrameLabel label, bool attached)
    {
        if (label.Natural.Width > 0 && (label.MeasuredAttached || !attached))
        {
            return;
        }

        Border visual = label.Visual;
        visual.IsVisible = true;   // 감춘 컨트롤은 크기가 0으로 재인다
        visual.MaxWidth = double.PositiveInfinity;
        visual.Measure(Size.Infinity);
        label.Natural = visual.DesiredSize;
        label.MeasuredAttached = attached;
    }

    /// <summary>그리는 배수 — 판의 배율, 단 화면 글자가 바닥(챕터 13 · 장면 12px)보다 작아지지 않는다.</summary>
    private static double ScaleOf(FrameLabel label, double zoom) =>
        Math.Max(zoom, (label.Chapter ? ChapterLabelMinPx : SceneLabelMinPx) / FontOf(label));

    /// <summary>
    /// 이름표를 그 자리에 그 배수로 — 말줄임 폭은 판 단위로 돌려 건다. 작은 표의 자리면 이름표를 감추고 작은 표를 세운다(작은 표는 배수 없이 화면 크기
    /// 그대로다). 자리는 화면의 온 픽셀에 맞춘다(글자가 번지지 않게).
    /// </summary>
    private static void ShowLabel(FrameLabel label, Rect rect, double scale, FrameLabelPlace place)
    {
        Border visual = label.Visual;
        label.Place = place;
        label.Screen = rect;

        if (place == FrameLabelPlace.Chip && label.Chip is { } chip)
        {
            visual.IsVisible = false;
            Canvas.SetLeft(chip, Math.Round(rect.X));
            Canvas.SetTop(chip, Math.Round(rect.Y));
            chip.IsVisible = true;
            return;
        }

        if (label.Chip is { } unused)
        {
            unused.IsVisible = false;
        }

        double maxWidth = rect.Width < (label.Natural.Width * scale) - 0.5 ? rect.Width / scale : double.PositiveInfinity;

        if (!visual.MaxWidth.Equals(maxWidth))
        {
            visual.MaxWidth = maxWidth;
        }

        if (Math.Abs(label.Scale - scale) > 0.0001 || (visual.RenderTransform is null) != (Math.Abs(scale - 1) < 0.0001))
        {
            visual.RenderTransform = Math.Abs(scale - 1) < 0.0001 ? null : new ScaleTransform(scale, scale);
            label.Scale = scale;
        }

        Canvas.SetLeft(visual, Math.Round(rect.X));
        Canvas.SetTop(visual, Math.Round(rect.Y));
        visual.IsVisible = true;
    }

    /// <summary>이름표가 선 자리 — 시험이 본다(Tag · 화면 겹의 자리 · 자리 종류).</summary>
    internal IReadOnlyList<(string Key, Rect Screen, FrameLabelPlace Place)> FrameLabelPlaces =>
        _frameLabels.Select(label => (label.Visual.Tag as string ?? string.Empty, label.Screen, label.Place)).ToList();

    /// <summary>그 장면 테두리의 판 자리 — 시험이 테두리 안의 빈 곳을 찾는다.</summary>
    internal Rect? SceneAreaOf(string sceneId) =>
        _sceneAreas.Where(item => string.Equals(item.SceneId, sceneId, StringComparison.Ordinal)).Select(item => (Rect?)item.Area).FirstOrDefault();

    /// <summary>그 챕터 테두리의 판 자리 — 시험이 챕터 안 · 장면 밖의 빈 곳을 찾는다.</summary>
    internal Rect? ChapterAreaOf(string chapterId) =>
        _chapterAreas.Where(item => string.Equals(item.ChapterId, chapterId, StringComparison.Ordinal)).Select(item => (Rect?)item.Area).FirstOrDefault();
}
