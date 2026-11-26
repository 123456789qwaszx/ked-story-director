using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vn.App.Views;

/// <summary>
/// <b>챕터 · 장면 이름표의 화면 겹</b>(design/board-zoom.md 4절 · 2026-11-26 소유자 — 「챕터와 장면 이름표가 축소되면서 겹치는데 … 챕터는 경계 줄 대신,
/// 경계 바깥 아웃라인 위쪽에 얹고」).
///
/// 이름표는 판(<c>GraphCanvas</c>)이 아니라 판 위에 겹친 화면 겹(<c>FrameLabelLayer</c>)에 선다. 판의 여백은 판 단위로 고정인데 이름표는 화면에서 읽히는
/// 크기를 지켜야 해서, 판 위에서 키우면 줄일수록 이름표가 판을 1/배율만큼 차지해 서로 · 카드와 겹쳤다(⛔ 1차 구현 `135a508b`).
///
/// <b>장면 이름표는 늘 그 자리에, 늘 이름 그대로 선다</b>(같은 날 소유자 — 써 보고: 「장면 이름이 사실상 점으로 줄어들게 되는데, 조금 아쉽네요. 그냥 과감하게
/// 표시해주셔도 … 그냥 항상 장면 이름이 고정된채로 표시되고 있게 하는 게 더 낫겠습니다. 예외적으로 챕터이름과만 겹치지 않도록」). 자리는 장면의 왼위 —
/// 첫 카드 위 여백의 아래 끝에 아래 끝을 붙여 위로 자란다. 다른 장면의 카드 · 테두리를 비켜 가지 않고, 글자를 줄이지도 작은 표로 바꾸지도 않는다. 예외는
/// 하나 — 챕터 테두리 위(챕터 이름표의 자리)로는 나가지 않고, 챕터 이름표가 테두리 안에 섰으면 그 아래로 내려선다. 챕터 이름표가 테두리 안에 서는 것은 판의
/// 맨 위라 바깥에 자리가 없을 때뿐이고, 카드를 끌어서는 거기까지 못 간다(<see cref="BoardTopFloor"/>).
/// ⛔ 같은 날 앞서 두 번 고쳤다 — 처음에는 자리가 없으면 한가운데로 옮겨 가거나 물러났고(35%에서 첫 장면의 이름표가 사라졌다), 다음에는 그 자리에서 글자가
/// 줄다가 장면 색 점 + ⌄의 작은 표가 되고 아주 멀리서는 장면 한가운데에 이름이 옅게 깔렸다 — 위쪽 장면의 카드 · 경계에 닿을 때마다 점이 됐다 이름이
/// 됐다 했다.
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

    /// <summary>장면 이름표가 화면에서 이보다 작아지지 않는다(px) — 어느 배율에서나.</summary>
    internal const double SceneLabelMinPx = 12;

    /// <summary>챕터 이름표를 이보다 좁게 말줄임하지 않는다(px) — 「챕터 …」만 남은 이름표는 읽히지 않는다.</summary>
    internal const double LabelShortestPx = 36;

    /// <summary>장면 이름표의 아래 끝과 장면의 첫 카드 사이(판 단위).</summary>
    private const double SceneLabelGap = 4;

    /// <summary>챕터 이름표의 판 높이를 아직 못 잰 때(테두리를 긋기 전) 쓰는 값 — 「파스텔 하늘」 100%의 높이쯤(판 단위).</summary>
    private const double ChapterLabelRoomFallback = 40;

    /// <summary>챕터 이름표의 Tag 머리 — 뒤에 챕터 ID가 붙는다.</summary>
    internal const string ChapterLabelTag = "chapter-label:";

    /// <summary>이름표가 선 자리(<see cref="PlaceFrameLabels"/>).</summary>
    internal enum FrameLabelPlace
    {
        /// <summary>챕터 — 테두리 바깥 위(위 선에 아래 끝을 붙인 탭).</summary>
        Above,

        /// <summary>챕터 — 판의 맨 위라 바깥에 자리가 없어 테두리 안쪽 왼위.</summary>
        Inside,

        /// <summary>장면 — 왼위, 장면 위 여백의 아래 끝에 아래 끝을 붙여 위로 자란 이름표(100%에서는 위 테두리 선에 걸터앉는다). 챕터 이름표 아래로만 내려선다.</summary>
        Corner
    }

    /// <summary>화면 겹의 이름표 하나 — 판의 테두리 자리를 안다.</summary>
    private sealed class FrameLabel
    {
        public FrameLabel(Border visual, bool chapter, Rect area, double inset)
        {
            Visual = visual;
            Chapter = chapter;
            Area = area;
            Inset = inset;
        }

        public Border Visual { get; }

        public bool Chapter { get; }

        /// <summary>그 테두리(판 단위).</summary>
        public Rect Area { get; }

        /// <summary>테두리 왼쪽 선에서 이름표까지(판 단위).</summary>
        public double Inset { get; }

        /// <summary>장면 — 그 장면이 든 챕터의 테두리(판 단위). 이름표는 이 위로 나가지 않고, 이 챕터의 이름표와 겹치지 않는다.</summary>
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

    /// <summary>
    /// 그 카드가 지금 글을 보이나(글 단계 · <see cref="CardStage"/>) — 카드의 그림자가 이것을 따른다. 판에 없는 카드는 글을 보이는 것으로 친다.
    /// </summary>
    private bool CardShowsText(string nodeId) => FindCard(nodeId) is not { } card || card.Stage == CardStage.Text;

    /// <summary>
    /// 이름표의 자리와 크기를 지금 배율 · 스크롤에 맞춘다(design/board-zoom.md 4.2절). 챕터는 테두리 바깥 위(판 맨 위면 안쪽). 장면은 늘 왼위에 이름 그대로 —
    /// 챕터 테두리 위로는 나가지 않고 챕터 이름표와만 겹치지 않는다. 배율 · 스크롤 · 테두리가 바뀔 때마다 부른다.
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

        var chapters = new List<(Rect Area, Rect Screen)>();

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
            bool outside = above.Y >= boardTop - 0.5;
            Rect rect = outside
                ? above
                : new Rect(above.X, Math.Max(boardTop, frame.Y + Math.Max(4, 8 * zoom)), width, height);

            ShowLabel(label, rect, scale, outside ? FrameLabelPlace.Above : FrameLabelPlace.Inside);
            chapters.Add((label.Area, rect));
        }

        foreach (FrameLabel label in _frameLabels.Where(label => !label.Chapter))
        {
            MeasureLabel(label, attached);

            double scale = ScaleOf(label, zoom);
            Rect frame = Screen(label.Area);
            Rect chapter = Screen(label.Container);
            double left = frame.X + Math.Max(4, label.Inset * zoom);
            double width = Math.Min(label.Natural.Width * scale, Math.Max(chapter.Right - 4 - left, LabelShortestPx));
            double height = label.Natural.Height * scale;

            // 아래 끝 — 장면 위 여백의 아래 끝(첫 카드 바로 위). 위로 자란다.
            double bottom = ((label.Area.Y + SceneFramePad.Top - SceneLabelGap) * zoom) - offset.Y;
            double top = bottom - height;

            // 예외 하나 — 챕터 이름표와는 겹치지 않는다: 챕터 테두리(그 위가 챕터 이름표의 자리) 위로 나가지 않고, 챕터 이름표가 테두리 안에 섰으면 그 아래로.
            double ceiling = Math.Max(boardTop, chapter.Y + 2);

            foreach ((Rect area, Rect rect) in chapters)
            {
                if (area == label.Container && rect.Right > left && rect.X < left + width)
                {
                    ceiling = Math.Max(ceiling, rect.Bottom + 2);
                }
            }

            ShowLabel(label, new Rect(left, Math.Max(top, ceiling), width, height), scale, FrameLabelPlace.Corner);
        }
    }

    /// <summary>
    /// 카드가 판 위쪽으로 갈 수 있는 끝(판 단위) — 그 카드가 챕터의 맨 위 카드가 되어도 챕터 이름표가 판 맨 위 안에 다 서는 자리(2026-11-26 소유자 —
    /// 「챕터이름이 장면 안으로 침범한다는 것은, 챕터를 화면 바깥으로 끌었다는 것인데, 애초에 그렇게 화면 바깥으로는 안 끌리게」). 챕터 테두리의 위 여백
    /// (<see cref="ChapterFramePad"/>)에 지금 배율의 챕터 이름표 높이를 더한다 — 이름표는 화면 최소 크기가 있어 줄인 판에서는 판 단위로 더 높다. 지금 보는
    /// 배율에서 이름표가 판 맨 위에 닿는 데서 끌기가 멈춘다(더 줄이면 이름표가 테두리 안에 설 수 있다 — 그때는 장면 이름표가 그 아래로 비킨다).
    /// </summary>
    internal double BoardTopFloor => ChapterFramePad.Top + ChapterLabelRoom();

    /// <summary>카드가 판 왼쪽으로 갈 수 있는 끝(판 단위) — 챕터 종이(테두리)가 판 왼쪽 밖으로 나가지 않는다.</summary>
    private double BoardLeftFloor => ChapterFramePad.Left;

    /// <summary>챕터 이름표가 테두리 바깥 위에 서는 데 드는 판 높이 — 지금 배율의 화면 높이를 판 단위로(못 쟀으면 <see cref="ChapterLabelRoomFallback"/>).</summary>
    private double ChapterLabelRoom()
    {
        double zoom = Math.Max(_zoom, MinZoom);

        if (_frameLabels.FirstOrDefault(label => label.Chapter && label.Natural.Height > 0) is not { } label)
        {
            return ChapterLabelRoomFallback / Math.Min(zoom, 1);
        }

        return Math.Ceiling(label.Natural.Height * ScaleOf(label, zoom) / zoom);
    }

    /// <summary>이름표 글의 판 크기 — 챕터는 글 하나, 장면은 점 · 이름 줄(<see cref="SceneLabel"/>)의 이름.</summary>
    private static double FontOf(FrameLabel label) =>
        (label.Visual.Child as TextBlock ?? (label.Visual.Child as Panel)?.Children.OfType<TextBlock>().FirstOrDefault()) is { FontSize: > 0 } name
            ? name.FontSize
            : 12;

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
    /// 이름표를 그 자리에 그 배수로 — 말줄임 폭은 판 단위로 돌려 건다. 자리는 화면의 온 픽셀에 맞춘다(글자가 번지지 않게).
    /// </summary>
    private static void ShowLabel(FrameLabel label, Rect rect, double scale, FrameLabelPlace place)
    {
        Border visual = label.Visual;
        label.Place = place;
        label.Screen = rect;

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
