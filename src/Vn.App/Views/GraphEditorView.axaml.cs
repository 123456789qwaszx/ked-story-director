using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;

namespace Vn.App.Views;

/// <summary>
/// GraphProjection을 그리는 저작 화면.
///
/// StoryProject를 직접 순회하지 않는다. 공식 원본과 workspace의 파일 펼침 상태를
/// <see cref="GraphProjectionBuilder"/>에 전달하고, 펼쳐진 노드는 NodeCard로, 접힌 파일은
/// FileProxy로 받은 결과만 그린다. 파일을 접어도 실제 연결 target은 NodeId로 유지되고
/// endpoint만 프록시의 해당 노드 행으로 바뀐다.
/// </summary>
public partial class GraphEditorView : UserControl
{
    private const double CardWidth = 210;

    // 카드 머리 = 제목 한 줄과 그 아래 틈. 포트 줄의 자리(PortAnchor)가 이 값으로 세므로 머리는 높이를 고정한다 —
    // 글꼴에 맡기면 선 끝이 점에서 비껴 닿는다(2026-10-29까지 부제를 둔 머리가 41px인데 46으로 세어 5px 아래에 닿았다).
    // ⛔ 제목 아래의 부제(「대사 · 📄 에피소드 · {대본} · k줄」)는 2026-10-29에 걷혔다(소유자: "이 정보는 필요없으니까 치워").
    private const double TitleHeight = 20;
    private const double HeaderGap = 6;
    private const double HeaderHeight = TitleHeight + HeaderGap;
    private const double PortRowHeight = 24;
    private const double CardPadding = 8;
    private const double CardBorder = 1;
    private const double PortRadius = 6;

    /// <summary>카드 왼쪽 등뼈 막대(3) + 그 오른쪽 틈(7) — 몸통 칸은 그만큼 안에서 시작한다.</summary>
    private const double SpineInset = 10;

    // 대사 카드 안의 글 칸 (D4) — 다섯 줄 남짓. 넘치면 칸이 스크롤한다(카드 높이가 글에 따라 출렁이면
    // 아래 레일과 선택지 칸이 쓰는 동안 들썩인다).
    private const double WritingBoxHeight = 88;
    private const double WritingBoxGap = 6;
    private const string WritingTag = "writing:";

    private const double ProxyWidth = 250;
    private const double ProxyHeaderHeight = 50;
    private const double ProxyRowHeight = 28;
    private const double ProxyPortRadius = 5;

    private readonly List<NodeCard> _cards = new();
    private readonly List<FileProxyVisual> _proxies = new();
    private readonly List<EdgeVisual> _edges = new();

    /// <summary>챕터(판) 박스 — 펼친 판의 노드들을 감싸는 배경 프레임 + 이름표 (2단계 무대 3번).</summary>
    private readonly List<Control> _frames = new();

    /// <summary>챕터 간선의 철도 배선 (T1) — 미러 선·분기점·선택지 칩.</summary>
    private readonly List<Control> _railVisuals = new();

    /// <summary>
    /// 챕터 그래프 뷰가 읽은 챕터 목록 (T1). 이 뷰는 챕터를 직접 읽지 않는다 —
    /// 챕터 모델의 원천은 하나여야 감시·재시도 규칙이 두 벌이 되지 않는다.
    /// </summary>
    private IReadOnlyList<ChapterEntry> _chapters = Array.Empty<ChapterEntry>();

    internal void SupplyChapters(IReadOnlyList<ChapterEntry> entries)
    {
        _chapters = entries;
        RefreshChapterCombo();

        if (_projection is not null)
        {
            DrawChapterRails();
        }
    }

    /// <summary>선택을 비추는 동안에는 <c>SelectionChanged</c>가 선택을 바꾸지 않게 한다.</summary>
    private bool _syncingChapter;

    /// <summary>
    /// 챕터 목록과 지금 활성인 판을 콤보에 비춘다 (2026-08-25).
    ///
    /// 목록의 원천은 <b>챕터 그래프가 읽은 하나</b>다(<see cref="SupplyChapters"/>) —
    /// 여기서 따로 읽으면 감시·재시도 규칙이 두 벌이 된다. 선택도 마찬가지로
    /// <c>ActiveFileId</c> 하나를 비출 뿐이라, 챕터 그래프에서 고른 것이 여기에도 보인다.
    /// </summary>
    private void RefreshChapterCombo()
    {
        _syncingChapter = true;

        try
        {
            string[] ids = _chapters.Select(entry => entry.ChapterId).ToArray();

            if (!ids.SequenceEqual(
                    (ChapterCombo.ItemsSource as IEnumerable<string>) ?? [], StringComparer.Ordinal))
            {
                ChapterCombo.ItemsSource = ids;
            }

            // 판 이름 = ChapterId (챕터=판 1:1) — 활성 판이 곧 고른 챕터다.
            ChapterCombo.SelectedItem = _session?.ActiveFile?.Name is { } name && ids.Contains(name)
                ? name
                : null;
        }
        finally
        {
            _syncingChapter = false;
        }
    }

    private AuthoringSession? _session;
    private GraphProjection? _projection;

    private NodeCard? _draggingCard;
    private Point _dragOffset;

    private GraphOutputPortProjection? _connectingFrom;
    private Line? _connectingLine;

    private EdgeVisual? _selectedEdge;

    // ── 그래프 내비게이션 (W40) ────────────────────────────────────────────
    private const double MinZoom = 0.1;  // 큰 판(W41)에서도 전체 조망이 가능하게
    private const double MaxZoom = 2.0;

    // 판 크기 (W41) — 캔버스 크기의 유일한 정의. 미니맵과 같은 3:2 비율을 유지해야
    // 미니맵이 왜곡 없이 축소된다. Canvas는 좌표 공간일 뿐이라 크기 자체는 비용이
    // 없다 — 비용은 노드 수에서 나온다(가상화는 파일 판 백로그).
    private const double CanvasWidth = 12000;
    private const double CanvasHeight = 8000;
    private const double MinimapWidth = 180;   // 캔버스와 같은 3:2 비율
    private const double MinimapHeight = 120;

    private double _zoom = 1;
    private bool _panning;
    private Point _panStart;
    private Vector _panStartOffset;
    private bool _minimapDragging;
    private Rectangle? _minimapViewport;
    private string? _followedNodeId; // 선택이 바뀐 순간에만 화면이 따라간다 (GB-4)

    // 판별 뷰 상태 (GB-1) — 판(활성 파일)마다 보던 자리·배율을 기억한다.
    // 뷰 상태라 저장하지 않는다(원칙 E) — 세션 안에서만 산다.
    private readonly Dictionary<string, (Vector Offset, double Zoom)> _boardViews = new(StringComparer.Ordinal);
    private string? _viewedFileId;

    // 접힌 파일 드래그 (W52) — 프록시 위치는 안 노드들의 평균이므로,
    // 끌기는 파일 안 노드 전부를 같은 delta로 옮기는 것이다(펼치면 그 자리에 있다).
    private string? _draggingProxyFileId;
    private Point _proxyDragStart;
    private readonly Dictionary<string, Point> _proxyStartPositions = new(StringComparer.Ordinal);

    // 범위 선택 (W40) — 좌클릭 드래그로 잡은 노드 무리는 한 번에 움직인다.
    private Rectangle? _rubberBand;
    private Point _rubberStart;
    private readonly HashSet<string> _multiSelected = new(StringComparer.Ordinal);
    private bool _draggingGroup;
    private Point _groupDragStart;
    private readonly Dictionary<string, Point> _groupStartPositions = new(StringComparer.Ordinal);

    // 카드 안 글 칸 (D4 · 2026-10-29, docs/design/two-graphs.md §7 D4) — 노드마다 지금 선 칸과, 그 칸에
    // 마지막으로 보인 글(= 모델의 글). 칸의 글이 이것과 같으면 넣을 것이 없다.
    private readonly Dictionary<string, TextBox> _writingBoxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _writingShown = new(StringComparer.Ordinal);

    // 끄는 동안의 자리 (D3 · 2026-10-29, docs/design/two-graphs.md §6-2) — 카드 · 무리 · 접힌 판
    // 어느 끌기든 프로젝트에는 <b>놓을 때 한 번</b> 적는다(되돌리기 한 칸). 그때까지 화면은 이 값을 따른다.
    private readonly Dictionary<string, Point> _dragPositions = new(StringComparer.Ordinal);
    private GraphPosition _proxyPositionStart;
    private Vector _proxyDragDelta;

    /// <summary>
    /// 우측 곁기둥의 내용을 셸이 물려준다 (2026-08-22) — 대사 노드 편집기.
    /// 자리는 이 판의 것이고(쓰는 화면이 여기뿐이다) 배선은 셸의 것이다: 편집기가
    /// 세션·선택 전환과 얽혀 있어 이 뷰로 옮기면 그 배선이 두 벌이 된다.
    /// </summary>
    internal void SetSidePanel(Control content) => SideHost.Content = content;

    public GraphEditorView()
    {
        InitializeComponent();

        // 판 크기는 상수 한 곳이 정한다 (W41) — 미니맵 배율과 어긋날 길을 없앤다.
        GraphCanvas.Width = CanvasWidth;
        GraphCanvas.Height = CanvasHeight;

        AddDialogueButton.Click += (_, _) => AddDialogueNode();
        DeleteNodeButton.Click += (_, _) => DeleteSelectedNode();

        // 고른 챕터의 판을 활성으로 — 그 뒤의 [+ 대사 노드]가 거기에 선다.
        // ⚠ 다시 그리기가 콤보를 맞출 때는 안 돈다(_syncingChapter) — 안 그러면
        //    선택을 비추는 일이 선택을 바꾸는 일이 되어 서로를 부른다.
        ChapterCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingChapter || _session is null ||
                ChapterCombo.SelectedItem is not string chapterId)
            {
                return;
            }

            UiGuard.Run(_session, "챕터 고르기", () =>
                _session.SelectFile(_session.EnsureChapterBoard(chapterId)));
        };

        GraphCanvas.PointerMoved += OnCanvasPointerMoved;
        GraphCanvas.PointerReleased += OnCanvasPointerReleased;
        GraphCanvas.PointerPressed += OnCanvasPointerPressed;

        // 그래프 내비게이션 (W40) — 휠 줌·중간 버튼 팬은 스크롤보다 먼저 가로챈다.
        GraphScroll.AddHandler(PointerWheelChangedEvent, OnGraphWheel, RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerPressedEvent, OnGraphPanPressed, RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerMovedEvent, OnGraphPanMoved, RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerReleasedEvent, OnGraphPanReleased, RoutingStrategies.Tunnel);
        GraphScroll.ScrollChanged += (_, _) => RefreshMinimapViewport();
        MinimapCanvas.PointerPressed += OnMinimapPressed;
        MinimapCanvas.PointerMoved += OnMinimapMoved;
        MinimapCanvas.PointerReleased += (_, _) => _minimapDragging = false;

        // 전체 보기·배율 프리셋 (GB-4). 프리셋은 화면 중앙 기준으로 배율만 바꾼다.
        FitAllButton.Click += (_, _) => FitAll();
        Zoom50Button.Click += (_, _) => ApplyZoom(0.5, null);
        Zoom100Button.Click += (_, _) => ApplyZoom(1, null);
        Zoom150Button.Click += (_, _) => ApplyZoom(1.5, null);
    }

    internal void Attach(AuthoringSession session)
    {
        _session = session;

        // 단추가 <b>무엇을 지울지</b>는 고른 것에 달렸다 — 고를 때마다 이름을 맞춘다.
        session.SelectionChanged += (_, _) =>
            UiGuard.Run(_session, "삭제 단추 맞추기", RefreshDeleteButton);

        RefreshDeleteButton();
    }

    /// <summary>
    /// 지울 것의 이름을 <b>단추에 적는다</b> (2026-09-18).
    ///
    /// ⛔ 챕터 판의 카드를 고른 채로 「노드 삭제」라고 적혀 있으면 <b>단추가 거짓말</b>이다 —
    /// 그것을 누르면 에피소드가, 그러니까 간선까지 함께 사라진다. 누르기 <b>전에</b>
    /// 보이는 것이 확인 한 걸음보다 먼저다.
    /// </summary>
    private void RefreshDeleteButton()
    {
        bool episode =
            _session?.SelectedNode is DialogueNode card &&
            EpisodeNaming.ChapterOf(_session.Project, card) is not null;

        DeleteNodeButton.Content = episode ? "에피소드 삭제…" : "노드 삭제";
        ToolTip.SetTip(
            DeleteNodeButton,
            episode
                ? "이 카드는 챕터의 에피소드입니다 — 간선·픽스처 참조까지 함께 지웁니다. " +
                  "되돌리기(Ctrl+Z)로 돌아옵니다."
                : "이 판의 카드 하나를 지웁니다. 되돌리기 한 번으로 돌아옵니다.");
    }

    // ── 그리기 ──────────────────────────────────────────────────────────────

    internal void Rebuild()
    {
        if (_session is null)
        {
            return;
        }

        // 활성 판이 바뀌면 콤보도 따라간다 — 챕터 그래프에서 고른 것이 여기에도 보인다.
        RefreshChapterCombo();

        _projection = GraphProjectionBuilder.Build(
            _session.Project,
            _session.ExpandedFileIds,
            _session.Definition);

        // 카드 안 글 칸에 초점이 있었으면 다시 그린 뒤 그 칸으로 돌려준다(D4) — 판을 비우기 전에 잰다.
        WritingFocus? writing = CaptureWritingFocus();

        GraphCanvas.Children.Clear();
        _writingBoxes.Clear();
        _writingShown.Clear();
        _cards.Clear();
        _proxies.Clear();
        _edges.Clear();
        _frames.Clear();
        _selectedEdge = null;
        _selectedRail = null;
        _bending = false;
        _shapePreview = null;
        HideShapeOverlay();
        SlotLabelBox = null;

        // 고른 간선이 사라졌으면 그 간선을 설명하던 줄도 사라져야 한다 — 안 그러면
        // 방금 지운 간선의 이름이 띠에 남는다(늘 서 있던 설명이 그 자리를 덮고 있어서
        // 여태 안 보였다).
        SetHint(null);

        foreach (GraphItemProjection item in _projection.Items)
        {
            switch (item)
            {
                case ExpandedNodeProjection node:
                    _cards.Add(BuildCard(node));
                    break;

                case CollapsedFileProjection file:
                    _proxies.Add(BuildFileProxy(file));
                    break;
            }
        }

        DrawChapterFrames();

        AddDialogueButton.IsEnabled = _session.ActiveFileId is not null;

        DrawEdges();
        HighlightSelection();
        RefreshMinimap();
        HandleBoardSwitch();
        RestoreWritingFocus(writing);
    }

    /// <summary>
    /// 챕터(판) 박스 (2단계 무대 3번, PDF 3·9장) — 펼친 판마다 노드들을 감싸는 프레임과
    /// 이름표를 카드 <b>뒤에</b> 깐다. 작가가 "이 노드가 어느 챕터 소속인지"를 보고 붙일
    /// 곳을 정하는 무대다. 히트 대상이 아니라서 클릭·드래그·포트에 아무 영향이 없다.
    /// </summary>
    private void DrawChapterFrames()
    {
        foreach (Control frame in _frames)
        {
            GraphCanvas.Children.Remove(frame);
        }

        _frames.Clear();

        if (_session is null || _projection is null)
        {
            return;
        }

        foreach (IGrouping<string, ExpandedNodeProjection> group in _projection.Items
                     .OfType<ExpandedNodeProjection>()
                     .GroupBy(item => item.FileId, StringComparer.Ordinal))
        {
            Rect? bounds = null;

            foreach (ExpandedNodeProjection node in group)
            {
                if (FindCard(node.NodeId) is not { } card)
                {
                    continue;
                }

                Rect rect = FrameRectOf(node, card);
                bounds = bounds is { } current ? current.Union(rect) : rect;
            }

            if (bounds is not { } area)
            {
                continue;
            }

            // 위쪽은 이름표 자리까지 여유를 둔다.
            area = area.Inflate(new Thickness(24, 46, 24, 24));

            bool isActive = string.Equals(group.Key, _session.ActiveFileId, StringComparison.Ordinal);
            string chapterName = _session.Project.Files
                .FirstOrDefault(file => string.Equals(file.Id, group.Key, StringComparison.Ordinal))
                ?.Name ?? group.Key;

            var frame = new Border
            {
                Width = area.Width,
                Height = area.Height,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(isActive ? 1.6 : 1),
                BorderBrush = new SolidColorBrush(
                    isActive ? Color.FromArgb(150, 61, 123, 217) : Color.FromArgb(70, 128, 128, 128)),
                Background = new SolidColorBrush(
                    isActive ? Color.FromArgb(12, 61, 123, 217) : Color.FromArgb(8, 128, 128, 128)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(frame, area.X);
            Canvas.SetTop(frame, area.Y);

            var label = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3),
                Background = new SolidColorBrush(
                    isActive ? Color.FromArgb(220, 61, 123, 217) : Color.FromArgb(160, 107, 114, 128)),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock
                {
                    Text = $"챕터 {chapterName} ⌄",
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White
                }
            };
            ToolTip.SetTip(label, "클릭하면 이 챕터를 표 형태로 접습니다. 접힌 표를 더블클릭하면 다시 펼칩니다.");

            // 이름표만 히트 대상이다(프레임은 아님) — 클릭 = 접기 (PDF 9장 "체크해제하면 표형태").
            string frameFileId = group.Key;
            label.PointerPressed += (_, args) =>
            {
                args.Handled = true;
                _session?.SetFileExpanded(frameFileId, expanded: false);
            };

            Canvas.SetLeft(label, area.X + 12);
            Canvas.SetTop(label, area.Y + 10);

            _frames.Add(frame);
            _frames.Add(label);

            // 챕터 안에 장면을 한 겹 더 (R6 S-3b) — 챕터 프레임 <b>뒤에</b> 더해야 그 위에
            // 깔린다(아래 Insert가 목록 순서대로 넣는다).
            DrawSceneFrames(group.Key, group);
        }

        // 카드·간선보다 뒤에 깔리도록 맨 앞 인덱스에 순서대로 끼운다.
        for (int index = 0; index < _frames.Count; index++)
        {
            GraphCanvas.Children.Insert(index, _frames[index]);
        }

        DrawChapterRails();
    }

    /// <summary>
    /// <b>장면 프레임</b> — 챕터 프레임 안에 한 겹 더 (R6 S-3b · <c>docs/plans/R6-explorer.md</c> §5).
    ///
    /// 같은 장면의 카드가 한 영역으로 보여야 작가가 <b>어디까지가 한 수명</b>인지 안다 —
    /// 장면 안에서는 모든 게 물릴 수 있고 장면이 끝나면 확정되기 때문이다.
    ///
    /// ⚠ <b>안 그리는 경우 셋</b>: 챕터가 아닌 판 · 장면ID를 하나도 안 적은 챕터 ·
    /// 장면이 하나뿐인 챕터. 앞의 둘은 접힌 판과 <b>같은 규칙</b>이고(규격 §2), 셋째는
    /// 챕터 프레임과 똑같은 자리에 겹쳐 그려 봐야 테두리만 두 겹이 되기 때문이다.
    ///
    /// ⚠ 히트 대상이 아니다 — 클릭·드래그·포트에 아무 영향이 없다(챕터 프레임과 같다).
    /// </summary>
    private void DrawSceneFrames(string fileId, IEnumerable<ExpandedNodeProjection> nodes)
    {
        if (_session?.Project.Files.FirstOrDefault(file =>
                string.Equals(file.Id, fileId, StringComparison.Ordinal)) is not { } board ||
            _session.Editor.FindChapter(board.Name) is not { } chapter)
        {
            return;
        }

        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(
            chapter.ToGraphModel(chapter.ChapterId));

        if (scenes.Count < 2 || scenes.All(scene => scene.IsDefault))
        {
            return;
        }

        var sceneOf = new Dictionary<string, ChapterScene>(StringComparer.Ordinal);

        foreach (ChapterScene scene in scenes)
        {
            foreach (ChapterEpisode episode in scene.Episodes)
            {
                sceneOf[episode.EpisodeId] = scene;
            }
        }

        List<ExpandedNodeProjection> all = nodes.ToList();

        foreach (ChapterScene scene in scenes)
        {
            Rect? bounds = null;

            foreach (ExpandedNodeProjection node in all)
            {
                if (EpisodeOf(node) is not { } episodeId ||
                    !sceneOf.TryGetValue(episodeId, out ChapterScene? owner) ||
                    !ReferenceEquals(owner, scene) ||
                    FindCard(node.NodeId) is not { } card)
                {
                    continue;
                }

                Rect rect = FrameRectOf(node, card);
                bounds = bounds is { } current ? current.Union(rect) : rect;
            }

            if (bounds is not { } area)
            {
                continue;
            }

            // 챕터 프레임(24·46)보다 얕게 — 두 테두리가 붙어 보이지 않게 한다.
            area = area.Inflate(new Thickness(10, 26, 10, 10));

            _frames.Add(SceneFrame(area));
            _frames.Add(SceneLabel(area, scene));
        }
    }

    /// <summary>그 노드가 대신하는 에피소드 — 표식이 먼저고, 없으면 이름이다(EpisodeNaming 한 벌).</summary>
    private string? EpisodeOf(ExpandedNodeProjection node) =>
        _session?.Project.FindNode(node.NodeId) is DialogueNode dialogue
            ? EpisodeNaming.EpisodeIdOf(dialogue)
            : null;

    private static Border SceneFrame(Rect area)
    {
        var frame = new Border
        {
            Width = area.Width,
            Height = area.Height,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 217, 119, 6)),
            Background = new SolidColorBrush(Color.FromArgb(10, 217, 119, 6)),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(frame, area.X);
        Canvas.SetTop(frame, area.Y);

        return frame;
    }

    private static Border SceneLabel(Rect area, ChapterScene scene)
    {
        var label = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1),
            Background = new SolidColorBrush(Color.FromArgb(180, 217, 119, 6)),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                // ⚠ 장면이 확정·롤백의 경계라는 것이 이름표에서 읽혀야 한다.
                Text = scene.HasSplitEntry
                    ? $"장면 {scene.DisplayName} ⚠"
                    : $"장면 {scene.DisplayName}",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            }
        };

        Canvas.SetLeft(label, area.X + 10);
        Canvas.SetTop(label, area.Y + 6);

        return label;
    }

    // ── 챕터 간선 철도 배선 (T1) ────────────────────────────────────────────

    private static readonly IBrush RailBrush = new SolidColorBrush(Color.FromArgb(170, 120, 120, 120));
    private static readonly IBrush RailChipBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x6A, 0x14));
    private const double RailBranchGap = 44;    // 가지 사이 세로 간격
    private const double RailFirstDrop = 36;    // 카드 아래 첫 가지까지
    private const double RailArrivalRise = 26;  // 도착 카드 위 가운데 접점까지의 높이 (D3)
    private const double RailLaneOut = 12;      // 출발 카드 오른쪽에서 내려가는 레인까지 (D3)
    private const double RailLaneGap = 8;       // 레인끼리 가로 간격 — 같은 줄로 겹치지 않게 (D3)

    /// <summary>
    /// 챕터 간선을 철도 노선처럼 (T1, 소유자 그림) — 출발 카드에서 수직 줄기가 내려오고,
    /// 단일 분기점에서 가지들이 갈라져 도착 카드로 들어간다. 선택은 항상 에피소드 끝이므로
    /// (CHOICE는 파일 맨 끝 0~1개) 분기점은 하나다 — Line 비율 같은 것은 존재하지 않는다.
    /// 기본 칩은 문구만(T-D4 — 수치는 챕터 그래프의 책임), 클릭하면 읽기 전용 상세.
    /// </summary>
    private void DrawChapterRails()
    {
        foreach (Control visual in _railVisuals)
        {
            GraphCanvas.Children.Remove(visual);
        }

        _railVisuals.Clear();

        if (_session is null || _projection is null)
        {
            return;
        }

        // 고른 레인이 이번에 안 그려지면(판을 접었다 · 길이 사라졌다) 손잡이도 내린다 — 맨 끝에서 본다.
        _selectedRailDrawn = false;

        foreach (IGrouping<string, ExpandedNodeProjection> group in _projection.Items
                     .OfType<ExpandedNodeProjection>()
                     .GroupBy(item => item.FileId, StringComparer.Ordinal))
        {
            string boardName = _session.Project.Files
                .FirstOrDefault(file => string.Equals(file.Id, group.Key, StringComparison.Ordinal))
                ?.Name ?? string.Empty;

            // 살아 있는 프로젝트의 챕터를 본다 — 이 화면이 간선을 직접 긋는다.
            // ⛔ 셸이 공급한 챕터 목록(<c>_chapters</c>) 스냅샷을 뒷받침으로 보던 길은 2026-10-29에 걷혔다
            //    (legacy-cleanup.md L3). 그 목록도 같은 프로젝트를 읽은 것이라, 프로젝트에 없는 챕터를 줄 수 있는 것은
            //    낡은 스냅샷뿐이었다.
            ChapterGraphModel? chapter =
                _session.Editor.FindChapter(boardName)?.ToGraphModel(boardName, _session.Definition);

            if (chapter is null)
            {
                continue; // 챕터 판이 아니다 — 일반 프로젝트는 아무 변화 없다.
            }

            // 에피소드 Id → 그 카드 자리. 아직 카드가 없는 에피소드는 없는 것으로 둔다 — 없는 노드로 선을 그으면 거짓말이다.
            var spots = new Dictionary<string, (DialogueNode Node, Rect Rect)>(StringComparer.Ordinal);

            foreach (ExpandedNodeProjection item in group)
            {
                if (FindCard(item.NodeId) is not { } card)
                {
                    continue;
                }

                var rect = new Rect(item.Position.X, item.Position.Y, CardWidth, CardHeightOf(card));

                if (_session.Project.FindNode(item.NodeId) is DialogueNode dialogue)
                {
                    // ⭐ <b>곁가지도 제 가지와 선택지 칸을 받는다</b> (R7 P-6). 전에는 척추
                    //    카드만 여기 들어와 레일을 받았는데, 이제는 판의 모든 카드가
                    //    에피소드라 <b>전부 아래에 3칸이 뚫려야</b> 한다 — 곁가지에서
                    //    진짜 분기를 뚫는 것이 「분기 추가」가 노리는 바로 그 손짓이다.
                    if (dialogue.MarkedEpisodeId is { Length: > 0 } episodeId)
                    {
                        spots[episodeId] = (dialogue, rect);
                    }
                }
            }

            // 도착 포트 (2026-08-15 소유자) — 여러 선택지가 같은 에피소드로 이어지는 일이
            // 많으므로, 들어오는 가지들은 도착 카드 앞의 접점 하나로 모인다.
            var arrivals = new Dictionary<string, List<(string From, string Label)>>(StringComparer.Ordinal);

            foreach ((string episodeId, (DialogueNode node, Rect rect)) in spots)
            {
                // 챕터에서 지운 에피소드의 노드가 판에 남아 있으면 레일을 긋지 않는다 —
                // 챕터 밖의 노드에 진행·종료를 그리는 것은 거짓말이다(동기화 보고의 몫).
                if (chapter.Episodes.All(episode =>
                        !string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal)))
                {
                    continue;
                }

                List<ChapterEdge> edges = chapter.Edges
                    .Where(edge => string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal))
                    .ToList();

                DrawRailsFrom(boardName, node, rect, edges, spots, arrivals);
            }

            foreach ((string toId, List<(string From, string Label)> incoming) in arrivals)
            {
                DrawArrivalPort(spots[toId].Rect, toId, incoming);
            }
        }

        // 프레임 뒤·카드 앞 — 프레임들 바로 다음 인덱스에 끼운다.
        for (int index = 0; index < _railVisuals.Count; index++)
        {
            GraphCanvas.Children.Insert(_frames.Count + index, _railVisuals[index]);
        }

        if (_selectedRail is not null && !_selectedRailDrawn)
        {
            _selectedRail = null;
            HideShapeOverlay();
        }
    }

    /// <summary>
    /// 한 출발 카드의 줄기와 가지들 (T2, v9로 개정 2026-08-17).
    ///
    /// <b>가지의 주인은 챕터 `간선` 시트다</b> — 가지 하나 = 간선 하나이고 순서도 시트의 행
    /// 순서다(그것이 화면에 뜨는 순서다). 예전에는 대본의 OPTION 줄이 가지를 만들고 간선이
    /// 문구로 짝을 찾았는데, v9에서 대본에 OPTION이 없는 것이 정상이 되면서 모든 간선이
    /// "유령"으로 보였다(소유자 보고). 이제 그 개념 자체가 없다.
    ///
    /// 가지마다 그 칸(<see cref="SlotChip"/>)이 선다 — 문구를 고치고 점을 끌어 잇는 자리다.
    ///
    /// ⛔ 칸이 없는 가지의 옛 칩(「● 문구」 · 「○ 진행」)과 그 칩이 열던 자유 씬 배선, 배선된 자유 씬을 지나는 웹
    ///    경로 · (진행) 합류선 · 「⏹ 종료」 스텁은 2026-10-29에 걷혔다(legacy-cleanup.md L3). 칸은 간선마다 늘 서므로
    ///    칸 없는 가지는 낡은 챕터 스냅샷에서만 나왔고, 자유 씬 배선(<see cref="DialogueNode.ChoiceExits"/>)은 그 칩만
    ///    적었다. 옛 파일의 배선 데이터는 그대로 오간다.
    /// </summary>
    private void DrawRailsFrom(
        string chapterId,
        DialogueNode source,
        Rect sourceRect,
        IReadOnlyList<ChapterEdge> edges,
        IReadOnlyDictionary<string, (DialogueNode Node, Rect Rect)> spots,
        Dictionary<string, List<(string From, string Label)>> arrivals)
    {
        // 이 에피소드의 선택지 칸들 (R7) — 가지마다 제 칸을 얹어 <b>철도 위에서</b> 고치고 잇는다.
        IReadOnlyList<GraphChoicePort> slots = SlotsOf(source.Id);

        var branches = new List<(ChapterEdge? Edge, Rect? Target, GraphChoicePort Slot)>();

        // 가지 순서 = <b>칸의 차례 그대로</b> (H2 · 2026-10-29 소유자: "선택지 문구의 순서를 유지해") — 이은 줄 · 적어 둔
        // 줄 · 빈 칸이 사람이 둔 자리에 선다(<see cref="SlotsOf"/>). 이은 줄끼리는 간선 시트 행 순서와 같다 — 잇는 순간
        // 그 자리에 끼워 넣기 때문이다(<see cref="JoinChoice"/>).
        // ⛔ 예전에는 이은 길이 늘 위로, 적어만 둔 칸이 그 아래로 모였다 — 아래 칸을 먼저 이으면 그 줄이 위로 올라가
        //    적은 순서가 뒤바뀌었다.
        // ⚠ 문구 없는 길(진행)은 이은 줄 가운데 맨 아래다 — 소유자 보고("진행이 선택지 위에 서니 헷갈린다"). 보이지 않는
        //   기본은 플레이어에게 버튼으로 안 뜨므로 선택지 순서를 왜곡하지도 않는다.
        foreach (GraphChoicePort slot in slots)
        {
            if (slot.IsEmpty)
            {
                branches.Add((null, null, slot));
            }
            else if (edges.FirstOrDefault(edge =>
                         string.Equals(edge.ToEpisodeId, slot.ToEpisodeId, StringComparison.Ordinal) &&
                         string.Equals(edge.OptionLabel ?? string.Empty, slot.Label, StringComparison.Ordinal))
                     is { } edge)
            {
                // 도착 노드가 아직 이 판에 없어도 가지는 보인다 — 숨기면 선택지가 통째로 사라져 그래프가 고장 난
                // 것처럼 보인다(소유자 보고 2026-08-15). 없는 노드로 선을 긋는 대신 「카드 없음」 표식에서 멈춘다.
                Rect? target = spots.TryGetValue(edge.ToEpisodeId, out (DialogueNode Node, Rect Rect) spot)
                    ? spot.Rect
                    : null;

                branches.Add((edge, target, slot));
            }
        }

        if (branches.Count == 0)
        {
            return;
        }

        // 선택지마다 제 도착을 본다 (H1 · 2026-10-29 소유자: "연결되어 있는 하나의 루트의 선택지 방향이 바뀌길") —
        // 도착 카드가 이 카드보다 왼쪽이면 <b>그 줄만</b> 거울에 비춘 모양이다: 점이 줄기 쪽 · 문구가 그 왼쪽에 서고,
        // 내려가는 레인은 카드 <b>왼쪽</b>으로 빠진다. 아직 안 이은 칸은 오른쪽이다. 지금 그린 자리로 재므로 끄는 동안에도
        // 넘어가는 순간 뒤집힌다.
        List<bool> lefts = branches
            .Select(branch => branch.Target is { } target && EdgeSide.GoesLeft(sourceRect.X, target.X))
            .ToList();

        // 줄기는 <b>언제나 카드 가운데 아래</b>다 (2026-10-29 소유자: "항상 노드 중앙 아래쪽에 선택지가 있도록").
        // ⛔ H1에서는 줄들이 나가는 쪽의 반대편에 섰다(전부 오른쪽이면 카드 왼쪽 안, 전부 왼쪽이면 오른쪽 안, 섞이면
        //    가운데) — 선 하나를 이으면 줄기가 옆으로 뛰어 카드마다 선택지 자리가 달라 보였다. 문구가 길면 레인이 그만큼
        //    바깥으로 비켜 가므로(`DrawRailBranch`의 dropX) 반폭이어도 겹치지 않는다.
        double trunkX = sourceRect.X + (sourceRect.Width / 2);
        double branchY = sourceRect.Bottom + RailFirstDrop;
        int leftLane = 0;
        int rightLane = 0;

        for (int index = 0; index < branches.Count; index++)
        {
            (ChapterEdge? edge, Rect? target, GraphChoicePort slot) = branches[index];
            bool mirrored = lefts[index];

            // 내려가는 레인은 카드 바깥에 가지마다 한 줄씩(쪽마다 따로 센다) — 같은 줄로 겹치면 어느 선이 어디로 가는지 안 보인다.
            double laneX = mirrored
                ? sourceRect.X - RailLaneOut - (leftLane++ * RailLaneGap)
                : sourceRect.Right + RailLaneOut + (rightLane++ * RailLaneGap);
            DrawRailBranch(chapterId, source, edge, trunkX, branchY, laneX, target, slot, mirrored);

            if (edge is not null && target is not null)
            {
                // 도착 포트 장부 — 어느 에피소드의 어느 선택지가 여기로 들어오는가.
                if (!arrivals.TryGetValue(edge.ToEpisodeId, out List<(string From, string Label)>? list))
                {
                    arrivals[edge.ToEpisodeId] = list = new List<(string, string)>();
                }

                list.Add((EpisodeNaming.EpisodeIdOf(source),
                    edge.HasNoOptionLabel ? "(진행)" : edge.OptionLabel!));
            }

            branchY += RailBranchGap;
        }

        _railVisuals.Add(RailLine(trunkX, sourceRect.Bottom, trunkX, branchY - RailBranchGap));
    }

    /// <summary>도착 접점 — 카드의 위 가운데, 조금 위 (D3 · 2026-10-29).</summary>
    private static (double X, double Y) ArrivalJunction(Rect target) =>
        (target.X + target.Width / 2, target.Y - RailArrivalRise);

    /// <summary>
    /// 카드 아래로 레일(선택지 칩)이 차지하는 깊이 — 프레임이 카드만이 아니라 레일까지 감싸게 한다 (D3).
    /// 에피소드 카드가 아니면(칩이 없으면) 0이다.
    /// </summary>
    private double RailDepthOf(string nodeId)
    {
        int slots = SlotsOf(nodeId).Count;

        return slots == 0 ? 0 : RailFirstDrop + ((slots - 1) * RailBranchGap) + 14;
    }

    /// <summary>프레임이 감쌀 한 카드의 자리 — 카드와 그 아래 레일까지.</summary>
    private Rect FrameRectOf(ExpandedNodeProjection node, NodeCard card) =>
        new(node.Position.X, node.Position.Y, CardWidth, CardHeightOf(card) + RailDepthOf(node.NodeId));

    /// <summary>
    /// 도착 포트 (2026-08-15 소유자) — 들어오는 가지들이 도착 카드 앞의 접점 하나로 모인다.
    /// 누르면 어느 에피소드의 어느 선택지들이 여기로 이어지는지 목록이 열린다(읽기 전용 —
    /// 잇고 끊는 곳은 출발 쪽 칩과 챕터 그래프다).
    /// </summary>
    private void DrawArrivalPort(Rect target, string episodeId, IReadOnlyList<(string From, string Label)> incoming)
    {
        (double junctionX, double junctionY) = ArrivalJunction(target);

        _railVisuals.Add(RailLine(junctionX, junctionY, junctionX, target.Y - 10));
        _railVisuals.Add(RailArrow(junctionX, target.Y - 9));

        var port = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = RailChipBrush,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(port, $"들어오는 길 {incoming.Count}개 — 누르면 목록이 열립니다.");
        Canvas.SetLeft(port, junctionX - 5);
        Canvas.SetTop(port, junctionY - 5);

        string captured = episodeId;
        var capturedIncoming = incoming.ToList();
        port.PointerPressed += (_, args) =>
        {
            args.Handled = true;

            var panel = new StackPanel { Spacing = 3, MinWidth = 200 };
            panel.Children.Add(new TextBlock
            {
                Text = $"{captured}로 들어오는 길",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold
            });

            foreach ((string from, string label) in capturedIncoming)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"{from} · {label}",
                    FontSize = 11,
                    Opacity = 0.85
                });
            }

            panel.Children.Add(new TextBlock
            {
                Text = "잇고 끊는 곳은 출발 쪽 칩과 챕터 그래프입니다.",
                FontSize = 10,
                Opacity = 0.5,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 240
            });

            new Flyout { Content = panel }.ShowAt(port);
        };

        _railVisuals.Add(port);
    }

    /// <summary>
    /// <b>철도 가지 위의 선택지 칸</b> (R7 · 2026-09-16 소유자: "아래쪽으로 3개").
    ///
    /// 점 하나와 문구 하나. 점은 세 색으로 자리를 말하고(회색·붉은색·초록색), 문구를 누르면
    /// 그 자리에서 고치며, 점을 끌면 이어진다. 카드 <b>아래</b>에 서는 이유는 선택지가
    /// 실제로 뻗어 나가는 자리가 거기이기 때문이다 — 카드 안에 두면 이야기가 어디로
    /// 갈라지는지 한눈에 안 보인다.
    /// </summary>
    /// <param name="mirrored">왼쪽으로 나가는 선택지(도착 카드가 이 카드보다 왼쪽, <see cref="EdgeSide"/>)의 칩 — 점이 줄기 쪽(오른쪽)에,
    /// 문구가 그 왼쪽에 선다.</param>
    private Control SlotChip(GraphChoicePort slot, string nodeId, bool mirrored = false)
    {
        IReadOnlyList<GraphChoicePort> ports = SlotsOf(nodeId);
        bool live = IsLive(slot, ports);
        bool auto = IsAutoSlot(slot, ports);
        string text = LabelOf(slot);
        IBrush dot = SlotDot(slot, ports);

        var knob = new Ellipse
        {
            Width = PortRadius * 2,
            Height = PortRadius * 2,
            Fill = live ? dot : Brushes.Transparent,
            Stroke = dot,
            StrokeThickness = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            [ToolTip.TipProperty] = slot.IsEmpty
                ? live ? "끌어서 다음 에피소드에 놓으세요." : "선택지 문구를 적으면 끌 수 있습니다."
                : "이어져 있습니다. 빈 곳에 끌어다 놓으면 끊깁니다."
        };

        var label = new TextBlock
        {
            Text = auto ? "자동" : text.Length > 0 ? text : "＋ 선택지",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = live ? dot : null,
            Opacity = live ? 1 : 0.45,
            FontStyle = text.Length > 0 || auto ? FontStyle.Normal : FontStyle.Italic,
            VerticalAlignment = VerticalAlignment.Center,

            // ⚠ 배경이 없으면 <b>눌림이 안 온다</b> — 투명한 TextBlock은 히트 대상이 아니다.
            //   2026-09-16에 "문구를 적을 칸이 안 열린다"의 진짜 원인이 이것이었다.
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Ibeam),
            [ToolTip.TipProperty] = auto
                ? "자동 길 — 문구 없이 다음으로 갑니다. 선택지 문구를 하나라도 적으면 이 자리가 닫힙니다."
                : "눌러서 선택지 문구를 적습니다. 적어야 끌어서 이을 수 있습니다."
        };

        if (!slot.IsAuto)
        {
            label.PointerPressed += (_, args) => UiGuard.Run(_session, "선택지 문구", () =>
            {
                args.Handled = true;
                _editingSlot = SlotKey(slot);
                Rebuild();
            });
        }

        var line = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Background = Brushes.Transparent,

            // 어느 에피소드의 몇 번 칸인지 — 검증이 그 줄을 찾는 자리다.
            Tag = SlotKey(slot)
        };

        line.Children.Add(knob);

        line.Children.Insert(
            mirrored ? 0 : 1,
            string.Equals(_editingSlot, SlotKey(slot), StringComparison.Ordinal)
                ? SlotEditor(slot)
                : label);

        // 꺼진 점을 누르면 <b>켠다</b>, 켜진 점을 누르면 <b>끈다</b> — 손짓 하나가 한 가지 뜻이다.
        knob.PointerPressed += (_, args) => UiGuard.Run(_session, "선택지", () =>
        {
            args.Handled = true;

            if (live)
            {
                BeginSlotDrag(slot, knob, nodeId);
                return;
            }

            // 꺼진 점을 누르면 <b>문구 칸을 연다</b> — 살리는 것은 적는 행위이지 누르는
            // 행위가 아니다(2026-09-17 소유자). 점도 문구도 같은 자리로 데려간다.
            _editingSlot = SlotKey(slot);
            Rebuild();
        });

        return line;
    }

    /// <summary>
    /// 그 노드의 선택지 칸들 — <b>사람이 둔 차례 그대로</b> (H2). <see cref="GraphChoicePort.Slot"/>은 화면의 줄 번호다.
    ///
    /// 투영은 이은 칸(간선 순서)을 앞에, 빈 칸을 뒤에 낸다 — 적어 둔 문구는 투영이 모른다(저장하지 않는다). 여기서
    /// <see cref="RowsOf"/>의 차례에 맞춰 다시 세운다: 이은 줄 자리에는 간선을 순서대로 채우고, 나머지는 빈 칸이다.
    /// </summary>
    private IReadOnlyList<GraphChoicePort> SlotsOf(string nodeId)
    {
        List<GraphChoicePort> raw = _projection?.Items.OfType<ExpandedNodeProjection>()
            .FirstOrDefault(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            ?.OutputPorts
            .Select(port => port.ChoicePort)
            .Where(choice => choice is not null)
            .Select(choice => choice!)
            .ToList() ?? [];

        if (raw.Count == 0)
        {
            return raw;
        }

        // 이은 칸은 간선 순서대로 — 단 문구 없는 길(진행)은 그 가운데 맨 아래다(소유자: "진행이 선택지 위에 서니 헷갈린다").
        List<GraphChoicePort> joined = raw
            .Where(choice => !choice.IsEmpty)
            .OrderBy(choice => choice.Label.Length == 0)
            .ToList();
        List<ChoiceRow?> rows = RowsOf(raw[0].ChapterId, raw[0].FromEpisodeId, joined.Count);
        var slots = new List<GraphChoicePort>();
        int next = 0;

        GraphChoicePort Blank(int at) => new(
            raw[0].ChapterId, raw[0].FromEpisodeId, at,
            Label: string.Empty, ToEpisodeId: null, ToNodeId: null, IsAuto: false);

        foreach (ChoiceRow? row in rows)
        {
            slots.Add(row is ChoiceRow.Joined && next < joined.Count
                ? joined[next++] with { Slot = slots.Count }
                : Blank(slots.Count));
        }

        while (next < joined.Count)
        {
            slots.Add(joined[next++] with { Slot = slots.Count });
        }

        // 새로 낼 수 있는 칸은 늘 셋까지 선다(투영이 정한 수).
        while (slots.Count < raw.Count)
        {
            slots.Add(Blank(slots.Count));
        }

        return slots;
    }

    /// <summary>이은 칸의 간선 — 신원은 (출발, 도착, 문구)다.</summary>
    private ChapterEdge? EdgeOf(GraphChoicePort slot) =>
        _session?.Editor.FindChapter(slot.ChapterId)?.Edges.FirstOrDefault(edge =>
            string.Equals(edge.FromEpisodeId, slot.FromEpisodeId, StringComparison.Ordinal) &&
            string.Equals(edge.ToEpisodeId, slot.ToEpisodeId, StringComparison.Ordinal) &&
            string.Equals(edge.OptionLabel ?? string.Empty, slot.Label, StringComparison.Ordinal));

    /// <summary>철도 위의 점에서 끌기를 시작한다 — 선은 그 점에서 나간다.</summary>
    private void BeginSlotDrag(GraphChoicePort slot, Control knob, string nodeId)
    {
        _connectingFrom = new GraphOutputPortProjection(
            $"choice:{slot.Slot}", GraphOutputPortKind.Choice, nodeId,
            slot.Label, slot.Slot, !slot.IsEmpty, ExecutionPort: null, slot);

        Point start = knob.TranslatePoint(new Point(PortRadius, PortRadius), GraphCanvas)
            ?? new Point(0, 0);

        _connectingLine = new Line
        {
            StartPoint = start,
            EndPoint = start,
            Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            StrokeThickness = 2,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 }
        };

        GraphCanvas.Children.Add(_connectingLine);
        SetHint("이을 에피소드 노드 위에서 놓으세요. 빈 곳에 놓으면 길이 걷힙니다.");
    }

    private void DrawRailBranch(
        string chapterId,
        DialogueNode source,
        ChapterEdge? edge,
        double trunkX,
        double y,
        double laneX,
        Rect? target,
        GraphChoicePort slot,
        bool mirrored)
    {
        // 나아가는 쪽 — 거울이면 왼쪽(-1)이다. 칩 · 스텁 · 레인이 모두 이 쪽으로 선다.
        double toward = mirrored ? -1 : 1;

        Control chip = SlotChip(slot, source.Id, mirrored);
        chip.Measure(Size.Infinity);

        _railVisuals.Add(RailLine(trunkX, y, trunkX + (toward * 10), y));
        Canvas.SetLeft(chip, mirrored ? trunkX - 14 - chip.DesiredSize.Width : trunkX + 14);
        Canvas.SetTop(chip, y - chip.DesiredSize.Height / 2);

        _railVisuals.Add(chip);
        double cursorX = mirrored
            ? trunkX - 14 - chip.DesiredSize.Width - 6
            : trunkX + 14 + chip.DesiredSize.Width + 6;

        // 아직 안 이은 칸은 <b>여기서 끝</b>이다 — 갈 곳이 없으니 선도 없고 "종료"도 아니다
        // (2026-09-16 소유자: "연결되지 않은 상태라면 '종료'라는 글자가 표시됩니다").
        if (edge is null)
        {
            return;
        }

        if (target is { } targetRect)
        {
            // 도착 포트로 모인다 (소유자 보고 — 접점만 있고 가지는 직접 붙던 결함).
            // 화살표는 포트의 몫이다 — 가지들은 접점까지만 간다.
            //
            // ⭐ 접점은 도착 카드의 <b>위 가운데</b>다 (D3 · 2026-10-29 소유자: "위에서 아래로"). 칩에서 나온
            //    가지는 카드 바깥의 제 레인으로 빠져 <b>아래로</b> 내려가고, 접점 줄에서 가로로 들어간다.
            (double junctionX, double junctionY) = ArrivalJunction(targetRect);

            double dropX = mirrored
                ? Math.Min(cursorX - RailLaneGap, laneX)
                : Math.Max(cursorX + RailLaneGap, laneX);

            // 레인은 사람이 모양을 고칠 수 있는 선이다(G3) — 간선이 그 모양을 진다.
            DrawLane(
                new RailKey(chapterId, edge.FromEpisodeId, edge.ToEpisodeId, edge.OptionLabel),
                edge.Shape,
                new GraphPosition(cursorX, y),
                toward,
                dropX,
                new GraphPosition(junctionX, junctionY));

            return;
        }

        // 카드 없음 스텁 — 간선은 있는데 도착 에피소드의 카드가 아직 이 판에 없다. 없는 노드로
        // 선을 긋는 대신 여기서 멈추고, 어디로 가는 길인지는 문구가 말한다.
        var pending = new TextBlock
        {
            Text = $"▢ {edge.ToEpisodeId} (카드 없음)",
            FontSize = 10,
            Opacity = 0.55,
            Foreground = RailChipBrush
        };
        ToolTip.SetTip(pending,
            $"도착 에피소드 '{edge.ToEpisodeId}'의 카드가 아직 이 판에 없습니다 — " +
            "프로젝트를 저장하고 다시 열면 빈 카드가 서고 여기로 이어집니다.");
        pending.Measure(Size.Infinity);

        _railVisuals.Add(RailLine(cursorX, y, cursorX + (toward * 18), y));
        Canvas.SetLeft(pending, mirrored ? cursorX - 22 - pending.DesiredSize.Width : cursorX + 22);
        Canvas.SetTop(pending, y - pending.DesiredSize.Height / 2);
        _railVisuals.Add(pending);
    }

    private static Line RailLine(double x1, double y1, double x2, double y2) => new()
    {
        StartPoint = new Point(x1, y1),
        EndPoint = new Point(x2, y2),
        Stroke = RailBrush,
        StrokeThickness = 1.4,
        IsHitTestVisible = false
    };

    /// <summary>아래로 드는 화살촉 — 도착 접점에서 카드 위 가운데로 든다.</summary>
    private static Polygon RailArrow(double x, double y)
    {
        var arrow = new Polygon
        {
            Fill = RailBrush,
            IsHitTestVisible = false,
            Points = [new Point(-4, 0), new Point(0, 7), new Point(4, 0)]
        };

        Canvas.SetLeft(arrow, x);
        Canvas.SetTop(arrow, y);
        return arrow;
    }

    /// <summary>
    /// 판 전환 (GB-1) — 활성 파일이 바뀌었으면 떠나는 판의 자리·배율을 기억하고,
    /// 새 판은 기억해 둔 자리로(처음이면 전체 보기로) 돌아간다.
    /// </summary>
    private void HandleBoardSwitch()
    {
        string? active = _session?.ActiveFileId;

        if (string.Equals(active, _viewedFileId, StringComparison.Ordinal))
        {
            return;
        }

        if (_viewedFileId is { } previous)
        {
            _boardViews[previous] = (GraphScroll.Offset, _zoom);
        }

        _viewedFileId = active;

        void Restore()
        {
            if (active is not null &&
                _boardViews.TryGetValue(active, out (Vector Offset, double Zoom) view))
            {
                SetZoom(view.Zoom);
                GraphScroll.Offset = view.Offset;
                RefreshMinimapViewport();
            }
            else if (active is not null)
            {
                FitFile(active); // 처음 여는 챕터는 그 챕터 박스가 들어오게 (PDF 9장 "그 챕터로 이동")
            }
            else
            {
                FitAll();
            }
        }

        if (GraphScroll.Viewport.Width > 0)
        {
            Restore();
        }
        else
        {
            // 앱 시작 직후 첫 레이아웃 전 — 뷰포트 크기가 잡힌 다음 턴에 자리를 잡는다.
            Avalonia.Threading.Dispatcher.UIThread.Post(Restore);
        }
    }

    /// <summary>좌표만 바뀌었을 때. projection을 다시 계산하되 컨트롤은 유지한다.</summary>
    internal void RefreshPositions()
    {
        if (_session is null)
        {
            return;
        }

        _projection = GraphProjectionBuilder.Build(
            _session.Project,
            _session.ExpandedFileIds,
            _session.Definition);

        // 카드를 옮겨 분기의 도착이 출발 카드를 넘었으면 포트가 반대쪽으로 선다 — 포트 줄은 카드 안에 지어지므로 카드부터 다시 짓는다.
        if (PortSidesChanged(_projection))
        {
            Rebuild();
            return;
        }

        PlaceVisuals(_projection);
    }

    /// <summary>지금 선 카드들의 포트 쪽(<see cref="GraphOutputPortProjection.OnLeft"/>)이 새 투영과 다른가.</summary>
    private bool PortSidesChanged(GraphProjection projection) =>
        projection.Items.OfType<ExpandedNodeProjection>().Any(node =>
            FindCard(node.NodeId) is { } card &&
            card.Ports.Any(port => node.OutputPorts.FirstOrDefault(fresh =>
                string.Equals(fresh.Key, port.Key, StringComparison.Ordinal)) is { } now && now.OnLeft != port.OnLeft));

    /// <summary>
    /// 끄는 동안 — 프로젝트는 그대로 두고 <b>화면의 자리만</b> 옮긴다 (D3 · §6-2).
    ///
    /// 예전에는 포인터가 움직일 때마다 <c>MoveNode</c>로 프로젝트에 적었다. 그때마다 투영 · 프레임 ·
    /// 레일 재계산에 더해 dirty 판정(프로젝트 전체 인코딩)과 챕터 그래프 재읽기가 돌았고, 되돌리기는
    /// 남지 않았다. 이제 지금 그린 투영의 자리만 바꿔 다시 놓고, 놓을 때 <see cref="CommitDrag"/>가 한 번 적는다.
    /// </summary>
    private void PreviewDrag()
    {
        if (_projection is null)
        {
            return;
        }

        // 자리는 절대값이라 몇 번을 덧입혀도 같다 — 끄는 도중에 판이 다시 그려져도 다음 움직임이 되살린다.
        _projection = new GraphProjection(
            _projection.Items
                .Select(item => item switch
                {
                    ExpandedNodeProjection node when _dragPositions.TryGetValue(node.NodeId, out Point at) =>
                        node with { Position = new GraphPosition(at.X, at.Y) },
                    CollapsedFileProjection file when string.Equals(file.FileId, _draggingProxyFileId, StringComparison.Ordinal) =>
                        file with
                        {
                            Position = new GraphPosition(
                                _proxyPositionStart.X + _proxyDragDelta.X,
                                _proxyPositionStart.Y + _proxyDragDelta.Y)
                        },
                    _ => item
                })
                .ToList(),
            _projection.Connections);

        PlaceVisuals(_projection);
    }

    /// <summary>
    /// 끌기를 놓는다 — 끄는 동안 화면에만 있던 자리를 <see cref="Vn.Authoring.Editing.ProjectEditor.MoveNodes"/> 한 번으로
    /// 적는다. 눌렀다 놓기만 했으면(선택) 적을 것이 없다.
    /// </summary>
    private void CommitDrag()
    {
        _draggingCard = null;
        _draggingGroup = false;
        _draggingProxyFileId = null;

        if (_dragPositions.Count == 0)
        {
            return;
        }

        var positions = _dragPositions.ToDictionary(
            pair => pair.Key, pair => (pair.Value.X, pair.Value.Y), StringComparer.Ordinal);
        _dragPositions.Clear();
        _proxyDragDelta = default;

        _session?.Editor.MoveNodes(positions);
    }

    /// <summary>
    /// 끌기를 걷는다 — 더블클릭의 첫 누름이 시작한 끌기다. 화면만 움직였으니 프로젝트 자리로 되돌려 놓는다.
    /// </summary>
    private void CancelDrag()
    {
        _draggingCard = null;
        _draggingGroup = false;
        _draggingProxyFileId = null;

        if (_dragPositions.Count == 0)
        {
            return;
        }

        _dragPositions.Clear();
        _proxyDragDelta = default;
        RefreshPositions();
    }

    /// <summary>투영의 자리대로 카드 · 접힌 판 · 간선 · 프레임 · 레일 · 미니맵을 놓는다.</summary>
    private void PlaceVisuals(GraphProjection projection)
    {
        foreach (ExpandedNodeProjection node in projection.Items.OfType<ExpandedNodeProjection>())
        {
            NodeCard? card = FindCard(node.NodeId);

            if (card is not null)
            {
                Canvas.SetLeft(card.Visual, node.Position.X);
                Canvas.SetTop(card.Visual, node.Position.Y);
            }
        }

        foreach (CollapsedFileProjection file in projection.Items.OfType<CollapsedFileProjection>())
        {
            FileProxyVisual? proxy = FindProxy(file.FileId);

            if (proxy is not null)
            {
                Canvas.SetLeft(proxy.Visual, file.Position.X);
                Canvas.SetTop(proxy.Visual, file.Position.Y);
            }
        }

        foreach (EdgeVisual edge in _edges)
        {
            GraphConnectionProjection? current = projection.Connections.FirstOrDefault(
                item => string.Equals(item.Key, edge.Connection.Key, StringComparison.Ordinal));

            if (current is not null)
            {
                edge.Connection = current;
                PositionEdge(edge);
            }
        }

        // 노드가 움직이면 그 노드를 감싸는 챕터 박스도 따라 늘고 준다.
        DrawChapterFrames();

        RefreshMinimap();
    }

    internal void HighlightSelection()
    {
        foreach (NodeCard card in _cards)
        {
            bool selected = string.Equals(card.NodeId, _session?.SelectedNodeId, StringComparison.Ordinal);
            bool grouped = _multiSelected.Contains(card.NodeId); // 범위 선택 무리 (W40)

            card.Visual.BorderBrush = selected || grouped
                ? new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))
                : new SolidColorBrush(card.Style.Border);

            card.Visual.BorderThickness = new Thickness(selected || grouped ? 2 : 1);
        }

        foreach (FileProxyVisual proxy in _proxies)
        {
            foreach (ProxyNodeRow row in proxy.Rows)
            {
                bool selected = string.Equals(row.NodeId, _session?.SelectedNodeId, StringComparison.Ordinal);
                row.Visual.Background = selected
                    ? new SolidColorBrush(Color.FromArgb(42, 37, 99, 235))
                    : Brushes.Transparent;
            }
        }

        // 선택이 실제로 바뀐 순간에만 따라간다 (GB-4) — 같은 선택의 재하이라이트
        // (편집·재빌드)에 화면이 끼어들면 스크롤해 둔 자리를 빼앗는다.
        if (!string.Equals(_session?.SelectedNodeId, _followedNodeId, StringComparison.Ordinal))
        {
            // 노드 삭제의 대체 선택(사라진 노드 → 첫 노드)은 사용자가 고른 게 아니다 —
            // 화면을 옮기지 않는다 (소유자 지시 2026-08-06).
            bool deletionFallback = _followedNodeId is not null &&
                _session?.Project.FindNode(_followedNodeId) is null;

            _followedNodeId = _session?.SelectedNodeId;

            if (!deletionFallback)
            {
                ScrollToSelected();
            }
        }
    }

    /// <summary>노드 종 하나의 시각 언어 — 등뼈(왼쪽 색 막대)·바탕·테두리·모서리·아이콘.</summary>
    private readonly record struct CardStyle(
        Color Accent, Color Background, Color Border, double Radius, string Icon);

    /// <summary>
    /// 종별 시각 체계 (2026-08-15 소유자 — "대본노드와 엑셀노드가 똑같이 생겨서 구분이 어렵다").
    /// 색 하나에 기대지 않고 세 채널을 겹친다: 등뼈 색 + 아이콘 + 카드 형태.
    /// 챕터의 에피소드만 <b>각진 미색 서류</b>다 — 작가의 자유 씬은 둥근 흰 원고(✎)라서
    /// 섞여 있어도 한눈에 갈린다. 미니맵·접힌 목록도 같은 언어를 쓴다.
    ///
    /// ⚠ <b>모양이 잠김을 뜻하지는 않는다</b>(2026-09-16 · R-E). 서류 모양이던 시절에는
    /// 본문이 실제로 읽기 전용이었지만 이제 대본은 어느 노드든 열린다 — 이 모양이 말하는
    /// 것은 <b>소속</b>뿐이다: 챕터의 에피소드인가, 아니면 자유 씬인가.
    /// </summary>
    private static CardStyle CardStyleFor(GraphNodeKind kind, bool chapterEpisode) => kind switch
    {
        GraphNodeKind.Dialogue when chapterEpisode => new CardStyle(
            Color.FromRgb(0xD9, 0x77, 0x06), Color.FromRgb(0xFB, 0xF6, 0xEA),
            Color.FromRgb(0xE3, 0xD5, 0xB7), 4, "📄"),
        GraphNodeKind.Dialogue => new CardStyle(
            Color.FromRgb(0x0D, 0x94, 0x88), Colors.White,
            Color.FromArgb(90, 128, 128, 128), 10, "✎"),
        _ => new CardStyle(
            Color.FromRgb(0x3B, 0x82, 0xF6), Color.FromRgb(0xEF, 0xF6, 0xFF),
            Color.FromArgb(90, 128, 128, 128), 10, "⚙")
    };

    /// <summary>챕터의 에피소드인가 — 소속을 묻는 것이지 잠금을 묻는 것이 아니다.</summary>
    private bool IsChapterEpisode(string nodeId) =>
        _session?.Project.FindNode(nodeId) is DialogueNode { MarkedEpisodeId: not null };

    private NodeCard BuildCard(ExpandedNodeProjection node)
    {
        CardStyle style = CardStyleFor(node.NodeKind, IsChapterEpisode(node.NodeId));

        var body = new StackPanel { Spacing = 0 };

        var titleIcon = new TextBlock
        {
            Text = style.Icon,
            FontSize = 11,
            Foreground = new SolidColorBrush(style.Accent),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleName = new TextBlock
        {
            Text = node.NodeName,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var title = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Height = TitleHeight,
            Margin = new Thickness(0, 0, 0, HeaderGap)
        };
        Grid.SetColumn(titleIcon, 0);
        Grid.SetColumn(titleName, 1);
        title.Children.Add(titleIcon);
        title.Children.Add(titleName);
        body.Children.Add(title);

        // 등뼈 — 카드 왼쪽의 종 색 막대. 선택 테두리(파랑 2px)와 채널이 겹치지 않는다.
        var spine = new Border
        {
            Width = 3,
            Background = new SolidColorBrush(style.Accent),
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 1, SpineInset - 3, 1),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(spine, 0);
        Grid.SetColumn(body, 1);
        layout.Children.Add(spine);
        layout.Children.Add(body);

        var card = new Border
        {
            Width = CardWidth,
            Padding = new Thickness(CardPadding),
            CornerRadius = new CornerRadius(style.Radius),
            Background = new SolidColorBrush(style.Background),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(style.Border),
            Child = layout,
            Tag = node.NodeId
        };

        // ⛔ <b>선택지 칸은 카드 안에 없다</b> (2026-09-16 소유자: "아래쪽으로 3개").
        //    카드 아래 철도 가지에 선다 — 그 자리가 선택지가 실제로 뻗어 나가는 자리이고,
        //    카드 안에 두면 이야기가 어디로 갈라지는지 한눈에 안 보인다.
        IReadOnlyList<GraphOutputPortProjection> inCard = node.OutputPorts
            .Where(port => port.Kind != GraphOutputPortKind.Choice)
            .ToList();

        var visual = new NodeCard(node.NodeId, node.NodeKind, card, inCard, style);

        for (int index = 0; index < inCard.Count; index++)
        {
            body.Children.Add(BuildPortRow(inCard[index], visual, index));
        }

        // 글 칸은 포트 줄들 <b>아래</b>에 선다 — 포트의 자리 계산(PortAnchor)이 머리 바로 밑부터 센다.
        if (node.NodeKind == GraphNodeKind.Dialogue)
        {
            body.Children.Add(BuildWritingBox(node.NodeId));
        }

        Canvas.SetLeft(card, node.Position.X);
        Canvas.SetTop(card, node.Position.Y);
        GraphCanvas.Children.Add(card);

        card.PointerPressed += (_, args) => OnCardPressed(visual, args);

        // ⛔ 카드 더블클릭 = "무대에서 본다"는 2026-10-29에 무대가 ked-story-compositor로 가며 사라졌다
        //    (그쪽에는 그래프가 없다 — 무대가 제 챕터 · 씬 선택기로 씬을 고른다).
        //    더블클릭은 이제 두 번 누른 것일 뿐이다(첫 누름이 시작한 끌기는 놓을 때 끝난다).

        return visual;
    }

    /// <summary>
    /// <b>카드 안 글 칸</b> (D4 · 2026-10-29, two-graphs.md §7 D4 — 소유자: *"노드 안에 텍스트 공간이 있고 그곳에
    /// 직접 글을 작성하는 것까지"*). 한 줄이 대사 한 줄이고 <c>화자: 대사</c>로 쓴다(화자가 없으면 지문).
    /// 보이는 글은 <see cref="WorkingDialoguePreview.ScenarioText"/>, 넣는 길은 <see cref="ApplyWriting"/>이다.
    ///
    /// ⚠ 칸 안의 손짓을 카드와 판이 가로채지 않는다 — 누르기는 TextBox가 받아 끝내므로 끌기가 시작되지
    ///   않고, 휠은 판을 확대하지 않고 칸이 스크롤한다(<see cref="InWritingBox"/>).
    /// </summary>
    private TextBox BuildWritingBox(string nodeId)
    {
        string shown = WorkingDialoguePreview.ScenarioText(_session!.Project, nodeId, _session.Definition);
        _writingShown[nodeId] = shown;

        var box = new TextBox
        {
            Text = shown,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Height = WritingBoxHeight,
            MinHeight = WritingBoxHeight,
            Margin = new Thickness(0, WritingBoxGap, 0, 0),
            Padding = new Thickness(4, 3),
            PlaceholderText = "화자: 대사 — 한 줄이 대사 한 줄",
            Tag = WritingTag + nodeId
        };
        ScrollViewer.SetVerticalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        _writingBoxes[nodeId] = box;

        box.GotFocus += (_, _) =>
        {
            if (!string.Equals(_session?.SelectedNodeId, nodeId, StringComparison.Ordinal))
            {
                _session?.Select(nodeId);
            }
        };

        // 넣을지는 <b>지금</b> 판단하고(미룬 뒤에는 되돌리기 따위로 판이 바뀌었을 수 있다), 넣기는 초점이 다른 곳에
        // <b>다 옮겨 간 뒤에</b> 한다 — 넣기가 판을 다시 그리면(줄이 늘고 줄면) 그 사이에 새로 초점을 받은 칸이
        // 사라진다. 다시 그린 뒤에는 Rebuild가 그 칸으로 초점을 되돌린다.
        box.LostFocus += (_, _) =>
        {
            if (PendingWriting(nodeId, box) is { } text)
            {
                Dispatcher.UIThread.Post(() => ApplyWriting(nodeId, box, text));
            }
        };

        box.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                // 넣지 않은 글을 버리고 마지막으로 넣은 글로 돌아간다.
                box.Text = _writingShown.GetValueOrDefault(nodeId, shown);
                MarkWriting(box, problem: null);
                args.Handled = true;
            }
        };

        return box;
    }

    /// <summary>
    /// 칸에 넣지 않은 글이 있으면 그 글, 없으면 null — 칸의 글이 마지막으로 보인 글과 같으면 칸을 눌렀다
    /// 나오기만 한 것이다(§5-8).
    /// </summary>
    private string? PendingWriting(string nodeId, TextBox box)
    {
        string text = box.Text ?? string.Empty;

        return string.Equals(text, _writingShown.GetValueOrDefault(nodeId), StringComparison.Ordinal) ? null : text;
    }

    /// <summary>
    /// 글 칸의 글을 넣는다 — <c>ApplyScenarioText</c>를 <b>엄격하게</b>(문제가 하나라도 있으면 아무것도 안 넣는다)
    /// 지움 확인 없이. 지운 줄은 은퇴하고 되돌리기 한 번이면 돌아오므로, 초점 이탈에 확인을 물리지 않는다.
    ///
    /// 못 넣으면 글을 칸에 그대로 두고 칸을 붉게 하고 사유를 칸의 풍선과 상태줄에 보인다. 다음 초점 이탈이
    /// 다시 넣는다. 엄격한 까닭: 넣은 뒤 칸은 모델의 모습으로 다시 그려지므로, 반쯤 넣으면 못 넣은 줄이
    /// 사람이 쓴 글에서 조용히 사라진다.
    /// </summary>
    private void ApplyWriting(string nodeId, TextBox box, string text)
    {
        if (_session is not { } session || session.Project.FindDialogue(nodeId) is null)
        {
            return;
        }

        UiGuard.Run(session, "글 넣기", () =>
        {
            ScenarioPasteOutcome outcome = session.Editor.ApplyScenarioText(
                nodeId, text, session.Definition, confirmDeletes: true, strict: true);

            if (!outcome.Applied)
            {
                IReadOnlyList<string> problems = outcome.Problems.Count > 0
                    ? outcome.Problems
                    : ["빈 글은 넣지 않습니다 — 줄을 모두 지우려면 노드를 지우세요."];

                MarkWriting(box, string.Join(Environment.NewLine, problems.Select(problem => $"• {problem}")));
                session.SetStatus($"글을 넣지 못했습니다 — {problems[0]} (글은 칸에 그대로 있습니다)");
                return;
            }

            // 판이 다시 그려지지 않았으면(바뀐 줄이 없다 — 글이 모양만 달랐다) 그 칸이 아직 살아 있다. 그때만
            // 기준을 칸의 글로 옮긴다 — 다시 그렸으면 새 칸이 모델의 글을 기준으로 이미 들고 있다.
            if (_writingBoxes.TryGetValue(nodeId, out TextBox? live) && ReferenceEquals(live, box))
            {
                _writingShown[nodeId] = text;
            }

            MarkWriting(box, problem: null);
            session.SetStatus($"글을 넣었습니다 — {outcome.Summary()}. 되돌리기 한 번이면 돌아갑니다.");
        });
    }

    /// <summary>
    /// 저장 전에 — 초점이 있는 글 칸의 넣지 않은 글을 먼저 넣는다(Ctrl+S도 넣는 때다, §7 D4).
    /// 셸의 저장이 부른다.
    /// </summary>
    internal void FlushWriting()
    {
        if (CaptureWritingFocus() is { Dirty: true } focus && _writingBoxes.TryGetValue(focus.NodeId, out TextBox? box))
        {
            ApplyWriting(focus.NodeId, box, focus.Text);
        }
    }

    /// <summary>
    /// 그 노드의 글 칸을 모델의 글로 다시 채운다 — 곁기둥이 줄을 고쳤을 때(판은 다시 안 그린다). 사람이 그 칸에서
    /// 쓰는 중이거나(초점) 넣지 않은 글이 있으면 건드리지 않는다 — 쓰던 글이 사라지면 안 된다.
    /// </summary>
    internal void RefreshWriting(string nodeId)
    {
        if (_session is null ||
            !_writingBoxes.TryGetValue(nodeId, out TextBox? box) ||
            box.IsFocused ||
            !string.Equals(box.Text ?? string.Empty, _writingShown.GetValueOrDefault(nodeId), StringComparison.Ordinal) ||
            _session.Project.FindDialogue(nodeId) is null)
        {
            return;
        }

        string shown = WorkingDialoguePreview.ScenarioText(_session.Project, nodeId, _session.Definition);
        _writingShown[nodeId] = shown;
        box.Text = shown;
    }

    /// <summary>
    /// 카드 머리나 빈 판을 누르면 글 칸의 초점을 놓는다 — 그래야 다른 노드를 고를 때 안 넣은 글이 먼저 들어간다
    /// (§7 D4). 카드와 판은 초점을 받지 않아서, 놓지 않으면 칸이 초점을 쥔 채 다른 노드가 골라진다. 넣기는
    /// 칸의 초점 이탈이 미뤄서 부르므로 이 누르기 처리기 안에서 판을 다시 그리지 않는다(§5-13).
    /// </summary>
    private void ReleaseWritingFocus()
    {
        if (CaptureWritingFocus() is not null)
        {
            WritingFocusSink.Focus();
        }
    }

    /// <summary>못 넣은 칸은 붉게, 사유는 풍선으로. <paramref name="problem"/>이 null이면 걷는다.</summary>
    private static void MarkWriting(TextBox box, string? problem)
    {
        if (problem is null)
        {
            box.ClearValue(BorderBrushProperty);
            ToolTip.SetTip(box, null);
            return;
        }

        box.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        ToolTip.SetTip(box, problem);
    }

    /// <summary>이 누름 · 휠이 글 칸 안에서 났는가(선택지 문구 칸도 글 칸이다).</summary>
    private static bool InWritingBox(object? source) =>
        (source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is not null;

    /// <summary>판을 다시 그리기 전, 초점이 있던 글 칸 — 노드 · 글 · 커서. 넣지 않은 글이면 <see cref="Dirty"/>다.</summary>
    private sealed record WritingFocus(string NodeId, string Text, int SelectionStart, int SelectionEnd, bool Dirty);

    private WritingFocus? CaptureWritingFocus()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox { Tag: string tag } box ||
            !tag.StartsWith(WritingTag, StringComparison.Ordinal))
        {
            return null;
        }

        string nodeId = tag[WritingTag.Length..];
        string text = box.Text ?? string.Empty;

        return new WritingFocus(
            nodeId, text, box.SelectionStart, box.SelectionEnd,
            Dirty: !string.Equals(text, _writingShown.GetValueOrDefault(nodeId), StringComparison.Ordinal));
    }

    /// <summary>
    /// 다시 그린 뒤 그 노드의 새 칸으로 초점과 커서를 돌려준다. 넣지 않은 글이 있었으면 그 글도 옮긴다 —
    /// 마지막으로 보인 글은 새 모델의 것이라, 다음 초점 이탈이 그 글을 넣는다.
    /// </summary>
    private void RestoreWritingFocus(WritingFocus? focus)
    {
        if (focus is null || !_writingBoxes.TryGetValue(focus.NodeId, out TextBox? box))
        {
            return;
        }

        if (focus.Dirty)
        {
            box.Text = focus.Text;
        }

        int length = box.Text?.Length ?? 0;
        box.Focus();
        box.SelectionStart = Math.Clamp(focus.SelectionStart, 0, length);
        box.SelectionEnd = Math.Clamp(focus.SelectionEnd, 0, length);
    }

    private FileProxyVisual BuildFileProxy(CollapsedFileProjection file)
    {
        var content = new StackPanel { Spacing = 0 };

        // 화면에 실제로 선 행 수 — 장면 머리글을 포함한다. 프록시 높이와 간선 끝점이
        // 이 수를 쓴다(노드 행 수만 쓰면 머리글만큼 짧아진다).
        int visualRows = 0;

        var headerDot = new Ellipse
        {
            Width = 11,
            Height = 11,
            Fill = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var headerText = new StackPanel
        {
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = file.FileName,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                new TextBlock
                {
                    Text = $"접힘 · {file.Nodes.Count}개 노드 · {file.RelativePath}",
                    FontSize = 9,
                    Opacity = 0.6,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };

        var header = new Grid
        {
            Height = ProxyHeaderHeight,
            Margin = new Thickness(10, 0),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(headerDot, 0);
        Grid.SetColumn(headerText, 1);
        header.Children.Add(headerDot);
        header.Children.Add(headerText);
        content.Children.Add(header);

        var rows = new List<ProxyNodeRow>();

        if (file.Nodes.Count == 0)
        {
            content.Children.Add(new Border
            {
                Height = ProxyRowHeight,
                Child = new TextBlock
                {
                    Text = "빈 파일",
                    Margin = new Thickness(22, 0),
                    FontSize = 10,
                    Opacity = 0.5,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
        }
        else
        {
            // ⛔ <b>판 → 장면 → 노드</b> (R6 S-3). 장면 머리글도 <b>한 행을 차지한다</b> —
            //    간선 끝점의 Y가 `머리글높이 + 행번호 × 행높이`라, 안 세면 그 아래 노드의
            //    간선이 한 칸씩 위로 붙는다. 세는 규칙의 주인은 `CollapsedSceneGroup.HasHeader`다.
            foreach (CollapsedSceneGroup scene in file.Scenes)
            {
                if (scene.HasHeader)
                {
                    content.Children.Add(BuildProxySceneHeader(scene));
                    visualRows++;
                }

                foreach (CollapsedNodeEntry entry in scene.Entries)
                {
                    ProxyNodeRow row = BuildProxyRow(entry, visualRows++);
                    rows.Add(row);
                    content.Children.Add(row.Visual);
                }
            }
        }

        var visual = new Border
        {
            Width = ProxyWidth,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
            BorderThickness = new Thickness(1),
            ClipToBounds = false,
            Child = content,
            Tag = file.FileId
        };

        Canvas.SetLeft(visual, file.Position.X);
        Canvas.SetTop(visual, file.Position.Y);
        GraphCanvas.Children.Add(visual);

        // 접힌 파일도 끌어 옮긴다 (W52). 노드 행은 자기 클릭을 Handled로 막으므로
        // 여기 오는 좌클릭은 헤더·테두리다. 빈 파일 프록시는 옮길 내용이 없다.
        visual.Cursor = new Cursor(StandardCursorType.SizeAll);
        visual.PointerPressed += (_, args) =>
        {
            if (_session is null ||
                !args.GetCurrentPoint(visual).Properties.IsLeftButtonPressed ||
                _session.Project.FindFile(file.FileId) is not { Nodes.Count: > 0 } storyFile)
            {
                return;
            }

            _draggingProxyFileId = file.FileId;
            _proxyDragStart = args.GetPosition(GraphCanvas);
            _proxyPositionStart = new GraphPosition(Canvas.GetLeft(visual), Canvas.GetTop(visual));
            _proxyStartPositions.Clear();

            foreach (StoryNode node in storyFile.Nodes)
            {
                _proxyStartPositions[node.Id] = new Point(node.Layout.X, node.Layout.Y);
            }

            args.Handled = true;
        };

        // 접힌 표를 더블클릭 = 펼치기 — 이름표 클릭(접기)의 짝이다.
        ToolTip.SetTip(visual, "더블클릭하면 이 챕터를 펼칩니다.");
        visual.DoubleTapped += (_, args) =>
        {
            args.Handled = true;
            CancelDrag(); // 둘째 누름이 시작한 끌기 상태를 걷는다
            _session?.SetFileExpanded(file.FileId, expanded: true);
        };

        return new FileProxyVisual(file.FileId, visual, rows, Math.Max(1, visualRows));
    }

    /// <summary>
    /// 접힌 판 안의 <b>장면 머리글</b> 한 줄 (R6 S-3).
    ///
    /// ⚠ 누를 수 없다 — 장면은 담는 자리이고, 고르는 것은 노드다
    /// (<c>docs/plans/R6-explorer.md</c> §1). 여기서 누르면 판 끌기가 된다.
    /// </summary>
    private static Border BuildProxySceneHeader(CollapsedSceneGroup scene) =>
        new()
        {
            Height = ProxyRowHeight,
            Background = new SolidColorBrush(Color.FromArgb(26, 107, 114, 128)),
            Child = new TextBlock
            {
                Text = scene.SceneId.Length == 0 ? scene.DisplayName : "▸ " + scene.DisplayName,
                Margin = new Thickness(14, 0),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
                [ToolTip.TipProperty] = scene.SceneId.Length == 0
                    ? "이 챕터의 진행에 안 실리는 노드들입니다 — 장면 경계가 없습니다."
                    : $"장면 '{scene.DisplayName}' — 같은 장면끼리 저장·롤백 구간을 공유합니다."
            }
        };

    private ProxyNodeRow BuildProxyRow(CollapsedNodeEntry entry, int index)
    {
        IBrush dotBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

        var input = new Ellipse
        {
            Width = ProxyPortRadius * 2,
            Height = ProxyPortRadius * 2,
            Margin = new Thickness(-ProxyPortRadius, 0, 7, 0),
            Fill = entry.IncomingCount > 0 ? dotBrush : Brushes.Transparent,
            Stroke = dotBrush,
            StrokeThickness = 1.5,
            VerticalAlignment = VerticalAlignment.Center
        };

        var name = new TextBlock
        {
            // 접힌 목록에도 종 아이콘 — 카드와 같은 시각 언어.
            Text = $"{CardStyleFor(entry.NodeKind, IsChapterEpisode(entry.NodeId)).Icon} {entry.NodeName}",
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var output = new Ellipse
        {
            Width = ProxyPortRadius * 2,
            Height = ProxyPortRadius * 2,
            Margin = new Thickness(7, 0, -ProxyPortRadius, 0),
            Fill = entry.OutgoingCount > 0 ? dotBrush : Brushes.Transparent,
            Stroke = dotBrush,
            StrokeThickness = 1.5,
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid
        {
            Height = ProxyRowHeight,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };
        Grid.SetColumn(input, 0);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(output, 2);
        grid.Children.Add(input);
        grid.Children.Add(name);
        grid.Children.Add(output);

        var row = new Border
        {
            Height = ProxyRowHeight,
            Padding = new Thickness(0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(35, 107, 114, 128)),
            BorderThickness = new Thickness(0, index == 0 ? 1 : 0, 0, 1),
            Child = grid,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = entry.NodeId
        };

        row.PointerPressed += (_, args) =>
        {
            _session?.Select(entry.NodeId);
            HighlightSelection();
            args.Handled = true;
        };

        return new ProxyNodeRow(entry.NodeId, index, row);
    }

    // ── 선택지 슬롯 (R7 P-2) ────────────────────────────────────────────────

    /// <summary>
    /// <b>선택지 줄 하나의 자리</b> (H2 · 2026-10-29 소유자: "선택지 문구의 순서를 유지해"). 이은 줄은 간선이 채우고,
    /// 적어 둔 줄은 문구를 든다. 목록의 <c>null</c>은 아직 아무것도 안 적은 빈 칸이다.
    /// </summary>
    private abstract record ChoiceRow
    {
        public sealed record Joined : ChoiceRow;

        public sealed record Written(string Text) : ChoiceRow;
    }

    /// <summary>
    /// <b>에피소드마다 선택지 줄의 차례</b> — 사람이 둔 자리 그대로다. 열쇠는 <c>{챕터}/{에피소드}</c>.
    ///
    /// ⭐ 잇거나 끊어도 줄은 제자리다 — 이으면 그 줄이 「이은 줄」이 되고(간선은 이은 줄들 사이 같은 자리에 끼워
    /// 넣는다), 끊으면 문구를 든 「적어 둔 줄」로 돌아온다. ⛔ 예전(`_pending`)에는 적어 둔 문구만 빈 칸들 사이의 차례로
    /// 들고, 이은 줄은 늘 위로 모였다 — 아래 칸을 먼저 이으면 그 줄이 위로 올라가 적은 순서가 뒤바뀌었다.
    ///
    /// ⚠ 저장하지 않는다. 간선은 도착이 있어야 존재하므로 도착 없는 문구를 담을 칸이 챕터에 없다(R6의 빈 장면과
    /// 같은 부류). 문구 자체는 적는 즉시 챕터의 선택지 사전에 배운다. 다시 열면 이은 줄이 간선 순서대로 위에 선다 —
    /// 이은 줄끼리의 차례는 간선 순서와 늘 같으므로 잃는 것이 없다.
    /// </summary>
    private readonly Dictionary<string, List<ChoiceRow?>> _rows = new(StringComparer.Ordinal);

    private static string RowsKey(string chapterId, string episodeId) => chapterId + "/" + episodeId;

    /// <summary>
    /// 그 에피소드의 줄 차례 — 이은 줄 자리 수를 지금 간선 수에 맞춘 뒤 돌려준다. 다른 화면(챕터 그래프)이나
    /// 되돌리기가 길을 더하거나 걷었을 수 있다: 남는 이은 줄은 끝에서부터 빈 칸이 되고, 모자라면 마지막 이은 줄
    /// 뒤의 빈 칸(없으면 끝)에 선다 — 간선은 끝에 붙으므로 이은 줄끼리의 차례가 간선 순서와 맞는다.
    /// </summary>
    private List<ChoiceRow?> RowsOf(string chapterId, string episodeId, int joinedCount)
    {
        string key = RowsKey(chapterId, episodeId);

        if (!_rows.TryGetValue(key, out List<ChoiceRow?>? rows))
        {
            rows = [];
        }

        int joinedRows = rows.Count(row => row is ChoiceRow.Joined);

        for (int index = rows.Count - 1; index >= 0 && joinedRows > joinedCount; index--)
        {
            if (rows[index] is ChoiceRow.Joined)
            {
                rows[index] = null;
                joinedRows--;
            }
        }

        while (joinedRows < joinedCount)
        {
            int last = rows.FindLastIndex(row => row is ChoiceRow.Joined);
            int blank = rows.FindIndex(last + 1, row => row is null);

            if (blank >= 0)
            {
                rows[blank] = new ChoiceRow.Joined();
            }
            else
            {
                rows.Add(new ChoiceRow.Joined());
            }

            joinedRows++;
        }

        Keep(key, rows);
        return rows;
    }

    /// <summary>꼬리의 빈 칸은 들고 있을 이유가 없다 — 다 비면 열쇠째 걷는다.</summary>
    private void Keep(string key, List<ChoiceRow?> rows)
    {
        while (rows.Count > 0 && rows[^1] is null)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        if (rows.Count == 0)
        {
            _rows.Remove(key);
        }
        else
        {
            _rows[key] = rows;
        }
    }

    /// <summary>그 줄 자리를 바꾼다 — 목록이 짧으면 빈 칸으로 늘린다.</summary>
    private void SetRow(GraphChoicePort slot, ChoiceRow? row)
    {
        string key = RowsKey(slot.ChapterId, slot.FromEpisodeId);
        List<ChoiceRow?> rows = _rows.TryGetValue(key, out List<ChoiceRow?>? existing) ? existing : [];

        while (rows.Count <= slot.Slot)
        {
            rows.Add(null);
        }

        rows[slot.Slot] = row;
        Keep(key, rows);
    }

    private string? PendingText(GraphChoicePort slot) =>
        slot.IsEmpty &&
        _rows.TryGetValue(RowsKey(slot.ChapterId, slot.FromEpisodeId), out List<ChoiceRow?>? rows) &&
        slot.Slot < rows.Count &&
        rows[slot.Slot] is ChoiceRow.Written written
            ? written.Text
            : null;

    private static string SlotKey(GraphChoicePort choice) => choice.FromEpisodeId + "#" + choice.Slot;

    /// <summary>그 칸이 지금 지고 있는 문구 — 이은 것은 간선에서, 안 이은 것은 적어 둔 데서.</summary>
    private string LabelOf(GraphChoicePort choice) =>
        choice.IsEmpty ? PendingText(choice) ?? string.Empty : choice.Label;

    /// <summary>
    /// 살아난 칸인가 — <b>문구를 적으면</b> 살아나고 비우면 도로 꺼진다 (2026-09-17 소유자).
    ///
    /// ⚠ 점을 눌러 켜는 길도 잠깐 있었는데 소유자가 물렸다 — 켜기와 적기가 갈라져 있으면
    /// 손짓이 둘이 되고, 켜 두고 안 적은 칸이 <b>뜻 없이</b> 붉게 남는다.
    /// </summary>
    private bool IsArmed(GraphChoicePort choice) =>
        choice.IsEmpty && PendingText(choice) is not null;

    /// <summary>
    /// <b>자동 길 자리</b>인가 — 세 칸이 전부 꺼져 있을 때의 첫 칸 (R7 §2 ③-a).
    ///
    /// 자동 길은 문구가 없어야 하고(<c>AutoEdgeHasChoiceLabel</c>) 그 에피소드의 <b>유일한
    /// 간선</b>이어야 한다(<c>AutoEdgeHasSiblings</c>) — 그래서 이 조건이 정확히 자동 길이
    /// 설 수 있는 유일한 자리다. <b>화면이 규칙을 미리 지킨다.</b>
    /// </summary>
    private bool IsAutoSlot(GraphChoicePort choice, IReadOnlyList<GraphChoicePort> slots) =>
        choice.IsAuto ||
        (choice.IsEmpty && choice.Slot == 0 &&
         slots.All(other => other.IsEmpty && PendingText(other) is null));

    /// <summary>
    /// 살아 있는 칸인가 — 이어졌거나, 켰거나, 자동 길 자리다.
    /// </summary>
    private bool IsLive(GraphChoicePort choice, IReadOnlyList<GraphChoicePort> slots) =>
        !choice.IsEmpty || IsArmed(choice) || IsAutoSlot(choice, slots);

    /// <summary>지금 문구를 적고 있는 칸 — <b>테스트의 손잡이</b>이자 실제 입력칸이다.</summary>
    internal TextBox? SlotLabelBox { get; private set; }

    /// <summary>안 이은 활성 칸의 붉은 점.</summary>
    internal static readonly IBrush SlotLive = new SolidColorBrush(Color.FromRgb(0xD9, 0x3A, 0x3A));

    /// <summary>이어진 칸의 초록 점.</summary>
    internal static readonly IBrush SlotJoined = new SolidColorBrush(Color.FromRgb(0x1F, 0x9D, 0x55));

    /// <summary>아직 못 쓰는 칸의 회색 테두리.</summary>
    internal static readonly IBrush SlotAsleep = new SolidColorBrush(Color.FromArgb(120, 150, 150, 150));

    /// <summary>
    /// 그 칸의 색 — <b>세 자리를 한눈에</b> 가른다 (2026-09-16 소유자).
    ///
    /// | 텅 빈 회색 | 아직 꺼져 있다 — 눌러서 켠다 |
    /// | 붉은색 | 켜졌다 — 끌어다 놓을 수 있다 |
    /// | 초록색 | 이어졌다 |
    /// </summary>
    private IBrush SlotDot(GraphChoicePort choice, IReadOnlyList<GraphChoicePort> slots) =>
        !choice.IsEmpty ? SlotJoined : IsLive(choice, slots) ? SlotLive : SlotAsleep;

    private Control BuildPortRow(GraphOutputPortProjection port, NodeCard card, int index)
    {
        bool branch = port.Kind == GraphOutputPortKind.ExecutionBranch;

        var label = new TextBlock
        {
            Text = port.Label,
            FontSize = 10,
            Opacity = port.Kind == GraphOutputPortKind.ExecutionDefault ? 0.6 : 1,
            FontWeight = branch ? FontWeight.SemiBold : FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = port.OnLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right
        };

        IBrush portBrush = branch ? BranchPalette.Accent(port.PaletteIndex) : Brushes.DimGray;

        // ⚠ 갈래가 아닐 때 Foreground를 null로 <b>적으면</b> 글이 사라진다 — 테마 값을 덮는다.
        if (branch)
        {
            label.Foreground = portBrush;
        }

        var knob = new Ellipse
        {
            Width = PortRadius * 2,
            Height = PortRadius * 2,
            // 점의 가운데가 카드 가장자리에 걸린다 — 이 카드보다 왼쪽 카드로 가는 포트는 왼쪽 가장자리다(2026-10-29 소유자).
            Margin = port.OnLeft
                ? new Thickness(-CardPadding - SpineInset - PortRadius, 0, 6, 0)
                : new Thickness(6, 0, -CardPadding - PortRadius, 0),
            Fill = port.IsConnected ? portBrush : Brushes.Transparent,
            Stroke = portBrush,
            StrokeThickness = 2,
            VerticalAlignment = VerticalAlignment.Center,
            // 왼쪽 칸은 음수 여백이 넓어 열 폭이 0이 된다 — 가운데로 밀리지 않게 가장자리에 붙인다.
            HorizontalAlignment = port.OnLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        knob.PointerPressed += (_, args) => OnPortPressed(port, card, index, args);

        var row = new Grid
        {
            Height = PortRowHeight,
            ColumnDefinitions = new ColumnDefinitions(port.OnLeft ? "Auto,*" : "*,Auto")
        };

        Grid.SetColumn(label, port.OnLeft ? 1 : 0);
        Grid.SetColumn(knob, port.OnLeft ? 0 : 1);
        row.Children.Add(label);
        row.Children.Add(knob);

        return row;
    }

    /// <summary>지금 문구를 적고 있는 칸. 없으면 null.</summary>
    private string? _editingSlot;

    /// <summary>
    /// 문구 입력칸 — Enter 확정 · Esc 취소 · 딴 데를 누르면 취소.
    ///
    /// 이미 이어진 길이면 <b>그 간선의 문구를 고치고</b>, 아직 안 이었으면 적어만 둔다.
    /// 어느 쪽이든 <b>챕터의 선택지 사전에 배운다</b> — 문구의 주인은 챕터다(v9).
    /// </summary>
    private Control SlotEditor(GraphChoicePort choice)
    {
        var box = new TextBox
        {
            Text = LabelOf(choice),
            FontSize = 10,
            Padding = new Thickness(3, 0),
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center
        };

        void Commit(bool keep)
        {
            _editingSlot = null;
            SlotLabelBox = null;

            string wanted = box.Text?.Trim() ?? string.Empty;

            if (keep && !string.Equals(wanted, LabelOf(choice), StringComparison.Ordinal))
            {
                Relabel(choice, wanted);
            }

            Rebuild();
        }

        box.KeyDown += (_, args) => UiGuard.Run(_session, "선택지 문구", () =>
        {
            if (args.Key is Key.Enter or Key.Escape)
            {
                args.Handled = true;
                Commit(args.Key == Key.Enter);
            }
        });

        box.LostFocus += (_, _) => UiGuard.Run(_session, "선택지 문구", () =>
        {
            if (string.Equals(_editingSlot, SlotKey(choice), StringComparison.Ordinal))
            {
                Commit(keep: false);
            }
        });

        SlotLabelBox = box;
        return box;
    }

    private void Relabel(GraphChoicePort choice, string wanted)
    {
        if (_session is null)
        {
            return;
        }

        if (choice.IsEmpty)
        {
            // 담을 간선이 아직 없다 — 화면이 들고, 문구만 챕터의 어휘집에 남긴다.
            // ⚠ <b>비우면 도로 꺼진다</b> (2026-09-17 소유자) — 문구가 곧 그 칸의 생사다.
            SetRow(choice, wanted.Length > 0 ? new ChoiceRow.Written(wanted) : null);

            if (wanted.Length > 0)
            {
                // ⚠ <b>이미 있으면 안 배운다.</b> 사전은 <b>어휘집</b>이라 같은 말이 두 번 들어갈
                //    자리가 없고, `AddChoiceLabel`은 [선택지 사전] 화면을 위해 중복을 거절한다 —
                //    여기서 그대로 부르면 <b>흔한 문구를 다시 쓰는 것만으로 실패한다</b>
                //    (2026-09-16에 테스트가 잡았다).
                if (!_session.Editor.FindChapter(choice.ChapterId)!.ChoiceOptions.Any(option =>
                        string.Equals(option.Text, wanted, StringComparison.Ordinal)))
                {
                    _session.Editor.AddChoiceLabel(choice.ChapterId, wanted);
                }
            }

            return;
        }

        // ⚠ 간선의 신원은 (출발, 도착, 문구)다 — 고치기 <b>전</b>의 문구로 찾아야 한다.
        _session.Editor.UpdateEdge(
            choice.ChapterId, choice.FromEpisodeId, choice.ToEpisodeId!,
            matchOptionLabel: choice.Label, optionLabel: wanted);
    }

    private void DrawEdges()
    {
        if (_projection is null)
        {
            return;
        }

        foreach (GraphConnectionProjection connection in _projection.Connections)
        {
            AddEdge(connection);
        }
    }

    private void AddEdge(GraphConnectionProjection connection)
    {
        if (!TryEndpointAnchor(connection.Source, out _) ||
            !TryEndpointAnchor(connection.Target, out _))
        {
            return;
        }

        IBrush stroke = ConnectionBrush(connection);
        var path = new ShapePath
        {
            Stroke = stroke,
            StrokeThickness = 2
        };

        bool showLabel = connection.Kind == GraphConnectionKind.ExecutionBranch;
        var label = new Border
        {
            Padding = new Thickness(5, 1),
            CornerRadius = new CornerRadius(3),
            Background = showLabel ? stroke : Brushes.Transparent,
            IsVisible = showLabel,
            Child = new TextBlock
            {
                Text = connection.Label,
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            }
        };

        var edge = new EdgeVisual(connection, path, label);

        // 좌클릭 = 선택(하단에 정보), 우클릭 = 삭제 (W47 — 툴바의 간선 삭제 버튼을 대신한다).
        void OnEdgePressed(PointerPressedEventArgs args, Control source)
        {
            SelectEdge(edge);

            if (args.GetCurrentPoint(source).Properties.IsRightButtonPressed)
            {
                DeleteSelectedEdge();
            }

            args.Handled = true;
        }

        path.PointerPressed += (_, args) => OnEdgePressed(args, path);
        label.PointerPressed += (_, args) => OnEdgePressed(args, label);

        GraphCanvas.Children.Insert(0, path);
        GraphCanvas.Children.Add(label);
        _edges.Add(edge);

        PositionEdge(edge);
    }

    private void PositionEdge(EdgeVisual edge)
    {
        if (!TryEndpointAnchor(edge.Connection.Source, out Point from) ||
            !TryEndpointAnchor(edge.Connection.Target, out Point to))
        {
            edge.Path.IsVisible = false;
            edge.Label.IsVisible = false;
            return;
        }

        // 왼쪽 포트에서 나간 선은 도착 카드의 <b>오른쪽</b>으로 든다 — 왼쪽으로 들면 도착 카드를 가로지른다.
        bool leftward = FromLeftPort(edge.Connection);

        if (leftward &&
            edge.Connection.Target.Kind == GraphEndpointKind.ExpandedNodeInput &&
            FindCard(edge.Connection.Target.NodeId) is { } targetCard)
        {
            to = new Point(Canvas.GetLeft(targetCard.Visual) + CardWidth, to.Y);
        }

        edge.Path.IsVisible = true;
        edge.Label.IsVisible = edge.Connection.Kind == GraphConnectionKind.ExecutionBranch;

        // 사람이 고친 모양대로 긋는다(G3) — 끄는 중이면 그 미리보기로.
        ShapedEdge shaped = EdgeShapeRouter.Execution(
            new GraphPosition(from.X, from.Y),
            new GraphPosition(to.X, to.Y),
            leftward,
            ReferenceEquals(edge, _selectedEdge) && _shapePreview is { } preview ? preview : ExitShapeOf(edge));

        edge.Path.Data = ShapedGeometry(shaped);

        // 이름표는 선의 가운데 — 직각이면 가운데 세로 마디, 곡선이면 굽이의 한가운데다.
        GraphPosition labelPoint = MiddleOf(shaped);
        edge.Label.Measure(Size.Infinity);

        if (ReferenceEquals(edge, _selectedEdge))
        {
            PlaceShapeOverlay(shaped, ExitShapeOf(edge));

            // 손잡이도 선의 가운데에 선다 — 고른 동안 이름표는 손잡이 위로 비켜 가려지지 않는다.
            if (shaped.Handle is not null)
            {
                labelPoint = labelPoint with { Y = labelPoint.Y - (edge.Label.DesiredSize.Height / 2) - 10 };
            }
        }

        Canvas.SetLeft(edge.Label, labelPoint.X - (edge.Label.DesiredSize.Width / 2));
        Canvas.SetTop(edge.Label, labelPoint.Y - (edge.Label.DesiredSize.Height / 2));
    }

    /// <summary>그 선이 카드 <b>왼쪽</b> 포트에서 나가는가.</summary>
    private bool FromLeftPort(GraphConnectionProjection connection) =>
        connection.Source.Kind == GraphEndpointKind.ExpandedNodeOutput &&
        FindCard(connection.Source.NodeId) is { } card &&
        card.PortIndex(connection.Source.PortKey) is var index and >= 0 &&
        card.Ports[index].OnLeft;

    private bool TryEndpointAnchor(GraphEndpointProjection endpoint, out Point point)
    {
        switch (endpoint.Kind)
        {
            case GraphEndpointKind.ExpandedNodeOutput:
            {
                NodeCard? card = FindCard(endpoint.NodeId);
                int portIndex = card?.PortIndex(endpoint.PortKey) ?? -1;

                if (card is not null && portIndex >= 0)
                {
                    point = PortAnchor(card, portIndex);
                    return true;
                }

                break;
            }

            case GraphEndpointKind.ExpandedNodeInput:
            {
                NodeCard? card = FindCard(endpoint.NodeId);

                if (card is not null)
                {
                    point = InputAnchor(card);
                    return true;
                }

                break;
            }

            case GraphEndpointKind.CollapsedFileNodeOutput:
            case GraphEndpointKind.CollapsedFileNodeInput:
            {
                FileProxyVisual? proxy = FindProxy(endpoint.FileId);

                if (proxy is not null && endpoint.ProxyRowIndex is { } rowIndex)
                {
                    point = ProxyRowAnchor(
                        proxy,
                        rowIndex,
                        output: endpoint.Kind == GraphEndpointKind.CollapsedFileNodeOutput);
                    return true;
                }

                break;
            }
        }

        point = default;
        return false;
    }

    /// <summary>펼쳐진 노드 카드의 출력 포트 좌표 — 왼쪽 포트(<see cref="GraphOutputPortProjection.OnLeft"/>)면 왼쪽 가장자리다.</summary>
    private static Point PortAnchor(NodeCard card, int portIndex)
    {
        double x = Canvas.GetLeft(card.Visual) + (card.Ports[portIndex].OnLeft ? 0 : CardWidth);
        double y = Canvas.GetTop(card.Visual)
            + CardBorder + CardPadding + HeaderHeight
            + (portIndex * PortRowHeight) + (PortRowHeight / 2);

        return new Point(x, y);
    }

    private static Point InputAnchor(NodeCard card)
    {
        return new Point(
            Canvas.GetLeft(card.Visual),
            Canvas.GetTop(card.Visual) + CardBorder + CardPadding + (TitleHeight / 2));
    }

    private static Point ProxyRowAnchor(FileProxyVisual proxy, int rowIndex, bool output)
    {
        double x = Canvas.GetLeft(proxy.Visual) + (output ? ProxyWidth : 0);
        double y = Canvas.GetTop(proxy.Visual)
            + ProxyHeaderHeight
            + (rowIndex * ProxyRowHeight)
            + (ProxyRowHeight / 2);

        return new Point(x, y);
    }

    // ── 그래프 내비게이션 (W40): 줌·팬·미니맵 ─────────────────────────────

    /// <summary>휠 — 누른 키와 무관하게 배율이다 (2026-08-18 팀장 미팅에서 Ctrl 요구가 빠졌다).</summary>
    private void OnGraphWheel(object? sender, PointerWheelEventArgs args)
    {
        if (InWritingBox(args.Source))
        {
            return;   // 글 칸 위의 휠은 칸이 스크롤한다(D4)
        }

        double factor = args.Delta.Y > 0 ? 1.15 : 1 / 1.15;
        ApplyZoom(_zoom * factor, args.GetPosition(GraphScroll));
        args.Handled = true;
    }

    /// <summary>배율 적용 — anchor(뷰포트 좌표) 아래의 내용이 그 자리에 남게 오프셋을 맞춘다.</summary>
    private void ApplyZoom(double zoom, Point? anchor)
    {
        Point pivot = anchor ?? new Point(
            GraphScroll.Viewport.Width / 2, GraphScroll.Viewport.Height / 2);

        // 지금 pivot 아래에 있는 캔버스 좌표.
        var content = new Point(
            (GraphScroll.Offset.X + pivot.X) / _zoom,
            (GraphScroll.Offset.Y + pivot.Y) / _zoom);

        SetZoom(zoom);
        GraphScroll.Offset = new Vector(
            Math.Max(0, (content.X * _zoom) - pivot.X),
            Math.Max(0, (content.Y * _zoom) - pivot.Y));
        RefreshMinimapViewport();
    }

    /// <summary>배율의 변환·표시만 — 오프셋은 호출자가 자기 규칙(고정점·중앙)으로 정한다.</summary>
    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ZoomHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ZoomText.Text = $"{Math.Round(_zoom * 100)}%";
        ZoomHost.UpdateLayout(); // 새 크기를 알아야 오프셋 상한이 맞는다
    }

    /// <summary>
    /// 특정 챕터(판)로 이동 — 그 판의 노드들이 여백을 두고 들어오는 배율과 위치.
    /// 왼쪽 챕터 목록을 클릭했을 때 여러 챕터 박스 사이에서 그 챕터를 눈앞에 가져온다.
    /// </summary>
    private void FitFile(string fileId)
    {
        if (GraphScroll.Viewport.Width <= 0 || GraphScroll.Viewport.Height <= 0)
        {
            return;
        }

        Rect? bounds = null;

        foreach (ExpandedNodeProjection node in (_projection?.Items ?? [])
                     .OfType<ExpandedNodeProjection>()
                     .Where(item => string.Equals(item.FileId, fileId, StringComparison.Ordinal)))
        {
            if (FindCard(node.NodeId) is not { } card)
            {
                continue;
            }

            var rect = new Rect(node.Position.X, node.Position.Y, CardWidth, CardHeightOf(card));
            bounds = bounds is { } current ? current.Union(rect) : rect;
        }

        if (bounds is not { } area)
        {
            FitAll(); // 빈 판이거나 접혀 있다 — 전체 보기가 차선이다
            return;
        }

        area = area.Inflate(80);
        SetZoom(Math.Min(
            1.0,
            Math.Min(GraphScroll.Viewport.Width / area.Width, GraphScroll.Viewport.Height / area.Height)));
        GraphScroll.Offset = new Vector(
            Math.Max(0, (area.Center.X * _zoom) - (GraphScroll.Viewport.Width / 2)),
            Math.Max(0, (area.Center.Y * _zoom) - (GraphScroll.Viewport.Height / 2)));
        RefreshMinimapViewport();
    }

    /// <summary>전체 보기 (GB-4) — 노드·프록시 전부가 여백을 두고 들어오는 배율과 위치.</summary>
    private void FitAll()
    {
        if (GraphScroll.Viewport.Width <= 0 || GraphScroll.Viewport.Height <= 0)
        {
            return; // 레이아웃 전 — 맞출 화면이 아직 없다
        }

        Rect? bounds = null;

        foreach (NodeCard card in _cards)
        {
            var rect = new Rect(
                Canvas.GetLeft(card.Visual), Canvas.GetTop(card.Visual),
                CardWidth, CardHeightOf(card));
            bounds = bounds is { } current ? current.Union(rect) : rect;
        }

        foreach (FileProxyVisual proxy in _proxies)
        {
            var rect = new Rect(
                Canvas.GetLeft(proxy.Visual), Canvas.GetTop(proxy.Visual),
                ProxyWidth, ProxyHeaderHeight + (proxy.VisualRowCount * ProxyRowHeight));
            bounds = bounds is { } current ? current.Union(rect) : rect;
        }

        if (bounds is not { } all)
        {
            ApplyZoom(1, null); // 빈 판 — 기본 배율로 돌아간다
            GraphScroll.Offset = default;
            return;
        }

        all = all.Inflate(80); // 가장자리 여백
        SetZoom(Math.Min(
            1.0, // 노드가 적다고 확대까지 하지는 않는다
            Math.Min(GraphScroll.Viewport.Width / all.Width, GraphScroll.Viewport.Height / all.Height)));
        GraphScroll.Offset = new Vector(
            Math.Max(0, (all.Center.X * _zoom) - (GraphScroll.Viewport.Width / 2)),
            Math.Max(0, (all.Center.Y * _zoom) - (GraphScroll.Viewport.Height / 2)));
        RefreshMinimapViewport();
    }

    /// <summary>
    /// 선택 노드로 화면 이동 (GB-4) — 이미 보이는 노드에는 끼어들지 않는다.
    /// 접힌 파일 안의 노드면 그 프록시로 간다.
    /// </summary>
    private void ScrollToSelected()
    {
        if (_session?.SelectedNodeId is not { } nodeId)
        {
            return;
        }

        if (TargetVisualOf(nodeId) is not { } target)
        {
            return;
        }

        double left = Canvas.GetLeft(target);
        double top = Canvas.GetTop(target);
        double width = target.Bounds.Width > 0 ? target.Bounds.Width : CardWidth;
        double height = target.Bounds.Height > 0 ? target.Bounds.Height : 160;

        var viewRect = new Rect(
            GraphScroll.Offset.X / _zoom,
            GraphScroll.Offset.Y / _zoom,
            GraphScroll.Viewport.Width / _zoom,
            GraphScroll.Viewport.Height / _zoom);

        if (viewRect.Intersects(new Rect(left, top, width, height)))
        {
            return;
        }

        Center(target);
    }

    /// <summary>카드가 있으면 카드, 접힌 판 안이면 그 프록시 — 눈이 실제로 가야 할 것.</summary>
    private Control? TargetVisualOf(string nodeId) =>
        FindCard(nodeId)?.Visual
        ?? _proxies.FirstOrDefault(proxy => proxy.Rows.Any(row =>
            string.Equals(row.NodeId, nodeId, StringComparison.Ordinal)))?.Visual;

    private void Center(Control target)
    {
        double left = Canvas.GetLeft(target);
        double top = Canvas.GetTop(target);
        double width = target.Bounds.Width > 0 ? target.Bounds.Width : CardWidth;
        double height = target.Bounds.Height > 0 ? target.Bounds.Height : 160;

        GraphScroll.Offset = new Vector(
            Math.Max(0, ((left + (width / 2)) * _zoom) - (GraphScroll.Viewport.Width / 2)),
            Math.Max(0, ((top + (height / 2)) * _zoom) - (GraphScroll.Viewport.Height / 2)));
    }

    private void OnGraphPanPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(GraphScroll).Properties.IsMiddleButtonPressed)
        {
            return;
        }

        _panning = true;
        _panStart = args.GetPosition(GraphScroll);
        _panStartOffset = GraphScroll.Offset;
        args.Pointer.Capture(GraphScroll);
        args.Handled = true;
    }

    private void OnGraphPanMoved(object? sender, PointerEventArgs args)
    {
        if (!_panning)
        {
            return;
        }

        Point now = args.GetPosition(GraphScroll);
        GraphScroll.Offset = new Vector(
            Math.Max(0, _panStartOffset.X - (now.X - _panStart.X)),
            Math.Max(0, _panStartOffset.Y - (now.Y - _panStart.Y)));
        args.Handled = true;
    }

    private void OnGraphPanReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_panning)
        {
            _panning = false;
            args.Pointer.Capture(null);
            args.Handled = true;
        }
    }

    /// <summary>미니맵 전체 다시 그리기 — 카드·프록시를 축소 사각형으로, 위에 뷰포트 틀.</summary>
    private void RefreshMinimap()
    {
        MinimapCanvas.Children.Clear();

        foreach (NodeCard card in _cards)
        {
            AddMinimapRect(
                Canvas.GetLeft(card.Visual),
                Canvas.GetTop(card.Visual),
                CardWidth,
                CardHeightOf(card),
                card.Style.Accent); // 카드와 같은 시각 언어 — 미니맵에서도 에피소드·낙서가 갈린다.
        }

        foreach (FileProxyVisual proxy in _proxies)
        {
            AddMinimapRect(
                Canvas.GetLeft(proxy.Visual),
                Canvas.GetTop(proxy.Visual),
                ProxyWidth,
                ProxyHeaderHeight + (proxy.VisualRowCount * ProxyRowHeight),
                Color.FromRgb(0x6B, 0x72, 0x80));
        }

        _minimapViewport = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            StrokeThickness = 1,
            Fill = new SolidColorBrush(Color.FromArgb(20, 37, 99, 235)),
            IsHitTestVisible = false
        };
        MinimapCanvas.Children.Add(_minimapViewport);
        RefreshMinimapViewport();
    }

    private void AddMinimapRect(double x, double y, double width, double height, Color color)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(2, width * (MinimapWidth / CanvasWidth)),
            Height = Math.Max(2, height * (MinimapHeight / CanvasHeight)),
            Fill = new SolidColorBrush(Color.FromArgb(170, color.R, color.G, color.B)),
            RadiusX = 1,
            RadiusY = 1,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rect, x * (MinimapWidth / CanvasWidth));
        Canvas.SetTop(rect, y * (MinimapHeight / CanvasHeight));
        MinimapCanvas.Children.Add(rect);
    }

    /// <summary>뷰포트 틀만 옮긴다 — 스크롤·줌마다 불리므로 전체를 다시 그리지 않는다.</summary>
    private void RefreshMinimapViewport()
    {
        if (_minimapViewport is null)
        {
            return;
        }

        double scaleX = MinimapWidth / CanvasWidth;
        double scaleY = MinimapHeight / CanvasHeight;

        double left = GraphScroll.Offset.X / _zoom * scaleX;
        double top = GraphScroll.Offset.Y / _zoom * scaleY;
        double width = Math.Min(MinimapWidth, GraphScroll.Viewport.Width / _zoom * scaleX);
        double height = Math.Min(MinimapHeight, GraphScroll.Viewport.Height / _zoom * scaleY);

        Canvas.SetLeft(_minimapViewport, Math.Clamp(left, 0, MinimapWidth));
        Canvas.SetTop(_minimapViewport, Math.Clamp(top, 0, MinimapHeight));
        _minimapViewport.Width = width;
        _minimapViewport.Height = height;
    }

    private void OnMinimapPressed(object? sender, PointerPressedEventArgs args)
    {
        _minimapDragging = true;
        MoveViewportTo(args.GetPosition(MinimapCanvas));
        args.Pointer.Capture(MinimapCanvas);
        args.Handled = true;
    }

    private void OnMinimapMoved(object? sender, PointerEventArgs args)
    {
        if (_minimapDragging)
        {
            MoveViewportTo(args.GetPosition(MinimapCanvas));
            args.Handled = true;
        }
    }

    /// <summary>미니맵의 한 점이 뷰포트 중앙에 오도록 스크롤을 옮긴다.</summary>
    private void MoveViewportTo(Point minimapPoint)
    {
        double contentX = minimapPoint.X / (MinimapWidth / CanvasWidth);
        double contentY = minimapPoint.Y / (MinimapHeight / CanvasHeight);

        GraphScroll.Offset = new Vector(
            Math.Max(0, (contentX * _zoom) - (GraphScroll.Viewport.Width / 2)),
            Math.Max(0, (contentY * _zoom) - (GraphScroll.Viewport.Height / 2)));
    }

    private static double CardHeightOf(NodeCard card)
    {
        return card.Visual.Bounds.Height > 0
            ? card.Visual.Bounds.Height
            : HeaderHeight + (card.Ports.Count * PortRowHeight) + ((CardPadding + CardBorder) * 2) +
              (card.NodeKind == GraphNodeKind.Dialogue ? WritingBoxGap + WritingBoxHeight : 0);
    }

    // ── 조작 ────────────────────────────────────────────────────────────────

    private void OnCardPressed(NodeCard card, PointerPressedEventArgs args)
    {
        ReleaseWritingFocus();
        _session?.Select(card.NodeId);

        // 범위 선택된 카드를 잡으면 무리가 함께 움직인다 (W40).
        if (_multiSelected.Contains(card.NodeId) && _multiSelected.Count > 1)
        {
            _draggingGroup = true;
            _groupDragStart = args.GetPosition(GraphCanvas);
            _groupStartPositions.Clear();

            foreach (NodeCard member in _cards)
            {
                if (_multiSelected.Contains(member.NodeId))
                {
                    _groupStartPositions[member.NodeId] = new Point(
                        Canvas.GetLeft(member.Visual), Canvas.GetTop(member.Visual));
                }
            }

            HighlightSelection();
            args.Handled = true;
            return;
        }

        _multiSelected.Clear(); // 무리 밖 카드를 잡으면 단일 선택으로 돌아간다
        HighlightSelection();

        _draggingCard = card;
        Point position = args.GetPosition(GraphCanvas);
        _dragOffset = new Point(
            position.X - Canvas.GetLeft(card.Visual),
            position.Y - Canvas.GetTop(card.Visual));

        args.Handled = true;
    }

    private void OnPortPressed(
        GraphOutputPortProjection port,
        NodeCard card,
        int index,
        PointerPressedEventArgs args)
    {
        _connectingFrom = port;

        Point start = PortAnchor(card, index);
        _connectingLine = new Line
        {
            StartPoint = start,
            EndPoint = start,
            Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            StrokeThickness = 2,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 }
        };

        GraphCanvas.Children.Add(_connectingLine);
        // 공급 포트 안내는 2026-08-22에 사라졌다 — 끌 수 있는 포트가 실행 출구뿐이다.
        SetHint("연결할 실행 노드 또는 접힌 파일의 실행 노드 행 위에서 놓으세요. " +
            "빈 곳에 놓으면 실행 연결이 끊어집니다.");

        args.Handled = true;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (args.Source is not Canvas)
        {
            return;
        }

        SelectEdge(null);
        ReleaseWritingFocus();

        // 빈 곳 좌클릭 = 범위 선택 시작 (W40). 이전 무리는 새 범위가 대신한다.
        if (args.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
        {
            _multiSelected.Clear();
            HighlightSelection();

            _rubberStart = args.GetPosition(GraphCanvas);
            _rubberBand = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                StrokeThickness = 1,
                StrokeDashArray = new AvaloniaList<double> { 4, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(28, 37, 99, 235)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_rubberBand, _rubberStart.X);
            Canvas.SetTop(_rubberBand, _rubberStart.Y);
            GraphCanvas.Children.Add(_rubberBand);
            args.Pointer.Capture(GraphCanvas);
        }
    }

    private Rect RubberRect(Point current)
    {
        return new Rect(
            new Point(Math.Min(_rubberStart.X, current.X), Math.Min(_rubberStart.Y, current.Y)),
            new Point(Math.Max(_rubberStart.X, current.X), Math.Max(_rubberStart.Y, current.Y)));
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs args)
    {
        Point position = args.GetPosition(GraphCanvas);

        if (_rubberBand is not null)
        {
            Rect rect = RubberRect(position);
            Canvas.SetLeft(_rubberBand, rect.X);
            Canvas.SetTop(_rubberBand, rect.Y);
            _rubberBand.Width = rect.Width;
            _rubberBand.Height = rect.Height;
            return;
        }

        if (_connectingLine is not null)
        {
            _connectingLine.EndPoint = position;
            return;
        }

        if (_session is null)
        {
            return;
        }

        if (_draggingGroup)
        {
            double deltaX = position.X - _groupDragStart.X;
            double deltaY = position.Y - _groupDragStart.Y;

            foreach ((string nodeId, Point start) in _groupStartPositions)
            {
                _dragPositions[nodeId] = new Point(
                    ClampNodeX(start.X + deltaX),
                    ClampNodeY(start.Y + deltaY));
            }

            PreviewDrag();
            return;
        }

        if (_draggingProxyFileId is not null)
        {
            double deltaX = position.X - _proxyDragStart.X;
            double deltaY = position.Y - _proxyDragStart.Y;

            foreach ((string nodeId, Point start) in _proxyStartPositions)
            {
                _dragPositions[nodeId] = new Point(
                    ClampNodeX(start.X + deltaX),
                    ClampNodeY(start.Y + deltaY));
            }

            _proxyDragDelta = new Vector(deltaX, deltaY);
            PreviewDrag();
            return;
        }

        if (_draggingCard is null)
        {
            return;
        }

        _dragPositions[_draggingCard.NodeId] = new Point(
            ClampNodeX(position.X - _dragOffset.X),
            ClampNodeY(position.Y - _dragOffset.Y));
        PreviewDrag();
    }

    // 판 경계 클램프 (GB-4) — 카드가 스크롤이 닿지 않는 바깥으로 끌려 나가지 않는다.
    private static double ClampNodeX(double x) => Math.Clamp(x, 0, CanvasWidth - CardWidth);

    private static double ClampNodeY(double y) => Math.Clamp(y, 0, CanvasHeight - 240);

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        CommitDrag();

        // 범위 선택 확정 (W40) — 사각형에 걸친 카드 전부가 무리가 된다.
        if (_rubberBand is not null)
        {
            Rect rect = RubberRect(args.GetPosition(GraphCanvas));
            GraphCanvas.Children.Remove(_rubberBand);
            _rubberBand = null;
            args.Pointer.Capture(null);

            _multiSelected.Clear();

            foreach (NodeCard card in _cards)
            {
                var bounds = new Rect(
                    Canvas.GetLeft(card.Visual),
                    Canvas.GetTop(card.Visual),
                    CardWidth,
                    CardHeightOf(card));

                if (rect.Intersects(bounds))
                {
                    _multiSelected.Add(card.NodeId);
                }
            }

            HighlightSelection();
            return;
        }

        if (_connectingFrom is null)
        {
            return;
        }

        GraphOutputPortProjection port = _connectingFrom;
        _connectingFrom = null;

        if (_connectingLine is not null)
        {
            GraphCanvas.Children.Remove(_connectingLine);
            _connectingLine = null;
        }

        // 끌기가 끝났으면 할 말도 끝났다. 예전에는 여기서 <b>조작법 설명으로 되돌렸는데</b>
        // (2026-08-24 폐기), 그것이 곧 늘 서 있던 그 문구였다.
        SetHint(null);

        string? dropped = NodeAt(args.GetPosition(GraphCanvas));

        string? target = dropped is null || string.Equals(dropped, port.NodeId, StringComparison.Ordinal)
            ? null
            : dropped;

        if (port.ChoicePort is { } choice)
        {
            // ⚠ <b>UiGuard로 받는다.</b> 규칙 위반은 편집기가 예외로 거절하는데(신원 겹침 ·
            //    없는 에피소드), 놓임 처리기는 Avalonia가 부르는 자리라 그대로 터진다.
            //    거절은 상태줄로 가야 한다 — 이 저장소의 모든 편집 창구가 그 규약을 쓴다.
            UiGuard.Run(_session, "선택지 잇기", () => JoinChoice(choice, SlotsOf(port.NodeId), target));
            return;
        }

        if (port.ExecutionPort is not null)
        {
            _session?.Editor.SetExitTarget(port.ExecutionPort, target);
        }
    }

    /// <summary>
    /// <b>선택지를 잇거나 끊는다</b> (R7 P-3) — 이 손짓이 고치는 것은 <b>챕터 간선</b>이다.
    ///
    /// ⛔ 연출 층에 사본을 두지 않는다(<c>docs/plans/R7.md</c> §2 ①). 그래서 여기서 그은 길이
    /// [챕터 그래프]에도 그대로 보이고, 내보내기·도달성 증명·V1/V2 진단이 <b>하나도 안 바뀐
    /// 채</b> 그 값을 본다.
    /// </summary>
    private void JoinChoice(GraphChoicePort choice, IReadOnlyList<GraphChoicePort> slots, string? targetNodeId)
    {
        if (_session is null)
        {
            return;
        }

        if (targetNodeId is null)
        {
            Unjoin(choice);
            return;
        }

        if (EpisodeAt(targetNodeId) is not var (chapterId, episodeId))
        {
            _session.SetStatus("에피소드가 아닌 노드에는 선택지를 놓을 수 없습니다.");
            return;
        }

        // ⚠ 간선은 챕터 안의 길이다 — 두 챕터를 이을 수 없다. 조용히 무시하면 사람은
        //   손이 미끄러진 줄 안다.
        if (!string.Equals(chapterId, choice.ChapterId, StringComparison.Ordinal))
        {
            _session.SetStatus(
                $"'{chapterId}'는 다른 챕터입니다 — 선택지는 챕터 안에서만 잇습니다.");
            return;
        }

        string label = LabelOf(choice);

        // 문구가 없는 채로 살아 있는 칸은 <b>자동 길</b> 자리다 (§2 ③-a). 이미 이어진 자동 길을 옮기면
        // 자동 길 그대로다 — 그래서 아래의 장면 검사도 받는다.
        bool auto = choice.IsEmpty ? label.Length == 0 : choice.IsAuto;

        if (string.Equals(choice.ToEpisodeId, episodeId, StringComparison.Ordinal))
        {
            return;   // 이어져 있던 그 카드에 도로 놓았다 — 바뀐 것이 없다(§5-8)
        }

        // ⚠ <b>자동 길은 장면을 못 넘는다</b> — 장면 경계는 실제로 `Commit → Exit → Enter`라,
        //   묻지 않고 넘어가면 안 된다(`AutoEdgeCrossesScene`). 관문에서야 알면 되돌려야 하니
        //   여기서 먼저 말하고, <b>대신 할 수 있는 것</b>을 함께 말한다.
        //
        // ⚠ 장면ID를 아무 데도 안 적은 챕터에서는 에피소드마다 제 장면이라(`__scene_*`)
        //   자동 길이 아예 설 수 없다. 그것도 규칙 그대로다 — 숨기지 않고 말한다.
        if (auto && SceneOf(choice.ChapterId, choice.FromEpisodeId) is { } from &&
            SceneOf(choice.ChapterId, episodeId) is { } to &&
            !string.Equals(from, to, StringComparison.Ordinal))
        {
            _session.SetStatus(
                "자동 길은 같은 장면 안에서만 이어집니다 — 두 에피소드를 같은 장면에 두거나, " +
                "문구를 적어 선택지로 이어 주세요.");

            return;
        }

        if (!choice.IsEmpty)
        {
            // 이미 이어진 칸을 다른 노드로 옮긴 것이다 — 그 간선의 도착만 바꾼다(D3 · two-graphs.md §6-3).
            // ⛔ 예전에는 옛 길을 걷고 새로 그었다: 간선이 목록 끝으로 가 선택지 순서(= 런타임
            //    OptionIndex, §5-3)가 바뀌고, 관문 · 스탯 변화를 잃고, 되돌리기도 두 칸이었다.
            _session.Editor.SetEdgeRoute(
                choice.ChapterId, choice.FromEpisodeId, choice.ToEpisodeId!, choice.Label,
                episodeId, label.Length > 0 ? label : null);
        }
        else
        {
            // 그 줄 자리 그대로 잇는다 (H2 · 2026-10-29 소유자: "선택지 문구의 순서를 유지해") — 간선은 이 줄 아래의 첫
            // 이은 줄 앞에 끼워 넣는다. 그래야 이은 줄끼리의 차례(= 간선 순서 = 런타임 OptionIndex, §5-3)가 화면과 같다.
            // ⛔ 예전에는 간선을 끝에 붙이고 이은 줄을 위로 모았다 — 아래 칸을 먼저 이으면 그 줄이 위로 올라갔다.
            // ⚠ 문구 없는 길(진행)은 화면에서만 맨 아래로 내려 서므로 기준으로 삼지 않는다 — 간선 순서의 엉뚱한 자리다.
            ChapterEdge? before = slots
                .Skip(choice.Slot + 1)
                .Where(slot => !slot.IsEmpty && slot.Label.Length > 0)
                .Select(EdgeOf)
                .FirstOrDefault(edge => edge is not null);

            // 줄 차례를 먼저 바꾼다 — 간선이 서는 순간 판이 다시 그려지므로 그때 이미 맞아 있어야 한다.
            ChoiceRow? was = PendingText(choice) is { } written ? new ChoiceRow.Written(written) : null;
            SetRow(choice, new ChoiceRow.Joined());

            try
            {
                _session.Editor.AddEdge(
                    choice.ChapterId, choice.FromEpisodeId, episodeId,
                    optionLabel: label.Length > 0 ? label : null,
                    auto: auto,
                    before: before);
            }
            catch
            {
                SetRow(choice, was);
                throw;
            }
        }

        _session.SetStatus(auto
            ? $"'{choice.FromEpisodeId}' → '{episodeId}' 자동으로 이었습니다(문구 없이 지나갑니다)."
            : $"'{choice.FromEpisodeId}' → '{episodeId}' 선택지 '{label}'을 이었습니다.");

        Rebuild();
    }

    /// <summary>
    /// 빈 곳에 놓았다 — 이어져 있던 길을 걷는다.
    ///
    /// ⚠ <b>문구는 안 잃는다</b> (2026-09-16 소유자: "연결을 끊더라도 사라지지 않고 유지").
    /// 길을 끊는 것과 무엇을 물을지 정한 것은 다른 일이다 — 문구는 켜진 칸으로 돌아와 다시 이을 수 있다.
    /// 줄은 <b>제자리</b>에 남는다(H2 — 예전에는 적어 둔 칸들의 맨 앞으로 갔다).
    ///
    /// ⚠ 자동 길은 문구가 없으니 칸도 꺼진 채로 돌아온다.
    /// </summary>
    private void Unjoin(GraphChoicePort choice)
    {
        if (choice.IsEmpty)
        {
            return;
        }

        // 줄 차례를 먼저 바꾼다 — 간선이 걷히는 순간 판이 다시 그려진다.
        SetRow(choice, choice.Label.Length > 0 ? new ChoiceRow.Written(choice.Label) : null);

        try
        {
            _session!.Editor.RemoveEdge(
                choice.ChapterId, choice.FromEpisodeId, choice.ToEpisodeId!, choice.Label);
        }
        catch
        {
            SetRow(choice, new ChoiceRow.Joined());
            throw;
        }

        _session!.SetStatus(
            $"'{choice.FromEpisodeId}' → '{choice.ToEpisodeId}' 길을 걷었습니다." +
            (choice.Label.Length > 0 ? $" 문구 '{choice.Label}'은 칸에 남겨 뒀습니다." : string.Empty));

        Rebuild();
    }

    /// <summary>그 에피소드의 장면 — 빈 칸이면 <c>__scene_{에피소드}</c>로 퇴화한다.</summary>
    private string? SceneOf(string chapterId, string episodeId) =>
        _session?.Editor.FindChapter(chapterId)?.Episodes
            .FirstOrDefault(episode =>
                string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
            ?.EffectiveSceneId;

    /// <summary>
    /// 그 노드가 선 자리 — (챕터, 에피소드). 에피소드가 아니면 null.
    ///
    /// ⚠ 판 이름이 챕터고 표식이 먼저고 없으면 이름이다 — 장면 묶기·슬롯 투영이
    /// 쓰는 그 규칙이다(새 규약을 만들지 않는다).
    /// </summary>
    private (string ChapterId, string EpisodeId)? EpisodeAt(string nodeId)
    {
        if (_session?.Project.FindFileContainingNode(nodeId) is not { } file ||
            _session.Project.FindNode(nodeId) is not DialogueNode dialogue ||
            _session.Editor.FindChapter(file.Name) is not { } chapter)
        {
            return null;
        }

        string episodeId = EpisodeNaming.EpisodeIdOf(dialogue);

        return chapter.Episodes.Any(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
            ? (chapter.ChapterId, episodeId)
            : null;
    }

    /// <summary>그 자리의 노드 — 펼친 카드이거나 접힌 판의 노드 행. 없으면 null.</summary>
    private string? NodeAt(Point point)
    {
        foreach (NodeCard card in _cards)
        {
            double left = Canvas.GetLeft(card.Visual);
            double top = Canvas.GetTop(card.Visual);
            double height = CardHeightOf(card);

            if (point.X >= left && point.X <= left + CardWidth &&
                point.Y >= top && point.Y <= top + height)
            {
                return card.NodeId;
            }
        }

        foreach (FileProxyVisual proxy in _proxies)
        {
            double left = Canvas.GetLeft(proxy.Visual);
            double top = Canvas.GetTop(proxy.Visual);

            if (point.X < left || point.X > left + ProxyWidth || point.Y < top + ProxyHeaderHeight)
            {
                continue;
            }

            int rowIndex = (int)((point.Y - top - ProxyHeaderHeight) / ProxyRowHeight);

            // ⚠ 행 번호는 <b>장면 머리글을 포함한</b> 화면 순서다 (R6 S-3) — 자리로 세지 않고
            //    각 행이 든 제 번호로 찾는다. 머리글을 누르면 아무 노드도 안 맞는다(맞다).
            if (proxy.Rows.FirstOrDefault(row => row.Index == rowIndex) is { } hit)
            {
                return hit.NodeId;
            }
        }

        return null;
    }

    private void SelectEdge(EdgeVisual? edge)
    {
        foreach (EdgeVisual item in _edges)
        {
            item.Path.StrokeThickness = 2;
        }

        _selectedEdge = edge;
        HideShapeOverlay();

        // 선은 한 번에 하나만 고른다 — 실행 선을 고르면(또는 빈 곳을 누르면) 고른 레인은 놓는다.
        if (_selectedRail is not null)
        {
            _selectedRail = null;
            DrawChapterRails();
        }

        if (edge is null)
        {
            return;
        }

        edge.Path.StrokeThickness = 4;
        PositionEdge(edge);

        string sourceName = FindNodeName(edge.Connection.SourceNodeId) ?? edge.Connection.SourceNodeId;
        string targetName = FindNodeName(edge.Connection.TargetNodeId) ?? edge.Connection.TargetNodeId;
        string kind = edge.Connection.Kind == GraphConnectionKind.ExecutionBranch
            ? $"조건 '{edge.Connection.Label}'"
            : "기본 출구";

        SetHint($"{sourceName} — {kind} → {targetName} · 우클릭으로 삭제");
    }

    /// <summary>
    /// 아래 띠에 한 줄 — <b>지금 벌어지는 일만</b> 말한다 (2026-08-24 소유자: "포트를 끌어
    /// 놓으면 연결, 빈곳좌클릭 드래그 범위선택 이런 설명문구도 제거").
    ///
    /// 늘 서 있던 조작법 설명은 폐지됐다. 손이 이미 아는 것을 매번 다시 일러 주면, 정작
    /// 무언가를 말해야 할 때 그 줄이 눈에 안 띈다 — 검증 보고에서 동기화 문구를 지운 것과
    /// 같은 선이다(같은 날).
    ///
    /// <b>null이면 띠가 접힌다.</b> 할 말 없는 빈 칸이 서 있으면 그것도 소음이다.
    /// 여닫는 문을 여기 하나로 둔 이유이기도 하다 — 문구만 지우고 띠를 안 접는 자리가
    /// 생기면 빈 줄이 남는다.
    /// </summary>
    private void SetHint(string? text)
    {
        HintText.Text = text ?? string.Empty;
        HintBar.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void DeleteSelectedEdge()
    {
        if (_selectedEdge is null || _session is null)
        {
            return;
        }

        if (_selectedEdge.Connection.ExecutionPort is { } exit)
        {
            // 분기 선을 지우는 것은 「분기 지우기」다 — 표식과, 분기가 다녀가던 에피소드를 함께 걷는다
            // (2026-10-29 소유자). 그 밖의 출구는 잇던 선만 끊는다.
            if (exit.Kind == ExitPortKind.Detour &&
                exit.BranchOpenLineId is { } lineId &&
                exit.TargetNodeId is { } targetId &&
                _session.Editor.RemoveBranchMarker(exit.NodeId, lineId, targetId) is { } removal)
            {
                _session.SetStatus(removal.Describe());
                return;
            }

            _session.Editor.SetExitTarget(exit, null);
        }
    }

    private void AddDialogueNode()
    {
        if (_session is null)
        {
            return;
        }

        if (_session.ActiveFileId is not { } fileId)
        {
            _session.SetStatus("새 노드를 추가할 StoryFile이 없습니다.");
            return;
        }

        StoryFile activeFile = _session.ActiveFile
            ?? throw new InvalidOperationException($"현재 StoryFile '{fileId}'를 찾을 수 없습니다.");

        // 지금 보고 있는 곳에 만든다 (GB-4) — 큰 판에서 "만들었는데 안 보임"을 없앤다.
        // 연속 생성은 계단으로 밀려 정확히 겹치지 않는다.
        int fileNodeCount = activeFile.Nodes.Count;
        double stagger = (fileNodeCount % 5) * 26;
        double x = ClampNodeX(
            ((GraphScroll.Offset.X + (GraphScroll.Viewport.Width / 2)) / _zoom) - (CardWidth / 2) + stagger);
        double y = ClampNodeY(
            ((GraphScroll.Offset.Y + (GraphScroll.Viewport.Height / 2)) / _zoom) - 90 + stagger);

        // 이 판에서 사람이 만드는 노드는 대사뿐이다.
        _session.Select(_session.Editor.AddDialogueNode(fileId, x, y).Id);
    }

    private void DeleteSelectedNode()
    {
        if (_session?.SelectedNodeId is not { } nodeId)
        {
            return;
        }

        // ⛔ 설정 노드를 지우지 않게 막던 자리는 2026-10-29(L6)에 걷혔다 — 설정 노드는 이제
        //    연출 그래프에 카드가 없어(`GraphProjectionBuilder.IsHiddenPlumbing`) 고를 수 없다.

        // ⭐ <b>챕터 판의 대사 카드는 에피소드다</b> (R7 P-6 · 결정 ⑤). 그러니 그 카드를
        //    지우는 것은 <b>에피소드를 지우는 일</b>이고, 챕터 그래프와 같은 길을
        //    지나야 한다 — 2026-09-18까지는 카드만 걷어 <b>대본 없는 반쪽 에피소드</b>가
        //    남았다(만들기는 합쳐 놓고 지우기는 안 합친 자리였다).
        //
        // ⚠ <b>여기서만 묻는다.</b> 챕터 그래프의 [에피소드 삭제]는 이름이 이미 무엇을
        //    지우는지 말하지만, 여기서 사람은 <b>카드를 지운다고 생각하고</b> 누른다 —
        //    지워지는 것은 카드 하나가 아니라 에피소드와 그 간선이다.
        if (_session.Project.FindNode(nodeId) is DialogueNode card &&
            EpisodeNaming.ChapterOf(_session.Project, card) is { } chapter)
        {
            string episodeId = EpisodeNaming.EpisodeIdOf(card);

            DeleteConfirmButton = ConfirmFlyout.Show(
                DeleteNodeButton,
                _session,
                $"'{episodeId}'은 챕터 '{chapter.ChapterId}'의 에피소드입니다. " +
                "간선·픽스처 참조까지 함께 지웁니다 — 되돌리기(Ctrl+Z)로 돌아옵니다.",
                "에피소드 지우기",
                () => DeleteEpisodeOfCard(chapter.ChapterId, episodeId),
                closed: () => DeleteConfirmButton = null);

            return;
        }

        // 작가의 낙서판 — 에피소드가 아니라 카드다. 되돌리기 한 번에 돌아온다.
        _session.Editor.RemoveNode(nodeId);
    }

    /// <summary>
    /// 지금 떠 있는 확인 단추 — <b>테스트의 손잡이</b>다. 플라이아웃의 팝업은 창 밖에 살아
    /// 나무를 타고 내려가 찾을 수 없다.
    /// </summary>
    internal Button? DeleteConfirmButton { get; private set; }

    private void DeleteEpisodeOfCard(string chapterId, string episodeId)
    {
        EpisodeDeleter.Result result = EpisodeDeleter.Delete(_session!.Editor, chapterId, episodeId);

        _session.SetStatus(result.Ok ? result.Describe(episodeId) : result.Failure!);
    }

    private NodeCard? FindCard(string nodeId)
    {
        return _cards.FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));
    }

    private FileProxyVisual? FindProxy(string fileId)
    {
        return _proxies.FirstOrDefault(item => string.Equals(item.FileId, fileId, StringComparison.Ordinal));
    }

    private string? FindNodeName(string nodeId)
    {
        ExpandedNodeProjection? expanded = _projection?.Items
            .OfType<ExpandedNodeProjection>()
            .FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));

        if (expanded is not null)
        {
            return expanded.NodeName;
        }

        return _projection?.Items
            .OfType<CollapsedFileProjection>()
            .SelectMany(file => file.Nodes)
            .FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal))
            ?.NodeName;
    }

    private static IBrush ConnectionBrush(GraphConnectionProjection connection)
    {
        return connection.Kind switch
        {
            GraphConnectionKind.ExecutionBranch => BranchPalette.Accent(connection.PaletteIndex),
            _ => new SolidColorBrush(Color.FromArgb(150, 100, 100, 100))
        };
    }

    private static Point ToPoint(GraphPosition position) => new(position.X, position.Y);

    private sealed class NodeCard
    {
        public NodeCard(
            string nodeId,
            GraphNodeKind nodeKind,
            Border visual,
            IReadOnlyList<GraphOutputPortProjection> ports,
            CardStyle style)
        {
            NodeId = nodeId;
            NodeKind = nodeKind;
            Visual = visual;
            Ports = ports;
            Style = style;
        }

        public string NodeId { get; }
        public GraphNodeKind NodeKind { get; }
        public Border Visual { get; }
        public IReadOnlyList<GraphOutputPortProjection> Ports { get; }
        public CardStyle Style { get; }

        public int PortIndex(string? key)
        {
            return key is null
                ? -1
                : Ports.ToList().FindIndex(port => string.Equals(port.Key, key, StringComparison.Ordinal));
        }
    }

    private sealed class FileProxyVisual
    {
        public FileProxyVisual(
            string fileId, Border visual, IReadOnlyList<ProxyNodeRow> rows, int visualRowCount)
        {
            FileId = fileId;
            Visual = visual;
            Rows = rows;
            VisualRowCount = visualRowCount;
        }

        public string FileId { get; }
        public Border Visual { get; }
        public IReadOnlyList<ProxyNodeRow> Rows { get; }

        /// <summary>
        /// 화면에 선 행 수 — <b>장면 머리글을 포함한다</b> (R6 S-3).
        ///
        /// ⚠ 높이·미니맵이 이것을 쓴다. <c>Rows.Count</c>(노드 행만)를 쓰면 머리글만큼
        /// 짧아져 아래 행이 상자 밖으로 삐져나온다.
        /// </summary>
        public int VisualRowCount { get; }
    }

    private sealed record ProxyNodeRow(string NodeId, int Index, Border Visual);

    // ── 선 모양 고치기 (G3) ─────────────────────────────────────────────────
    //
    // 2026-10-29 소유자: "선이 생기는 모양새가 종종 이상할 때가 있는데, 원한다면 저것도 곡선이나 그런 걸 조절할 수
    // 있도록 해줘. 피그잼처럼". 선(실행 선 · 선택지 레인)을 누르면 손잡이와 꼴 도구줄이 선다 — 손잡이를 끌면 선이
    // 굽고, 도구줄로 직각 · 곡선 · 직선을 고르거나 자동으로 되돌린다. 끄는 동안은 화면만 바뀌고 놓을 때 한 번 적는다
    // (되돌리기 한 칸 — 카드 끌기와 같다).

    private static readonly IBrush SelectedLineBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));

    /// <summary>고른 선택지 레인 — 챕터 간선의 신원(출발, 도착, 문구)이다.</summary>
    private sealed record RailKey(string ChapterId, string From, string To, string? Label);

    private RailKey? _selectedRail;

    /// <summary>이번 레일 그리기에서 고른 레인이 그려졌는가.</summary>
    private bool _selectedRailDrawn;

    /// <summary>손잡이와 도구줄. 판을 다시 지어도 같은 것을 다시 얹는다 — 끄는 동안 붙잡은 포인터를 잃지 않는다.</summary>
    private Ellipse? _bendHandle;
    private Border? _shapeBar;
    private StackPanel? _shapeButtons;
    private EdgeShape? _shapeBarShows;

    /// <summary>고른 선을 마지막으로 그은 모양 — 손잡이가 위아래로도 움직이는지(<see cref="ShapedEdge.FreeY"/>)를 본다.</summary>
    private ShapedEdge? _selectedShaped;

    private bool _bending;
    private Point _bendStart;
    private EdgeShape _bendFrom = EdgeShape.Default;

    /// <summary>끄는 동안의 모양 — 놓을 때까지 프로젝트에 적지 않는다.</summary>
    private EdgeShape? _shapePreview;

    /// <summary>선택지 레인 하나 — 칩 끝에서 레인을 타고 도착 접점까지(<see cref="EdgeShapeRouter.Lane"/>).</summary>
    private void DrawLane(
        RailKey key, EdgeShape? stored, GraphPosition from, double toward, double laneX, GraphPosition junction)
    {
        bool selected = key.Equals(_selectedRail);
        ShapedEdge lane = EdgeShapeRouter.Lane(
            from, toward, laneX, junction, selected && _shapePreview is { } preview ? preview : stored);

        _railVisuals.Add(new ShapePath
        {
            Data = ShapedGeometry(lane),
            Stroke = selected ? SelectedLineBrush : RailBrush,
            StrokeThickness = selected ? 2.4 : 1.4,
            IsHitTestVisible = false,
            Tag = $"레인:{key.From}>{key.To}"
        });

        // 누르는 자리 — 1.4px 선은 겨누기 어렵다. 넓고 투명한 같은 선이 받는다(카드 뒤라 카드를 가리지 않는다).
        var hit = new ShapePath
        {
            Data = ShapedGeometry(lane),
            Stroke = Brushes.Transparent,
            StrokeThickness = 10,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = key
        };
        hit.PointerPressed += (_, args) =>
        {
            args.Handled = true;
            SelectRail(key);
        };
        _railVisuals.Add(hit);

        if (selected)
        {
            _selectedRailDrawn = true;
            PlaceShapeOverlay(lane, stored);
        }
    }

    private void SelectRail(RailKey key)
    {
        SelectEdge(null);
        _selectedRail = key;
        DrawChapterRails();
        SetHint(key.Label is { Length: > 0 } label ? $"{key.From} → {key.To} · 「{label}」" : $"{key.From} → {key.To}");
    }

    /// <summary>실행 선의 모양 — 나가는 노드가 진다(<see cref="StoryNode.EdgeShapes"/>).</summary>
    private EdgeShape? ExitShapeOf(EdgeVisual edge) =>
        edge.Connection.ExecutionPort is { } exit && _session?.Project.FindNode(exit.NodeId) is { } node
            ? node.EdgeShapes.GetValueOrDefault(EdgeShape.KeyOf(exit))
            : null;

    /// <summary>선택지 레인의 모양 — 간선이 진다(<see cref="ChapterEdge.Shape"/>).</summary>
    private EdgeShape? RailShapeOf(RailKey key) =>
        _session?.Editor.FindChapter(key.ChapterId)?.Edges.FirstOrDefault(edge =>
            string.Equals(edge.FromEpisodeId, key.From, StringComparison.Ordinal) &&
            string.Equals(edge.ToEpisodeId, key.To, StringComparison.Ordinal) &&
            string.Equals(edge.OptionLabel, key.Label, StringComparison.Ordinal))?.Shape;

    private EdgeShape? SelectedLineShape() =>
        _selectedEdge is { } edge ? ExitShapeOf(edge)
        : _selectedRail is { } rail ? RailShapeOf(rail)
        : null;

    private static StreamGeometry ShapedGeometry(ShapedEdge shaped)
    {
        var geometry = new StreamGeometry();

        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(ToPoint(shaped.Points[0]), isFilled: false);

            if (shaped.Kind == EdgeShapeKind.Curve)
            {
                for (int index = 1; index + 2 < shaped.Points.Count; index += 3)
                {
                    context.CubicBezierTo(
                        ToPoint(shaped.Points[index]),
                        ToPoint(shaped.Points[index + 1]),
                        ToPoint(shaped.Points[index + 2]));
                }
            }
            else
            {
                for (int index = 1; index < shaped.Points.Count; index++)
                {
                    context.LineTo(ToPoint(shaped.Points[index]));
                }
            }

            context.EndFigure(isClosed: false);
        }

        return geometry;
    }

    /// <summary>선의 가운데 — 손잡이가 있으면 그 자리, 직선이면 두 끝의 가운데다.</summary>
    private static GraphPosition MiddleOf(ShapedEdge shaped) =>
        shaped.Handle ?? new GraphPosition(
            (shaped.Points[0].X + shaped.Points[^1].X) / 2,
            (shaped.Points[0].Y + shaped.Points[^1].Y) / 2);

    /// <summary>고른 선에 손잡이와 도구줄을 얹는다. 도구줄은 <b>적힌</b> 모양을 비춘다(끄는 중의 미리보기가 아니라).</summary>
    private void PlaceShapeOverlay(ShapedEdge shaped, EdgeShape? stored)
    {
        EnsureShapeOverlay();
        _selectedShaped = shaped;

        _bendHandle!.IsVisible = shaped.Handle is not null;

        if (shaped.Handle is { } handle)
        {
            Canvas.SetLeft(_bendHandle, handle.X - 6);
            Canvas.SetTop(_bendHandle, handle.Y - 6);
        }

        FillShapeBar(stored ?? EdgeShape.Default);

        GraphPosition middle = MiddleOf(shaped);
        Canvas.SetLeft(_shapeBar!, middle.X + 12);
        Canvas.SetTop(_shapeBar!, middle.Y + 12);
        _shapeBar!.IsVisible = true;
    }

    private void HideShapeOverlay()
    {
        _selectedShaped = null;

        if (_bendHandle is not null)
        {
            _bendHandle.IsVisible = false;
        }

        if (_shapeBar is not null)
        {
            _shapeBar.IsVisible = false;
        }
    }

    private void EnsureShapeOverlay()
    {
        if (_bendHandle is null)
        {
            _bendHandle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = Brushes.White,
                Stroke = SelectedLineBrush,
                StrokeThickness = 2,
                ZIndex = 1000,
                Cursor = new Cursor(StandardCursorType.SizeAll),
                [ToolTip.TipProperty] = "끌어서 선을 굽힙니다."
            };
            _bendHandle.PointerPressed += OnBendPressed;
            _bendHandle.PointerMoved += OnBendMoved;
            _bendHandle.PointerReleased += OnBendReleased;

            _shapeButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            _shapeBar = new Border
            {
                Child = _shapeButtons,
                Padding = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x80, 0x80, 0x80)),
                Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xF6, 0xF7, 0xFA)),
                ZIndex = 1000
            };
        }

        // 판을 다시 지으면(Children.Clear) 떨어져 나간다 — 같은 것을 다시 얹는다.
        if (_bendHandle.Parent is null)
        {
            GraphCanvas.Children.Add(_bendHandle);
        }

        if (_shapeBar!.Parent is null)
        {
            GraphCanvas.Children.Add(_shapeBar);
        }
    }

    /// <summary>꼴 셋과 「자동」. 지금 꼴은 굵게 서고, 같은 꼴을 또 누르면 굽이를 그대로 둔다.</summary>
    private void FillShapeBar(EdgeShape stored)
    {
        if (stored.Equals(_shapeBarShows))
        {
            return;
        }

        _shapeBarShows = stored;
        _shapeButtons!.Children.Clear();

        Button Choice(string text, string tip, EdgeShapeKind kind)
        {
            bool current = stored.Kind == kind;
            var button = new Button
            {
                Content = text,
                FontSize = 10,
                Padding = new Thickness(6, 1),
                MinHeight = 0,
                FontWeight = current ? FontWeight.Bold : FontWeight.Normal,
                [ToolTip.TipProperty] = tip
            };

            // ⚠ 지금 꼴이 아닐 때 Foreground를 null로 <b>적으면</b> 글이 사라진다 — 테마 값을 덮는다.
            if (current)
            {
                button.Foreground = SelectedLineBrush;
            }

            // 꼴을 바꾸면 굽이는 처음부터다 — 직각에서 민 양이 곡선에서는 다른 뜻이다.
            button.Click += (_, _) => UiGuard.Run(_session, "선 모양", () =>
            {
                if ((SelectedLineShape() ?? EdgeShape.Default).Kind != kind)
                {
                    ApplyShape(new EdgeShape(kind));
                }
            });

            return button;
        }

        _shapeButtons.Children.Add(Choice("직각", "꺾인 선 — 손잡이로 꺾이는 자리를 옮깁니다.", EdgeShapeKind.Elbow));
        _shapeButtons.Children.Add(Choice("곡선", "부드러운 곡선 — 손잡이로 굽이를 옮깁니다.", EdgeShapeKind.Curve));
        _shapeButtons.Children.Add(Choice("직선", "두 끝을 곧게 잇습니다.", EdgeShapeKind.Straight));

        var reset = new Button
        {
            Content = "↺ 자동",
            FontSize = 10,
            Padding = new Thickness(6, 1),
            MinHeight = 0,
            IsEnabled = !stored.IsDefault,
            [ToolTip.TipProperty] = "고친 모양을 걷고 처음 자리로 되돌립니다."
        };
        reset.Click += (_, _) => UiGuard.Run(_session, "선 모양", () => ApplyShape(null));
        _shapeButtons.Children.Add(reset);
    }

    /// <summary>고른 선에 모양을 적는다 — 실행 선은 노드에, 선택지 레인은 간선에.</summary>
    private void ApplyShape(EdgeShape? shape)
    {
        if (_session is null)
        {
            return;
        }

        if (_selectedEdge?.Connection.ExecutionPort is { } exit)
        {
            _session.Editor.SetExitShape(exit, shape);
        }
        else if (_selectedRail is { } rail)
        {
            _session.Editor.SetEdgeShape(rail.ChapterId, rail.From, rail.To, rail.Label, shape);
        }
    }

    private void OnBendPressed(object? sender, PointerPressedEventArgs args)
    {
        args.Handled = true;

        if (!args.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _bending = true;
        _bendStart = args.GetPosition(GraphCanvas);
        _bendFrom = SelectedLineShape() ?? EdgeShape.Default;
        _shapePreview = null;
        args.Pointer.Capture(_bendHandle);
    }

    private void OnBendMoved(object? sender, PointerEventArgs args)
    {
        if (!_bending)
        {
            return;
        }

        args.Handled = true;
        Point at = args.GetPosition(GraphCanvas);
        bool freeY = _selectedShaped?.FreeY ?? true;

        _shapePreview = _bendFrom with
        {
            BendX = Math.Round(_bendFrom.BendX + (at.X - _bendStart.X)),
            BendY = freeY ? Math.Round(_bendFrom.BendY + (at.Y - _bendStart.Y)) : _bendFrom.BendY
        };

        if (_selectedEdge is { } edge)
        {
            PositionEdge(edge);
        }
        else if (_selectedRail is not null)
        {
            DrawChapterRails();
        }
    }

    private void OnBendReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (!_bending)
        {
            return;
        }

        args.Handled = true;
        _bending = false;
        args.Pointer.Capture(null);

        EdgeShape? bent = _shapePreview;
        _shapePreview = null;

        // 누르기만 하고 놓았으면 적을 것이 없다.
        if (bent is not null)
        {
            UiGuard.Run(_session, "선 모양", () => ApplyShape(bent));
        }
    }

    private sealed class EdgeVisual
    {
        public EdgeVisual(GraphConnectionProjection connection, ShapePath path, Border label)
        {
            Connection = connection;
            Path = path;
            Label = label;
        }

        public GraphConnectionProjection Connection { get; set; }
        public ShapePath Path { get; }
        public Border Label { get; }
    }
}
