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
/// 크기를 지켜야 해서, 판 위에서 키우면 줄일수록 이름표가 판을 1/배율만큼 차지해 서로 · 카드와 겹쳤다(⛔ 1차 구현 `135a508b`). 화면 겹에서는 자리와 크기를
/// 화면 기준으로 셈하고, <b>자리 후보를 차례로 대 보아 겹치지 않는 첫 자리에 선다</b> — 지도의 지명처럼 위계(챕터 > 장면)가 있고, 자리가 없으면 장면 이름표가
/// 물러난다.
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

    /// <summary>장면 이름표가 화면에서 이보다 작아지지 않는다(px).</summary>
    internal const double SceneLabelMinPx = 12;

    /// <summary>장면 이름표를 이보다 좁게 말줄임해야 하면 서지 않는다(px) — 「장면 …」만 남은 이름표는 읽히지 않는다.</summary>
    internal const double LabelShortestPx = 36;

    /// <summary>장면 이름표의 아래 끝과 장면의 첫 카드 사이(판 단위).</summary>
    private const double SceneLabelGap = 4;

    /// <summary>챕터 이름표의 Tag 머리 — 뒤에 챕터 ID가 붙는다.</summary>
    internal const string ChapterLabelTag = "chapter-label:";

    /// <summary>이름표가 선 자리(<see cref="PlaceFrameLabels"/>).</summary>
    internal enum FrameLabelPlace
    {
        /// <summary>챕터 — 테두리 바깥 위(위 선에 아래 끝을 붙인 탭).</summary>
        Above,

        /// <summary>챕터 — 판의 맨 위라 바깥에 자리가 없어 테두리 안쪽 왼위.</summary>
        Inside,

        /// <summary>장면 — 장면 위 여백의 아래 끝에 아래 끝을 붙여 위로 자란 자리(100%에서는 위 테두리 선에 걸터앉는다).</summary>
        Rising,

        /// <summary>장면 — 장면 한가운데(지명식). 장면의 카드가 모두 글을 보이지 않을 때만.</summary>
        Center,

        /// <summary>장면 — 설 자리가 없어 물러났다.</summary>
        Hidden
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

        /// <summary>그 테두리의 카드 — 장면 이름표가 위로 자랄 때 비켜 갈 것에서 빠지고, 지명식으로 설 수 있는지 본다.</summary>
        public HashSet<string> Members { get; }

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
    }

    /// <summary>화면 겹의 이름표를 모두 걷는다.</summary>
    private void ClearFrameLabels()
    {
        foreach (FrameLabel label in _frameLabels)
        {
            FrameLabelLayer.Children.Remove(label.Visual);
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
    /// 그 카드가 지금 글을 보이나(글 단계 · <see cref="CardStage"/>) — 장면 이름표는 글을 보이는 카드를 덮지 않는다(지명식도). 카드의 그림자도 이것을 따른다.
    /// 판에 없는 카드는 글을 보이는 것으로 친다.
    /// </summary>
    private bool CardShowsText(string nodeId) => FindCard(nodeId) is not { } card || card.Stage == CardStage.Text;

    /// <summary>
    /// 이름표의 자리와 크기를 지금 배율 · 스크롤에 맞춘다(design/board-zoom.md 4.2절). 챕터를 먼저 세우고(숨지 않는다), 장면은 위에서부터 자리 후보를 대 본다:
    /// ① 위로 자라는 자리 → ② 지명식(장면의 카드가 모두 글을 보이지 않을 때) → ③ 물러남. 배율 · 스크롤 · 테두리가 바뀔 때마다 부른다.
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
            double scale = ScaleOf(label, zoom);
            Rect frame = Screen(label.Area);
            double inset = Math.Max(4, label.Inset * zoom);
            double natural = label.Natural.Width * scale;
            double room = frame.Width - inset - 4;
            double height = label.Natural.Height * scale;

            // 장면의 폭에서 말줄임한다 — 너무 좁으면 서지 않는다.
            if (natural > room && room < LabelShortestPx)
            {
                ShowLabel(label, default, scale, FrameLabelPlace.Hidden);
                continue;
            }

            double width = Math.Min(natural, room);

            // ① 위로 자라는 자리 — 장면 위 여백의 아래 끝(첫 카드 바로 위)에 아래 끝을 붙인다. 판의 맨 위 밖으로는 나가지 않는다(그때는 제 카드를 덮을 수 있다 —
            //    판의 맨 위에 붙은 장면).
            double bottom = ((label.Area.Y + SceneFramePad.Top - SceneLabelGap) * zoom) - offset.Y;
            var rising = new Rect(frame.X + inset, Math.Max(boardTop, bottom - height), width, height);

            if (Free(rising, label, textOnly: false))
            {
                ShowLabel(label, rising, scale, FrameLabelPlace.Rising);
                placed.Add(rising);
                continue;
            }

            // ② 지명식 — 장면 한가운데. 장면의 카드가 모두 글을 보이지 않을 때만(글을 읽는 카드는 덮지 않는다).
            if (label.Members.All(nodeId => !CardShowsText(nodeId)) && width <= frame.Width - 8 && height <= frame.Height - 8)
            {
                var center = new Rect(frame.Center.X - (width / 2), frame.Center.Y - (height / 2), width, height);

                if (Free(center, label, textOnly: true))
                {
                    ShowLabel(label, center, scale, FrameLabelPlace.Center);
                    placed.Add(center);
                    continue;
                }
            }

            // ③ 물러난다 — 챕터 이름표는 남아 있다.
            ShowLabel(label, default, scale, FrameLabelPlace.Hidden);
        }

        // 겹치지 않나 — 이미 선 이름표, 제 장면 밖의 카드(그 아래 선택지 칸까지) · 접은 상자. 지명식은 글을 보이는 카드만 피한다(접은 상자는 늘 피한다).
        bool Free(Rect rect, FrameLabel label, bool textOnly)
        {
            if (placed.Any(other => other.Intersects(rect)))
            {
                return false;
            }

            foreach ((Rect area, string? nodeId) in obstacles)
            {
                if (nodeId is not null && label.Members.Contains(nodeId))
                {
                    continue;
                }

                if (area.Intersects(rect) && (!textOnly || nodeId is null || CardShowsText(nodeId)))
                {
                    return false;
                }
            }

            return true;
        }
    }

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
    private static double ScaleOf(FrameLabel label, double zoom)
    {
        double font = label.Visual.Child is TextBlock name && name.FontSize > 0 ? name.FontSize : 12;
        return Math.Max(zoom, (label.Chapter ? ChapterLabelMinPx : SceneLabelMinPx) / font);
    }

    /// <summary>이름표를 그 자리에 그 배수로 — 말줄임 폭은 판 단위로 돌려 건다. 자리는 화면의 온 픽셀에 맞춘다(글자가 번지지 않게).</summary>
    private static void ShowLabel(FrameLabel label, Rect rect, double scale, FrameLabelPlace place)
    {
        Border visual = label.Visual;
        label.Place = place;
        label.Screen = rect;

        if (place == FrameLabelPlace.Hidden)
        {
            visual.IsVisible = false;
            return;
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
