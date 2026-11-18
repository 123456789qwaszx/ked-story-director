using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App;

/// <summary>
/// 앱 셸. 세션을 하나 만들고 그래프 화면과 노드 화면에 같은 것을 물려준다.
///
/// 두 화면은 서로를 모른다. 둘 다 <see cref="AuthoringSession"/>만 보고 있고,
/// 편집은 전부 <see cref="ProjectEditor"/>를 지난다. 그래서 여기에는 화면을 잇는 코드가 없다.
/// </summary>
public partial class MainWindow : Window
{
    private const string BaseTitle = "VnTool";

    private readonly AuthoringSession _session = new();

    /// <summary>검증용 손잡이 — 창이 쓰는 세션 하나. 테스트가 판을 짓고 선택을 옮긴다.</summary>
    internal AuthoringSession SessionProbe => _session;

    /// <summary>[⚙ 설정] — 설정 창을 모달로 연다(<see cref="SettingsWindow"/>). 시험이 연 창을 잡도록 돌려준다.</summary>
    internal SettingsWindow OpenSettings()
    {
        var settings = new SettingsWindow(_session);
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
        Text = "노드를 선택하면 여기서 편집합니다.",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.5
    };

    private readonly DispatcherTimer _autoSaveTimer;
    private bool _restoredRecent;
    /// <summary>왼쪽 챕터 목록의 데이터. 원천은 챕터 그래프 뷰의 읽기 하나다.</summary>
    private IReadOnlyList<ChapterEntry> _chapters = Array.Empty<ChapterEntry>();

    public MainWindow()
    {
        InitializeComponent();

        // 마지막 안전망 (공통 불변식 4). 개별 핸들러의 포획을 놓친 예외가 여기까지 오면
        // 로그와 상태줄에 남기고 앱은 계속 산다 — 클릭 하나가 미저장 원고를 끝내지 않는다.
        // 개별 경로의 포획을 대신하는 장치가 아니라, 새 코드가 실수했을 때의 바닥이다.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Report(_session, "화면 동작", e.Exception);
        };

        // 곁기둥을 제 화면에 건다 — Attach보다 먼저여야 첫 그리기부터 자리가 서 있다.
        Graph.SetSidePanel(BuildSidePanel());

        Graph.Attach(_session);
        // 왼쪽 챕터 목록의 원천은 챕터 그래프 뷰가 읽은 목록 하나다 — 두 곳이 따로 읽으면
        // 감시·재시도 규칙이 두 벌이 된다. Attach 전에 구독해야 첫 읽기부터 받는다.
        ChapterGraph.EntriesReloaded += entries =>
        {
            _chapters = entries;
            RebuildFileList();
            // 연출 그래프의 철도 배선도 같은 목록 하나를 본다 — 두 곳이 따로 읽지 않는다.
            Graph.SupplyChapters(entries);
        };
        // 위 드롭다운으로 골라도 왼쪽 목록의 강조가 따라온다 —
        // 목록 클릭이 하던 일(판 활성 + 목록 다시 그리기)을 여기서도 똑같이 한다.
        ChapterGraph.ChapterSelected += chapterId => UiGuard.Run(_session, "챕터 선택", () =>
        {
            _session.SelectFile(_session.EnsureChapterBoard(chapterId));
            RebuildFileList();
        });
        ChapterGraph.Attach(_session);
        DialogueEditor.Attach(_session);
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
        // 프로젝트가 아직 없을 때는 [새 프로젝트]가 먼저다 —
        // 툴바의 [새로 만들기]와 같은 길 하나를 지난다.
        ChapterGraph.ProjectNewCenterButton.Click += OnNewClick;
        NewMenuItem.Click += OnNewClick;
        OpenMenuItem.Click += OnOpenClick;
        SaveMenuItem.Click += OnSaveClick;
        SettingsButton.Click += (_, _) => UiGuard.Run(_session, "설정", () => OpenSettings());
        WorkFolderButton.Click += (_, _) =>
            UiGuard.Run(_session, "작업 폴더 열기", OpenWorkFolder);

        Opened += OnOpened;

        // 저장 단축키 — 어디에 포커스가 있어도 Ctrl+S가 저장이다.
        //
        // Ctrl+Z · Ctrl+Y도 창 전체에서 받는다. 되돌리기 단추는 창 위쪽 띠에 있어서, 트리에서
        //    실수로 지운 사람이 찾아 올라가게 해서는 안 된다 — 지우는 단추는 Ctrl+Z로 되돌릴 수
        //    있어야 한다.
        AddHandler(KeyDownEvent, (_, args) =>
        {
            // Delete = [대본] 탭에서 고른 카드(에피소드 · 분기 카드)를 지운다(2026-11-18 소유자) — 묻지 않는다(되돌리기 한 칸). 글 칸에서는
            //    가로채지 않는다 — 거기서 Delete는 글자 지우기다.
            if (args.Key == Avalonia.Input.Key.Delete && args.KeyModifiers == Avalonia.Input.KeyModifiers.None &&
                ReferenceEquals(MainTabs.SelectedItem, GraphTabItem) &&
                FocusManager?.GetFocusedElement() is not TextBox)
            {
                UiGuard.Run(_session, "지우기", () => args.Handled = Graph.DeleteSelectedCard());
                return;
            }

            if (!args.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
            {
                return;
            }

            switch (args.Key)
            {
                case Avalonia.Input.Key.S:
                    args.Handled = true;
                    OnSaveClick(this, new RoutedEventArgs());
                    break;

                // [대본] 카드 글 칸은 예외다 — 칸은 쓰다 멈출 때마다 글을 넣고(되돌리기 한 칸) 줄이 늘고 줄면 새 칸이 되어
                //    칸 자체의 되돌리기 기록이 비어 있다. 그래서 거기서는 프로젝트 되돌리기 · 다시 실행이다(넣지 않은 글이
                //    있으면 그것부터 걷는다).
                case Avalonia.Input.Key.Z or Avalonia.Input.Key.Y when Graph.IsWritingFocused:
                    args.Handled = true;
                    bool redo = args.Key == Avalonia.Input.Key.Y;
                    UiGuard.Run(_session, redo ? "다시 실행" : "되돌리기", () => Graph.UndoInWriting(redo));
                    break;

                // 주의: 다른 글 입력칸에서는 <b>가로채지 않는다</b> — 거기서 Ctrl+Z는 <b>타이핑</b>을
                //    되돌리는 것이고, 그것을 프로젝트 되돌리기로 바꾸면 쓰던 문장이 통째로
                //    날아간다. TextBox가 제 되돌리기를 먼저 하도록 둔다.
                case Avalonia.Input.Key.Z when FocusManager?.GetFocusedElement() is not TextBox:
                    args.Handled = true;
                    UiGuard.Run(_session, "되돌리기", _session.Editor.Undo);
                    break;

                case Avalonia.Input.Key.Y when FocusManager?.GetFocusedElement() is not TextBox:
                    args.Handled = true;
                    UiGuard.Run(_session, "다시 실행", _session.Editor.Redo);
                    break;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // 10분 자동 저장 —저장 경로가 있고 바뀐 것이 있을 때만, 상태줄로 알린다.
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _autoSaveTimer.Tick += (_, _) => UiGuard.Run(_session, "자동 저장", () =>
        {
            if (_session.ProjectPath is not null && _session.IsDirty)
            {
                _session.Save();
                _session.SetStatus($"자동 저장했습니다 ({DateTime.Now:HH:mm})");
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

    /// <summary>
    /// 검증용 손잡이 — 최근 프로젝트 복원을 끈다. 헤드리스 테스트의 창은 "이 컴퓨터가
    /// 마지막에 연 프로젝트"를 물려받으면 안 된다: 복원된 챕터의 감시자가 부르는 에피소드
    /// 동기화가 테스트가 세운 노드를 솎아 내고 판을 다시 그려, 부하에 따라 매번 다른
    /// 테스트가 흔들린다(더블클릭 넷 사이에 판이 재건되면 제스처도 끊긴다).
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
            StartupLog.TryWrite("최근 프로젝트 복원", exception);
            _session.SetStatus(
                $"마지막에 열었던 프로젝트를 열지 못했습니다. '열기…'로 다시 시도해 주세요. ({exception.Message})");
        }
    }

    private void OnSessionChanged(object? sender, ProjectChangedEventArgs e)
    {
        void Apply()
        {
            ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(e.Kind, _session.SelectedNode);

            if (plan.RebuildGraph)
            {
                Graph.Rebuild();
            }
            else if (plan.RefreshGraphPositions)
            {
                // 좌표만 바뀌었다. 카드를 다시 만들면 드래그가 끊긴다.
                Graph.RefreshPositions();
            }
            else if (plan.RefreshCardWriting && _session.SelectedNodeId is { } writtenNodeId)
            {
                Graph.RefreshWriting(writtenNodeId);

                // 카드 글 칸에서 고친 글이 곁기둥에도 바로 비친다.곁기둥에서 쓰는 중이면(초점이 그 안에 있다)
                // 그대로 둔다 — 고친 곳이 거기다.
                if (DialogueEditor.IsVisible &&
                    string.Equals(DialogueEditor.NodeId, writtenNodeId, StringComparison.Ordinal) &&
                    !DialogueEditor.IsKeyboardFocusWithin)
                {
                    DialogueEditor.Rebuild();
                }
            }

            if (plan.RebuildInspector)
            {
                RebuildInspector();
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
            _session.SetStatus(failure ?? "챕터를 만들지 못했습니다.");
            return;
        }

        ChapterGraph.Refresh();
        ChapterGraph.SelectChapter(chapterId);

        // 만든 챕터를 고른다 — 하이어아키에서 그 줄을 누른 것과 같다(줄이 칠해지고 인스펙터에 챕터 요약 · 2026-11-18 소유자).
        OnDirectorHierarchyPicked(new HierarchyPick(HierarchyKind.Chapter, chapterId));
        Graph.GoToBoard(_session.EnsureChapterBoard(chapterId));   // 「챕터 추가」는 [대본]에 있다 — 만든 챕터로 간다
        _session.SetStatus(
            $"챕터 「{NewNames.Chapter}」를 장면 「{NewNames.Scene}」 · 「{NewNames.Episode}」와 함께 만들었습니다 — 이름은 오른쪽 인스펙터 머리나 하이어아키에서 고칩니다.");
    }

    /// <summary>
    /// 하이어아키를 다시 세운다 (docs/plans/hierarchy.md). 원천은 챕터 그래프 뷰가 읽은
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
            ? $" ⚠ 옛 진행 JSON '{stale}'을 걷지 못했습니다 — 다른 프로그램에 잡혀 있는지 확인하세요."
            : $" 옛 진행 JSON '{stale}'은 걷었습니다(보낼 곳의 같은 파일은 저장할 때 걷습니다).";
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
            $"챕터 「{shown}」와 그 장면 · 에피소드 · [대본] 탭의 카드가 함께 사라집니다. 되돌리기(Ctrl+Z)로 돌아옵니다.",
            "챕터 지우기",
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
                    $"챕터 「{shown}」를 지웠습니다([대본] 탭의 카드 {result.NodesRemoved}장도 함께). 되돌리기(Ctrl+Z)로 돌아옵니다." +
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
            if (await PickFolderAsync("새 프로젝트를 깔 폴더") is not { } folder)
            {
                return;
            }

            CreateProjectIn(folder);
        }
        catch (Exception exception)
        {
            Report("새 프로젝트", exception);
        }
    }

    /// <summary>
    /// 그 폴더에 새 프로젝트를 깐다 — 이미 프로젝트가 있는 폴더면 그것을 연다. 새 프로젝트는 [구성] 탭에서 시작한다: 탭 순서가
    /// 작업 순서라(구성 → 대본) 뼈대를 짜는 자리가 첫걸음이다.
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
        RefreshShell();
        MainTabs.SelectedItem = ChapterTabItem;
    }

    /// <summary>열기 = 폴더 고르기.폴더 안의 유일한 manifest를 찾는다 — 없거나 둘이면 사유를 말한다.</summary>
    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (await PickFolderAsync("프로젝트 폴더 열기") is { } folder)
            {
                OpenFolder(folder);
            }
        }
        catch (Exception exception)
        {
            Report("프로젝트 열기", exception);
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
            _session.SetStatus("이 환경에서는 폴더 선택 창을 열 수 없습니다.");
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
                _session.SetStatus("프로젝트 폴더가 없습니다 — File 메뉴의 [새 프로젝트…]로 폴더를 고르세요.");
                return;
            }

            _session.Save();
            RefreshShell();
        }
        catch (Exception exception)
        {
            Report("저장", exception);
        }
    }

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
    /// <summary>[📂 작업 폴더] — 프로젝트 폴더의 <c>exported/</c>를 연다(툴이 저절로 내는 진행 JSON · 장면 파일 · 대사 Yarn의 자리).</summary>
    private void OpenWorkFolder()
    {
        if (_session?.ProjectPath is not { } projectPath)
        {
            _session?.SetStatus("프로젝트를 먼저 저장해야 작업 폴더가 정해집니다.");
            return;
        }

        string folder = Path.Combine(
            Path.GetDirectoryName(projectPath)!, ChapterExportService.ExportFolderName);

        if (!Directory.Exists(folder))
        {
            _session.SetStatus(
                $"작업 폴더가 아직 없습니다 — 검증을 통과한 챕터가 생기면 진행 JSON이 저절로 " +
                $"여기 나갑니다: {folder}");
            return;
        }

        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
    }

    // ── 화면 ────────────────────────────────────────────────────────────────



    private void ShowSelectedNode()
    {
        StoryNode? node = _session.SelectedNode;
        bool summary = (_directorSummary is not null || _directorLine is not null) && node is null;

        DialogueEditor.IsVisible = !summary && node is DialogueNode;
        EmptyText.IsVisible = !summary && node is null;
        EmptyText.Text = "노드를 선택하면 여기서 편집합니다.";

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

    private void RefreshShell()
    {
        string name = ProjectDisplayTitle();

        ProjectNameText.Text = name;
        ProjectPathText.Text = _session.ProjectPath ?? "저장되지 않음";
        ChapterGraph.ChapterSummaryText.Text = _session.ActiveFile is { } activeFile
            ? $"현재: {activeFile.Name}"
            : "현재 작업 파일 없음";
        StatusText.Text = _session.StatusMessage;

        bool dirty = _session.IsDirty;
        DirtyText.Text = dirty ? "● 저장되지 않은 변경" : string.Empty;

        Title = dirty ? $"* {BaseTitle} — {name}" : $"{BaseTitle} — {name}";
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
        _session.SetStatus($"{action}하지 못했습니다. {exception.Message}");
        RefreshShell();
    }
}
