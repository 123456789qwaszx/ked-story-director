using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;
using Vn.App.Strings;
using Vn.Authoring.Strings;

namespace Vn.App;

/// <summary>
/// 앱 셸. 세션을 하나 만들고 그래프 화면과 노드 화면에 같은 것을 물려준다.
///
/// 두 화면은 서로를 모른다. 둘 다 <see cref="AuthoringSession"/>만 보고 있고,
/// 편집은 전부 <see cref="ProjectEditor"/>를 지난다. 그래서 여기에는 화면을 잇는 코드가 없다.
/// </summary>
public partial class MainWindow : Window
{
    private static string BaseTitle => AppIdentity.TitleName;   // 그때그때 — 정적 필드로 두면 화면 언어를 정하기 전에 지어질 수 있다(시험 격리 · 2026-11-25)

    private readonly AuthoringSession _session = new();

    /// <summary>검증용 손잡이 — 창이 쓰는 세션 하나. 테스트가 판을 짓고 선택을 옮긴다.</summary>
    internal AuthoringSession SessionProbe => _session;

    /// <summary>[⚙ 설정] — 설정 창을 모달로 연다(<see cref="SettingsWindow"/>). 시험이 연 창을 잡도록 돌려준다.</summary>
    /// <summary>
    /// 고른 탭의 판만 보인다 — 두 판은 늘 화면 나무에 붙어 있고 보이기만 바꾼다(2026-11-19 · usability UX14). ⛔ 판이 탭 안에 들어 있던 때는
    /// 탭을 바꾸면 안 보이는 판이 떨어졌다 다시 붙어, [대본] 탭으로 돌아오는 데 카드 160장에서 1초가 넘었다.
    /// </summary>
    private void ShowTab()
    {
        bool graph = ReferenceEquals(MainTabs.SelectedItem, GraphTabItem);
        ChapterGraph.IsVisible = !graph;
        Graph.IsVisible = graph;
    }

    internal SettingsWindow OpenSettings()
    {
        var settings = new SettingsWindow(_session);

        // 처음 세로가 이 창이 선 화면을 넘지 않게(UX1) — 화면을 모르면(헤드리스) 내용에 맞춘 그대로다.
        if (Screens.ScreenFromWindow(this) is { } screen)
        {
            settings.FitScreen(screen.WorkingArea.Height / screen.Scaling);
        }

        _ = settings.ShowDialog(this);
        return settings;
    }

    // ── 연출 그래프의 곁기둥 ──
    //
    // 주의: XAML이 아니라 여기서 만든다. 자리는 연출 그래프의 것이지만(SetSidePanel) 배선은
    // 셸의 것이다 — 이 편집기는 세션·선택 전환과 얽혀 있어 뷰로 옮기면 그 배선이 두 벌이
    // 된다. 필드 이름을 XAML 컨트롤처럼 지은 것은 의도다.
    private readonly DialogueNodeEditor DialogueEditor = new();

    private readonly TextBlock EmptyText = new()
    {
        Text = Ui.Main_SelectNodeToEditIt,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = Skin.Dim(0.5)
    };

    private readonly DispatcherTimer _autoSaveTimer;
    private bool _restoredRecent;
    /// <summary>하이어아키의 챕터 데이터. 원천은 [구성] 판(ChapterGraphView)의 다시 읽기 하나다.</summary>
    private IReadOnlyList<ChapterEntry> _chapters = Array.Empty<ChapterEntry>();

    public MainWindow()
    {
        InitializeComponent();

        // 탭 줄은 머리만 — 고른 탭의 판만 보인다. 두 판은 늘 화면 나무에 붙어 있다(usability UX14).
        MainTabs.SelectionChanged += (_, _) => ShowTab();
        ShowTab();
        _sheet = DirectorSummary;   // 요약 조각은 보통 인스펙터에 선다 — 화자 · 조건 탭은 짓는 동안만(FillSheet)

        // 마지막 안전망 (공통 불변식 4). 개별 핸들러의 포획을 놓친 예외가 여기까지 오면
        // 로그와 상태줄에 남기고 앱은 계속 산다 — 클릭 하나가 미저장 원고를 끝내지 않는다.
        // 개별 경로의 포획을 대신하는 장치가 아니라, 새 코드가 실수했을 때의 바닥이다.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Report(_session, Ui.Main_ScreenAction, e.Exception);
        };

        // 곁기둥을 제 화면에 건다 — Attach보다 먼저여야 첫 그리기부터 자리가 서 있다.
        Graph.SetSidePanel(BuildSidePanel());
        Graph.SetSpeakerPanel(TabScroll(DirectorSpeakers, "director-speakers-scroll"));
        Graph.SetConditionPanel(TabScroll(DirectorConditions, "director-conditions-scroll"));

        Graph.Attach(_session);
        // 하이어아키 · 콤보의 챕터 목록은 [구성] 판이 다시 읽은 목록 하나다 — 두 곳이 따로 읽지 않는다.
        // Attach 전에 구독해야 첫 읽기부터 받는다.
        ChapterGraph.EntriesReloaded += entries =>
        {
            _chapters = entries;
            RebuildFileList();
            Graph.SupplyChapters(entries);   // [대본] 툴바의 챕터 콤보 · 빈 판의 [＋ 첫 챕터 만들기]
            FillConditionTab();   // 스탯 표는 이 목록의 모델을 본다
        };
        // [구성] 툴바 콤보로 골라도 하이어아키의 강조가 따라온다 — 하이어아키에서 고를 때 하는 일(판 활성 + 다시 그리기)을 여기서도 한다.
        ChapterGraph.ChapterSelected += chapterId => UiGuard.Run(_session, Ui.Main_SelectChapter, () =>
        {
            _session.SelectFile(_session.EnsureChapterBoard(chapterId));
            RebuildFileList();
        });
        ChapterGraph.Attach(_session);
        DialogueEditor.Attach(_session);

        // 인스펙터의 「빈 선택지 숨기기」는 하이어아키에서 고른 줄들에 든 에피소드 모두에 한 번에(2026-11-23 소유자) — 고른 줄이 바뀌면 체크를 맞춘다.
        DialogueEditor.BatchCards = Graph.PickedCards;
        Graph.HierarchyView.SelectionsChanged += () => UiGuard.Run(_session, Ui.Main_ChoiceSlots, DialogueEditor.RefreshHideEmpty);
        _session.Changed += OnSessionChanged;
        _session.SpeakersChanged += OnSpeakersChanged;
        // 상태줄만 바뀐 경우는 셸 갱신 하나로 끝난다 — 판을 다시 그리거나
        // 챕터를 다시 읽을 이유가 없다. 그 구분이 없으면 노드가 늘수록 느려진다.
        _session.StatusChanged += (_, _) =>
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                RefreshShell();
            }
            else
            {
                Dispatcher.UIThread.Post(RefreshShell);
            }
        };
        _session.SelectionChanged += OnSelectionChanged;
        _session.FileGraphStateChanged += OnFileGraphStateChanged;

        // 두 하이어아키 · 탭 사이 선택 맞추기 (MainWindow.Hierarchy.cs).
        WireHierarchies();
        // 프로젝트가 아직 없을 때는 빈 판 한가운데의 단추가 먼저다 — File 메뉴의 「새 프로젝트…」 · 「열기…」와 같은 길 하나를 지난다
        // ([대본]은 둘 다 · usability UX35, [구성]은 [새 프로젝트]).
        ChapterGraph.ProjectNewCenterButton.Click += OnNewClick;
        Graph.ProjectNewCenterButton.Click += OnNewClick;
        Graph.ProjectOpenCenterButton.Click += OnOpenClick;
        NewMenuItem.Click += OnNewClick;
        OpenMenuItem.Click += OnOpenClick;
        SaveMenuItem.Click += OnSaveClick;
        OutsideReloadButton.Click += OnOutsideReloadClick;
        OutsideOverwriteButton.Click += OnOutsideOverwriteClick;

        // 창으로 돌아올 때 — 그 사이 다른 곳(GitHub Desktop의 받기 등)이 프로젝트 파일을 바꿨는지 본다(writer-release WR1).
        Activated += (_, _) => UiGuard.Run(_session, Ui.Main_CheckChangedFiles, CheckOutsideChanges);
        UndoMenuItem.Click += (_, _) => UndoOrRedo(redo: false);
        RedoMenuItem.Click += (_, _) => UndoOrRedo(redo: true);
        SettingsButton.Click += (_, _) => UiGuard.Run(_session, Ui.Main_Settings, () => OpenSettings());
        WorkFolderButton.Click += (_, _) =>
            UiGuard.Run(_session, Ui.Main_OpenWorkFolder, OpenWorkFolder);

        Opened += OnOpened;

        // 저장 단축키 — 어디에 포커스가 있어도 Ctrl+S가 저장이다.
        //
        // Ctrl+Z · Ctrl+Y도 창 전체에서 받는다 — 하이어아키나 판에서 실수로 지운 사람이 메뉴를 찾아 올라가지 않아도 그 자리에서
        //    되돌린다. 같은 일을 File 메뉴의 「되돌리기」 · 「다시 실행」도 한다(UndoOrRedo).
        //
        // 맥에서는 ⌘도 Ctrl처럼 듣고 ⌘⇧Z가 다시 실행이며 ⌫도 카드를 지운다 — OS마다 갈리는 것은 PlatformKeys 한 벌이다(writer-app WA1).
        SaveMenuItem.InputGesture = PlatformKeys.Gesture(Avalonia.Input.Key.S);
        UndoMenuItem.InputGesture = PlatformKeys.Gesture(Avalonia.Input.Key.Z);
        RedoMenuItem.InputGesture = PlatformKeys.IsMac
            ? PlatformKeys.Gesture(Avalonia.Input.Key.Z, Avalonia.Input.KeyModifiers.Shift)
            : PlatformKeys.Gesture(Avalonia.Input.Key.Y);

        AddHandler(KeyDownEvent, (_, args) =>
        {
            // Delete = [대본] 탭에서 고른 카드(에피소드 · 분기 카드)를 지운다(2026-11-18 소유자) — 묻지 않는다(되돌리기 한 칸). 글 칸에서는
            //    가로채지 않는다 — 거기서 Delete는 글자 지우기다.
            if (PlatformKeys.IsDelete(args.Key) && args.KeyModifiers == Avalonia.Input.KeyModifiers.None &&
                ReferenceEquals(MainTabs.SelectedItem, GraphTabItem) &&
                FocusManager?.GetFocusedElement() is not TextBox)
            {
                UiGuard.Run(_session, Ui.Main_Delete, () => args.Handled = Graph.DeleteSelectedCard());
                return;
            }

            // F = 고른 것에 화면 맞추기 — [대본] 탭(2026-11-18 소유자 · writer-comfort WC2)과 [구성] 탭(2026-11-23 소유자 — 「구성탭에서도 F를 눌러서」).
            //    글 칸 · 콤보에서는 그 칸의 것이다.
            if (args.Key == Avalonia.Input.Key.F && args.KeyModifiers == Avalonia.Input.KeyModifiers.None &&
                FocusManager?.GetFocusedElement() is not (TextBox or ComboBox))
            {
                if (ReferenceEquals(MainTabs.SelectedItem, GraphTabItem))
                {
                    UiGuard.Run(_session, Ui.Main_FitToScreen, () => args.Handled = Graph.FocusSelection());
                    return;
                }

                if (ReferenceEquals(MainTabs.SelectedItem, ChapterTabItem))
                {
                    UiGuard.Run(_session, Ui.Main_FitToScreen, () => args.Handled = ChapterGraph.FocusSelection());
                    return;
                }
            }

            if (!PlatformKeys.HasCommand(args.KeyModifiers))
            {
                return;
            }

            switch (args.Key)
            {
                case Avalonia.Input.Key.S:
                    args.Handled = true;
                    OnSaveClick(this, new RoutedEventArgs());
                    break;

                // Ctrl+F(맥은 ⌘F) = [대본] 판의 찾기 띠(2026-11-25 소유자 — 「대본의 그래프뷰에서 Ctrl+F 기능이 있으면 좋겠다」). 글 칸에서 쓰는 중이어도
                //    연다 — 글 칸은 Ctrl+F를 쓰지 않는다.
                case Avalonia.Input.Key.F when ReferenceEquals(MainTabs.SelectedItem, GraphTabItem):
                    args.Handled = true;
                    UiGuard.Run(_session, Ui.Main_Find, Graph.OpenFind);
                    break;

                // [대본] 카드 글 칸은 예외다 — 칸은 쓰다 멈출 때마다 글을 넣고(되돌리기 한 칸) 줄이 늘고 줄면 새 칸이 되어
                //    칸 자체의 되돌리기 기록이 비어 있다. 그래서 거기서는 프로젝트 되돌리기 · 다시 실행이다(넣지 않은 글이
                //    있으면 그것부터 걷는다).
                case Avalonia.Input.Key.Z or Avalonia.Input.Key.Y when Graph.IsWritingFocused:
                    args.Handled = true;
                    UndoOrRedo(redo: PlatformKeys.IsRedo(args.Key, args.KeyModifiers));
                    break;

                // 주의: 다른 글 입력칸에서는 <b>가로채지 않는다</b> — 거기서 Ctrl+Z는 <b>타이핑</b>을
                //    되돌리는 것이고, 그것을 프로젝트 되돌리기로 바꾸면 쓰던 문장이 통째로
                //    날아간다. TextBox가 제 되돌리기를 먼저 하도록 둔다.
                case Avalonia.Input.Key.Z or Avalonia.Input.Key.Y when FocusManager?.GetFocusedElement() is not TextBox:
                    args.Handled = true;
                    UndoOrRedo(redo: PlatformKeys.IsRedo(args.Key, args.KeyModifiers));
                    break;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // 10분 자동 저장 —저장 경로가 있고 바뀐 것이 있을 때만, 상태줄로 알린다.
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _autoSaveTimer.Tick += (_, _) => UiGuard.Run(_session, Ui.Main_Autosave, () =>
        {
            if (_session.ProjectPath is not null && _session.IsDirty)
            {
                // 다른 곳이 바꾼 파일은 자동 저장이 덮지 않는다 — 띠가 고르게 한다(writer-release WR1).
                if (_session.ChangedOutside)
                {
                    ShowOutsideChangeBar();
                    _session.SetStatus(string.Format(Ui.Main_AutosaveStoppedProjectFileWas, DateTime.Now));
                    return;
                }

                _session.Save();
                _session.SetStatus(string.Format(Ui.Main_Autosaved, DateTime.Now));
            }
        });
        _autoSaveTimer.Start();

        RebuildFileList();
        Graph.Rebuild();
        ShowSelectedNode();
        RefreshShell();
    }

    /// <summary>
    /// 연출 그래프의 곁기둥 한 판 — 대사 노드 편집기와 빈 안내가 겹쳐 선 자리(하나만 보인다).
    /// </summary>
    private Control BuildSidePanel()
    {
        // 요약 칸(챕터 · 장면 — 장면 메모 공간이 길어진다)은 기둥을 넘치면 세로로 스크롤한다. 보이기는 요약 칸을 따른다 —
        // 빈 스크롤 칸이 대사 편집기 위에 겹쳐 누름을 가로채지 않게.
        var summary = new ScrollViewer
        {
            Content = DirectorSummary,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Tag = "director-summary-scroll"
        };
        summary.Bind(Visual.IsVisibleProperty, DirectorSummary.GetObservable(Visual.IsVisibleProperty));

        return new Panel
        {
            Children = { DialogueEditor, summary, EmptyText }
        };
    }

    /// <summary>화자 · 조건 탭의 칸 — 넘치면 세로로 스크롤한다.</summary>
    private static ScrollViewer TabScroll(StackPanel content, string tag) => new()
    {
        Content = content,
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        Tag = tag
    };

    /// <summary>
    /// 검증용 손잡이 — 최근 프로젝트 복원을 끈다. 헤드리스 테스트의 창은 "이 컴퓨터가
    /// 마지막에 연 프로젝트"를 물려받으면 안 된다: 테스트가 세운 프로젝트 대신 이 컴퓨터의 프로젝트가 열리면 판이 다시 그려져
    /// 부하에 따라 매번 다른 테스트가 흔들린다(더블클릭 넷 사이에 판이 재건되면 제스처도 끊긴다).
    /// </summary>
    internal static bool RestoreRecentProjectOnOpen { get; set; } = true;

    /// <summary>
    /// 최근 프로젝트 복원은 편의 기능이다. 실패해도 빈 창은 그대로 쓸 수 있어야 한다.
    /// 이 핸들러는 async void라 예외가 새면 잡아 줄 곳이 없고 곧장 프로세스를 죽인다.
    /// </summary>
    private void OnOpened(object? sender, EventArgs e)
    {
        if (_restoredRecent || !RestoreRecentProjectOnOpen)
        {
            return;
        }

        _restoredRecent = true;

        try
        {
            if (AppSettingsService.LoadRecentProject() is { } recent)
            {
                _session.Open(recent);
            }
        }
        catch (Exception exception)
        {
            StartupLog.TryWrite(Ui.App_LogRestoreRecentProject, exception);
            _session.SetStatus(
                string.Format(Ui.Main_CouldnTOpenProjectYou, exception.Message));
        }
    }

    private void OnSessionChanged(object? sender, ProjectChangedEventArgs e)
    {
        void Apply()
        {
            ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(e.Kind, _session.SelectedNode, e.FromHistory);

            if (plan.RebuildGraph || ((plan.RebuildCard || plan.RefreshCardTitle) && e.NodeId is null))
            {
                Graph.Rebuild();
            }
            else if (plan.RefreshCardTitle)
            {
                // 카드 하나의 제목만 바뀌었다 — 그 제목 글만 고친다(usability UX15). 카드 머리에서 고쳤으면 인스펙터의 이름 칸도 따라온다(2026-11-25 —
                // 인스펙터는 다시 세우지 않는다 · 거기서 쓰는 중이면 그대로).
                Graph.RefreshCardTitle(e.NodeId!);
                DialogueEditor.RefreshName();
            }
            else if (plan.RebuildCard)
            {
                // 한 카드의 줄만 바뀌었다 — 그 카드만 다시 짓는다(usability UX13).
                Graph.RebuildCard(e.NodeId!);
            }
            else if (plan.RefreshGraphPositions)
            {
                // 좌표만 바뀌었다. 카드를 다시 만들면 드래그가 끊긴다.
                Graph.RefreshPositions();
            }
            else if (plan.RedrawChoices)
            {
                // 선택지 하나의 내용만 — 그 출발 카드의 철도만 다시 긋는다(usability UX16).
                Graph.RedrawChoices(e.NodeId);
            }
            else if (plan.RefreshBranchConditions)
            {
                // 챕터 조건만 — 분기 카드의 조건 표만 다시 단다(usability UX16).
                Graph.RefreshBranchConditions();
            }
            else if (plan.RefreshWriterMarks)
            {
                // 작가용 글 서식만 — 그 카드의 글 칸 꾸밈만 다시 입힌다(writer-formatting). 글 맞춤을 켠 카드는 새 모양에 다시 맞춘다(되돌리기 · 다시
                // 실행은 빼고 — 그때의 크기가 이미 그때의 서식에 맞는다).
                Graph.RefreshWriterMarks(e.NodeId, refit: !e.FromHistory);
            }
            else if (plan.RefreshSceneFrames)
            {
                // 장면 이름표의 「메모」 표시만 — 판을 다시 짓지 않는다(에피소드 지시 · usability UX7).
                Graph.RefreshSceneFrames();
            }
            else if (plan.RefreshAllCardWriting)
            {
                // 글을 되돌렸다 — 어느 카드의 글인지 모르므로 글 칸마다 모델의 글로 다시 채운다(쓰는 중인 칸은 UndoInWriting이 채운다).
                Graph.RefreshAllWriting();

                if (DialogueEditor.IsVisible && !DialogueEditor.IsKeyboardFocusWithin)
                {
                    DialogueEditor.Rebuild();
                }
            }
            else if (plan.RefreshCardWriting)
            {
                // 그 카드의 글 칸을 다시 채운다 — 어느 카드인지 모르면(화자 이름 바꾸기처럼 여러 카드의 글이 바뀌었다) 칸마다
                // (usability UX18 · ⛔ 고른 카드의 칸만 — 다른 카드는 옛 화자 이름을 보이다가 그 칸을 고치면 옛 이름을 도로 넣었다).
                if (e.NodeId is { } writtenNodeId)
                {
                    Graph.RefreshWriting(writtenNodeId);
                }
                else
                {
                    Graph.RefreshAllWriting();
                }

                // 카드 글 칸에서 고친 글이 곁기둥에도 바로 비친다.곁기둥에서 쓰는 중이면(초점이 그 안에 있다)
                // 그대로 둔다 — 고친 곳이 거기다.
                if (DialogueEditor.IsVisible &&
                    (e.NodeId is null || string.Equals(DialogueEditor.NodeId, e.NodeId, StringComparison.Ordinal)) &&
                    !DialogueEditor.IsKeyboardFocusWithin)
                {
                    DialogueEditor.Rebuild();
                }
            }

            if (plan.RebuildInspector)
            {
                RebuildInspector();
                FillSideTabs();
            }
            else
            {
                RefreshChapterTextAmount();   // 챕터 요약이 서 있으면 「쓴 글」을 다시 센다(writer-comfort WC7)
            }

            RefreshShell();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Dispatcher.UIThread.Post(Apply);
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        // 카드를 고르면 인스펙터 탭이 그것을 보인다 — 고름을 놓는 것은 탭을 바꾸지 않는다(side-tabs V3).
        if (_session.SelectedNode is not null)
        {
            Graph.ShowSideTab(SideTab.Inspector);
        }

        SyncSelection();
        ShowSelectedNode();
        Graph.HighlightSelection();
        RefreshShell();
    }

    private void OnFileGraphStateChanged(object? sender, FileGraphStateChangedEventArgs e)
    {
        void Apply()
        {
            RebuildFileList();

            if (e.ExpandedFilesChanged)
            {
                Graph.Rebuild();
            }

            if (e.ActiveFileChanged)
            {
                FillConditionTab();   // 「조건 및 스탯」 탭은 지금 챕터(활성 판)를 보인다
            }

            RefreshShell();
        }

        // 체크박스나 라디오 버튼의 RoutedEvent가 끝나기 전에 파일 목록을 통째로
        // 교체하면 클릭한 컨트롤을 자기 이벤트 안에서 제거하게 된다. 다음 UI turn에서 갱신한다.
        Dispatcher.UIThread.Post(Apply);
    }

    // ── 파일 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 「챕터 추가」(하이어아키 프로젝트 줄 · 빈 곳, [대본] 판의 챕터 밖 우클릭) — 「새 챕터」(자동 ID)를 <b>바로</b> 세우고 그 챕터를 고른다. 이름은
    /// 하이어아키에서 두 번 눌러 고친다(2026-11-18 소유자 — ⛔ 이름 칸을 먼저 띄우던 창구 <c>ChapterAddFlyout</c>와 하이어아키 머리의 [＋]는 걷혔다).
    /// <b>첫 장면 「새 장면」과 첫 에피소드를 함께 세운다</b>(같은 날 — 「빈 챕터를 생성했을 때, 화면상으로 아무 변화없는게 진짜 거슬리네」) — 챕터 ·
    /// 판 · 장면 · 카드가 한 되돌리기 칸이다. 카드는 <paramref name="at"/>(판 우클릭의 자리)에, 없으면 판의 모든 카드 오른쪽에 선다. 챕터를 세우는
    /// 규칙은 <see cref="AuthoringSession.CreateNamedChapter"/> 한 벌이다([＋ 에피소드]도 그것을 부른다).
    /// </summary>
    private void CreateChapterNow(Avalonia.Point? at)
    {
        string? chapterId = null;
        string? failure = null;

        _session.Editor.Batch(() =>
        {
            (chapterId, failure) = _session.CreateNamedChapter(null);

            if (chapterId is not null)
            {
                Graph.AddFirstEpisode(chapterId, at);
            }
        });

        if (failure is not null || chapterId is null)
        {
            _session.SetStatus(failure ?? Ui.Main_CouldnTCreateChapter);
            return;
        }

        ChapterGraph.Refresh();
        ChapterGraph.SelectChapter(chapterId);

        // 만든 챕터를 고른다 — 하이어아키에서 그 줄을 누른 것과 같다(줄이 칠해지고 인스펙터에 챕터 요약 · 2026-11-18 소유자).
        OnDirectorHierarchyPicked(new HierarchyPick(HierarchyKind.Chapter, chapterId));
        Graph.GoToBoard(_session.EnsureChapterBoard(chapterId));   // 「챕터 추가」는 [대본]에 있다 — 만든 챕터로 간다
        _session.SetStatus(
            string.Format(Ui.Main_CreatedChapterWithSceneRename, NewNames.Chapter, NewNames.Scene, NewNames.Episode));
    }

    /// <summary>
    /// 하이어아키를 다시 세운다 (hierarchy). 원천은 챕터 그래프 뷰가 읽은
    /// 챕터 목록 하나다. 고른 것은 하이어아키가 들고 있다가 다시 세운 뒤에도 잇는다. 챕터가 없는 판은 하이어아키에 없다.
    /// </summary>
    private void RebuildFileList()
    {
        RebuildHierarchies();
    }

    /// <summary>창 머리와 하이어아키 맨 위 줄이 같은 이름을 쓴다.</summary>
    private string ProjectDisplayTitle() => _session.ProjectPath is null
        ? _session.Project.Title
        : ProjectDisplayName(_session.ProjectPath);

    /// <summary>
    /// 지우거나 이름을 바꾼 챕터의 옛 진행 JSON이 어떻게 됐는지 한 마디 — 바로 앞의 다시 읽기가 내보내며 걷는다
    /// (<see cref="ChapterExportService"/>). 못 걷었으면 그렇다고 말한다: 그냥 두면 게임이 없는 챕터를 계속 싣는다.
    /// </summary>
    private string StaleExportNote(string? stale, string chapterId)
    {
        if (stale is null || _session.ProjectPath is not { } projectPath)
        {
            return string.Empty;
        }

        return File.Exists(ChapterExportService.ExportPathFor(projectPath, chapterId))
            ? string.Format(Ui.Main_CouldnTRemoveOldProgression, stale)
            : string.Format(Ui.Main_RemovedOldProgressionJSONSame, stale);
    }

    /// <summary>
    /// 고른 챕터를 지운다([대본] 툴바 [삭제] → 「챕터 삭제」) — <b>확인 한 번</b> 뒤에 지운다(누른 단추 옆에 붙는 확인 칸 — 「에피소드 삭제」와 같은
    /// <see cref="ConfirmFlyout"/>). 챕터 · 판 · 조건 배관을 함께 걷는다(<see cref="ChapterDeleter"/>) — 사람이 폴더에서 손으로 지우면 절반만
    /// 지워진다(판과 노드가 남아 이름이 겹치는 번들로 뒤늦게 터진다). 되돌리기 한 칸.
    /// ⛔ 2026-11-18까지는 이름 칸 · [이름 바꾸기] · 두 번 누르는 [챕터 제거…]를 한 플라이아웃에 담았다(소유자 — 둘을 나눈다).
    /// </summary>
    private void ConfirmChapterRemoval(Control anchor, string chapterId)
    {
        string shown = _session.Editor.FindChapter(chapterId)?.ShownName ?? chapterId;

        RemoveConfirmButton = ConfirmFlyout.Show(
            anchor,
            _session,
            string.Format(Ui.Main_ChapterWillBeRemovedAlong, shown),
            Ui.Main_DeleteChapter,
            () =>
            {
                ChapterDeleter.Result result = ChapterDeleter.Delete(_session.Editor, _session.ProjectPath, chapterId);

                if (!result.Deleted)
                {
                    _session.SetStatus(result.Failure!);
                    return;
                }

                ChapterGraph.Refresh();
                ForgetRemovedPick();
                RefreshShell();
                _session.SetStatus(
                    Plural.Format(Ui.Main_DeletedChapterAndCardsIn, shown, result.NodesRemoved) +
                    StaleExportNote(result.StaleExport, chapterId));
            },
            closed: () => RemoveConfirmButton = null);
    }

    /// <summary>
    /// 뜬 「챕터 지우기」 · 「장면 지우기」 확인 단추([삭제] → 「챕터 삭제」 · 「장면 삭제」가 띄운다) — 화면 시험의 손잡이다(플라이아웃의 팝업은 창 밖에 살아 나무를 타고 내려가 찾을 수 없다).
    /// 없으면 null.
    /// </summary>
    internal Button? RemoveConfirmButton { get; private set; }

    /// <summary>
    /// 새 프로젝트 = <b>폴더 하나</b>.고른 폴더에 고정 이름(<see cref="ProjectFolder.DefaultManifestName"/>)으로
    /// 깐다. 이미 프로젝트가 있는 폴더면 덮지 않고 연다.
    /// </summary>
    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (await PickFolderAsync(Ui.Main_FolderForNewProject) is not { } folder)
            {
                return;
            }

            CreateProjectIn(folder);
        }
        catch (Exception exception)
        {
            Report(Ui.Main_NewProject, exception);
        }
    }

    /// <summary>
    /// 그 폴더에 새 프로젝트를 깐다 — 이미 프로젝트가 있는 폴더면 그것을 연다. 새 프로젝트는 [대본] 탭에서 시작한다: 탭 순서가
    /// 작업 순서라(대본 → 구성) 화를 만들고 쓰는 자리가 첫걸음이다(usability UX23 · ⛔ [구성]에서 시작했다 — 빈 [구성] 판의 첫 챕터 단추는
    /// 어차피 [대본]으로 데려갔다).
    /// </summary>
    internal void CreateProjectIn(string folder)
    {
        if (ProjectFolder.WhyCannotCreateIn(folder) is { } reason)
        {
            _session.SetStatus(reason);
            OpenFolder(folder);
            return;
        }

        _session.NewProject();
        _session.Save(ProjectFolder.NewManifestPathIn(folder));
        _session.SetStatus(Ui.Main_CreatedNewProjectPressCreate);
        RefreshShell();
        RebuildFileList();   // 하이어아키의 빈 목록 안내도 「저장된 프로젝트」의 것으로
        MainTabs.SelectedItem = GraphTabItem;
    }

    /// <summary>열기 = 폴더 고르기.폴더 안의 유일한 manifest를 찾는다 — 없거나 둘이면 사유를 말한다.</summary>
    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (await PickFolderAsync(Ui.Main_OpenProjectFolder) is { } folder)
            {
                OpenFolder(folder);
            }
        }
        catch (Exception exception)
        {
            Report(Ui.Main_OpenProject, exception);
        }
    }

    private void OpenFolder(string folder)
    {
        ProjectFolder.Found found = ProjectFolder.Find(folder);

        if (!found.IsFound)
        {
            _session.SetStatus(found.Problem!);
            return;
        }

        _session.Open(found.ManifestPath!);
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        IStorageProvider? storage = GetTopLevel(this)?.StorageProvider;

        if (storage is null || !storage.CanPickFolder)
        {
            _session.SetStatus(Ui.Main_FolderPickerCanTBe);
            return null;
        }

        IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });

        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // 카드 글 칸에 넣지 않은 글이 있으면 먼저 넣는다 — Ctrl+S도 넣는 때다.
            Graph.FlushWriting();

            if (_session.ProjectPath is null)
            {
                _session.SetStatus(Ui.Main_ThereIsNoProjectFolder);
                return;
            }

            _session.Save();
            RefreshShell();
        }
        catch (ProjectChangedOutsideException refused)
        {
            ShowOutsideChangeBar();
            _session.SetStatus(refused.Message);
        }
        catch (Exception exception)
        {
            Report(Ui.Main_Save, exception);
        }
    }

    // ── 바깥에서 바뀐 프로젝트 (writer-release WR1) ─────────────────────────

    /// <summary>
    /// <b>창으로 돌아왔을 때 프로젝트 파일이 다른 곳에서 바뀌었는가</b>(2026-11-19 소유자 — 작가와 프로젝트를 Git으로 주고받는다). GitHub
    /// Desktop의 받기(Pull) 따위가 파일을 바꿨는데 이 창이 옛것을 들고 있으면, 다음 저장 · 10분 자동 저장이 받은 것을 말없이 덮는다. 그래서 —
    /// <list type="bullet">
    /// <item>여기서 고친 것이 없으면 <b>그대로 다시 읽는다</b>(띠 없이 · 상태줄로 알린다) — 받고 돌아오면 받은 것이 보인다.</item>
    /// <item>고친 것이 남아 있으면 <b>위 띠</b>가 선다 — [다시 열기](받은 것을 읽고 여기서 고친 것은 버림) · [내 것으로 덮어쓰기]. 고를 때까지
    ///   저장 · 자동 저장은 쓰지 않는다(<see cref="AuthoringSession.Save"/>가 거절한다).</item>
    /// </list>
    /// 카드 글 칸에 넣지 않은 글도 고친 것이다 — 먼저 넣고 견준다.
    /// </summary>
    internal void CheckOutsideChanges()
    {
        if (_session.ProjectPath is null || !_session.ChangedOutside)
        {
            OutsideChangeBar.IsVisible = false;
            return;
        }

        Graph.FlushWriting();

        if (_session.IsDirty)
        {
            ShowOutsideChangeBar();
            _session.SetStatus(Ui.Main_ProjectFileWasChangedElsewhere);
            return;
        }

        ReopenFromDisk();
        _session.SetStatus(string.Format(Ui.Main_ProjectFileWasChangedElsewhere2, DateTime.Now));
    }

    private void ShowOutsideChangeBar() => OutsideChangeBar.IsVisible = true;

    /// <summary>디스크의 프로젝트를 다시 연다 — 여기서 고친 것(되돌리기 기록 포함)은 버린다.</summary>
    private void ReopenFromDisk()
    {
        OutsideChangeBar.IsVisible = false;
        _session.Open(_session.ProjectPath!);
    }

    private void OnOutsideReloadClick(object? sender, RoutedEventArgs e) => UiGuard.Run(_session, Ui.Main_Reload, () =>
    {
        ReopenFromDisk();
        _session.SetStatus(Ui.Main_ReloadedProjectFileChangedElsewhere);
    });

    private void OnOutsideOverwriteClick(object? sender, RoutedEventArgs e) => UiGuard.Run(_session, Ui.Main_Overwrite, () =>
    {
        Graph.FlushWriting();
        _session.Save(overwriteOutsideChanges: true);
        OutsideChangeBar.IsVisible = false;
        RefreshShell();
        _session.SetStatus(Ui.Main_OverwroteWithThisWindowS);
    });

    // ── 내보내기 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 진행 JSON이 나가는 <c>exported/</c>를 연다.
    ///
    /// 이 파일들은 <b>사람 손을 안 기다리고 저절로 나간다</b>. 그래서 사람이
    /// 할 일은 만드는 것이 아니라 <b>가서 꺼내 오는 것</b>뿐인데, 그 자리를 몰라 못 꺼내
    /// 가면 안 된다 — 비개발자 작가에게 건네는 준비의 일부다.
    ///
    /// 주의: 폴더를 <b>대신 만들지 않는다.</b> 없다는 것은 아직 한 챕터도 안 나갔다는 뜻이고,
    /// 빈 폴더를 열어 주면 "나갔는데 비었다"로 읽힌다 — 사유를 말하는 편이 낫다.
    /// </summary>
    /// <summary>[📂 작업 폴더] — 프로젝트 폴더의 <c>exported/</c>를 연다(툴이 저절로 내는 진행 JSON · 대사 Yarn의 자리).</summary>
    private void OpenWorkFolder()
    {
        if (_session?.ProjectPath is not { } projectPath)
        {
            _session?.SetStatus(Ui.Main_SaveProjectFirstToSet);
            return;
        }

        string folder = WorkFolder.Of(projectPath);

        if (!Directory.Exists(folder))
        {
            _session.SetStatus(
                string.Format(Ui.Main_ThereIsNoWorkFolder, folder));
            return;
        }

        WorkFolder.Open(folder);
    }

    // ── 화면 ────────────────────────────────────────────────────────────────



    private void ShowSelectedNode()
    {
        StoryNode? node = _session.SelectedNode;
        bool summary = (_directorSummary is not null || _directorLine is not null) && node is null;

        DialogueEditor.IsVisible = !summary && node is DialogueNode;
        EmptyText.IsVisible = !summary && node is null;
        EmptyText.Text = Ui.Main_SelectNodeToEditIt;

        if (!summary && node is DialogueNode)
        {
            DialogueEditor.Show(node.Id);
        }

        ShowDirectorInspector(node);
    }

    /// <summary>
    /// 구조가 바뀌었을 때 오른쪽 화면을 다시 만든다.
    /// 선택된 노드가 그대로면 같은 노드를 다시 그리고, 바뀌었으면 그쪽으로 옮긴다.
    /// </summary>
    private void RebuildInspector()
    {
        StoryNode? node = _session.SelectedNode;

        if (node is DialogueNode && DialogueEditor.NodeId == node.Id)
        {
            DialogueEditor.Rebuild();
            ShowDirectorInspector(node);   // 머리글의 제목도 바뀌었을 수 있다
            return;
        }

        ShowSelectedNode();
    }

    /// <summary>
    /// 되돌리기 · 다시 실행 — Ctrl+Z · Ctrl+Y와 File 메뉴의 「되돌리기」 · 「다시 실행」이 이 한 벌을 지난다(finishing FX7). [대본] 카드 글 칸에
    /// 초점이 있으면 칸의 규칙을 따른다(넣지 않은 글이 있으면 그것부터 걷는다 — <see cref="GraphEditorView.UndoInWriting"/>). 메뉴를 열면 초점이
    /// 메뉴로 가 칸이 쓰던 글을 먼저 넣으므로, 메뉴의 되돌리기는 그 글부터 되돌린다 — 키와 같은 결과다.
    /// </summary>
    private void UndoOrRedo(bool redo) =>
        UiGuard.Run(_session, redo ? Ui.Main_Redo : Ui.Main_Undo, () =>
        {
            if (Graph.IsWritingFocused)
            {
                Graph.UndoInWriting(redo);
            }
            else if (redo)
            {
                _session.Editor.Redo();
            }
            else
            {
                _session.Editor.Undo();
            }

            RefreshShell();
        });

    private void RefreshShell()
    {
        // 되돌릴 것 · 다시 할 것이 없으면 메뉴가 꺼진다 — 눌러도 아무 일이 없는 항목은 고장처럼 보인다.
        UndoMenuItem.IsEnabled = _session.Editor.CanUndo;
        RedoMenuItem.IsEnabled = _session.Editor.CanRedo;

        string name = ProjectDisplayTitle();

        ProjectNameText.Text = name;
        ProjectPathText.Text = _session.ProjectPath ?? Ui.Main_NotSaved;
        ChapterGraph.ChapterSummaryText.Text = _session.ActiveFile is { } activeFile
            ? string.Format(Ui.Main_Current, activeFile.Name)
            : Ui.Main_NoCurrentFile;
        StatusText.Text = _session.StatusMessage;

        bool dirty = _session.IsDirty;
        DirtyText.Text = dirty ? Ui.Main_UnsavedChanges : string.Empty;

        Title = dirty ? $"* {BaseTitle} — {name}" : $"{BaseTitle} — {name}";

        // 저장된 프로젝트가 생기거나(새 프로젝트 · 열기) 없어지면 [대본] 빈 판의 첫걸음이 바뀐다(usability UX35).
        Graph.RefreshEmptyBoard();
    }

    private static string ProjectDisplayName(string path)
    {
        string fileName = Path.GetFileName(path) ?? string.Empty;
        return fileName.EndsWith(ProjectManifestJson.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^ProjectManifestJson.FileExtension.Length]
            : Path.GetFileNameWithoutExtension(fileName) ?? fileName;
    }

    private void Report(string action, Exception exception)
    {
        StartupLog.TryWrite(action, exception);
        _session.SetStatus(string.Format(Ui.Main_CouldnTComplete, action, exception.Message));
        RefreshShell();
    }
}
