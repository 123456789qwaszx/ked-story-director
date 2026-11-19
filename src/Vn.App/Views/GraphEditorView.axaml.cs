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
using System.Text.RegularExpressions;
using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;
using Vn.Authoring.Text;

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
    private const double CardWidth = BoardSize.CardWidth;   // 판 크기 계산(BoardResize)과 같은 값 — 한 곳에서 정한다

    // 카드 크기는 사람이 가장자리를 끌어 키운다 — 오른쪽은 폭, 아래는 글 칸 높이, 오른쪽 아래 모서리는 둘 다. 기본보다 작게는
    // 줄이지 않는다(머리 · 포트 줄이 기본 폭에, 분기 점이 글 칸 기본 높이에 맞춰 섰다). 끝은 판의 가장 작은 변이다 — 판보다 큰 카드는
    // 쓸 데가 없다(2026-11-19 소유자 — 「크기 제한을 없애거나 더 늘릴 필요」 · usability UX4 · ⛔ 그 전에는 폭 720 · 글 칸 높이 900).
    internal const double MaxCardWidth = BoardSize.Minimum;
    internal const double MaxWritingHeight = BoardSize.Minimum;
    private const double ResizeGripWidth = 8;
    private const double ResizeCornerSize = 14;

    /// <summary>카드 크기 손잡이가 지는 표 — 화면 테스트가 이것으로 찾는다. 오른쪽 가장자리(폭).</summary>
    internal const string ResizeGripTag = "card-resize:";

    /// <summary>아래 가장자리(글 칸 높이) 손잡이의 표.</summary>
    internal const string ResizeHeightGripTag = "card-resize-height:";

    /// <summary>오른쪽 아래 모서리(폭과 높이) 손잡이의 표.</summary>
    internal const string ResizeCornerGripTag = "card-resize-corner:";

    /// <summary>카드 제목 줄 오른쪽 끝의 「글에 맞춤」 단추의 표(<see cref="FitCardToWriting"/> · usability UX5).</summary>
    internal const string FitButtonTag = "card-fit:";

    /// <summary>글에 맞출 때 더하는 몫 — 커서 · 반올림에 줄이 꺾이지 않게.</summary>
    private const double FitSlack = 4;

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
    private const double WritingBoxHeight = BoardSize.CardWritingHeight;
    private const double WritingBoxGap = 6;

    internal const string WritingTag = "writing:";

    // 한 대사의 분기 점들을 접은 공통 점과, 누르면 카드 밖으로 펼쳐지는 개별 점들 (2026-10-30 — PlaceLineKnobs).
    private const double BranchHubRadius = 8;

    /// <summary>펼친 개별 점이 카드 가장자리에서 떨어진 거리(점의 가운데까지).</summary>
    private const double BranchFanOut = 34;

    /// <summary>펼친 개별 점끼리의 세로 간격 — 지름 12에 틈 6. 끌어 잇는 손잡이라 겹치면 안 된다.</summary>
    private const double BranchFanStep = 18;

    private const double ProxyWidth = 250;
    private const double ProxyHeaderHeight = 50;
    private const double ProxyRowHeight = 28;
    private const double ProxyPortRadius = 5;

    /// <summary>접은 장면 상자의 Tag 머리 — 뒤에 <c>{판 Id}/{장면ID}</c>가 붙는다(2026-10-30 — 하이어아키의 눈).</summary>
    internal const string SceneProxyTag = "scene-proxy:";

    /// <summary>장면 이름표의 Tag 머리 — 뒤에 장면ID가 붙는다. 누르면 그 장면을 접는다.</summary>
    internal const string SceneLabelTag = "scene-label:";

    /// <summary>장면 색 — 장면 프레임 · 이름표 · 접은 장면 상자가 함께 쓴다.</summary>
    private static readonly Color SceneColor = Color.FromRgb(217, 119, 6);

    private readonly List<NodeCard> _cards = new();
    private readonly List<FileProxyVisual> _proxies = new();
    private readonly List<EdgeVisual> _edges = new();

    /// <summary>챕터(판) 박스 — 펼친 판의 노드들을 감싸는 배경 프레임 + 이름표 (2단계 무대 3번).</summary>
    private readonly List<Control> _frames = new();

    /// <summary>
    /// 판에 그린 장면 테두리의 자리 — 빈 곳을 눌렀을 때 그 자리가 어느 장면 안인지 찾는다(<see cref="SceneAt"/>). 테두리는 손을
    /// 받지 않으므로(카드 · 선 · 범위 선택이 먼저다) 자리로 찾는다. 테두리를 그릴 때마다 다시 채운다.
    /// </summary>
    private readonly List<(Rect Area, string FileId, string ChapterId, string SceneId)> _sceneAreas = new();

    /// <summary>판에 그린 챕터 테두리의 자리(챕터인 판만) — 장면 밖 · 챕터 안의 빈 곳을 찾는다.</summary>
    private readonly List<(Rect Area, string ChapterId)> _chapterAreas = new();

    /// <summary>
    /// 판의 빈 곳을 눌렀다(끌지 않고) — 셸이 하이어아키에서 그 줄을 고른 것처럼 다룬다(2026-11-17 소유자). 장면 테두리 안이면 그 장면,
    /// 장면 밖 · 챕터 테두리 안이면 그 챕터, 챕터 밖의 빈 판이면 프로젝트다.
    /// </summary>
    internal event Action<HierarchyPick>? AreaPicked;

    /// <summary>
    /// [＋ 에피소드]가 세울 장면(챕터, 장면) — 셸이 인스펙터에 장면 요약을 세운 동안 맞춰 둔다(하이어아키에서 골랐든 판에서
    /// 눌렀든). null이면 고른 카드의 장면, 그것도 없으면 새 장면이다(<see cref="AddEpisodeCard"/>).
    /// </summary>
    internal (string ChapterId, string SceneId)? NewNodeScene { get; set; }

    /// <summary>챕터 간선의 철도 배선 (T1) — 미러 선·분기점·선택지 칩.</summary>
    private readonly List<Control> _railVisuals = new();

    /// <summary>
    /// [구성]이 읽은 챕터 목록 — 툴바 콤보가 이것을 비춘다(목록의 원천은 하나). 철도는 이것이 아니라 살아 있는 프로젝트를 읽는다.
    /// </summary>
    private IReadOnlyList<ChapterEntry> _chapters = Array.Empty<ChapterEntry>();

    /// <remarks>
    /// 철도는 다시 긋지 않는다 — 철도는 살아 있는 프로젝트와 이 판의 투영을 읽고(<see cref="DrawChapterRails"/>), 그 둘이 바뀌면 판이
    /// 제 갱신으로 긋는다. ⛔ 2026-11-18까지는 여기서도 그었다(철도가 이 목록을 읽던 때의 흔적 — 그 길은 2026-10-29에 걷혔다): [구성]이
    /// 다시 읽을 때마다, 곧 글을 넣거나 끌기를 놓을 때마다 판의 철도를 통째로 두 번 그었다(에피소드 300개에서 한 번에 0.75초 ·
    /// finishing FX3).
    /// </remarks>
    internal void SupplyChapters(IReadOnlyList<ChapterEntry> entries)
    {
        _chapters = entries;
        RefreshChapterCombo();
        EmptyBoardPanel.IsVisible = entries.Count == 0;   // 챕터가 없으면 판 한가운데 [＋ 첫 챕터 만들기](first-chapter F1)
    }

    /// <summary>선택을 비추는 동안에는 <c>SelectionChanged</c>가 선택을 바꾸지 않게 한다.</summary>
    private bool _syncingChapter;

    /// <summary>
    /// 챕터 목록과 지금 활성인 판을 콤보에 비춘다 (2026-08-25).
    ///
    /// 목록의 원천은 <b>챕터 그래프가 읽은 하나</b>다(<see cref="SupplyChapters"/>) —
    /// 여기서 따로 읽으면 목록이 두 벌이 되어 서로 어긋날 수 있다. 선택도 마찬가지로
    /// <c>ActiveFileId</c> 하나를 비출 뿐이라, 챕터 그래프에서 고른 것이 여기에도 보인다.
    /// </summary>
    /// <summary>챕터 콤보가 지금 보이는 이름들 — 이름만 바뀌어도 항목을 다시 세우려고 들고 있는다.</summary>
    private string? _chapterComboNames;

    private void RefreshChapterCombo()
    {
        _syncingChapter = true;

        try
        {
            string[] ids = _chapters.Select(entry => entry.ChapterId).ToArray();
            string names = string.Join("\u001f", _chapters.Select(entry => entry.Model.ShownName));

            // 항목은 ID(고르는 규칙 그대로), 보이는 글은 이름이다(display-names N3). 이름만 바뀌어도 다시 세운다.
            if (!ids.SequenceEqual(
                    (ChapterCombo.ItemsSource as IEnumerable<string>) ?? [], StringComparer.Ordinal) ||
                !string.Equals(names, _chapterComboNames, StringComparison.Ordinal))
            {
                _chapterComboNames = names;
                ChapterCombo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((id, _) => new TextBlock
                {
                    Text = _chapters.FirstOrDefault(entry => string.Equals(entry.ChapterId, id, StringComparison.Ordinal))
                        ?.Model.ShownName ?? id
                });
                ChapterCombo.ItemsSource = ids;

                // 「조건 및 스탯」 탭 머리의 작은 드롭다운도 같은 목록이다(side-tabs V3).
                ConditionChapterCombo.ItemTemplate = ChapterCombo.ItemTemplate;
                ConditionChapterCombo.ItemsSource = ids;
            }

            // 판 이름 = ChapterId (챕터=판 1:1) — 활성 판이 곧 고른 챕터다.
            ChapterCombo.SelectedItem = _session?.ActiveFile?.Name is { } name && ids.Contains(name)
                ? name
                : null;
            ConditionChapterCombo.SelectedItem = ChapterCombo.SelectedItem;
        }
        finally
        {
            _syncingChapter = false;
        }
    }

    /// <summary>콤보에서 챕터를 골랐다 — 그 챕터의 판을 활성으로 하고 그리로 간다. 선택을 비추는 동안(<see cref="_syncingChapter"/>)은 하지 않는다.</summary>
    private void PickChapterFromCombo(ComboBox combo)
    {
        if (_syncingChapter || _session is null || combo.SelectedItem is not string chapterId)
        {
            return;
        }

        UiGuard.Run(_session, "챕터 고르기", () =>
        {
            string board = _session.EnsureChapterBoard(chapterId);
            _session.SelectFile(board);
            GoToBoard(board);   // 사람이 고른 챕터다 — 그리로 간다
        });
    }

    private AuthoringSession? _session;
    private GraphProjection? _projection;

    private NodeCard? _draggingCard;
    private Point _dragOffset;

    // 크기 끌기 — 끄는 동안 화면의 카드만 키우고 놓을 때 한 번 적는다(CommitResize).
    private NodeCard? _resizingCard;
    private ResizeEdges _resizeEdges;
    private Point _resizeStart;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    /// <summary>끄는 가장자리 — 모서리는 둘 다다.</summary>
    [Flags]
    private enum ResizeEdges
    {
        None = 0,
        Right = 1,
        Bottom = 2
    }

    private GraphOutputPortProjection? _connectingFrom;
    private Line? _connectingLine;

    private EdgeVisual? _selectedEdge;

    // ── 그래프 내비게이션 (W40) ────────────────────────────────────────────
    private const double MinZoom = 0.1;  // 큰 판(W41)에서도 전체 조망이 가능하게
    private const double MaxZoom = 2.0;

    // 판 크기 (W41) — 캔버스 크기의 유일한 정의. 미니맵과 같은 3:2 비율을 유지해야
    // 미니맵이 왜곡 없이 축소된다. Canvas는 좌표 공간일 뿐이라 크기 자체는 비용이
    // 없다 — 비용은 노드 수에서 나온다(가상화는 파일 판 백로그).
    // 판 크기 — 프로젝트의 것(StoryProject.Board, [⚙ 설정]에서 고친다 · 2026-11-17). 그려지는 카드가 그보다 바깥이면 그 카드까지
    // 넓혀 그린다(ApplyBoardSize — 안전장치: 편집 명령은 카드 자리를 판에 묶지 않으므로 판 밖에 선 카드가 사라지지 않게). 캔버스 · 카드 자리
    // 제한 · 미니맵이 이 둘만 본다.
    private double _boardWidth = BoardSize.Default.Width;
    private double _boardHeight = BoardSize.Default.Height;

    private double CanvasWidth => _boardWidth;

    private double CanvasHeight => _boardHeight;
    private const double MinimapWidth = 180;   // 캔버스와 같은 3:2 비율
    private const double MinimapHeight = 120;

    private double _zoom = 1;
    private bool _panning;
    private Point _panStart;
    private Vector _panStartOffset;
    private bool _minimapDragging;
    private Rectangle? _minimapViewport;
    private string? _followedNodeId; // 선택이 바뀐 순간에만 화면이 따라간다 (GB-4)

    // ⛔ 판별 뷰 상태(GB-1 — 활성 판이 바뀌면 떠나는 판의 자리 · 배율을 기억하고 새 판은 그 자리로, 처음이면 그 챕터 전체 보기로)는
    //    2026-11-17에 걷혔다(소유자 보고). 모든 챕터가 한 캔버스에 서므로 활성 판은 카드 하나를 눌러도 바뀌는데, 그 확인이 판을 다시
    //    그릴 때만 돌아서 <b>선택지 문구를 처음 열 때</b> 화면이 아까 활성이 된 챕터로 튀었다. 이제 화면은 사람이 그 챕터로 가자고 할
    //    때만 옮긴다(<see cref="GoToBoard"/>).

    /// <summary>맞추기로 한 판 — 판을 다 그리고 화면 크기가 선 뒤에 맞춘다(<see cref="ApplyPendingFit"/>). 없으면 null.</summary>
    private PendingFit? _pendingFit;

    /// <summary>첫 화면을 맞춘 프로젝트의 세대(<see cref="AuthoringSession.ProjectGeneration"/>) — 새로 열면 다시 맞춘다.</summary>
    private int _fittedGeneration = -1;

    /// <param name="FileId">맞출 판. null이면 맞추는 그때의 활성 판(프로젝트를 연 첫 화면).</param>
    private sealed record PendingFit(string? FileId);

    // 접힌 파일 드래그 (W52) — 프록시 위치는 안 노드들의 평균이므로,
    // 끌기는 파일 안 노드 전부를 같은 delta로 옮기는 것이다(펼치면 그 자리에 있다).
    private string? _draggingProxyFileId;

    /// <summary>끄는 상자가 접은 장면이면 그 장면 — 판째 접힌 상자면 null이다.</summary>
    private string? _draggingProxySceneId;
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

    /// <summary>글 칸마다 그 글의 줄 짝 — 분기 점을 그 대사 옆에 세우고, 고친 줄의 신원을 잇는다(2026-10-30).</summary>
    private readonly Dictionary<string, ScenarioWritingLayout> _writingLayouts = new(StringComparer.Ordinal);

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

    /// <summary>「화자」 탭의 내용 — 셸이 짓는다(화자 이름 목록).</summary>
    internal void SetSpeakerPanel(Control content) => SpeakerHost.Content = content;

    /// <summary>「조건 및 스탯」 탭의 내용 — 셸이 짓는다(지금 챕터의 스탯 표 · 조건 이름).</summary>
    internal void SetConditionPanel(Control content) => ConditionHost.Content = content;

    /// <summary>오른쪽 기둥이 지금 보이는 탭 — 화면 시험의 손잡이.</summary>
    internal SideTab ShownSideTab =>
        ReferenceEquals(SideTabs.SelectedItem, SpeakerTabItem) ? SideTab.Speakers
        : ReferenceEquals(SideTabs.SelectedItem, ConditionTabItem) ? SideTab.Conditions
        : SideTab.Inspector;

    /// <summary>지금 보이는 탭의 내용 자리(<c>SideHost</c> · <c>SpeakerHost</c> · <c>ConditionHost</c>) — 화면 시험이 이 안을 본다.</summary>
    internal ContentControl ShownSideHost => ShownSideTab switch
    {
        SideTab.Speakers => SpeakerHost,
        SideTab.Conditions => ConditionHost,
        _ => SideHost
    };

    /// <summary>오른쪽 기둥의 탭을 연다 — 셸의 고르기가 부르고, 화면 시험이 탭을 누른 것 대신 부른다.</summary>
    internal void ShowSideTab(SideTab tab) => SideTabs.SelectedItem = tab switch
    {
        SideTab.Speakers => SpeakerTabItem,
        SideTab.Conditions => ConditionTabItem,
        _ => InspectorTabItem
    };

    /// <summary>판 왼쪽 하이어아키 (2026-10-30 HY3) — 셸이 내용을 채우고 고른 줄을 받는다(챕터 그래프의 것과 같은 컨트롤).</summary>
    internal StoryHierarchy HierarchyView => Hierarchy;

    /// <summary>하이어아키 기둥 접기 (2026-10-30 HC) — 셸이 두 탭을 맞추고 사용자 설정에 기억한다.</summary>
    internal ColumnDock HierarchyDock { get; }

    /// <summary>오른쪽 기둥(탭 셋) 접기 — 셸이 두 탭을 맞추고 사용자 설정에 기억한다.</summary>
    internal ColumnDock SideDock { get; }

    /// <summary>
    /// 인스펙터 머리글 — 무엇을 보는지. 내용(<see cref="SetSidePanel"/>)을 짓는 셸이 함께 맞춘다. 부제가 비면 그 줄이 서지
    /// 않는다.
    /// </summary>
    internal void SetInspectorHeader(string title, string subtitle)
    {
        _nameCommit = null;
        _nameKey = null;
        InspectorNameBox.IsVisible = false;
        InspectorTitle.IsVisible = true;
        InspectorTitle.Text = title;
        InspectorSubtitle.Text = subtitle;
        InspectorSubtitle.IsVisible = subtitle.Length > 0;
    }

    /// <summary>
    /// 머리를 <b>이름 칸</b>으로 — 장면 요약(2026-11-18 소유자 — 「챕터이름과 장면 이름을 우측 인스펙터에서 바꿀 수 있도록 … 에피소드와
    /// 마찬가지로」). 손짓은 에피소드의 이름 칸과 같다: Enter · 초점 이탈 = 바꾸기, Esc = 그만두기. 같은 이름이면 부르지 않는다.
    /// 챕터는 부르지 않는다 — 챕터 이름 칸은 「조건 및 스탯」 탭 맨 위다(같은 날 · chapter-pick Y1, ⛔ 그 전에는 챕터 요약도 여기였다).
    /// </summary>
    /// <param name="key">무엇의 이름인가 — 같은 것을 다시 비출 때 쓰던 글을 덮지 않는다(고친 뒤 다시 그리기가 이것을 다시 부른다).</param>
    /// <param name="shown">지금 보이는 이름(비었으면 ID).</param>
    /// <param name="commit">고친 이름(앞뒤 빈칸을 걷은)을 받아 바꾸고 이제 보일 이름을 돌려준다 — 비웠으면 ID다. 이름만 바뀌면 요약을 다시 세우지
    /// 않으므로 칸은 이것으로 맞춘다.</param>
    internal void SetInspectorNameEditor(string key, string shown, string placeholder, string tip, Func<string, string> commit)
    {
        bool typing = string.Equals(_nameKey, key, StringComparison.Ordinal) && InspectorNameBox.IsFocused;

        _nameKey = key;
        _nameShown = shown;
        _nameCommit = commit;

        InspectorTitle.IsVisible = false;
        InspectorSubtitle.IsVisible = false;
        InspectorNameBox.IsVisible = true;
        InspectorNameBox.PlaceholderText = placeholder;
        ToolTip.SetTip(InspectorNameBox, tip);

        if (!typing)
        {
            InspectorNameBox.Text = shown;
        }
    }

    /// <summary>지금 머리에 보이는 것 — 이름 칸이 서 있으면 그 글, 아니면 이름 글. 시험의 손잡이다.</summary>
    internal string? InspectorHeaderText => InspectorNameBox.IsVisible ? InspectorNameBox.Text : InspectorTitle.Text;

    private string? _nameKey;
    private string _nameShown = string.Empty;
    private Func<string, string>? _nameCommit;

    private void CommitInspectorName()
    {
        string wanted = (InspectorNameBox.Text ?? string.Empty).Trim();

        if (_nameCommit is { } commit && !string.Equals(wanted, _nameShown, StringComparison.Ordinal))
        {
            _nameShown = commit(wanted);
            InspectorNameBox.Text = _nameShown;
        }
    }

    /// <summary>고른 카드가 화면 밖이면 그리로 옮긴다 — 하이어아키에서 고른 카드가 판에서 보이게.</summary>
    internal void RevealSelected() => ScrollToSelected();

    /// <summary>
    /// 카드를 두 번 눌렀다(글 칸 · 포트 · 선택지 칸 밖 — 글 칸의 두 번 누르기는 낱말 고르기다). 셸이 [구성] 탭의 그 에피소드로
    /// 데려간다.
    /// </summary>
    internal event Action<string>? CardOpened;

    /// <summary>선택지 레인을 두 번 눌렀다(챕터, 출발, 도착, 문구) — 셸이 [구성] 탭의 그 간선으로 데려간다.</summary>
    internal event Action<string, string, string, string>? ChoiceOpened;

    /// <summary>
    /// 그 선택지 레인을 고르고(인스펙터가 그 선을 설명한다) 출발 카드가 화면 밖이면 그리로 옮긴다 — [구성] 탭에서 간선을 두 번
    /// 눌러 넘어왔을 때. 그 레인이 판에 없으면(접힌 판 · 장면) 출발 카드를 고르고 false다.
    /// </summary>
    internal bool SelectChoiceRail(string chapterId, string from, string to, string label)
    {
        RailKey? key = _railVisuals.Select(visual => visual.Tag).OfType<RailKey>().FirstOrDefault(rail =>
            string.Equals(rail.ChapterId, chapterId, StringComparison.Ordinal) &&
            string.Equals(rail.From, from, StringComparison.Ordinal) &&
            string.Equals(rail.To, to, StringComparison.Ordinal) &&
            string.Equals(rail.Label?.Trim() ?? string.Empty, label, StringComparison.Ordinal));
        string? fromNodeId = _session is { } session ? EpisodeNaming.CardFor(session.Project, chapterId, from)?.Id : null;

        if (key is null)
        {
            if (fromNodeId is not null)
            {
                _session!.Select(fromNodeId);
                ScrollToSelected();
            }

            return false;
        }

        SelectRail(key);

        if (fromNodeId is not null && TargetVisualOf(fromNodeId) is { } target)
        {
            RevealTarget(target);
        }

        return true;
    }

    /// <summary>판에 붙어 있는 동안 「대본 글 칸」 설정을 듣는다 — 떨어지면 끊는다(설정은 앱 하나라 붙들고 있으면 판이 안 풀린다).</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ScriptFont.Changed += ApplyScriptFont;
        ApplyScriptFont();   // 떨어져 있던 사이에 바뀌었을 수 있다
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ScriptFont.Changed -= ApplyScriptFont;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>서 있는 카드 글 칸이 모두 새 글꼴 · 크기 · 줄 간격을 입는다 — 판은 다시 짓지 않는다(writer-comfort WC5).</summary>
    private void ApplyScriptFont()
    {
        foreach (TextBox box in _writingBoxes.Values)
        {
            ScriptFont.Apply(box);
        }
    }

    public GraphEditorView()
    {
        InitializeComponent();

        // 하이어아키 기둥 접기 (2026-10-30 HC) — 「‹」로 접고 띠를 눌러 편다.
        HierarchyDock = new ColumnDock(
            BoardColumns.ColumnDefinitions[0], BoardColumns.ColumnDefinitions[1], HierarchySplitter, HierarchyOpen, HierarchyStrip);
        FoldHierarchyButton.Click += (_, _) => HierarchyDock.Toggle(folded: true);
        HierarchyStrip.Click += (_, _) => HierarchyDock.Toggle(folded: false);

        // 오른쪽 기둥(탭 셋)도 같은 손짓으로 접는다(2026-11-18 소유자 · side-fold K1).
        SideDock = new ColumnDock(
            BoardColumns.ColumnDefinitions[4], BoardColumns.ColumnDefinitions[3], SideSplitter, SideOpen, SideStrip);
        FoldSideButton.Click += (_, _) => SideDock.Toggle(folded: true);
        SideStrip.Click += (_, _) => SideDock.Toggle(folded: false);

        // 판 크기는 한 곳(_boardWidth · _boardHeight — 프로젝트의 판 크기, ApplyBoardSize)이 정한다 (W41) — 미니맵 배율과 어긋날 길을 없앤다.
        GraphCanvas.Width = CanvasWidth;
        GraphCanvas.Height = CanvasHeight;

        AddEpisodeButton.Click += (_, _) => AddEpisodeCard();
        FirstChapterButton.Click += (_, _) => FirstChapterRequested?.Invoke();

        // 머리의 이름 칸 — Enter · 초점 이탈 = 바꾸기, Esc = 그만두기(에피소드 이름 칸 · 하이어아키 이름 칸과 같은 손짓).
        InspectorNameBox.LostFocus += (_, _) => UiGuard.Run(_session, "이름 바꾸기", CommitInspectorName);
        InspectorNameBox.KeyDown += (_, args) => UiGuard.Run(_session, "이름 바꾸기", () =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                CommitInspectorName();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                InspectorNameBox.Text = _nameShown;
            }
        });
        DeleteButton.Click += (_, _) => UiGuard.Run(_session, "삭제", DeleteSelected);

        // 고른 챕터의 판을 활성으로 — 그 뒤의 [+ 에피소드]가 거기에 선다.
        // ⚠ 다시 그리기가 콤보를 맞출 때는 안 돈다(_syncingChapter) — 안 그러면
        //    선택을 비추는 일이 선택을 바꾸는 일이 되어 서로를 부른다.
        ChapterCombo.SelectionChanged += (_, _) => PickChapterFromCombo(ChapterCombo);

        // 「조건 및 스탯」 탭의 작은 드롭다운 — 툴바 콤보와 같은 길이다. 탭은 그대로 둔다(보던 탭에서 챕터만 바뀐다).
        ConditionChapterCombo.SelectionChanged += (_, _) => PickChapterFromCombo(ConditionChapterCombo);

        GraphCanvas.PointerMoved += OnCanvasPointerMoved;
        GraphCanvas.PointerReleased += OnCanvasPointerReleased;
        GraphCanvas.PointerPressed += OnCanvasPointerPressed;

        // 그래프 내비게이션 (W40) — 휠 줌·중간 버튼 팬은 스크롤보다 먼저 가로챈다.
        GraphScroll.AddHandler(PointerWheelChangedEvent, OnGraphWheel, RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerTouchPadGestureMagnifyEvent, OnGraphMagnify, RoutingStrategies.Bubble);   // 집기 신호는 버블로만 온다
        GraphCanvas.AddHandler(PointerWheelChangedEvent, OnWritingBoxWheel, RoutingStrategies.Bubble);
        GraphScroll.AddHandler(PointerPressedEvent, OnGraphPanPressed, RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerMovedEvent, OnGraphPanMoved, RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerReleasedEvent, OnGraphPanReleased, RoutingStrategies.Tunnel);
        GraphScroll.ScrollChanged += (_, _) => RefreshMinimapViewport();

        // 맞추기로 한 판이 남아 있는데 화면 크기가 이제 섰다(탭이 처음 보였다) — 그때 맞춘다.
        GraphScroll.PropertyChanged += (_, args) =>
        {
            if (args.Property == ScrollViewer.ViewportProperty && _pendingFit is not null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(ApplyPendingFit, Avalonia.Threading.DispatcherPriority.Background);
            }
        };
        MinimapCanvas.PointerPressed += OnMinimapPressed;
        MinimapCanvas.PointerMoved += OnMinimapMoved;
        MinimapCanvas.PointerReleased += (_, _) => _minimapDragging = false;

        // 전체 보기·배율 프리셋 (GB-4). 프리셋은 화면 중앙 기준으로 배율만 바꾼다.
        FitAllButton.Click += (_, _) => FitAll();
        Zoom50Button.Click += (_, _) => ApplyZoom(0.5, null);
        Zoom100Button.Click += (_, _) => ApplyZoom(1, null);
        Zoom150Button.Click += (_, _) => ApplyZoom(1.5, null);

        _liveWriting.Tick += (_, _) => CommitLiveWriting();
    }

    internal void Attach(AuthoringSession session)
    {
        _session = session;

        // 단추가 <b>무엇을 지울지</b>는 고른 것에 달렸다 — 고를 때마다 이름을 맞춘다.
        session.SelectionChanged += (_, _) =>
            UiGuard.Run(_session, "삭제 단추 맞추기", RefreshDeleteButton);

        // 카드를 고르면 고른 선은 놓는다 — 인스펙터는 그 카드를 보인다(셸이 선택을 보고 맞춘다).
        session.SelectionChanged += (_, _) => UiGuard.Run(_session, "선 놓기", () =>
        {
            if (_session?.SelectedNodeId is not null)
            {
                ReleaseLine();
            }
        });

        // 고른 카드의 챕터가 활성 판이다(2026-11-18 소유자 — 「누른 챕터로 드롭다운의 챕터역시 바뀌도록」) — 하이어아키 · 판의 빈 곳은 셸이
        // 이미 그 판을 활성으로 한다. 판에서 다른 챕터의 카드를 눌렀을 때가 여기다. 보는 자리는 옮기지 않는다(reveal: false).
        session.SelectionChanged += (_, _) => UiGuard.Run(_session, "챕터 따라가기", () =>
        {
            if (_session?.SelectedNode is { } node &&
                _session.Project.FindFileContainingNode(node.Id) is { } board &&
                !string.Equals(board.Id, _session.ActiveFileId, StringComparison.Ordinal))
            {
                _session.SelectFile(board.Id, reveal: false);
            }
        });

        // 활성 판이 바뀌면 콤보가 따라간다 — ⛔ 그 전에는 다시 그릴 때만 맞춰, 하이어아키에서 다른 챕터를 골라도 콤보는 그대로였다.
        session.FileGraphStateChanged += (_, args) =>
        {
            if (args.ActiveFileChanged)
            {
                UiGuard.Run(_session, "챕터 콤보 맞추기", RefreshChapterCombo);
            }
        };

        // 카드의 「빈 선택지 숨기기」 — 그 카드의 철도만 다시 긋는다(board-speed PF2).
        session.HiddenChoicesChanged += nodeId => UiGuard.Run(_session, "선택지 칸", () => RedrawChoicesOf(nodeId));

        // 미니맵은 설정 창 「기타」가 켜고 끈다(side-fold K3).
        session.ShowMinimapChanged += (_, _) => ApplyShowMinimap();
        ApplyShowMinimap();

        RefreshDeleteButton();
    }

    /// <summary>
    /// 미니맵을 세우거나 걷는다(<see cref="AuthoringSession.ShowMinimap"/>). 걷는 것은 지도뿐이다 — [전체 · 50 · 100 · 150] 배율 줄은 남는다.
    /// 지도가 없으면 배율 줄 위의 구분선도 걷는다.
    /// </summary>
    private void ApplyShowMinimap()
    {
        bool show = _session?.ShowMinimap != false;
        MinimapCanvas.IsVisible = show;
        ZoomBar.BorderThickness = show ? new Thickness(0, 1, 0, 0) : default;
    }

    /// <summary>
    /// 인스펙터가 요약을 세운 줄(프로젝트 · 챕터 · 장면) — 셸이 하이어아키 · 판의 빈 곳에서 고른 것을 비춘다. 카드를 고르면 null이다.
    /// [삭제]가 무엇을 지울지 이것과 고른 카드로 정한다(<see cref="RefreshDeleteButton"/>).
    /// </summary>
    internal HierarchyPick? SummaryPick
    {
        get => _summaryPick;
        set
        {
            _summaryPick = value;
            RefreshDeleteButton();   // 같은 값이어도 — 구조가 바뀌어 고른 것이 사라졌을 수 있다
        }
    }

    private HierarchyPick? _summaryPick;

    /// <summary>[삭제]를 고른 챕터 · 장면에 눌렀다 — 셸이 단추 옆에 확인 칸을 띄우고 지운다. 에피소드 · 분기 카드는 이 뷰가 지운다.</summary>
    internal event Action<HierarchyPick, Control>? RemoveRequested;

    /// <summary>[삭제]가 지울 것.</summary>
    private enum DeleteTarget
    {
        None,
        Chapter,
        Scene,
        Episode,
        BranchCard
    }

    /// <summary>
    /// 그것이 챕터의 마지막 에피소드 · 장면이면 지우지 않는 까닭(<see cref="ChapterMinimum"/>), 아니면 null. 챕터 · 분기 카드는 늘 null이다.
    /// </summary>
    private string? KeptReason(DeleteTarget target)
    {
        switch (target)
        {
            case DeleteTarget.Episode when _session!.SelectedNode is DialogueNode card &&
                                           _session.Editor.FindChapter(EpisodeNaming.ChapterOf(_session.Project, card).ChapterId) is { } chapter &&
                                           ChapterMinimum.IsLastEpisode(chapter.Episodes, EpisodeNaming.EpisodeIdOf(card)):
                return ChapterMinimum.LastEpisodeReason;

            case DeleteTarget.Scene when _summaryPick is { ChapterId: { } chapterId, SceneId: { } sceneId } &&
                                         _session!.Editor.FindChapter(chapterId) is { } chapter &&
                                         ChapterMinimum.IsLastScene(chapter.Episodes, sceneId):
                return ChapterMinimum.LastSceneReason;

            default:
                return null;
        }
    }

    /// <summary>고른 카드가 먼저다(에피소드 · 분기 카드), 없으면 요약이 선 챕터 · 장면 — 그것이 아직 있을 때만.</summary>
    private DeleteTarget TargetToDelete()
    {
        if (_session is null)
        {
            return DeleteTarget.None;
        }

        if (_session.SelectedNode is DialogueNode card)
        {
            return card.IsBranchCard ? DeleteTarget.BranchCard : DeleteTarget.Episode;
        }

        ChapterDocument? chapter = _summaryPick?.ChapterId is { } chapterId ? _session.Editor.FindChapter(chapterId) : null;

        return _summaryPick?.Kind switch
        {
            HierarchyKind.Chapter when chapter is not null => DeleteTarget.Chapter,
            HierarchyKind.Scene when chapter is not null && chapter.Episodes.Any(episode =>
                string.Equals(episode.SceneId, _summaryPick.SceneId, StringComparison.Ordinal)) => DeleteTarget.Scene,
            _ => DeleteTarget.None
        };
    }

    /// <summary>
    /// 지울 것의 이름을 <b>단추에 적는다</b> (2026-09-18 · 2026-11-18 소유자 — 고른 것이 없으면 꺼진 「삭제」, 챕터 · 장면 · 에피소드 · 분기 카드를
    /// 고르면 「챕터 삭제」 · 「장면 삭제」 · 「에피소드 삭제」 · 「분기 카드 삭제」).
    ///
    /// ⛔ 고른 것과 다른 이름이 적혀 있으면 <b>단추가 거짓말</b>이다 — 누르기 <b>전에</b> 무엇이 사라지는지 보이는 것이 확인 한 걸음보다 먼저다.
    /// </summary>
    private void RefreshDeleteButton()
    {
        DeleteTarget target = TargetToDelete();
        string? kept = KeptReason(target);

        // 챕터의 마지막 에피소드 · 장면이면 꺼진 「삭제 불가」 — 툴팁이 까닭을 말한다(2026-11-18 소유자 · ChapterMinimum). ⛔ 처음에는 「에피소드 삭제」 ·
        //    「장면 삭제」 이름을 둔 채 껐는데, 같은 날 소유자가 「일종의 버그처럼 느껴집니다」 — 할 수 없는 일의 이름이 꺼진 채 서 있었다.
        DeleteButton.IsEnabled = target != DeleteTarget.None && kept is null;
        DeleteButton.Content = kept is not null ? "삭제 불가" : target switch
        {
            DeleteTarget.Chapter => "챕터 삭제",
            DeleteTarget.Scene => "장면 삭제",
            DeleteTarget.Episode => "에피소드 삭제",
            DeleteTarget.BranchCard => "분기 카드 삭제",
            _ => "삭제"
        };
        ToolTip.SetTip(DeleteButton, kept ?? target switch
        {
            DeleteTarget.Chapter => "고른 챕터와 그 장면 · 에피소드 · 카드를 지웁니다 — 한 번 더 확인합니다. 되돌리기(Ctrl+Z)로 돌아옵니다.",
            DeleteTarget.Scene => "고른 장면의 에피소드 · 카드와 그 에피소드들이 부르던 분기 카드, 장면 이름 · 메모를 지웁니다 — 한 번 더 확인합니다. " +
                                  "되돌리기(Ctrl+Z)로 돌아옵니다.",
            DeleteTarget.Episode => "고른 에피소드를 간선 · 픽스처 참조까지 함께 지웁니다 — 한 번 더 확인합니다. 되돌리기(Ctrl+Z)로 돌아옵니다.",
            DeleteTarget.BranchCard => "분기 카드를 지웁니다 — 부르던 분기점은 이어지지 않은 채 남습니다. 되돌리기 한 번으로 돌아옵니다.",
            _ => "하이어아키나 판에서 챕터 · 장면 · 에피소드를 고르면 그것을 지웁니다."
        });
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
        RefreshDeleteButton();   // 에피소드가 늘거나 줄면 마지막인지가 바뀐다(ChapterMinimum)

        _projection = GraphProjectionBuilder.Build(
            _session.Project,
            _session.ExpandedFileIds,
            _session.CollapsedScenes);
        ApplyBoardSize();

        // 카드 안 글 칸에 초점이 있었으면 다시 그린 뒤 그 칸으로 돌려준다(D4) — 판을 비우기 전에 잰다.
        WritingFocus? writing = CaptureWritingFocus();

        // 칸 목록을 먼저 비운다 — 판에서 떼어 낼 때 초점을 쥔 칸이 초점 이탈을 받는데, 그 칸은 이제 넣을 곳이 아니다
        // (넣지 않은 글은 RestoreWritingFocus가 새 칸으로 옮긴다).
        _writingBoxes.Clear();
        GraphCanvas.Children.Clear();
        _writingShown.Clear();
        _writingLayouts.Clear();
        _cards.Clear();
        _proxies.Clear();
        _edges.Clear();
        _frames.Clear();
        _sceneAreas.Clear();
        _chapterAreas.Clear();
        bool hadLine = _selectedEdge is not null || _selectedRail is not null;
        _selectedEdge = null;
        _selectedRail = null;
        _bending = false;
        _shapePreview = null;
        HideShapeOverlay();
        SlotLabelBox = null;
        _finishSlot = null;   // 아직 적고 있으면 아래에서 칸을 새로 지으며 다시 잡는다

        // 고른 간선이 사라졌으면 그 간선을 설명하던 줄도 사라져야 한다 — 안 그러면
        // 방금 지운 간선의 이름이 띠에 남는다(늘 서 있던 설명이 그 자리를 덮고 있어서
        // 여태 안 보였다).
        SetHint(null);

        _sceneRootNodeIds = SceneRootCards();
        _branchEntries = BranchEntries(_projection);

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

                case CollapsedSceneProjection scene:
                    _proxies.Add(BuildSceneProxy(scene));
                    break;
            }
        }

        DrawChapterFrames();

        AddEpisodeButton.IsEnabled = _session.ActiveFileId is not null;

        DrawEdges();
        HighlightSelection();
        RefreshMinimap();
        FitNewProject();
        RestoreWritingFocus(writing);

        // 고른 선이 판과 함께 사라졌다 — 인스펙터도 그 선을 내린다.
        if (hadLine)
        {
            LineSelected?.Invoke(null);
        }
    }

    /// <summary>
    /// 챕터(판) 박스 (2단계 무대 3번, PDF 3·9장) — 펼친 판마다 노드들을 감싸는 프레임과
    /// 이름표를 카드 <b>뒤에</b> 깐다. 작가가 "이 노드가 어느 챕터 소속인지"를 보고 붙일
    /// 곳을 정하는 무대다. 히트 대상이 아니라서 클릭·드래그·포트에 아무 영향이 없다.
    /// </summary>
    /// <param name="followRails">철도를 통째로 긋지 않고 따라가게 한다(<see cref="FollowRails"/>) — 끄는 동안과 그 끌기를 적은 직후.</param>
    private void DrawChapterFrames(bool followRails = false)
    {
        foreach (Control frame in _frames)
        {
            GraphCanvas.Children.Remove(frame);
        }

        _frames.Clear();
        _sceneAreas.Clear();
        _chapterAreas.Clear();

        if (_session is null || _projection is null)
        {
            return;
        }

        // 펼친 판의 카드와 그 판 안의 접은 장면 상자(2026-10-30)를 함께 감싼다 — 장면을 다 접어도 챕터 박스는 선다.
        foreach (IGrouping<string, GraphItemProjection> group in _projection.Items
                     .Where(item => item is ExpandedNodeProjection or CollapsedSceneProjection)
                     .GroupBy(item => item.FileId, StringComparer.Ordinal))
        {
            Rect? bounds = null;

            foreach (GraphItemProjection item in group)
            {
                Rect? rect = item switch
                {
                    ExpandedNodeProjection node when FindCard(node.NodeId) is { } card => FrameRectOf(node, card),
                    CollapsedSceneProjection scene when FindSceneProxy(scene.FileId, scene.SceneId) is { } proxy =>
                        ProxyRect(proxy),
                    _ => null
                };

                if (rect is { } found)
                {
                    bounds = bounds is { } current ? current.Union(found) : found;
                }
            }

            if (bounds is not { } area)
            {
                continue;
            }

            // 위쪽은 이름표 자리까지 여유를 둔다.
            area = area.Inflate(new Thickness(24, 46, 24, 24));

            bool isActive = string.Equals(group.Key, _session.ActiveFileId, StringComparison.Ordinal);
            // 판 이름 = 챕터 ID — 이름표는 보이는 이름(표시 이름, 비면 ID)이다(display-names N3).
            string boardName = _session.Project.Files
                .FirstOrDefault(file => string.Equals(file.Id, group.Key, StringComparison.Ordinal))
                ?.Name ?? group.Key;
            string chapterName = _session.Editor.FindChapter(boardName)!.ShownName;
            _chapterAreas.Add((area, boardName));

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
            ToolTip.SetTip(label, "누르면 이 챕터의 카드를 모두 고릅니다 — 그대로 끌면 함께 움직입니다. 더블클릭하면 표 형태로 접고, 접힌 표를 더블클릭하면 다시 펼칩니다.");

            // 이름표만 히트 대상이다(프레임은 아님) — 한 번 = 이 챕터의 카드를 모두 골라 함께 끈다, 두 번 = 접기(2026-11-18 소유자 · ⛔ 그 전에는 한 번에
            // 접었다 — PDF 9장 "체크해제하면 표형태").
            string frameFileId = group.Key;
            List<string> chapterCards = group.OfType<ExpandedNodeProjection>()
                .Where(node => FindCard(node.NodeId) is not null)
                .Select(node => node.NodeId)
                .ToList();
            label.PointerPressed += (_, args) => OnFrameLabelPressed(
                args,
                chapterCards,
                new HierarchyPick(HierarchyKind.Chapter, boardName),
                () => _session?.SetFileExpanded(frameFileId, expanded: false));

            Canvas.SetLeft(label, area.X + 12);
            Canvas.SetTop(label, area.Y + 10);

            _frames.Add(frame);
            _frames.Add(label);

            // 챕터 안에 장면을 한 겹 더 (R6 S-3b) — 챕터 프레임 <b>뒤에</b> 더해야 그 위에
            // 깔린다(아래 Insert가 목록 순서대로 넣는다).
            DrawSceneFrames(group.Key, group.OfType<ExpandedNodeProjection>());
        }

        // 카드·간선보다 뒤에 깔리도록 맨 앞 인덱스에 순서대로 끼운다.
        for (int index = 0; index < _frames.Count; index++)
        {
            GraphCanvas.Children.Insert(index, _frames[index]);
        }

        if (followRails)
        {
            FollowRails();
        }
        else
        {
            DrawChapterRails();
        }
    }

    /// <summary>
    /// <b>장면 프레임</b> — 챕터 프레임 안에 한 겹 더 (R6 S-3b · <c>docs/plans/R6-explorer.md</c> §5).
    ///
    /// 같은 장면의 카드가 한 영역으로 보여야 작가가 <b>어디까지가 한 수명</b>인지 안다 —
    /// 장면 안에서는 모든 게 물릴 수 있고 장면이 끝나면 확정되기 때문이다.
    ///
    /// 에피소드는 늘 장면에 속하므로 장면마다 그린다(⛔ 장면ID를 하나도 안 적은 챕터에 안 그리던 갈래는 2026-11-18에 걷혔다 · old-shapes O7).
    /// 장면이 하나뿐인 챕터에도 그린다(2026-11-17 소유자 — 전에는 챕터 프레임과 겹친다고 걷었다): 테두리가 있어야 그 장면을
    /// 눌러 고르고 접을 수 있다.
    ///
    /// ⚠ 히트 대상이 아니다 — 클릭·드래그·포트에 아무 영향이 없다(챕터 프레임과 같다). 테두리 안의 빈 곳을 <b>누르면</b>(끌지 않고)
    /// 그 장면을 고른다 — 자리를 <see cref="_sceneAreas"/>에 적어 두고 범위 선택이 아무것도 못 잡았을 때 찾는다(2026-11-17).
    /// </summary>
    private void DrawSceneFrames(string fileId, IEnumerable<ExpandedNodeProjection> nodes)
    {
        if (_session?.Project.Files.FirstOrDefault(file =>
                string.Equals(file.Id, fileId, StringComparison.Ordinal)) is not { } board)
        {
            return;
        }

        ChapterDocument chapter = EpisodeNaming.ChapterOfBoard(_session.Project, board);

        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(
            chapter.ToGraphModel(chapter.ChapterId));

        var sceneOf = new Dictionary<string, ChapterScene>(StringComparer.Ordinal);

        foreach (ChapterScene scene in scenes)
        {
            foreach (ChapterEpisode episode in scene.Episodes)
            {
                sceneOf[episode.EpisodeId] = scene;
            }
        }

        List<ExpandedNodeProjection> all = nodes.ToList();

        // 분기 카드는 집(부르는 에피소드)의 장면에 든다 — 저장하지 않고 연결에서 읽는다(2026-11-17).
        var homeOf = ChapterBranchCards.HomesOf(_session.Project, chapter.ChapterId)
            .ToDictionary(home => home.Card.Id, home => home.EpisodeId, StringComparer.Ordinal);

        string? SceneEpisodeOf(ExpandedNodeProjection node) =>
            IsBranchCard(node.NodeId) ? homeOf.GetValueOrDefault(node.NodeId) : EpisodeOf(node);

        foreach (ChapterScene scene in scenes)
        {
            Rect? bounds = null;
            var members = new List<string>();

            foreach (ExpandedNodeProjection node in all)
            {
                if (SceneEpisodeOf(node) is not { } episodeId ||
                    !sceneOf.TryGetValue(episodeId, out ChapterScene? owner) ||
                    !ReferenceEquals(owner, scene) ||
                    FindCard(node.NodeId) is not { } card)
                {
                    continue;
                }

                Rect rect = FrameRectOf(node, card);
                bounds = bounds is { } current ? current.Union(rect) : rect;
                members.Add(node.NodeId);
            }

            if (bounds is not { } area)
            {
                continue;
            }

            // 챕터 프레임(24·46)보다 얕게 — 두 테두리가 붙어 보이지 않게 한다.
            area = area.Inflate(new Thickness(10, 26, 10, 10));

            _sceneAreas.Add((area, fileId, chapter.ChapterId, scene.SceneId));
            _frames.Add(SceneFrame(area));
            _frames.Add(SceneLabel(area, fileId, chapter.ChapterId, scene, members, ChapterSceneNotes.HasMemo(chapter.SceneNotes, scene)));
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

    /// <summary>
    /// 장면 이름표 — <b>한 번 누르면 그 장면의 카드를 모두 골라 함께 끌고, 두 번 누르면 그 장면을 접는다</b>(2026-11-18 소유자 · ⛔ 2026-10-30부터는
    /// 한 번에 접었다 — 챕터 이름표와 같은 손짓). 접은 장면은 줄 목록 상자로 간소화되고(<see cref="BuildSceneProxy"/>), 그 상자를 두 번 누르면 다시
    /// 편다. 하이어아키의 눈과 같은 상태다.
    /// </summary>
    /// <param name="members">그 장면에 서 있는 카드(에피소드와 집이 그 장면인 분기 카드) — 테두리를 잰 그 카드들이다.</param>
    private Border SceneLabel(Rect area, string fileId, string chapterId, ChapterScene scene, IReadOnlyList<string> members, bool hasMemo)
    {
        var label = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1),
            Background = new SolidColorBrush(Color.FromArgb(180, SceneColor.R, SceneColor.G, SceneColor.B)),
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = SceneLabelTag + scene.SceneId,
            Child = new TextBlock
            {
                // ⚠ 장면이 확정·롤백의 경계라는 것이 이름표에서 읽혀야 한다.
                // 장면 메모나 에피소드 지시가 있으면 「· 메모」 — 하이어아키의 장면 줄과 같은 표시다.
                Text = $"장면 {scene.DisplayName}" + (hasMemo ? " · 메모" : string.Empty) +
                       (scene.HasSplitEntry ? " ⚠ ⌄" : " ⌄"),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            }
        };
        ToolTip.SetTip(label, "누르면 이 장면의 카드를 모두 고릅니다 — 그대로 끌면 함께 움직입니다. 더블클릭하면 줄 목록으로 접습니다(간선은 접힌 줄에 " +
                              "붙습니다). 접힌 상자를 더블클릭하면 다시 펼칩니다.");

        string sceneId = scene.SceneId;
        label.PointerPressed += (_, args) => OnFrameLabelPressed(
            args,
            members,
            new HierarchyPick(HierarchyKind.Scene, chapterId, SceneId: sceneId),
            () => _session?.SetSceneCollapsed(fileId, sceneId, collapsed: true));

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
    /// <summary>철도를 통째로 다시 그은 횟수 — 시험이 「일을 몇 번 했는가」를 보는 창이다(에피소드 300개에서 한 번에 0.75초 · finishing FX3).</summary>
    internal int RailDrawCount { get; private set; }

    private void DrawChapterRails()
    {
        RailDrawCount++;

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

        _railGroups.Clear();
        Dictionary<string, Rect> drawnAt = RailAnchors();

        // 장면을 다 접은 판도 레일이 선다 — 상자의 줄끼리, 줄과 카드 사이를 잇는다(2026-10-30).
        foreach (string fileId in _projection.Items
                     .Where(item => item is ExpandedNodeProjection or CollapsedSceneProjection)
                     .Select(item => item.FileId)
                     .Distinct(StringComparer.Ordinal))
        {
            RailBoard board = RailBoardOf(fileId);

            // 도착 포트 (2026-08-15 소유자) — 여러 선택지가 같은 에피소드로 이어지는 일이
            // 많으므로, 들어오는 가지들은 도착 카드 앞의 접점 하나로 모인다.
            var arrivals = new Dictionary<string, List<(string From, string Label)>>(StringComparer.Ordinal);

            foreach ((string episodeId, (DialogueNode node, Rect _)) in board.Spots)
            {
                RecordRailGroup(new RailOrigin.FromCard(fileId, episodeId), board.DepsFrom(node.Id, episodeId), drawnAt,
                    () => DrawRailsFromCard(board, episodeId, arrivals));
            }

            foreach ((string episodeId, FoldedRow row) in board.Folded)
            {
                if (board.Chapter.Episodes.Any(episode => string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal)))
                {
                    RecordRailGroup(new RailOrigin.FromRow(fileId, episodeId), board.DepsFrom(RowAnchor(row.Proxy), episodeId), drawnAt,
                        () => DrawRailsFromRow(board.BoardName, episodeId, row, board.Chapter.Edges, board.Spots, board.Folded, arrivals));
                }
            }

            foreach ((string toId, List<(string From, string Label)> incoming) in arrivals)
            {
                (DialogueNode target, Rect rect) = board.Spots[toId];
                RecordRailGroup(new RailOrigin.Arrival(fileId, toId, incoming), [target.Id], drawnAt,
                    () => DrawArrivalPort(rect, toId, incoming));
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
            LineSelected?.Invoke(null);
        }

        SettleRailsLater();
    }

    // ── 철도 따라가기 (2026-11-18 · finishing FX3) ─────────────────────────────────────────────
    //
    // 철도는 판에서 가장 비싼 그림이다 — 카드마다 줄기 · 가지 · 선택지 칸(글을 재야 한다) · 레인이 서서, 에피소드 300개면
    // 3,600개쯤이고 통째로 다시 긋는 데 0.75초가 든다. ⛔ 그 전에는 끄는 동안 포인터가 움직일 때마다 통째로 다시 그어 무리
    // 끌기가 한 번 움직이는 데 1.2초였다. 이제 철도를 묶음(한 출발의 가지들 · 한 도착 포트)으로 그어 두고, 묶음마다 기대는
    // 자리(카드 · 접은 상자)와 그때의 사각형(자리 · 크기)을 적어 둔다. 끄는 동안에는 기대는 자리가 모두 같은 만큼 움직이고 크기가
    // 그대로인 묶음은 그림째 옮기고(<see cref="TranslateTransform"/>), 아니면 — 무리 안과 밖을 잇는 길, 무리로 골라 테두리가 굵어진
    // 카드 — 그 출발에서만 다시 긋는다. 놓은 뒤의 자리 갱신도 같은 길로 간다(그 끌기가 적은 자리뿐이다). 모양 · 고름 · 글이 바뀌는
    // 다른 갱신은 늘 통째로 긋는다. 카드를 새로 지은 직후의 철도는 아직 재지 않은 카드 높이(어림)로 그어지므로, 배치가 끝난 뒤
    // 한 번 따라가 맞춘다(<see cref="SettleRailsLater"/>).

    /// <summary>철도 묶음의 출발 — 다시 그을 때 어디서부터인지.</summary>
    private abstract record RailOrigin(string FileId)
    {
        /// <summary>펼친 카드에서 나가는 줄기 · 가지 · 칸 · 레인.</summary>
        public sealed record FromCard(string FileId, string EpisodeId) : RailOrigin(FileId);

        /// <summary>접은 장면 상자의 줄에서 나가는 레인.</summary>
        public sealed record FromRow(string FileId, string EpisodeId) : RailOrigin(FileId);

        /// <summary>도착 카드 위의 접점 · 화살표 · 포트.</summary>
        public sealed record Arrival(string FileId, string EpisodeId, List<(string From, string Label)> Incoming) : RailOrigin(FileId);
    }

    /// <summary>철도 묶음 하나 — 그 그림과, 그림이 기대는 자리(카드 Id · 접은 상자 열쇠)를 그었을 때의 사각형.</summary>
    private sealed class RailGroup(RailOrigin origin, Dictionary<string, Rect> drawnAt, List<Control> visuals, bool holdsSelected)
    {
        public RailOrigin Origin { get; } = origin;

        public Dictionary<string, Rect> DrawnAt { get; set; } = drawnAt;

        public List<Control> Visuals { get; set; } = visuals;

        /// <summary>고른 레인이 이 묶음에 있다 — 모양 손잡이가 레인에 붙어 있어 옮기지 않고 다시 긋는다.</summary>
        public bool HoldsSelected { get; set; } = holdsSelected;
    }

    private readonly List<RailGroup> _railGroups = new();

    /// <summary>판 하나의 철도 재료 — 챕터, 에피소드 Id → 카드 자리, 접은 장면의 줄.</summary>
    private sealed record RailBoard(
        string BoardName,
        ChapterGraphModel Chapter,
        Dictionary<string, (DialogueNode Node, Rect Rect)> Spots,
        Dictionary<string, FoldedRow> Folded)
    {
        /// <summary>그 출발의 묶음이 기대는 자리 — 출발 자신과 가지가 닿는 카드 · 접은 상자.</summary>
        public List<string> DepsFrom(string self, string episodeId)
        {
            var deps = new List<string> { self };

            foreach (ChapterEdge edge in Chapter.Edges.Where(edge =>
                         string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal)))
            {
                if (Spots.TryGetValue(edge.ToEpisodeId, out (DialogueNode Node, Rect Rect) spot))
                {
                    deps.Add(spot.Node.Id);
                }
                else if (Folded.TryGetValue(edge.ToEpisodeId, out FoldedRow? row))
                {
                    deps.Add(RowAnchor(row.Proxy));
                }
            }

            return deps;
        }
    }

    /// <summary>접은 장면 상자의 자리 열쇠 — 카드 Id와 겹치지 않는다.</summary>
    private static string RowAnchor(FileProxyVisual proxy) => $"scene-proxy\u001f{proxy.FileId}\u001f{proxy.SceneId}";

    /// <summary>
    /// 투영의 사각형 — 카드 Id와 접은 장면 상자 열쇠마다. 크기는 철도가 재는 그대로다(카드 높이는 잰 값, 아직 안 쟀으면 어림 —
    /// <see cref="CardHeightOf"/>). 어림으로 잰 카드가 있으면 배치 뒤에 한 번 따라가 맞춘다.
    /// </summary>
    private Dictionary<string, Rect> RailAnchors()
    {
        var anchors = new Dictionary<string, Rect>(StringComparer.Ordinal);
        Dictionary<string, NodeCard> cards = _cards.ToDictionary(card => card.NodeId, StringComparer.Ordinal);

        foreach (GraphItemProjection item in _projection?.Items ?? [])
        {
            switch (item)
            {
                case ExpandedNodeProjection node when cards.TryGetValue(node.NodeId, out NodeCard? card):
                    _railsGuessed |= card.Visual.Bounds.Height <= 0;
                    anchors[node.NodeId] = new Rect(node.Position.X, node.Position.Y, card.Visual.Width, CardHeightOf(card));
                    break;

                case CollapsedSceneProjection scene when FindSceneProxy(scene.FileId, scene.SceneId) is { } proxy:
                    anchors[RowAnchor(proxy)] = new Rect(new Point(scene.Position.X, scene.Position.Y), proxy.Visual.Bounds.Size);
                    break;
            }
        }

        return anchors;
    }

    /// <summary>아직 재지 않은 카드의 어림 높이로 그은 철도가 있다 — 배치 뒤에 한 번 따라가 맞춘다.</summary>
    private bool _railsGuessed;

    private bool _settleQueued;

    /// <summary>배치가 끝난 뒤(낮은 순서) 철도를 한 번 따라가게 한다 — 어림으로 그은 묶음만 다시 긋는다.</summary>
    private void SettleRailsLater()
    {
        if (!_railsGuessed || _settleQueued)
        {
            return;
        }

        _settleQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _settleQueued = false;
                _railsGuessed = false;
                UiGuard.Run(_session, "철도 맞추기", FollowRails);
            },
            DispatcherPriority.Background);
    }

    /// <summary>그 판의 철도 재료를 지금 자리로 모은다.</summary>
    private RailBoard RailBoardOf(string fileId)
    {
        string boardName = _session!.Project.Files
            .FirstOrDefault(file => string.Equals(file.Id, fileId, StringComparison.Ordinal))
            ?.Name ?? string.Empty;

        // 살아 있는 프로젝트의 챕터를 본다 — 이 화면이 간선을 직접 긋는다.
        // ⛔ 셸이 공급한 챕터 목록(<c>_chapters</c>) 스냅샷을 뒷받침으로 보던 길은 2026-10-29에 걷혔다
        //    (legacy-cleanup.md L3). 그 목록도 같은 프로젝트를 읽은 것이라, 프로젝트에 없는 챕터를 줄 수 있는 것은
        //    낡은 스냅샷뿐이었다.
        ChapterGraphModel chapter = _session.Editor.FindChapter(boardName)!.ToGraphModel(boardName);

        // 에피소드 Id → 그 카드 자리. 아직 카드가 없는 에피소드는 없는 것으로 둔다 — 없는 노드로 선을 그으면 거짓말이다.
        var spots = new Dictionary<string, (DialogueNode Node, Rect Rect)>(StringComparer.Ordinal);
        Dictionary<string, NodeCard> cards = _cards.ToDictionary(card => card.NodeId, StringComparer.Ordinal);

        foreach (ExpandedNodeProjection item in _projection!.Items.OfType<ExpandedNodeProjection>()
                     .Where(item => string.Equals(item.FileId, fileId, StringComparison.Ordinal)))
        {
            if (!cards.TryGetValue(item.NodeId, out NodeCard? card))
            {
                continue;
            }

            var rect = new Rect(item.Position.X, item.Position.Y, card.Visual.Width, CardHeightOf(card));

            // ⭐ <b>곁가지도 제 가지와 선택지 칸을 받는다</b> (R7 P-6). 전에는 척추 카드만 여기 들어와 레일을 받았는데, 이제는
            //    판의 모든 카드가 에피소드라 <b>전부 아래에 3칸이 뚫려야</b> 한다 — 곁가지에서 진짜 분기를 뚫는 것이
            //    「분기 추가」가 노리는 바로 그 손짓이다.
            if (_session.Project.FindNode(item.NodeId) is DialogueNode { MarkedEpisodeId: { Length: > 0 } episodeId } dialogue)
            {
                spots[episodeId] = (dialogue, rect);
            }
        }

        // 접은 장면의 에피소드 → 그 상자의 줄 (2026-10-30). 카드가 없어도 길은 그 줄에 붙는다.
        return new RailBoard(boardName, chapter, spots, FoldedRowsOf(fileId));
    }

    /// <summary>그 카드에서 나가는 가지들을 긋는다.</summary>
    private void DrawRailsFromCard(RailBoard board, string episodeId, Dictionary<string, List<(string From, string Label)>> arrivals)
    {
        if (!board.Spots.TryGetValue(episodeId, out (DialogueNode Node, Rect Rect) spot))
        {
            return;
        }

        List<ChapterEdge> edges = board.Chapter.Edges
            .Where(edge => string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal))
            .ToList();

        DrawRailsFrom(board.BoardName, spot.Node, spot.Rect, edges, board.Spots, board.Folded, arrivals);
    }

    /// <summary>묶음 하나를 긋고 적어 둔다 — 그은 그림 · 기대는 자리의 그때 자리 · 고른 레인이 들었는가.</summary>
    private void RecordRailGroup(RailOrigin origin, IEnumerable<string> deps, IReadOnlyDictionary<string, Rect> now, Action draw)
    {
        int start = _railVisuals.Count;
        bool selectedBefore = _selectedRailDrawn;
        draw();

        _railGroups.Add(new RailGroup(
            origin,
            deps.Distinct(StringComparer.Ordinal).Where(now.ContainsKey).ToDictionary(dep => dep, dep => now[dep], StringComparer.Ordinal),
            _railVisuals.GetRange(start, _railVisuals.Count - start),
            !selectedBefore && _selectedRailDrawn));
    }

    /// <summary>
    /// 철도를 지금 투영의 자리로 따라가게 한다 — 기대는 자리가 모두 같은 만큼 움직인 묶음은 그림째 옮기고, 아니면 그 묶음만
    /// 다시 긋는다(위 머리글). 끄는 동안과 그 끌기를 적은 직후에만 쓴다.
    /// </summary>
    private void FollowRails()
    {
        if (_session is null || _projection is null)
        {
            return;
        }

        Dictionary<string, Rect> now = RailAnchors();
        var stale = new List<RailGroup>();

        foreach (RailGroup group in _railGroups)
        {
            Vector? shift = null;
            bool rigid = true;

            foreach ((string anchor, Rect drawn) in group.DrawnAt)
            {
                // 사라졌거나 크기가 바뀌었다(무리로 골라 테두리가 굵어졌다 · 어림이 잰 값이 됐다) — 옮겨서는 맞지 않는다.
                if (!now.TryGetValue(anchor, out Rect at) ||
                    Math.Abs(at.Width - drawn.Width) > 0.01 || Math.Abs(at.Height - drawn.Height) > 0.01)
                {
                    rigid = false;
                    break;
                }

                Vector moved = at.Position - drawn.Position;

                if (shift is { } first && (Math.Abs(first.X - moved.X) > 0.01 || Math.Abs(first.Y - moved.Y) > 0.01))
                {
                    rigid = false;
                    break;
                }

                shift ??= moved;
            }

            Vector by = shift ?? default;
            bool still = Math.Abs(by.X) < 0.01 && Math.Abs(by.Y) < 0.01;

            if (!rigid || (group.HoldsSelected && !still))
            {
                stale.Add(group);
                continue;
            }

            foreach (Control visual in group.Visuals)
            {
                visual.RenderTransform = still ? null : new TranslateTransform(by.X, by.Y);
            }
        }

        RedrawRailGroups(stale, now);
    }

    /// <summary>
    /// 그 카드의 빈 선택지를 감추거나 다시 세웠다 — 그 카드에서 나가는 철도 묶음만 다시 긋고, 테두리를 맞춘다(테두리는 카드 아래 칸까지
    /// 감싼다). 판은 다시 짓지 않는다(2026-11-18 · board-speed PF2).
    /// </summary>
    private void RedrawChoicesOf(string nodeId)
    {
        if (_session is null || _projection is null ||
            _session.Project.FindNode(nodeId) is not DialogueNode { MarkedEpisodeId: { Length: > 0 } episodeId } ||
            _session.Project.FindFileContainingNode(nodeId) is not { } file)
        {
            return;
        }

        RedrawRailGroups(
            _railGroups.Where(group => group.Origin is RailOrigin.FromCard card &&
                                       string.Equals(card.FileId, file.Id, StringComparison.Ordinal) &&
                                       string.Equals(card.EpisodeId, episodeId, StringComparison.Ordinal)).ToList(),
            RailAnchors());
        DrawChapterFrames(followRails: true);
        RefreshMinimap();
    }

    /// <summary>그 묶음들만 지금 자리로 다시 긋는다 — 그림이 있던 캔버스 자리에 끼운다(겹침 차례를 지킨다).</summary>
    private void RedrawRailGroups(List<RailGroup> stale, Dictionary<string, Rect> now)
    {
        if (stale.Count == 0)
        {
            return;
        }

        RailFollowRedrawCount += stale.Count;
        var boards = new Dictionary<string, RailBoard>(StringComparer.Ordinal);

        foreach (RailGroup group in stale)
        {
            int at = group.Visuals.Count > 0 ? GraphCanvas.Children.IndexOf(group.Visuals[0]) : -1;

            foreach (Control visual in group.Visuals)
            {
                GraphCanvas.Children.Remove(visual);
                _railVisuals.Remove(visual);
            }

            if (!boards.TryGetValue(group.Origin.FileId, out RailBoard? board))
            {
                boards[group.Origin.FileId] = board = RailBoardOf(group.Origin.FileId);
            }

            int start = _railVisuals.Count;
            bool selectedBefore = _selectedRailDrawn;
            _selectedRailDrawn = false;

            switch (group.Origin)
            {
                // 이웃 출발의 도착 장부는 그대로다 — 도착 포트는 도착 카드에만 기대어 제 묶음으로 따라간다.
                case RailOrigin.FromCard card:
                    DrawRailsFromCard(board, card.EpisodeId, new Dictionary<string, List<(string From, string Label)>>(StringComparer.Ordinal));
                    break;

                case RailOrigin.FromRow row when board.Folded.TryGetValue(row.EpisodeId, out FoldedRow? folded):
                    DrawRailsFromRow(board.BoardName, row.EpisodeId, folded, board.Chapter.Edges, board.Spots, board.Folded,
                        new Dictionary<string, List<(string From, string Label)>>(StringComparer.Ordinal));
                    break;

                case RailOrigin.Arrival arrival when board.Spots.TryGetValue(arrival.EpisodeId, out (DialogueNode Node, Rect Rect) spot):
                    DrawArrivalPort(spot.Rect, arrival.EpisodeId, arrival.Incoming);
                    break;
            }

            group.HoldsSelected = _selectedRailDrawn;
            _selectedRailDrawn |= selectedBefore;
            group.Visuals = _railVisuals.GetRange(start, _railVisuals.Count - start);
            group.DrawnAt = group.DrawnAt.Keys.Where(now.ContainsKey)
                .ToDictionary(anchor => anchor, anchor => now[anchor], StringComparer.Ordinal);

            int insertAt = at >= 0 ? Math.Min(at, GraphCanvas.Children.Count) : Math.Min(_frames.Count, GraphCanvas.Children.Count);

            foreach (Control visual in group.Visuals)
            {
                GraphCanvas.Children.Insert(insertAt++, visual);
            }
        }
    }

    /// <summary>철도를 따라가며 다시 그은 묶음의 수 — 시험이 「통째로 긋지 않았다」를 보는 창이다.</summary>
    internal int RailFollowRedrawCount { get; private set; }

    /// <summary>지금 판에 선 철도 그림 — 시험이 따라간 모습과 통째로 그은 모습을 견준다.</summary>
    internal IReadOnlyList<Control> RailVisuals => _railVisuals;

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
    ///    칸 없는 가지는 낡은 챕터 스냅샷에서만 나왔다. 그 배선 데이터(`ChoiceExits`)도 2026-11-17에 걷혔다.
    /// </summary>
    private void DrawRailsFrom(
        string chapterId,
        DialogueNode source,
        Rect sourceRect,
        IReadOnlyList<ChapterEdge> edges,
        IReadOnlyDictionary<string, (DialogueNode Node, Rect Rect)> spots,
        IReadOnlyDictionary<string, FoldedRow> folded,
        Dictionary<string, List<(string From, string Label)>> arrivals)
    {
        // 이 에피소드의 선택지 칸들 (R7) — 가지마다 제 칸을 얹어 <b>철도 위에서</b> 고치고 잇는다.
        IReadOnlyList<GraphChoicePort> slots = ShownSlotsOf(source.Id);

        var branches = new List<(ChapterEdge? Edge, Rect? Target, FoldedRow? Row, GraphChoicePort Slot)>();

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
                branches.Add((null, null, null, slot));
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

                // 도착이 접은 장면에 들었으면 그 상자의 줄로 간다 (2026-10-30) — 「카드 없음」이 아니다.
                FoldedRow? row = target is null && folded.TryGetValue(edge.ToEpisodeId, out FoldedRow? foldedRow)
                    ? foldedRow
                    : null;

                branches.Add((edge, target, row, slot));
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
            .Select(branch => branch.Target is { } target
                ? EdgeSide.GoesLeft(sourceRect.X, sourceRect.Width, target.X, target.Width)
                : branch.Row is { } row && EdgeSide.GoesLeft(sourceRect.X, sourceRect.Width, row.Left, row.Right - row.Left))
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
            (ChapterEdge? edge, Rect? target, FoldedRow? row, GraphChoicePort slot) = branches[index];
            bool mirrored = lefts[index];

            // 내려가는 레인은 카드 바깥에 가지마다 한 줄씩(쪽마다 따로 센다) — 같은 줄로 겹치면 어느 선이 어디로 가는지 안 보인다.
            double laneX = mirrored
                ? sourceRect.X - RailLaneOut - (leftLane++ * RailLaneGap)
                : sourceRect.Right + RailLaneOut + (rightLane++ * RailLaneGap);
            DrawRailBranch(chapterId, source, edge, trunkX, branchY, laneX, target, row, slot, mirrored);

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

    /// <summary>
    /// 접은 장면 상자의 줄에서 나가는 길 (2026-10-30 — 하이어아키의 눈). 상자는 간소화된 자리라 선택지 칸은 서지 않고, <b>이은
    /// 길만</b> 줄의 가장자리에서 나간다 — 오른쪽(도착이 왼쪽이면 왼쪽)으로 빠져 레인을 타고 도착 카드의 접점이나 다른 상자의
    /// 줄로 든다. 같은 상자 안끼리의 길은 긋지 않는다(상자 머리가 센다). 도착 카드가 판에 없는 길도 긋지 않는다.
    /// </summary>
    private void DrawRailsFromRow(
        string chapterId,
        string episodeId,
        FoldedRow row,
        IReadOnlyList<ChapterEdge> chapterEdges,
        IReadOnlyDictionary<string, (DialogueNode Node, Rect Rect)> spots,
        IReadOnlyDictionary<string, FoldedRow> folded,
        Dictionary<string, List<(string From, string Label)>> arrivals)
    {
        int leftLane = 0;
        int rightLane = 0;

        foreach (ChapterEdge edge in chapterEdges.Where(item =>
                     string.Equals(item.FromEpisodeId, episodeId, StringComparison.Ordinal)))
        {
            FoldedRow? toRow = folded.TryGetValue(edge.ToEpisodeId, out FoldedRow? other) ? other : null;
            Rect? toCard = toRow is null && spots.TryGetValue(edge.ToEpisodeId, out (DialogueNode Node, Rect Rect) spot)
                ? spot.Rect
                : null;

            if ((toRow is null && toCard is null) || ReferenceEquals(toRow?.Proxy, row.Proxy))
            {
                continue;
            }

            bool left = toRow is { } arrival
                ? EdgeSide.GoesLeft(row.Left, row.Right - row.Left, arrival.Left, arrival.Right - arrival.Left)
                : EdgeSide.GoesLeft(row.Left, row.Right - row.Left, toCard!.Value.X, toCard.Value.Width);
            double toward = left ? -1 : 1;
            var from = new GraphPosition(left ? row.Left : row.Right, row.Y);
            double laneX = from.X + (toward * (RailLaneOut + ((left ? leftLane++ : rightLane++) * RailLaneGap)));
            var key = new RailKey(chapterId, edge.FromEpisodeId, edge.ToEpisodeId, edge.OptionLabel);

            if (toRow is not null)
            {
                DrawLaneIntoRow(key, edge.Shape, from, toward, laneX, toRow);
                continue;
            }

            (double junctionX, double junctionY) = ArrivalJunction(toCard!.Value);
            DrawLane(key, edge.Shape, from, toward, laneX, new GraphPosition(junctionX, junctionY));

            if (!arrivals.TryGetValue(edge.ToEpisodeId, out List<(string From, string Label)>? list))
            {
                arrivals[edge.ToEpisodeId] = list = new List<(string, string)>();
            }

            list.Add((episodeId, edge.HasNoOptionLabel ? "(진행)" : edge.OptionLabel!));
        }
    }

    /// <summary>
    /// 레인을 접은 장면 상자의 줄로 들인다 — 레인이 상자 왼쪽에서 내려오면 줄의 왼쪽(들어오는 점)으로, 오른쪽에서 내려오면
    /// 오른쪽으로 든다(왼쪽 포트의 선이 도착 카드의 오른쪽으로 드는 것과 같은 규칙).
    ///
    /// ⚠ <b>레인이 상자의 폭 안으로 내려오면 바깥으로 비킨다</b> — 위아래로 쌓인 판에서는 카드 오른쪽 레인이 바로 아래 상자의
    ///   몸통을 지나 상자 뒤로 사라진다(상자가 레인 위에 그려진다). 나아가던 쪽으로 비킨다: 되돌아가면 칩을 가로지른다.
    /// </summary>
    private void DrawLaneIntoRow(
        RailKey key, EdgeShape? stored, GraphPosition from, double toward, double laneX, FoldedRow row)
    {
        if (laneX > row.Left && laneX < row.Right)
        {
            laneX = toward > 0 ? row.Right + RailLaneOut : row.Left - RailLaneOut;
        }

        bool fromLeft = laneX <= row.Left;
        var entry = new GraphPosition(fromLeft ? row.Left : row.Right, row.Y);

        DrawLane(key, stored, from, toward, laneX, entry);
        _railVisuals.Add(RailArrowInto(entry.X, entry.Y, fromLeft));
    }

    /// <summary>옆으로 드는 화살촉 — 접은 장면 상자의 줄 가장자리에 끝이 닿는다.</summary>
    private static Polygon RailArrowInto(double x, double y, bool fromLeft)
    {
        double back = fromLeft ? -7 : 7;
        var arrow = new Polygon
        {
            Fill = RailBrush,
            IsHitTestVisible = false,
            Points = [new Point(back, -4), new Point(0, 0), new Point(back, 4)]
        };

        Canvas.SetLeft(arrow, x);
        Canvas.SetTop(arrow, y);
        return arrow;
    }

    /// <summary>
    /// 그 판의 접은 장면 상자들의 줄 — 에피소드 Id로 찾는다. 카드 자리(<c>spots</c>)와 같은 규칙으로 표식을 단 대사 노드만 든다.
    /// </summary>
    private Dictionary<string, FoldedRow> FoldedRowsOf(string fileId)
    {
        var rows = new Dictionary<string, FoldedRow>(StringComparer.Ordinal);

        foreach (FileProxyVisual proxy in _proxies.Where(item =>
                     item.SceneId is not null && string.Equals(item.FileId, fileId, StringComparison.Ordinal)))
        {
            foreach (ProxyNodeRow row in proxy.Rows)
            {
                if (_session?.Project.FindNode(row.NodeId) is DialogueNode { MarkedEpisodeId: { Length: > 0 } episodeId })
                {
                    rows[episodeId] = new FoldedRow(proxy, row);
                }
            }
        }

        return rows;
    }

    /// <summary>접은 장면 상자의 한 줄 — 레인이 붙는 양쪽 가장자리와 줄의 가운데 높이.</summary>
    private sealed record FoldedRow(FileProxyVisual Proxy, ProxyNodeRow Row)
    {
        public double Left => Canvas.GetLeft(Proxy.Visual);

        public double Right => Left + ProxyWidth;

        public double Y => ProxyRowAnchor(Proxy, Row.Index, output: false).Y;
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
        int slots = ShownSlotsOf(nodeId).Count;

        return slots == 0 ? 0 : RailFirstDrop + ((slots - 1) * RailBranchGap) + 14;
    }

    /// <summary>프레임이 감쌀 한 카드의 자리 — 카드와 그 아래 레일까지.</summary>
    private Rect FrameRectOf(ExpandedNodeProjection node, NodeCard card) =>
        new(node.Position.X, node.Position.Y, card.Visual.Width, CardHeightOf(card) + RailDepthOf(node.NodeId));

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
                Text = "잇고 끊는 곳은 출발 쪽 칩과 [구성] 탭입니다.",
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

        // 빈 칸(자동 자리가 아니고 문구가 없다)은 <b>적는 칸 모양</b>이다(2026-11-18 소유자 — 「무엇을 입력할 수 있다는 걸 모르는 것 같습니다」):
        // 네모 칸에 회색 「빈 선택지」. 폭은 문구 칸(SlotEditor)의 가장 좁은 폭과 같아 누르면 그 자리에서 칸이 열린다. ⛔ 그 전의 「＋ 선택지」는
        // 글자색을 null로 못박아(붓이 없다) 보이지 않았다 — 점만 섰다.
        Control label = !auto && text.Length == 0
            ? new Border
            {
                Width = SlotEditorMinWidth,
                Padding = new Thickness(4, 1),
                BorderThickness = new Thickness(1),
                BorderBrush = SlotPlaceholderBorder,
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Ibeam),
                Tag = SlotPlaceholderTag,
                Child = new TextBlock { Text = SlotPlaceholderText, FontSize = 10, Foreground = SlotPlaceholderInk },
                [ToolTip.TipProperty] = "눌러서 선택지 문구를 적습니다. 적어야 끌어서 이을 수 있습니다."
            }
            : new TextBlock
            {
                Text = auto ? "자동" : text,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = dot,
                Opacity = live ? 1 : 0.45,
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

        // 빈 칸을 투영이 정한 수까지 채운다. 감추기는 그리는 쪽(<see cref="ShownSlotsOf"/>)이 한다 — 잇기 · 문구 적기는
        // 감춘 칸까지 든 이 차례로 칸 번호를 맞춘다.
        while (slots.Count < raw.Count)
        {
            slots.Add(Blank(slots.Count));
        }

        return slots;
    }

    /// <summary>
    /// 판에 그릴 칸 — 인스펙터의 「빈 선택지 숨기기」를 켠 카드는 이은 칸만이다. 안 이은 칸은 자동 길 자리 · 적어 둔 칸까지
    /// 감춘다(적어 둔 문구는 끄면 그 자리에 돌아온다). 칸 번호는 <see cref="SlotsOf"/>의 것을 그대로 지닌다.
    /// </summary>
    private IReadOnlyList<GraphChoicePort> ShownSlotsOf(string nodeId)
    {
        IReadOnlyList<GraphChoicePort> slots = SlotsOf(nodeId);

        return _session?.HiddenChoiceNodeIds.Contains(nodeId) == true
            ? slots.Where(slot => !slot.IsEmpty).ToList()
            : slots;
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
        FoldedRow? row,
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

        if (row is not null)
        {
            // 도착이 접은 장면에 들었다 (2026-10-30) — 그 상자의 줄로 든다.
            DrawLaneIntoRow(
                new RailKey(chapterId, edge.FromEpisodeId, edge.ToEpisodeId, edge.OptionLabel),
                edge.Shape,
                new GraphPosition(cursorX, y),
                toward,
                mirrored ? Math.Min(cursorX - RailLaneGap, laneX) : Math.Max(cursorX + RailLaneGap, laneX),
                row);

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
    /// <b>그 챕터로 간다</b> — 그 판의 카드(접혔으면 그 표)가 여백을 두고 들어오게 배율과 자리를 맞춘다(<see cref="FitFile"/>).
    /// 사람이 챕터를 골랐을 때만 부른다: 챕터 콤보, 하이어아키의 챕터 줄, 「챕터 추가」. 카드를 누르거나 [구성] 탭에서 챕터를
    /// 고른 것처럼 활성 판만 바뀐 일로는 화면을 옮기지 않는다(2026-11-17 — 그것이 늦게 돌아 화면이 튀었다).
    ///
    /// 맞추기는 미룬다 — 고르기가 판을 펼치면 다시 그리기는 다음 차례에 오고, 그 전에 맞추면 카드를 못 찾아 엉뚱한 곳(전체 보기)으로
    /// 간다. 탭이 아직 안 보여 화면 크기가 0이면 보일 때 맞춘다.
    /// </summary>
    internal void GoToBoard(string fileId) => RequestFit(new PendingFit(fileId));

    private void RequestFit(PendingFit fit)
    {
        _pendingFit = fit;
        Avalonia.Threading.Dispatcher.UIThread.Post(ApplyPendingFit, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void ApplyPendingFit()
    {
        if (_pendingFit is not { } fit || _session is null)
        {
            return;
        }

        if (GraphScroll.Viewport.Width <= 0 || GraphScroll.Viewport.Height <= 0)
        {
            return;   // 아직 안 보인다 — 화면 크기가 서면 다시 온다(생성자의 Viewport 구독)
        }

        _pendingFit = null;

        if ((fit.FileId ?? _session.ActiveFileId) is { } fileId)
        {
            FitFile(fileId);
        }
        else
        {
            FitAll();
        }
    }

    /// <summary>
    /// 프로젝트를 새로 열었거나 새로 만들었으면 <b>첫 화면</b>을 활성 챕터에 맞춘다. 탭이 안 보일 때 열었으면(앱은 [구성] 탭에서 열린다)
    /// [대본] 탭이 처음 보일 때 맞춘다. 되돌리기 · 다시 그리기로는 다시 맞추지 않는다.
    /// </summary>
    private void FitNewProject()
    {
        if (_session is null || _session.ProjectGeneration == _fittedGeneration)
        {
            return;
        }

        _fittedGeneration = _session.ProjectGeneration;
        RequestFit(new PendingFit(null));
    }

    /// <summary>
    /// 챕터 · 장면 테두리와 이름표만 다시 긋는다 — 카드 · 철도는 그대로다(철도는 따라가기만 하고, 아무것도 움직이지 않았으니 다시 긋지 않는다).
    /// 장면 이름표의 「메모」 표시가 에피소드 지시를 따른다(usability UX7).
    /// </summary>
    internal void RefreshSceneFrames()
    {
        SceneFrameDrawCount++;
        DrawChapterFrames(followRails: true);
    }

    /// <summary>테두리 · 이름표만 다시 그은 번 — 시험이 「판을 다시 짓지 않았다」를 본다.</summary>
    internal int SceneFrameDrawCount { get; private set; }

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
            _session.CollapsedScenes);

        // 카드를 옮겨 분기의 도착이 출발 카드를 넘었으면 포트가 반대쪽으로 선다 — 포트 줄은 카드 안에 지어지므로 카드부터 다시 짓는다.
        // 끌기 없이 크기만 바뀐 것(되돌리기 · 두 번 눌러 기본 크기)도 다시 짓는다 — 카드 높이는 배치가 끝나야 재므로 자리만
        // 고치면 레일이 옛 높이에 선다.
        if (PortSidesChanged(_projection) || SizesChanged())
        {
            Rebuild();
            return;
        }

        bool boardChanged = ApplyBoardSize();

        // 끌기 · 크기 바꾸기를 적은 직후의 갱신은 철도를 통째로 긋지 않고 따라간다 — 움직이거나 크기가 바뀐 카드에 기대는 묶음만 다시 긋는다
        // (FollowRails). ⛔ 크기는 2026-11-19까지 통째로 그어 에피소드 160개에서 끌어 놓기 0.8초 · 「글에 맞춤」 0.5초였다(usability UX8).
        PlaceVisuals(_projection, followRails: _committingDrag || _committingResize);

        if (boardChanged)
        {
            RefreshMinimap();   // 미니맵 배율이 판 크기를 따른다
        }
    }

    /// <summary>
    /// 판 크기를 프로젝트에 맞춘다 — <see cref="StoryProject.Board"/>, 그려지는 카드가 그보다 바깥이면 그 카드까지(안전장치). 바뀌었으면 참.
    /// 크기 바꾸기(<see cref="ProjectEditor.SetBoardSize"/>)는 자리 변경(Layout)이라 <see cref="RefreshPositions"/>로, 되돌리기는 다시 그리기로 온다 —
    /// 둘 다 여기를 지난다.
    /// </summary>
    private bool ApplyBoardSize()
    {
        if (_session is null)
        {
            return false;
        }

        double width = _session.Project.Board.Width;
        double height = _session.Project.Board.Height;

        foreach (StoryNode node in _session.Project.EnumerateNodes().Where(node => !GraphProjectionBuilder.IsHiddenPlumbing(node)))
        {
            width = Math.Max(width, node.Layout.X + BoardSize.CardWidthOf(node));
            height = Math.Max(height, node.Layout.Y + BoardSize.CardHeightOf(node));
        }

        if (width == _boardWidth && height == _boardHeight)
        {
            return false;
        }

        _boardWidth = width;
        _boardHeight = height;
        GraphCanvas.Width = width;
        GraphCanvas.Height = height;
        return true;
    }

    /// <summary>지금 선 카드들의 크기가 프로젝트의 것과 다른가 — 폭과 글 칸 높이.</summary>
    private bool SizesChanged() =>
        _cards.Any(card =>
            Math.Abs(card.Visual.Width - WidthFor(card.NodeId)) > 0.5 ||
            (card.WritingBox is { } box && Math.Abs(box.Height - WritingHeightFor(card.NodeId)) > 0.5));

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
                    CollapsedFileProjection file when _draggingProxySceneId is null &&
                                                      string.Equals(file.FileId, _draggingProxyFileId, StringComparison.Ordinal) =>
                        file with
                        {
                            Position = new GraphPosition(
                                _proxyPositionStart.X + _proxyDragDelta.X,
                                _proxyPositionStart.Y + _proxyDragDelta.Y)
                        },
                    CollapsedSceneProjection scene when string.Equals(scene.SceneId, _draggingProxySceneId, StringComparison.Ordinal) &&
                                                        string.Equals(scene.FileId, _draggingProxyFileId, StringComparison.Ordinal) =>
                        scene with
                        {
                            Position = new GraphPosition(
                                _proxyPositionStart.X + _proxyDragDelta.X,
                                _proxyPositionStart.Y + _proxyDragDelta.Y)
                        },
                    _ => item
                })
                .ToList(),
            _projection.Connections);

        PlaceVisuals(_projection, followRails: true);
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
        _draggingProxySceneId = null;

        if (_dragPositions.Count == 0)
        {
            return;
        }

        var positions = _dragPositions.ToDictionary(
            pair => pair.Key, pair => (pair.Value.X, pair.Value.Y), StringComparer.Ordinal);
        _dragPositions.Clear();
        _proxyDragDelta = default;

        // 적은 자리는 끄는 동안 이미 그린 자리다 — 그 갱신(셸이 곧바로 부르는 RefreshPositions)은 철도를 통째로 긋지 않고 따라간다.
        _committingDrag = true;

        try
        {
            _session?.Editor.MoveNodes(positions);
        }
        finally
        {
            _committingDrag = false;
        }
    }

    /// <summary>끌기가 자리를 적는 중이다 — 그 자리 갱신은 철도를 따라가게 한다(<see cref="FollowRails"/>).</summary>
    private bool _committingDrag;

    /// <summary>
    /// 화면의 카드를 이미 그 크기로 둔 채 크기를 적는 중이다(끌어 놓기 · 「글에 맞춤」) — 그 갱신은 철도를 따라가게 한다. 크기가 바뀐 카드에
    /// 기대는 묶음은 사각형이 달라져 그 출발에서만 다시 긋는다(<see cref="FollowRails"/>).
    /// </summary>
    private bool _committingResize;

    /// <summary>
    /// 크기를 한 번 적는다 — 화면의 카드는 이미 그 크기다. 배치를 먼저 끝내 둔다 — 철도가 카드의 잰 높이에 기댄다(끄는 동안의 마지막 움직임이
    /// 아직 배치되지 않았을 수 있다).
    /// </summary>
    private void WriteSize(NodeCard card, double? width, double? writingHeight)
    {
        card.Visual.UpdateLayout();
        _committingResize = true;

        try
        {
            _session?.Editor.ResizeNode(card.NodeId, width, writingHeight);
        }
        finally
        {
            _committingResize = false;
        }
    }

    /// <summary>그 노드의 카드 폭 — 사람이 넓힌 폭이 있으면 그것, 없으면 기본 폭이다.</summary>
    private double WidthFor(string nodeId) =>
        _session?.Project.FindNode(nodeId)?.Layout.Width is { } width
            ? Math.Clamp(width, CardWidth, MaxCardWidth)
            : CardWidth;

    /// <summary>그 노드의 글 칸 높이 — 사람이 늘린 높이가 있으면 그것, 없으면 기본 높이다.</summary>
    private double WritingHeightFor(string nodeId) =>
        _session?.Project.FindNode(nodeId)?.Layout.WritingHeight is { } height
            ? Math.Clamp(height, WritingBoxHeight, MaxWritingHeight)
            : WritingBoxHeight;

    /// <summary>
    /// 카드 가장자리의 크기 손잡이 — 오른쪽은 폭(기본 ~ <see cref="MaxCardWidth"/>, 글 칸이 함께 넓어진다), 아래는 글 칸 높이(기본 ~
    /// <see cref="MaxWritingHeight"/>), 오른쪽 아래 모서리는 둘 다다. 손잡이는 카드 여백(<see cref="CardPadding"/>) 안에 서서 글 칸을
    /// 가리지 않는다. 두 번 누르면 그 손잡이가 맡은 크기가 기본으로 돌아간다. 크기는 자리처럼 판의 모양이라 프로젝트에 싣고,
    /// 놓을 때 한 번 적는다(되돌리기 한 칸).
    /// </summary>
    private Border BuildResizeGrip(NodeCard card, ResizeEdges edges)
    {
        IBrush hover = new SolidColorBrush(Color.FromArgb(70, card.Style.Accent.R, card.Style.Accent.G, card.Style.Accent.B));

        var grip = new Border { Background = Brushes.Transparent };

        switch (edges)
        {
            case ResizeEdges.Right:
                grip.Width = ResizeGripWidth;
                grip.Margin = new Thickness(0, -CardPadding, -CardPadding, -CardPadding);
                grip.HorizontalAlignment = HorizontalAlignment.Right;
                grip.VerticalAlignment = VerticalAlignment.Stretch;
                grip.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                grip.Tag = ResizeGripTag + card.NodeId;
                ToolTip.SetTip(grip, "끌어서 카드 폭을 바꿉니다 — 글 칸이 함께 넓어집니다. 두 번 누르면 처음 폭으로 돌아갑니다.");
                break;

            case ResizeEdges.Bottom:
                grip.Height = ResizeGripWidth;
                grip.Margin = new Thickness(-CardPadding, 0, -CardPadding, -CardPadding);
                grip.HorizontalAlignment = HorizontalAlignment.Stretch;
                grip.VerticalAlignment = VerticalAlignment.Bottom;
                grip.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                grip.Tag = ResizeHeightGripTag + card.NodeId;
                ToolTip.SetTip(grip, "끌어서 글 칸 높이를 바꿉니다. 두 번 누르면 처음 높이로 돌아갑니다.");
                break;

            default:
                grip.Width = ResizeCornerSize;
                grip.Height = ResizeCornerSize;
                grip.Margin = new Thickness(0, 0, -CardPadding, -CardPadding);
                grip.HorizontalAlignment = HorizontalAlignment.Right;
                grip.VerticalAlignment = VerticalAlignment.Bottom;
                grip.Cursor = new Cursor(StandardCursorType.BottomRightCorner);
                grip.Tag = ResizeCornerGripTag + card.NodeId;
                ToolTip.SetTip(grip, "끌어서 카드 폭과 글 칸 높이를 함께 바꿉니다. 두 번 누르면 처음 크기로 돌아갑니다.");
                break;
        }

        grip.PointerEntered += (_, _) => grip.Background = hover;
        grip.PointerExited += (_, _) =>
        {
            if (_resizingCard != card)
            {
                grip.Background = Brushes.Transparent;
            }
        };

        grip.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // 카드 끌기(OnCardPressed)로 번지지 않게 여기서 받는다.
            args.Handled = true;
            ReleaseWritingFocus();
            _session?.Select(card.NodeId);

            if (args.ClickCount >= 2)
            {
                // 그 손잡이가 맡은 크기만 기본으로 — 다른 쪽 크기는 그대로 둔다.
                _resizingCard = null;
                UiGuard.Run(_session, "카드 크기", () =>
                {
                    if (_session?.Project.FindNode(card.NodeId)?.Layout is { } layout)
                    {
                        _session.Editor.ResizeNode(
                            card.NodeId,
                            edges.HasFlag(ResizeEdges.Right) ? null : layout.Width,
                            edges.HasFlag(ResizeEdges.Bottom) ? null : layout.WritingHeight);
                    }
                });
                return;
            }

            _resizingCard = card;
            _resizeEdges = edges;
            _resizeStart = args.GetPosition(GraphCanvas);
            _resizeStartWidth = card.Visual.Width;
            _resizeStartHeight = card.WritingBox?.Height ?? 0;
        };

        return grip;
    }

    /// <summary>카드 제목 줄의 「글에 맞춤」 단추 — 초점을 가져가지 않는다(쓰던 글 칸이 그대로 쓰는 중이다).</summary>
    private Button FitButton(string nodeId, CardStyle style)
    {
        var fit = new Button
        {
            Content = "⤢",
            FontSize = 12,
            Padding = new Thickness(3, 0),
            MinHeight = 0,
            Height = TitleHeight - 2,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(style.Accent),
            Focusable = false,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = FitButtonTag + nodeId
        };
        ToolTip.SetTip(fit, "글에 맞춤 — 지금 글꼴 · 글자 크기로 재서, 줄이 꺾이지 않게 폭을, 모든 줄이 보이게 글 칸 높이를 맞춥니다. " +
                            "글을 고친 뒤 다시 누르면 다시 맞춥니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
        fit.Click += (_, args) =>
        {
            args.Handled = true;
            UiGuard.Run(_session, "글에 맞춤", () => FitCardToWriting(nodeId));
        };
        return fit;
    }

    /// <summary>
    /// <b>「글에 맞춤」</b> (2026-11-19 소유자 — 「현재 글꼴과 글 크기를 고려해서 줄바꿈이 일어나지 않도록 가로의 길이를 자동으로 늘리고,
    /// 세로길이역시 모든 줄이 바로 보이도록 … 다시 누른다면 변경된 내용에 맞춰 다시」 · usability UX5).
    ///
    /// 글 칸의 글(넣지 않은 글도)을 지금 글꼴 · 글자 크기 · 줄 간격(<see cref="ScriptFont"/>)으로 잰다 — 폭은 가장 긴 줄이 꺾이지 않게, 글 칸
    /// 높이는 그 폭에서 모든 줄이 스크롤 없이 보이게. 카드 폭과 글이 놓이는 폭의 차(여백 · 테두리 · 등뼈 · 칸 안 여백)는 지금 배치에서 잰다 —
    /// 산식으로 세면 테마가 바뀔 때 어긋난다. 기본보다 작게는 줄이지 않고(짧은 글은 기본 크기로 돌아간다) 끝(<see cref="MaxCardWidth"/> ·
    /// <see cref="MaxWritingHeight"/>)을 넘지 않는다 — 폭이 끝에 걸리면 그 폭에서 꺾어 높이를 잰다.
    ///
    /// 끌어 키운 것(<see cref="CommitResize"/>)과 같은 길로 적는다 — 화면의 카드를 먼저 그 크기로 두고 <c>ResizeNode</c> 한 번(되돌리기 한 칸 ·
    /// 같은 크기면 적지 않는다). 그래서 판을 다시 짓지 않는다.
    /// </summary>
    internal void FitCardToWriting(string nodeId)
    {
        if (_session is null ||
            FindCard(nodeId) is not { WritingBox: { } box } card ||
            box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().FirstOrDefault() is not { } presenter ||
            box.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll)
        {
            return;
        }

        _session.Select(nodeId);

        // 폭 — 줄마다 꺾지 않고 잰 가장 긴 줄.
        var typeface = new Typeface(box.FontFamily, box.FontStyle, box.FontWeight);
        double longest = 0;

        foreach (string line in (box.Text ?? string.Empty).ReplaceLineEndings("\n").Split('\n'))
        {
            using var measured = new Avalonia.Media.TextFormatting.TextLayout(
                line, typeface, box.FontSize, foreground: null, lineHeight: box.LineHeight);
            longest = Math.Max(longest, measured.WidthIncludingTrailingWhitespace);
        }

        // 카드 폭 − 글이 놓이는 폭. 지금 글이 넘쳐 세로 스크롤 막대가 자리를 먹었으면 그 몫은 뺀다 — 맞춘 뒤에는 막대가 없다(빼지 않으면 다시
        // 누를 때마다 폭이 달라진다).
        double around = card.Visual.Bounds.Width - (presenter.Bounds.Width + (scroll.Bounds.Width - scroll.Viewport.Width));
        double width = Math.Clamp(Math.Ceiling(longest + around + FitSlack), CardWidth, MaxCardWidth);
        card.Visual.Width = width;

        // 높이 — 그 폭에서 실제로 배치된 글의 높이(글 자리가 칸을 채워 늘어나므로 그 자리가 아니라 배치된 줄들의 높이) + 칸의 위아래 몫.
        // 재는 동안은 칸을 끝까지 키워 둔다 — 글이 넘쳐 스크롤 막대가 서면 그만큼 좁아져 줄이 꺾인 채로 잰다.
        box.Height = MaxWritingHeight;
        card.Visual.UpdateLayout();
        double height = Math.Clamp(
            Math.Ceiling(presenter.TextLayout.Height + (box.Bounds.Height - presenter.Bounds.Height)),
            WritingBoxHeight,
            MaxWritingHeight);
        box.Height = height;
        PlaceLineKnobs(card);

        WriteSize(card, width <= CardWidth ? null : width, height <= WritingBoxHeight ? null : height);
    }

    /// <summary>
    /// 크기 끌기를 놓는다 — 화면에서만 키우던 폭 · 글 칸 높이를 한 번 적는다. 기본까지 줄였으면 적어 둔 값을 걷는다. 눌렀다
    /// 놓기만 했으면 적을 것이 없다.
    /// </summary>
    private void CommitResize()
    {
        if (_resizingCard is not { } card)
        {
            return;
        }

        _resizingCard = null;
        double width = Math.Round(card.Visual.Width);
        double height = Math.Round(card.WritingBox?.Height ?? 0);

        if (Math.Abs(width - _resizeStartWidth) < 0.5 && Math.Abs(height - _resizeStartHeight) < 0.5)
        {
            card.Visual.Width = _resizeStartWidth;

            if (card.WritingBox is { } box)
            {
                box.Height = _resizeStartHeight;
            }

            return;
        }

        // 화면의 크기를 적는 값(정수)에 맞춘다 — 끄는 동안의 소수 꼬리가 남으면 화면과 프로젝트가 어긋난다.
        card.Visual.Width = width;

        if (card.WritingBox is { } writing)
        {
            writing.Height = height;
        }

        UiGuard.Run(_session, "카드 크기", () => WriteSize(
            card,
            width <= CardWidth ? null : width,
            card.WritingBox is null || height <= WritingBoxHeight ? null : height));
    }

    /// <summary>
    /// 끌기를 걷는다 — 더블클릭의 첫 누름이 시작한 끌기다. 화면만 움직였으니 프로젝트 자리로 되돌려 놓는다.
    /// </summary>
    private void CancelDrag()
    {
        _draggingCard = null;
        _draggingGroup = false;
        _draggingProxyFileId = null;
        _draggingProxySceneId = null;

        if (_dragPositions.Count == 0)
        {
            return;
        }

        _dragPositions.Clear();
        _proxyDragDelta = default;
        RefreshPositions();
    }

    /// <summary>투영의 자리대로 카드 · 접힌 판 · 간선 · 프레임 · 레일 · 미니맵을 놓는다.</summary>
    /// <param name="followRails">끄는 동안 · 그 끌기를 적은 직후 — 자리가 그대로인 카드는 건드리지 않고 철도는 따라가게 한다.</param>
    private void PlaceVisuals(GraphProjection projection, bool followRails = false)
    {
        Dictionary<string, NodeCard> cards = _cards.ToDictionary(card => card.NodeId, StringComparer.Ordinal);

        foreach (ExpandedNodeProjection node in projection.Items.OfType<ExpandedNodeProjection>())
        {
            if (!cards.TryGetValue(node.NodeId, out NodeCard? card))
            {
                continue;
            }

            if (followRails && Canvas.GetLeft(card.Visual) == node.Position.X && Canvas.GetTop(card.Visual) == node.Position.Y)
            {
                continue;
            }

            Canvas.SetLeft(card.Visual, node.Position.X);
            Canvas.SetTop(card.Visual, node.Position.Y);
            PlaceLineKnobs(card);
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

        foreach (CollapsedSceneProjection scene in projection.Items.OfType<CollapsedSceneProjection>())
        {
            if (FindSceneProxy(scene.FileId, scene.SceneId) is { } proxy)
            {
                Canvas.SetLeft(proxy.Visual, scene.Position.X);
                Canvas.SetTop(proxy.Visual, scene.Position.Y);
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
        DrawChapterFrames(followRails);

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
            // 지우기의 대체 선택(사라진 노드 → 첫 노드)은 사용자가 고른 게 아니다 —
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
    /// 판의 카드는 에피소드(<b>각진 미색 서류</b> 📄 — 장면 루트는 파랑 ⌂)이거나 분기 카드(보라 ⑂)다. 미니맵·접힌 목록도 같은 언어를 쓴다.
    /// ⛔ 작가의 자유 씬(둥근 흰 원고 ✎)은 종류째 없어졌고 그 모양은 2026-11-18에 걷혔다(old-shapes O9).
    /// </summary>
    /// <param name="sceneRoot">
    /// 장면의 루트 에피소드인가 (2026-10-30 SR — 소유자: "장면의 Root 노드는 색깔을 조금 다르게"). 같은 서류 모양에 등뼈 ·
    /// 바탕만 챕터 그래프의 「SCENE ROOT」 표와 같은 파랑 쪽으로 바꾸고, 아이콘은 하이어아키의 ⌂다 — 세 화면이 한 말을 한다.
    /// </param>
    private static CardStyle CardStyleFor(GraphNodeKind kind, bool sceneRoot = false, bool branchCard = false) => kind switch
    {
        // 분기 카드(2026-11-17) — 들렀다 돌아오는 카드. 에피소드와 다른 물건이라 색 · 표가 다르다(보라 · ⑂).
        GraphNodeKind.Dialogue when branchCard => new CardStyle(
            Color.FromRgb(0x7C, 0x5C, 0xC4), Color.FromRgb(0xF6, 0xF3, 0xFC),
            Color.FromRgb(0xD5, 0xCB, 0xEE), 4, "⑂"),
        GraphNodeKind.Dialogue when sceneRoot => new CardStyle(
            Color.FromRgb(0x3D, 0x7B, 0xD9), Color.FromRgb(0xEF, 0xF4, 0xFC),
            Color.FromRgb(0xC3, 0xD5, 0xEF), 4, "⌂"),
        GraphNodeKind.Dialogue => new CardStyle(
            Color.FromRgb(0xD9, 0x77, 0x06), Color.FromRgb(0xFB, 0xF6, 0xEA),
            Color.FromRgb(0xE3, 0xD5, 0xB7), 4, "📄"),
        _ => new CardStyle(
            Color.FromRgb(0x3B, 0x82, 0xF6), Color.FromRgb(0xEF, 0xF6, 0xFF),
            Color.FromArgb(90, 128, 128, 128), 10, "⚙")
    };

    /// <summary>분기로 다녀오는 카드 → 그 분기 선의 색 번호. 판을 다시 세울 때마다 묻는다(<see cref="BranchEntries"/>).</summary>
    private Dictionary<string, int> _branchEntries = new(StringComparer.Ordinal);

    /// <summary>
    /// 분기 선이 들어오는 카드들 — 챕터 판의 분기만 든다(조건은 챕터의 조건이다). 한 카드에 분기 선이 여럿 들어오면 처음 것의 색을
    /// 쓴다 — 조건은 카드의 첫머리 하나라 어느 선으로 가도 같은 조건이다.
    /// </summary>
    private Dictionary<string, int> BranchEntries(GraphProjection projection)
    {
        var entries = new Dictionary<string, int>(StringComparer.Ordinal);

        if (_session is null)
        {
            return entries;
        }

        foreach (GraphConnectionProjection connection in projection.Connections)
        {
            if (connection.ExecutionPort is not { Kind: ExitPortKind.Detour } ||
                entries.ContainsKey(connection.TargetNodeId) ||
                _session.Project.FindNode(connection.SourceNodeId) is not DialogueNode)
            {
                continue;
            }

            entries[connection.TargetNodeId] = connection.PaletteIndex;
        }

        return entries;
    }

    /// <summary>분기 조건 표의 Tag 머리 — 뒤에 카드(노드) Id.</summary>
    internal const string BranchConditionChipTag = "branch-condition-chip:";

    /// <summary>
    /// 분기로 다녀오는 카드 머리의 조건 표 — 「⑂ 조건 없음」 · 「⑂ 조건: X」. 조건 없는 분기와 조건 걸린 분기가 판에서 똑같아 보이면
    /// 헷갈린다. 색은 그 분기 선의 색이다. <b>누르면 그 카드를 고른다</b>(카드를 누른 것과 같다 — 인스펙터 위쪽에 조건 칸이 선다,
    /// 2026-11-17 소유자). ⛔ 전에는 [구성] 탭의 그 분기로 갔다.
    /// </summary>
    private Border BranchConditionChip(string nodeId, int palette)
    {
        BranchEntryCondition.State state = BranchEntryCondition.Read(_session!.Project, nodeId);
        IBrush accent = BranchPalette.Accent(palette);
        bool undecided = BranchConditionNote.IsUndecided(_session.Project, nodeId, state.Label);

        var chip = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 0),
            Margin = new Thickness(4, 0, 0, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = accent,
            Background = state.Label is null ? Brushes.Transparent : BranchPalette.Background(palette),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = BranchConditionChipTag + nodeId,
            Child = new TextBlock
            {
                Text = BranchConditionNote.Chip(state, undecided),
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = accent,
                MaxWidth = 110,
                TextTrimming = TextTrimming.CharacterEllipsis
            },
            [ToolTip.TipProperty] = $"분기로 다녀오는 카드입니다. {BranchConditionNote.Sentence(state, undecided)} {BranchConditionNote.Where}"
        };

        // 누르기는 카드로 흘린다 — 카드를 누른 것과 같다(고른다 · 끈다).
        return chip;
    }

    /// <summary>장면의 루트 에피소드를 진 카드들 — 판을 다시 세울 때마다 묻는다(<see cref="SceneRootCards"/>).</summary>
    private HashSet<string> _sceneRootNodeIds = new(StringComparer.Ordinal);

    /// <summary>
    /// 장면의 루트 에피소드를 진 카드들 (2026-10-30 SR) — <b>하이어아키가 ⌂를 다는 그것</b>이다(<see cref="ChapterScene.RootEpisodeId"/>).
    ///
    /// ⚠ 장면ID를 하나도 안 적은 챕터는 칠하지 않는다 — 에피소드마다 제 장면이라 모든 카드가 루트이고, 모두 칠하면 표식이 아니라
    ///   소음이다(하이어아키가 같은 까닭으로 ⌂를 뗀다).
    /// </summary>
    private HashSet<string> SceneRootCards()
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);

        if (_session is null)
        {
            return roots;
        }

        foreach (StoryFile file in _session.Project.Files)
        {
            ChapterDocument chapter = EpisodeNaming.ChapterOfBoard(_session.Project, file);
            foreach (ChapterScene scene in ChapterSceneGrouping.Of(chapter.ToGraphModel(chapter.ChapterId)))
            {
                if (EpisodeNaming.CardFor(_session.Project, chapter.ChapterId, scene.RootEpisodeId) is { } card)
                {
                    roots.Add(card.Id);
                }
            }
        }

        return roots;
    }

    /// <summary>챕터의 에피소드인가 — 소속을 묻는 것이지 잠금을 묻는 것이 아니다.</summary>
    private bool IsBranchCard(string nodeId) =>
        _session?.Project.FindNode(nodeId) is DialogueNode { IsBranchCard: true };

    private NodeCard BuildCard(ExpandedNodeProjection node)
    {
        bool sceneRoot = _sceneRootNodeIds.Contains(node.NodeId);
        bool branchCard = IsBranchCard(node.NodeId);
        CardStyle style = CardStyleFor(node.NodeKind, sceneRoot, branchCard);

        var body = new StackPanel { Spacing = 0 };

        var titleIcon = new TextBlock
        {
            Text = branchCard ? $"{style.Icon} 분기" : style.Icon,
            FontSize = 11,
            Foreground = new SolidColorBrush(style.Accent),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            [ToolTip.TipProperty] = sceneRoot
                ? "장면 시작점 — 장면이 시작하는 에피소드입니다. 롤백이 되돌아가고 이어하기가 재개하는 자리입니다."
                : branchCard
                    ? "분기 카드 — 다른 카드의 분기점으로 들렀다가 그 자리로 돌아옵니다. 선택지가 없고 진행 JSON에 실리지 않습니다. " +
                      "장면은 부르는 에피소드의 것입니다."
                    : null
        };

        // 에피소드 카드는 보이는 제목이다 — EpisodeId는 [구성] 탭에서 정하고, 여기서는 툴팁으로만 보인다.
        var titleName = new TextBlock
        {
            Text = ShownName(node.NodeId, node.NodeName),
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            [ToolTip.TipProperty] = _session?.Project.FindNode(node.NodeId) switch
            {
                DialogueNode { MarkedEpisodeId: not null } episode =>
                    $"에피소드ID: {EpisodeNaming.EpisodeIdOf(episode)} — 에피소드ID는 [구성] 탭에서 정합니다.",
                DialogueNode { IsBranchCard: true } branch =>
                    $"분기 카드 ID: {branch.Name} — ID(= Yarn 노드 이름)는 [구성] 탭에서 정합니다.",
                _ => null
            }
        };

        var title = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Height = TitleHeight,
            Margin = new Thickness(0, 0, 0, HeaderGap)
        };
        Grid.SetColumn(titleIcon, 0);
        Grid.SetColumn(titleName, 1);
        title.Children.Add(titleIcon);
        title.Children.Add(titleName);

        if (_branchEntries.TryGetValue(node.NodeId, out int palette))
        {
            Control chip = BranchConditionChip(node.NodeId, palette);
            Grid.SetColumn(chip, 2);
            title.Children.Add(chip);
        }

        // 「글에 맞춤」 — 제목 줄 오른쪽 끝(글 칸이 있는 카드만 · 2026-11-19 소유자 · usability UX5).
        if (node.NodeKind == GraphNodeKind.Dialogue)
        {
            Button fit = FitButton(node.NodeId, style);
            Grid.SetColumn(fit, 3);
            title.Children.Add(fit);
        }

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

        // 카드 안의 한 겹 — 내용 위에 오른쪽 가장자리의 폭 손잡이가 얹힌다(아래 BuildResizeGrip).
        var frame = new Panel();
        frame.Children.Add(layout);

        var card = new Border
        {
            Width = WidthFor(node.NodeId),
            Padding = new Thickness(CardPadding),
            CornerRadius = new CornerRadius(style.Radius),
            Background = new SolidColorBrush(style.Background),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(style.Border),
            Child = frame,
            Tag = node.NodeId
        };

        // ⛔ <b>선택지 칸은 카드 안에 없다</b> (2026-09-16 소유자: "아래쪽으로 3개").
        //    카드 아래 철도 가지에 선다 — 그 자리가 선택지가 실제로 뻗어 나가는 자리이고,
        //    카드 안에 두면 이야기가 어디로 갈라지는지 한눈에 안 보인다.
        IReadOnlyList<GraphOutputPortProjection> inCard = node.OutputPorts
            .Where(port => port.Kind != GraphOutputPortKind.Choice)
            .ToList();

        var visual = new NodeCard(node.NodeId, node.NodeKind, card, inCard, style);

        // ⭐ 「조건 분기」 포트는 포트 줄에 서지 않는다 — 점이 글 칸의 <b>그 대사 높이</b>에 선다(아래 BuildLineKnob ·
        //    2026-10-30 소유자: *"어떤 대사에서 분기가 시작되는지, 대사의 정확한 위치에 분기 점이 생성되도록"*).
        for (int index = 0; index < inCard.Count; index++)
        {
            if (!visual.IsLinePort(index))
            {
                body.Children.Add(BuildPortRow(inCard[index], visual, index));
            }
        }

        // 글 칸은 포트 줄들 <b>아래</b>에 선다 — 포트의 자리 계산(PortAnchor)이 머리 바로 밑부터 센다.
        if (node.NodeKind == GraphNodeKind.Dialogue)
        {
            TextBox box = BuildWritingBox(node.NodeId);
            visual.WritingBox = box;
            body.Children.Add(box);

            // 글 칸이 있는 카드만 아래로 늘린다 — 모서리가 맨 위에 얹혀 두 가장자리가 만나는 자리를 받는다.
            frame.Children.Add(BuildResizeGrip(visual, ResizeEdges.Bottom));

            if (visual.HasLinePorts)
            {
                // 칸이 배치를 마칠 때마다(처음 · 크기 · 스크롤 · 글이 바뀔 때) 분기 점의 높이를 다시 잰다.
                box.LayoutUpdated += (_, _) => MeasureLineAnchors(visual);
            }
        }

        frame.Children.Add(BuildResizeGrip(visual, ResizeEdges.Right));

        if (visual.WritingBox is not null)
        {
            frame.Children.Add(BuildResizeGrip(visual, ResizeEdges.Right | ResizeEdges.Bottom));
        }

        Canvas.SetLeft(card, node.Position.X);
        Canvas.SetTop(card, node.Position.Y);
        GraphCanvas.Children.Add(card);

        for (int index = 0; index < inCard.Count; index++)
        {
            if (visual.IsLinePort(index))
            {
                Ellipse knob = BuildLineKnob(inCard[index], visual, index);
                visual.LineKnobs[inCard[index].Key] = knob;
                GraphCanvas.Children.Add(knob);
            }
        }

        // 한 대사(의 같은 쪽)에 분기가 둘 이상이면 그 점들을 공통 점 하나로 접는다 — 누르면 펼친다.
        foreach ((string group, IReadOnlyList<int> members) in visual.LineGroups)
        {
            if (members.Count < 2)
            {
                continue;
            }

            if (_expandedBranchGroups.Contains(BranchGroupId(visual.NodeId, group)))
            {
                visual.ExpandedGroups.Add(group);
            }

            BranchHub hub = BuildBranchHub(visual, group, members.Count);
            visual.LineHubs[group] = hub;
            GraphCanvas.Children.Add(hub.Fan);
            GraphCanvas.Children.Add(hub.Dot);
        }

        PlaceLineKnobs(visual);

        card.PointerPressed += (_, args) => OnCardPressed(visual, args);

        // 카드 더블클릭 = [구성] 탭의 그 에피소드로 간다(OnCardPressed → CardOpened — 첫 누름이 시작한 끌기는 놓을 때 끝난다).
        // ⛔ 옛 「무대에서 본다」는 2026-10-29에 무대가 ked-story-compositor로 가며 사라졌다.

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
        ScenarioWritingLayout layout = WorkingDialoguePreview.ScenarioWriting(_session!.Project, nodeId);
        string shown = layout.Text;
        _writingShown[nodeId] = shown;
        _writingLayouts[nodeId] = layout;

        var box = new TextBox
        {
            Text = shown,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = WritingHeightFor(nodeId),
            Margin = new Thickness(0, WritingBoxGap, 0, 0),
            Padding = new Thickness(4, 3),
            PlaceholderText = "화자: 대사 — 한 줄이 대사 한 줄",
            Tag = WritingTag + nodeId
        };
        ScrollViewer.SetVerticalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);

        // 글꼴 · 글자 크기 · 줄 간격은 설정 창 「대본 글 칸」의 것(writer-comfort WC5 — ⛔ 그 전에는 11 · 줄 높이 15로 굳었다). 분기 점은 실제 글자
        // 배치로 그 대사 높이를 재므로(MeasureLineAnchors) 어느 줄 간격에도 따라간다 — 줄 간격을 아주 좁히면 이웃 대사의 분기 점이 맞닿을 수 있다.
        ScriptFont.Apply(box);
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
            if (!ReferenceEquals(_writingBoxes.GetValueOrDefault(nodeId), box))
            {
                return;   // 판을 다시 그리느라 떼어 낸 칸이다
            }

            _liveWriting.Stop();

            if (PendingWriting(nodeId, box) is { } text)
            {
                Dispatcher.UIThread.Post(() => ApplyWriting(nodeId, box, text, live: false));
            }
        };

        // ⭐ <b>쓰는 대로 들어간다</b> (2026-10-30 소유자: *"에피소드 노드 속의 텍스트 편집하는 게 그대로 반영이 바로바로
        //    되기를"*) — 사람이 치다가 잠깐 멈추면(LiveWritingDelay) 넣는다. 곁기둥이 곧바로 따라온다. 줄을 지우는 것만은
        //    칸을 나올 때 넣는다 — 다 지우고 새로 쓰는 도중의 모습에서 줄을 은퇴시키면 그 줄의 분기 · 연출이 떨어져 나간다.
        box.TextChanged += (_, _) =>
        {
            if (box.IsFocused)
            {
                _liveWritingNodeId = nodeId;
                _liveWriting.Stop();
                _liveWriting.Start();
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
    /// <param name="live">
    /// 쓰는 도중에 넣는가(<see cref="CommitLiveWriting"/>). 그러면 줄을 지우는 글은 넣지 않고(칸을 나올 때 넣는다), 못
    /// 넣어도 칸을 붉게 하지 않으며(아직 쓰는 중이다), 칸의 글을 고치지 않고, 잘 들어갔다는 말도 하지 않는다. 초점
    /// 이탈 · 저장의 넣기는 넣은 뒤 칸을 도구의 표기로 다시 쓴다.
    /// </param>
    private void ApplyWriting(string nodeId, TextBox box, string text, bool live)
    {
        if (_session is not { } session || session.Project.FindDialogue(nodeId) is null)
        {
            return;
        }

        UiGuard.Run(session, "글 넣기", () =>
        {
            // 분기로 다녀오는 카드의 첫머리 조건 줄은 [구성] 탭이 정한다 — 글 칸에서 지우거나 고쳤으면 그 줄을 되살리고, 조건
            // 줄 바깥(위 · 아래)에 쓴 줄은 조건 안쪽으로 모은다.
            bool gateRestored = false;

            if (EntryGateOf(nodeId) is { } gate && KeepEntryGate(text, gate) is { } kept)
            {
                text = kept;
                gateRestored = true;
            }

            // 처음 보인 글의 줄 신원을 잇는다(2026-10-30) — 같은 자리에서 고친 줄은 그 줄이다. 이게 없으면 동기화가 내용만
            // 보고 짝을 맞추다 두 줄 이상 고친 구간에서 "확신할 수 없다"며 통째로 거절했다.
            string withIds = _writingLayouts.TryGetValue(nodeId, out ScenarioWritingLayout? shownLayout)
                ? ScenarioLineIdentity.CarryLineIds(shownLayout.Text, shownLayout.RowOfLine, text)
                : text;

            string? before = _writingShown.GetValueOrDefault(nodeId);
            ScenarioPasteOutcome outcome = session.Editor.ApplyScenarioText(
                nodeId, withIds, confirmDeletes: !live, strict: true);

            if (!outcome.Applied && live)
            {
                return;
            }

            if (!outcome.Applied)
            {
                IReadOnlyList<string> problems = outcome.Problems.Count > 0
                    ? outcome.Problems
                    : ["빈 글은 넣지 않습니다 — 줄을 모두 지우려면 노드를 지우세요."];

                MarkWriting(box, string.Join(Environment.NewLine, problems.Select(problem => $"• {problem}")));
                session.SetStatus($"글을 넣지 못했습니다 — {problems[0]} (글은 칸에 그대로 있습니다)");
                return;
            }

            // 기준(처음 보인 글과 그 줄 짝)을 지금 모델의 글로 옮긴다 — 다음 넣기가 이 줄 짝으로 신원을 잇는다. 판이 다시
            // 그려졌으면 새 칸이 이미 그 기준을 들고 있다.
            ScenarioWritingLayout now = WorkingDialoguePreview.ScenarioWriting(session.Project, nodeId);
            _writingShown[nodeId] = now.Text;
            _writingLayouts[nodeId] = now;

            TextBox current = _writingBoxes.GetValueOrDefault(nodeId) ?? box;
            MarkWriting(current, problem: null);

            if (live)
            {
                return;
            }

            // 다 쓰고 나왔다 — 칸을 도구의 표기(화자 띄어쓰기 · 빈 줄 걷기)로 다시 쓴다.
            if (!current.IsFocused)
            {
                current.Text = now.Text;
            }

            // 쓰는 동안 이미 다 들어갔으면(표기만 달랐다) 알릴 것이 없다.
            if (!string.Equals(before, now.Text, StringComparison.Ordinal))
            {
                session.SetStatus($"글을 넣었습니다 — {outcome.Summary()}. 되돌리기 한 번이면 돌아갑니다.");
            }

            if (gateRestored)
            {
                session.SetStatus("분기 조건 줄을 지켰습니다 — 조건 줄은 되살리고 바깥에 쓴 줄은 조건 안쪽으로 모았습니다. " +
                                  "이 카드에 들어갈 조건은 [구성] 탭의 분기 칸에서 바꿉니다.");
            }
        });
    }

    /// <summary>
    /// 그 카드의 첫머리 조건 줄 — 분기로 다녀오는 카드에 조건이 걸려 있을 때만이다. 글 칸에 보인 그대로의 두 줄(바깥쪽
    /// <c>&lt;&lt;if …&gt;&gt;</c>과 그 짝 <c>&lt;&lt;endif&gt;&gt;</c>)이다.
    /// <list type="bullet">
    /// <item>[구성] 탭이 아는 모양(첫 줄 <c>if</c> 하나 + 꼬리 <c>endif</c> 하나)이면 그 두 줄이다.</item>
    /// <item>그 모양이 이미 깨졌으면(조건 줄 바깥에 줄이 있다 — [구성] 탭이 「여기서 고칠 수 없습니다」를 말한다) 글 칸의 처음
    /// <c>if</c> 줄과 마지막 <c>endif</c> 줄이다 — 다음에 넣을 때 바깥 줄을 안쪽으로 모아 바로잡는다.</item>
    /// </list>
    /// 조건이 없는 분기 카드에 작가가 중간에 쓴 조건은 건드리지 않는다(그때는 [구성] 탭이 「언제나」로 읽는다).
    /// </summary>
    private (string Head, string Tail)? EntryGateOf(string nodeId)
    {
        if (_session is not { } session ||
            !_branchEntries.ContainsKey(nodeId) ||
            _writingShown.GetValueOrDefault(nodeId) is not { } shown)
        {
            return null;
        }

        BranchEntryCondition.State state = BranchEntryCondition.Read(session.Project, nodeId);

        if (state is not { Label: not null, Editable: true } and not { Editable: false })
        {
            return null;
        }

        List<string> lines = WritingLines(shown);
        int head = lines.FindIndex(IsIfLine);
        int tail = lines.FindLastIndex(IsEndIfLine);

        return head >= 0 && tail > head ? (lines[head].Trim(), lines[tail].Trim()) : null;
    }

    /// <summary>
    /// 첫머리 조건 줄을 지킨 글 — 조건 줄이 맨 처음과 맨 끝에 보인 그대로 제 줄로 있으면 <c>null</c>(손댈 것이 없다). 아니면:
    /// <list type="bullet">
    /// <item>조건 줄과 같은 줄에 붙여 쓴 글(「&lt;&lt;if …&gt;&gt;ASD」 · 「SAD&lt;&lt;endif&gt;&gt;」)은 떼어 제 줄로 세운다.</item>
    /// <item>지우거나 고친 조건 줄은 보인 그대로 되살린다.</item>
    /// <item>조건 줄 위 · 아래(바깥)에 쓴 줄은 조건 안쪽으로 모은다 — 위에 쓴 줄은 안쪽 맨 위, 아래에 쓴 줄은 안쪽 맨 아래다.</item>
    /// </list>
    /// 사이의 대사는 사람이 쓴 그대로다. 대사를 다 지웠으면 손대지 않는다 — 빈 글은 넣지 않는 규칙이 그대로 받는다.
    /// </summary>
    private static string? KeepEntryGate(string text, (string Head, string Tail) gate)
    {
        List<string> original = WritingLines(text);
        List<string> lines = SplitGateTokens(original);

        // 바깥 if — 보인 그대로면 어디에 있든 그 줄이고, 고쳤으면 맨 첫 줄의 if다(지웠으면 없다).
        int head = lines.FindIndex(line => string.Equals(line.Trim(), gate.Head, StringComparison.Ordinal));

        if (head < 0 && lines.Count > 0 && IsIfLine(lines[0]))
        {
            head = 0;
        }

        if (head >= 0)
        {
            lines.RemoveAt(head);
        }

        // 바깥 endif — 안쪽 if보다 endif가 하나 더 많으면 마지막 endif가 바깥 것이다(지웠으면 수가 같다).
        if (lines.Count(IsEndIfLine) > lines.Count(IsIfLine))
        {
            lines.RemoveAt(lines.FindLastIndex(IsEndIfLine));
        }

        if (lines.All(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        List<string> kept = [gate.Head, .. lines, gate.Tail];

        return kept.Select(line => line.Trim()).SequenceEqual(original.Select(line => line.Trim()), StringComparer.Ordinal)
            ? null
            : string.Join('\n', kept);
    }

    /// <summary>조건 줄의 꼴 — 같은 줄에 붙은 글과 떼어 낼 때 쓴다(조건식의 <c>&gt;=</c>는 <c>&gt;&gt;</c>가 아니라 걸리지 않는다).</summary>
    [GeneratedRegex("(<<if\\b.*?>>|<<endif>>)")]
    private static partial Regex GateToken();

    /// <summary>
    /// 조건 줄(<c>&lt;&lt;if …&gt;&gt;</c> · <c>&lt;&lt;endif&gt;&gt;</c>)에 같은 줄로 붙여 쓴 글을 떼어 제 줄로 세운다 —
    /// 「&lt;&lt;if …&gt;&gt;ASD」는 「&lt;&lt;if …&gt;&gt;」와 「ASD」 두 줄이다. 조건 줄이 없는 줄은 그대로다.
    /// </summary>
    private static List<string> SplitGateTokens(List<string> lines) =>
        [.. lines.SelectMany(line => GateToken().IsMatch(line)
            ? GateToken().Split(line).Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim())
            : [line])];

    /// <summary>글 칸의 줄들 — 앞뒤의 빈 줄은 뺀다.</summary>
    private static List<string> WritingLines(string text)
    {
        List<string> lines = [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static bool IsIfLine(string line) => line.TrimStart().StartsWith("<<if", StringComparison.Ordinal);

    private static bool IsEndIfLine(string line) => string.Equals(line.Trim(), "<<endif>>", StringComparison.Ordinal);

    /// <summary>쓰다가 이만큼 멈추면 넣는다.</summary>
    private static readonly TimeSpan LiveWritingDelay = TimeSpan.FromMilliseconds(400);

    private readonly DispatcherTimer _liveWriting = new() { Interval = LiveWritingDelay };

    /// <summary>쓰는 중인 글 칸의 노드 — 멈추면 넣을 곳.</summary>
    private string? _liveWritingNodeId;

    /// <summary>
    /// 쓰다가 멈춘 글 칸의 글을 넣는다(<see cref="LiveWritingDelay"/>가 지나면 저절로 — 화면 시험은 이것을 바로 부른다).
    /// 넣지 않은 글이 없으면 아무것도 하지 않는다.
    /// </summary>
    internal void CommitLiveWriting()
    {
        _liveWriting.Stop();

        if (_liveWritingNodeId is { } nodeId &&
            _writingBoxes.TryGetValue(nodeId, out TextBox? box) &&
            PendingWriting(nodeId, box) is { } text)
        {
            ApplyWriting(nodeId, box, text, live: true);
        }
    }

    /// <summary>
    /// 저장 전에 — 초점이 있는 글 칸의 넣지 않은 글을 먼저 넣는다(Ctrl+S도 넣는 때다, §7 D4).
    /// 셸의 저장이 부른다.
    /// </summary>
    internal void FlushWriting()
    {
        if (CaptureWritingFocus() is { Dirty: true } focus && _writingBoxes.TryGetValue(focus.NodeId, out TextBox? box))
        {
            ApplyWriting(focus.NodeId, box, focus.Text, live: false);
        }
    }

    /// <summary>카드 글 칸에 초점이 있는가 — 셸의 Ctrl+Z · Ctrl+Y가 칸 대신 <see cref="UndoInWriting"/>으로 가는 때.</summary>
    internal bool IsWritingFocused => CaptureWritingFocus() is not null;

    /// <summary>
    /// 카드 글 칸의 Ctrl+Z · Ctrl+Y — 프로젝트 되돌리기 · 다시 실행이다. 칸은 쓰다 멈출 때마다 글을 넣고(넣을 때마다 되돌리기
    /// 한 칸), 줄이 늘고 줄면 판을 다시 그려 새 칸이 되므로 칸 자체의 되돌리기 기록은 비어 있다.
    /// <list type="bullet">
    /// <item>아직 넣지 않은 글이 있으면 Ctrl+Z는 그것부터 걷는다 — 마지막으로 넣은 글로(Esc와 같다). Ctrl+Y는 하지 않는다.</item>
    /// <item>되돌린 뒤 판을 다시 그리지 않아 칸이 그대로면 칸을 모델의 글로 다시 채운다 — 쓰는 중인 칸은 다시 채우지 않는
    /// 규칙(<see cref="RefreshWriting"/>)이 있어, 그냥 두면 되돌린 글이 칸에 보이지 않는다. 커서는 그 자리(글 끝을 넘지 않게)다.</item>
    /// </list>
    /// </summary>
    internal void UndoInWriting(bool redo)
    {
        if (_session is not { } session ||
            CaptureWritingFocus() is not { } focus ||
            !_writingBoxes.TryGetValue(focus.NodeId, out TextBox? box))
        {
            return;
        }

        _liveWriting.Stop();

        if (focus.Dirty)
        {
            if (!redo)
            {
                box.Text = _writingShown.GetValueOrDefault(focus.NodeId, box.Text ?? string.Empty);
                MarkWriting(box, problem: null);
                PlaceCaret(box, focus);
            }

            return;
        }

        if (redo)
        {
            session.Editor.Redo();
        }
        else
        {
            session.Editor.Undo();
        }

        // 판을 다시 그렸으면 Rebuild가 새 칸에 모델의 글과 초점을 이미 줬다.
        if (!ReferenceEquals(_writingBoxes.GetValueOrDefault(focus.NodeId), box) ||
            session.Project.FindDialogue(focus.NodeId) is null)
        {
            return;
        }

        ScenarioWritingLayout layout = WorkingDialoguePreview.ScenarioWriting(session.Project, focus.NodeId);

        if (string.Equals(layout.Text, box.Text, StringComparison.Ordinal))
        {
            return;
        }

        _writingShown[focus.NodeId] = layout.Text;
        _writingLayouts[focus.NodeId] = layout;
        box.Text = layout.Text;
        _liveWriting.Stop();
        PlaceCaret(box, focus);
    }

    private static void PlaceCaret(TextBox box, WritingFocus focus)
    {
        int length = box.Text?.Length ?? 0;
        box.SelectionStart = Math.Clamp(focus.SelectionStart, 0, length);
        box.SelectionEnd = Math.Clamp(focus.SelectionStart, 0, length);
    }

    /// <summary>
    /// 모든 카드의 글 칸을 모델의 글로 다시 채운다 — 글을 되돌리거나 다시 했을 때(판은 다시 안 그린다 · board-speed PF1). 칸마다
    /// <see cref="RefreshWriting"/>의 규칙 그대로다 — 쓰는 중인 칸 · 넣지 않은 글이 있는 칸은 건드리지 않는다.
    /// </summary>
    internal void RefreshAllWriting()
    {
        foreach (string nodeId in _writingBoxes.Keys.ToList())
        {
            RefreshWriting(nodeId);
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

        ScenarioWritingLayout layout = WorkingDialoguePreview.ScenarioWriting(_session.Project, nodeId);
        _writingShown[nodeId] = layout.Text;
        _writingLayouts[nodeId] = layout;
        box.Text = layout.Text;
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

        // 열린 선택지 문구 칸도 딴 데를 누르면 닫는다 — 빈 판 · 카드는 초점을 받지 않아 칸이 초점을 쥔 채 남는다.
        FinishSlotLater();
    }

    /// <summary>
    /// 열린 선택지 문구 칸을 Enter처럼 닫는다 — 적은 문구는 넣고, 아무것도 안 적었으면 그냥 닫힌다. 닫기는 판을 다시 그리므로
    /// 지금 도는 누르기 · 초점 처리가 끝난 뒤로 미룬다(§5-13). 그 사이에 칸이 이미 닫혔거나 새로 열렸으면 하지 않는다.
    /// </summary>
    private void FinishSlotLater()
    {
        if (_finishSlot is not { } finish)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => UiGuard.Run(_session, "선택지 문구", () =>
        {
            if (ReferenceEquals(_finishSlot, finish))
            {
                finish();
            }
        }));
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

        content.Children.Add(ProxyHeader(
            file.FileName,
            $"접힘 · {file.Nodes.Count}개 노드 · {file.RelativePath}",
            new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))));

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
            if (_session?.Project.FindFile(file.FileId) is { Nodes.Count: > 0 } storyFile)
            {
                BeginProxyDrag(visual, args, file.FileId, sceneId: null, storyFile.Nodes);
            }
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
    /// 펼친 판 안의 <b>접은 장면</b> 상자 (2026-10-30 — 하이어아키의 눈 · <c>docs/plans/hierarchy.md</c> SF). 접힌 판의 상자와
    /// 줄 · 치수가 같아 간선 끝 셈법(<see cref="ProxyRowAnchor"/>)이 하나다 — 다른 것은 머리(장면 색 · 이름 · 안쪽 길 수)뿐이고
    /// 장면 머리글이 없다(상자 하나가 장면 하나다).
    ///
    /// 더블클릭하면 편다. 끌면 그 장면의 카드들이 함께 옮겨진다(펴면 그 자리에 있다).
    /// </summary>
    private FileProxyVisual BuildSceneProxy(CollapsedSceneProjection scene)
    {
        var content = new StackPanel { Spacing = 0 };
        content.Children.Add(ProxyHeader(
            $"장면 {scene.DisplayName}",
            $"접힘 · {scene.Nodes.Count}개 노드" + (scene.InnerCount > 0 ? $" · 안쪽 길 {scene.InnerCount}개" : string.Empty),
            new SolidColorBrush(SceneColor)));

        var rows = new List<ProxyNodeRow>();

        foreach (CollapsedNodeEntry entry in scene.Nodes)
        {
            ProxyNodeRow row = BuildProxyRow(entry, rows.Count);
            rows.Add(row);
            content.Children.Add(row.Visual);
        }

        var visual = new Border
        {
            Width = ProxyWidth,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF7, 0xEC)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(170, SceneColor.R, SceneColor.G, SceneColor.B)),
            BorderThickness = new Thickness(1),
            ClipToBounds = false,
            Child = content,
            Tag = SceneProxyTag + scene.FileId + "/" + scene.SceneId,
            Cursor = new Cursor(StandardCursorType.SizeAll)
        };

        Canvas.SetLeft(visual, scene.Position.X);
        Canvas.SetTop(visual, scene.Position.Y);
        GraphCanvas.Children.Add(visual);

        visual.PointerPressed += (_, args) =>
        {
            if (_session?.Project.FindFile(scene.FileId) is not { } storyFile)
            {
                return;
            }

            var inside = scene.Nodes.Select(entry => entry.NodeId).ToHashSet(StringComparer.Ordinal);
            BeginProxyDrag(visual, args, scene.FileId, scene.SceneId, storyFile.Nodes.Where(node => inside.Contains(node.Id)));
        };

        ToolTip.SetTip(visual, "더블클릭하면 이 장면을 펼칩니다.");
        visual.DoubleTapped += (_, args) =>
        {
            args.Handled = true;
            CancelDrag(); // 둘째 누름이 시작한 끌기 상태를 걷는다
            _session?.SetSceneCollapsed(scene.FileId, scene.SceneId, collapsed: false);
        };

        return new FileProxyVisual(scene.FileId, visual, rows, Math.Max(1, rows.Count), scene.SceneId);
    }

    /// <summary>상자 머리 — 색 점 · 이름 · 한 줄 설명. 접힌 판과 접은 장면이 함께 쓴다.</summary>
    private static Grid ProxyHeader(string title, string subtitle, IBrush dot)
    {
        var headerDot = new Ellipse
        {
            Width = 11,
            Height = 11,
            Fill = dot,
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
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                new TextBlock
                {
                    Text = subtitle,
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

        return header;
    }

    /// <summary>
    /// 상자 끌기를 시작한다 — 상자 자리는 안 노드들의 자리에서 나오므로(판은 평균, 장면은 왼쪽 위) 끌기는 그 노드들을
    /// 같은 만큼 옮기는 것이다. 놓을 때 한 번 적는다(<see cref="CommitDrag"/>).
    /// </summary>
    private void BeginProxyDrag(
        Border visual, PointerPressedEventArgs args, string fileId, string? sceneId, IEnumerable<StoryNode> nodes)
    {
        if (!args.GetCurrentPoint(visual).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggingProxyFileId = fileId;
        _draggingProxySceneId = sceneId;
        _proxyDragStart = args.GetPosition(GraphCanvas);
        _proxyPositionStart = new GraphPosition(Canvas.GetLeft(visual), Canvas.GetTop(visual));
        _proxyStartPositions.Clear();

        foreach (StoryNode node in nodes)
        {
            _proxyStartPositions[node.Id] = new Point(node.Layout.X, node.Layout.Y);
        }

        args.Handled = true;
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
            Text = $"{CardStyleFor(entry.NodeKind, _sceneRootNodeIds.Contains(entry.NodeId), IsBranchCard(entry.NodeId)).Icon} {ShownName(entry.NodeId, entry.NodeName)}",
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

    /// <summary>문구 입력칸의 가장 좁은 폭 — 빈 칸에도 적을 자리가 보이게.</summary>
    private const double SlotEditorMinWidth = 90;

    /// <summary>빈 선택지 칸의 글 — 회색으로 선다. 시험이 이 글로 찾는다.</summary>
    internal const string SlotPlaceholderText = "빈 선택지";

    /// <summary>빈 선택지 칸(네모)의 Tag — 칸 줄 안에서 글자 대신 선 그 칸을 찾는다.</summary>
    internal const string SlotPlaceholderTag = "slot-placeholder";

    private static readonly IBrush SlotPlaceholderInk = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
    private static readonly IBrush SlotPlaceholderBorder = new SolidColorBrush(Color.FromRgb(0xB8, 0xBC, 0xC4));

    /// <summary>문구 입력칸의 여유 — 테두리 · 안쪽 여백 · 커서 자리.</summary>
    private const double SlotEditorSlack = 24;

    /// <summary>열린 문구 입력칸을 Enter처럼 닫는 일 — 칸이 열려 있을 때만 있다.</summary>
    private Action? _finishSlot;

    /// <summary>
    /// 문구 입력칸 — Enter 확정 · Esc 취소. 딴 데(빈 판 · 카드)를 누르거나 초점이 떠나면 <b>Enter처럼</b> 닫는다 — 적은
    /// 문구는 넣고, 아무것도 안 적었으면 그냥 닫힌다(<see cref="FinishSlotLater"/>).
    ///
    /// 이미 이어진 길이면 <b>그 간선의 문구를 고치고</b>, 아직 안 이었으면 적어만 둔다.
    /// 어느 쪽이든 <b>챕터의 선택지 사전에 배운다</b> — 문구의 주인은 챕터다(v9).
    /// </summary>
    private Control SlotEditor(GraphChoicePort choice)
    {
        // 폭을 정해 둔다 — 판은 칸 줄의 폭을 재서 레인이 나갈 자리를 정하는데(DrawRailBranch), 판에 붙기 전의 글자 칸은 틀이
        // 아직 없어 제 폭을 작게 잰다. 그러면 레인이 글자 칸 위를 지나간다. 지금 문구의 폭에 여유를 더한다(적는 동안 넘치면
        // 칸 안에서 밀린다).
        var probe = new TextBlock { Text = LabelOf(choice), FontSize = 10 };
        probe.Measure(Size.Infinity);

        var box = new TextBox
        {
            Text = LabelOf(choice),
            FontSize = 10,
            Padding = new Thickness(3, 0),
            MinHeight = 0,
            Width = Math.Max(SlotEditorMinWidth, Math.Ceiling(probe.DesiredSize.Width) + SlotEditorSlack),
            VerticalAlignment = VerticalAlignment.Center
        };

        void Commit(bool keep)
        {
            _editingSlot = null;
            SlotLabelBox = null;
            _finishSlot = null;

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
                FinishSlotLater();
            }
        });

        SlotLabelBox = box;
        _finishSlot = () => Commit(keep: true);
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
            _multiSelected.Clear(); // 선 하나를 고르면 카드 무리는 풀린다
            HighlightSelection();
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
            to = new Point(Canvas.GetLeft(targetCard.Visual) + targetCard.Visual.Width, to.Y);
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

            case GraphEndpointKind.CollapsedSceneNodeOutput:
            case GraphEndpointKind.CollapsedSceneNodeInput:
            {
                if (FindSceneProxy(endpoint.FileId, endpoint.SceneId) is { } proxy && endpoint.ProxyRowIndex is { } rowIndex)
                {
                    point = ProxyRowAnchor(
                        proxy,
                        rowIndex,
                        output: endpoint.Kind == GraphEndpointKind.CollapsedSceneNodeOutput);
                    return true;
                }

                break;
            }
        }

        point = default;
        return false;
    }

    /// <summary>
    /// 펼쳐진 노드 카드의 출력 포트 좌표 — 왼쪽 포트(<see cref="GraphOutputPortProjection.OnLeft"/>)면 왼쪽 가장자리다.
    /// 포트 줄의 포트는 그 줄의 가운데, 「조건 분기」는 글 칸의 그 대사 높이다(<see cref="MeasureLineAnchors"/>).
    /// </summary>
    private static Point PortAnchor(NodeCard card, int portIndex)
    {
        double x = Canvas.GetLeft(card.Visual) + (card.Ports[portIndex].OnLeft ? 0 : card.Visual.Width);

        if (card.IsLinePort(portIndex))
        {
            Point line = LineAnchor(card, portIndex);
            string group = card.GroupOf(portIndex);
            IReadOnlyList<int> members = card.LineGroups[group];

            // 접혀 있으면(또는 혼자면) 그 대사 옆 — 공통 점에서 나간다. 펼쳤으면 카드 밖으로 갈라진 제 점에서.
            if (members.Count < 2 || !card.ExpandedGroups.Contains(group))
            {
                return line;
            }

            int order = members.ToList().IndexOf(portIndex);
            double outward = card.Ports[portIndex].OnLeft ? -1 : 1;

            return new Point(
                line.X + (outward * BranchFanOut),
                line.Y + ((order - ((members.Count - 1) / 2.0)) * BranchFanStep));
        }

        double y = Canvas.GetTop(card.Visual)
            + CardBorder + CardPadding + HeaderHeight
            + (card.RowOf(portIndex) * PortRowHeight) + (PortRowHeight / 2);

        return new Point(x, y);
    }

    /// <summary>
    /// 「조건 분기」의 점 — 카드 가장자리, 글 칸의 그 대사 높이에 선다(<see cref="PlaceLineKnobs"/>). 카드 밖(판)에 서는
    /// 까닭: 글 칸은 줄이 접히고 스크롤해서 높이가 배치를 마친 뒤에야 정해진다 — 포트 줄처럼 카드 안 줄로 지을 수 없다.
    /// 누르면 포트 줄의 점과 같다(끌어 다른 카드로 잇는다).
    /// </summary>
    private Ellipse BuildLineKnob(GraphOutputPortProjection port, NodeCard card, int index)
    {
        IBrush brush = BranchPalette.Accent(port.PaletteIndex);
        var knob = new Ellipse
        {
            Width = PortRadius * 2,
            Height = PortRadius * 2,
            Fill = port.IsConnected ? brush : Brushes.Transparent,
            Stroke = brush,
            StrokeThickness = 2,
            Cursor = new Cursor(StandardCursorType.Hand),
            ZIndex = 50,
            Tag = LineKnobTag + port.Key
        };
        ToolTip.SetTip(knob, port.IsConnected
            ? "분기 — 이 대사 앞에서 다녀온다. 끌어 다른 카드로 옮겨 잇고, 누르면 연결만 끊긴다(분기점은 남는다)."
            : "이어지지 않은 분기점 — 끌어 카드에 놓으면 분기가 된다. 지우려면 곁기둥의 분기점 카드에서 [✕].");

        knob.PointerPressed += (_, args) => OnPortPressed(port, card, index, args);
        return knob;
    }

    /// <summary>분기 점이 지는 표 — 화면 테스트가 이것으로 찾는다.</summary>
    internal const string LineKnobTag = "detour-knob:";

    /// <summary>분기 점의 그 대사 옆 자리 — 카드 가장자리, 글 칸의 그 대사 높이(<see cref="MeasureLineAnchors"/>).</summary>
    private static Point LineAnchor(NodeCard card, int portIndex)
    {
        double x = Canvas.GetLeft(card.Visual) + (card.Ports[portIndex].OnLeft ? 0 : card.Visual.Width);

        // 아직 재지 않았으면(첫 배치 전) 글 칸의 위 끝이다.
        double fallback = CardBorder + CardPadding + HeaderHeight + (card.RowCount * PortRowHeight) +
                          WritingBoxGap + PortRadius + 2;
        string? lineId = card.Ports[portIndex].ExecutionPort?.BranchOpenLineId;

        return new Point(
            x,
            Canvas.GetTop(card.Visual) + (lineId is null ? fallback : card.LineAnchorY.GetValueOrDefault(lineId, fallback)));
    }

    /// <summary>
    /// 그 카드의 분기 점들을 지금 자리(<see cref="PortAnchor"/>)에 놓는다. 한 대사(의 같은 쪽)에 분기가 둘 이상이면 접힌
    /// 동안은 공통 점 하나만 서고 개별 점은 숨는다. 펼치면 공통 점에서 가지가 갈라져 개별 점들이 카드 밖에 세로로 선다.
    /// </summary>
    private static void PlaceLineKnobs(NodeCard card)
    {
        foreach ((string key, Ellipse knob) in card.LineKnobs)
        {
            int index = card.PortIndex(key);

            if (index < 0)
            {
                continue;
            }

            string group = card.GroupOf(index);
            Point at = PortAnchor(card, index);
            Canvas.SetLeft(knob, at.X - PortRadius);
            Canvas.SetTop(knob, at.Y - PortRadius);
            knob.Opacity = LineOpacity(card, index);
            knob.IsVisible = card.LineGroups[group].Count < 2 || card.ExpandedGroups.Contains(group);
        }

        foreach ((string group, BranchHub hub) in card.LineHubs)
        {
            IReadOnlyList<int> members = card.LineGroups[group];
            Point at = LineAnchor(card, members[0]);
            bool expanded = card.ExpandedGroups.Contains(group);

            Canvas.SetLeft(hub.Dot, at.X - BranchHubRadius);
            Canvas.SetTop(hub.Dot, at.Y - BranchHubRadius);
            hub.Dot.Opacity = LineOpacity(card, members[0]);
            hub.Dot.Background = expanded ? BranchHubOpenBrush : BranchHubBrush;

            hub.Fan.IsVisible = expanded;

            if (!expanded)
            {
                continue;
            }

            // 공통 점 → 가지 기둥 → 개별 점. 선택지 칸이 레일에서 갈라지는 모양 그대로.
            double outward = card.Ports[members[0]].OnLeft ? -1 : 1;
            double stem = at.X + (outward * (BranchHubRadius + ((BranchFanOut - BranchHubRadius - PortRadius) / 2)));
            var figures = new PathFigures();

            foreach (int member in members)
            {
                Point knob = PortAnchor(card, member);
                figures.Add(new PathFigure
                {
                    StartPoint = new Point(at.X + (outward * BranchHubRadius), at.Y),
                    IsClosed = false,
                    IsFilled = false,
                    Segments =
                    [
                        new LineSegment { Point = new Point(stem, at.Y) },
                        new LineSegment { Point = new Point(stem, knob.Y) },
                        new LineSegment { Point = new Point(knob.X - (outward * PortRadius), knob.Y) }
                    ]
                });
            }

            hub.Fan.Data = new PathGeometry { Figures = figures };
            hub.Fan.Opacity = hub.Dot.Opacity;
        }
    }

    /// <summary>그 대사가 글 칸 밖으로 스크롤돼 칸 끝에 붙었으면 흐리게.</summary>
    private static double LineOpacity(NodeCard card, int portIndex) =>
        card.Ports[portIndex].ExecutionPort?.BranchOpenLineId is { } lineId && card.LineOutOfView.Contains(lineId) ? 0.45 : 1;

    private static readonly IBrush BranchHubBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
    private static readonly IBrush BranchHubOpenBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

    /// <summary>접은 공통 점이 지는 표 — <c>{접두}{노드}:{대사 LineId}</c>(왼쪽으로 나가는 무리면 끝에 <c>:left</c>).</summary>
    internal const string BranchHubTag = "detour-hub:";

    /// <summary>
    /// 펼쳐 둔 분기 무리 — 노드와 무리(<see cref="NodeCard.GroupOf"/>)로. 판을 다시 그려도 펼친 채로 남는다(화면의 일이라
    /// 저장하지 않는다).
    /// </summary>
    private readonly HashSet<string> _expandedBranchGroups = new(StringComparer.Ordinal);

    private static string BranchGroupId(string nodeId, string group) => $"{nodeId}:{group}";

    /// <summary>한 대사의 분기들을 접은 공통 점과, 펼쳤을 때 개별 점으로 갈라지는 가지.</summary>
    private sealed record BranchHub(Border Dot, ShapePath Fan);

    /// <summary>
    /// 공통 점 — 그 대사 옆에 서고 분기 수를 보인다(2026-10-30 소유자: *"특정 글자 옆쪽에 분기점 표시 하나만 공통으로
    /// 남기고, 그걸 클릭하면, 선택지 분기를 갈라주는 것처럼 펼쳐지면서 개별 분기점을 끌어다 놓을 수 있게"*). 누르면 펼치고
    /// 다시 누르면 접는다. 접힌 동안 그 분기 선들은 이 점에서 나간다.
    /// </summary>
    private BranchHub BuildBranchHub(NodeCard card, string group, int count)
    {
        var dot = new Border
        {
            Width = BranchHubRadius * 2,
            Height = BranchHubRadius * 2,
            CornerRadius = new CornerRadius(BranchHubRadius),
            Background = BranchHubBrush,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5),
            Cursor = new Cursor(StandardCursorType.Hand),
            ZIndex = 51,
            Tag = BranchHubTag + card.NodeId + ":" + group,
            Child = new TextBlock
            {
                Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        ToolTip.SetTip(dot, $"분기 {count}개 — 눌러 펼치고 접는다. 펼친 점을 끌어 다른 카드로 잇는다.");

        var fan = new ShapePath
        {
            Stroke = BranchHubOpenBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
            IsVisible = false,
            ZIndex = 49,
            Tag = BranchHubTag + card.NodeId + ":" + group + ":fan"
        };

        dot.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(dot).Properties.IsLeftButtonPressed)
            {
                return;
            }

            args.Handled = true;
            ToggleBranchGroup(card, group);
        };

        return new BranchHub(dot, fan);
    }

    /// <summary>공통 점을 눌렀다 — 펼치거나 접고, 점과 그 카드에서 나가는 선을 다시 놓는다.</summary>
    private void ToggleBranchGroup(NodeCard card, string group)
    {
        string id = BranchGroupId(card.NodeId, group);

        if (!card.ExpandedGroups.Remove(group))
        {
            card.ExpandedGroups.Add(group);
            _expandedBranchGroups.Add(id);
        }
        else
        {
            _expandedBranchGroups.Remove(id);
        }

        PlaceLineKnobs(card);

        foreach (EdgeVisual edge in _edges.Where(edge =>
                     string.Equals(edge.Connection.Source.NodeId, card.NodeId, StringComparison.Ordinal)))
        {
            PositionEdge(edge);
        }
    }

    /// <summary>
    /// 분기 점의 높이를 잰다 — 글 칸에서 <b>그 대사</b>의 첫 화면 줄 가운데(줄이 접혀도 · 스크롤해도 실제 글자 배치로).
    /// 대사마다 잰다 — 한 대사의 분기들은 그 높이의 공통 점 하나로 접힌다. 그 대사가 칸 밖으로 스크롤돼 있으면 칸의 위 ·
    /// 아래 끝에 붙고 흐려진다. 바뀌었으면 점과 그 카드에서 나가는 선을 다시 놓는다.
    /// </summary>
    private void MeasureLineAnchors(NodeCard card)
    {
        if (card.WritingBox is not { } box ||
            box.FindDescendantOfType<Avalonia.Controls.Presenters.TextPresenter>() is not { } presenter ||
            box.TranslatePoint(default, card.Visual) is not { } boxTop ||
            box.Bounds.Height <= 0)
        {
            return;
        }

        string text = box.Text ?? string.Empty;
        IReadOnlyDictionary<string, int> rows =
            _writingLayouts.GetValueOrDefault(card.NodeId)?.RowOfLine ?? new Dictionary<string, int>();
        double top = boxTop.Y + BranchHubRadius + 1;
        double bottom = boxTop.Y + box.Bounds.Height - BranchHubRadius - 1;
        bool changed = false;

        foreach (string key in card.BranchLines)
        {
            double wanted = top;

            if (rows.TryGetValue(key, out int row) &&
                presenter.TranslatePoint(new Point(0, RowMiddle(presenter, text, row)), card.Visual) is { } at)
            {
                wanted = at.Y;
            }

            double y = Math.Clamp(wanted, top, Math.Max(top, bottom));
            bool outOfView = Math.Abs(y - wanted) > 0.5;

            if (!card.LineAnchorY.TryGetValue(key, out double was) || Math.Abs(was - y) > 0.5 ||
                card.LineOutOfView.Contains(key) != outOfView)
            {
                card.LineAnchorY[key] = y;
                changed = true;

                if (outOfView)
                {
                    card.LineOutOfView.Add(key);
                }
                else
                {
                    card.LineOutOfView.Remove(key);
                }
            }
        }

        if (!changed)
        {
            return;
        }

        PlaceLineKnobs(card);

        foreach (EdgeVisual edge in _edges.Where(edge =>
                     string.Equals(edge.Connection.Source.NodeId, card.NodeId, StringComparison.Ordinal)))
        {
            PositionEdge(edge);
        }
    }

    /// <summary>글의 <paramref name="row"/>번째 줄(0부터)이 선 화면 줄의 가운데 높이 — 글 칸 안 좌표.</summary>
    private static double RowMiddle(Avalonia.Controls.Presenters.TextPresenter presenter, string text, int row)
    {
        int index = 0;

        for (int seen = 0; seen < row && index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                seen++;
            }
        }

        Rect caret = presenter.TextLayout.HitTestTextPosition(Math.Min(index, text.Length));
        return caret.Y + (caret.Height / 2);
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

    /// <summary>
    /// 휠 — 배율이다(누른 키와 무관하게 · 2026-08-18 팀장 미팅에서 Ctrl 요구가 빠졌다). 설정 「휠로 판 옮기기」를 켜면(맥은 처음부터) 판을 옮기고
    /// 배율은 Ctrl(맥은 ⌘도)+휠이다. 무엇을 얼마나 하는가는 <see cref="BoardWheel"/> 한 벌 — 배율은 신호의 크기에 비례한다.
    /// 옮기기도 여기서 한다(스크롤에 맡기지 않는다) — 고르지 않은 카드의 글 칸이 휠을 먹지 않게.
    /// </summary>
    private void OnGraphWheel(object? sender, PointerWheelEventArgs args)
    {
        if (WheelBelongsToBox(args.Source))
        {
            return;   // 고른 카드의 글 칸 위다 — 칸이 스크롤한다
        }

        BoardWheel.Move move = BoardWheel.Read(args);

        if (move.Zoom != 1)
        {
            ApplyZoom(_zoom * move.Zoom, args.GetPosition(GraphScroll));
        }

        if (move.Pan != default)
        {
            GraphScroll.Offset = new Vector(
                Math.Max(0, GraphScroll.Offset.X + move.Pan.X),
                Math.Max(0, GraphScroll.Offset.Y + move.Pan.Y));
        }

        args.Handled = true;
    }

    /// <summary>트랙패드 집기(맥) — 늘 배율이다. 글 칸 위에서도 판의 것이다(집기로 글을 스크롤하지는 않는다).</summary>
    private void OnGraphMagnify(object? sender, PointerDeltaEventArgs args)
    {
        ApplyZoom(_zoom * BoardWheel.Magnify(args.Delta), args.GetPosition(GraphScroll));
        args.Handled = true;
    }

    /// <summary>
    /// <b>이 휠이 글 칸의 것인가</b> (2026-11-17 소유자) — <b>고른 카드</b>의 글 칸 위이거나, 초점을 쥔 칸(적고 있는 선택지 문구 칸) 위일
    /// 때만이다. 그때는 칸이 스크롤하고(스크롤할 것이 없으면 아무 일도 없다) 판은 확대도 스크롤도 하지 않는다. 고르지 않은 카드의 글 칸
    /// 위에서는 칸 밖과 같이 판을 확대한다 — 카드는 거의 글 칸이라, 칸 위라는 이유로 휠을 먹으면 판을 확대할 자리가 없다
    /// (⛔ 전에는 모든 글 칸이 휠을 먹어 글이 칸에 다 들어간 카드 위에서는 휠이 아무 일도 안 했다).
    /// </summary>
    private bool WheelBelongsToBox(object? source)
    {
        if ((source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is not { } box)
        {
            return false;
        }

        if (box.IsKeyboardFocusWithin)
        {
            return true;
        }

        return box.Tag is string tag &&
               tag.StartsWith(WritingTag, StringComparison.Ordinal) &&
               string.Equals(tag[WritingTag.Length..], _session?.SelectedNodeId, StringComparison.Ordinal);
    }

    /// <summary>
    /// 글 칸이 쓰지 않은 휠을 판에서 멈춘다 (2026-11-17 소유자 보고). 칸의 것인 휠(<see cref="WheelBelongsToBox"/>)은 확대가 가로채지
    /// 않는데, 글이 칸에 다 들어가 스크롤할 것이 없으면 칸이 휠을 받지 않고 그대로 올라가 <b>판의 ScrollViewer가 판 전체를 세로로
    /// 스크롤했다</b>. 버블에서 캔버스는 칸보다 바깥이고 판의 스크롤보다 안쪽이라, 칸이 먼저 제 몫을 하고 남은 것만 여기서 끝난다.
    /// </summary>
    private void OnWritingBoxWheel(object? sender, PointerWheelEventArgs args)
    {
        if (WheelBelongsToBox(args.Source))
        {
            args.Handled = true;
        }
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
    /// 하이어아키의 챕터 줄이나 툴바 콤보로 챕터를 골랐을 때(<see cref="GoToBoard"/>) 여러 챕터 박스 사이에서 그 챕터를 눈앞에 가져온다.
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

            var rect = new Rect(node.Position.X, node.Position.Y, card.Visual.Width, CardHeightOf(card));
            bounds = bounds is { } current ? current.Union(rect) : rect;
        }

        // 접힌 판 · 접힌 장면은 표로 선다 — 하이어아키에서 접힌 챕터를 골라도 그 표로 간다.
        foreach (FileProxyVisual proxy in _proxies.Where(proxy => string.Equals(proxy.FileId, fileId, StringComparison.Ordinal)))
        {
            var rect = new Rect(
                Canvas.GetLeft(proxy.Visual), Canvas.GetTop(proxy.Visual),
                ProxyWidth, ProxyHeaderHeight + (proxy.VisualRowCount * ProxyRowHeight));
            bounds = bounds is { } current ? current.Union(rect) : rect;
        }

        if (bounds is not { } area)
        {
            FitAll(); // 빈 판이다(카드가 아직 없다) — 전체 보기가 차선이다
            return;
        }

        FitRect(area.Inflate(80));
    }

    /// <summary>그 네모가 화면에 들어오는 배율(100%를 넘지 않는다)로 그 가운데를 화면 가운데에.</summary>
    private void FitRect(Rect area)
    {
        SetZoom(Math.Min(
            1.0,
            Math.Min(GraphScroll.Viewport.Width / area.Width, GraphScroll.Viewport.Height / area.Height)));
        CenterOn(area.Center);
    }

    /// <summary>판의 그 점을 화면 가운데에 — 판 밖으로는 넘기지 않는다(스크롤이 막는다).</summary>
    private void CenterOn(Point point)
    {
        GraphScroll.Offset = new Vector(
            Math.Max(0, (point.X * _zoom) - (GraphScroll.Viewport.Width / 2)),
            Math.Max(0, (point.Y * _zoom) - (GraphScroll.Viewport.Height / 2)));
        RefreshMinimapViewport();
    }

    /// <summary>F로 카드에 맞출 때의 배율 — 글 칸을 읽고 쓰기에 알맞은 크기.</summary>
    internal const double EditZoom = 1.0;

    /// <summary>
    /// <b>F — 고른 것에 화면을 맞춘다</b>(2026-11-18 소유자 · writer-comfort WC2). 카드(에피소드 · 분기 카드)를 골랐으면 <see cref="EditZoom"/>로 그
    /// 카드를 화면 가운데에, 장면이면 그 장면 테두리(접혔으면 그 상자)가 들어오게, 챕터면 그 챕터가 들어오게(<see cref="GoToBoard"/>와 같다).
    /// 고른 것이 없으면 거짓이다.
    /// </summary>
    internal bool FocusSelection()
    {
        if (_session is null || GraphScroll.Viewport.Width <= 0 || GraphScroll.Viewport.Height <= 0)
        {
            return false;
        }

        if (_session.SelectedNodeId is { } nodeId && FindCard(nodeId) is { } card)
        {
            SetZoom(EditZoom);
            CenterOn(new Rect(Canvas.GetLeft(card.Visual), Canvas.GetTop(card.Visual), card.Visual.Width, CardHeightOf(card)).Center);
            return true;
        }

        switch (_summaryPick)
        {
            case { Kind: HierarchyKind.Scene, ChapterId: { } chapterId, SceneId: { } sceneId }:
                if (_sceneAreas.FirstOrDefault(scene => string.Equals(scene.ChapterId, chapterId, StringComparison.Ordinal) &&
                                                        string.Equals(scene.SceneId, sceneId, StringComparison.Ordinal)) is { Area.Width: > 0 } shown)
                {
                    FitRect(shown.Area.Inflate(40));
                    return true;
                }

                if (_session.Project.Files.FirstOrDefault(file => string.Equals(file.Name, chapterId, StringComparison.Ordinal)) is { } folded &&
                    FindSceneProxy(folded.Id, sceneId) is { } proxy)
                {
                    FitRect(new Rect(
                        Canvas.GetLeft(proxy.Visual), Canvas.GetTop(proxy.Visual),
                        ProxyWidth, ProxyHeaderHeight + (proxy.VisualRowCount * ProxyRowHeight)).Inflate(40));
                    return true;
                }

                return false;

            case { Kind: HierarchyKind.Chapter, ChapterId: { } chapterId }
                when _session.Project.Files.FirstOrDefault(file => string.Equals(file.Name, chapterId, StringComparison.Ordinal)) is { } board:
                FitFile(board.Id);
                return true;

            default:
                return false;
        }
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
                card.Visual.Width, CardHeightOf(card));
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

        if (TargetVisualOf(nodeId) is { } target)
        {
            RevealTarget(target);
        }
    }

    /// <summary>그 카드(또는 프록시)가 화면에 하나도 안 보이면 가운데로 옮긴다 — 이미 보이면 끼어들지 않는다.</summary>
    private void RevealTarget(Control target)
    {
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
                card.Visual.Width,
                CardHeightOf(card),
                card.Style.Accent); // 카드와 같은 시각 언어 — 미니맵에서도 에피소드·분기 카드가 갈린다.
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
            : HeaderHeight + (card.RowCount * PortRowHeight) + ((CardPadding + CardBorder) * 2) +
              (card.WritingBox is { } box ? WritingBoxGap + box.Height : 0);
    }

    // ── 조작 ────────────────────────────────────────────────────────────────

    private void OnCardPressed(NodeCard card, PointerPressedEventArgs args)
    {
        ReleaseWritingFocus();
        _session?.Select(card.NodeId);

        // 두 번 누름 — 끌기를 시작하지 않고 셸에 알린다(첫 누름이 시작한 끌기는 놓을 때 끝났다).
        if (args.ClickCount >= 2 && args.GetCurrentPoint(card.Visual).Properties.IsLeftButtonPressed)
        {
            args.Handled = true;
            CardOpened?.Invoke(card.NodeId);
            return;
        }

        // 범위 선택된 카드를 잡으면 무리가 함께 움직인다 (W40).
        if (_multiSelected.Contains(card.NodeId) && _multiSelected.Count > 1)
        {
            BeginGroupDrag(args.GetPosition(GraphCanvas));
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

    /// <summary>무리(<see cref="_multiSelected"/>)를 함께 끌기 시작한다 — 움직임은 판이 받고(<see cref="OnCanvasPointerMoved"/>) 놓을 때 한 번 적는다.</summary>
    private void BeginGroupDrag(Point at)
    {
        _draggingGroup = true;
        _groupDragStart = at;
        _groupStartPositions.Clear();

        foreach (NodeCard member in _cards)
        {
            if (_multiSelected.Contains(member.NodeId))
            {
                _groupStartPositions[member.NodeId] = new Point(Canvas.GetLeft(member.Visual), Canvas.GetTop(member.Visual));
            }
        }

        HighlightSelection();
    }

    /// <summary>
    /// 챕터 · 장면 이름표를 눌렀다(2026-11-18 소유자 — 「한번 클릭했을 때 접히는 대신, 더블 클릭을 해야 접히도록 … 한번 좌클릭을 했을 때는 그 챕터내에
    /// 포함된 에피소드나, 그 장면 내에 포함된 모든 에피소드가 클릭되도록 해서 이동시키는 용도」). 한 번 = 그 안의 카드를 모두 무리로 고르고(인스펙터 ·
    /// 하이어아키는 그 챕터 · 장면 — 판의 빈 곳을 누른 것과 같다) 그대로 끌면 함께 움직인다. 두 번 = 접기.
    /// </summary>
    private void OnFrameLabelPressed(PointerPressedEventArgs args, IReadOnlyList<string> members, HierarchyPick pick, Action fold)
    {
        args.Handled = true;

        if (!args.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (args.ClickCount >= 2)
        {
            fold();
            return;
        }

        ReleaseWritingFocus();
        SelectEdge(null);
        AreaPicked?.Invoke(pick);

        _multiSelected.Clear();
        _multiSelected.UnionWith(members);
        BeginGroupDrag(args.GetPosition(GraphCanvas));
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
        SetHint(port.ExecutionPort is { Kind: ExitPortKind.Detour } detour
            ? detour.IsOpenDetour
                ? "분기 카드 위에 놓으면 잇습니다. 빈 곳에 놓으면 그 자리에 새 분기 카드가 섭니다. 에피소드에는 잇지 않습니다."
                : "분기 카드 위에 놓으면 그리로 옮겨 잇습니다. 빈 곳에 놓으면 연결만 끊기고 분기점은 남습니다."
            : "연결할 실행 노드 또는 접힌 파일의 실행 노드 행 위에서 놓으세요. " +
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

        // 빈 곳 우클릭 = 그 자리에 더하기(2026-11-18 소유자) — 장면 안이면 에피소드, 챕터 안이면 장면, 챕터 밖이면 챕터.
        if (args.GetCurrentPoint(GraphCanvas).Properties.IsRightButtonPressed)
        {
            args.Handled = true;
            ShowBoardMenu(args.GetPosition(GraphCanvas));
            return;
        }

        // 빈 곳 좌클릭 = 범위 선택 시작 (W40). 이전 무리는 새 범위가 대신한다.
        if (args.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
        {
            _multiSelected.Clear();
            HighlightSelection();

            _rubberStart = args.GetPosition(GraphCanvas);
            _rubberBand = RubberBand.Create(_rubberStart);
            GraphCanvas.Children.Add(_rubberBand);
            args.Pointer.Capture(GraphCanvas);
        }
    }

    /// <summary>
    /// 판의 빈 곳 우클릭 차림표 — <b>하나뿐이다</b>(2026-11-18 소유자 보고 — 우클릭을 빠르게 하면 차림표가 겹겹이 떠 누를 때마다 챕터 · 에피소드가
    /// 하나씩 더 섰다). 우클릭마다 새로 짓던 것을 한 벌로 두고, 다시 띄우기 전에 닫는다. 시험의 손잡이이기도 하다(팝업은 창 밖에 산다).
    /// </summary>
    internal ContextMenu? BoardMenu { get; private set; }

    /// <summary>[대본] 판 빈 곳의 「챕터 추가」 — 셸이 하이어아키의 「챕터 추가」와 같이 세우고 고른다. 누른 자리에 첫 카드가 선다.</summary>
    internal event Action<Point>? ChapterAddRequested;

    /// <summary>빈 판 한가운데의 [＋ 첫 챕터 만들기] — 셸이 「챕터 추가」와 같이 세우고 고른다(first-chapter F1). 첫 카드는 새 챕터 자리에 선다.</summary>
    internal event Action? FirstChapterRequested;

    /// <summary>
    /// <b>판의 빈 곳 우클릭</b>(2026-11-18 소유자 — 「챕터 바깥 빈 공간에서 우클릭 하면 챕터 추가. 챕터 내에서 우클릭 하면 장면 추가, 장면 내에서
    /// 우클릭 하면 에피소드 추가」) — 그 자리에 맞는 항목 하나. 자리는 좌클릭이 고르는 자리와 같은 테두리로 잰다(<see cref="SceneAt"/> ·
    /// <see cref="ChapterAt"/>). 에피소드 · 장면은 누른 자리에 카드가 선다.
    /// </summary>
    private void ShowBoardMenu(Point at)
    {
        (string header, Action act) = SceneAt(at) is { } scene
            ? ("에피소드 추가", () => AddEpisodeAt(scene.ChapterId, scene.SceneId, at))
            : ChapterAt(at) is { } chapterId
                ? ("장면 추가", () => AddEpisodeAt(chapterId, sceneId: null, at))
                : ("챕터 추가", (Action)(() => ChapterAddRequested?.Invoke(at)));

        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            BoardMenu?.Close();
            UiGuard.Run(_session, header, act);
        };

        BoardMenu ??= NewBoardMenu();
        BoardMenu.Close();
        BoardMenu.ItemsSource = new[] { item };
        BoardMenu.Open(GraphCanvas);
    }

    /// <summary>
    /// 판 차림표 한 벌. 떠 있는 동안 붙은 창의 누름을 스스로 본다 — 차림표 밖을 누르면 닫는다(확인 칸 <see cref="ConfirmFlyout"/>과 같은 까닭: 팝업의
    /// 바깥 누름 닫기에만 기대면 데스크톱에서 닫히지 않고 남았다). 띄운 누름은 이미 지나갔으므로 다음 누름부터 본다.
    /// </summary>
    private ContextMenu NewBoardMenu()
    {
        var menu = new ContextMenu();

        void OnPress(object? sender, PointerPressedEventArgs args)
        {
            if (args.Source is not Visual source || !(ReferenceEquals(source, menu) || menu.IsVisualAncestorOf(source)))
            {
                menu.Close();
            }
        }

        TopLevel? window = null;

        menu.Opened += (_, _) =>
        {
            window = TopLevel.GetTopLevel(GraphCanvas);
            window?.AddHandler(PointerPressedEvent, OnPress, RoutingStrategies.Tunnel, handledEventsToo: true);
        };
        menu.Closed += (_, _) =>
        {
            window?.RemoveHandler(PointerPressedEvent, OnPress);
            window = null;
        };

        return menu;
    }

    /// <summary>
    /// 누른 자리에 에피소드 카드를 세운다 — <paramref name="sceneId"/>의 장면에, 없으면 새 장면(「새 장면」)과 함께. 카드 · 장면 이름이 한 되돌리기
    /// 칸이고(<see cref="AddEpisodeCard"/>와 같은 명령), 세운 카드를 고른다.
    /// </summary>
    private void AddEpisodeAt(string chapterId, string? sceneId, Point at)
    {
        if (_session?.Editor.FindChapter(chapterId) is not { } chapter ||
            CreateEpisodeCard(chapterId, sceneId, ClampNodeX(at.X - (CardWidth / 2)), ClampNodeY(at.Y - 20)) is not { } made)
        {
            return;
        }

        _session.Select(made.Card.Id);
        _session.SetStatus(made.SceneMade is not null
            ? $"장면 「{NewNames.Scene}」을 세우고 누른 자리에 에피소드를 더했습니다 — 이름은 오른쪽 인스펙터 머리나 하이어아키에서 고칩니다."
            : $"장면 「{(ChapterSceneNotes.NameOf(chapter.SceneNotes, sceneId!) is { Length: > 0 } named ? named : sceneId)}」의 누른 자리에 에피소드를 더했습니다.");
    }

    /// <summary>
    /// 새 챕터의 첫 장면 · 첫 에피소드(2026-11-18 소유자 — 「새로운 챕터를 만들 때, 1개의 장면과 1개의 에피소드는 같이」) — 셸의 「챕터 추가」가 챕터와
    /// 같은 되돌리기 칸 안에서 부른다. 카드는 판 우클릭이면 누른 자리, 아니면 판의 모든 카드 오른쪽(<see cref="NewChapterSpot"/>)에 선다.
    /// </summary>
    internal void AddFirstEpisode(string chapterId, Point? at)
    {
        (double x, double y) = at is { } point
            ? (ClampNodeX(point.X - (CardWidth / 2)), ClampNodeY(point.Y - 20))
            : NewChapterSpot();

        CreateEpisodeCard(chapterId, sceneId: null, x, y);
    }

    /// <summary>새 챕터의 첫 카드가 설 자리 — 판의 모든 카드 오른쪽, 맨 위 카드와 같은 높이. 챕터들은 한 판에 서므로 겹치지 않게 비켜 선다.</summary>
    private (double X, double Y) NewChapterSpot()
    {
        List<DialogueNode> cards = _session!.Project.Files.SelectMany(file => file.Nodes).OfType<DialogueNode>().ToList();

        if (cards.Count == 0)
        {
            return (ClampNodeX(NewChapterGap / 2), ClampNodeY(NewChapterGap / 2));
        }

        double right = cards.Max(card => card.Layout.X + (card.Layout.Width ?? CardWidth));
        return (ClampNodeX(right + NewChapterGap), ClampNodeY(cards.Min(card => card.Layout.Y)));
    }

    /// <summary>새 챕터가 앞 챕터들의 카드에서 비켜 서는 거리 — 챕터 · 장면 테두리와 이름표가 들어가고도 남는다.</summary>
    private const double NewChapterGap = 240;

    /// <summary>
    /// 그 자리에 에피소드 카드를 세운다 — <paramref name="sceneId"/>의 장면에, 없으면 새 장면(「새 장면」)과 함께. 카드 · 장면 이름이 한 되돌리기
    /// 칸이다([＋ 에피소드]와 같은 명령). 고르지는 않는다 — 부른 쪽이 정한다.
    /// </summary>
    private (DialogueNode Card, string? SceneMade)? CreateEpisodeCard(string chapterId, string? sceneId, double x, double y)
    {
        if (_session?.Editor.FindChapter(chapterId) is not { } chapter ||
            EpisodeNaming.BoardOf(_session.Project, chapterId) is not { } board)
        {
            return null;
        }

        string? sceneMade = null;
        DialogueNode? created = null;

        _session.Editor.Batch(() =>
        {
            string scene = sceneId ?? (sceneMade = NewNames.NextSceneId(chapter));
            created = _session.Editor.AddDialogueNode(board.Id, scene, x, y, title: NewNames.Episode);

            if (sceneMade is not null)
            {
                _session.Editor.SetSceneName(chapterId, sceneMade, NewNames.Scene);
            }
        });

        return (created!, sceneMade);
    }

    /// <summary>
    /// 범위 선택을 놓았다. 카드가 걸렸으면 <b>카드만</b> 무리가 되고(함께 끌린다) 끌기 시작한 자리에서 가장 가까운 카드를
    /// 고른다 — 함께 걸친 선은 고르지 않는다. 카드 없이 선만 걸렸으면 <b>놓은 자리(포인터)에서 가장 가까운 선 하나</b>를
    /// 고른다. 인스펙터는 고른 것을 보인다.
    /// </summary>
    private void FinishRubberBand(Rect rect, Point end)
    {
        // 끌지 않고 누르기만 한 자리가 장면 테두리 안이면 그 장면을 고른다(2026-11-17 소유자) — 하이어아키에서 그 장면 줄을 고른
        // 것과 같다. 카드 · 선은 제가 먼저 누름을 받으므로 여기까지 오는 것은 빈 곳뿐이다.
        // 장면 밖 · 챕터 테두리 안이면 그 챕터, 챕터 밖의 빈 판이면 프로젝트(화자)다.
        if (rect.Width < ClickSlop && rect.Height < ClickSlop)
        {
            _multiSelected.Clear();
            HighlightSelection();
            AreaPicked?.Invoke(
                SceneAt(end) is { } scene ? new HierarchyPick(HierarchyKind.Scene, scene.ChapterId, SceneId: scene.SceneId)
                : ChapterAt(end) is { } chapterId ? new HierarchyPick(HierarchyKind.Chapter, chapterId)
                : HierarchyPick.Project);
            return;
        }

        _multiSelected.Clear();
        var cards = new List<(double Distance, string NodeId)>();

        foreach (NodeCard card in _cards)
        {
            var bounds = new Rect(
                Canvas.GetLeft(card.Visual),
                Canvas.GetTop(card.Visual),
                card.Visual.Width,
                CardHeightOf(card));

            if (rect.Intersects(bounds))
            {
                _multiSelected.Add(card.NodeId);
                cards.Add((RubberBand.DistanceTo(rect.Intersect(bounds), _rubberStart), card.NodeId));
            }
        }

        HighlightSelection();

        if (cards.Count > 0)
        {
            _session?.Select(cards.MinBy(item => item.Distance).NodeId);
            return;
        }

        var lines = new List<(double Distance, Action Pick)>();

        foreach (EdgeVisual edge in _edges)
        {
            if (edge.Path.IsVisible && edge.Path.Data is { } shape &&
                RubberBand.CaughtDistance(shape, rect, end) is { } distance)
            {
                lines.Add((distance, () => SelectEdge(edge)));
            }
        }

        // 레인은 누르는 자리(넓고 투명한 같은 선)가 신원을 든다.
        foreach (ShapePath hit in _railVisuals.OfType<ShapePath>())
        {
            if (hit.Tag is RailKey key && hit.Data is { } shape &&
                RubberBand.CaughtDistance(shape, rect, end) is { } distance)
            {
                lines.Add((distance, () => SelectRail(key)));
            }
        }

        if (lines.Count > 0)
        {
            lines.MinBy(item => item.Distance).Pick();
        }
    }

    /// <summary>누르기와 끌기를 가르는 폭(px) — 이보다 작게 움직였으면 누른 것이다.</summary>
    private const double ClickSlop = 4;

    /// <summary>그 자리를 품은 장면 테두리. 장면 테두리는 서로 겹치지 않게 서지만, 겹쳤다면 작은 쪽이 먼저다.</summary>
    private (string FileId, string ChapterId, string SceneId)? SceneAt(Point point)
    {
        foreach ((Rect area, string fileId, string chapterId, string sceneId) in
                 _sceneAreas.OrderBy(item => item.Area.Width * item.Area.Height))
        {
            if (area.Contains(point))
            {
                return (fileId, chapterId, sceneId);
            }
        }

        return null;
    }

    /// <summary>그 자리를 품은 챕터 테두리의 챕터.</summary>
    private string? ChapterAt(Point point)
    {
        foreach ((Rect area, string chapterId) in _chapterAreas)
        {
            if (area.Contains(point))
            {
                return chapterId;
            }
        }

        return null;
    }

    /// <summary>
    /// 장면에 세울 새 카드의 자리 — 그 장면 테두리의 오른쪽 옆, 위를 맞춰. 다른 카드와 겹치면 아래로 비킨다. 테두리가 없으면
    /// (접은 장면) null — 보고 있는 곳에 세운다.
    /// </summary>
    private (double X, double Y)? PlaceBesideScene(string fileId, string sceneId)
    {
        if (_sceneAreas.FirstOrDefault(item =>
                string.Equals(item.FileId, fileId, StringComparison.Ordinal) &&
                string.Equals(item.SceneId, sceneId, StringComparison.Ordinal)) is not { SceneId: not null } found)
        {
            return null;
        }

        // 테두리는 카드에서 (10, 26, 10, 10)만큼 나와 있다 — 새 카드가 들어가면 테두리가 그만큼 넓어진다.
        double x = found.Area.Right - 10 + 40;
        double y = found.Area.Y + 26;
        List<Rect> taken = _cards
            .Select(card => new Rect(Canvas.GetLeft(card.Visual), Canvas.GetTop(card.Visual), card.Visual.Width, CardHeightOf(card)))
            .ToList();

        for (int attempt = 0; attempt < 40; attempt++)
        {
            var wanted = new Rect(x - 20, y - 20, CardWidth + 40, 200);

            if (!taken.Any(wanted.Intersects))
            {
                break;
            }

            y += 60;
        }

        return (ClampNodeX(x), ClampNodeY(y));
    }

    /// <summary>실행 선의 굵기 — 고른 선만 굵다.</summary>
    private void PaintEdges()
    {
        foreach (EdgeVisual item in _edges)
        {
            item.Path.StrokeThickness = ReferenceEquals(item, _selectedEdge) ? 4 : 2;
        }
    }

    /// <summary>
    /// 판에서 선을 골랐다(null이면 고른 선을 놓았다) — 셸이 인스펙터에 그 선을 보인다. 카드를 고르면 셸은 선택이 바뀐 것으로
    /// 알고, 이 화면은 고른 선을 조용히 놓는다.
    /// </summary>
    internal event Action<PickedLine?>? LineSelected;

    /// <summary>판에서 고른 선 — 선택지 레인이거나 실행 선(분기 · 갈래)이다.</summary>
    internal abstract record PickedLine
    {
        private PickedLine()
        {
        }

        /// <summary>선택지 레인 — 챕터 간선 하나, 신원은 (출발, 도착, 문구)다.</summary>
        internal sealed record Choice(string ChapterId, string From, string To, string? Label) : PickedLine;

        /// <summary>실행 선 — 카드의 출구 포트에서 나간다.</summary>
        internal sealed record Exit(GraphConnectionProjection Connection) : PickedLine;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs args)
    {
        Point position = args.GetPosition(GraphCanvas);

        if (_rubberBand is not null)
        {
            RubberBand.Stretch(_rubberBand, _rubberStart, position);
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

        if (_resizingCard is not null)
        {
            // 판 밖에서 놓았으면 놓임이 안 온다 — 단추가 떨어진 채 돌아오면 거기서 놓은 것으로 친다.
            if (!args.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
            {
                CommitResize();
                return;
            }

            // 끄는 동안은 화면의 카드만 키운다 — 대사 옆 분기 점은 가장자리를 따라 함께 간다. 레일은 놓은 뒤 다시 선다.
            if (_resizeEdges.HasFlag(ResizeEdges.Right))
            {
                _resizingCard.Visual.Width = Math.Clamp(
                    _resizeStartWidth + (position.X - _resizeStart.X), CardWidth, MaxCardWidth);
            }

            if (_resizeEdges.HasFlag(ResizeEdges.Bottom) && _resizingCard.WritingBox is { } writing)
            {
                writing.Height = Math.Clamp(
                    _resizeStartHeight + (position.Y - _resizeStart.Y), WritingBoxHeight, MaxWritingHeight);
            }

            PlaceLineKnobs(_resizingCard);
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
    private double ClampNodeX(double x) => Math.Clamp(x, 0, CanvasWidth - CardWidth);

    private double ClampNodeY(double y) => Math.Clamp(y, 0, CanvasHeight - BoardSize.CardHeight);

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        CommitResize();
        CommitDrag();

        // 범위 선택 확정 (W40)
        if (_rubberBand is not null)
        {
            Rect rect = RubberBand.Between(_rubberStart, args.GetPosition(GraphCanvas));
            GraphCanvas.Children.Remove(_rubberBand);
            _rubberBand = null;
            args.Pointer.Capture(null);
            FinishRubberBand(rect, args.GetPosition(GraphCanvas));
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

        if (port.ExecutionPort is { Kind: ExitPortKind.Detour } detour)
        {
            Point at = args.GetPosition(GraphCanvas);
            UiGuard.Run(_session, "분기 잇기", () => JoinBranch(detour, target, at));
            return;
        }

        if (port.ExecutionPort is not null)
        {
            _session?.Editor.SetExitTarget(port.ExecutionPort, target);
        }
    }

    /// <summary>
    /// <b>분기점을 놓았다</b> (2026-11-17 · branch-card-orders B3). 분기점은 <b>분기 카드에만</b> 잇는다 — 에피소드에 놓으면 받지 않고 까닭을
    /// 말한다(들렀다 돌아오는 동안 그 에피소드의 선택지는 게임에 나오지 않는다). <b>이어지지 않은 분기점을 빈 곳에</b> 놓으면 그 자리에 새 분기
    /// 카드가 서고 이어진다(소유자 — 분기 카드를 지운 뒤 다시 이을 때 「분기 추가」로 분기점을 하나 더 만들지 않는다). 이어진 분기점을 빈 곳에
    /// 놓으면 연결만 끊긴다(분기점은 남는다).
    /// </summary>
    private void JoinBranch(ExitPort detour, string? target, Point at)
    {
        if (_session is null)
        {
            return;
        }

        if (target is null)
        {
            if (!detour.IsOpenDetour)
            {
                _session.Editor.SetExitTarget(detour, null);
                return;
            }

            if (_session.Editor.AddBranchCardAt(detour, ClampNodeX(at.X), ClampNodeY(at.Y - (TitleHeight / 2))) is { } created)
            {
                _session.SetStatus($"분기 카드 '{created.Name}'{Josa.을를(created.Name)} 세우고 이었습니다 — 들러서 할 대사를 적으세요. {BranchConditionNote.Where}");
            }

            return;
        }

        if (!_session.Editor.CanBranchTo(detour.NodeId, target))
        {
            string name = EpisodeNaming.ShownNameOf(_session.Project, _session.Project.FindNode(target)!);
            _session.SetStatus(_session.Editor.IsBranchCardElsewhere(detour.NodeId, target)
                ? $"'{name}'{Josa.은는(name)} 다른 챕터의 분기 카드입니다 — 분기점은 같은 챕터의 분기 카드에만 잇습니다. 빈 곳에 놓으면 새 분기 카드가 섭니다."
                : $"'{name}'{Josa.은는(name)} 에피소드입니다 — 분기점은 분기 카드에만 잇습니다(들렀다 돌아오는 동안 그 에피소드의 선택지는 게임에 " +
                  "나오지 않습니다). 빈 곳에 놓으면 새 분기 카드가 섭니다.");
            return;
        }

        _session.Editor.SetExitTarget(detour, target);
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
            _session.SetStatus(IsBranchCard(targetNodeId)
                ? "분기 카드에는 선택지를 잇지 않습니다 — 분기 카드는 분기점으로 들렀다 돌아오는 카드입니다. 선택지는 에피소드에 잇습니다."
                : "선택지는 에피소드에 잇습니다.");
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
        // 자동 길 그대로다. 자동 길은 장면을 넘어도 된다(⛔ 2026-11-18까지는 같은 장면 안에서만이라 여기서 먼저 거절했다).
        bool auto = choice.IsEmpty ? label.Length == 0 : choice.IsAuto;

        if (string.Equals(choice.ToEpisodeId, episodeId, StringComparison.Ordinal))
        {
            return;   // 이어져 있던 그 카드에 도로 놓았다 — 바뀐 것이 없다(§5-8)
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

    /// <summary>
    /// 그 노드가 선 자리 — (챕터, 에피소드). 에피소드가 아니면 null.
    ///
    /// ⚠ 판 이름이 챕터고 표식이 먼저고 없으면 이름이다 — 장면 묶기·슬롯 투영이
    /// 쓰는 그 규칙이다(새 규약을 만들지 않는다).
    /// </summary>
    private (string ChapterId, string EpisodeId)? EpisodeAt(string nodeId)
    {
        if (_session?.Project.FindFileContainingNode(nodeId) is not { } file ||
            _session.Project.FindNode(nodeId) is not DialogueNode dialogue)
        {
            return null;
        }

        ChapterDocument chapter = EpisodeNaming.ChapterOfBoard(_session.Project, file);

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

            if (point.X >= left && point.X <= left + card.Visual.Width &&
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

    /// <summary>고른 선을 알리지 않고 놓는다 — 셸이 인스펙터에 다른 것(카드 · 하이어아키 줄)을 세울 때.</summary>
    internal void ReleaseLine()
    {
        if (_selectedEdge is null && _selectedRail is null)
        {
            return;
        }

        MarkEdge(null);
        SetHint(null);
    }

    /// <summary>실행 선 하나를 고른다(null이면 고른 선을 놓는다) — 셸이 인스펙터를 맞추도록 <see cref="LineSelected"/>로 알린다.</summary>
    private void SelectEdge(EdgeVisual? edge)
    {
        bool had = _selectedEdge is not null || _selectedRail is not null;
        MarkEdge(edge);

        if (edge is not null || had)
        {
            LineSelected?.Invoke(edge is null ? null : new PickedLine.Exit(edge.Connection));
        }
    }

    /// <summary>고른 실행 선을 판에 칠한다 — 알리지는 않는다.</summary>
    private void MarkEdge(EdgeVisual? edge)
    {
        _selectedEdge = edge;
        PaintEdges();
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

        PositionEdge(edge);

        string sourceName = FindNodeName(edge.Connection.SourceNodeId) ?? edge.Connection.SourceNodeId;
        string targetName = FindNodeName(edge.Connection.TargetNodeId) ?? edge.Connection.TargetNodeId;
        SetHint($"{sourceName} — 조건 '{edge.Connection.Label}' → {targetName} · 우클릭으로 삭제");
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
            // 분기 선을 지우는 것은 「분기 지우기」다 — 분기점과 선만 걷고 다녀가던 카드는 남긴다
            // (2026-11-17 소유자 — 전에는 함께 선 카드도 걷었다). 그 밖의 출구는 잇던 선만 끊는다.
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

    /// <summary>
    /// <b>[＋ 에피소드]</b> — 에피소드는 늘 챕터와 장면에 속한다(2026-11-17 소유자 · <c>docs/work-orders/scene-required-orders.md</c> E1).
    /// 챕터는 지금 판(활성 판)의 챕터다 — 활성 판이 없으면(챕터가 하나도 없을 때) 단추가 꺼져 있다(<see cref="Rebuild"/>). 첫 챕터는 판
    /// 한가운데 [＋ 첫 챕터 만들기]나 「챕터 추가」로 세운다(first-chapter F1).
    /// 장면은 고른 장면(인스펙터에 장면 요약이 서 있다) → 고른 카드의 장면(분기 카드면 그 집의 장면) → <b>새 장면</b> 차례로 정한다(빈
    /// 챕터도). 이미 있는 장면이면 그 장면 테두리 옆에(접은 장면이면 보고 있는 곳에), 새 장면이면 보고 있는 화면 가운데에 선다. 장면 ·
    /// 카드가 되돌리기 한 칸이다.
    /// ⛔ 2026-11-01~11-17에는 장면을 안 고르면 장면 없는 에피소드(「챕터 직속」)가 섰다. ⛔ 챕터가 없을 때 챕터 + 장면을 함께 세우던
    /// 갈래(<see cref="TargetChapter"/>)는 남아 있지만 단추가 꺼져 있어 닿지 않는다.
    /// </summary>
    private void AddEpisodeCard()
    {
        if (_session is null)
        {
            return;
        }

        string? failure = null;
        string? chapterMade = null;
        string? sceneMade = null;
        string? sceneName = null;
        DialogueNode? created = null;

        _session.Editor.Batch(() =>
        {
            if (TargetChapter(out failure, out chapterMade) is not { } chapter ||
                EpisodeNaming.BoardOf(_session.Project, chapter.ChapterId) is not { } board)
            {
                return;
            }

            // 지금 보고 있는 곳에 만든다 (GB-4) — 큰 판에서 "만들었는데 안 보임"을 없앤다.
            // 연속 생성은 계단으로 밀려 정확히 겹치지 않는다.
            double stagger = (board.Nodes.Count % 5) * 26;
            double x = ClampNodeX(
                ((GraphScroll.Offset.X + (GraphScroll.Viewport.Width / 2)) / _zoom) - (CardWidth / 2) + stagger);
            double y = ClampNodeY(
                ((GraphScroll.Offset.Y + (GraphScroll.Viewport.Height / 2)) / _zoom) - 90 + stagger);

            string sceneId;

            if (SceneForNewCard(chapter, board) is { } existing)
            {
                sceneId = existing;

                // 그 장면 테두리 옆에 선다 — 장면에 들어간 것이 보인다.
                if (PlaceBesideScene(board.Id, sceneId) is { } beside)
                {
                    (x, y) = beside;
                }
            }
            else
            {
                sceneId = sceneMade = NewNames.NextSceneId(chapter);
            }

            // 작가는 ID를 보지 않으므로 처음 제목을 붙인다(display-names N2).
            created = _session.Editor.AddDialogueNode(board.Id, sceneId, x, y, title: NewNames.Episode);

            if (sceneMade is not null)
            {
                _session.Editor.SetSceneName(chapter.ChapterId, sceneMade, NewNames.Scene);
            }

            sceneName = ChapterSceneNotes.NameOf(chapter.SceneNotes, sceneId) is { Length: > 0 } named ? named : sceneId;
        });

        if (created is null)
        {
            _session.SetStatus(failure ?? "새 카드를 세울 챕터를 찾지 못했습니다.");
            return;
        }

        _session.Select(created.Id);
        _session.SetStatus(
            chapterMade is not null
                ? $"챕터 「{NewNames.Chapter}」와 장면 「{NewNames.Scene}」을 세우고 에피소드를 더했습니다 — 이름은 하이어아키에서 두 번 눌러 고칩니다."
                : sceneMade is not null
                    ? $"장면 「{NewNames.Scene}」을 세우고 에피소드를 더했습니다 — 장면이나 그 장면의 카드를 골라 두면 그 장면에 더합니다."
                    : $"장면 「{sceneName}」에 에피소드를 더했습니다.");
    }

    /// <summary>
    /// 새 카드가 설 챕터 — 지금 판의 챕터, 아니면 첫 챕터. 챕터가 하나도 없으면 「새 챕터」를 세운다(<paramref name="chapterMade"/>).
    /// </summary>
    private ChapterDocument? TargetChapter(out string? failure, out string? chapterMade)
    {
        failure = null;
        chapterMade = null;

        if (_session!.ActiveFile is { } active)
        {
            return EpisodeNaming.ChapterOfBoard(_session.Project, active);
        }

        if (_session.Project.Chapters.FirstOrDefault() is { } first)
        {
            _session.SelectFile(_session.EnsureChapterBoard(first.ChapterId));
            return first;
        }

        (string? chapterId, string? refused) = _session.CreateNamedChapter(NewNames.Chapter);
        failure = refused;
        chapterMade = chapterId;

        return chapterId is null ? null : _session.Editor.FindChapter(chapterId);
    }

    /// <summary>
    /// 이미 있는 장면 가운데 새 카드가 들 곳 — 고른 장면, 아니면 고른 카드의 장면(분기 카드면 그 집의 장면). 없으면 null(새 장면을 세운다).
    /// </summary>
    private string? SceneForNewCard(ChapterDocument chapter, StoryFile board)
    {
        if (NewNodeScene is { } target &&
            string.Equals(target.ChapterId, chapter.ChapterId, StringComparison.Ordinal) &&
            chapter.Episodes.Any(episode => string.Equals(episode.SceneId, target.SceneId, StringComparison.Ordinal)))
        {
            return target.SceneId;
        }

        if (_session!.SelectedNode is not DialogueNode selected ||
            !board.Nodes.Contains(selected))
        {
            return null;
        }

        string? episodeId = selected.IsBranchCard
            ? ChapterBranchCards.HomesOf(_session.Project, chapter.ChapterId)
                .FirstOrDefault(home => home.Card.Id == selected.Id)?.EpisodeId
            : EpisodeNaming.EpisodeFor(chapter, selected)?.EpisodeId;

        return episodeId is null
            ? null
            : chapter.Episodes.FirstOrDefault(episode => string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))?.SceneId;
    }

    private void DeleteSelected()
    {
        if (_session is null)
        {
            return;
        }

        DeleteTarget target = TargetToDelete();

        // 단추는 그때 꺼져 있다 — 다시 그리기 전에 눌렸을 때를 위해 한 번 더 본다.
        if (KeptReason(target) is { } kept)
        {
            _session.SetStatus(kept);
            return;
        }

        switch (target)
        {
            // 챕터 · 장면은 셸이 묻고 지운다 — 챕터 지우기(판 · 조건 배관 · 진행 JSON)와 장면 지우기는 셸의 일이다.
            case DeleteTarget.Chapter or DeleteTarget.Scene:
                RemoveRequested?.Invoke(_summaryPick!, DeleteButton);
                return;

            // 분기 카드는 에피소드가 아니다(2026-11-17) — 카드만 지운다. 부르던 분기점은 이어지지 않은 채 남는다. 되돌리기 한 번에 돌아온다.
            case DeleteTarget.BranchCard:
                RemoveSelectedBranchCard();
                return;

            // ⭐ <b>판의 카드는 에피소드 아니면 분기 카드다</b> (R7 P-6 · 2026-11-17 분기 카드). 에피소드 카드를 지우는 것은 <b>에피소드를
            //    지우는 일</b>이고, 하이어아키의 「에피소드 지우기」와 같은 길을 지나야 한다 — 2026-09-18까지는 카드만 걷어 <b>대본 없는 반쪽
            //    에피소드</b>가 남았다(만들기는 합쳐 놓고 지우기는 안 합친 자리였다).
            //
            // ⚠ <b>여기서 묻는다.</b> 하이어아키의 「에피소드 지우기」는 이름이 이미 무엇을 지우는지 말하지만, 여기서 사람은 <b>카드를
            //    지운다고 생각하고</b> 누른다 — 지워지는 것은 카드 하나가 아니라 에피소드와 그 간선이다.
            case DeleteTarget.Episode:
            {
                DialogueNode card = (DialogueNode)_session.SelectedNode!;
                ChapterDocument chapter = EpisodeNaming.ChapterOf(_session.Project, card);
                string episodeId = EpisodeNaming.EpisodeIdOf(card);

                DeleteConfirmButton = ConfirmFlyout.Show(
                    DeleteButton,
                    _session,
                    $"'{episodeId}'은 챕터 '{chapter.ChapterId}'의 에피소드입니다. " +
                    "간선·픽스처 참조까지 함께 지웁니다 — 되돌리기(Ctrl+Z)로 돌아옵니다.",
                    "에피소드 지우기",
                    () => DeleteEpisodeOfCard(chapter.ChapterId, episodeId),
                    closed: () => DeleteConfirmButton = null);
                return;
            }
        }
    }

    /// <summary>
    /// <b>Delete 키</b>(2026-11-18 소유자 — 「특정 에피소드를 누른 상태에서 DELETE를 누르면 그 에피소드가 지워지도록」) — 고른 카드를 <b>묻지 않고</b>
    /// 지운다: 에피소드면 에피소드째(간선 · 픽스처 참조까지 — [삭제]의 「에피소드 삭제」와 같은 길), 분기 카드면 그 카드. 되돌리기 한 칸이고 상태줄이
    /// 말한다. 챕터 · 장면은 하지 않는다 — 여럿이 함께 사라지는 일은 [삭제]로 한 번 확인한다.
    /// </summary>
    /// <returns>지웠으면 참 — 셸이 키를 처리한 것으로 적는다.</returns>
    internal bool DeleteSelectedCard()
    {
        DeleteTarget target = TargetToDelete();

        // 챕터의 마지막 에피소드는 남는다 — 키는 받되 까닭을 말한다(2026-11-18 · ChapterMinimum).
        if (KeptReason(target) is { } kept)
        {
            _session!.SetStatus(kept);
            return true;
        }

        switch (target)
        {
            case DeleteTarget.BranchCard:
                RemoveSelectedBranchCard();
                return true;

            case DeleteTarget.Episode:
            {
                DialogueNode card = (DialogueNode)_session!.SelectedNode!;
                DeleteEpisodeOfCard(EpisodeNaming.ChapterOf(_session.Project, card).ChapterId, EpisodeNaming.EpisodeIdOf(card));
                return true;
            }

            default:
                return false;
        }
    }

    private void RemoveSelectedBranchCard()
    {
        DialogueNode branch = (DialogueNode)_session!.SelectedNode!;
        _session.Editor.RemoveNode(branch.Id);
        _session.SetStatus(
            $"분기 카드 '{branch.Name}'{Josa.을를(branch.Name)} 지웠습니다 — 부르던 분기점은 이어지지 않은 채 남습니다(빈 곳에 놓으면 새 분기 카드가 섭니다). " +
            "되돌리기(Ctrl+Z)로 돌아옵니다.");
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
        return _proxies.FirstOrDefault(item =>
            item.SceneId is null && string.Equals(item.FileId, fileId, StringComparison.Ordinal));
    }

    /// <summary>펼친 판 안의 접은 장면 상자.</summary>
    private FileProxyVisual? FindSceneProxy(string fileId, string? sceneId) =>
        _proxies.FirstOrDefault(item =>
            item.SceneId is not null &&
            string.Equals(item.FileId, fileId, StringComparison.Ordinal) &&
            string.Equals(item.SceneId, sceneId, StringComparison.Ordinal));

    /// <summary>상자가 판에서 차지하는 자리 — 머리와 줄 전부.</summary>
    private static Rect ProxyRect(FileProxyVisual proxy) =>
        new(Canvas.GetLeft(proxy.Visual), Canvas.GetTop(proxy.Visual),
            ProxyWidth, ProxyHeaderHeight + (proxy.VisualRowCount * ProxyRowHeight));

    private string? FindNodeName(string nodeId)
    {
        ExpandedNodeProjection? expanded = _projection?.Items
            .OfType<ExpandedNodeProjection>()
            .FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));

        if (expanded is not null)
        {
            return ShownName(nodeId, expanded.NodeName);
        }

        return _projection?.Items
            .SelectMany(item => item switch
            {
                CollapsedFileProjection file => file.Nodes,
                CollapsedSceneProjection scene => scene.Nodes,
                _ => []
            })
            .FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal))
            ?.NodeName is { } name
            ? ShownName(nodeId, name)
            : null;
    }

    /// <summary>카드가 [대본] 판에 보이는 이름 — 에피소드면 제목(비면 EpisodeId), 아니면 노드 이름(<see cref="EpisodeNaming.ShownNameOf"/>).</summary>
    private string ShownName(string nodeId, string fallback) =>
        _session?.Project.FindNode(nodeId) is { } found ? EpisodeNaming.ShownNameOf(_session.Project, found) : fallback;

    /// <summary>그 챕터 에피소드의 보이는 제목 — 없으면 Id 그대로.</summary>
    private string EpisodeTitle(string chapterId, string episodeId) =>
        _session?.Editor.FindChapter(chapterId)?.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))?.ShownTitle ?? episodeId;

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

        /// <summary>
        /// 글 칸 안 대사 높이에 서는 포트인가 — 대사 카드의 「조건 분기」다(2026-10-30). 포트 줄에 서지 않는다.
        /// </summary>
        public bool IsLinePort(int index) =>
            NodeKind == GraphNodeKind.Dialogue && Ports[index].ExecutionPort?.Kind == ExitPortKind.Detour;

        public bool HasLinePorts => Enumerable.Range(0, Ports.Count).Any(IsLinePort);

        /// <summary>포트 줄의 수 — 분기 점은 줄을 차지하지 않는다.</summary>
        public int RowCount => Enumerable.Range(0, Ports.Count).Count(index => !IsLinePort(index));

        /// <summary>그 포트가 몇 번째 포트 줄인가.</summary>
        public int RowOf(int index) => Enumerable.Range(0, index).Count(before => !IsLinePort(before));

        /// <summary>카드의 글 칸. 대사 카드가 아니면 null.</summary>
        public TextBox? WritingBox { get; set; }

        /// <summary>분기 점의 높이(카드 위 끝에서) — 그 대사의 LineId로. <see cref="MeasureLineAnchors"/>가 잰다.</summary>
        public Dictionary<string, double> LineAnchorY { get; } = new(StringComparer.Ordinal);

        /// <summary>글 칸 밖으로 스크롤돼 분기 점이 칸 끝에 붙은 대사들(LineId).</summary>
        public HashSet<string> LineOutOfView { get; } = new(StringComparer.Ordinal);

        /// <summary>분기 점들 — 판(캔버스)에 선다. 포트 열쇠로.</summary>
        public Dictionary<string, Ellipse> LineKnobs { get; } = new(StringComparer.Ordinal);

        /// <summary>분기가 걸린 대사들(LineId) — 다녀오는 차례대로.</summary>
        public IEnumerable<string> BranchLines => Enumerable.Range(0, Ports.Count)
            .Where(IsLinePort)
            .Select(index => Ports[index].ExecutionPort!.BranchOpenLineId)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

        /// <summary>
        /// 그 분기 점이 드는 무리 — 대사(LineId)와 나가는 쪽. 한 대사의 분기도 저마다 도착 카드 쪽으로 나가므로(H1) 쪽마다
        /// 따로 접는다.
        /// </summary>
        public string GroupOf(int index) =>
            (Ports[index].ExecutionPort?.BranchOpenLineId ?? string.Empty) + (Ports[index].OnLeft ? ":left" : string.Empty);

        /// <summary>무리마다 그 분기 점들의 포트 차례 — 다녀오는 차례대로(펼치면 위에서 아래로 선다).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<int>> LineGroups => _lineGroups ??= Enumerable.Range(0, Ports.Count)
            .Where(IsLinePort)
            .GroupBy(GroupOf, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<int>)group.ToList(), StringComparer.Ordinal);

        private Dictionary<string, IReadOnlyList<int>>? _lineGroups;

        /// <summary>접은 공통 점들 — 분기가 둘 이상인 무리마다.</summary>
        public Dictionary<string, BranchHub> LineHubs { get; } = new(StringComparer.Ordinal);

        /// <summary>펼친 무리들.</summary>
        public HashSet<string> ExpandedGroups { get; } = new(StringComparer.Ordinal);
    }

    private sealed class FileProxyVisual
    {
        public FileProxyVisual(
            string fileId, Border visual, IReadOnlyList<ProxyNodeRow> rows, int visualRowCount, string? sceneId = null)
        {
            FileId = fileId;
            Visual = visual;
            Rows = rows;
            VisualRowCount = visualRowCount;
            SceneId = sceneId;
        }

        public string FileId { get; }

        /// <summary>펼친 판 안의 접은 장면 상자면 그 장면 — 판째 접힌 상자면 null이다 (2026-10-30).</summary>
        public string? SceneId { get; }
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
            _multiSelected.Clear(); // 선 하나를 고르면 카드 무리는 풀린다
            HighlightSelection();
            SelectRail(key);

            // 두 번 누르면 [구성] 탭의 그 간선으로 간다(셸이 데려간다).
            if (args.ClickCount >= 2)
            {
                ChoiceOpened?.Invoke(key.ChapterId, key.From, key.To, key.Label?.Trim() ?? string.Empty);
            }
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
        MarkEdge(null);
        _selectedRail = key;
        DrawChapterRails();
        string route = $"{EpisodeTitle(key.ChapterId, key.From)} → {EpisodeTitle(key.ChapterId, key.To)}";
        SetHint(key.Label is { Length: > 0 } label ? $"{route} · 「{label}」" : route);
        LineSelected?.Invoke(new PickedLine.Choice(key.ChapterId, key.From, key.To, key.Label));
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
