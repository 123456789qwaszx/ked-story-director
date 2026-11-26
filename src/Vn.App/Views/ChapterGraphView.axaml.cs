using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.Authoring.Chapters;
using Vn.Authoring.Text;
using Vn.App.Strings;
using Vn.Authoring.Strings;

namespace Vn.App.Views;

/// <summary>
/// 챕터·에피소드 그래프 뷰. <b>별도 화면이고 기존 대사·연출 그래프는 손대지 않는다</b>.
///
/// 원본은 프로젝트다. 이 화면의 편집은 <c>ProjectEditor</c>를 거치고, 되돌리기도 거기서 온다.
///
/// <b>자리의 주인은 에피소드 제 X·Y다</b> — 연출 그래프와 같이 사람이 카드를 자유롭게 옮긴다.
/// <see cref="ChapterBranchPlanner"/>는 <b>그리는 자리가 아니라 첫 자리</b>를 낸다 — 자세한 것은 그쪽 주석에 있다.
///
/// 오류가 있어도 읽힌 데까지 그린다. 빈 화면 + "오류"보다, 그려진 그래프 옆에 무엇이 어디서
/// 잘못됐는지 세워 두는 편이 고칠 자리를 알려 준다(규칙 14).
/// </summary>
public partial class ChapterGraphView : UserControl
{
    private const double CardWidth = 190;

    /// <summary>
    /// 카드의 높이 — 도착 스탯이 하나 이하인 카드(가장 낮은 카드). 스탯이 더 있으면 한 줄(<see cref="StatRowHeight"/>)씩 아래로 자란다
    /// (2026-11-19 소유자 · usability UX3 · ⛔ 그 전에는 모든 카드가 74였고 스탯은 한 줄에서 「…」로 잘렸다). 카드마다의 높이는
    /// <see cref="CardHeightOf"/> — 선택지 점 · 선 · 고르기 범위 · 판 크기가 그것을 따른다.
    /// </summary>
    private const double CardHeight = 74;

    /// <summary>스탯 줄 위까지 — 여백 · 칩 줄 · 이름 줄.</summary>
    private const double CardHeadHeight = 58;

    /// <summary>스탯 한 줄의 높이 — 글은 11이다.</summary>
    internal const double StatRowHeight = 16;

    private const double CanvasMargin = 60;

    private readonly List<ChapterEntry> _entries = new();

    /// <summary>선택된 챕터의 구조 검증 + 도달성 증명 결과.다시 읽을 때 갱신된다.</summary>
    private ChapterValidationResult? _validation;

    /// <summary>`스탯변화` 줄 편집기 — 간선 패널의 것.</summary>
    private readonly StatChangeEditor _edgeStats = new();

    private AuthoringSession? _session;
    private string? _selectedChapterId;
    private bool _updatingCombo;

    /// <summary>챕터 목록이 다시 읽혔다. 셸(MainWindow)이 듣고 두 하이어아키 · [대본] 툴바 콤보 · 「조건 및 스탯」 탭을 다시 세운다.</summary>
    internal event Action<IReadOnlyList<ChapterEntry>>? EntriesReloaded;

    /// <summary>프로젝트를 다시 읽어 판을 다시 세운다. 셸이 챕터를 만들거나 지운 직후 부른다.</summary>
    internal void Refresh() => Reload();

    /// <summary>
    /// 판을 실제로 다시 만든 횟수. <see cref="ValidationComputeCount"/>와 같은 종류의 창이다 —
    /// 그리기 자체는 싸지만, 다시 만드는 순간 <b>사람이 누르고 있던 카드가 사라진다</b>.
    /// </summary>
    internal int CanvasDrawCount { get; private set; }

    /// <summary>
    /// 챕터를 고른다 — <b>어디서 골랐든 이 길 하나를 지난다</b> (하이어아키 줄 · 툴바 콤보 ·
    /// 「조건 및 스탯」 탭의 작은 드롭다운 · 코드).
    ///
    /// 이 길을 건너뛰고 <c>Draw()</c>만 부르면: ① 검증을 다시 안 돌려 <b>이전 챕터의 결과</b>로
    /// 그린다 — 도달 불가 빨간 테두리가 남고 도착 스탯이 안 보인다 ② 판을 활성으로 바꾸지 않아
    /// <b>[대본] 툴바 콤보 · 「조건 및 스탯」 탭이 따라오지 않는다</b>(둘 다 활성 판을 본다).
    /// </summary>
    internal void SelectChapter(string chapterId)
    {
        if (string.Equals(_selectedChapterId, chapterId, StringComparison.Ordinal))
        {
            return;
        }

        _selectedChapterId = chapterId;
        _centerSheet = true;   // 다른 챕터의 판 — 가운데에 놓는다
        _updatingCombo = true;
        ChapterCombo.SelectedItem = chapterId;
        ConditionChapterCombo.SelectedItem = chapterId;
        _updatingCombo = false;

        // 고른 챕터의 선택은 이전 챕터의 것이 아니다 — 들고 있으면 없는 간선을 가리킨다.
        _selectedEpisodeId = null;
        _selectedEdgeKey = null;
        _grouped.Clear();
        UnfoldChoices();
        ShowInHierarchy(new HierarchyPick(HierarchyKind.Chapter, chapterId));
        _inspector = InspectorMode.Chapter;   // 챕터를 골랐다 — 하이어아키의 그 줄을 고른 것과 같다

        Validate();
        Draw();

        // 챕터를 고르는 것은 <b>보는 일</b>이지 들여오는 일이 아니다 — 대본은 연출 그래프 카드의
        //    글 칸에서 쓴다.

        // 그 챕터의 판을 활성으로 — [대본] 툴바 콤보 · 「조건 및 스탯」 탭이 이 값을 본다.
        ChapterSelected?.Invoke(chapterId);
    }

    /// <summary>
    /// 챕터가 골라졌다. 셸(MainWindow)이 듣고 그 챕터의 판을 활성으로 바꾼다 — 판을
    /// 만드는 일은 셸의 몫이라(<c>EnsureChapterBoard</c>) 이 뷰가 직접 하지 않는다.
    /// </summary>
    internal event Action<string>? ChapterSelected;

    // ── 하이어아키 (hierarchy) ──────────
    //
    // 내용(프로젝트 · 챕터 목록)과 접힘은 <b>셸(MainWindow)이 짓는다</b> — 두 탭의 하이어아키가 한 원천 · 한 접힘을 쓴다.
    // 이 탭의 하이어아키는 <b>읽기 전용</b>이다(셸이 ReadOnly로 세운다 — 차림표 · 이름 고치기 · 끌어 옮기기가 없다. 더하고 지우고
    // 고치는 것은 [대본] 탭). 줄을 고른 뒤 이 판에서 할 일(챕터 · 에피소드 고르기, 오른쪽 탭 바꾸기)은
    // 이 뷰가 한다(<see cref="OnHierarchyPicked"/>).

    /// <summary>하이어아키 — 셸이 내용을 채우고 접힘을 맞춘다(읽기 전용).</summary>
    internal StoryHierarchy HierarchyView => Hierarchy;

    /// <summary>하이어아키 기둥 접기 —셸이 두 탭을 맞추고 사용자 설정에 기억한다.</summary>
    internal ColumnDock HierarchyDock { get; }

    /// <summary>오른쪽 기둥(탭 셋) 접기 — 셸이 두 탭을 맞추고 사용자 설정에 기억한다.</summary>
    internal ColumnDock SideDock { get; }

    /// <summary>하이어아키에서 고른 것을 처리하는 중 — 그동안은 판의 선택을 트리에 되비추지 않는다(이미 고른 줄이다).</summary>
    private bool _fromHierarchy;

    /// <summary>
    /// 하이어아키에서 줄을 골랐다 —챕터 · 에피소드를 판에서 고르고 오른쪽 기둥을 그것에 맞춘다: 프로젝트 → 「화자」 탭,
    /// 챕터 → 「조건 및 스탯」 탭, 장면 · 에피소드 · 분기 → 「인스펙터」 탭.
    /// </summary>
    private void OnHierarchyPicked(HierarchyPick pick)
    {
        _fromHierarchy = true;

        try
        {
            if (pick.ChapterId is { } chapterId)
            {
                SelectChapter(chapterId);

                // 분기 카드 줄 — 그 칩을 고른다(부르는 곳 · 조건 · 대본).
                if (pick.Kind == HierarchyKind.Branch)
                {
                    SelectBranchCard(pick.NodeId);
                }
                else
                {
                    SelectEpisode(pick.Kind == HierarchyKind.Episode ? pick.EpisodeId : null);
                }
            }

            _inspector = pick.Kind switch
            {
                HierarchyKind.Project => InspectorMode.Project,
                HierarchyKind.Chapter => InspectorMode.Chapter,
                HierarchyKind.Scene => InspectorMode.Scene,
                _ => InspectorMode.Selection
            };
            _inspectedSceneId = pick.SceneId;
            ApplyInspector();

            // 고른 것을 보이는 탭으로 간다 — 프로젝트는 화자, 챕터는 조건 및 스탯([구성]은 수치를 잡는 자리다), 그 밖은 인스펙터
            // (2026-11-18 소유자 · side-tabs V1).
            ShowSideTab(pick.Kind switch
            {
                HierarchyKind.Project => SideTab.Speakers,
                HierarchyKind.Chapter => SideTab.Conditions,
                _ => SideTab.Inspector
            });
        }
        finally
        {
            _fromHierarchy = false;
        }
    }

    /// <summary>
    /// 하이어아키에서 고른 것처럼 인스펙터에 세운다 — 하이어아키 줄을 누른 것과 같은 길이다(트리도 그 줄을 비춘다).
    /// 화면 시험과 장면 인스펙터의 에피소드 단추가 부른다.
    /// </summary>
    internal void Inspect(HierarchyPick pick)
    {
        OnHierarchyPicked(pick);
        Hierarchy.Show(pick);
    }

    // ── 인스펙터 (hierarchy) ─────────────────────────────────

    /// <summary>인스펙터가 무엇을 보이나 — 고른 하나(에피소드 · 길 · 분기)거나, 하이어아키의 프로젝트 · 챕터 · 장면 줄이다.</summary>
    private enum InspectorMode
    {
        Selection,
        Project,
        Chapter,
        Scene
    }

    private InspectorMode _inspector = InspectorMode.Selection;

    /// <summary>장면 인스펙터가 보이는 장면(유효 장면ID).</summary>
    private string? _inspectedSceneId;

    /// <summary>오른쪽 기둥이 지금 보이는 탭 — 화면 시험의 손잡이.</summary>
    internal SideTab ShownSideTab =>
        ReferenceEquals(SideTabs.SelectedItem, SpeakerTabItem) ? SideTab.Speakers
        : ReferenceEquals(SideTabs.SelectedItem, ConditionTabItem) ? SideTab.Conditions
        : SideTab.Inspector;

    /// <summary>오른쪽 기둥의 탭을 연다 — 고르기가 부르고, 화면 시험이 탭을 누른 것 대신 부른다.</summary>
    internal void ShowSideTab(SideTab tab) => SideTabs.SelectedItem = tab switch
    {
        SideTab.Speakers => SpeakerTabItem,
        SideTab.Conditions => ConditionTabItem,
        _ => InspectorTabItem
    };


    /// <summary>인스펙터 탭의 칸 하나만 세우고 머리글을 맞춘다. 화자 · 조건 및 스탯은 제 탭에 늘 서 있다.</summary>
    private void ApplyInspector()
    {
        SelectionSection.IsVisible = _inspector is InspectorMode.Selection or InspectorMode.Scene;
        OverviewSection.IsVisible = _inspector is InspectorMode.Project or InspectorMode.Chapter;
        ChapterIdGrid.IsVisible = _inspector == InspectorMode.Chapter;
        OverviewText.Full = _inspector == InspectorMode.Chapter
            ? Ui.Structure_ThisChapterSConditionsStats
            : Ui.Side_SpeakersAreInSpeakersTab;
        ScenePanel.IsVisible = _inspector == InspectorMode.Scene;

        if (_inspector == InspectorMode.Scene)
        {
            NoSelectionText.IsVisible = false;   // 장면을 보고 있다 — "아무것도 안 골랐다" 안내가 아니다
        }

        ChapterGraphModel? model = SelectedModel;

        (string title, string subtitle) = _inspector switch
        {
            InspectorMode.Project => (Ui.Side_Project, string.Empty),
            // 머리글은 표시 이름이다(비면 ID) — 장면 · 에피소드 머리글과 같다(2026-11-17 소유자). ID는 아래 「챕터ID」 칸이 보인다.
            InspectorMode.Chapter => (string.Format(Ui.Structure_Chapter, model?.ShownName ?? _selectedChapterId), string.Empty),
            InspectorMode.Scene => SceneHeader(model),
            _ when _selectedEpisodeId is { } episodeId => (EpisodeHeader(model, episodeId), string.Empty),
            // 머리글은 문구(판의 선 이름표와 같은 글), 부제는 양 끝 에피소드의 보이는 제목(2026-11-17 소유자 — ⛔ 머리글의
            // 「출발 → 도착」 ID는 걷었다. 문구를 따로 보이던 읽기 전용 칸이 고칠 수 있는 칸처럼 보여서다).
            _ when _selectedEdgeKey is { } edge => (string.Format(Ui.Structure_Choice, EdgeHeaderText(model, edge)),
                $"{model?.FindEpisode(edge.From)?.ShownTitle ?? edge.From} → {model?.FindEpisode(edge.To)?.ShownTitle ?? edge.To}"),
            _ when _selectedBranchCardId is { } chipId => (string.Format(Ui.Structure_BranchCard, BranchCardName(chipId)),
                Ui.Structure_CardVisitedAndReturnedFrom),
            _ when _selectedSlot is { } slot => (string.Format(Ui.Structure_ChoiceSlot, slot.Episode), Ui.Structure_SlotWithNoPathYet),
            _ => (Ui.Board_Inspector, Ui.Side_SelectSomethingInHierarchyOr)
        };

        InspectorTitle.Text = title;
        InspectorSubtitle.Text = subtitle;
        InspectorSubtitle.IsVisible = subtitle.Length > 0;

        if (_inspector == InspectorMode.Scene)
        {
            FillScenePanel(model);
        }
    }

    /// <summary>고른 간선의 문구 — 판의 선 이름표와 같은 규칙(<see cref="EdgeOptionText"/>). 간선이 사라졌으면 고를 때의 문구.</summary>
    private static string EdgeHeaderText(ChapterGraphModel? model, (string From, string To, string Label) key) =>
        model?.Edges.FirstOrDefault(candidate =>
                candidate.FromEpisodeId == key.From &&
                candidate.ToEpisodeId == key.To &&
                EdgeLabelKey(candidate) == key.Label) is { } edge
            ? EdgeOptionText(edge)
            : key.Label.Length > 0 ? key.Label : Ui.Structure_NoText;

    /// <summary>선택지 간선의 문구 한 줄 — 자동 길은 「AUTO」, 문구가 비면 「⚠ 문구 없음」. 판의 이름표와 인스펙터 머리글이 같이 쓴다.</summary>
    internal static string EdgeOptionText(ChapterEdge edge) =>
        edge.Auto ? "AUTO"
        : edge.HasNoOptionLabel ? Ui.Structure_NoText
        : edge.OptionLabel!;

    /// <summary>
    /// 에피소드 인스펙터의 머리글 — 「장면 - 제목」(<see cref="ChapterEpisode.SceneAndTitle"/>). EpisodeId는 바로 아래 칸이
    /// 보인다. [대본] 탭의 머리글은 「{챕터} - {장면}」이다(제목은 그쪽 이름 칸이 보인다).
    /// </summary>
    private static string EpisodeHeader(ChapterGraphModel? model, string episodeId) =>
        model?.FindEpisode(episodeId)?.SceneAndTitle ?? episodeId;

    private (string Title, string Subtitle) SceneHeader(ChapterGraphModel? model) =>
        InspectedScene(model) is { } scene
            ? (string.Format(Ui.Structure_Scene, scene.DisplayName), Plural.Format(Ui.Structure_Episodes, scene.Episodes.Count))
            : (Ui.Structure_Scene2, Ui.Structure_ThisSceneIsGone);

    /// <summary>장면 인스펙터가 보이는 장면 — 이 챕터를 지금 묶어 본 것에서 찾는다(장면은 엔티티가 아니다).</summary>
    private ChapterScene? InspectedScene(ChapterGraphModel? model) =>
        model is null || _inspectedSceneId is null
            ? null
            : ChapterSceneGrouping.Of(model).FirstOrDefault(scene =>
                string.Equals(scene.SceneId, _inspectedSceneId, StringComparison.Ordinal));

    /// <summary>장면 인스펙터 — 안내 · 에피소드 단추들(이름은 머리글이 보인다).</summary>
    private void FillScenePanel(ChapterGraphModel? model)
    {
        SceneEpisodesPanel.Children.Clear();

        if (InspectedScene(model) is not { } scene)
        {
            SceneHintText.Set(Ui.Structure_ThisSceneIsGoneSelect, Ui.Structure_ThisSceneIsGone2);
            return;
        }

        if (!SceneIdBox.IsFocused)
        {
            SceneIdBox.Text = scene.SceneId;
        }

        SceneHintText.Set(
            Plural.Format(Ui.Structure_GroupOfEpisodesWithThis, scene.Episodes.Count),
            brief: null);

        foreach (ChapterEpisode episode in scene.Episodes)
        {
            string episodeId = episode.EpisodeId;
            bool root = string.Equals(scene.RootEpisodeId, episodeId, StringComparison.Ordinal);
            var link = new Button
            {
                Content = (root ? "⌂ " : "· ") + episodeId,
                FontSize = Skin.Font(11),
                Padding = new Thickness(6, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = "scene-episode:" + episodeId
            };
            link.Click += (_, _) => UiGuard.Run(_session, Ui.Structure_SelectEpisode, () =>
                Inspect(new HierarchyPick(HierarchyKind.Episode, _selectedChapterId, scene.SceneId, episodeId)));
            SceneEpisodesPanel.Children.Add(link);
        }
    }

    /// <summary>
    /// 에피소드를 골랐다 (2026-10-30 HY3) — 셸이 그 카드를 세션의 선택으로 삼는다(연출 그래프로 넘어가도 같은 것을 본다).
    /// 밖의 선택을 비출 때(<see cref="ShowEpisode"/>)는 알리지 않는다.
    /// </summary>
    internal event Action<string, string>? EpisodePicked;

    /// <summary>에피소드 카드를 두 번 눌렀다(챕터, 에피소드) — 셸이 [대본] 탭의 그 카드로 데려간다.</summary>
    internal event Action<string, string>? EpisodeOpened;

    /// <summary>
    /// 분기 카드의 칩을 두 번 눌렀다(노드 Id) — 셸이 [대본] 탭의 그 분기 카드로 데려간다(에피소드 카드와 같다 — 2026-11-17 소유자 보고:
    /// 칩은 두 번 눌러도 [대본]으로 가지 않았다).
    /// </summary>
    internal event Action<string>? BranchCardOpened;

    /// <summary>선택지 간선을 두 번 눌렀다(챕터, 출발, 도착, 문구) — 셸이 [대본] 탭의 그 선택지 레인으로 데려간다.</summary>
    internal event Action<string, string, string, string>? ChoiceOpened;

    /// <summary>선택지 간선을 눌렀다 — 고르고, 두 번 누름이면 [대본] 탭으로 가자고 알린다.</summary>
    private void PressChoice(string from, string to, string label, int clickCount)
    {
        SelectEdgeKey(from, to, label);

        if (clickCount >= 2 && _selectedChapterId is { } chapterId)
        {
            ChoiceOpened?.Invoke(chapterId, from, to, label);
        }
    }

    /// <summary>밖에서 고른 에피소드를 비추는 중 — 그동안은 <see cref="EpisodePicked"/>를 알리지 않는다.</summary>
    private bool _showingOutside;

    /// <summary>
    /// 밖(연출 그래프)에서 고른 에피소드를 이 판에도 고른다 — 그 챕터로 넘어가고 그 에피소드를 인스펙터에 세우며 하이어아키가
    /// 비춘다. 알리지는 않는다.
    /// </summary>
    internal void ShowEpisode(string chapterId, string episodeId)
    {
        if (string.Equals(_selectedChapterId, chapterId, StringComparison.Ordinal) &&
            string.Equals(_selectedEpisodeId, episodeId, StringComparison.Ordinal))
        {
            return;
        }

        _showingOutside = true;

        try
        {
            SelectChapter(chapterId);
            SelectEpisode(episodeId);
        }
        finally
        {
            _showingOutside = false;
        }
    }

    /// <summary>판에서 고른 것을 하이어아키에 비춘다 — 하이어아키가 부른 선택이면 하지 않는다.</summary>
    private void ShowInHierarchy(HierarchyPick? pick)
    {
        if (!_fromHierarchy)
        {
            Hierarchy.Show(pick);
        }
    }

    /// <summary>
    /// 판 한가운데의 [새 프로젝트] — 프로젝트가 아직 없을 때만 보인다(챕터보다 먼저다).
    /// 클릭의 뜻은 File 메뉴의 「새 프로젝트…」와 같으므로 배선도 같은 셸이 짓는다.
    /// </summary>
    internal Button ProjectNewCenterButton => EmptyNewProjectButton;

    /// <summary>판 한가운데의 [＋ 첫 챕터 만들기] — 프로젝트는 있고 챕터가 없을 때만 보인다. 셸이 [대본] 탭으로 넘어가 거기서 만든다(F2).</summary>
    internal event Action? FirstChapterRequested;


    /// <summary>머리글 아래 한 줄 — 지금 활성인 판의 요약.</summary>
    internal TextBlock ChapterSummaryText => ChapterSummary;

    public ChapterGraphView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => UiGuard.Run(_session, Ui.Structure_DrawStructureBoard, OnShownAgain);

        // 창을 떠나면(Windows 키 · Alt+Tab) 하던 끌기를 정리한다 — 놓음이 이 창으로 오지 않는다(usability UX20).
        AttachedToVisualTree += (_, _) =>
        {
            _window = TopLevel.GetTopLevel(this) as WindowBase;

            if (_window is not null)
            {
                _window.Deactivated += OnWindowDeactivated;
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_window is not null)
            {
                _window.Deactivated -= OnWindowDeactivated;
                _window = null;
            }
        };

        // 셸이 탭을 바꿀 때는 떼지 않고 보이기만 바꾼다(usability UX14) — 다시 보이는 순간 미뤄 둔 그리기를 한다.
        PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty && IsVisible)
            {
                UiGuard.Run(_session, Ui.Structure_DrawStructureBoard, OnShownAgain);
            }
        };

        // 하이어아키 기둥 접기 — 「‹」로 접고 띠를 눌러 편다.
        HierarchyDock = new ColumnDock(
            BoardColumns.ColumnDefinitions[0], BoardColumns.ColumnDefinitions[1], HierarchySplitter, HierarchyOpen, HierarchyStrip);
        FoldHierarchyButton.Click += (_, _) => HierarchyDock.Toggle(folded: true);
        HierarchyStrip.Click += (_, _) => HierarchyDock.Toggle(folded: false);

        // 오른쪽 기둥(탭 셋)도 같은 손짓으로 접는다(2026-11-18 소유자 · side-fold K1).
        SideDock = new ColumnDock(
            BoardColumns.ColumnDefinitions[4], BoardColumns.ColumnDefinitions[3], SideSplitter, SideOpen, SideStrip);
        FoldSideButton.Click += (_, _) => SideDock.Toggle(folded: true);
        SideStrip.Click += (_, _) => SideDock.Toggle(folded: false);

        // [다시 읽기] 단추는 없다 — 화면이 스스로 따라온다. <see cref="Reload"/>는
        // 세션의 변경 알림(QueueReload) · 붙이기(Attach) · 셸의 Refresh(챕터를 만들거나 지운 뒤)가 부른다.

        // [화자] 탭은 캐릭터 키만 잇는다 — 이름을 더하는 폼은 [대본] 탭에 있다(writer-fields W3).

        ChapterCombo.SelectionChanged += (_, _) =>
        {
            if (_updatingCombo)
            {
                return;
            }

            // 드롭다운도 하이어아키 줄 클릭과 같은 길을 지난다 — 여기서
            // Draw()만 부르면 빨간 테두리가 안 사라진다.
            if (ChapterCombo.SelectedItem is string picked)
            {
                UiGuard.Run(_session, Ui.Main_SelectChapter, () => SelectChapter(picked));
            }
        };

        // 「조건 및 스탯」 탭의 작은 드롭다운 — 툴바 콤보와 같은 길이다. 탭은 그대로 둔다(보던 탭에서 챕터만 바뀐다).
        ConditionChapterCombo.SelectionChanged += (_, _) =>
        {
            if (!_updatingCombo && ConditionChapterCombo.SelectedItem is string picked)
            {
                UiGuard.Run(_session, Ui.Main_SelectChapter, () => SelectChapter(picked));
            }
        };


        // 편집 — 전부 편집기 명령으로 프로젝트에 가고, 세션의 변경 알림이 다시 읽어 화면이 따라온다.
        // [개명]·[적용] 단추는 없다: Id는 Enter나 다른 곳을 누르면 개명하고(⛔ 2026-11-18까지는 Enter만 — side-boxes J1), 조건 콤보는
        // 고르는 순간 저장된다. Esc는 지금 값으로 돌린다([대본]과 같다). 편집은 늘 열려 있다.
        //
        // ⚠ 초점을 잃을 때 바꾸려면 <b>그때의 대상이 적기 시작한 대상이어야 한다</b> — 판의 카드 · 선 · 점은 초점을 가져가지 않아
        //    고름이 먼저 바뀔 수 있다. 고르기의 첫머리가 초점을 받이로 옮겨 먼저 낸다(FlushInspectorTyping).
        IdBox.LostFocus += (_, _) => UiGuard.Run(_session, Ui.Structure_RenameEpisode, RenameSelectedEpisode);
        ChapterIdBox.LostFocus += (_, _) => UiGuard.Run(_session, Ui.Structure_ChangeChapterID, RenameSelectedChapter);
        SceneIdBox.LostFocus += (_, _) => UiGuard.Run(_session, Ui.Structure_ChangeSceneID, RenameInspectedScene);
        BranchIdBox.LostFocus += (_, _) => UiGuard.Run(_session, Ui.Structure_ChangeBranchCardID, RenameInspectedBranchCard);
        EscToRevert.Wire(IdBox, () => _selectedEpisodeId);
        EscToRevert.Wire(ChapterIdBox, () => _selectedChapterId);
        EscToRevert.Wire(SceneIdBox, () => _inspectedSceneId);
        EscToRevert.Wire(BranchIdBox, () => _session?.Project.FindDialogue(_selectedBranchCardId)?.Name);
        EscToRevert.Wire(EdgeLockedMsgBox, () => SelectedEdge()?.LockedMessage);
        IdBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                UiGuard.Run(_session, Ui.Structure_RenameEpisode, RenameSelectedEpisode);
            }
        };
        ChapterIdBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                UiGuard.Run(_session, Ui.Structure_ChangeChapterID, RenameSelectedChapter);
            }
        };
        SceneIdBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                UiGuard.Run(_session, Ui.Structure_ChangeSceneID, RenameInspectedScene);
            }
        };
        BranchIdBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                UiGuard.Run(_session, Ui.Structure_ChangeBranchCardID, RenameInspectedBranchCard);
            }
        };
        Hierarchy.Picked += pick => UiGuard.Run(_session, Ui.Keys_Hierarchy, () => OnHierarchyPicked(pick));
        // 판 다루기 — 휠 확대·축소, 가운데 단추 끌어 이동. 휠은 <b>터널</b>로 받는다:
        // 스크롤뷰가 먼저 먹어 버리면 휠이 배율이 아니라 그냥 스크롤이 된다.
        GraphScroll.AddHandler(
            PointerWheelChangedEvent, OnGraphWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(   // 집기 신호는 버블로만 온다
            PointerTouchPadGestureMagnifyEvent, OnGraphMagnify, Avalonia.Interactivity.RoutingStrategies.Bubble);
        // 가운데 단추 끌기도 <b>터널</b>로 먼저 받는다 — [대본] 판과 같다. 선 · 문구 · 점 · 칩은 누름을 받아 먹으므로(Handled), 버블로
        // 받으면 그 위에서 시작한 끌기가 판을 못 움직였다(⛔ 2026-11-18까지 · 소유자 — inspector-boxes-pan J2). 가운데 단추가 아니면 그냥 지나간다.
        GraphScroll.AddHandler(PointerPressedEvent, OnGraphPanPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerMovedEvent, OnGraphPanMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        GraphScroll.AddHandler(PointerReleasedEvent, OnGraphPanReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ZoomResetButton.Click += (_, _) => ApplyZoom(1, null);
        FitChapterButton.Click += (_, _) => UiGuard.Run(_session, Ui.Structure_FitToScreen, FitChapter);
        EmptyFirstChapterButton.Click += (_, _) => FirstChapterRequested?.Invoke();

        // 스탯변화 줄 편집기 — 간선 패널.
        EdgeStatsHost.Children.Add(_edgeStats);

        // 칼럼의 빈 곳을 누르면 Enter처럼 — 적던 글자 칸을 놓는다(초점을 잃을 때 낸다). 누른 것의 처리보다 먼저(터널) 낸다:
        // 누른 것이 선택을 옮기는 고리면, 옮긴 뒤에 내면 글이 엉뚱한 자리로 간다.
        ChapterRightRows.AddHandler(
            PointerPressedEvent, (_, args) => ReleaseInspectorTyping(args.Source), RoutingStrategies.Tunnel, handledEventsToo: true);

        AutoArrangeButton.Click += (_, _) => UiGuard.Run(_session, Ui.Structure_AutoLayout, AutoArrange);
        HideEmptyChoicesCheck.IsCheckedChanged += (_, _) => UiGuard.Run(_session, Ui.Dialogue_HideEmptyChoices, () =>
            _session?.SetHideEmptyChoices(HideEmptyChoicesCheck.IsChecked == true));
        ShowIdsCheck.IsCheckedChanged += (_, _) => UiGuard.Run(_session, Ui.Structure_ShowIDs, () =>
            _session?.SetShowIds(ShowIdsCheck.IsChecked == true));

        // 간선 패널은 고르는 순간 저장된다. 에피소드 패널의 개명이 Enter로 확정되는 것과 같은 감각이다 —
        // [적용]이라는 문턱이 하나 더 있으면 "고쳤는데 왜 그대로지"가 생긴다.
        //
        // 글자 칸만 초점을 잃을 때 낸다. 자판 하나마다 저장하면 되돌리기가 글자 수만큼 쪼개지고,
        // 그 사이의 다시 그리기가 칸을 다시 채워 쓰던 글을 끊는다.
        // 문구는 글자 칸이다 — 다른 글자 칸처럼 Enter나 초점을 잃을 때 낸다.
        // 문구 칸은 읽기 전용이다 — 고치는 곳은 [대본] 탭(2026-11-17 소유자).
        EdgeVisibleNewButton.Click += (_, _) => UiGuard.Run(_session, Ui.Structure_NewCondition, () => ShowNewConditionFlyout(EdgeVisibleNewButton, visible: true));
        EdgeUnlockNewButton.Click += (_, _) => UiGuard.Run(_session, Ui.Structure_NewCondition, () => ShowNewConditionFlyout(EdgeUnlockNewButton, visible: false));

        EdgeVisibleCombo.SelectionChanged += (_, _) => AutoSaveEdge();
        EdgeConditionCombo.SelectionChanged += (_, _) => AutoSaveEdge();
        BranchConditionCombo.SelectionChanged += (_, _) =>
            UiGuard.Run(_session, Ui.Branch_BranchCondition, SaveBranchCondition);

        EdgeLockedMsgBox.LostFocus += (_, _) => AutoSaveEdge();
        EdgeLockedMsgBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                AutoSaveEdge();
            }
        };
        _edgeStats.Changed += (_, _) => AutoSaveEdge();
        CopyDiagnosticsButton.Click += async (_, _) =>
            await UiGuard.RunAsync(_session, Ui.Structure_CopyReport, CopyDiagnosticsAsync);

        // 대사 접기 — 토글이 곧 제목이다(상자 없이). 삼각형이 상태를 말한다
        // (▸ 접힘 · ▾ 펼침).
        DialogueToggle.IsCheckedChanged += (_, _) =>
        {
            bool open = DialogueToggle.IsChecked == true;
            DialoguePreviewText.IsVisible = open;
            DialogueToggle.Content = open ? Ui.Structure_Dialogue : Ui.Structure_Dialogue2;
        };

        // 펼친 채로 시작한다.
        // 주의: XAML에서 IsChecked를 켜지 않는다 — 그러면 위 핸들러가 붙기 <b>전에</b> 켜져
        // 글자와 본문이 접힌 모양 그대로 남는다. 여기서 켜야 셋이 한 번에 맞는다.
        DialogueToggle.IsChecked = true;

        // 빈 판 클릭 = 선택 해제, 끌면 범위 선택. 카드·간선·라벨 핸들러가 각자 누름을 소비하므로(e.Handled)
        // 여기까지 흘러오는 왼쪽 누름은 진짜 빈 공간뿐이다. 판 바깥(BoardDesk)이 받는다 — 판 안의 빈 곳도 판 둘레도 같다(usability UX22).
        // 자리는 늘 판 좌표다(판 밖이면 음수이거나 판 크기를 넘는다).
        BoardDesk.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
            {
                SelectEpisode(null);
                StartRubberBand(e);
            }
        };

        // 카드 끌기 —누름은 카드가 받고(e.Handled), 움직임과 놓음은 여기로 올라온다.
        // 연출 그래프와 같은 자리에 같은 손놀림이다.
        BoardDesk.PointerMoved += OnCanvasPointerMoved;
        BoardDesk.PointerReleased += OnCanvasPointerReleased;

        // 화면 크기를 알게 되거나 바뀌면 판을 다시 놓는다 — 가운데로 놓기로 한 것이 남았으면 그때 가운데로. 창 · 칸의 크기가 바뀌었으면 판이 화면에
        // 들어가면 들어가게 띄움을 줄인다. 스크롤 막대가 서거나 걷혀 바뀐 것(판이 넘쳤다 — 놓은 직후)에는 줄이지 않는다 — 놓은 카드가 밀린다.
        GraphScroll.PropertyChanged += (_, args) =>
        {
            if (args.Property == BoundsProperty)
            {
                PlaceSheet(keepInView: true);
            }
            else if (args.Property == ScrollViewer.ViewportProperty)
            {
                PlaceSheet();
            }
        };
    }

    internal void Attach(AuthoringSession session)
    {
        DetachSession();   // 두 번 붙이면 구독이 두 벌이 된다

        _session = session;
        _centerSheet = true;   // 새 세션의 판 — 가운데에 놓는다
        // 작가용 글 서식만 바뀐 것은 다시 읽지 않는다 — [구성]에 보이지 않고 내보내기에도 나가지 않는다(writer-formatting). 되돌리기 · 다시 실행은 프로젝트가
        // 통째로 새것이라 다시 읽는다.
        _sessionChanged = (_, args) =>
        {
            if (args.Kind != Vn.Authoring.Editing.ProjectChangeKind.WriterMarks || args.FromHistory)
            {
                QueueReload();
            }
        };
        session.Changed += _sessionChanged;

        // 「빈 선택지 숨기기」 — [구성] 판의 설정이다(앱 설정에 기억한다). 체크를 맞추고 판을 다시 그린다.
        HideEmptyChoicesCheck.IsChecked = session.HideEmptyChoices;
        _hideEmptyChanged = (_, _) => UiGuard.Run(_session, Ui.Dialogue_HideEmptyChoices, () =>
        {
            HideEmptyChoicesCheck.IsChecked = _session?.HideEmptyChoices == true;
            Draw();
        });
        session.HideEmptyChoicesChanged += _hideEmptyChanged;

        // 「ID로 보기」 — 켜면 ID, 끄면 이름(하이어아키는 [대본] 탭의 모양으로). 체크를 맞추고 하이어아키 · 판을 다시 세운다.
        ApplyShowIds();
        _showIdsChanged = (_, _) => UiGuard.Run(_session, Ui.Structure_ShowIDs, () =>
        {
            ApplyShowIds();
            Draw();
        });
        session.ShowIdsChanged += _showIdsChanged;

        // 화자 목록이 바뀔 때만(명령 · 되돌리기 · 열기) [화자] 탭을 다시 그린다 — [대본] 탭이 이름을 더하면 여기 키 목록도 선다.
        _speakersChanged = (_, _) => UiGuard.Run(_session, Ui.Board_Speakers, RebuildSpeakerTab);
        session.SpeakersChanged += _speakersChanged;
        Reload();
    }

    /// <summary>세션 구독을 끊을 수 있도록 들고 있는다 — 람다를 바로 걸면 뗄 수가 없다.</summary>
    private EventHandler<Vn.Authoring.Editing.ProjectChangedEventArgs>? _sessionChanged;
    private EventHandler? _hideEmptyChanged;
    private EventHandler? _showIdsChanged;
    private EventHandler? _speakersChanged;

    /// <summary>「ID로 보기」를 체크와 하이어아키에 비춘다 — 끄면 하이어아키가 [대본] 탭처럼 이름으로 선다(곁글 없음).</summary>
    private void ApplyShowIds()
    {
        bool showIds = _session?.ShowIds != false;
        ShowIdsCheck.IsChecked = showIds;
        HierarchyView.ShowEpisodeTitles = !showIds;
    }

    /// <summary>
    /// 세션에서 손을 뗀다 — 구독을 끊는다.
    ///
    /// <b>왜 필요한가</b>: 앱에서는
    /// 뷰가 프로세스와 수명이 같아 티가 안 나지만, 테스트에서는 한 어셈블리가 <b>디스패처
    /// 하나</b>를 나눠 쓴다. 떼지 않으면 창을 안 닫고 끝낸 테스트의 뷰가 그대로 살아 그 세션의 알림을 계속 듣고,
    /// 다른 세션에 다시 붙인 뷰는 옛 세션이 바뀔 때도 다시 읽는다.
    ///
    /// ⛔ 폴더 감시자가 있던 때는 더 나빴다 — 남은 뷰의 감시자가 지워진 임시 폴더를 보고, 250ms 디바운스가 <b>타이머
    /// 스레드에서</b> 깨어나 없어진 디스패처에 <c>Post</c>를 해 <b>프로세스가 내려갔다</b>(매번 다른 테스트가 실패로 찍히고,
    /// 혼자 돌리면 통과했다). 감시자는 걷혔고 다시 읽기는 세션의 변경 알림이 부른다(<see cref="QueueReload"/>).
    ///
    /// 앱에서는 뷰가 하나뿐이라 새는 일이 없지만, <b>끊을 수 있다는 것 자체가 규격</b>이다 —
    /// 붙이는 길만 있고 떼는 길이 없으면 수명은 언제나 우연에 맡겨진다.
    /// </summary>
    internal void DetachSession()
    {
        if (_session is not null && _sessionChanged is not null)
        {
            _session.Changed -= _sessionChanged;
        }

        if (_session is not null && _hideEmptyChanged is not null)
        {
            _session.HideEmptyChoicesChanged -= _hideEmptyChanged;
        }

        if (_session is not null && _showIdsChanged is not null)
        {
            _session.ShowIdsChanged -= _showIdsChanged;
        }

        if (_session is not null && _speakersChanged is not null)
        {
            _session.SpeakersChanged -= _speakersChanged;
        }

        _sessionChanged = null;
        _hideEmptyChanged = null;
        _showIdsChanged = null;
        _speakersChanged = null;
        _session = null;
    }

    /// <summary>이미 예약된 재읽기가 있는가. 한 번의 UI 차례에 하나만 돈다.</summary>
    private bool _reloadQueued;

    /// <summary>
    /// 재읽기 예약 — <b>몰려 오는 변경을 한 번으로 합친다</b>.
    ///
    /// 프로젝트 변경 알림은 한 UI 차례에 여럿 올 수 있다. 그 하나하나가 재읽기를 예약하면, 그 한 번마다
    /// 챕터 전부를 다시 읽고 진행 JSON을 쓰고 판을 통째로 다시 그린다 — 마지막 한 번 말고는 전부 버려질
    /// 그림이다. ⛔ 에피소드 동기화가 있던 때는 한 번이 변경을 수십 개 냈고(에피소드마다 노드·줄이 붙었다),
    /// 노드 60개에서 <b>123번</b> 돌아 58초가 됐다.
    ///
    /// 예약 표시는 실제로 돌기 직전에 내린다: 재읽기가 도는 동안 들어온 변경은
    /// 다음 차례를 새로 예약한다(놓치지 않는다).
    /// </summary>
    private void QueueReload()
    {
        if (_reloadQueued)
        {
            return;
        }

        _reloadQueued = true;

        Dispatcher.UIThread.Post(() =>
        {
            _reloadQueued = false;
            Reload();
        });
    }

    // ── [화자] 탭 = 캐릭터 키 ──────────────────────────
    //
    // 화자는 챕터의 것이 아니라 프로젝트의 것이다(StoryProject.Speakers — 이름과 초상화 키가 한 줄). 이름은 [대본] 탭(작가)이
    // 더하고 고치고 지운다. 여기는 그 이름마다 <b>캐릭터 키</b>를 잇는 자리다 — 초상화 · 표정이 이 키로 붙는다(writer-fields W3).

    /// <summary>[화자] 탭을 지금 프로젝트의 화자 목록으로 다시 그린다.</summary>
    private void RebuildSpeakerTab()
    {
        SpeakerListPanel.Children.Clear();

        if (_session is null)
        {
            return;
        }

        IReadOnlyList<Vn.Authoring.Model.Speaker> speakers = _session.Project.Speakers;

        if (speakers.Count == 0)
        {
            SpeakerListPanel.Children.Add(new GuideLine
            {
                Full = Ui.Structure_NoSpeakersYetSpeakerNames,
                Brief = Ui.Speakers_NoSpeakersYet,   // 설명 간소화(brief-guides G1)
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.55),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        int missing = speakers.Count(speaker => string.IsNullOrWhiteSpace(speaker.CharacterId));
        int temporary = speakers.Count(speaker => Vn.Authoring.Model.CharacterKey.IsTemp(speaker.CharacterId));

        if (missing + temporary > 0)
        {
            // 임시 키(temp_ — 화자를 더할 때 이름의 로마자로 선다 · UX29)도 아직 정하지 않은 키다.
            SpeakerListPanel.Children.Add(new TextBlock
            {
                Text = (missing, temporary) switch
                {
                    (> 0, > 0) => Plural.Format(Ui.Structure_SpeakersWithoutCharacterKeySpeakers, missing, temporary),
                    (> 0, _) => Plural.Format(Ui.Structure_SpeakersWithoutCharacterKeyTheir, missing),
                    _ => Plural.Format(Ui.Structure_SpeakersWithTemporaryKeyTemp, temporary)
                },
                FontSize = Skin.Font(10),
                Foreground = UnlockConditionInk,   // 같은 호박색 — 「아직 비었다」
                TextWrapping = TextWrapping.Wrap,
                Tag = "speaker-missing-keys"
            });
        }

        foreach (Vn.Authoring.Model.Speaker speaker in speakers)
        {
            SpeakerListPanel.Children.Add(BuildSpeakerRow(speaker));
        }
    }

    /// <summary>
    /// 화자 한 줄 — `이름 [캐릭터 키]`. 이름은 읽기만 한다([대본] 탭의 것). 키를 고치면 그 자리에서 프로젝트에 들어간다 — 되돌리기 한 칸
    /// ([적용] 없음 — 챕터 편집 탭과 같은 규칙).
    /// </summary>
    private Control BuildSpeakerRow(Vn.Authoring.Model.Speaker speaker)
    {
        string speakerName = speaker.Name;
        bool missing = string.IsNullOrWhiteSpace(speaker.CharacterId);
        bool temporary = Vn.Authoring.Model.CharacterKey.IsTemp(speaker.CharacterId);
        var name = new TextBlock
        {
            Text = missing || temporary ? "⚠ " + speakerName : speakerName,
            FontSize = Skin.Font(12),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Tag = "speaker-name:" + speakerName
        };
        ToolTip.SetTip(name, missing
            ? string.Format(Ui.Structure_NoCharacterKeyNameIs, speakerName)
            : temporary
                ? string.Format(Ui.Structure_CharacterKeyIsTemporaryMade, speakerName)
                : string.Format(Ui.Structure_NameIsEditedInScript, speakerName));
        var characterId = new TextBox
        {
            Text = speaker.CharacterId,
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = Skin.Font(12),
            PlaceholderText = Ui.Structure_CharacterKey,
            Tag = "speaker-key:" + speakerName
        };

        void Commit()
        {
            if (_session is null)
            {
                return;
            }

            if (!_session.Project.Speakers.Any(item => string.Equals(item.Name, speakerName, StringComparison.Ordinal)))
            {
                return; // 그 사이 목록이 바뀌었다 — 다음 그리기가 정본이다.
            }

            string typed = (characterId.Text ?? string.Empty).Trim();

            if (_session.SetSpeakerCharacterId(speakerName, typed))
            {
                _session.SetStatus(typed.Length == 0
                    ? string.Format(Ui.Structure_ClearedCharacterKeyOfSpeaker, speakerName)
                    : string.Format(Ui.Structure_SpeakerCharacterKey, speakerName, typed));
            }
        }

        characterId.LostFocus += (_, _) => UiGuard.Run(_session, Ui.Structure_CharacterKey, Commit);
        characterId.KeyDown += (_, args) => CommitOnEnter(args, Commit);
        EscToRevert.Wire(characterId, () =>
            _session?.Project.Speakers.FirstOrDefault(item => string.Equals(item.Name, speakerName, StringComparison.Ordinal))?.CharacterId);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        Grid.SetColumn(name, 0);
        Grid.SetColumn(characterId, 1);
        row.Children.Add(name);
        row.Children.Add(characterId);

        return row;
    }

    /// <summary>
    /// 칼럼 안의 글자 칸에 초점이 있고 누른 곳이 글자 칸 · 스크롤 막대가 아니면, 초점을 받이로 옮겨 그 칸을 놓는다 — 칸은 초점을
    /// 잃을 때 내므로 Enter와 같다(간선 패널의 문구 · 잠금 안내문 · 스탯변화 숫자).
    /// </summary>
    private void ReleaseInspectorTyping(object? pressed)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox typing ||
            !typing.GetVisualAncestors().Contains(ChapterRightRows) ||
            pressed is Visual visual && visual.GetSelfAndVisualAncestors().Any(item => item is TextBox or ScrollBar))
        {
            return;
        }

        InspectorFocusSink.Focus();
    }

    private static void CommitOnEnter(Avalonia.Input.KeyEventArgs args, Action commit)
    {
        if (args.Key == Avalonia.Input.Key.Enter)
        {
            commit();
            args.Handled = true;
        }
    }

    /// <summary>다시 읽은 횟수 — 시험이 「서식만 바꾸면 [구성]을 다시 읽지 않는다」를 본다(writer-formatting).</summary>
    internal int ReloadCount { get; private set; }

    private void Reload()
    {
        ReloadCount++;

        // 판은 프로젝트에서 온다 — 파일을 읽지 않는다.
        _entries.Clear();
        _entries.AddRange(ChapterEntries());

        _updatingCombo = true;

        // 항목은 ID(고르는 규칙 그대로), 보이는 글은 챕터 이름(비면 ID) — [대본] 콤보와 같다(2026-11-18 소유자 · ⛔ 그 전에는 ID가 보였다).
        // 목록을 늘 새로 주므로 이름만 바뀌어도 다시 선다.
        // 「조건 및 스탯」 탭 머리의 작은 드롭다운도 같은 목록 · 같은 선택이다(2026-11-18 소유자 · side-tabs V1).
        ChapterCombo.ItemTemplate ??= new Avalonia.Controls.Templates.FuncDataTemplate<string>((id, _) => new TextBlock
        {
            Text = _entries.FirstOrDefault(entry => string.Equals(entry.ChapterId, id, StringComparison.Ordinal))?.Model.ShownName ?? id
        });
        ConditionChapterCombo.ItemTemplate ??= ChapterCombo.ItemTemplate;
        ChapterCombo.ItemsSource = _entries.Select(entry => entry.ChapterId).ToList();
        ConditionChapterCombo.ItemsSource = _entries.Select(entry => entry.ChapterId).ToList();

        if (_selectedChapterId is null ||
            _entries.All(entry => entry.ChapterId != _selectedChapterId))
        {
            _selectedChapterId = _entries.FirstOrDefault()?.ChapterId;

            // 저절로 고른 챕터도 하이어아키의 그 줄을 고른 것과 같다 — 인스펙터에 그 챕터가 선다.
            _inspector = InspectorMode.Chapter;
            ShowInHierarchy(_selectedChapterId is { } picked ? new HierarchyPick(HierarchyKind.Chapter, picked) : null);
        }

        ChapterCombo.SelectedItem = _selectedChapterId;
        ConditionChapterCombo.SelectedItem = _selectedChapterId;
        _updatingCombo = false;

        RebuildSpeakerTab();

        AutoExport();        // 진행 JSON은 사람 손을 기다리지 않는다
        Validate();
        DrawWhenShown();     // 못 나갔으면 그 결론이 검증 보고 맨 위에 선다

        EntriesReloaded?.Invoke(_entries);
    }

    /// <summary>다시 읽은 뒤 아직 그리지 않았다 — [구성] 탭이 가려져 있었다. 탭이 다시 보이면 그린다.</summary>
    private bool _drawPending;

    /// <summary>
    /// 판 · 검증 보고 · 인스펙터를 다시 그린다 — <b>[구성] 탭이 가려져 있으면 보일 때까지 미룬다</b>(2026-11-18 · finishing FX3). [대본] 탭에서
    /// 글을 넣거나 카드를 옮길 때마다 다시 읽기가 돌고, 그때마다 보이지 않는 이 판을 통째로 다시 지었다(에피소드 300개에서 한 번에 0.3초).
    /// 내보내기와 검증은 미루지 않는다 — 진행 JSON은 사람 손을 기다리지 않는다. 가려졌다 = 화면 트리에 없거나(창에 붙기 전) 보이지 않는다
    /// (셸이 고른 탭의 판만 보인다 — usability UX14 · ⛔ 그 전에는 탭 안에 들어 있어 가려지면 화면 트리에서 떨어졌다) — 다시 보이는 순간
    /// <see cref="OnShownAgain"/>이 그린다.
    /// </summary>
    private void DrawWhenShown()
    {
        if (TopLevel.GetTopLevel(this) is null || !IsVisible)
        {
            _drawPending = true;
            return;
        }

        _drawPending = false;
        Draw();
    }

    private void OnShownAgain()
    {
        if (_drawPending)
        {
            _drawPending = false;
            Draw();
        }
    }

    /// <summary>프로젝트가 든 챕터를 화면이 쓰는 모양으로 낸다.</summary>
    private IReadOnlyList<ChapterEntry> ChapterEntries()
    {
        if (_session is null)
        {
            return Array.Empty<ChapterEntry>();
        }

        return _session.Project.Chapters
            .Select(chapter => new ChapterEntry(
                chapter.ChapterId,
                chapter.ToGraphModel(chapter.ChapterId),
                ChapterBranchCards.HomesOf(_session.Project, chapter.ChapterId)))
            .OrderBy(entry => entry.ChapterId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 구조 검증 + 도달성 증명.모델을 다시 읽을 때마다 한 번 돌려 두고 화면은 그 결과를
    /// 그리기만 한다 — 픽스처를 바꿀 때마다 상태공간을 다시 훑을 이유가 없다.
    /// </summary>
    private void Validate()
    {
        _validation = null;

        ChapterEntry? entry = _entries.FirstOrDefault(item => item.ChapterId == _selectedChapterId);

        if (entry is null)
        {
            return;
        }

        _validation = ValidationFor(entry);
    }

    /// <summary>
    /// 증명과 내보내기 — 규칙은 `Vn.Authoring.Chapters`에 산다.
    ///
    /// 캐시·지문·거부 장부가 전부 저쪽에 있다. 여기 있는 것은 <b>언제 부르나</b>뿐이고,
    /// 그것이 화면의 일이다. 규칙이 화면 파일 안에 살면 밖에서 보이지 않는다 — 예컨대
    /// "동기화는 고른 챕터만, 내보내기는 전 챕터"라는 사실이 화면에 묻히면 저작 관문을
    /// 걸려는 쪽이 그것을 모른다.
    /// </summary>
    private readonly ChapterExportService _export = new();

    /// <summary>직전 내보내기에서 못 나간 챕터들.</summary>
    private ChapterExportRun _exportRun = ChapterExportRun.Empty;

    /// <summary>실제로 증명을 돌린 횟수 — 테스트가 "일을 몇 번 했는가"를 보는 창이다.</summary>
    internal int ValidationComputeCount => _export.ValidationComputeCount;

    private ChapterValidationResult ValidationFor(ChapterEntry entry) =>
        _export.ValidationFor(entry, _session?.Project);

    // ── 판 다루기: 휠 확대·축소, 가운데 단추 끌어 이동 ──
    //
    // 연출 그래프(GraphEditorView)와 같은 손놀림·같은 산식이다. 판이 둘인데 다루는
    // 법이 다르면 손이 매번 헷갈린다.
    //
    // Ctrl을 요구하지 않는다 — 판 위에서 휠은 곧 배율이다.
    // 세로로 훑는 일은 가운데 단추 끌기가 맡는다.
    //
    // 설정 「휠로 판 옮기기 (터치패드용)」를 켜면(맥은 처음부터) 거꾸로다 — 휠이 판을 옮기고 배율은 Ctrl(맥은 ⌘도)+휠 · 트랙패드 집기다
    // (BoardWheel 한 벌 · writer-app WA3).

    private const double MinZoom = 0.1;  // 큰 챕터도 [화면 맞춤]으로 한 화면에 들어오게
    private const double MaxZoom = 2.5;

    private double _zoom = 1;
    private bool _panning;
    private Point _panStart;
    private Vector _panStartOffset;

    /// <summary>
    /// 휠 — 배율이거나(처음 값 · 누른 키와 무관하게) 판 옮기기다(설정 「휠로 판 옮기기 (터치패드용)」). 스크롤로 새지 않게 여기서 삼킨다. 무엇을 얼마나 하는가는
    /// <see cref="BoardWheel"/> 한 벌([대본]과 같다) — 배율은 신호의 크기에 비례한다.
    /// </summary>
    private void OnGraphWheel(object? sender, Avalonia.Input.PointerWheelEventArgs args)
    {
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

    /// <summary>트랙패드 집기(맥) — 늘 배율이다.</summary>
    private void OnGraphMagnify(object? sender, Avalonia.Input.PointerDeltaEventArgs args)
    {
        ApplyZoom(_zoom * BoardWheel.Magnify(args.Delta), args.GetPosition(GraphScroll));
        args.Handled = true;
    }

    /// <summary>배율 적용 — 커서 아래의 내용이 그 자리에 남게 오프셋을 맞춘다.</summary>
    private void ApplyZoom(double zoom, Point? anchor)
    {
        Point pivot = anchor ?? new Point(
            GraphScroll.Viewport.Width / 2, GraphScroll.Viewport.Height / 2);

        // 지금 pivot 아래에 있는 캔버스 좌표 — 판이 띄워 서 있으면 그만큼 뺀다(PlaceSheet).
        var content = new Point(
            (GraphScroll.Offset.X + pivot.X - _sheetOffset.X) / _zoom,
            (GraphScroll.Offset.Y + pivot.Y - _sheetOffset.Y) / _zoom);

        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ZoomHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ZoomText.Text = $"{Math.Round(_zoom * 100)}%";
        ZoomResetButton.IsVisible = Math.Abs(_zoom - 1) > 0.001;
        _centerSheet = true;   // 배율이 바뀌면 판이 화면보다 작은 쪽은 가운데에 다시 선다
        PlaceSheet();
        ZoomHost.UpdateLayout(); // 새 크기를 알아야 오프셋 상한이 맞는다

        GraphScroll.Offset = new Vector(
            Math.Max(0, (content.X * _zoom) + _sheetOffset.X - pivot.X),
            Math.Max(0, (content.Y * _zoom) + _sheetOffset.Y - pivot.Y));
    }

    /// <summary>
    /// [화면 맞춤] — 이 챕터의 카드가 모두 한 화면에 들어오는 배율과 자리. 판은 이 챕터의 카드만 여백째 감싸므로
    /// (<see cref="DisplayPlacement"/>) 판 전체를 화면에 맞춘다. 카드가 적다고 100%보다 키우지는 않는다.
    /// </summary>
    internal void FitChapter()
    {
        double width = GraphCanvas.Width;
        double height = GraphCanvas.Height;

        if (GraphScroll.Viewport.Width <= 0 || GraphScroll.Viewport.Height <= 0 ||
            double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            return;   // 레이아웃 전이거나 그릴 판이 없다
        }

        ApplyZoom(
            Math.Min(1.0, Math.Min(GraphScroll.Viewport.Width / width, GraphScroll.Viewport.Height / height)),
            anchor: null);

        GraphScroll.Offset = new Vector(
            Math.Max(0, ((width * _zoom) - GraphScroll.Viewport.Width) / 2),
            Math.Max(0, ((height * _zoom) - GraphScroll.Viewport.Height) / 2));
    }

    /// <summary>
    /// <b>F — 고른 것에 화면을 맞춘다</b>(2026-11-23 소유자 — 「구성탭에서도 F를 눌러서 원하는 노드로 바로 화면이동이 되도록」 · [대본] 판의 F와 같은 손짓 ·
    /// writer-comfort WC2). 에피소드 카드 · 분기 칩 · 빈 선택지 칸을 골랐으면 100%로 그것을 화면 가운데에, 길(간선)이면 그 두 카드가, 장면이면 그 장면의
    /// 카드들이 들어오게(100%보다 키우지는 않는다), 챕터 · 프로젝트면 [화면 맞춤]과 같다. 고른 것이 없으면 거짓이다.
    /// </summary>
    internal bool FocusSelection()
    {
        if (GraphScroll.Viewport.Width <= 0 || GraphScroll.Viewport.Height <= 0)
        {
            return false;
        }

        Rect? CardRect(string? episodeId) =>
            episodeId is not null && _placed.TryGetValue(episodeId, out (double X, double Y) at)
                ? new Rect(at.X, at.Y, CardWidth, CardHeightOf(episodeId))
                : null;

        if (CardRect(_selectedEpisodeId) is { } card)
        {
            CenterAt(card.Center, zoom: 1);
            return true;
        }

        if (_selectedBranchCardId is { } chipId && _chipPlaced.TryGetValue(chipId, out (double X, double Y) chip))
        {
            CenterAt(new Rect(chip.X, chip.Y, BranchChipWidth, ChipHeightOf(chipId)).Center, zoom: 1);
            return true;
        }

        if (_selectedSlot is { } slot && CardRect(slot.Episode) is { } slotCard)
        {
            CenterAt(slotCard.Center, zoom: 1);
            return true;
        }

        if (_selectedEdgeKey is { } edge && CardRect(edge.From) is { } from && CardRect(edge.To) is { } to)
        {
            FitArea(from.Union(to));
            return true;
        }

        if (_inspector == InspectorMode.Scene && InspectedScene(SelectedModel) is { } scene)
        {
            Rect? area = null;

            foreach (ChapterEpisode episode in scene.Episodes)
            {
                if (CardRect(episode.EpisodeId) is { } rect)
                {
                    area = area is { } union ? union.Union(rect) : rect;
                }
            }

            if (area is { } found)
            {
                FitArea(found);
                return true;
            }

            return false;
        }

        if (_inspector is InspectorMode.Chapter or InspectorMode.Project)
        {
            FitChapter();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 그 배율로 판의 그 점(판 좌표)을 화면 가운데에. [구성] 판은 카드만 여백째 감싸서 끝의 카드는 스크롤만으로는 가운데에 못 온다 — 모자란 만큼 판을
    /// 바탕 위에서 더 띄우거나(왼쪽 · 위) 판 바깥을 덧붙인다(오른쪽 · 아래 · <see cref="_sheetTrail"/> — 판을 넓힐 때와 같은 길 · usability UX22 · UX25).
    /// </summary>
    private void CenterAt(Point content, double zoom)
    {
        ApplyZoom(zoom, anchor: null);
        Size viewport = GraphScroll.Viewport;
        double wantX = (content.X * _zoom) + _sheetOffset.X - (viewport.Width / 2);
        double wantY = (content.Y * _zoom) + _sheetOffset.Y - (viewport.Height / 2);

        if (wantX < 0 || wantY < 0)
        {
            _sheetOffset = new Vector(_sheetOffset.X + Math.Max(0, -wantX), _sheetOffset.Y + Math.Max(0, -wantY));
            wantX = Math.Max(0, wantX);
            wantY = Math.Max(0, wantY);
        }

        _sheetTrail = new Vector(
            Math.Max(_sheetTrail.X, wantX + viewport.Width - (_sheetOffset.X + (GraphCanvas.Width * _zoom))),
            Math.Max(_sheetTrail.Y, wantY + viewport.Height - (_sheetOffset.Y + (GraphCanvas.Height * _zoom))));
        ZoomHost.Margin = new Thickness(_sheetOffset.X, _sheetOffset.Y, _sheetTrail.X, _sheetTrail.Y);
        GraphScroll.UpdateLayout();   // 넓어진 바깥을 알아야 스크롤이 거기까지 간다
        GraphScroll.Offset = new Vector(wantX, wantY);
    }

    /// <summary>판의 그 넓이(판 좌표)가 여백째 화면에 들어오게 — 100%보다 키우지 않는다.</summary>
    private void FitArea(Rect area)
    {
        Rect padded = area.Inflate(40);
        double zoom = Math.Min(1.0, Math.Min(GraphScroll.Viewport.Width / padded.Width, GraphScroll.Viewport.Height / padded.Height));
        CenterAt(padded.Center, zoom);
    }

    private void OnGraphPanPressed(object? sender, Avalonia.Input.PointerPressedEventArgs args)
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

    private void OnGraphPanMoved(object? sender, Avalonia.Input.PointerEventArgs args)
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

    private void OnGraphPanReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs args)
    {
        if (!_panning)
        {
            return;
        }

        _panning = false;
        args.Pointer.Capture(null);
        args.Handled = true;
    }
    /// <summary>
    /// 다시 읽을 때마다 <b>모든</b> 챕터의 진행 JSON을 저절로 낸다. 규칙과 장부는
    /// <see cref="ChapterExportService"/>가 갖고, 여기는 <b>언제 부르나</b>만 정한다.
    ///
    /// 결과는 상태줄이 아니라 <b>검증 보고</b>에 세운다. 다시 읽기는 저장 직후에 오고
    /// 그 뒤로 동기화 보고까지 따라오므로, 상태줄에 얹으면 서로를 덮는다(실제로 덮였다).
    /// 못 나간 사유는 어차피 그 보고에 이미 서 있는 오류들이다 — 결론을 그 옆에 둔다.
    /// </summary>
    private void AutoExport()
    {
        _exportRun = _export.ExportAll(_entries, _session?.ProjectPath, _session?.Project);

        // 저장 직후에만 보낸다 — exported/를 방금 새로 쓴 뒤라 보낼 곳이 저장된 판과 같다(2026-11-17 · ExportDelivery).
        if (_session is { ProjectPath: { } path } session && session.TakeDeliveryRequest())
        {
            _exportRun = _exportRun with { Delivery = ExportDelivery.Deliver(path, session.Destinations) };
        }
    }

    /// <summary>검증 보고 맨 위에 세울 내보내기 결론 — 못 나갔을 때만 있다.</summary>
    private string? ExportNotice() => _exportRun.Notice;

    // ── 그리기 ──────────────────────────────────────────────────────────────

    /// <summary>다시 그리기 직전의 판 크기 — 끄는 동안은 판을 이 크기로 둔다(usability UX22).</summary>
    private Size _heldBoardSize;

    /// <summary>
    /// 판(흰 종이)이 바탕 위에 띄워 선 거리(화면 px · usability UX22). 처음 · 챕터를 바꿀 때 · 화면 맞춤 · 배율을 바꿀 때는 판이 화면보다 작은 쪽을
    /// 가운데로 띄우고, 그 밖에는 띄움을 그대로 둔다 — 편집으로 판이 넓어지거나 좁아져도 판은 제자리에 선다(넘치면 스크롤이 생긴다).
    /// ⛔ 정렬로 늘 가운데에 두었을 때는 카드를 판 밖에 놓아 판이 넓어질 때마다 판이 그 반만큼 비켜 서 놓은 카드가 커서에서 밀렸다.
    /// </summary>
    private Vector _sheetOffset;

    /// <summary>다음에 판을 놓을 때 가운데로 놓는가 — 화면 크기를 아직 모르면(배치 전) 알게 될 때 놓는다.</summary>
    private bool _centerSheet = true;

    /// <summary>
    /// 판 오른쪽 · 아래에 덧붙인 바깥(화면 px) — 판을 왼쪽 · 위로 넓히며 카드를 화면 제자리에 두려면 그만큼 더 스크롤해야 하는데, 판이 화면보다
    /// 조금만 넓으면 스크롤할 자리가 모자란다(usability UX25). 가운데로 다시 놓거나 창 크기가 바뀌면 걷는다.
    /// </summary>
    private Vector _sheetTrail;

    /// <summary>
    /// 판의 기준(가장 왼쪽 · 위 카드)이 옮겨진 거리(판 좌표) — 판을 다시 그린 뒤 그만큼 되돌린다(<see cref="CommitDrag"/>). 양수면 기준 카드를
    /// 오른쪽 · 아래로 옮겨 카드가 모두 당겨진 것이고, 음수면 기준보다 왼쪽 · 위에 놓아 판이 그쪽으로 넓어지며 카드가 모두 밀린 것이다.
    /// </summary>
    private Vector _sheetNudge;

    /// <summary>판을 바탕 위에 놓는다 — <see cref="_sheetOffset"/>.</summary>
    /// <param name="keepInView">화면 크기가 바뀌었다(창 크기) — 판이 화면에 들어가면 들어가게 띄움을 줄인다. 편집으로 판 크기가 바뀔 때는 줄이지 않는다
    /// (카드가 놓은 자리에서 밀리지 않게 — 넘치면 스크롤이 생긴다).</param>
    /// <param name="redrawn">판을 막 다시 그렸다 — 기준이 옮겨진 만큼(<see cref="_sheetNudge"/>)을 이때만 되돌린다. 다시 그리기 전에 화면 크기가 바뀌어 불리면
    /// 카드는 아직 옛 기준에 서 있다.</param>
    private void PlaceSheet(bool keepInView = false, bool redrawn = false)
    {
        Size viewport = GraphScroll.Viewport;
        double width = GraphCanvas.Width;
        double height = GraphCanvas.Height;

        if (viewport.Width <= 0 || viewport.Height <= 0 || double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            return;   // 배치 전이거나 그릴 판이 없다 — 화면 크기와 판을 알게 되면 놓는다(⛔ 빈 판을 가운데로 놓아 버리면 그린 판이 반 화면 비켜 섰다)
        }

        Vector nudge = redrawn ? _sheetNudge * _zoom : default;

        if (redrawn)
        {
            _sheetNudge = default;
        }

        Vector? scrollTo = null;

        if (_centerSheet)
        {
            _sheetOffset = new Vector(
                Math.Max(0, viewport.Width - (width * _zoom)) / 2,
                Math.Max(0, viewport.Height - (height * _zoom)) / 2);
            _sheetTrail = default;
            _centerSheet = false;
        }
        else if (nudge != default)
        {
            // 카드가 화면에서 그 자리에 남게 한다 — 당겨졌으면(양수) 보던 자리를 그만큼 되돌리고 모자라면 판을 그만큼 띄우고, 밀렸으면(음수 —
            // 판이 왼쪽 · 위로 넓어졌다) 판을 그만큼 덜 띄우고 모자라면 보던 자리를 그만큼 넘긴다.
            (double marginX, double offsetX) = Absorb(nudge.X, _sheetOffset.X, GraphScroll.Offset.X);
            (double marginY, double offsetY) = Absorb(nudge.Y, _sheetOffset.Y, GraphScroll.Offset.Y);
            _sheetOffset = new Vector(marginX, marginY);

            // 그만큼 스크롤하려면 판 오른쪽 · 아래로 바깥이 그만큼 있어야 한다 — 모자라면 덧붙인다.
            _sheetTrail = new Vector(
                Math.Max(0, offsetX + viewport.Width - (marginX + (width * _zoom))),
                Math.Max(0, offsetY + viewport.Height - (marginY + (height * _zoom))));

            if (Math.Abs(offsetX - GraphScroll.Offset.X) > 0.01 || Math.Abs(offsetY - GraphScroll.Offset.Y) > 0.01)
            {
                scrollTo = new Vector(offsetX, offsetY);
            }
        }

        if (keepInView)
        {
            _sheetOffset = new Vector(
                Math.Min(_sheetOffset.X, Math.Max(0, viewport.Width - (width * _zoom))),
                Math.Min(_sheetOffset.Y, Math.Max(0, viewport.Height - (height * _zoom))));
            _sheetTrail = default;
        }

        ZoomHost.Margin = new Thickness(_sheetOffset.X, _sheetOffset.Y, _sheetTrail.X, _sheetTrail.Y);

        if (scrollTo is { } offset)
        {
            GraphScroll.UpdateLayout();   // 넓어진 판을 알아야 스크롤이 거기까지 간다
            GraphScroll.Offset = offset;
        }

        // 한 축의 옮김을 띄움과 스크롤로 나눠 받는다 — (새 띄움, 새 스크롤).
        static (double Margin, double Offset) Absorb(double nudge, double margin, double offset)
        {
            if (nudge >= 0)
            {
                double back = Math.Min(offset, nudge);
                return (margin + nudge - back, offset - back);
            }

            double less = Math.Min(margin, -nudge);
            return (margin - less, offset + (-nudge - less));
        }
    }

    private void Draw()
    {
        CanvasDrawCount++;

        GraphCanvas.Children.Clear();
        DiagnosticsPanel.Children.Clear();
        _cardById.Clear();
        _cardBase.Clear();
        _emptySlotDots.Clear();
        _lineByEdge.Clear();
        _lineBase.Clear();
        _labelRects.Clear();

        // 그릴 것이 없으면 판도 없다. 이걸 지우지 않으면 큰 챕터를 보다가 못 읽는 챕터로
        // 넘어갔을 때 텅 빈 캔버스가 이전 크기 그대로 남아 스크롤만 넓어진다. 끄는 동안은 그 크기를 아래에서 그대로 쓴다(UX22).
        _heldBoardSize = new Size(GraphCanvas.Width, GraphCanvas.Height);
        GraphCanvas.Width = 0;
        GraphCanvas.Height = 0;

        ChapterEntry? entry = _entries.FirstOrDefault(item => item.ChapterId == _selectedChapterId);

        if (entry is null)
        {
            bool saved = _session?.ProjectPath is not null;

            EmptyText.IsVisible = true;
            EmptyText.Text = !saved
                ? Ui.Structure_NoProjectYetCreateNew
                : _entries.Count == 0
                    ? Ui.Structure_NoChaptersYetClickTo
                    : Ui.Structure_SelectChapterInHierarchyAnd;

            // 빈 판의 첫걸음 — 프로젝트가 없으면 [새 프로젝트], 챕터가 없으면 [＋ 첫 챕터 만들기]([대본]으로 데려가 거기서 만든다 · F2).
            EmptyNewProjectButton.IsVisible = !saved;
            EmptyFirstChapterButton.IsVisible = saved && _entries.Count == 0;

            // 챕터가 없으면 인스펙터는 프로젝트를 보인다 — 판을 그리지 않고 돌아가더라도 인스펙터는 다시 세운다. ⛔ 그 전에는 여기서
            // 돌아가 마지막 챕터를 지워도 그 챕터의 이름 · 챕터ID가 인스펙터에 남았다(소유자 그림 · F2).
            if (_entries.Count == 0)
            {
                _inspector = InspectorMode.Project;
            }

            ApplyInspector();

            DiagnosticsExpander.Header = Ui.Structure_ValidationReport;
            return;
        }

        EmptyText.IsVisible = false;
        EmptyNewProjectButton.IsVisible = false;
        EmptyFirstChapterButton.IsVisible = false;

        ChapterGraphModel model = entry.Model;

        // 에피소드가 없는 챕터 — 판이 그냥 텅 비어 있지 않게 안내문만 세우고 나머지 그리기는 그대로 지나간다(그릴 것이 없을
        // 뿐 검증 · 보고는 여느 챕터와 같다). 에피소드는 [대본] 탭에서 더한다.
        if (model.Episodes.Count == 0)
        {
            EmptyText.IsVisible = true;
            EmptyText.Text = string.Format(Ui.Structure_HasNoEpisodesEpisodesAren, entry.ChapterId);
        }

        DrawCanvas(model);

        DrawDiagnostics(model);
        RefreshPropertyPanel(preserveTyping: true);
    }

    /// <summary>
    /// <b>판만</b> 다시 그린다 — 카드·포트·간선. 검증 보고와 속성 패널은 안 건드린다.
    ///
    /// <see cref="Draw"/>와 따로 둔 이유는 <b>드래그</b>다. 카드를 끄는
    /// 동안 매 프레임 필요한 것은 판뿐인데, <c>Draw</c>를 부르면 검증 보고 패널을 통째로
    /// 다시 세우고 속성 패널의 타이핑까지 건드린다 — 끄는 중에 낼 비용도, 낼 이유도 없다.
    ///
    /// 주의: 자리 계산이 여기 한 벌만 있어야 한다. 끄는 동안과 놓은 뒤가 다른 산식을 쓰면
    /// 놓는 순간 카드가 튄다.
    /// </summary>
    private void DrawCanvas(ChapterGraphModel model)
    {
        GraphCanvas.Children.Clear();
        _cardById.Clear();
        _cardBase.Clear();
        _emptySlotDots.Clear();
        _lineByEdge.Clear();
        _lineBase.Clear();
        _labelRects.Clear();
        _parts.Clear();
        CanvasBuildCount++;

        // 자리는 사람이 소유한다 — 에피소드가 제 X·Y를 지고 있고 드래그가 그것을
        // 고친다. 그리기는 <b>덮지 않는다</b>: 덮으면 옮긴 일이 아무 일도 아니게 된다.
        //
        // 주의: 끄는 중인 카드 하나만 임시 자리(`_dragPosition`)를 쓴다 — 아직 프로젝트에
        //   안 적은 자리다. 놓을 때 한 번 적는다(되돌리기 한 번 = 드래그 한 번).
        _storedPlacement = DisplayPlacement(model).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        _placed = _storedPlacement.ToDictionary(
            pair => pair.Key,
            pair => DrawnAt(pair.Key, pair.Value),
            StringComparer.Ordinal);

        // 카드마다의 높이 — 스탯 줄 수로 정해진다. 선 · 점은 카드보다 먼저 그어지므로 여기서 먼저 센다(UX3).
        _cardHeight.Clear();

        foreach (ChapterEpisode episode in model.Episodes)
        {
            _cardHeight[episode.EpisodeId] = CardHeightFor(StatRows(episode).Rows.Count);
        }

        // 분기 카드 칩은 부르는 에피소드 곁에 붙는다 — 그 에피소드의 자리(끄는 중이면 임시 자리)를 따라간다.
        PlaceBranchChips();

        List<(double Right, double Bottom)> extents =
        [
            .. _placed.Select(pair => (pair.Value.X + CardWidth, pair.Value.Y + CardHeightOf(pair.Key))),
            .. _chipPlaced.Select(pair => (pair.Value.X + BranchChipWidth, pair.Value.Y + ChipHeightOf(pair.Key)))
        ];

        // 끄는 동안은 판 크기를 그대로 둔다 — 판 밖으로 끈 카드는 바깥 위에 선 채 따라오고(배율 칸이 판 밖을 자르지 않는다), 놓으면 판이
        // 그만큼 넓어진다(usability UX22). 끄는 걸음은 판을 다시 그리지 않지만(MoveInPlace) 그 사이에 다시 읽기가 판을 그리면, 넓어진 판이
        // 가운데로 다시 서며 카드가 커서에서 비켜 난다.
        if (_draggingEpisodeId is not null && _heldBoardSize.Width > 0 && _heldBoardSize.Height > 0)
        {
            GraphCanvas.Width = _heldBoardSize.Width;
            GraphCanvas.Height = _heldBoardSize.Height;
        }
        else
        {
            GraphCanvas.Width = extents.Count == 0
                ? CanvasMargin * 2
                : extents.Max(extent => extent.Right) + CanvasMargin;
            GraphCanvas.Height = extents.Count == 0
                ? CanvasMargin * 2
                : extents.Max(extent => extent.Bottom) + CanvasMargin;
        }

        PlaceSheet(redrawn: true);


        // 에피소드별 보이는 선택지 = 문구가 붙은 나가는 간선들 (길 하나가 곧 선택지
        // 하나). 포트 그리기와 간선 그리기가 같은 목록 하나를 본다.
        _optionsByEpisode.Clear();

        foreach (ChapterEpisode episode in model.Episodes)
        {
            // **나가는 길은 전부 포트를 받는다.** 문구 없는 간선을 따로 걸러 내지 않는다 —
            // 문구가 비어 있으면 그것은 종류가 아니라 **고칠 것**이고,
            // 판에서 그 자리를 보여 줘야 기획자가 고친다.
            _optionsByEpisode[episode.EpisodeId] = model.Edges
                .Where(edge =>
                    string.Equals(edge.FromEpisodeId, episode.EpisodeId, StringComparison.Ordinal))
                .ToList();
        }

        // 간선을 먼저 그려야 노드 카드 아래로 깔린다.
        DrawEpisodeRails(model);
        DrawBranchMarkers();

        // 장면 색 — [대본] 판 · 하이어아키와 같은 셈이다(고른 칸이 먼저, 그 밖은 자동 · design/scene-colors.md). 두 탭에서 같은 장면이 한 색이다.
        IReadOnlyDictionary<string, int> sceneColors = SceneColors.Of(ChapterSceneGrouping.Of(model), model.SceneNotes);

        foreach (ChapterEpisode episode in model.Episodes)
        {
            DrawPart([episode.EpisodeId], () => DrawEpisode(model, episode, sceneColors), redrawable: false);
        }

        // 칩은 카드 위에 선다 — 카드 사이가 좁으면 옆 카드에 걸치는데, 그래도 읽히고 눌려야 한다(자리는 에피소드를 옮겨 낸다).
        DrawBranchChips();

        ApplySelectionVisuals();

        // 범위를 긋는 중에 판이 다시 서도 사각형은 남는다.
        if (_rubberBand is not null)
        {
            GraphCanvas.Children.Add(_rubberBand);
        }
    }

    /// <summary>카드를 그릴 자리(여백 포함) — 끄는 중인 카드와 무리는 아직 프로젝트에 적지 않은 임시 자리다.</summary>
    private (double X, double Y) DrawnAt(string episodeId, (double X, double Y) stored)
    {
        if (_groupStart is { } group && group.TryGetValue(episodeId, out (double X, double Y) start))
        {
            return (start.X + _groupDelta.X + CanvasMargin, start.Y + _groupDelta.Y + CanvasMargin);
        }

        if (string.Equals(episodeId, _draggingEpisodeId, StringComparison.Ordinal) && _dragPosition is { } dragged)
        {
            return (dragged.X + CanvasMargin, dragged.Y + CanvasMargin);
        }

        return (stored.X + CanvasMargin, stored.Y + CanvasMargin);
    }

    /// <summary>
    /// <b>분기점</b>을 카드 옆변에 세우고, 이어진 분기는 그 점에서 점선을 긋는다.
    ///
    /// <b>선택지와 다른 종류의 선이다.</b> 문구도 스탯변화도 없다. 점선인 것이 그 뜻이다: 사람이 고르는 길(실선)이
    /// 아니라 <b>반드시 한 번 다녀오는 통로</b>다. 분기점을 만들고 지우고 잇고 끊는 것은 [대본] 탭의 일이고, 여기서는
    /// 그것을 <b>보이기만 한다</b> — 분기점 · 점선은 눌러도 아무 일이 없고, 분기(정보 · 조건)는 그 칩을 눌러 고른다(2026-11-17 소유자 ·
    /// <see cref="BranchKnob"/> · <see cref="DrawBranchLine"/>). 연결만 끊은 분기점도 속 빈 점으로 남아 보인다. ⛔ 그 전에는 분기점 · 점선을
    /// 누르면 그 분기의 정보와 조건이 열렸다.
    ///
    /// <b>다녀갈 곳은 분기 카드다</b>(2026-11-17 소유자) — 에피소드가 아니라 판에 따로 서지 않고, 부르는 에피소드 곁에 <b>작은 칩</b>으로
    /// 붙는다(<see cref="PlaceBranchChips"/>). 점선은 분기점에서 그 칩으로 든다. 분기 카드가 또 들르면 칩의 옆변에도 분기점이 선다.
    ///
    /// ⚠ <b>옆변으로 나간다</b> (v5 · 2026-09-18 소유자: *"분기는 점선인 상태로, 현재
    /// 선택지처럼 우측으로 붙이되"*) — 선택지가 아래변의 칸을 쓰므로 자리가 겹치지 않는다(포트 하나 =
    /// 선택지 하나, v9). <b>다녀갈 카드가 왼쪽에 있으면 왼변으로</b> 나간다(2026-10-30 소유자: *"자기가 나온
    /// 에피소드보다 왼쪽에 있어도 오른쪽으로만 나오는데, 그걸 연출그래프처럼 왼쪽에 있으면 왼쪽으로"*) —
    /// 연출 그래프의 분기 포트와 같은 규칙 하나(<see cref="Vn.Authoring.Graph.EdgeSide.GoesLeft"/>)다. 칩은 집(먼저 부르는 에피소드)의
    /// 오른쪽에 서므로, 다른 곳에서 같은 분기 카드를 부르는 분기점만 왼변으로 나갈 수 있다.
    /// </summary>
    private void DrawBranchMarkers()
    {
        if (_session is null || _selectedChapterId is not { } chapterId)
        {
            return;
        }

        _branchMarkers.Clear();
        _branchChips.Clear();
        _chipBase.Clear();
        _branchPoints = [];

        // 분기점은 카드 옆변에 선다 — 다녀갈 카드가 왼쪽이면 왼변, 아니면(이어지지 않은 분기점도) 오른변이다. 한 변에
        // 여럿이면 옆변 가운데를 중심으로 세로로 늘어선다(대본의 분기점처럼 점에서 선이 나간다).
        IReadOnlyList<ChapterBranchMarkers.BranchPoint> points =
            ChapterBranchMarkers.PointsFor(_session.Editor.Project, chapterId);

        foreach (IGrouping<(string From, bool Left), ChapterBranchMarkers.BranchPoint> side in points
                     .Where(point => FromRect(point) is not null)
                     .GroupBy(point => (point.FromNodeId, GoesLeft(point))))
        {
            Rect from = FromRect(side.First())!.Value;
            List<ChapterBranchMarkers.BranchPoint> stacked = side.ToList();

            for (int index = 0; index < stacked.Count; index++)
            {
                ChapterBranchMarkers.BranchPoint point = stacked[index];
                var at = new Point(
                    side.Key.Left ? from.X : from.Right,
                    from.Y + (from.Height / 2) + ((index - ((stacked.Count - 1) / 2.0)) * BranchPointGap));

                IBrush accent = BranchPalette.Accent(point.Port.PaletteIndex);

                // 분기점은 출발(카드 · 칩)에, 점선은 출발과 도착 칩에 기댄다 — 칩은 집 에피소드를 따라 선다.
                DrawPart(
                    [point.FromEpisodeId ?? _chipHome.GetValueOrDefault(point.FromNodeId),
                     point.Port.TargetNodeId is { } target ? _chipHome.GetValueOrDefault(target) : null],
                    () =>
                    {
                        if (point.Marker is { } marker && ToRect(point) is { } to)
                        {
                            DrawBranchLine(point, marker, at, to, accent);
                        }

                        GraphCanvas.Children.Add(BranchKnob(point, at, accent));
                    },
                    redrawable: false);
            }
        }

        _branchPoints = points;
    }

    /// <summary>이번 그리기의 분기점들 — 칩의 색을 정할 때 다시 본다.</summary>
    private IReadOnlyList<ChapterBranchMarkers.BranchPoint> _branchPoints = [];

    /// <summary>분기 카드 칩들을 세운다 — 에피소드 카드를 그린 뒤에(위에) 선다.</summary>
    private void DrawBranchChips()
    {
        foreach ((string nodeId, (double X, double Y) at) in _chipPlaced)
        {
            // 칩의 색은 그 분기 카드를 처음 부르는 분기점의 색이다 — [대본] 탭의 그 분기 포트 · 선과 같다.
            int palette = _branchPoints.FirstOrDefault(point =>
                string.Equals(point.Port.TargetNodeId, nodeId, StringComparison.Ordinal))?.Port.PaletteIndex ?? 0;
            DrawPart([_chipHome.GetValueOrDefault(nodeId)], () => DrawBranchChip(nodeId, at, BranchPalette.Accent(palette)), redrawable: false);
        }
    }

    /// <summary>
    /// 분기 카드 칩의 자리 — 집(그 분기 카드를 먼저 부르는 에피소드 — <see cref="ChapterBranchCards.HomesOf"/>)의 오른쪽에 대본 차례로 세로로
    /// 늘어선다. 자리를 저장하지 않는다 — 끌어 옮기지 않고 에피소드를 따라간다(소유자). ⛔ 분기 카드가 부르는 분기 카드를 한 칸 들여 세우던
    /// 것은 2026-11-19에 걷혔다 — 분기 카드는 분기를 내지 않는다(usability UX27). 조건이 걸린 칩은 두 줄이라 높고(<see cref="BranchChipTallHeight"/>),
    /// 그 아래 칩은 그만큼 내려선다.
    /// </summary>
    private void PlaceBranchChips()
    {
        _chipPlaced = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        _chipHeight.Clear();
        _chipHome.Clear();

        if (_session is null || _selectedChapterId is not { } chapterId)
        {
            return;
        }

        var below = new Dictionary<string, double>(StringComparer.Ordinal);   // 에피소드마다 다음 칩이 설 높이(카드 위변에서)

        foreach (BranchCardHome home in ChapterBranchCards.HomesOf(_session.Editor.Project, chapterId))
        {
            if (!_placed.TryGetValue(home.EpisodeId, out (double X, double Y) at))
            {
                continue;
            }

            double top = below.GetValueOrDefault(home.EpisodeId);
            double height = BranchEntryCondition.Read(_session.Editor.Project, home.Card.Id).Label is null ? BranchChipHeight : BranchChipTallHeight;
            below[home.EpisodeId] = top + height + BranchChipGap;

            _chipHome[home.Card.Id] = home.EpisodeId;
            _chipHeight[home.Card.Id] = height;
            _chipPlaced[home.Card.Id] = (at.X + CardWidth + BranchChipOffset, at.Y + top);
        }
    }

    /// <summary>분기점이 선 카드의 네모 — 에피소드 카드 또는 분기 카드 칩. 판에 없으면 <c>null</c>.</summary>
    private Rect? FromRect(ChapterBranchMarkers.BranchPoint point) =>
        point.FromEpisodeId is { } episode && _placed.TryGetValue(episode, out (double X, double Y) card)
            ? new Rect(card.X, card.Y, CardWidth, CardHeightOf(episode))
            : ChipRect(point.FromNodeId);

    /// <summary>다녀갈 분기 카드의 칩 네모. 이어지지 않았거나 판에 없으면 <c>null</c>.</summary>
    private Rect? ToRect(ChapterBranchMarkers.BranchPoint point) =>
        point.Port.TargetNodeId is { } target ? ChipRect(target) : null;

    private Rect? ChipRect(string nodeId) =>
        _chipPlaced.TryGetValue(nodeId, out (double X, double Y) chip)
            ? new Rect(chip.X, chip.Y, BranchChipWidth, ChipHeightOf(nodeId))
            : null;

    /// <summary>분기점이 설 변 — 다녀갈 카드가 이 카드보다 왼쪽이면 왼변이다. 이어지지 않은 분기점은 오른변이다.</summary>
    private bool GoesLeft(ChapterBranchMarkers.BranchPoint point) =>
        FromRect(point) is { } from &&
        ToRect(point) is { } target &&
        Vn.Authoring.Graph.EdgeSide.GoesLeft(from.X, from.Width, target.X, target.Width);

    /// <summary>
    /// 이어진 분기의 점선 — 분기점에서 나가 도착(칩 · 카드)의 <b>가까운 세로 변</b> 가운데로 든다(둘이 가로로 겹쳐 서도 선이
    /// 카드를 관통하지 않는다). <b>누르지 않는다</b>(2026-11-17 소유자) — 분기를 고르는 것은 칩이다(점선을 누른 것과 칩을 누른 것이
    /// 같아졌다). ⛔ 전에는 굵은 누름 선을 겹쳐 누르면 그 분기를 골랐다(조건 칸).
    /// </summary>
    private void DrawBranchLine(
        ChapterBranchMarkers.BranchPoint point, ChapterBranchMarkers.Marker marker, Point start, Rect to, IBrush accent)
    {
        bool goesLeft = GoesLeft(point);
        double entryX = goesLeft
            ? to.Right <= start.X ? to.Right : to.X
            : start.X <= to.X ? to.X : to.Right;

        var line = new Line
        {
            StartPoint = start,
            EndPoint = new Point(entryX, to.Y + (to.Height / 2)),
            Stroke = accent,
            StrokeThickness = BranchThickness,
            StrokeDashArray = new AvaloniaList<double> { 5, 4 },
            IsHitTestVisible = false,
            // 화면 없는 검증이 "무엇이 그려졌는지"를 색·좌표로 역추론하지 않게 한다.
            Tag = $"분기:{marker.FromEpisodeId ?? marker.FromNodeId}->{marker.ToName}"
        };

        GraphCanvas.Children.Add(line);
        _branchMarkers.Add(marker);
    }

    /// <summary>
    /// 분기 카드 칩 — 「⑂ 이름」 한 줄. 누르면 인스펙터에 그 분기 카드(부르는 곳 · 조건 · 대본)가 선다. <b>읽기 전용</b>이다 — 끌어
    /// 옮기지 않고(자리는 부르는 에피소드를 따라간다) 대본도 여기서 고치지 않는다. 고치는 것은 조건뿐이다(소유자).
    /// 이름은 에피소드 카드 머리와 같이 「ID로 보기」를 따른다 — 켜면 분기 카드 ID(= Yarn 노드 이름), 끄면 제목(2026-11-23 소유자 — 「분기 칩도 ID로
    /// 보이게 해주십시오」 · ⛔ 그 전에는 체크와 상관없이 제목). 툴팁은 늘 「제목」(ID) 둘 다다.
    /// 조건이 걸리면 칩이 높아져 위 줄에 「조건: 라벨」, 아래 줄에 「⑂ 이름」이다(같은 날 소유자 — 「오른쪽에 ...으로 보이게 잘리는 대신, ID 위쪽으로
    /// 조건문을 보여주는 건 어때? 분기 칩 자체의 높이를 늘리고」 · ⛔ 그 전에는 「⑂ 이름 · 라벨」 한 줄이라 ID 뒤의 조건이 말줄임으로 잘렸다).
    /// </summary>
    private void DrawBranchChip(string nodeId, (double X, double Y) at, IBrush accent)
    {
        if (_session?.Project.FindDialogue(nodeId) is not { } card)
        {
            return;
        }

        BranchEntryCondition.State state = BranchEntryCondition.Read(_session.Project, nodeId);
        string name = _session.ShowIds ? card.Name : EpisodeNaming.BranchTitleOf(card);   // 「ID로 보기」

        var chip = new Border
        {
            Width = BranchChipWidth,
            Height = ChipHeightOf(nodeId),
            Padding = new Thickness(7, 0),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1.2),
            BorderBrush = accent,
            Background = Skin.StructureBranchChipSurface.Brush(),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Child = BranchChipText(name, state.Label),
            Tag = new BranchCardMark(nodeId, EpisodeNaming.BranchTitleOf(card))
        };

        ToolTip.SetTip(chip,
            string.Format(Ui.Structure_BranchCardCardVisitedAnd, EpisodeNaming.BranchLabelOf(card)) +
            BranchConditionNote.Sentence(state, BranchConditionNote.IsUndecided(_session.Project, nodeId, state.Label)) +
            Ui.Structure_ClickToOpenItsCondition);

        chip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true; // 캔버스(빈 공간 = 선택 해제 · 범위 선택)까지 흘러가면 곧바로 풀린다
            UiGuard.Run(_session, Ui.Structure_SelectBranchCard, () => SelectBranchCard(nodeId));

            // 두 번 누름 — 에피소드 카드처럼 셸에 알린다([대본] 탭의 그 분기 카드로).
            if (e.ClickCount >= 2)
            {
                BranchCardOpened?.Invoke(nodeId);
            }
        };

        _branchChips[nodeId] = chip;
        _chipBase[nodeId] = (chip.BorderBrush, chip.BorderThickness);
        Canvas.SetLeft(chip, at.X);
        Canvas.SetTop(chip, at.Y);
        GraphCanvas.Children.Add(chip);
    }

    /// <summary>칩의 글 — 「⑂ 이름」 한 줄, 조건이 있으면 그 위에 작은 글로 「조건: 라벨」 한 줄을 더한다. 넘치면 말줄임(툴팁에 온전히 있다).</summary>
    private static Control BranchChipText(string name, string? condition)
    {
        var nameLine = new TextBlock
        {
            Text = $"⑂ {name}",
            FontSize = 10.5,
            Foreground = Skin.StructureInk.Brush(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (condition is null)
        {
            return nameLine;
        }

        return new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = string.Format(Ui.Structure_Condition, condition),
                    FontSize = 9.5,
                    Foreground = Skin.StructureInkSoft.Brush(),
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                nameLine
            }
        };
    }

    /// <summary>
    /// 분기점 — 이어진 분기는 속이 찬 점, 연결만 끊은 분기점은 속 빈 점이다. <b>보이기만 한다</b>(2026-11-17 소유자) — 마우스를 올리면
    /// 어디로 가는지(이어지지 않았으면 [대본] 탭에서 잇는다는 것)를 말하고, 눌러도 아무 일이 없다(빈 판으로 흘려 선택을 풀지도 않는다).
    /// 분기를 고르는 것은 칩이다. ⛔ 전에는 누르면 점선과 같은 분기 패널이 열렸다.
    /// </summary>
    private Ellipse BranchKnob(ChapterBranchMarkers.BranchPoint point, Point at, IBrush accent)
    {
        var knob = new Ellipse
        {
            Width = BranchKnobRadius * 2,
            Height = BranchKnobRadius * 2,
            Fill = point.ToName is null ? Brushes.Transparent : accent,
            Stroke = accent,
            StrokeThickness = 2,
            ZIndex = 50,
            Tag = BranchKnobTag + point.Key
        };
        Canvas.SetLeft(knob, at.X - BranchKnobRadius);
        Canvas.SetTop(knob, at.Y - BranchKnobRadius);
        ToolTip.SetTip(knob, point.ToName is { } to
            ? string.Format(Ui.Structure_BranchClickThatBranchCard, to)
            : Ui.Structure_UnconnectedBranchPointThereS);

        knob.PointerPressed += (_, e) => e.Handled = true;   // 아무 일도 없다 — 빈 판(선택 풀기 · 범위 선택)으로도 흘리지 않는다

        return knob;
    }


    // ⛔ 픽스처 (G6) — 2026-08-24에 화면에서 걷었다 (소유자: "꽤 오래 다뤘는데 단 한 번도
    // 안 썼어"). 여기 있던 것: `_selectedFixture`·`RefreshFixtureCombo`·`WalkSelectedFixture`,
    // 그리고 걸은 경로를 초록으로 칠하던 `onPath`/`pathEdges` 인자들.
    //
    // 콤보를 없애면 고를 길이 없고, 고를 수 없는 하이라이트는 켤 수도 끌 수도 없다 —
    // 시트의 `활성` 픽스처만 남겨 두면 견본 챕터가 초록으로 켜진 채 <b>끄는 손잡이 없이</b>
    // 서 있게 된다. 그래서 화면 쪽은 통째로 걷는 것이 옳다.
    //
    // ⚠ <b>`ChapterDocument.Fixtures`와 `ChapterFixtureWalker`는 안 지웠다</b> — 픽스처는 챕터의
    // 데이터이고(옛 엑셀 `픽스처` 시트에서 왔다), 걷기는 저작 계층의 순수 함수다(제 테스트가 있다).
    // 되살릴 때 필요한 것은 콤보 하나와 이 자리의 걷기 호출뿐이다.

    /// <summary>
    /// 언제나 서 있는 선택지 칸 수 (R7 §2 ③ — 소유자: "딱 3개").
    ///
    /// ⚠ <b>상한이 아니다.</b> 간선이 넷이면 넷을 다 그린다 — 넷째를 숨기면 사람은 줄이
    /// <b>사라진</b> 줄 안다. 규칙의 주인은 <c>GraphProjectionBuilder.ChoiceSlots</c>이고
    /// 여기는 그 수를 그릴 뿐이다.
    /// </summary>
    private const int ChoiceSlots = 3;

    /// <summary>포트 점의 반지름 — 카드 변 위에 걸터앉는다.</summary>
    private const double PortRadius = 4.5;

    /// <summary>선택지 문구의 가장 넓은 폭 — 넘으면 「…」로 줄이고 마우스를 올리면 전체가 보인다.</summary>
    private const double LabelMaxWidth = 160;

    /// <summary>
    /// 선택지 문구가 지는 표 — 화면 테스트가 이것으로 찾는다. 글자 표(string)가 아닌 것은 판의 카드가 글자 표(에피소드 Id)를
    /// 지기 때문이다 — 「글자 표를 진 Border = 카드」를 깨지 않는다.
    /// </summary>
    internal sealed record EdgeLabelMark(string FromEpisodeId, string ToEpisodeId, string Label);

    /// <summary>이번 그리기에서 이미 놓인 선택지 문구의 자리 — 다음 문구가 이것을 비켜 선다.</summary>
    private readonly List<Rect> _labelRects = new();

    /// <summary>빈 선택지 칸 점이 지는 표 — 화면 테스트가 이것으로 찾는다. 뒤에 「{에피소드}#{칸}」이 붙는다.</summary>
    internal const string EmptySlotTag = "empty-slot:";

    /// <summary>빈 선택지 칸 점의 테두리 — 고르면 파랗게 굵어지고 풀리면 이리 돌아온다.</summary>
    private static IBrush EmptySlotBrush => Skin.StructureSlot.Brush();

    /// <summary>판에 선 빈 선택지 칸 점들 — (에피소드, 칸).</summary>
    private readonly Dictionary<(string Episode, int Index), Ellipse> _emptySlotDots = new();

    /// <summary>고른 빈 선택지 칸. 없으면 <c>null</c>.</summary>
    private (string Episode, int Index)? _selectedSlot;

    /// <summary>에피소드 → 보이는 선택지(문구 붙은 나가는 간선). Draw가 채우고 카드·간선이 함께 본다.</summary>
    private readonly Dictionary<string, List<ChapterEdge>> _optionsByEpisode = new(StringComparer.Ordinal);

    /// <summary>이 에피소드가 낼 칸 수 — 언제나 셋 이상이고, 간선이 더 많으면 그만큼이다.</summary>
    private static int SlotsFor(int optionCount) => Math.Max(ChoiceSlots, optionCount);

    /// <summary>
    /// 판에 세울 칸 수 — 툴바의 「빈 선택지 숨기기」를 켜면 이은 칸만이다(빈 칸을 세지 않으니 이은 선택지가 카드 아래에 고르게 선다).
    /// </summary>
    private int ShownSlotsFor(int optionCount) => _session?.HideEmptyChoices == true ? optionCount : SlotsFor(optionCount);

    /// <summary>
    /// 선택지 포트의 가로 자리 — <b>카드 아래변</b>에 고르게 선다 (v5, 2026-09-18).
    /// 카드 그리기와 간선 그리기가 같은 산식 하나를 본다.
    /// </summary>
    private static double PortX(double cardX, int index, int slots) =>
        cardX + (CardWidth * (index + 1) / (slots + 1.0));

    /// <summary>모든 길이 들어오는 <b>입구 한 점</b> — 카드 위변 가운데.</summary>
    private static Point EntryDot(double cardX, double cardY) =>
        new(cardX + (CardWidth / 2), cardY);

    /// <summary>
    /// <b>선택지는 아래로, 분기는 오른쪽으로</b> (v5 · 2026-09-18 소유자).
    ///
    /// ⛔ <b>v4까지 정확히 반대였다</b> — 선택지 포트가 카드 <i>오른쪽</i>, 분기가 <i>아래</i>.
    /// 연출 그래프는 그 반대였고(분기가 오른쪽, 선택지가 아래), 소유자가 그것을 짚었다:
    /// *"여기는 그게 반대로 분기가 아래, 선택지가 우측이다보니 헷갈립니다."*
    /// 판이 둘인데 같은 것이 다른 변에서 나가면 손이 매번 헷갈린다.
    ///
    /// 그래서 아래변에 <b>점 세 개가 가로로</b> 서고(간선이 더 많으면 그만큼), 나간 길은
    /// 모두 도착 카드 <b>위변의 점 하나</b>로 들어간다 — 들어오는 자리가 하나면 "이 카드로
    /// 오는 길이 몇인가"가 한눈에 보인다.
    /// </summary>
    private void DrawEpisodeRails(ChapterGraphModel model)
    {
        foreach (ChapterEpisode episode in model.Episodes)
        {
            List<ChapterEdge> edges = model.Edges
                .Where(edge => string.Equals(edge.FromEpisodeId, episode.EpisodeId, StringComparison.Ordinal))
                .ToList();

            List<ChapterEdge> options = _optionsByEpisode.GetValueOrDefault(episode.EpisodeId) ?? [];

            // 자리는 늘 있다 — 자리를 정하는 쪽(DisplayPlacement)이 에피소드마다 하나씩 낸다.
            (double X, double Y) position = _placed[episode.EpisodeId];

            int slots = ShownSlotsFor(options.Count);

            for (int index = 0; index < options.Count; index++)
            {
                ChapterEdge edge = options[index];
                int slot = index;

                // 선은 두 카드에 기댄다 — 끄는 동안 한쪽만 움직였으면 그 선만 지금 자리로 다시 긋는다(PortOf가 그때의 자리를 읽는다).
                DrawPart([edge.FromEpisodeId, edge.ToEpisodeId], () => DrawPortEdge(edge, PortOf(edge.FromEpisodeId, slot, slots)), redrawable: true);
            }
        }
    }

    /// <summary>
    /// 포트에서 도착 카드로 <b>직선</b> 하나. 도착이 오른쪽이면 왼쪽 변으로, 왼쪽이면
    /// 오른쪽 변으로 들어간다(▶ ◀).
    ///
    /// 구간이 하나뿐이라 <b>히트 선이 간선 전체를 덮는다</b> — 예전에는 첫 가로 구간
    /// 위에서만 눌렸다.
    /// </summary>
    private void DrawPortEdge(ChapterEdge edge, Point port)
    {
        // 색이 말하는 것은 하나뿐이다 — 관문이 걸렸나(주황) 아닌가(회색). 픽스처 경로를
        // 초록으로 칠하던 갈래는 2026-08-24에 걷혔다.
        IBrush stroke = edge.ConditionLabel is null
            ? Skin.StructureEdge.Brush()
            : Skin.StructureEdgeConditioned.Brush();
        const double thickness = 1.6;

        var segments = new List<Line>();

        void Segment(double x1, double y1, double x2, double y2)
        {
            var line = new Line
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                Stroke = stroke,
                StrokeThickness = thickness,
                Tag = EdgeTag(edge),
                IsHitTestVisible = false
            };


            GraphCanvas.Children.Add(line);
            segments.Add(line);
        }

        if (!_placed.TryGetValue(edge.ToEpisodeId, out (double X, double Y) target))
        {
            return; // 없는 도착지는 구조 검증이 이미 잡았다.
        }

        // ⚠ 직선이다 (2026-08-23 소유자 보고: "선이 완전히 겹치다보니 어디로 이어지는지
        // 확인하기가 힘들어").
        //
        // 예전에는 직교 3구간(가로 → 세로 → 가로)으로 돌렸는데, 꺾이는 x가
        // `(port.X + target.X) / 2`라 **같은 열로 가는 간선들이 세로 구간을 공유**했다.
        // 결과적으로 한 에피소드에서 나가는 길이 여럿이면 화면에서 한 줄로 보였다.
        //
        // 직선은 그 문제가 원천적으로 없다: 포트마다 x가 다르므로 **기울기가 저절로 갈린다.**
        //
        // ⚠ 도착은 <b>위변의 점 하나</b>다 (v5) — 들어오는 자리를 하나로 모으면 "이 카드로
        //   오는 길이 몇인가"가 한눈에 보인다. 출발이 갈리므로 선이 겹치지도 않는다.
        Point entry = EntryDot(target.X, target.Y);
        double entryY = entry.Y - 8;

        Segment(port.X, port.Y, entry.X, entryY);

        // 화살촉은 진입하는 변을 향한다 — 위에서 아래로 꽂힌다(기본 방향이 아래다).
        AddEdgeArrow(entry.X, entryY + 1, stroke);

        RegisterEdgeLines(edge, segments, stroke, thickness);

        // 첫 가로 구간 위 히트 선 — 포트 밖에서도 간선을 누를 수 있다.
        var hit = new Line
        {
            StartPoint = segments[0].StartPoint,
            EndPoint = segments[0].EndPoint,
            Stroke = Brushes.Transparent,
            StrokeThickness = 12,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        string hitFrom = edge.FromEpisodeId;
        string hitTo = edge.ToEpisodeId;
        string hitLabel = EdgeLabelKey(edge);
        hit.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Run(_session, Ui.Structure_SelectPath, () => PressChoice(hitFrom, hitTo, hitLabel, e.ClickCount));
        };
        GraphCanvas.Children.Add(hit);

        AddEdgeLabel(edge, port, new Point(entry.X, entryY));
    }

    /// <summary>아래로 드는 화살촉 — 도착 카드 위 가운데로 들어간다.</summary>
    private void AddEdgeArrow(double x, double y, IBrush fill)
    {
        var arrow = new Avalonia.Controls.Shapes.Polygon
        {
            Fill = fill,
            IsHitTestVisible = false,
            Points = [new Point(-4, 0), new Point(0, 7), new Point(4, 0)]
        };

        Canvas.SetLeft(arrow, x);
        Canvas.SetTop(arrow, y);
        GraphCanvas.Children.Add(arrow);
    }

    /// <summary>
    /// 간선의 <b>문구</b>를 그 선 위에 놓는다 — 사람이 간선을 누르려고 겨누는 바로
    /// 그 자리다. 누르면 그 간선이 선택된다.
    ///
    /// 자리는 선 한가운데가 먼저다. 이미 놓인 문구와 겹치면 <b>제 선을 따라</b> 위아래로 번갈아 비켜 빈자리를 찾는다
    /// (<see cref="LabelSpots"/>) — 한 카드에서 나간 선택지 여럿이 같은 카드로 모이면 선 가운데가 거의 한 점이라 문구가
    /// 하나로 포개졌다. 제 선 위에 있어야 어느 문구가 어느 선인지 헷갈리지 않는다. 빈자리가 없으면 가장 덜 겹치는 자리다.
    /// 문구가 길면 <see cref="LabelMaxWidth"/>에서 줄이고(…) 마우스를 올리면 전체가 보인다. 문구 위에는 표시조건 · 해금조건이,
    /// 아래에는 스탯변화가 선다(있을 때만) — 조건과 스탯은 줄이지 않는다.
    ///
    /// ⭐ <b>v5에서 문구가 여기로 왔다</b> (2026-09-18). 전에는 카드 오른변 안쪽에 포트마다
    /// 한 줄씩 적었는데, 포트가 아래변으로 내려가면서 한 칸에 남는 폭이 카드의 1/4이 됐다 —
    /// 한글 몇 자에서 잘린다. 옮기지 않았다면 판에서 <b>문구가 통째로 사라졌을</b> 자리다.
    ///
    /// ⚠ 말하는 것은 옛 카드 안 문구와 <b>똑같다</b>: 자동이면 `AUTO`, 문구가 비면
    /// `⚠ 문구 없음`(종류가 아니라 고칠 것이다, v12), 그 뒤에 해금조건. 규칙이 한 벌이라
    /// 포트 간선과 직행선이 같은 말을 한다 — 전에는 직행선만 이 라벨을 갖고 있었다.
    /// </summary>
    private void AddEdgeLabel(ChapterEdge edge, Point from, Point to)
    {
        string option = EdgeOptionText(edge);

        IBrush ink = (edge.Auto ? Skin.Accent : edge.HasNoOptionLabel ? Skin.DangerDeep : Skin.StructureEdgeInk).Brush();

        // 세 줄이다 — 위는 이 선택지가 언제 보이고(표시조건) 언제 고를 수 있는지(해금조건), 가운데는 문구, 아래는 이 길을
        // 건너면 바뀌는 스탯. 없는 줄은 서지 않는다. 줄이는 것은 문구뿐이다 — 조건과 스탯은 늘 다 보인다.
        var rows = new StackPanel { Spacing = 1 };
        List<string> tip = [option];

        var gates = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };

        if (edge.VisibleConditionLabel is { } visible)
        {
            gates.Children.Add(LabelPart(string.Format(Ui.Structure_Show, visible), 9, VisibleConditionInk));
            tip.Add(string.Format(Ui.Structure_ShowConditionOtherwiseItIsn, visible));
        }

        if (edge.ConditionLabel is { } unlock)
        {
            gates.Children.Add(LabelPart(string.Format(Ui.Structure_Unlock, unlock), 9, UnlockConditionInk));
            tip.Add(string.Format(Ui.Structure_UnlockConditionOtherwiseItS, unlock));
        }

        if (gates.Children.Count > 0)
        {
            gates.Tag = EdgeLabelRow.Conditions;
            rows.Children.Add(gates);
        }

        TextBlock optionText = LabelPart(option, 10, ink);
        optionText.MaxWidth = LabelMaxWidth;
        optionText.TextTrimming = TextTrimming.CharacterEllipsis;
        optionText.HorizontalAlignment = HorizontalAlignment.Center;
        optionText.Tag = EdgeLabelRow.Option;
        rows.Children.Add(optionText);

        if (edge.StatChanges.Count > 0)
        {
            var stats = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Tag = EdgeLabelRow.Stats
            };

            foreach (StatDelta delta in edge.StatChanges)
            {
                (string shown, IBrush colour) = DescribeDelta(delta);
                stats.Children.Add(LabelPart(shown, 9, colour));
            }

            rows.Children.Add(stats);
            tip.Add(Ui.Structure_StatChange + string.Join(" · ", edge.StatChanges.Select(delta => DescribeDelta(delta).Text)));
        }

        // 선 위의 작은 알약 — 옅은 구분선 테두리가 있어 판의 면과 갈린다(2026-11-26 — 디자인 점검 4번 · ⛔ 그 전에는 바탕이 판과 거의 같아 선 위에 맨 글자처럼 보였다).
        var text = new Border
        {
            Background = Skin.StructureLabelSurface.Brush(),
            BorderBrush = Skin.Divider.Brush(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 1),
            Child = rows,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Tag = new EdgeLabelMark(edge.FromEpisodeId, edge.ToEpisodeId, EdgeLabelKey(edge))
        };
        ToolTip.SetTip(text, string.Join("\n", tip));

        string fromId = edge.FromEpisodeId;
        string toId = edge.ToEpisodeId;
        string labelKey = EdgeLabelKey(edge);

        // 히트 선보다 나중에 그려져 클릭을 삼키므로, 라벨 클릭도 간선 선택으로 잇는다.
        text.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Run(_session, Ui.Structure_SelectPath, () => PressChoice(fromId, toId, labelKey, e.ClickCount));
        };

        text.Measure(Size.Infinity);
        Size size = text.DesiredSize;
        Rect spot = LabelSpots(from, to, size)
            .Select(rect => (Rect: rect, Overlap: _labelRects.Sum(placed => OverlapArea(placed, rect))))
            .Aggregate((best, next) => best.Overlap <= 0 || next.Overlap >= best.Overlap ? best : next)
            .Rect;

        _labelRects.Add(spot);
        Canvas.SetLeft(text, spot.X);
        Canvas.SetTop(text, spot.Y);
        GraphCanvas.Children.Add(text);
    }

    /// <summary>선택지 문구 상자의 줄 — 위(조건) · 가운데(문구) · 아래(스탯변화). 화면 테스트가 이것으로 줄을 가린다.</summary>
    internal enum EdgeLabelRow
    {
        Conditions,
        Option,
        Stats
    }

    private static IBrush VisibleConditionInk => Skin.ConditionVisible.Brush();
    private static IBrush UnlockConditionInk => Skin.ConditionUnlock.Brush();

    private static TextBlock LabelPart(string text, double size, IBrush ink) =>
        new() { Text = text, FontSize = size, Foreground = ink };

    /// <summary>
    /// 스탯 변화 하나를 작가의 말로 — 키로 「trust +1」, 활성/비활성 스탯은 「열쇠 → 활성」(2026-11-19 · usability UX12 — ⛔ 「→ 예」 · 「→ 아니오」).
    /// 오르면 초록, 내리면 빨강이다.
    /// </summary>
    private (string Text, IBrush Ink) DescribeDelta(StatDelta delta) =>
    (
        DeltaText(SelectedModel?.Stats, delta),
        (delta.IsSet ? Skin.Accent : delta.Amount < 0 ? Skin.DangerDeep : Skin.Success).Brush()
    );

    /// <summary>스탯 변화 하나의 글 — 스탯은 키만 있다(writer-fields W2). [대본] 탭 인스펙터도 같은 말을 쓴다.</summary>
    internal static string DeltaText(IReadOnlyList<ChapterStat>? stats, StatDelta delta)
    {
        string name = delta.Key;

        return delta.IsSet
            ? delta.Amount != 0 ? string.Format(Ui.Structure_On, name) : string.Format(Ui.Structure_Off, name)
            : $"{name} {delta.Amount.ToString("+0;-0;+0", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    /// <summary>문구가 설 만한 자리들 — 선 한가운데부터 제 선을 따라 위아래로 번갈아 멀어진다(선의 15%~85%).</summary>
    private static IEnumerable<Rect> LabelSpots(Point from, Point to, Size size)
    {
        foreach (double t in LabelStops)
        {
            var center = new Point(from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
            yield return new Rect(center.X - (size.Width / 2), center.Y - (size.Height / 2), size.Width, size.Height);
        }
    }

    /// <summary>선 위에서 문구를 놓아 보는 차례 — 한가운데가 먼저, 겹치면 위(출발 쪽) · 아래(도착 쪽)로 번갈아.</summary>
    private static readonly double[] LabelStops = [0.5, 0.38, 0.62, 0.26, 0.74, 0.15, 0.85];

    /// <summary>두 문구가 겹친 넓이 — 사이에 2px 틈을 두어 붙어 서는 것도 겹친 것으로 친다.</summary>
    private static double OverlapArea(Rect a, Rect b)
    {
        Rect widened = a.Inflate(2);
        double width = Math.Min(widened.Right, b.Right) - Math.Max(widened.Left, b.Left);
        double height = Math.Min(widened.Bottom, b.Bottom) - Math.Max(widened.Top, b.Top);
        return width > 0 && height > 0 ? width * height : 0;
    }

    private void RegisterEdgeLines(ChapterEdge edge, List<Line> segments, IBrush stroke, double thickness)
    {
        (string, string, string) key = (edge.FromEpisodeId, edge.ToEpisodeId, EdgeLabelKey(edge));
        _lineByEdge[key] = segments;
        _lineBase[key] = (stroke, thickness);
    }

    private void DrawEpisode(ChapterGraphModel model, ChapterEpisode episode, IReadOnlyDictionary<string, int> sceneColors)
    {
        // 모델의 오류든 도달 불가든 노드에는 같은 ⚠로 선다 — 기획자에게는 둘 다 "고칠 것"이다.
        bool hasError = model.EpisodeHasError(episode) || IsUnreachable(episode);

        // 머리는 두 줄이다 — 위는 칩(장면 시작점 · 장면 · 🔒 · ⚠), 아래는 에피소드 이름(2026-11-18 소유자 — 「에피소드 이름이 시작점과 새장면
        // 아래쪽에」 · chrome-card-header U2). ⛔ 그 전에는 칩 뒤 같은 줄에 이름이 섰다.
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        bool showIds = _session?.ShowIds != false;   // 「ID로 보기」

        // 시작 에피소드(첫 행)가 곧 Root다 — 도달성의 씨앗이자 분기 저작의 출발점.
        bool start = ReferenceEquals(model.StartEpisode, episode);

        if (model.IsSceneRoot(episode))
        {
            // 「장면 시작점」(2026-11-18 소유자 — ⛔ 같은 날 H2에서 「시작점」으로 줄였다가 되돌렸다). 색은 [대본]의 장면 첫 에피소드와 같은 시작의 색이다
            // (2026-11-26 — ⛔ 그 전에는 강조색이었는데, 강조색은 「고른 것」의 파랑이다).
            Border root = CardChip(Ui.Structure_SceneStart, Skin.SceneRootAccent, FontWeight.Bold);
            ToolTip.SetTip(root, start
                ? Ui.Structure_StartingEpisodeAndSceneStart
                : Ui.Structure_SceneStartEnteredFromAnother);
            header.Children.Add(root);
        }

        // 장면ID만(2026-11-18 소유자 · ⛔ 그 전에는 「장면 · {장면ID}」) — 「ID로 보기」를 끄면 장면 이름. 무엇인지는 툴팁이 말한다.
        //
        // 꼬리표는 그 장면의 색(점 색 — SceneMark1 ~)이고, 카드 왼쪽 가장자리에 같은 색의 얇은 띠가 선다(<see cref="SceneStripeSurface"/>) — [대본] 판의 장면 면 ·
        // 하이어아키의 점과 한 색이라 두 탭에서 같은 장면을 색으로 찾는다(design/scene-colors.md ⑥ · 2026-11-27 소유자 — 「권고대로」). [구성] 판에는 장면
        // 테두리를 그리지 않는다: 카드가 흐름 차례로 서고 따로 옮겨져 한 장면의 카드가 흩어질 수 있어, 감싸는 면은 남의 카드까지 품고 서로 겹친다.
        // ⛔ 그 전에는 꼬리표가 모든 장면에서 회색(SceneChip)이었다.
        Color sceneMark = Skin.SceneMarkOf(SceneColors.ColorOf(sceneColors, episode.SceneId));
        Border scene = CardChip(
            showIds ? episode.SceneId : ChapterSceneNotes.NameOf(model.SceneNotes, episode.SceneId) is { Length: > 0 } sceneName
                ? sceneName
                : episode.SceneId,
            sceneMark,
            FontWeight.SemiBold);
        scene.Tag = SceneChipTag;
        ToolTip.SetTip(scene, string.Format(Ui.Structure_SceneEpisodesWithSameScene, episode.SceneId));
        header.Children.Add(scene);

        // 관문은 v8에서 길(간선)의 것이 됐다 — 카드는 "들어오는 길에 관문이 있다"를 표시한다.
        if (GatedIncoming(model, episode) is { Count: > 0 } gatedPaths)
        {
            var lockMark = new TextBlock { Text = "🔒", FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(lockMark, GateSummary(gatedPaths));
            header.Children.Add(lockMark);
        }

        if (hasError)
        {
            header.Children.Add(new TextBlock
            {
                Text = "⚠",
                FontSize = 11,
                Foreground = Skin.DangerText.Brush(),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        var body = new StackPanel { Spacing = 1 };
        body.Children.Add(header);

        // 이름 — 「ID로 보기」면 EpisodeId, 끄면 제목(비면 EpisodeId) · 2026-11-18 소유자. 칩과 같은 한글 먼저 글꼴이다.
        // 글자는 14 · 굵게 · 진한 글색이다 — 카드에서 가장 먼저 읽는 줄이다(2026-11-19 소유자 — 「에피소드 제목이랑 이런 스탯 표기가 엄청 중요한
        // 정보인데도 가독성이 떨어지는」 · UX3 · ⛔ 그 전에는 12 · SemiBold(반굵게가 없는 한글 글꼴에서는 보통 굵기로 그려졌다) · 테마 글색).
        body.Children.Add(new TextBlock
        {
            Text = showIds ? episode.EpisodeId : episode.ShownTitle,
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            Foreground = CardNameBrush,
            FontFamily = CardHeadFont,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Tag = CardNameTag
        });

        // 여기 도착했을 때의 스탯 (2026-08-17 소유자: "간선을 따라 왔을 때 스탯의 변화량이
        // 노드에 표시되도록. 여러 루트가 있을 때는 스탯의 최소최대량을 표기"). 도달성 증명이
        // 이미 (에피소드, 스탯 벡터)로 걸으므로 그 결과를 읽기만 한다 — 따로 세지 않는다.
        //
        // 스탯 하나에 한 줄 — 왼쪽에 키, 오른쪽 끝에 값(굵게). 키가 길면 키만 줄고 값은 늘 보인다. 카드는 줄 수만큼 아래로 자란다
        // (2026-11-19 소유자 — 「스탯 당 1줄씩만 차지하게 하며, 아래로 펼쳐지게」 · UX3 · ⛔ 그 전에는 「키 값 · 키 값」 한 줄(글자 9 · 흐리게)이
        // 둘만 넘어도 「…」로 잘렸다).
        if (StatRows(episode) is { Rows.Count: > 0 } stats)
        {
            var lines = new StackPanel { Margin = new Thickness(0, 3, 0, 0), Tag = StatLineTag };   // 검증이 이 묶음을 글자 짐작 없이 집는다

            foreach ((string label, string value) in stats.Rows)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = StatRowHeight };
                row.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    FontFamily = CardHeadFont,
                    Foreground = StatKeyBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var shown = new TextBlock
                {
                    Text = stats.Partial ? "≈ " + value : value,
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    FontFamily = CardHeadFont,
                    Foreground = StatValueBrush,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(shown, 1);
                row.Children.Add(shown);
                lines.Children.Add(row);
            }

            body.Children.Add(lines);
        }

        // 선택지 포트 (2026-08-15 소유자) — 연출 그래프의 조건 갈래 포트처럼 카드
        // 오른변에 뚫린다. 카드는 선택지 수만큼 아래로 자라고(많아야 3개), 문구·원은
        // 간선 그리기와 같은 PortY 산식으로 캔버스에 앉는다 — 줄과 선이 어긋날 수 없다.
        List<ChapterEdge> options = _optionsByEpisode.GetValueOrDefault(episode.EpisodeId) ?? [];

        var card = new Border
        {
            Width = CardWidth,
            // ⚠ 카드는 선택지 칸 수로는 <b>안 자란다</b> (v5) — 포트가 아래변 위에 걸터앉으므로 칸 수만큼
            //   키를 늘릴 이유가 없다. v4까지는 선택지 줄마다 18px씩 자랐다. 스탯 줄 수로는 자란다(UX3).
            Height = CardHeightOf(episode.EpisodeId),
            Padding = new Thickness(9, 7),
            CornerRadius = new CornerRadius(Skin.StructureCardRadius),
            BoxShadow = Skin.CardShadow,
            BorderThickness = new Thickness(hasError ? 2 : 1),
            BorderBrush = hasError
                ? Skin.DangerText.Brush()
                : Skin.StructureSlot.Brush(),
            Background = SceneStripeSurface(sceneMark),
            Child = body,
            // 노드 카드임을 EpisodeId로 표시한다. 간선 라벨도 Border라서, 표식이 없으면
            // 검증이 카드와 라벨을 구별하지 못한다.
            Tag = episode.EpisodeId
        };

        ToolTip.SetTip(card, Tooltip(model, episode) + StatSpanDetail(episode));

        // 아직 이어지지 않았다 — 오류가 아니라 작업 중이라 흐리게만 세운다(내보내기를 막지 않는다).
        if (IsNotConnected(episode))
        {
            card.Opacity = 0.55;
            ToolTip.SetTip(card, Ui.Structure_NotConnectedThereSNo +
                                 Tooltip(model, episode) + StatSpanDetail(episode));
        }

        if (GatedIncoming(model, episode).Count > 0)
        {
            card.BorderThickness = new Thickness(1.6);
            card.BorderBrush = Skin.StructureEdgeConditioned.Brush();
        }


        // 클릭 = 선택(속성 패널) · 끌기 = 자리 옮기기(놓을 때 MoveEpisodes 한 번, v4).
        // 더블클릭은 아무것도 안 한다(에피소드 엑셀을 열던 배선은 2026-10-29 D2에 걷혔다).
        WireCardInteraction(card, episode);
        card.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);

        // 선택 강조는 제자리에서 입힌다(ApplySelectionVisuals) — 여기서는 기본 모습만 등록한다.
        _cardById[episode.EpisodeId] = card;
        _cardBase[episode.EpisodeId] = (card.BorderBrush, card.BorderThickness);

        (double x, double y) = _placed[episode.EpisodeId];
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        GraphCanvas.Children.Add(card);

        DrawOptionPorts(episode.EpisodeId, options, x, y,
            hasIncoming: model.Edges.Any(edge => string.Equals(edge.ToEpisodeId, episode.EpisodeId, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 카드 오른변의 선택지 포트들 — 문구(카드 안 오른쪽 정렬) + 테두리 위의 원.
    /// v9에서는 포트 하나가 곧 간선 하나다(문구가 붙은 길). 클릭하면 그 간선이 선택된다.
    /// </summary>
    private void DrawOptionPorts(string episodeId, IReadOnlyList<ChapterEdge> options, double x, double y, bool hasIncoming)
    {
        // 들어오는 길이 모이는 점 하나 — 아래로 나가서 위로 들어온다. 「빈 선택지 숨기기」를 켜면 들어오는 길이 없을 때 감춘다.
        if (hasIncoming || _session?.HideEmptyChoices != true)
        {
            GraphCanvas.Children.Add(Dot(
                EntryDot(x, y),
                Skin.StructureSlot.Brush(),
                filled: false,
                tip: Ui.Structure_PathsIntoThisEpisodeGather));
        }

        int slots = ShownSlotsFor(options.Count);

        for (int index = 0; index < slots; index++)
        {
            ChapterEdge? wired = index < options.Count ? options[index] : null;

            // v12 — 문구가 비면 그것은 길의 종류가 아니라 **고칠 것**이다. 판에서 그 자리를
            // 짚어 주지 않으면 기획자는 빈 칸을 못 찾는다. 이제 그 말을 <b>점의 색</b>이 한다:
            // 파랑=자동 · 빨강=문구 없음 · 주황=이어진 선택지 · 회색 빈 점=아직 안 쓴 칸.
            IBrush colour = (wired is null ? Skin.StructureSlot
                : wired.Auto ? Skin.Accent
                : wired.HasNoOptionLabel ? Skin.DangerDeep
                : Skin.ChoiceInk).Brush();

            Ellipse port = Dot(
                new Point(PortX(x, index, slots), y + CardHeightOf(episodeId)),
                colour,
                filled: wired is not null,
                tip: wired is null
                    ? Ui.Structure_EmptyChoiceSlotClickTo
                    : PortTip(wired));

            if (wired is { } edge)
            {
                port.PointerPressed += (_, e) =>
                {
                    e.Handled = true;
                    UiGuard.Run(_session, Ui.Structure_ChoicePort, () =>
                        SelectEdgeKey(edge.FromEpisodeId, edge.ToEpisodeId, EdgeLabelKey(edge)));
                };
            }
            else
            {
                // 빈 칸도 누르면 정보가 선다 — 아직 길이 없다는 것과 길을 더하는 곳.
                int slotIndex = index;
                port.Tag = EmptySlotTag + episodeId + "#" + slotIndex;
                _emptySlotDots[(episodeId, slotIndex)] = port;
                port.PointerPressed += (_, e) =>
                {
                    e.Handled = true;
                    UiGuard.Run(_session, Ui.Structure_ChoiceSlot2, () => SelectEmptySlot(episodeId, slotIndex));
                };
            }

            GraphCanvas.Children.Add(port);
        }
    }

    /// <summary>
    /// 포트 점 하나 — 카드 변 위에 <b>걸터앉는다</b>(가운데가 변에 오게 반지름만큼 민다).
    /// 선택지·입구·분기가 같은 모양 하나를 쓴다.
    /// </summary>
    private static Ellipse Dot(Point at, IBrush colour, bool filled, string tip)
    {
        var dot = new Ellipse
        {
            Width = PortRadius * 2,
            Height = PortRadius * 2,
            Stroke = colour,
            StrokeThickness = 1.6,
            Fill = filled ? colour : Skin.Surface.Brush(),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        ToolTip.SetTip(dot, tip);
        Canvas.SetLeft(dot, at.X - PortRadius);
        Canvas.SetTop(dot, at.Y - PortRadius);

        return dot;
    }

    /// <summary>
    /// 점 위에 뜨는 말 — 문구가 카드 안에서 사라진 자리를 메운다 (v5).
    ///
    /// ⚠ 점 셋이 가로로 서면 글자를 놓을 폭이 카드 너비의 1/4뿐이라 한글 몇 자에서 잘린다.
    /// 그래서 문구는 <b>간선 가운데의 라벨</b>이 지고, 점은 색과 이 말로 제 사정을 말한다.
    /// </summary>
    private static string PortTip(ChapterEdge edge)
    {
        string option = edge.Auto ? Ui.Structure_Auto
            : edge.HasNoOptionLabel ? Ui.Structure_NoTextFixThisUnless
            : edge.OptionLabel!;

        string gate = edge.ConditionLabel is { } unlock ? string.Format(Ui.Structure_UnlockCondition, unlock) : string.Empty;
        string visible = edge.VisibleConditionLabel is { } show ? string.Format(Ui.Structure_ShowCondition, show) : string.Empty;

        return string.Format(Ui.Structure_ClickToEditThisPath, option, edge.ToEpisodeId, visible, gate);
    }

    /// <summary>
    /// 카드에 세울 스탯 줄들 — 스탯마다 (이름, 값) 한 줄: `신뢰 1~3` · `분노 0` (2026-08-17 · 한 줄씩은 2026-11-19 UX3). 값은 <b>도착 직후</b>다
    /// (그 노드로 들어오는 간선의 증감까지 커밋한 뒤). 루트가 하나면 값 하나, 갈래가 여럿이면 최소~최대로 벌어진다.
    /// <c>Partial</c> — 탐색이 상한에서 잘려 이 폭이 "적어도 이만큼"이다(확정처럼 보이면 안 된다 — 값 앞에 「≈」).
    ///
    /// 닿을 수 없는 에피소드에는 아무것도 안 쓴다 — 도착이 없으니 도착 시점 값도 없다.
    /// bool 스탯은 숫자 대신 참/거짓으로 읽는다(0·1이 무슨 뜻인지 카드에서 알 수 없다).
    /// </summary>
    private (List<(string Label, string Value)> Rows, bool Partial) StatRows(ChapterEpisode episode)
    {
        if (_validation is not { } validation || SelectedModel is not { } model)
        {
            return ([], false);
        }

        // 챕터 어디에서도 움직이지 않는 스탯은 뺀다 — 모든 카드에 `분노 0`이 붙으면
        // 정작 움직이는 스탯이 그 줄에 묻힌다.
        //
        // 다만 <b>아무 스탯도 안 움직이는 챕터</b>에서는 거르지 않고 전부 보여 준다
        // (2026-08-17 소유자: "도착 스탯이 보여야 하는데 아직 안보이네"). 증감을 아직
        // 하나도 안 적은 판이 그렇고, 그때 빈 카드는 기능이 없는 것처럼 읽힌다 —
        // 초기값이라도 서 있어야 "여기에 그 값이 뜬다"가 보인다.
        var moved = model.Edges
            .SelectMany(edge => edge.StatChanges)
            .Select(change => change.Key)
            .ToHashSet(StringComparer.Ordinal);

        List<ChapterStatSpan> spans = validation.Reachability
            .SpansFor(episode.EpisodeId)
            .Where(span => moved.Count == 0 || moved.Contains(span.Key))
            .ToList();

        string Value(ChapterStatSpan span)
        {
            bool isBool = model.Stats.FirstOrDefault(stat =>
                string.Equals(stat.Key, span.Key, StringComparison.Ordinal))?.Type == ChapterStatType.Bool;

            if (!isBool)
            {
                return span.IsFixed
                    ? span.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : $"{span.Minimum}~{span.Maximum}";
            }

            // 활성/비활성 스탯의 말은 앱 전체가 하나다(2026-11-19 소유자 · usability UX12 — ⛔ 「참」 · 「거짓」 · 「참·거짓」).
            return span.IsFixed ? (span.Minimum == 1 ? Ui.Side_On : Ui.Side_Off) : Ui.Structure_OnOff;
        }

        return (
            spans.Select(span => (span.DisplayName.Length > 0 ? span.DisplayName : span.Key, Value(span))).ToList(),
            spans.Count > 0 && !validation.Reachability.ExplorationComplete);
    }

    /// <summary>스탯 줄 수 → 카드의 높이. 하나 이하면 <see cref="CardHeight"/>, 그보다 많으면 한 줄씩 자란다.</summary>
    internal static double CardHeightFor(int statRows) => Math.Max(CardHeight, CardHeadHeight + (statRows * StatRowHeight));

    /// <summary>그 에피소드 카드의 높이 — 판을 그릴 때 센다(<see cref="DrawCanvas"/>). 모르면 가장 낮은 카드.</summary>
    private double CardHeightOf(string episodeId) => _cardHeight.GetValueOrDefault(episodeId, CardHeight);

    private readonly Dictionary<string, double> _cardHeight = new(StringComparer.Ordinal);

    private static IBrush CardNameBrush => Skin.CardName.Brush();
    private static IBrush StatKeyBrush => Skin.StatKey.Brush();
    private static IBrush StatValueBrush => Skin.StatValue.Brush();

    /// <summary>카드의 도착 스탯 줄들을 담은 묶음에 붙는 표식 — 검증이 그 줄들을 집는 손잡이다(줄마다 키 · 값 두 글).</summary>
    internal const string StatLineTag = "episode-stats";

    /// <summary>카드 머리의 이름 줄(「ID로 보기」면 EpisodeId, 끄면 제목) — 시험이 이 Tag로 집는다.</summary>
    internal const string CardNameTag = "episode-name";

    /// <summary>카드 머리 칩의 높이 — 칩 안 글의 글꼴이 무엇이든 상자가 같다(2026-11-18 소유자 · chrome-card-header U2).</summary>
    internal const double CardChipHeight = 16;

    /// <summary>
    /// 카드 머리(칩 · 이름)의 글꼴 — <b>한글 먼저</b>(맑은 고딕 → Apple SD Gothic Neo → Noto Sans KR · CJK KR → 앱 기본). 앱 글꼴(Inter)에는 한글이
    /// 없어 한글은 시스템 글꼴로, 라틴(장면ID · EpisodeId)은 Inter로 그려졌다 — 「ID로 보기」를 켜고 끌 때마다 글꼴과 줄 높이가 바뀌었다
    /// (2026-11-18 소유자 — 「글자체자체도 다른 것 같은데」). 한글 글꼴은 라틴 글자도 그리므로 두 모드가 같은 글꼴이다. 앱 전체는 그대로다.
    /// 테마가 본문 글꼴을 정했으면(「파스텔 하늘」 — 한글과 라틴을 함께 짠 Pretendard) 그것이다 — 틀의 글과 [구성] 카드 머리가 한 글꼴이다(2026-11-26 · 디자인 점검 1번).
    /// </summary>
    internal static FontFamily CardHeadFont =>
        Application.Current?.TryGetResource("UiFont", Avalonia.Styling.ThemeVariant.Light, out object? themed) == true && themed is FontFamily font
            ? font
            : SystemCardHeadFont;

    private static readonly FontFamily SystemCardHeadFont =
        new("Malgun Gothic, Apple SD Gothic Neo, Noto Sans KR, Noto Sans CJK KR, $Default");

    /// <summary>카드 머리 칩 하나 — 높이를 박고(<see cref="CardChipHeight"/>) 글은 가운데에 선다.</summary>
    /// <summary>[구성] 카드의 장면 꼬리표 — 검증이 글자 짐작 없이 집는다.</summary>
    internal const string SceneChipTag = "scene-chip";

    /// <summary>카드 왼쪽 가장자리의 장면 색 띠 폭(판 단위 · 테두리 밑까지) — 테두리 안에 3이 보인다. 글(안쪽 여백 9)과는 떨어져 선다.</summary>
    internal const double SceneStripeWidth = 4;

    /// <summary>
    /// [구성] 카드의 바탕 — 왼쪽 가장자리에 그 장면 색(점 색)의 얇은 띠, 나머지는 카드 면(<see cref="Skin.StructureCardSurface"/>). 띠를 바탕의 끊긴
    /// 그라데이션으로 그려 카드의 둥근 모서리를 그대로 따르고, 고름 · 오류 · 관문이 바꾸는 테두리와 다투지 않는다(테두리를 바꿔도 띠는 남는다).
    /// 띠는 얇아 옅은 면 색(SceneTint)으로는 보이지 않는다 — 점처럼 진한 색이다.
    /// </summary>
    private static IBrush SceneStripeSurface(Color mark) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
        EndPoint = new RelativePoint(SceneStripeWidth, 0, RelativeUnit.Absolute),
        GradientStops =
        {
            new GradientStop(mark, 0),
            new GradientStop(mark, 1),
            new GradientStop(Skin.StructureCardSurface, 1)   // 끝에서 끊기고, 그 뒤는 끝 색(카드 면)이 이어진다
        }
    };

    private static Border CardChip(string text, Color color, FontWeight weight) => new()
    {
        Background = color.Brush(),
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(4, 0),
        Height = CardChipHeight,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 9,
            FontWeight = weight,
            FontFamily = CardHeadFont,
            Foreground = Skin.OnAccent.Brush(),
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    /// <summary>카드 툴팁의 스탯 절 — 카드의 줄과 같은 것에 범위가 무엇인지를 덧붙인다.</summary>
    private string StatSpanDetail(ChapterEpisode episode)
    {
        (List<(string Label, string Value)> rows, bool partial) = StatRows(episode);

        if (rows.Count == 0)
        {
            return string.Empty;
        }

        return Ui.Structure_StatsOnArrival
            + string.Join("\n  ", rows.Select(row => $"{row.Label} {row.Value}"))
            + Ui.Structure_RangesWidenWhenSeveralRoutes
            + (partial ? Ui.Structure_SearchStoppedAtItsLimit : string.Empty);
    }

    /// <summary>
    /// 도달성 증명이 "닿을 수 없다"고 한 에피소드인가 (G7) — 간선은 있는데 조건 때문에 못 닿는 것. 아직 이어지지 않은 것은 오류가
    /// 아니라 작업 중이라 따로 흐리게 선다(<see cref="IsNotConnected"/>).
    /// </summary>
    private bool IsUnreachable(ChapterEpisode episode) =>
        _validation is { } validation &&
        !validation.Reachability.ReachableEpisodeIds.Contains(episode.EpisodeId) &&
        !IsNotConnected(episode);

    /// <summary>
    /// 시작에서 아직 이어지지 않은 에피소드인가(2026-11-17) — 오류가 아니라 작업 중이다. 카드를 흐리게 세운다(⚠를 달지 않는다).
    /// </summary>
    private bool IsNotConnected(ChapterEpisode episode) =>
        _validation is { } validation &&
        validation.Reachability.Diagnostics.Any(item =>
            item.Code == ChapterDiagnosticCode.EpisodeNotConnected &&
            string.Equals(item.EpisodeId, episode.EpisodeId, StringComparison.Ordinal));

    // ── 편집 (v4: 자리는 사람이 소유한다 — 드래그가 에피소드의 X·Y를 고친다) ──────

    private string? _selectedEpisodeId;

    /// <summary>이번 그리기의 캔버스 배치 = 에피소드의 X·Y + 여백. <see cref="DrawCanvas"/>가 채운다.</summary>
    private IReadOnlyDictionary<string, (double X, double Y)> _placed =
        new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

    /// <summary>분기 점선의 굵기.</summary>
    private const double BranchThickness = 1.6;

    private const double BranchKnobRadius = 5;

    /// <summary>한 변에 분기점이 여럿일 때 점 사이의 세로 간격.</summary>
    private const double BranchPointGap = 14;

    /// <summary>분기점이 지는 표 — 화면 테스트가 이것으로 찾는다. 뒤에 분기점의 열쇠(<see cref="ChapterBranchMarkers.BranchPoint.Key"/>)가 붙는다.</summary>
    internal const string BranchKnobTag = "branch-knob:";

    /// <summary>이번 그리기에서 점선을 그은 분기들 — 칩 인스펙터의 「부르는 곳」이 이것을 본다.</summary>
    private readonly List<ChapterBranchMarkers.Marker> _branchMarkers = [];

    /// <summary>분기 카드 칩의 크기 · 자리 (2026-11-17) — 부르는 에피소드 카드의 오른변에서 떨어져 세로로 늘어선다.</summary>
    private const double BranchChipWidth = 150;
    private const double BranchChipHeight = 24;
    private const double BranchChipOffset = 34;
    private const double BranchChipGap = 6;

    /// <summary>조건이 걸린 칩의 높이 — 위에 조건, 아래에 「⑂ 이름」 두 줄이다(2026-11-23 · <see cref="DrawBranchChip"/>).</summary>
    private const double BranchChipTallHeight = 38;


    /// <summary>
    /// 분기 카드 칩이 지는 표 — 화면 테스트가 이것으로 찾는다. 글자 표가 아닌 것은 「글자 표를 진 Border = 에피소드 카드」를 깨지 않기
    /// 위해서다(<see cref="EdgeLabelMark"/>와 같은 까닭).
    /// </summary>
    internal sealed record BranchCardMark(string NodeId, string Name);

    /// <summary>이번 그리기의 분기 카드 칩 — 열쇠는 NodeId.</summary>
    private readonly Dictionary<string, Border> _branchChips = new(StringComparer.Ordinal);

    /// <summary>칩의 기본 테두리 — 선택이 풀리면 이리 돌아온다.</summary>
    private readonly Dictionary<string, (IBrush? Brush, Thickness Thickness)> _chipBase = new(StringComparer.Ordinal);

    /// <summary>이번 그리기의 칩 자리(여백 포함, 왼쪽 위). <see cref="PlaceBranchChips"/>가 채운다.</summary>
    private Dictionary<string, (double X, double Y)> _chipPlaced = new(StringComparer.Ordinal);

    /// <summary>이번 그리기의 칩 높이 — 조건이 걸린 칩은 두 줄이라 높다. <see cref="PlaceBranchChips"/>가 채운다.</summary>
    private readonly Dictionary<string, double> _chipHeight = new(StringComparer.Ordinal);

    private double ChipHeightOf(string nodeId) => _chipHeight.GetValueOrDefault(nodeId, BranchChipHeight);

    /// <summary>고른 분기 카드(칩)의 NodeId.</summary>
    private string? _selectedBranchCardId;


    /// <summary>지금 끌고 있는 카드. 없으면 <c>null</c>.</summary>
    private string? _draggingEpisodeId;

    /// <summary>범위 선택 사각형 — 빈 판을 왼쪽 단추로 끄는 동안만 선다(<see cref="RubberBand"/>).</summary>
    private Rectangle? _rubberBand;
    private Point _rubberStart;

    /// <summary>범위 선택으로 묶은 카드 — 그 가운데 하나를 끌면 함께 움직인다. 무리 밖을 고르면 풀린다.</summary>
    private readonly HashSet<string> _grouped = new(StringComparer.Ordinal);

    /// <summary>무리째 끄는 중이면 무리의 처음 자리(여백 뺀 값 = 에피소드에 적힐 값). 아니면 <c>null</c>.</summary>
    private Dictionary<string, (double X, double Y)>? _groupStart;

    /// <summary>무리를 잡은 자리와 지금까지 옮긴 양 — 놓을 때 한 번 적는다.</summary>
    private Point _groupGrab;
    private Vector _groupDelta;

    /// <summary>잡은 지점과 카드 왼위 모서리의 차 — 카드가 커서 아래에서 튀지 않게 한다.</summary>
    private Point _dragOffset;

    /// <summary>
    /// 끄는 동안의 임시 자리(여백 뺀 값 = 에피소드에 적힐 값). <b>아직 프로젝트에 없다.</b>
    ///
    /// ⭐ 놓을 때 <b>한 번</b> 적는 것이 요점이다. <c>MoveEpisode</c>는 부를 때마다 되돌리기
    /// 스냅샷을 하나 쌓으므로, 포인터가 움직일 때마다 부르면 드래그 한 번에 되돌리기가
    /// 수십 개 쌓여 사람이 진짜 무르고 싶은 편집을 밀어낸다.
    /// </summary>
    private (double X, double Y)? _dragPosition;

    /// <summary>실제로 움직였나 — 그냥 누른 것과 끈 것을 가른다(안 움직였으면 안 적는다).</summary>
    private bool _dragMoved;

    // ── 끄는 동안 제자리에서 옮기기 (2026-11-18 · board-speed PF3) ───────────────────────────────
    //
    // [구성] 판은 카드 · 점 · 선택지 선(선 · 화살촉 · 누르는 자리 · 문구) · 분기 표시 · 분기 칩을 그리는 단위(조각)마다 그 조각이 기대는 에피소드와
    // 그었을 때의 자리를 적어 둔다. 끄는 동안에는 기대는 에피소드가 모두 같은 만큼 움직인 조각은 그림째 옮기고(<see cref="TranslateTransform"/>),
    // 무리 안팎을 잇는 선택지 선만 지금 자리로 다시 긋는다. 무리 안팎을 잇는 분기 점선은 끄는 동안 감춘다(드물다). 놓으면 지금처럼 한 번 적고
    // 통째로 다시 그린다(문구 자리 피하기 · 판 크기 · 검증은 그때). ⛔ 그 전에는 움직일 때마다 판을 통째로 다시 지었다(에피소드 60개 챕터에서
    // 0.13초, 300개에서 0.6초).

    /// <summary>판의 그림 조각 — 그 그림, 기대는 에피소드의 그었을 때 자리, 그때 더한 문구 자리, 다시 긋는 법(선택지 선만).</summary>
    private sealed record CanvasPart(
        List<Control> Visuals,
        Dictionary<string, (double X, double Y)> DrawnAt,
        List<Rect> Labels,
        IReadOnlyList<string> Deps,
        Action? Redraw);

    private readonly List<CanvasPart> _parts = new();

    /// <summary>분기 카드 칩 → 그 칩이 붙는 집 에피소드.</summary>
    private readonly Dictionary<string, string> _chipHome = new(StringComparer.Ordinal);

    /// <summary>이번 그리기의 저장된 자리(여백 전) — 끄는 동안 지금 자리를 다시 셈하는 바탕.</summary>
    private Dictionary<string, (double X, double Y)> _storedPlacement = new(StringComparer.Ordinal);

    /// <summary>판을 통째로 지은 횟수 — 시험이 「끄는 동안 다시 짓지 않았다」를 보는 창이다.</summary>
    internal int CanvasBuildCount { get; private set; }

    /// <summary>끄는 동안 다시 그은 선택지 선의 수.</summary>
    internal int PartRedrawCount { get; private set; }

    /// <summary>조각 하나를 그리고 적어 둔다 — 그리는 일은 캔버스 끝에 덧붙인다.</summary>
    private void DrawPart(IEnumerable<string?> deps, Action draw, bool redrawable)
    {
        int start = GraphCanvas.Children.Count;
        int labels = _labelRects.Count;
        draw();

        List<string> on = deps.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        _parts.Add(new CanvasPart(
            GraphCanvas.Children.Skip(start).ToList(),
            on.Where(_placed.ContainsKey).ToDictionary(dep => dep, dep => _placed[dep], StringComparer.Ordinal),
            _labelRects.Skip(labels).ToList(),
            on,
            redrawable ? draw : null));
    }

    /// <summary>그 에피소드의 선택지 점 자리 — 카드 아래 칸.</summary>
    private Point PortOf(string episodeId, int index, int slots)
    {
        (double X, double Y) position = _placed[episodeId];
        return new Point(PortX(position.X, index, slots), position.Y + CardHeightOf(episodeId) + 5);
    }

    /// <summary>끄는 동안의 한 걸음 — 판을 다시 짓지 않고 조각을 옮기거나(같이 움직였다) 다시 긋거나(한쪽만) 감춘다.</summary>
    private void MoveInPlace()
    {
        _placed = _storedPlacement.ToDictionary(pair => pair.Key, pair => DrawnAt(pair.Key, pair.Value), StringComparer.Ordinal);
        var stale = new List<CanvasPart>();

        foreach (CanvasPart part in _parts)
        {
            Vector? shift = null;
            bool rigid = true;

            foreach ((string dep, (double X, double Y) drawn) in part.DrawnAt)
            {
                (double X, double Y) now = _placed[dep];
                var moved = new Vector(now.X - drawn.X, now.Y - drawn.Y);

                if (shift is { } first && (Math.Abs(first.X - moved.X) > 0.01 || Math.Abs(first.Y - moved.Y) > 0.01))
                {
                    rigid = false;
                    break;
                }

                shift ??= moved;
            }

            if (rigid)
            {
                Vector by = shift ?? default;
                bool still = Math.Abs(by.X) < 0.01 && Math.Abs(by.Y) < 0.01;

                foreach (Control visual in part.Visuals)
                {
                    visual.RenderTransform = still ? null : new TranslateTransform(by.X, by.Y);
                    visual.IsVisible = true;
                }

                continue;
            }

            if (part.Redraw is null)
            {
                foreach (Control visual in part.Visuals)
                {
                    visual.IsVisible = false;   // 무리 안팎을 잇는 분기 점선 — 놓으면 다시 선다
                }

                continue;
            }

            stale.Add(part);
        }

        foreach (CanvasPart part in stale)
        {
            int at = part.Visuals.Count > 0 ? GraphCanvas.Children.IndexOf(part.Visuals[0]) : -1;

            foreach (Control visual in part.Visuals)
            {
                GraphCanvas.Children.Remove(visual);
            }

            foreach (Rect label in part.Labels)
            {
                _labelRects.Remove(label);
            }

            _parts.Remove(part);
            DrawPart(part.Deps, part.Redraw!, redrawable: true);
            PartRedrawCount++;

            // 선은 카드 아래에 깔려야 한다 — 덧붙인 그림을 옛 자리로 옮긴다.
            if (at >= 0)
            {
                CanvasPart redrawn = _parts[^1];

                foreach (Control visual in redrawn.Visuals)
                {
                    GraphCanvas.Children.Remove(visual);
                    GraphCanvas.Children.Insert(Math.Min(at++, GraphCanvas.Children.Count), visual);
                }
            }
        }

        if (stale.Count > 0)
        {
            ApplySelectionVisuals();
        }
    }

    /// <summary>
    /// 다시 그리기 예약 — <see cref="QueueReload"/>와 같은 수법이다. 포인터는 프레임보다
    /// 빨리 오므로, 들어오는 대로 판을 다시 그리면 대부분 버려질 그림을 그린다.
    /// </summary>
    private bool _dragRedrawQueued;

    private ChapterGraphModel? SelectedModel =>
        _entries.FirstOrDefault(item => item.ChapterId == _selectedChapterId)?.Model;

    /// <summary>
    /// 스탯·조건 표가 보는 <b>살아 있는</b> 챕터 — 없으면 <c>null</c>이고 부르는 쪽이
    /// 디스크 스냅샷(<see cref="SelectedModel"/>)으로 물러난다.
    ///
    /// ⛔ <b>표를 편집 가능하게 만든 순간 스냅샷으로는 못 그린다</b> (2026-09-17).
    /// <see cref="SelectedModel"/>은 마지막으로 읽은 <c>_entries</c>(스냅샷)에서 오므로, 방금 고친 값이
    /// 저장·재읽기 전까지 <b>안 비친다</b> — 사람은 적었는데 안 들어갔다고 읽는다.
    /// R7에서 철도가 같은 자리를 겪었고 답도 같다: <b>살아 있는 것을 먼저 보고 스냅샷은
    /// 뒷받침</b>. 둘 다 같은 값을 말하므로 정본이 둘이 되는 것은 아니다.
    ///
    /// ⚠ 이 탭의 <b>두 표에만</b> 쓴다. 속성 패널·검증 보고는 디스크 동기화가 이끄는
    /// 제 흐름이 있어 여기서 함께 바꾸지 않는다.
    /// </summary>
    private ChapterGraphModel? LiveSheetModel =>
        _selectedChapterId is { } id && _session?.Editor.FindChapter(id) is { } chapter
            ? chapter.ToGraphModel(id)
            : null;

    private void WireCardInteraction(Border card, ChapterEpisode episode)
    {
        card.PointerPressed += (_, e) =>
        {
            // 오른쪽·가운데 단추로는 선택하지 않는다.
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true; // 캔버스(빈 공간 = 선택 해제)로 흘러가면 방금 한 선택이 풀린다
            SelectEpisode(episode.EpisodeId);

            // 두 번 누름 — 끌기를 시작하지 않고 셸에 알린다(첫 누름이 시작한 끌기는 놓을 때 끝났다).
            if (e.ClickCount >= 2)
            {
                if (_selectedChapterId is { } chapterId)
                {
                    EpisodeOpened?.Invoke(chapterId, episode.EpisodeId);
                }

                return;
            }

            // 끌 채비 (v4). 아직 아무것도 안 적는다 — 움직여야 드래그다.
            Point position = e.GetPosition(GraphCanvas);

            _draggingEpisodeId = episode.EpisodeId;
            _dragOffset = new Point(
                position.X - Canvas.GetLeft(card), position.Y - Canvas.GetTop(card));
            _dragPosition = null;
            _dragMoved = false;

            // 범위 선택한 무리의 카드를 잡으면 무리가 함께 움직인다.
            _groupStart = _grouped.Count > 1 && _grouped.Contains(episode.EpisodeId)
                ? _grouped.Where(_placed.ContainsKey).ToDictionary(
                    id => id,
                    id => (X: _placed[id].X - CanvasMargin, Y: _placed[id].Y - CanvasMargin),
                    StringComparer.Ordinal)
                : null;
            _groupGrab = position;
            _groupDelta = default;
        };

        // 카드를 두 번 누르면 [대본] 탭의 그 카드로 간다(EpisodeOpened — 셸이 데려간다). ⛔ 대본 엑셀을 열던 옛 배선은
        // 2026-10-29(D2)에 엑셀 연동과 함께 걷혔다.
    }

    /// <summary>
    /// 고른 카드가 화면 밖이면 그 카드가 가운데 오게 스크롤한다 — [대본] 탭에서 두 번 눌러 넘어왔을 때 그 카드가 보이게.
    /// </summary>
    internal void RevealEpisode(string episodeId)
    {
        if (!_cardById.TryGetValue(episodeId, out Border? card))
        {
            return;
        }

        var cardRect = new Rect(
            Canvas.GetLeft(card), Canvas.GetTop(card),
            card.Bounds.Width > 0 ? card.Bounds.Width : CardWidth,
            card.Bounds.Height > 0 ? card.Bounds.Height : CardHeightOf(episodeId));
        var viewRect = new Rect(
            (GraphScroll.Offset.X - _sheetOffset.X) / _zoom, (GraphScroll.Offset.Y - _sheetOffset.Y) / _zoom,
            GraphScroll.Viewport.Width / _zoom, GraphScroll.Viewport.Height / _zoom);

        if (viewRect.Contains(cardRect))
        {
            return;
        }

        GraphScroll.Offset = new Vector(
            Math.Max(0, (cardRect.Center.X * _zoom) + _sheetOffset.X - (GraphScroll.Viewport.Width / 2)),
            Math.Max(0, (cardRect.Center.Y * _zoom) + _sheetOffset.Y - (GraphScroll.Viewport.Height / 2)));
    }

    /// <summary>
    /// 끄는 중 — 카드의 임시 자리를 옮기고 판을 다시 그린다 (v4, 2026-09-18).
    ///
    /// ⚠ <b>프로젝트는 아직 안 고친다.</b> 놓을 때 한 번 적는다
    /// (<see cref="OnCanvasPointerReleased"/>) — 그래야 되돌리기 한 번이 드래그 한 번이다.
    /// </summary>
    private void OnCanvasPointerMoved(object? sender, Avalonia.Input.PointerEventArgs args)
    {
        if (_rubberBand is not null)
        {
            RubberBand.Stretch(_rubberBand, _rubberStart, args.GetPosition(GraphCanvas));
            return;
        }

        if (_draggingEpisodeId is null)
        {
            return;
        }

        if (!args.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
        {
            // 판 밖에서 놓았다 — 놓음을 못 봤으므로 여기서 마무리한다.
            CommitDrag();
            return;
        }

        Point position = args.GetPosition(GraphCanvas);

        // 판의 왼쪽 · 위로도 끈다 — 놓으면 판이 그쪽으로 넓어진다(판은 가장 왼쪽 · 위 카드를 기준으로 감싼다 · DisplayPlacement). 끄는 동안 판 밖의
        // 카드는 바깥 위에 선다(usability UX25 · ⛔ 맨 왼쪽 · 맨 위 카드는 판 가장자리에서 막혀 판을 그쪽으로 넓힐 수 없었다).
        // 무리는 모양을 지킨 채 함께 민다.
        if (_groupStart is { } group)
        {
            _groupDelta = new Vector(position.X - _groupGrab.X, position.Y - _groupGrab.Y);
            _dragMoved = true;
            QueueDragRedraw();
            return;
        }

        _dragPosition = (
            X: position.X - _dragOffset.X - CanvasMargin,
            Y: position.Y - _dragOffset.Y - CanvasMargin);
        _dragMoved = true;

        QueueDragRedraw();
    }

    private void OnCanvasPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs args)
    {
        if (_rubberBand is not null)
        {
            Point end = args.GetPosition(GraphCanvas);
            Rect rect = RubberBand.Between(_rubberStart, end);
            GraphCanvas.Children.Remove(_rubberBand);
            _rubberBand = null;
            args.Pointer.Capture(null);
            UiGuard.Run(_session, Ui.Structure_SelectArea, () => FinishRubberBand(rect, end));
            return;
        }

        CommitDrag();
    }

    /// <summary>판이 선 창 — 창을 떠나는 순간을 듣는다(<see cref="EndGestures"/>).</summary>
    private WindowBase? _window;

    private void OnWindowDeactivated(object? sender, EventArgs e) => UiGuard.Run(_session, Ui.Board_CleanUpDrag, EndGestures);

    /// <summary>
    /// <b>하던 끌기를 정리한다</b> — 창을 떠나면 놓음이 이 창으로 오지 않아 끌기가 「끄는 중」으로 남는다(2026-11-19 소유자 — 범위 사각형이 판에 남아
    /// 쌓였다 · usability UX20). 범위 사각형은 거두고(아무것도 고르지 않는다), 카드 끌기는 보이던 자리에서 놓은 것으로 마무리하고(놓음을 못 본
    /// 끌기를 다음 움직임에서 마무리하는 규칙과 같다), 가운데 단추로 판 옮기기는 멈춘다.
    /// </summary>
    internal void EndGestures()
    {
        if (_rubberBand is not null)
        {
            GraphCanvas.Children.Remove(_rubberBand);
            _rubberBand = null;
        }

        _panning = false;
        CommitDrag();
    }

    /// <summary>빈 판을 눌렀다 — 끄는 동안 범위 사각형이 선다.</summary>
    private void StartRubberBand(Avalonia.Input.PointerPressedEventArgs args)
    {
        // 앞의 사각형이 남아 있으면(놓음을 못 봤다) 걷고 새로 연다 — 남은 채 새것을 열면 옛것이 판에 쌓인다.
        if (_rubberBand is not null)
        {
            GraphCanvas.Children.Remove(_rubberBand);
        }

        _rubberStart = args.GetPosition(GraphCanvas);
        _rubberBand = RubberBand.Create(_rubberStart);
        GraphCanvas.Children.Add(_rubberBand);
        args.Pointer.Capture(GraphCanvas);
    }

    /// <summary>
    /// 범위 선택을 놓았다 — [대본] 탭과 같은 규칙이다. 카드가 걸렸으면 <b>카드만</b> 무리가 되고(함께 끌린다) 끌기 시작한
    /// 자리에서 가장 가까운 카드를 고른다. 카드 없이 선(선택지 선 · 분기 점선)만 걸렸으면 <b>놓은 자리(포인터)에서 가장
    /// 가까운 선 하나</b>를 고른다. 인스펙터는 고른 것을 보인다.
    /// </summary>
    private void FinishRubberBand(Rect rect, Point end)
    {
        var cards = new List<(double Distance, string EpisodeId)>();

        foreach ((string episodeId, Border card) in _cardById)
        {
            var bounds = new Rect(Canvas.GetLeft(card), Canvas.GetTop(card), CardWidth, CardHeightOf(episodeId));

            if (rect.Intersects(bounds))
            {
                cards.Add((RubberBand.DistanceTo(rect.Intersect(bounds), _rubberStart), episodeId));
            }
        }

        if (cards.Count > 0)
        {
            _grouped.Clear();
            _grouped.UnionWith(cards.Select(item => item.EpisodeId));
            SelectEpisode(cards.MinBy(item => item.Distance).EpisodeId);
            return;
        }

        var lines = new List<(double Distance, Action Pick)>();

        foreach (((string from, string to, string label), List<Line> segments) in _lineByEdge)
        {
            if (segments.Select(segment => RubberBand.CaughtDistance(
                    new LineGeometry(segment.StartPoint, segment.EndPoint), rect, end)).Min() is { } distance)
            {
                lines.Add((distance, () => SelectEdgeKey(from, to, label)));
            }
        }

        if (lines.Count > 0)
        {
            lines.MinBy(item => item.Distance).Pick();
        }
    }

    /// <summary>
    /// 끌던 자리를 <b>한 번</b> 적는다. 안 움직였으면(그냥 누른 것) 아무것도 안 적는다 —
    /// 카드를 고르기만 해도 되돌리기가 쌓이면 Ctrl+Z가 아무 일도 안 하는 것처럼 보인다.
    ///
    /// ⭐ <b>판 전체가 보이던 자리로 함께 적힌다</b>(<see cref="DisplayPlacement"/> — 왼위를 (0,0)으로 민 자리). 끈 카드 하나만 그
    /// 자리로 적으면 나머지와 기준이 갈린다. 한 번의 변경이므로 되돌리기도 한 번이다.
    /// </summary>
    private void CommitDrag()
    {
        if (_draggingEpisodeId is not { } episodeId)
        {
            return;
        }

        (double X, double Y)? position = _dragPosition;
        bool moved = _dragMoved;
        Dictionary<string, (double X, double Y)>? group = _groupStart;
        Vector delta = _groupDelta;

        _draggingEpisodeId = null;
        _dragPosition = null;
        _dragMoved = false;
        _groupStart = null;
        _groupDelta = default;

        if (!moved || _selectedChapterId is not { } chapterId || SelectedModel is not { } model)
        {
            return;
        }

        Dictionary<string, (double X, double Y)> places =
            DisplayPlacement(model).ToDictionary(pair => pair.Key, pair => pair.Value,
                StringComparer.Ordinal);

        if (group is not null)
        {
            // 무리째 — 모두 같은 만큼, 한 번의 변경으로(되돌리기 한 칸).
            foreach ((string member, (double X, double Y) start) in group)
            {
                places[member] = (start.X + delta.X, start.Y + delta.Y);
            }
        }
        else if (position is { } dropped)
        {
            places[episodeId] = dropped;
        }
        else
        {
            return;
        }

        // 판은 가장 왼쪽 · 위 카드를 기준으로 감싼다(DisplayPlacement) — 그 카드를 오른쪽 · 아래로 옮겼으면 다시 그릴 때 모든 카드가 그만큼 당겨진다.
        // 판을 놓을 때 그만큼 되돌려 카드가 화면에서 놓은 자리에 남게 한다(usability UX22).
        Vector nudge = new(places.Values.Min(place => place.X), places.Values.Min(place => place.Y));

        UiGuard.Run(_session, Ui.Structure_EpisodePosition, () =>
        {
            _session!.Editor.MoveEpisodes(chapterId, places);
            _sheetNudge = nudge;
        });
    }

    /// <summary>
    /// <b>[자동 정렬]</b> — <b>연출 그래프의 카드 자리를 본떠</b> 전부 다시 세운다 (2026-10-30 소유자: *"연출 그래프에
    /// 배치된 노드들을 참고해서 그와 거의 같은 형태로"* — <see cref="ChapterBranchPlanner.LayoutLikeBoard"/>).
    /// 연출 그래프에 카드가 하나도 없으면 흐름 순서(위에서 아래)다(v4, 2026-09-18 · 방향은 2026-10-29 D3).
    ///
    /// 자리의 주인이 사람이 되면 <b>돌아올 길</b>이 있어야 한다 — 손으로 옮기다 판이 엉키면 이것을 누른다.
    ///
    /// ⚠ 한 번의 변경이다(<c>MoveEpisodes</c>) — Ctrl+Z 한 번이면 정렬 전으로 돌아온다.
    /// </summary>
    private void AutoArrange()
    {
        if (_selectedChapterId is not { } chapterId || SelectedModel is not { } model ||
            model.Episodes.Count == 0)
        {
            return;
        }

        var board = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

        foreach (ChapterEpisode episode in model.Episodes)
        {
            if (EpisodeNaming.CardFor(_session!.Editor.Project, chapterId, episode.EpisodeId) is { } card)
            {
                board[episode.EpisodeId] = (card.Layout.X, card.Layout.Y);
            }
        }

        _session!.Editor.MoveEpisodes(chapterId, ChapterBranchPlanner.LayoutLikeBoard(model, board));

        int stray = model.Episodes.Count - board.Count;
        _session.SetStatus(board.Count == 0
            ? Plural.Format(Ui.Structure_ReLaidOutCardsOf, chapterId, model.Episodes.Count)
            : stray > 0
                ? Plural.Format(Ui.Structure_ReLaidOutCardsOf2, chapterId, model.Episodes.Count, stray)
                : Plural.Format(Ui.Structure_ReLaidOutCardsOf3, chapterId, model.Episodes.Count));
    }

    /// <summary>
    /// <b>이 챕터를 어디에 그리나</b> — 에피소드의 X·Y다(v4, 2026-09-18 — 자리의 주인은 사람이다). ⛔ 카드가 한 점에 포개진 챕터(엑셀
    /// 시절 — 전부 0,0)에 깊이 배치를 빌려 그리던 것은 2026-11-18에 걷혔다(old-shapes O9 — 툴은 늘 자리를 주어 세운다).
    ///
    /// ⭐ <b>판은 이 챕터의 에피소드만 감싼다</b> — 왼위 에피소드가 (0,0)에 오게 통째로 민다. 서로의 거리는 그대로이므로
    /// 사람이 짠 모양은 안 바뀐다. 민 자리는 판의 크기도 정한다(<see cref="DrawCanvas"/>).
    /// <list type="bullet">
    /// <item>음수 자리 — 끌어 위 · 왼쪽 바깥으로 옮긴 카드(시험 견본 ch05의 <c>branch05.02A</c>도 Y=−120이다). 그대로 그리면 카드가 캔버스 위쪽
    /// 바깥에 서고 스크롤이 닿지 않아 못 만진다.</item>
    /// <item>먼 양수 자리 — [대본] 탭에서 세운 카드의 에피소드는 그 카드 자리(그때 보던 화면 한가운데)를 받을 수 있다.
    /// 그대로 그리면 판이 원점부터 그 자리까지를 다 잡아, 텅 빈 왼위가 다른 챕터의 자리처럼 보인다.</item>
    /// </list>
    ///
    /// ⚠ 미는 양은 <b>모델</b>에서 나온다(끄는 중의 임시 자리가 아니라). 그래야 끄는 동안
    /// 기준이 안 흔들려 판 전체가 출렁이지 않는다.
    /// </summary>
    private IReadOnlyDictionary<string, (double X, double Y)> DisplayPlacement(
        ChapterGraphModel model)
    {
        double left = model.Episodes.Count == 0 ? 0 : model.Episodes.Min(episode => episode.X);
        double top = model.Episodes.Count == 0 ? 0 : model.Episodes.Min(episode => episode.Y);

        return model.Episodes.ToDictionary(
            episode => episode.EpisodeId,
            episode => (X: episode.X - left, Y: episode.Y - top),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// 판 다시 그리기 예약 — 프레임마다 한 번으로 합친다. 검증 보고·속성 패널은 안 건드린다
    /// (<see cref="DrawCanvas"/>).
    /// </summary>
    private void QueueDragRedraw()
    {
        if (_dragRedrawQueued)
        {
            return;
        }

        _dragRedrawQueued = true;

        Dispatcher.UIThread.Post(
            () =>
            {
                _dragRedrawQueued = false;

                if (_draggingEpisodeId is not null)
                {
                    UiGuard.Run(_session, Ui.Structure_Drag, MoveInPlace);
                }
            },
            DispatcherPriority.Render);
    }

    /// <summary>선택은 노드 아니면 간선 하나다 — 패널이 무엇을 편집하는지 애매하면 안 된다.</summary>
    // 간선 신원 = (출발, 도착, 라벨) — 여러 선택지가 같은 에피소드로 이어질 수 있다
    // (2026-08-15 소유자). Label은 정규화된 값(무라벨 진행 = 빈 문자열)이다.
    private (string From, string To, string Label)? _selectedEdgeKey;

    private static string EdgeLabelKey(ChapterEdge edge) => edge.OptionLabel?.Trim() ?? string.Empty;

    // 선택 강조는 다시 그리지 않고 제자리에서 바꾼다. 클릭 핸들러 안에서 캔버스를 다시 만들면
    // 방금 누른 카드가 파괴되어 더블클릭(둘째 탭이 다른 인스턴스에 떨어짐)과 드래그(캡처가
    // 죽은 카드에 걸림)가 죽는다 — 실사용에서 "클릭이 안 먹힌다"로 나타났던 결함이다.
    private readonly Dictionary<string, Border> _cardById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (IBrush? Brush, Thickness Thickness)> _cardBase =
        new(StringComparer.Ordinal);
    // 포트 철도에서 간선 하나가 선분 여럿이 된다 — 강조·복원은 선분 묶음 단위다.
    private readonly Dictionary<(string, string, string), List<Line>> _lineByEdge = new();
    private readonly Dictionary<(string, string, string), (IBrush? Stroke, double Thickness)> _lineBase = new();


    // 2026-08-16 소유자 — 선택이 탭을 끌고 다니지 않는다. 대사 탭을 보며 노드를 갈아타는
    // 흐름이 실사용의 대부분이라, 편집 탭 강제 전환("클릭이 안 먹힌다" 방지책)을 폐지했다.
    // 편집이 필요하면 사람이 편집 탭을 누른다.
    internal void SelectEpisode(string? episodeId)
    {
        FlushEdgeTyping();
        episodeId = AfterRename(episodeId);   // 적던 ID를 낸 그 에피소드를 다시 누른 것이면 새 ID로

        // 범위 선택 무리는 그 안의 카드를 고르는 동안만 남는다.
        if (episodeId is null || !_grouped.Contains(episodeId))
        {
            _grouped.Clear();
        }

        _selectedEpisodeId = episodeId;
        _selectedEdgeKey = null;
        _selectedBranchCardId = null;
        _selectedSlot = null;
        _inspector = InspectorMode.Selection;

        if (episodeId is not null)
        {
            ShowSideTab(SideTab.Inspector);   // 고른 것을 보인다 — 고름을 놓는 것(null)은 탭을 바꾸지 않는다
        }

        UnfoldChoices(); // 다른 노드로 넘어가면 선택지는 다시 모두 펼쳐진다
        ApplySelectionVisuals();
        RefreshPropertyPanel();
        ShowInHierarchy(episodeId is not null && _selectedChapterId is { } chapterId
            ? new HierarchyPick(HierarchyKind.Episode, chapterId, EpisodeId: episodeId)
            : null);

        if (!_showingOutside && episodeId is not null && _selectedChapterId is { } picked)
        {
            EpisodePicked?.Invoke(picked, episodeId);
        }
    }

    internal void SelectEdgeKey(string fromEpisodeId, string toEpisodeId, string optionLabel = "")
    {
        FlushEdgeTyping();
        _grouped.Clear();
        _selectedEdgeKey = (AfterRename(fromEpisodeId)!, AfterRename(toEpisodeId)!, optionLabel.Trim());
        _selectedEpisodeId = null;
        _selectedBranchCardId = null;
        _selectedSlot = null;
        _inspector = InspectorMode.Selection;
        ShowSideTab(SideTab.Inspector);
        UnfoldChoices();
        ApplySelectionVisuals();
        RefreshPropertyPanel();
        ShowInHierarchy(null);   // 길은 하이어아키에 없다
    }

    /// <summary>
    /// 분기 카드(칩) 하나를 고른다 — 인스펙터에 부르는 곳 · 조건(고친다) · 대본(읽기 전용)이 선다(2026-11-17 소유자).
    /// </summary>
    internal void SelectBranchCard(string? nodeId)
    {
        FlushEdgeTyping();
        _grouped.Clear();
        _selectedBranchCardId = nodeId;
        _selectedEpisodeId = null;
        _selectedEdgeKey = null;
        _selectedSlot = null;
        _inspector = InspectorMode.Selection;

        if (nodeId is not null)
        {
            ShowSideTab(SideTab.Inspector);
        }
        UnfoldChoices();
        ApplySelectionVisuals();
        RefreshPropertyPanel();
        ShowInHierarchy(BranchPick(nodeId));
    }

    /// <summary>분기 카드의 하이어아키 줄 — 집(부르는 에피소드)이 없으면 줄도 없다.</summary>
    private HierarchyPick? BranchPick(string? nodeId) =>
        nodeId is not null &&
        _entries.FirstOrDefault(entry => entry.ChapterId == _selectedChapterId) is { } entry &&
        entry.BranchCards.FirstOrDefault(home => home.Card.Id == nodeId) is { } found
            ? new HierarchyPick(HierarchyKind.Branch, entry.ChapterId, EpisodeId: found.EpisodeId, NodeId: nodeId)
            : null;

    /// <summary>
    /// 빈 선택지 칸 하나를 고른다 — 카드 아래의 빈 점이다. 아직 이 칸에서 나가는 길이 없다는 것과 길을 더하는 곳을 보인다.
    /// </summary>
    /// <param name="index">카드 아래 칸의 차례(0부터) — 이어진 선택지들 뒤의 빈 칸이다.</param>
    internal void SelectEmptySlot(string episodeId, int index)
    {
        FlushEdgeTyping();
        _grouped.Clear();
        _selectedSlot = (AfterRename(episodeId)!, index);
        _selectedEpisodeId = null;
        _selectedEdgeKey = null;
        _selectedBranchCardId = null;
        _inspector = InspectorMode.Selection;
        ShowSideTab(SideTab.Inspector);
        UnfoldChoices();
        ApplySelectionVisuals();
        RefreshPropertyPanel();
        ShowInHierarchy(null);   // 칸은 하이어아키에 없다
    }

    /// <summary>모든 카드·간선을 기본 모습으로 되돌리고 선택된 것만 파랗게 강조한다.</summary>
    private void ApplySelectionVisuals()
    {
        foreach ((string id, Border card) in _cardById)
        {
            (IBrush? brush, Thickness thickness) = _cardBase[id];
            card.BorderBrush = brush;
            card.BorderThickness = thickness;
        }

        foreach (((string, string, string) key, List<Line> lines) in _lineByEdge)
        {
            (IBrush? stroke, double thickness) = _lineBase[key];

            foreach (Line line in lines)
            {
                line.Stroke = stroke;
                line.StrokeThickness = thickness;
            }
        }

        foreach ((string id, Border chip) in _branchChips)
        {
            (IBrush? brush, Thickness thickness) = _chipBase[id];
            chip.BorderBrush = brush;
            chip.BorderThickness = thickness;
        }

        if (_selectedBranchCardId is { } chipId && _branchChips.TryGetValue(chipId, out Border? selectedChip))
        {
            selectedChip.BorderBrush = Skin.StructureSelection.Brush();
            selectedChip.BorderThickness = new Thickness(2.4);
        }

        foreach (Ellipse dot in _emptySlotDots.Values)
        {
            dot.Stroke = EmptySlotBrush;
            dot.StrokeThickness = 1.6;
        }

        if (_selectedSlot is { } slot && _emptySlotDots.TryGetValue(slot, out Ellipse? selectedDot))
        {
            selectedDot.Stroke = Skin.StructureSelection.Brush();
            selectedDot.StrokeThickness = 3;
        }

        // 범위 선택 무리 — 고른 카드보다 한 단계 가늘다.
        foreach (string member in _grouped)
        {
            if (_cardById.TryGetValue(member, out Border? grouped))
            {
                grouped.BorderBrush = Skin.StructureSelection.Brush();
                grouped.BorderThickness = new Thickness(2);
            }
        }

        if (_selectedEpisodeId is { } episodeId && _cardById.TryGetValue(episodeId, out Border? selected))
        {
            selected.BorderBrush = Skin.StructureSelection.Brush();
            selected.BorderThickness = new Thickness(2.4);
        }

        if (_selectedEdgeKey is { } edgeKey && _lineByEdge.TryGetValue(edgeKey, out List<Line>? selectedLines))
        {
            foreach (Line line in selectedLines)
            {
                line.Stroke = Skin.StructureSelection.Brush();
                line.StrokeThickness = 3.4;
            }
        }
    }

    /// <summary>편집 칸을 마지막으로 채운 선택. 같은 선택이면 다시 채우지 않는다.</summary>
    private (string? Episode, (string From, string To, string Label)? Edge) _panelFilledFor;

    /// <summary>
    /// 선택된 에피소드(또는 간선)의 현재 값으로 패널을 채운다. 원천은 언제나 방금 읽은 모델이다.
    ///
    /// <b>편집 칸은 선택이 바뀔 때만 채운다.</b> 다시 읽기는 언제든 도는데(프로젝트가 바뀔 때마다 —
    /// 엑셀이 있던 때는 파일을 건드리기만 해도) 그때마다 칸을 모델 값으로 되돌리면, 적어 두고 아직 [적용]하지 않은
    /// 글자가 조용히 사라진다 — 사람 눈에는 "적용을 눌러도 안 바뀐다"로 보인다.
    /// 포커스가 어디 있는지로 판정하던 이전 방식은 콤보·체크박스를 만진 순간 뚫렸다.
    /// </summary>
    /// <param name="preserveTyping">다시 읽은 뒤의 다시 그리기(Draw)나 선택지 접기라면 참 — 선택이 그대로면 칸을 다시 채우지 않는다.</param>
    private void RefreshPropertyPanel(bool preserveTyping = false)
    {
        ChapterGraphModel? model = SelectedModel;
        ChapterEpisode? episode = model?.FindEpisode(_selectedEpisodeId ?? string.Empty);
        ChapterEdge? edge = _selectedEdgeKey is { } key
            ? model?.Edges.FirstOrDefault(candidate =>
                candidate.FromEpisodeId == key.From &&
                candidate.ToEpisodeId == key.To &&
                EdgeLabelKey(candidate) == key.Label)
            : null;

        (string Episode, int Index)? slot = _selectedSlot is { } picked && model?.FindEpisode(picked.Episode) is not null
            ? picked
            : null;

        // 고른 분기 카드(칩) — 판에서 사라졌으면(지웠다) 패널도 닫힌다.
        string? branchCard = _selectedBranchCardId is { } chosen && _branchChips.ContainsKey(chosen) ? chosen : null;

        PropertyPanel.IsVisible = episode is not null;
        EdgePanel.IsVisible = edge is not null;
        BranchPanel.IsVisible = branchCard is not null;
        SlotPanel.IsVisible = slot is not null;
        NoSelectionText.IsVisible = episode is null && edge is null && slot is null &&
                                    branchCard is null && _inspector != InspectorMode.Scene;
        ApplyInspector();

        if (slot is { } shownSlot && model is not null)
        {
            RefreshSlotPanel(model, shownSlot.Episode, shownSlot.Index);
        }

        if (branchCard is not null)
        {
            RefreshBranchCardPanel(model, branchCard);
        }

        (string? Episode, (string From, string To, string Label)? Edge) selection =
            (episode?.EpisodeId,
                edge is null ? null : (edge.FromEpisodeId, edge.ToEpisodeId, EdgeLabelKey(edge)));

        FillDialoguePreview(episode?.EpisodeId);

        bool fill = !preserveTyping || selection != _panelFilledFor;
        _panelFilledFor = selection;

        RefreshConditionList(model);

        if (model is not null && edge is not null)
        {
            // 자동 저장(2026-08-17)이 붙은 뒤로는 이 칸들을 채우는 것도 저장을 부를 수
            // 있다 — 화면을 그리는 일이 파일을 쓰면 안 된다.
            _fillingPanel = true;

            try
            {
                RefreshEdgePanel(model, edge, fill);
            }
            finally
            {
                _fillingPanel = false;
            }
        }

        if (model is null || episode is null)
        {
            return;
        }

        // 콤보를 프로그램이 채우는 동안 자동 저장이 울리면 안 된다 — 고르지도 않은 값이
        // 셀로 나간다. 사람 손이 만든 SelectionChanged만 저장을 부른다.
        _fillingPanel = true;

        try
        {
            if (fill)
            {
                IdBox.Text = episode.EpisodeId;
            }

            RefreshEdgeList(model, episode);
        }
        finally
        {
            _fillingPanel = false;
        }
    }

    // ── 편집 모드 ───────────────────────────────────────────────────────────
    //
    // 2026-08-16~08-23: 우측 위 [엑셀에서만 편집] 체크(기본 켬)가 문지기였다.
    // 2026-08-24 소유자 — "저걸 툴사용자가 체크하는 게 아니라, 엑셀이 켜지면 자동으로
    // 잠기면서 편집이 불가능하게 막는게 좋겠어."
    //
    // 그래서 체크를 없앴고(그때는 잠금이 곧 편집 가능 여부였다), 2026-10-29에는 그 잠금도
    // 편집을 막지 않게 됐다(VnTool C6). 엑셀 잠금을 듣는 감시자와 배너는 같은 날 걷혔다.

    /// <summary>콤보·목록을 프로그램이 채우는 중 — 자동 저장 억제.</summary>
    private bool _fillingPanel;

    /// <summary>
    /// 간선 패널의 자동 저장 (2026-08-17) — 고치는 순간 그 셀이 써진다. 패널을 채우는
    /// 중(<see cref="_fillingPanel"/>)에는 울리지 않는다: 화면을 채우는 일이 저장을 부르면
    /// 아무것도 안 고쳤는데 파일이 계속 써진다.
    /// </summary>
    private void AutoSaveEdge()
    {
        if (_fillingPanel || _selectedEdgeKey is null)
        {
            return;
        }

        UiGuard.Run(_session, Ui.Structure_SavePath, ApplyEdgeFromPanel);
    }

    /// <summary>고르기 첫머리(<see cref="FlushEdgeTyping"/>)에서 에피소드ID 칸을 내어 바뀐 이름 — 그 고르기가 옛 ID를 새 ID로 읽는다.</summary>
    private (string Old, string New)? _episodeRenamed;

    /// <summary>고르기 첫머리에서 바뀐 에피소드ID를 따라간다 — 옛 ID면 새 ID, 아니면 그대로.</summary>
    private string? AfterRename(string? episodeId) =>
        _episodeRenamed is { } renamed && string.Equals(episodeId, renamed.Old, StringComparison.Ordinal) ? renamed.New : episodeId;

    /// <summary>고른 간선 — 사라졌으면 null.</summary>
    private ChapterEdge? SelectedEdge() =>
        _selectedEdgeKey is { } key
            ? SelectedModel?.Edges.FirstOrDefault(candidate =>
                candidate.FromEpisodeId == key.From &&
                candidate.ToEpisodeId == key.To &&
                EdgeLabelKey(candidate) == key.Label)
            : null;

    /// <summary>
    /// 선택을 옮기기 전에 초점을 쥔 글 칸을 놓고(그 칸이 지금 대상에 낸다), 간선 패널의 글자 칸에 적던 것을 그 간선에 낸다 — 판의 선 · 점은 초점을 가져가지 않아, 적던 칸이
    /// 초점을 잃지 않은 채 다른 것이 골라지면 적던 글이 사라지거나(칸을 새 선택의 값으로 채운다) 엉뚱한 간선에 들어간다.
    /// </summary>
    private void FlushEdgeTyping()
    {
        // 초점을 쥔 글 칸을 먼저 놓는다 — 초점을 잃을 때 내는 칸(ID · 식 · 스탯 표 · 캐릭터 키 · 잠금 안내문 · 스탯변화 숫자)이
        // 고름이 바뀌기 전의 대상에 낸다(side-boxes J1). 칸이 지금 값과 같으면 아무것도 하지 않는다.
        _episodeRenamed = null;

        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox typing &&
            typing.GetVisualAncestors().Contains(ChapterRightRows))
        {
            InspectorFocusSink.Focus();
        }

        if (!EdgePanel.IsVisible ||
            _selectedEdgeKey is not { } key ||
            SelectedModel?.Edges.FirstOrDefault(candidate =>
                candidate.FromEpisodeId == key.From &&
                candidate.ToEpisodeId == key.To &&
                EdgeLabelKey(candidate) == key.Label) is not { } edge)
        {
            return;
        }

        bool messageTyped = !string.Equals(
            EdgeLockedMsgBox.Text ?? string.Empty, edge.LockedMessage ?? string.Empty, StringComparison.Ordinal);

        if (messageTyped)
        {
            AutoSaveEdge();
        }

        // 식 칸에 적던 것도 낸다 — 선택이 옮겨 가면 그 칸이 사라진다.
        foreach (TextBox expression in new[] { EdgeVisibleExpressionHost, EdgeUnlockExpressionHost }
                     .SelectMany(host => host.GetLogicalDescendants().OfType<TextBox>()))
        {
            if (expression.Tag is ExpressionCommit commit)
            {
                commit.Run();
            }
        }
    }

    /// <summary>
    /// [대사] 탭 — 선택된 에피소드 카드의 대사를 프로젝트에서 읽어 세운다. 연출 그래프까지
    /// 안 가도 챕터 그래프에서 방금 쓴 대사를 확인한다(소유자 요청). 읽기 전용이다.
    ///
    /// ⚠ 2026-10-29(D2)까지는 대본 엑셀을 읽었다. 엑셀 연동이 걷혀 원천은 카드의 대본 하나다.
    /// </summary>
    private void FillDialoguePreview(string? episodeId)
    {
        if (episodeId is null)
        {
            DialoguePreviewHeader.Set(Ui.Structure_SelectEpisodeToSeeIts, brief: null);
            DialoguePreviewText.Text = string.Empty;
            return;
        }

        string preview = DialogueOf(episodeId);

        if (preview.Length > 0)
        {
            DialoguePreviewHeader.Set(string.Empty, brief: null);
        }
        else
        {
            DialoguePreviewHeader.Set(Ui.Structure_NoDialogueWrittenYetWrite, Ui.Structure_NoDialogueWrittenYet);
        }

        DialoguePreviewText.Text = preview;
    }

    /// <summary>그 에피소드 카드의 대사를 「화자: 대사」 줄로 편다. 카드나 대본이 없으면 빈 글이다.</summary>
    private string DialogueOf(string episodeId) =>
        _session is not null &&
        _selectedChapterId is { } chapterId &&
        EpisodeNaming.CardFor(_session.Project, chapterId, episodeId) is { } card
            ? DialogueOf(card)
            : string.Empty;

    /// <summary>분기 카드의 대본 — 인스펙터의 읽기 전용 칸. 비었으면 그렇다고 말한다(줄 없는 분기 카드는 검증 오류다).</summary>
    private string ScriptOf(string nodeId) =>
        _session?.Project.FindDialogue(nodeId) is { } card && DialogueOf(card) is { Length: > 0 } text
            ? text
            : Ui.Structure_NoDialogueWrittenYetWrite2;

    private static string DialogueOf(Vn.Authoring.Model.DialogueNode card)
    {
        Vn.Authoring.Script.ScriptDocument script = card.Script;

        var lines = new List<string>();

        foreach (Vn.Authoring.Script.ScriptLine line in script.ActiveLines)
        {
            Vn.Authoring.Script.LocalizedLine text = script.Text(line.Id);
            string body = text.Speaker.Length > 0 ? $"{text.Speaker}: {text.Text}" : text.Text;

            if (body.Length > 0)
            {
                lines.Add(body);
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>목록을 갈되 고른 값은 지킨다(그 값이 새 목록에 남아 있다면).</summary>
    private static void SetItems(ComboBox combo, IReadOnlyList<string> items, string? selected)
    {
        combo.ItemsSource = items;
        combo.SelectedItem = selected is not null && items.Contains(selected) ? selected : null;
    }

    // ── 선택지 목록 (v9, 2026-08-17 소유자) ────────────────────────────────
    // 목록의 줄 = 이 에피소드에서 나가는 길 하나이고, 길 하나가 곧 선택지 하나다. 여기서는 확인만 한다 — 선택지를 잇고 끊는 것은
    // [대본] 탭(카드 아래 칸), 문구 · 조건 · 스탯변화는 판에서 그 선을 눌러 여는 간선 패널이다.

    /// <summary>
    /// 접은 선택지 줄들의 신원 (도착, 문구). 선택지 설명은 <b>기본으로 펼쳐져</b> 있고, 사람이 접은 줄만 여기 든다 — 줄마다
    /// 따로 접고 편다.
    /// </summary>
    private readonly HashSet<(string To, string Label)> _foldedChoices = [];

    /// <summary>펼친 선택지 설명이 지는 표 — 화면 시험이 이것으로 찾는다.</summary>
    internal const string ChoiceDetailTag = "choice-detail";

    /// <summary>선택지를 다시 모두 펼친다 — 다른 것을 고르면 부른다(접은 것은 그 에피소드를 보는 동안만이다).</summary>
    private void UnfoldChoices() => _foldedChoices.Clear();

    /// <summary>
    /// 선택지 줄 클릭 — 펼쳐진 줄이면 그 설명을 접고, 접힌 줄이면 다시 편다. 다른 줄은 그대로다.
    /// </summary>
    internal void ToggleChoice(ChapterEdge edge)
    {
        (string To, string Label) key = (edge.ToEpisodeId, EdgeLabelKey(edge));

        if (!_foldedChoices.Remove(key))
        {
            _foldedChoices.Add(key);
        }

        RefreshPropertyPanel(preserveTyping: true);
    }

    /// <summary>
    /// 펼친 선택지의 설명 — 표시조건 · 해금조건(+잠금 안내문) · 스탯변화. <b>읽기 전용</b>이다. 문구와 도착은 바로 위
    /// 줄이 보이므로 되풀이하지 않는다.
    /// </summary>
    private static Border BuildChoiceDetail(ChapterGraphModel model, ChapterEdge edge)
    {
        var lines = new StackPanel { Spacing = 2 };

        void Line(string text) => lines.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = Skin.Font(11),
            TextWrapping = TextWrapping.Wrap
        });

        if (edge.VisibleConditionLabel is { } visible)
        {
            Line(string.Format(Ui.Structure_ShowCondition2, visible));
        }

        if (edge.ConditionLabel is { } unlock)
        {
            Line(string.Format(Ui.Structure_UnlockCondition2, unlock));

            if (edge.LockedMessage is { Length: > 0 } locked)
            {
                Line(string.Format(Ui.Structure_LockedHint, locked));
            }
        }

        Line(edge.StatChanges.Count > 0
            ? Ui.Structure_StatChange + string.Join(" · ", edge.StatChanges.Select(delta => DeltaText(model.Stats, delta)))
            : Ui.Structure_StatChangeNone);

        return new Border
        {
            Tag = ChoiceDetailTag,
            Margin = new Thickness(22, 0, 0, 4),
            Padding = new Thickness(8, 4),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = Skin.Accent.Brush(),
            Child = lines
        };
    }

    private void RefreshEdgeList(ChapterGraphModel model, ChapterEpisode episode)
    {
        EdgeListPanel.Children.Clear();

        List<ChapterEdge> edges = model.Edges
            .Where(candidate =>
                string.Equals(candidate.FromEpisodeId, episode.EpisodeId, StringComparison.Ordinal))
            .ToList();
        ChoiceHeaderText.Text = edges.Count > 0 ? string.Format(Ui.Structure_Choices, edges.Count) : Ui.Structure_Choices2;

        for (int rowIndex = 0; rowIndex < edges.Count; rowIndex++)
        {
            ChapterEdge edge = edges[rowIndex];
            EdgeListPanel.Children.Add(BuildEdgeRow(edge, rowIndex));

            // 펼친 줄이면 바로 아래에 설명이 선다.
            if (!_foldedChoices.Contains((edge.ToEpisodeId, EdgeLabelKey(edge))))
            {
                EdgeListPanel.Children.Add(BuildChoiceDetail(model, edge));
            }
        }

        if (edges.Count == 0)
        {
            EdgeListPanel.Children.Add(new GuideLine
            {
                Full = Ui.Structure_ThereSNoWayOut,
                Brief = Ui.Structure_ThereSNoWayOut2,
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.5),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    /// <summary>
    /// 선택지 한 줄 — `문구 → 도착`. 자동 길이면 「AUTO · 자동 진행」, 문구가 없으면 「⚠ 문구 없음」이다. 조건은 줄에 붙이지
    /// 않는다 — 바로 아래 펼친 설명이 표시조건 · 해금조건을 보인다.
    /// 클릭 = 그 줄 아래에 조건 · 스탯변화를 펼치기, 다시 클릭 = 접기.
    /// </summary>
    private Control BuildEdgeRow(ChapterEdge edge, int rowIndex)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };

        // 순번 — 화면에 뜨는 차례다(간선 시트의 행 순서). 신원이 아니라 자리 표시다.
        row.Children.Add(new Border
        {
            Background = (edge.HasNoOptionLabel ? Skin.Hairline : Skin.ChoiceInk).Brush(0x22),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = (rowIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.8)
            }
        });

        string label = edge.Auto ? Ui.Structure_AUTOAutoPath : edge.HasNoOptionLabel ? Ui.Structure_NoText : edge.OptionLabel!;
        var text = new TextBlock
        {
            Text = $"{label}   → {edge.ToEpisodeId}",
            FontSize = Skin.Font(11),
            Opacity = edge.HasNoOptionLabel ? 0.7 : 1,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            // 배경 없는 TextBlock은 글자 획 위만 클릭된다 — 행 전체가 눌리게 깔아 둔다.
            Background = Brushes.Transparent,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        Grid.SetColumn(text, 1);

        // 툴팁 없음 (2026-08-16 소유자) — 목록 위에 뜨는 설명 상자가 화면을 가렸다.
        // 같은 줄을 다시 누르면 접힌다 (2026-08-16 소유자) — 여는 손과 닫는 손이 같다.
        text.PointerPressed += (_, _) => UiGuard.Run(_session, Ui.Structure_ExpandChoices, () => ToggleChoice(edge));

        // ✕(길 끊기)는 두지 않는다 — 선택지를 잇고 끊는 것은 [대본] 탭(카드 아래 칸)에서 한다.
        row.Children.Add(text);

        return row;
    }

    /// <summary>
    /// 간선 편집 패널 — "이 분기는 언제 보이고 언제 열리는가"를 채운다.
    /// <paramref name="fill"/>이 거짓이면 사람이 적던 값을 지킨다(위 설명 참조).
    /// </summary>
    private void RefreshEdgePanel(ChapterGraphModel model, ChapterEdge edge, bool fill)
    {
        if (fill)
        {
            EdgeLockedMsgBox.Text = edge.LockedMessage ?? string.Empty;
            _edgeStats.Load(model.Stats, edge.StatChanges);
        }

        // 자동 진행 길은 문구 · 조건 · 스탯변화를 갖지 않는다 — 그렇다고 말하고 칸을 잠근다(적어도 저장되지 않는다).
        EdgeAutoNote.IsVisible = edge.Auto;
        EdgeVisibleCombo.IsEnabled = !edge.Auto;
        EdgeConditionCombo.IsEnabled = !edge.Auto;
        EdgeVisibleNewButton.IsEnabled = !edge.Auto;
        EdgeUnlockNewButton.IsEnabled = !edge.Auto;
        EdgeLockedMsgBox.IsEnabled = !edge.Auto;
        _edgeStats.IsEnabled = !edge.Auto;

        var labels = new List<string> { NoGate };
        labels.AddRange(model.Conditions.Select(condition => condition.Label));

        // 관문 둘 — v8에서 에피소드에서 여기로 옮겨 왔다.
        SetItems(EdgeVisibleCombo, labels,
            fill ? edge.VisibleConditionLabel ?? NoGate : EdgeVisibleCombo.SelectedItem as string);
        SetItems(EdgeConditionCombo, labels,
            fill ? edge.ConditionLabel ?? NoGate : EdgeConditionCombo.SelectedItem as string);

        string? visible = GateOf(EdgeVisibleCombo);
        string? unlock = GateOf(EdgeConditionCombo);

        FillExpressionHost(EdgeVisibleExpressionHost, model, edge, edge.Auto ? null : visible);
        FillExpressionHost(EdgeUnlockExpressionHost, model, edge, edge.Auto ? null : unlock);

        // 잠금 안내문은 해금조건에 딸린 칸이다 — 해금조건을 걸었을 때만 선다.
        EdgeLockedMsgBox.IsVisible = !edge.Auto && unlock is not null;
    }

    /// <summary>마지막으로 연 [＋ 새 조건] 칸 — 화면 시험이 이것으로 찾는다(플라이아웃은 뷰의 논리 트리 밖에 선다).</summary>
    internal StackPanel? NewConditionPanel { get; private set; }

    /// <summary>관문 콤보의 「없음」 — 조건을 안 건 길.</summary>
    private static string NoGate => Ui.Structure_None;

    private static string? GateOf(ComboBox combo) =>
        combo.SelectedItem is string label && !string.Equals(label, NoGate, StringComparison.Ordinal) ? label : null;

    /// <summary>식 칸이 지는 표 — 칸의 값을 그 조건에 낸다(같은 값이면 아무것도 안 한다).</summary>
    private sealed record ExpressionCommit(Action Run);

    /// <summary>
    /// 관문 콤보 바로 아래의 <b>식 칸</b> — 고른 조건의 수치를 여기서 바로 고친다(2026-11-17 소유자: [구성]은 수치를 잡는 자리다).
    /// 고치면 챕터 조건의 식이 바뀐다 — 그 조건을 쓰는 다른 곳이 있으면 함께 바뀐다고 미리 말한다. 식이 비면 「식 미정」(항상 통과),
    /// 못 읽는 식이면 그 까닭을 빨갛게 세운다. 조건을 안 걸었으면 아무것도 세우지 않는다.
    ///
    /// 적던 칸은 다시 세우지 않는다 — 다른 칸을 고쳐 판이 다시 그려져도 쓰던 식이 끊기지 않게.
    /// </summary>
    private void FillExpressionHost(StackPanel host, ChapterGraphModel model, ChapterEdge edge, string? label)
    {
        if (host.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault() is { IsFocused: true } typing &&
            Equals(host.DataContext, label) &&
            label is not null &&
            model.Conditions.FirstOrDefault(item => item.Label == label) is { } same &&
            !string.Equals(typing.Text?.Trim() ?? string.Empty, same.Expression.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        host.DataContext = label;
        host.Children.Clear();

        if (label is null ||
            _session is null ||
            _selectedChapterId is not { } chapterId ||
            model.Conditions.FirstOrDefault(item => string.Equals(item.Label, label, StringComparison.Ordinal)) is not { } condition)
        {
            return;
        }

        string stored = condition.Expression.Trim();
        var box = new TextBox
        {
            Text = stored,
            FontSize = Skin.Font(11),
            PlaceholderText = Ui.Structure_UndecidedAlwaysPassesEG,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ToolTip.SetTip(box, Ui.Structure_ThisConditionSExpressionCompares);

        void Commit()
        {
            string typed = box.Text?.Trim() ?? string.Empty;

            if (string.Equals(typed, stored, StringComparison.Ordinal) || _session is null)
            {
                return;
            }

            UiGuard.Run(_session, Ui.Structure_ConditionExpression, () =>
            {
                _session.Editor.UpdateChapterCondition(chapterId, label, typed);
                stored = typed;
                _session.SetStatus(typed.Length == 0
                    ? string.Format(Ui.Structure_ClearedExpressionOfConditionUndecided, label)
                    : string.Format(Ui.Structure_ConditionExpressionUndoCtrlZ, label, typed));
            });
        }

        box.Tag = new ExpressionCommit(Commit);
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) => CommitOnEnter(args, Commit);
        EscToRevert.Wire(box, () => stored);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(12, 0, 0, 0) };
        row.Children.Add(new TextBlock
        {
            Text = Ui.Structure_Expression,
            FontSize = Skin.Font(10),
            Opacity = Skin.Dim(0.6),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(box, 1);
        row.Children.Add(box);
        host.Children.Add(row);

        TextBlock Note(string text, IBrush? ink = null) => new()
        {
            Text = text,
            FontSize = Skin.Font(10),
            Opacity = ink is null ? 0.6 : 1,
            Foreground = ink,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 0, 0, 0)
        };

        // 진단 문장의 머리와 같은 표에서 꺼낸다 — 화면 언어가 무엇이든 그 조건의 진단만 고른다(english-ui E5).
        string head = string.Format(Vn.Authoring.Strings.Msg.Diag_ConditionHead, label);

        foreach (ChapterDiagnostic problem in model.Diagnostics.Where(item =>
                     item.Severity == ChapterDiagnosticSeverity.Error &&
                     item.Message.StartsWith(head, StringComparison.Ordinal)))
        {
            host.Children.Add(Note("✕ " + problem.Message[head.Length..], Skin.DangerDeep.Brush()));
        }

        int others = _session.Editor.FindConditionUses(chapterId, label)
            .Count(use => !(use.Kind == Vn.Authoring.Editing.ChapterUseKind.Edge && string.Equals(use.Where, edge.Where, StringComparison.Ordinal)));

        if (others > 0)
        {
            host.Children.Add(Note(Plural.Format(Ui.Structure_ThisConditionIsAlsoUsed, others)));
        }
    }

    /// <summary>
    /// [＋ 새 조건] — 이름과 식을 한 번에 만들고 이 선택지의 표시조건(또는 해금조건)으로 건다. 되돌리기 한 칸이다.
    ///
    /// 간선에서 수치를 잡다가 조건이 없으면 탭을 오가야 해서 둔 자리다(2026-11-17 소유자). 이름 규칙은 조건 표의 더하기 줄과 같다
    /// (<see cref="ShowConditionFlyout"/>).
    /// </summary>
    private void ShowNewConditionFlyout(Button anchor, bool visible)
    {
        if (_session is null || _selectedChapterId is not { } chapterId || _selectedEdgeKey is not { } key)
        {
            return;
        }

        ShowConditionFlyout(
            anchor,
            chapterId,
            visible ? Ui.Structure_NewConditionThisChoiceShows : Ui.Structure_NewConditionThisChoiceCan,
            visible ? Ui.Structure_CreateAndSetAsShow : Ui.Structure_CreateAndSetAsUnlock,
            (label, typed) =>
            {
                if (SelectedModel?.Edges.FirstOrDefault(candidate =>
                        candidate.FromEpisodeId == key.From &&
                        candidate.ToEpisodeId == key.To &&
                        EdgeLabelKey(candidate) == key.Label) is not { } edge)
                {
                    _session.SetStatus(Ui.Structure_PleaseSelectPathAgainSelection);
                    return false;
                }

                // 조건 만들기와 걸기가 한 변경이다 — 되돌리기 한 번에 함께 돌아간다.
                _session.Editor.Batch(() =>
                {
                    _session.Editor.AddChapterCondition(chapterId, label, typed);
                    _session.Editor.UpdateEdge(
                        chapterId, key.From, key.To,
                        visibleConditionLabel: visible ? label : null,
                        conditionLabel: visible ? null : label,
                        matchOptionLabel: EdgeLabelKey(edge));
                });

                _panelFilledFor = default;   // 다시 그릴 때 콤보를 모델 값(방금 건 조건)으로 채운다
                _session.SetStatus(visible
                    ? typed.Length == 0
                        ? string.Format(Ui.Structure_CreatedConditionAndSetIt, label)
                        : string.Format(Ui.Structure_CreatedConditionAndSetIt2, label)
                    : typed.Length == 0
                        ? string.Format(Ui.Structure_CreatedConditionAndSetIt3, label)
                        : string.Format(Ui.Structure_CreatedConditionAndSetIt4, label));
                return true;
            });
    }

    /// <summary>
    /// 조건 이름과 식을 한 번에 받는 플라이아웃 — 간선의 [＋ 새 조건]. ⛔ 조건 표의 [＋ 조건]도 함께 썼는데 늘 서 있는 더하기 줄로 바뀌었다
    /// (writer-comfort WC4). 이름 규칙은 [대본] 탭과 같다 —
    /// 빈 이름 · 이미 있는 이름은 받지 않는다(상태줄이 말한다). 식은 비워도 된다(식 미정 — 항상 통과). 받으면(<paramref name="accept"/>가
    /// 참) 닫힌다. 만들기 단추와 두 칸의 Enter가 같다.
    /// </summary>
    private void ShowConditionFlyout(
        Button anchor, string chapterId, string intro, string createText, Func<string, string, bool> accept)
    {
        var name = new TextBox { FontSize = Skin.Font(11), MinWidth = 240, PlaceholderText = Ui.Structure_ConditionNameByMeaningE, Tag = "new-condition-name" };
        var expression = new TextBox { FontSize = Skin.Font(11), MinWidth = 240, PlaceholderText = Ui.Structure_ExpressionEmptyMeansUndecidedE, Tag = "new-condition-expression" };
        var create = new Button
        {
            Content = createText,
            FontSize = Skin.Font(10),
            Padding = new Thickness(7, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Tag = "new-condition-create"
        };
        create.VerticalAlignment = VerticalAlignment.Center;

        // 만들기 단추 오른쪽의 「식 예시 보기」 — 손을 올리면 이 챕터의 스탯으로 든 예시가 선다(2026-11-26 소유자).
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                create,
                ExpressionExamplesLink(_session?.Editor.FindChapter(chapterId)?.Stats ?? [])
            }
        };
        var panel = new StackPanel
        {
            Spacing = 4,
            MaxWidth = 340,
            Children =
            {
                new TextBlock { Text = intro, FontSize = Skin.Font(10), TextWrapping = TextWrapping.Wrap },
                name,
                expression,
                actions
            }
        };
        var flyout = new Flyout { Content = panel };
        NewConditionPanel = panel;

        void Create() => UiGuard.Run(_session, Ui.Structure_NewCondition, () =>
        {
            string label = name.Text?.Trim() ?? string.Empty;
            string typed = expression.Text?.Trim() ?? string.Empty;

            if (label.Length == 0)
            {
                _session!.SetStatus(Ui.Side_TypeConditionName);
                return;
            }

            if (_session!.Editor.FindChapter(chapterId)?.Conditions.Any(item => string.Equals(item.Label, label, StringComparison.Ordinal)) == true)
            {
                _session.SetStatus(string.Format(Ui.Structure_ConditionAlreadyExistsPickIt, label, Josa.이가(label)));
                return;
            }

            if (accept(label, typed))
            {
                flyout.Hide();
            }
        });

        create.Click += (_, _) => Create();
        name.KeyDown += (_, args) => CommitOnEnter(args, Create);
        expression.KeyDown += (_, args) => CommitOnEnter(args, Create);

        flyout.ShowAt(anchor);
        name.Focus();
    }

    /// <summary>
    /// 「식 예시 보기」 — 손을 올리면 <b>이 챕터의 스탯으로 든 식 예시</b>가 풍선으로 선다(2026-11-26 소유자 — 「호버를 하면 창이 뜨면서 구체적인
    /// 예시를 보여주도록」). 글은 검증 보고의 고치는 법과 같은 것(<see cref="ConditionExpressionHelp.Examples"/>)이고 상자 모양도 같다 — 적기 전에
    /// 보는 예시와 틀린 뒤에 보는 예시가 한 벌이다. 스탯이 아직 없으면 스탯부터 더하라고 말하고 흔한 이름으로 든다.
    ///
    /// 풍선은 보통 풍선보다 빨리 뜨고(<see cref="ExamplesShowDelay"/>) 넓다 — 예시의 식 칸이 한 줄에 서야 줄이 맞는다.
    /// 글자에 손이 닿지 않는 틈에서도 뜨게 투명한 바탕의 상자에 단다.
    /// </summary>
    private static Control ExpressionExamplesLink(IReadOnlyList<ChapterStat> stats)
    {
        var link = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(2, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Help),
            Tag = ExpressionExamplesTag,
            Child = new TextBlock
            {
                Text = Ui.Structure_ExpressionExamples,
                FontSize = Skin.Font(10),
                Foreground = Skin.Accent.Brush(),
                TextDecorations = TextDecorations.Underline
            }
        };

        ToolTip.SetTip(link, new ToolTip
        {
            MaxWidth = 640,
            Content = ExamplesBox(ConditionExpressionHelp.Examples(stats), margin: default)
        });
        ToolTip.SetShowDelay(link, ExamplesShowDelay);
        ToolTip.SetPlacement(link, PlacementMode.BottomEdgeAlignedLeft);
        return link;
    }

    /// <summary>「식 예시 보기」 — 화면 시험이 찾는다.</summary>
    internal const string ExpressionExamplesTag = "expression-examples";

    /// <summary>「식 예시 보기」 풍선이 뜨기까지(ms) — 보통 풍선(400)보다 빠르다. 보려고 손을 올린 자리다.</summary>
    internal const int ExamplesShowDelay = 150;

    /// <summary>
    /// 분기 카드(칩)의 패널 — 부르는 곳 · 조건(고친다) · 대본(읽기 전용). 분기 선을 고른 것과 같은 조건 칸이다 — 조건은 분기마다가 아니라
    /// 분기 카드의 첫머리에 산다(<see cref="BranchEntryCondition"/>).
    /// </summary>
    private void RefreshBranchCardPanel(ChapterGraphModel? model, string nodeId)
    {
        BranchIdBox.Text = _session?.Project.FindDialogue(nodeId)?.Name ?? string.Empty;
        BranchFromToPanel.Children.Clear();
        BranchFromToPanel.Children.Add(new TextBlock
        {
            Text = Ui.Structure_CalledFrom,
            FontSize = Skin.Font(11),
            Opacity = Skin.Dim(0.6),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        List<ChapterBranchMarkers.Marker> callers = _branchMarkers
            .Where(marker => string.Equals(marker.ToNodeId, nodeId, StringComparison.Ordinal))
            .DistinctBy(marker => marker.FromNodeId)
            .ToList();

        if (model is not null)
        {
            foreach (ChapterBranchMarkers.Marker caller in callers)
            {
                BranchFromToPanel.Children.Add(CardLink(model, caller.FromEpisodeId, caller.FromNodeId));
            }
        }

        if (callers.Count == 0)
        {
            BranchFromToPanel.Children.Add(new TextBlock
            {
                Text = Ui.Structure_NoneNotPlayed,
                FontSize = Skin.Font(11),
                Opacity = Skin.Dim(0.6),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
        }

        FillBranchCondition(model, nodeId);
        BranchScriptText.Text = ScriptOf(nodeId);
    }

    /// <summary>
    /// 분기 카드의 조건 칸을 채운다 — 작가가 대사 편집기에서 지은 조건 구조는 <b>읽기만</b> 한다(덮으면 짝이 어긋난 대본이 된다).
    /// 그때는 칸을 잠그고 사유를 세운다.
    /// </summary>
    private void FillBranchCondition(ChapterGraphModel? model, string nodeId)
    {
        BranchEntryCondition.State state = BranchEntryCondition.Read(
            _session!.Editor.Project, nodeId);

        BranchBlockedText.Text = state.Blocked ?? string.Empty;
        BranchBlockedText.IsVisible = state.Blocked is not null;
        BranchConditionCombo.IsEnabled = state.Editable;

        var labels = new List<string> { BranchAlwaysItem };
        labels.AddRange(model?.Conditions.Select(condition => condition.Label) ?? []);

        // 채우는 동안 자동 저장이 울리면 고르지도 않은 값이 대본으로 나간다.
        _fillingPanel = true;

        try
        {
            SetItems(BranchConditionCombo, labels, state.Label ?? BranchAlwaysItem);
        }
        finally
        {
            _fillingPanel = false;
        }
    }

    /// <summary>그 카드의 이름으로 건너뛰는 고리 — 에피소드면 그 에피소드를, 분기 카드면 그 칩을 고른다.</summary>
    private Control CardLink(ChapterGraphModel model, string? episodeId, string nodeId) =>
        episodeId is not null
            ? EpisodeLink(model, episodeId)
            : BranchCardLink(nodeId, BranchCardName(nodeId));

    /// <summary>분기 카드의 이름 — 판에 없으면 NodeId.</summary>
    /// <summary>
    /// 분기 카드 ID 칸의 Enter — 카드 이름(= Yarn 노드 이름)을 바꾼다(되돌리기 한 칸). 부르는 쪽은 노드 Id로 이으므로 따라갈 것이 없다.
    /// <b>비울 수 없고 프로젝트의 다른 노드가 쓰는 이름으로는 바꾸지 않는다</b> — Yarn 노드 이름은 프로젝트에서 하나다(2026-11-17).
    /// </summary>
    internal void RenameInspectedBranchCard()
    {
        if (_session is null || _selectedBranchCardId is not { } nodeId ||
            _session.Project.FindDialogue(nodeId) is not { IsBranchCard: true } card)
        {
            return;
        }

        ChapterDocument chapter = EpisodeNaming.ChapterOf(_session.Project, card);

        string wanted = BranchIdBox.Text?.Trim() ?? string.Empty;

        if (string.Equals(wanted, card.Name, StringComparison.Ordinal))
        {
            return;
        }

        HashSet<string> taken = NewNames.TakenNames(_session.Project, chapter);
        taken.Remove(card.Name);
        taken.Remove(Vn.Authoring.Rendering.YarnNaming.StoryNodeTitleOf(card.Name, card.Id));

        string? refusal = wanted.Length == 0
            ? Ui.Structure_BranchCardIDCanT
            : taken.Contains(wanted) || taken.Contains(Vn.Authoring.Rendering.YarnNaming.StoryNodeTitleOf(wanted, card.Id))
                ? string.Format(Ui.Structure_IsAlreadyUsedByAnother, wanted)
                : null;

        if (refusal is not null)
        {
            BranchIdBox.Text = card.Name;
            _session.SetStatus(refusal);
            return;
        }

        string old = card.Name;
        _session.Editor.RenameNode(card.Id, wanted);
        _session.SetStatus(string.Format(Ui.Structure_BranchCardIDTitleStays, old, wanted, EpisodeNaming.BranchTitleOf(card)));
    }

    private string BranchCardName(string nodeId) =>
        _session?.Project.FindDialogue(nodeId) is { } card ? EpisodeNaming.BranchTitleOf(card) : nodeId;

    /// <summary>분기 카드로 건너뛰는 고리 — 누르면 그 칩을 고른다(조건 · 대본).</summary>
    private Control BranchCardLink(string nodeId, string name)
    {
        var link = new TextBlock
        {
            Text = $"⑂ {name}",
            FontSize = Skin.Font(11),
            TextDecorations = TextDecorations.Underline,
            Foreground = Skin.SceneChip.Brush(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        ToolTip.SetTip(link, Ui.Structure_SelectsThisBranchCardShows);

        link.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Run(_session, Ui.Structure_SelectBranchCard, () => SelectBranchCard(nodeId));
        };

        return link;
    }

    /// <summary>
    /// 빈 선택지 칸의 패널 — 어느 에피소드의 몇째 칸인지, 아직 길이 없다는 것, 길을 더하는 곳을 말한다.
    /// </summary>
    private void RefreshSlotPanel(ChapterGraphModel model, string episodeId, int index)
    {
        int options = _optionsByEpisode.TryGetValue(episodeId, out List<ChapterEdge>? edges) ? edges.Count : 0;
        int slots = SlotsFor(options);

        SlotTitleText.Text = string.Format(Ui.Structure_ChoiceSlotEmpty, index + 1, slots);
        SlotFromPanel.Children.Clear();
        SlotFromPanel.Children.Add(EpisodeLink(model, episodeId));
        SlotFromPanel.Children.Add(new TextBlock
        {
            Text = Ui.Structure_NoPathYet,
            FontSize = Skin.Font(11),
            Opacity = Skin.Dim(0.6),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        string slotState = options == 0
            ? Ui.Structure_ThisEpisodeHasNoWay
            : Plural.Format(Ui.Structure_ThisEpisodeHasWaysOut, options);
        SlotDetailText.Set(slotState + Ui.Structure_InScriptTabWriteText, slotState);
    }

    /// <summary>조건을 안 건 분기 — 언제나 재생된다. 「안 고름」과 같은 뜻이다.</summary>
    private static string BranchAlwaysItem => Ui.Branch_Always;

    /// <summary>고른 분기의 조건을 적는다. 고르는 순간 나간다 — 간선 패널과 같은 감각이다.</summary>
    private void SaveBranchCondition()
    {
        if (_fillingPanel || _session is null || SelectedBranchTarget() is not { } target)
        {
            return;
        }

        string? label = BranchConditionCombo.SelectedItem as string;

        string? failure = BranchEntryCondition.Set(
            _session.Editor,
            target.NodeId,
            string.Equals(label, BranchAlwaysItem, StringComparison.Ordinal) ? null : label);

        _session.SetStatus(failure ?? (label is null || label == BranchAlwaysItem
            ? string.Format(Ui.Structure_RemovedConditionOfBranchCard, BranchCardName(target.NodeId))
            : string.Format(Ui.Structure_BranchCardPlaysWhenCondition, BranchCardName(target.NodeId), label)));
    }

    /// <summary>조건 칸이 고치는 분기 카드 — 고른 칩.</summary>
    private (string NodeId, string Name)? SelectedBranchTarget() =>
        _selectedBranchCardId is { } nodeId && _branchChips.ContainsKey(nodeId)
            ? (nodeId, BranchCardName(nodeId))
            : null;

    /// <summary>
    /// 간선 패널에서 그 끝의 에피소드로 건너뛰는 고리. 누르면 그 에피소드가 선택되어
    /// 속성 패널(Id [이름] 칸·제목)이 열린다 — 간선을 보다가 에피소드 이름을 고치려 할 때
    /// 노드를 다시 찾아 클릭하지 않아도 된다.
    /// </summary>
    private Control EpisodeLink(ChapterGraphModel model, string episodeId)
    {
        string title = model.FindEpisode(episodeId)?.Title ?? string.Empty;

        var link = new TextBlock
        {
            Text = title.Length == 0 || string.Equals(title, episodeId, StringComparison.Ordinal)
                ? episodeId
                : $"{episodeId} ({title})",
            FontSize = Skin.Font(11),
            TextDecorations = TextDecorations.Underline,
            Foreground = Skin.Accent.Brush(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        ToolTip.SetTip(link, Ui.Structure_SelectsThisEpisodeItsName);

        link.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Run(_session, Ui.Structure_SelectEpisode2, () => SelectEpisode(episodeId));
        };

        return link;
    }

    /// <summary>간선 패널의 [적용]. 바뀐 필드만 고친다.</summary>
    internal void ApplyEdgeFromPanel()
    {
        if (_selectedEdgeKey is not { } key || _selectedChapterId is not { } chapterId ||
            SelectedModel?.Edges.FirstOrDefault(candidate =>
                candidate.FromEpisodeId == key.From &&
                candidate.ToEpisodeId == key.To &&
                EdgeLabelKey(candidate) == key.Label) is not { } edge)
        {
            // 조용한 무동작 금지 — 사람에게는 "눌러도 아무 일이 없다"가 된다.
            _session?.SetStatus(Ui.Structure_PleaseSelectPathAgainSelection);
            return;
        }

        string? Changed(string? boxValue, string? current)
        {
            string value = boxValue?.Trim() ?? string.Empty;
            return string.Equals(value, current ?? string.Empty, StringComparison.Ordinal) ? null : value;
        }

        string Gate(ComboBox combo) => GateOf(combo) ?? string.Empty;

        // 문구는 여기서 고치지 않는다(읽기 전용 — 2026-11-17). 자동인지도 여기서 바꾸지 않는다.
        bool auto = edge.Auto;

        ChapterEdit(
            () =>
            {
                _session!.Editor.UpdateEdge(
                    chapterId, key.From, key.To,
                    visibleConditionLabel: Changed(auto ? string.Empty : Gate(EdgeVisibleCombo), edge.VisibleConditionLabel ?? string.Empty),
                    conditionLabel: Changed(auto ? string.Empty : Gate(EdgeConditionCombo), edge.ConditionLabel ?? string.Empty),
                    lockedMessage: Changed(auto ? string.Empty : EdgeLockedMsgBox.Text, edge.LockedMessage ?? string.Empty),
                    statChanges: Changed(auto ? string.Empty : _edgeStats.ToSheetText(), StatChangesText(edge)),
                    matchOptionLabel: EdgeLabelKey(edge));
            },
            string.Format(Ui.Structure_SavedPath, key.From, key.To));

        // ⛔ 여기 있던 `Reload()`는 2026-09-16에 걷혔다 (R-F). 손에 든 모델이 낡는 것을
        //    막으려던 줄인데, 이제 판이 프로젝트에서 오므로 편집이 곧 그 판이다 —
        //    낡을 사본이 없다.
    }

    /// <summary>간선 스탯변화를 시트 문법 그대로 — 패널 칸과 셀이 같은 글을 쓴다.</summary>
    private static string StatChangesText(ChapterEdge edge) => string.Join("; ", edge.StatChanges
        .Select(delta => $"{delta.Key} {(delta.Amount >= 0 ? "+" : "")}{delta.Amount}"));

    /// <summary>
    /// 챕터ID 칸의 Enter — 챕터 개명(<see cref="ChapterRenamer"/> — 판 · 조건 노드 · 진행 JSON 파일 이름이 함께 간다). [구성] 탭은 ID를
    /// 다루고 챕터 이름은 [대본] 탭에서 고친다(display-names N4).
    /// </summary>
    internal void RenameSelectedChapter()
    {
        if (_session is null || _selectedChapterId is not { } oldId)
        {
            return;
        }

        string newId = ChapterIdBox.Text?.Trim() ?? string.Empty;

        if (newId.Length == 0 || string.Equals(newId, oldId, StringComparison.Ordinal))
        {
            ChapterIdBox.Text = oldId;
            return;
        }

        ChapterRenamer.Result result = ChapterRenamer.Rename(_session.Editor, _session.ProjectPath, oldId, newId);

        if (!result.Renamed)
        {
            ChapterIdBox.Text = oldId;   // 받지 않았으면 칸이 지금 값으로 돌아온다
            _session.SetStatus(result.Failure!);
            return;
        }

        Refresh();
        SelectChapter(newId);
        _session.SetStatus(string.Format(Ui.Structure_ChapterIDBoardConditionNode, oldId, newId));
    }

    /// <summary>
    /// 장면ID 칸의 Enter — 그 장면의 에피소드 모두의 장면ID를 바꾼다(<see cref="ProjectEditor.RenameScene"/> — 장면 이름 · 메모가 함께
    /// 간다, 되돌리기 한 칸). <b>다른 장면이 쓰는 ID로는 바꾸지 않는다</b> — 그러면 두 장면이 합쳐진다(합치기는 [대본] 탭에서 에피소드를
    /// 끌어 옮긴다). 비울 수도 없다 — 에피소드는 늘 장면에 속한다.
    /// </summary>
    internal void RenameInspectedScene()
    {
        if (_session is null || _selectedChapterId is not { } chapterId || _inspectedSceneId is not { } oldId)
        {
            return;
        }

        string newId = SceneIdBox.Text?.Trim() ?? string.Empty;

        if (string.Equals(newId, oldId, StringComparison.Ordinal))
        {
            return;
        }

        if (newId.Length == 0)
        {
            SceneIdBox.Text = oldId;
            _session.SetStatus(Ui.Structure_SceneIDCanTBe);
            return;
        }

        if (_session.Editor.FindChapter(chapterId)?.Episodes.Any(episode =>
                string.Equals(episode.SceneId, newId, StringComparison.Ordinal)) == true)
        {
            SceneIdBox.Text = oldId;
            _session.SetStatus(string.Format(Ui.Structure_SceneIDIsUsedBy, newId));
            return;
        }

        // 자동 모양 그대로인 에피소드ID · 분기 카드 이름도 새 장면ID로 따라간다(AutoIdFollower — 같은 되돌리기 칸).
        int moved = AutoIdFollower.RenameScene(_session.Editor, chapterId, oldId, newId);
        _inspectedSceneId = newId;
        _session.SetStatus(Plural.Format(Ui.Structure_SceneIDEpisodesSceneName, oldId, newId, moved));
    }


    /// <summary>
    /// 「조건 및 스탯」 탭의 <b>스탯 표</b> — 2026-09-17부터 <b>편집 가능</b>하다 (소유자: *"이제 엑셀이
    /// 아닌 챕터그래프에서 직접 조건과 스탯을 정의"*).
    ///
    /// ⚠ 전에는 읽기 전용이었고 *"편집은 챕터 엑셀의 `스탯` 시트에서"*라고 적혀 있었다.
    /// 그 문장은 <b>R-F(2026-09-16)가 이미 낡게 만들었다</b> — 그날 워크북이 산출물이 되고
    /// 값의 주인이 <c>ChapterDocument</c>로 넘어왔는데 안내만 남아 있었다.
    ///
    /// <b>키는 그 줄의 이름 칸에서 고친다</b>(2026-11-18 소유자 — 「다른 화자이름 같은것처럼 편하게」 · writer-comfort WC4). 키를 바꾸는 것은
    /// 간선·조건·픽스처·대사 글을 함께 끌고 가는 일이라 <see cref="ProjectEditor.RenameChapterStat"/>로 가고, 칸은 Enter와 칸을 떠날 때만 넣는다 —
    /// 글자를 고치는 사이의 중간 상태는 개명이 되지 않는다. 키 칸에 초점이 있는 동안 그 줄에 세이브 경고가 선다(적기 전에 읽는다).
    /// ⛔ 그 전에는 키가 읽기만 하는 글이고 [✎] → 플라이아웃으로만 바꿨다.
    ///
    /// 더하기는 표 아래에 늘 서 있는 줄이다 — 키 칸 + [＋ 추가](Enter도 같다). ⛔ [＋ 스탯] → 플라이아웃은 걷었다.
    ///
    /// ⛔ 픽스처 표는 2026-08-24에 걷었다 (소유자 — 한 번도 안 썼다).
    /// </summary>
    private void RefreshChapterSheets(ChapterGraphModel? snapshot)
    {
        ChapterGraphModel? model = LiveSheetModel ?? snapshot;

        StatListPanel.Children.Clear();

        foreach (ChapterStat stat in model?.Stats ?? Enumerable.Empty<ChapterStat>())
        {
            StatListPanel.Children.Add(StatRow(stat));
        }

        if (StatListPanel.Children.Count == 0)
        {
            StatListPanel.Children.Add(new GuideLine
            {
                Full = Ui.Structure_NoStatsTypeKeyIn,
                Brief = Ui.Structure_NoStats,
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.5)
            });
        }

        if (_selectedChapterId is { } chapterId)
        {
            StatListPanel.Children.Add(AddRow(
                "stat-add",
                Ui.Structure_NewStatKeyInEnglish,
                string.Format(Ui.Structure_EnterAlsoAddsKeyIs, StatKeyRule.Rule),
                Ui.Structure_AddStat,
                key =>
                {
                    // 영어로만 — 받지 않으면 까닭을 말하고 적은 글은 칸에 그대로 둔다(2026-11-23 소유자 · StatKeyRule).
                    if (StatKeyRule.Problem(key) is { } refused)
                    {
                        _session!.SetStatus(refused);
                        return;
                    }

                    _session!.Editor.AddChapterStat(chapterId, key);
                    _session.SetStatus(string.Format(Ui.Structure_AddedStatNumberInteger0, key, Josa.을를(key)));
                }));
        }
    }

    /// <summary>스탯 종류의 이름 — 작가가 읽는 말이다. 차례는 <see cref="ChapterStatType"/>의 Int · Bool과 같다.</summary>
    internal static readonly string[] StatTypeNames = [Ui.Structure_NumberInteger, Ui.Structure_OnOff2];   // ⛔ 「예/아니오」(usability UX12) · 「숫자」(2026-11-23 소유자 — 「그냥 숫자 대신에 숫자(정수)라고」)

    /// <summary>
    /// 스탯 하나 — 두 줄이다. 위는 무엇인가(키 칸 · 종류 · 삭제), 아래는 값(숫자면 처음 값 · 최소 · 최대, 예/아니오면
    /// 처음 값 하나). 값 칸마다 이름을 붙인다 — 이름 없는 숫자 칸 셋은 처음 보는 사람이 무엇인지 모른다. 한 줄에 다 세우면
    /// 인스펙터 폭을 넘어 뒤쪽 칸이 밀린다. 아래 줄의 숫자 칸은 남은 폭을 셋이 나눠 가진다 — 고정 폭이면 좁은 인스펙터에서
    /// 마지막 칸이 잘린다.
    /// </summary>
    private Control StatRow(ChapterStat stat)
    {
        bool isBool = stat.Type == ChapterStatType.Bool;
        var row = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 0, 6), Tag = $"stat:{stat.Key}" };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        string chapterId = _selectedChapterId!;

        TextBox key = NameBox(stat.Key, "stat-key", Ui.Structure_RenameStatKey, to =>
        {
            Vn.Authoring.Editing.StatRenameOutcome outcome = _session!.Editor.RenameChapterStat(chapterId, stat.Key, to);

            if (!outcome.Applied)
            {
                return outcome.Refusal ?? Ui.Side_CouldnTRenameIt;
            }

            _session.SetStatus(
                Plural.Format(Ui.Structure_StatRenamedFollowed, stat.Key, to, outcome.Edges, outcome.Conditions, outcome.Fixtures, outcome.DialogueLines));
            return null;
        });
        ToolTip.SetTip(key, string.Format(Ui.Structure_KeyKeyInSaveData, StatKeyRule.Rule));
        head.Children.Add(key);

        // 표시 이름 칸은 걷혔다 — 스탯은 키만 있다. 게임 화면의 글자는 게임 쪽이 키로 로컬라이즈한다(writer-fields W2).

        var type = new ComboBox
        {
            FontSize = Skin.Font(10),
            MinHeight = 0,
            Padding = new Thickness(8, 2, 4, 2),
            Margin = new Thickness(4, 0, 0, 0),
            ItemsSource = StatTypeNames,
            SelectedIndex = isBool ? 1 : 0,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "stat-type"
        };
        ToolTip.SetTip(
            type,
            Ui.Structure_TypeNumberIntegerValueThat);
        type.SelectionChanged += (_, _) => UiGuard.Run(_session, Ui.Structure_StatType, () =>
            _session!.Editor.UpdateChapterStat(
                _selectedChapterId!,
                stat.Key,
                type: type.SelectedIndex == 1 ? ChapterStatType.Bool : ChapterStatType.Int));
        Grid.SetColumn(type, 1);
        head.Children.Add(type);

        var remove = new Button { Content = "✕", FontSize = Skin.Font(10), Padding = new Thickness(5, 1), Margin = new Thickness(3, 0, 0, 0) };
        ToolTip.SetTip(remove, Ui.Structure_DeletesItIfItS);
        remove.Click += (_, _) => UiGuard.Run(_session, Ui.Structure_DeleteStat, () => RemoveStat(remove, stat.Key));
        Grid.SetColumn(remove, 2);
        head.Children.Add(remove);

        row.Children.Add(head);

        // ⚠ 세이브 경고 — 키 칸에 초점이 있는 동안만 선다. 적기 전에 읽는다(세이브는 되돌리기로 돌아오지 않는다 — 런타임 회신 §6.2).
        var caution = new TextBlock
        {
            Text = Ui.Structure_KeyIsKeyInSave,
            FontSize = Skin.Font(10),
            Foreground = UnlockConditionInk,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
            Tag = "stat-key-caution"
        };
        key.GotFocus += (_, _) => caution.IsVisible = true;
        key.LostFocus += (_, _) => caution.IsVisible = false;
        row.Children.Add(caution);

        var values = new Grid { Margin = new Thickness(12, 0, 0, 0) };

        void Add(Control cell)
        {
            Grid.SetColumn(cell, values.Children.Count);
            values.Children.Add(cell);
        }

        if (isBool)
        {
            values.ColumnDefinitions = new ColumnDefinitions("Auto,Auto");
            Add(ValueLabel(Ui.Structure_Initial));

            var initial = new ComboBox
            {
                FontSize = Skin.Font(10),
                MinHeight = 0,
                Padding = new Thickness(8, 2, 4, 2),
                ItemsSource = new[] { Ui.Side_Off2, Ui.Side_On2 },   // ⛔ 「아니오」 · 「예」(usability UX12)
                SelectedIndex = stat.Initial == 0 ? 0 : 1,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "stat-initial"
            };
            ToolTip.SetTip(initial, Ui.Structure_InitialWhetherThisStatIs);
            initial.SelectionChanged += (_, _) => UiGuard.Run(_session, Ui.Structure_Initial, () =>
                _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, initial: initial.SelectedIndex));
            Add(initial);
        }
        else
        {
            values.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto,*");
            Add(ValueLabel(Ui.Structure_Initial));
            Add(NumberBox(stat.Initial, Ui.Structure_Initial, Ui.Structure_InitialThisStatSValue,
                value => _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, initial: value)));
            Add(ValueLabel(Ui.Structure_Min));
            Add(NumberBox(stat.Minimum, Ui.Structure_Min, Ui.Structure_MinThisStatNeverGoes,
                value => _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, minimum: value)));
            Add(ValueLabel(Ui.Structure_Max));
            Add(NumberBox(stat.Maximum, Ui.Structure_Max, Ui.Structure_MaxThisStatNeverGoes,
                value => _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, maximum: value)));
        }

        row.Children.Add(values);
        return row;
    }

    /// <summary>값 칸 앞의 이름.</summary>
    private static TextBlock ValueLabel(string text) => new()
    {
        Text = text,
        FontSize = Skin.Font(10),
        Opacity = Skin.Dim(0.7),
        Margin = new Thickness(6, 0, 3, 0),
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>
    /// 「조건 및 스탯」 탭의 <b>조건 표</b> — 조건마다 <b>이름</b>과 <b>식</b>을 고치고 [✕]로 지운다(⛔ 2026-11-26까지 지우기는 [대본] 탭 · writer-fields W4).
    /// 더하기는 표 아래에 늘 서 있는 줄 — 이름 칸 + [＋ 추가](writer-comfort WC4 — ⛔ [＋ 조건] → 이름과 식을 함께 받는 플라이아웃은 걷었다 ·
    /// 그 전 side-tabs V2). 식이 빈 조건은 「식 미정」(항상 통과)이라 ⚠로 알린다.
    /// </summary>
    private void RefreshConditionList(ChapterGraphModel? snapshot)
    {
        RefreshChapterSheets(snapshot);

        ChapterGraphModel? model = LiveSheetModel ?? snapshot;

        // 챕터ID는 이 표와 같은 자리에서 다시 채운다 — 손으로 적는 중이면 덮지 않는다.
        if (!ChapterIdBox.IsFocused)
        {
            ChapterIdBox.Text = _selectedChapterId ?? string.Empty;
        }

        ConditionListPanel.Children.Clear();

        foreach (ChapterCondition condition in model?.Conditions ?? Enumerable.Empty<ChapterCondition>())
        {
            ConditionListPanel.Children.Add(ConditionRow(condition));
        }

        if (ConditionListPanel.Children.Count == 0)
        {
            ConditionListPanel.Children.Add(new GuideLine
            {
                Full = Ui.Structure_NoConditionsTypeNameIn,
                Brief = Ui.Structure_NoConditions,
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.5)
            });
        }

        // 더하기는 여기서도 한다 — 스탯 표의 더하기 줄과 같은 결이다(2026-11-18 소유자 · side-tabs V2 · writer-comfort WC4).
        if (_selectedChapterId is { } chapterId)
        {
            ConditionListPanel.Children.Add(AddRow(
                "condition-add",
                Ui.Structure_NewConditionNameByMeaning,
                Ui.Structure_EnterAlsoAddsExpressionStarts,
                Ui.Side_AddCondition,
                label =>
                {
                    _session!.Editor.AddChapterCondition(chapterId, label, string.Empty);
                    _session.SetStatus(string.Format(Ui.Structure_AddedConditionUndecidedAlwaysPasses, label, Josa.을를(label)));
                }));
        }
    }

    /// <summary>
    /// 조건 한 줄 — 이름 칸(고정 폭 <see cref="ConditionLabelWidth"/>) · 식 · [✕]. 식 칸은 남은 폭을 채운다.
    /// 이름은 그 자리에서 고친다 — 그 이름을 건 간선(표시조건 · 해금조건)이 따라간다(<see cref="ProjectEditor.RenameChapterCondition"/> ·
    /// writer-comfort WC4 — ⛔ 그 전에는 읽기만 했다). 칸에 다 안 들어가는 이름은 마우스를 올리면 다 보인다.
    /// [✕]는 스탯 줄의 것과 같다 — 아무 데서도 안 쓰면 지우고, 쓰면 어디인지 보이고 한 번 더 묻는다(<see cref="ShowConditionRemoveFlyout"/>).
    /// ⛔ 2026-11-26까지 지우기는 [대본] 탭의 일이었다(writer-fields W4) — 소유자: 「조건을 추가는 할 수 있는데, 지울 수 없다는 게 조금 아쉽고」.
    /// </summary>
    private Control ConditionRow(ChapterCondition condition)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{ConditionLabelWidth},*,Auto"),
            Tag = $"condition:{condition.Label}"
        };

        string chapterId = _selectedChapterId!;
        TextBox label = NameBox(condition.Label, "condition-label", Ui.Side_ConditionName, to =>
        {
            Vn.Authoring.Editing.ChapterConditionRenameOutcome outcome = _session!.Editor.RenameChapterCondition(chapterId, condition.Label, to);

            if (!outcome.Applied)
            {
                return outcome.Refusal ?? Ui.Side_CouldnTRenameIt;
            }

            _session.SetStatus(outcome.Edges == 0
                ? string.Format(Ui.Side_Condition, condition.Label, to)
                : Plural.Format(Ui.Side_ConditionPathsFollowed, condition.Label, to, outcome.Edges));
            return null;
        });
        bool undecided = string.IsNullOrWhiteSpace(condition.Expression);
        ToolTip.SetTip(label, condition.Label + Ui.Structure_EditAndPressEnterOr);

        if (undecided)
        {
            label.Foreground = UnlockConditionInk;   // 「아직 비었다」의 호박색 — 화자의 키 없음과 같다
        }

        row.Children.Add(label);

        TextBox expression = SheetBox(
            condition.Expression, double.NaN, Ui.Structure_ExpressionEGTrust3,
            value => _session!.Editor.UpdateChapterCondition(_selectedChapterId!, condition.Label, value));
        expression.Margin = new Thickness(3, 0, 0, 0);
        expression.PlaceholderText = Ui.Side_UndecidedAlwaysPasses;
        expression.Tag = "condition-expression:" + condition.Label;
        Grid.SetColumn(expression, 1);
        row.Children.Add(expression);

        var remove = new Button
        {
            Content = "✕",
            FontSize = Skin.Font(10),
            Padding = new Thickness(5, 1),
            Margin = new Thickness(3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "condition-remove:" + condition.Label
        };
        ToolTip.SetTip(remove, Ui.Structure_DeletesItIfItS);
        remove.Click += (_, _) => UiGuard.Run(_session, Ui.Side_DeleteCondition, () => RemoveCondition(remove, chapterId, condition.Label));
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);

        return row;
    }

    /// <summary>조건 [✕] — 안 쓰면 지우고(되돌리기 한 칸), 쓰면 그 자리들을 보이고 한 번 더 묻는다.</summary>
    private void RemoveCondition(Control anchor, string chapterId, string label)
    {
        Vn.Authoring.Editing.ChapterConditionRemoveOutcome outcome = _session!.Editor.RemoveChapterCondition(chapterId, label);

        if (outcome.Applied)
        {
            _session.SetStatus(string.Format(Ui.Side_DeletedConditionUndoCtrlZ, label));
            return;
        }

        if (outcome.Uses.Count == 0)
        {
            throw new InvalidOperationException(outcome.Refusal ?? Ui.Structure_CouldnTDeleteIt);
        }

        ShowConditionRemoveFlyout(anchor, _session, chapterId, label, outcome);
    }

    /// <summary>
    /// 쓰는 조건의 [✕] — 쓰는 곳을 <b>보여 주고</b>, 간선에서만 쓰면 「모두 떼고 지우기」를 내놓는다. [대본] · [구성] 두 탭의 조건 표가 같이 쓴다.
    /// 상태줄에도 같은 목록이 선다(그 전의 거절 문장 그대로).
    ///
    /// 떼기는 그 길의 표시조건 · 해금조건만 「없음」으로 한다 — 길과 잠금 안내문은 남는다. 스탯의 「모두 비우고 지우기」와 같은 결이다
    /// (<see cref="ShowClearAndDeleteFlyout"/>). 대사 줄의 갈래(조건 블록 · 분기 카드 첫머리)가 쓰면 단추가 없다 — 툴이 대사의 조건을 걷으면
    /// 작가가 쓴 글의 모양이 바뀐다. 그 자리를 먼저 걷으라고 말한다.
    /// </summary>
    internal static void ShowConditionRemoveFlyout(
        Control anchor,
        AuthoringSession session,
        string chapterId,
        string label,
        Vn.Authoring.Editing.ChapterConditionRemoveOutcome refused)
    {
        session.SetStatus(Explain(refused.Refusal, refused.Uses));

        bool edgesOnly = refused.Uses.All(use => use.Kind == Vn.Authoring.Editing.ChapterUseKind.Edge);
        var panel = new StackPanel { Spacing = 4, MaxWidth = 420, Tag = ConditionRemovePanelTag };

        panel.Children.Add(new TextBlock
        {
            Text = refused.Refusal ?? string.Empty,
            FontSize = Skin.Font(11),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (Vn.Authoring.Editing.ChapterUse use in refused.Uses)
        {
            panel.Children.Add(new SelectableTextBlock
            {
                Text = $"· {use.Where} — {use.Detail}",
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.75),
                TextWrapping = TextWrapping.Wrap
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = edgesOnly ? Ui.Structure_DetachingSetsJustThat : Ui.Structure_ClearDialogueConditionsFirst,
            FontSize = Skin.Font(10),
            Opacity = Skin.Dim(0.6),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var flyout = new Flyout { Content = panel };

        if (edgesOnly)
        {
            var detach = new Button
            {
                Content = Ui.Structure_DetachAllAndDelete,
                FontSize = Skin.Font(10),
                Padding = new Thickness(7, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = "condition-detach-delete"
            };

            detach.Click += (_, _) =>
            {
                flyout.Hide();

                UiGuard.Run(session, Ui.Structure_DetachAndDeleteCondition, () =>
                {
                    Vn.Authoring.Editing.ChapterConditionRemoveOutcome done =
                        session.Editor.RemoveChapterCondition(chapterId, label, clearEdges: true);

                    if (!done.Applied)
                    {
                        throw new InvalidOperationException(Explain(done.Refusal, done.Uses));
                    }

                    session.SetStatus(Plural.Format(Ui.Structure_DeletedConditionDetachedPaths, label, Josa.을를(label), done.EdgesCleared));
                });
            };

            panel.Children.Add(detach);
        }

        flyout.ShowAt(anchor);
    }

    /// <summary>조건 [✕]의 확인 플라이아웃 — 화면 시험이 찾는다.</summary>
    internal const string ConditionRemovePanelTag = "condition-remove-panel";

    /// <summary>조건 표의 이름 칸 폭 — 「성실 3 이상 · 요령 3 미만」쯤은 다 보인다. 더 길면 칸 안에서 흐르고 손을 올리면 다 보인다.</summary>
    internal const double ConditionLabelWidth = 150;

    // ── 스탯·조건 편집의 잔손 ───────────────────────────────────────────────

    /// <summary>
    /// 표 안의 글자 칸. <b>초점을 잃을 때 저장한다</b> — 자판마다 쓰면 되돌리기가 글자 수만큼
    /// 쌓이고 다시 읽기가 매번 돈다(옛 엑셀노드 대사 칸이 배운 것과 같다). Enter도 확정이다.
    /// </summary>
    private TextBox SheetBox(string value, double width, string hint, Action<string> commit)
    {
        var box = new TextBox
        {
            Text = value,
            FontSize = Skin.Font(10),
            Width = width,
            MinHeight = 0,
            Padding = new Thickness(4, 1),
            VerticalAlignment = VerticalAlignment.Center
        };

        ToolTip.SetTip(box, hint);

        void Commit()
        {
            if (string.Equals(box.Text ?? string.Empty, value, StringComparison.Ordinal))
            {
                return;
            }

            UiGuard.Run(_session, hint, () => commit(box.Text ?? string.Empty));
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) => CommitOnEnter(args, Commit);
        EscToRevert.Wire(box, () => value);

        return box;
    }

    /// <summary>
    /// 숫자 칸 — 못 읽는 글자는 <b>조용히 버리지 않고</b> 옛 값으로 되돌린다. 폭은 놓인 칸을 채운다 — 테마의 글자 칸 최소 폭이
    /// 셋이 나란히 서기에는 넓어 걷는다.
    /// </summary>
    /// <param name="hint">칸의 이름 — 못 읽을 때 말머리가 된다.</param>
    /// <param name="tip">칸에 마우스를 올렸을 때의 풀이.</param>
    private TextBox NumberBox(int value, string hint, string tip, Action<int> commit)
    {
        TextBox box = SheetBox(
            value.ToString(System.Globalization.CultureInfo.InvariantCulture), double.NaN, hint,
            text =>
            {
                if (!int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed))
                {
                    throw new InvalidOperationException(string.Format(Ui.Structure_TakesNumberIntegerCanT, hint, text));
                }

                commit(parsed);
            });

        box.MinWidth = 0;
        ToolTip.SetTip(box, tip);
        return box;
    }

    /// <summary>
    /// 표 줄의 <b>이름 칸</b> — 스탯 키 · 조건 이름을 그 자리에서 고친다(writer-comfort WC4 — [대본] 탭의 화자 · 조건 이름 칸과 같은 손버릇).
    /// Enter와 칸을 떠날 때 <paramref name="rename"/>으로 넣고(참조가 따라간다 · 되돌리기 한 칸), Esc는 그만둔다. 같은 이름이면 넣지 않는다.
    /// 거절(빈 이름 · 이미 있는 이름 …)이면 옛 이름으로 돌리고 상태줄이 까닭을 말한다.
    /// </summary>
    /// <param name="rename">새 이름으로 바꾼다 — 거절이면 그 까닭, 바꿨으면 null.</param>
    private TextBox NameBox(string name, string tag, string action, Func<string, string?> rename)
    {
        var box = new TextBox
        {
            Text = name,
            FontSize = Skin.Font(10),
            FontWeight = FontWeight.SemiBold,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(4, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = tag + ":" + name
        };

        void Commit()
        {
            string typed = (box.Text ?? string.Empty).Trim();

            if (string.Equals(typed, name, StringComparison.Ordinal))
            {
                box.Text = name;   // 앞뒤 공백만 걷었다
                return;
            }

            UiGuard.Run(_session, action, () =>
            {
                if (rename(typed) is { } refusal)
                {
                    box.Text = name;
                    _session!.SetStatus(refusal);
                }
            });
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) => CommitOnEnter(args, Commit);
        EscToRevert.Wire(box, () => name);

        return box;
    }

    /// <summary>
    /// 표 아래에 <b>늘 서 있는 더하기 줄</b> — 이름 칸 + 그 오른쪽 [＋ 추가](Enter도 같다). [대본] 탭의 화자 · 조건 더하기 줄과 같은 모양이다
    /// (2026-11-18 소유자 — 「ID를 쓰는 공간이 항상 있고, 그 오른쪽에 '+추가'를 눌러서」 · writer-comfort WC4). 빈 이름은 받지 않는다.
    /// 더한 뒤에는 다시 선 표의 같은 칸에 초점을 준다 — 잇달아 더한다.
    /// </summary>
    /// <param name="tag">단추의 Tag — 칸은 「Tag-name」이다.</param>
    private Grid AddRow(string tag, string placeholder, string tip, string action, Action<string> add)
    {
        var box = new TextBox
        {
            FontSize = Skin.Font(10),
            MinHeight = 0,
            Padding = new Thickness(4, 2),
            PlaceholderText = placeholder,
            Tag = tag + "-name"
        };
        var button = new Button
        {
            Content = Ui.Speakers_Add,
            FontSize = Skin.Font(10),
            Padding = new Thickness(7, 2),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = tag
        };
        ToolTip.SetTip(button, tip);

        void Add()
        {
            string typed = (box.Text ?? string.Empty).Trim();

            if (typed.Length == 0)
            {
                _session?.SetStatus(Ui.Structure_TypeName);
                return;
            }

            if (UiGuard.Run(_session, action, () => add(typed)))
            {
                // 표는 넣기가 부른 다시 그리기에서 다시 선다 — 그 뒤에 새 칸에 초점을 준다.
                Dispatcher.UIThread.Post(() => ConditionSection.GetLogicalDescendants().OfType<TextBox>()
                    .FirstOrDefault(item => Equals(item.Tag, box.Tag))?.Focus());
            }
        }

        button.Click += (_, _) => Add();
        box.KeyDown += (_, args) => CommitOnEnter(args, Add);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 4, 0, 0) };
        Grid.SetColumn(button, 1);
        row.Children.Add(box);
        row.Children.Add(button);

        return row;
    }

    /// <summary>
    /// 스탯을 지운다 — 거절이 <b>어디서 쓰는지</b>를 들고 온다. 그 목록이 곧 정리 안내다.
    /// </summary>
    private void RemoveStat(Control anchor, string key)
    {
        Vn.Authoring.Editing.StatRemoveOutcome outcome = _session!.Editor.RemoveChapterStat(_selectedChapterId!, key);

        if (outcome.Applied)
        {
            _session.SetStatus(string.Format(Ui.Structure_DeletedStat, key));
            Draw();

            return;
        }

        if (outcome.Uses.Count == 0)
        {
            throw new InvalidOperationException(outcome.Refusal ?? Ui.Structure_CouldnTDeleteIt);
        }

        // ⭐ <b>한 번 더 묻는다</b> (2026-09-17 소유자 — 「가부터 만들고 다로 확장하자」).
        //    거절이 <b>목록을 들고 오므로</b> 그 목록이 곧 확인 화면이 된다: 무엇이 바뀔지
        //    보고 나서 누르는 것과, 경고만 읽고 누르는 것은 다른 일이다.
        ShowClearAndDeleteFlyout(anchor, key, outcome);
    }

    /// <summary>
    /// 쓰는 곳을 <b>보여 주고</b> 「모두 비우고 지우기」를 내놓는다.
    ///
    /// ⚠ 무엇이 어떻게 바뀌는지 글로 적는다 — 간선은 <b>그 항만</b> 빠지고, 조건은
    /// <b>식만 비고 남는다</b>. 조건을 통째로 지우면 그것을 쓰는 간선의 표시/해금이 함께
    /// 풀려 <b>연쇄가 한 단계 더</b> 간다(2026-09-17 소유자).
    /// </summary>
    private void ShowClearAndDeleteFlyout(
        Control anchor, string key, Vn.Authoring.Editing.StatRemoveOutcome refused)
    {
        var panel = new StackPanel { Spacing = 4, MaxWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = refused.Refusal ?? string.Empty,
            FontSize = Skin.Font(11),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (Vn.Authoring.Editing.ChapterUse use in refused.Uses)
        {
            panel.Children.Add(new SelectableTextBlock
            {
                Text = $"· {use.Where} — {use.Detail}",
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.75),
                TextWrapping = TextWrapping.Wrap
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = Ui.Structure_ClearingRemovesJustThatTerm +
                   (refused.Uses.Any(use => use.Kind == Vn.Authoring.Editing.ChapterUseKind.DialogueRead)
                       ? string.Format(Ui.Structure_StatInDialogueIsnT, key)
                       : string.Empty),
            FontSize = Skin.Font(10),
            Opacity = Skin.Dim(0.6),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var clear = new Button
        {
            Content = Ui.Structure_ClearAllAndDelete,
            FontSize = Skin.Font(10),
            Padding = new Thickness(7, 2),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var flyout = new Flyout { Content = panel };

        clear.Click += (_, _) =>
        {
            flyout.Hide();

            UiGuard.Run(_session, Ui.Structure_ClearAndDeleteStat, () =>
            {
                Vn.Authoring.Editing.StatRemoveOutcome done =
                    _session!.Editor.RemoveChapterStat(_selectedChapterId!, key, clearUses: true);

                if (!done.Applied)
                {
                    throw new InvalidOperationException(done.Refusal ?? Ui.Structure_CouldnTDeleteIt);
                }

                _session.SetStatus(
                    Plural.Format(Ui.Structure_DeletedStatRemovedItsTerm, key, done.EdgesCleared, done.ConditionsCleared) +
                    (done.DialogueReadsLeft > 0
                        ? string.Format(Ui.Structure_DialogueLinesReadingThisStat, done.DialogueReadsLeft)
                        : string.Empty));

                Draw();
            });
        };

        panel.Children.Add(clear);
        flyout.ShowAt(anchor);
    }

    /// <summary>
    /// 거절 사유 + <b>쓰는 자리 목록</b>. ⛔ *"쓰이고 있습니다"*로 끝내면 사람이 챕터를
    /// 뒤져야 한다 — 어디인지 말해야 거절이 정리 안내가 된다.
    /// </summary>
    internal static string Explain(string? refusal, IReadOnlyList<Vn.Authoring.Editing.ChapterUse> uses)
    {
        if (uses.Count == 0)
        {
            return refusal ?? Ui.Structure_CouldnTDeleteIt;
        }

        return refusal + "  " + string.Join(" / ", uses.Select(use => $"{use.Where} — {use.Detail}"));
    }

    /// <summary>
    /// 속성 패널의 [적용] — 에피소드 개명.
    ///
    /// ⛔ <b>규율은 여기 없다</b> — <see cref="EpisodeRenamer"/>가 갖는다 (2026-09-16에
    /// 부르는 자리가 둘이 되면서 한 벌로 모았다). 여기 남은 것은
    /// <b>이 화면이 그 뒤에 할 일</b>뿐이다: 선택을 새 이름으로 옮긴다.
    /// </summary>
    internal void RenameSelectedEpisode()
    {
        string oldId = _selectedEpisodeId ?? string.Empty;
        string newId = IdBox.Text?.Trim() ?? string.Empty;

        if (_session is null || _selectedChapterId is not { } chapterId)
        {
            return;
        }

        EpisodeRenamer.Result result =
            EpisodeRenamer.Rename(_session.Editor, chapterId, oldId, newId);

        if (!result.Renamed)
        {
            // "바뀐 이름이 없습니다"는 사람이 아무것도 안 한 것이라 말할 것이 없다.
            if (!string.Equals(oldId, newId, StringComparison.Ordinal) && oldId.Length > 0)
            {
                _session.SetStatus(result.Failure!);
            }

            // 받지 않았으면 칸이 지금 값으로 돌아온다 — 다른 곳을 눌러 낸 경우 칸에 남은 글이 바뀐 것처럼 보이면 안 된다.
            if (oldId.Length > 0)
            {
                IdBox.Text = oldId;
            }

            return;
        }

        _selectedEpisodeId = newId;
        _episodeRenamed = (oldId, newId);   // 고르기 첫머리에서 낸 것이면, 그 고르기가 옛 ID를 새 ID로 읽는다

        _session.SetStatus(
            string.Format(Ui.Structure_RenamedPathsTestStartsDialogue, oldId, newId));
    }

    /// <summary>
    /// <b>챕터 편집 하나</b> — 프로젝트를 고치고 상태줄에 말한다 (R-F · 지시서 §4.1).
    ///
    /// ⚠ <b>다시 그리라고 여기서 말하지 않는다.</b> 편집이 프로젝트를 바꿨으므로 세션의
    /// 변경 알림이 <see cref="QueueReload"/>를 부른다 — 쓴 자리가 곧 아는 자리다.
    ///
    /// ⚠ 고치기가 던지면(같은 Id·없는 간선·못 읽는 스탯변화) 부르는 쪽의
    /// <c>UiGuard</c>가 받아 상태줄에 말한다.
    ///
    /// (2026-10-29 · D2: 그 뒤에 따라 하던 챕터 워크북 재출력은 엑셀 연동과 함께 걷혔다.)
    /// </summary>
    private void ChapterEdit(Action change, string success)
    {
        change();
        _session?.SetStatus(success);
    }

    /// <summary>간선 하나를 가리키는 표식. 화면 검증이 이 이름으로 간선을 찾는다.</summary>
    internal static string EdgeTag(ChapterEdge edge) =>
        $"{edge.FromEpisodeId}→{edge.ToEpisodeId}" +
        (edge.HasNoOptionLabel ? string.Empty : $" [{edge.OptionLabel}]");

    /// <summary>그 에피소드로 들어오는 길 가운데 관문이 걸린 것들 (v8 — 관문은 길의 것).</summary>
    private static List<ChapterEdge> GatedIncoming(ChapterGraphModel model, ChapterEpisode episode) =>
        model.Edges
            .Where(edge =>
                string.Equals(edge.ToEpisodeId, episode.EpisodeId, StringComparison.Ordinal) &&
                edge.HasGate)
            .ToList();

    private static string GateSummary(IReadOnlyList<ChapterEdge> gated) => string.Join("\n",
        gated.Select(edge => string.Join(" · ", new[]
        {
            $"{edge.FromEpisodeId} →",
            edge.VisibleConditionLabel is null ? null : string.Format(Ui.Structure_Show, edge.VisibleConditionLabel),
            edge.ConditionLabel is null ? null : string.Format(Ui.Structure_Unlock, edge.ConditionLabel)
        }.Where(part => part is not null))));

    private static string Tooltip(ChapterGraphModel model, ChapterEpisode episode)
    {
        var lines = new List<string>
        {
            episode.EpisodeId,
            string.Format(Ui.Structure_PositionXY, episode.X, episode.Y),
            Plural.Format(Ui.Structure_Choices3, model.Edges.Count(edge => string.Equals(edge.FromEpisodeId, episode.EpisodeId, StringComparison.Ordinal)))
        };

        string gate = GateSummary(GatedIncoming(model, episode));

        if (gate.Length > 0)
        {
            lines.Add(Ui.Structure_GateOfIncomingPaths);
            lines.Add(gate);
        }

        if (!string.IsNullOrWhiteSpace(episode.Memo))
        {
            lines.Add(string.Format(Ui.Structure_Note, episode.Memo));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 머리글의 오류 표식 — <b>빨강</b> (2026-08-24 소유자: "관례대로 오류를 빨강,
    /// 경고를 노랑으로").
    ///
    /// 색을 <b>이모지로</b> 내는 이유: 머리글이 문자열 하나라 글자마다 색을 줄 자리가
    /// 없다. 표식을 글에 심으면 그 한 줄이 색을 갖는다.
    ///
    /// ⛔ <b>이 표식이 유일한 알림 창구다.</b> 같은 날 자동 펼침을 껐으므로(사람이 접어 둔
    /// 것을 저장할 때마다 다시 열던 규칙), 여기서 표식을 빼면 오류가 조용해진다.
    /// </summary>
    private const string ErrorMark = "🔴";

    /// <summary>머리글의 경고 표식 — <b>노랑</b>. 위 ⛔ 참조.</summary>
    private const string WarningMark = "🟡";

    private void DrawDiagnostics(ChapterGraphModel model)
    {
        // 모델 진단(구조 검사 포함) + 검증기(구조·교차·도달성) 진단을 한 목록으로 본다.
        // 같은 진단이 두 곳에서 나올 수 있으므로(모델이 낸 것을 검증기가 다시 담는다) 겹은 지운다.
        // 막힌 챕터들의 오류도 더한다(2026-11-18 소유자 — 「진행 JSON 미출력인데 보고할 것이 없다」) — 결론은 전 챕터의 내보내기에서 오므로, 다른
        // 챕터가 막혔거나 진행 코어가 거부한 까닭(검증 캐시에는 없다)이 여기 없으면 「아래 오류」가 아래에 없다. 줄마다 챕터 ID가 앞에 선다.
        List<ChapterDiagnostic> all = model.Diagnostics
            .Concat(_validation?.All ?? Enumerable.Empty<ChapterDiagnostic>())
            .Concat(_exportRun.RefusalErrors ?? [])
            .Distinct()
            .ToList();

        int errors = all.Count(item => item.Severity == ChapterDiagnosticSeverity.Error);
        int warnings = all.Count(item => item.Severity == ChapterDiagnosticSeverity.Warning);

        string? exportNotice = ExportNotice();

        var header = new System.Text.StringBuilder(Ui.Structure_ValidationReport);

        if (errors > 0)
        {
            header.Append(string.Format(Ui.Structure_Errors, ErrorMark, errors));
        }

        if (warnings > 0)
        {
            header.Append(string.Format(Ui.Structure_Warnings, WarningMark, warnings));
        }

        if (exportNotice is not null)
        {
            // 접혀 있어도 보여야 한다 — 런타임으로 나갈 것이 안 나간 상태다.
            header.Append(string.Format(Ui.Structure_ProgressionJSONNotWritten, ErrorMark));
        }

        if (header.Length == Ui.Structure_ValidationReport.Length)
        {
            header.Append(Plural.Format(Ui.Structure_NoErrorsNotices, all.Count));
        }

        DiagnosticsExpander.Header = header.ToString();

        // ⛔ <b>저절로 펼치지 않는다</b> (2026-08-24 소유자: "오류가 있으면 검증 보고가
        // 저절로 펼쳐지는 규칙이 있는데, 그것까지 꺼줘. 대신에 … 시각적인 이모티콘을
        // 붙여놓기만 해").
        //
        // 예전에는 여기서 <c>IsExpanded</c>를 <b>양방향으로</b> 밀었다 — 오류가 있으면
        // 열고, 없으면 닫았다. 그래서 사람이 접어 둔 것을 다시 열고, 펼쳐 둔 것을 다시
        // 닫았다. 다시 그리기는 저장할 때마다 도니까 그 싸움이 계속됐다.
        // 이제 그 칸은 <b>사람만 만진다.</b> 알릴 것은 머리글의 표식이 든다
        // (🔴 오류 · 🟡 경고 — <see cref="ErrorMark"/>).

        // 내보내기 결론이 맨 위다 — 아래 오류들이 그 사유이므로 결론과 근거가 붙어 선다.
        if (exportNotice is not null)
        {
            DiagnosticsPanel.Children.Add(
                DiagnosticLine(exportNotice, Skin.DangerText.Brush(), dim: false, bold: true));
        }

        // 나간 자리 — 늘 보인다 (2026-11-04 소유자: "내보내기를 어떻게 하지?"). 자동이되 말한다.
        if (_exportRun.WhereNotice is { } where)
        {
            DiagnosticsPanel.Children.Add(DiagnosticLine(where, null, dim: false));
        }
        else if (_session?.ProjectPath is not null && _exportRun.Notice is null)
        {
            DiagnosticsPanel.Children.Add(DiagnosticLine(
                Ui.Structure_ProgressionJSONNoChapterWritten, null, dim: true));
        }

        // 서버는 이 문자열이 아니라 **파일 바이트**를 해시한다. 내보내기와 같은
        // ChapterExportBytes 경로에서 계산한 값을 보여 줘야 서버 버전 목록과 대조할 수 있다.
        if (_selectedChapterId is { } selected &&
            _exportRun.Checksums?.TryGetValue(selected, out string? checksum) == true)
        {
            DiagnosticsPanel.Children.Add(DiagnosticLine(
                string.Format(Ui.Structure_ProgressionJSONSHA256, checksum), null, dim: true));
        }

        // 탐색이 상한에서 멈췄으면 "도달 불가"가 단정이 아니라는 사실을 먼저 말한다.
        //
        // ⚠ <b>문장은 여기서 쓰지 않는다</b> (2026-09-17). 전에는 화면이 제 문구를 들고
        //    있었는데, 같은 사실을 모델도 말하기 시작하면 두 문구가 갈린다 — 그리고 화면
        //    밖(내보내기 보고·기록)에서는 <b>이 사실이 아예 안 보였다</b>. 이제 증명기가
        //    진단으로 내고(센 상태 수까지), 화면은 그것을 <b>눈에 띄게 올리는</b> 일만 한다.
        if (all.FirstOrDefault(item =>
                item.Code == ChapterDiagnosticCode.ReachabilityExplorationIncomplete)
            is { } incomplete)
        {
            DiagnosticsPanel.Children.Add(DiagnosticLine(
                incomplete.Message, Skin.WarningText.Brush(), dim: false, bold: true));
        }

        // ⚠ 세는 것은 <b>말할 것이 있는</b> 보고뿐이다 (2026-08-24). 예전에는 보고가
        // 하나라도 있으면 여기를 지나쳤는데, 이제 대부분의 보고가 아무 줄도 내지 않으므로
        // 그대로 두면 <b>텅 빈 상자</b>가 선다("보고할 것이 없습니다"조차 없이).
        if (all.Count == 0 &&
            !(_selectedChapterId is { } id && _exportRun.Checksums?.ContainsKey(id) == true))
        {
            DiagnosticsPanel.Children.Add(new TextBlock
            {
                // 빈 챕터는 낼 것이 없을 뿐이다 — 막힌 것이 아니라고 말한다(2026-11-18).
                Text = model.Episodes.Count == 0
                    ? Ui.Structure_NoEpisodesYetOnceYou
                    : Ui.Structure_NothingToReport,
                FontSize = Skin.Font(11),
                Opacity = Skin.Dim(0.55)
            });

            return;
        }

        foreach (ChapterDiagnostic diagnostic in all
                     .OrderByDescending(item => item.Severity)
                     .ThenBy(item => item.Sheet, StringComparer.Ordinal))
        {
            DiagnosticsPanel.Children.Add(DiagnosticLine(
                diagnostic.Describe(),
                diagnostic.Severity switch
                {
                    ChapterDiagnosticSeverity.Error => Skin.DangerText.Brush(),
                    ChapterDiagnosticSeverity.Warning => Skin.WarningText.Brush(),
                    _ => null
                },
                dim: diagnostic.Severity == ChapterDiagnosticSeverity.Info));

            if (diagnostic.Help is { } help)
            {
                DiagnosticsPanel.Children.Add(DiagnosticHelp(help));
            }
        }
    }

    /// <summary>
    /// 진단 아래의 고치는 법(<see cref="ChapterDiagnostic.Help"/>) — 연한 바탕의 상자에 고정폭 글꼴로, 예시의 식이 한 칸으로 줄을 선다(2026-11-23 소유자 —
    /// 「구체적인 예시까지 보기좋게 설명」). 고를 수 있고 [보고 복사]에도 든다.
    /// </summary>
    private static Border DiagnosticHelp(string help) => ExamplesBox(help, new Thickness(14, 1, 0, 6), DiagnosticHelpTag);

    /// <summary>
    /// 식 예시 상자 — 검증 보고의 고치는 법과 「식 예시 보기」 풍선이 같이 쓴다. 고정폭 글꼴이라 예시의 식이 한 칸으로 줄을 선다.
    /// </summary>
    private static Border ExamplesBox(string text, Thickness margin, string? tag = null) => new()
    {
        Tag = tag,
        Margin = margin,
        Padding = new Thickness(8, 5),
        CornerRadius = new CornerRadius(4),
        BorderThickness = new Thickness(2, 0, 0, 0),
        BorderBrush = Skin.NoteBorder.Brush(),
        Background = Skin.Hairline.Brush(0x14),
        Child = new SelectableTextBlock
        {
            Text = text,
            FontSize = Skin.Font(11),
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace"),
            TextWrapping = TextWrapping.Wrap,
            FontFeatures = AsTyped
        }
    };

    /// <summary>고치는 법 상자가 지는 표 — 화면 시험이 찾는다.</summary>
    internal const string DiagnosticHelpTag = "diagnostic-help";

    /// <summary>
    /// 보고를 텍스트 그대로 클립보드에 — 협업자나 세션에 붙여넣는 통로. 화면에 그려진
    /// 줄이 곧 복사되는 줄이다(별도 조립 없음 — 두 벌이면 어긋난다).
    /// </summary>
    private async Task CopyDiagnosticsAsync()
    {
        var lines = new List<string>();

        if (DiagnosticsExpander.Header is string header)
        {
            lines.Add(header);
        }

        lines.AddRange(DiagnosticsPanel.Children
            .Select(child => child switch
            {
                TextBlock block => block.Text,
                Border { Child: TextBlock help } => help.Text,   // 고치는 법 상자
                _ => null
            } ?? string.Empty)
            .Where(text => text.Length > 0));

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
        {
            _session?.SetStatus(Ui.Structure_CanTAccessClipboard);
            return;
        }

        await clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
        _session?.SetStatus(Plural.Format(Ui.Structure_CopiedLinesOfValidationReport, lines.Count));
    }

    // SelectableTextBlock — [보고 복사]가 전체를 들고 가고, 드래그는 한 줄만 집어 간다.
    private static TextBlock DiagnosticLine(
        string text, IBrush? foreground, bool dim, bool bold = false) => new SelectableTextBlock
    {
        Text = text,
        FontSize = Skin.Font(11),
        TextWrapping = TextWrapping.Wrap,
        Foreground = foreground,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        Opacity = dim ? 0.6 : 1,
        FontFeatures = AsTyped
    };

    /// <summary>
    /// 적은 글자 그대로 — 글꼴의 합자를 끈다. Inter는 「=&gt;」를 「⇒」로 그려서, 잘못 적은 식을 짚는 보고가 사람이 적지 않은 글자를 보였다(2026-11-23).
    /// </summary>
    private static readonly FontFeatureCollection AsTyped = [FontFeature.Parse("-calt"), FontFeature.Parse("-liga")];
}
