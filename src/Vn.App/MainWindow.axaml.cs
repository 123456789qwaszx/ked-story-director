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

    // ── 연출 그래프의 곁기둥 (2026-08-22 소유자 — 창의 우측 열에서 그 판 안으로 이사) ──
    //
    // ⚠ XAML이 아니라 여기서 만든다. 자리는 연출 그래프의 것이지만(SetSidePanel) 배선은
    // 셸의 것이다 — 이 편집기는 세션·선택 전환과 얽혀 있어 뷰로 옮기면 그 배선이 두 벌이
    // 된다. 이름을 XAML 시절 그대로 둔 것은 의도다.
    // ⛔ 연출 노드 편집기 · 에셋 탐색기는 2026-10-29에 무대와 함께 ked-story-compositor로 갔다.
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
            // 연출 그래프의 철도 배선(T1)도 같은 목록 하나를 본다 — 두 곳이 따로 읽지 않는다.
            Graph.SupplyChapters(entries);
        };
        // 위 드롭다운으로 골라도 왼쪽 목록의 강조가 따라온다 (2026-08-17 소유자 보고) —
        // 목록 클릭이 하던 일(판 활성 + 목록 다시 그리기)을 여기서도 똑같이 한다.
        ChapterGraph.ChapterSelected += chapterId => UiGuard.Run(_session, "챕터 선택", () =>
        {
            _session.SelectFile(_session.EnsureChapterBoard(chapterId));
            RebuildFileList();
        });
        ChapterGraph.Attach(_session);
        DialogueEditor.Attach(_session);
        _session.Changed += OnSessionChanged;
        // 상태줄만 바뀐 경우는 셸 갱신 하나로 끝난다 (2026-08-18) — 판을 다시 그리거나
        // 챕터를 다시 읽을 이유가 없다. 그 구분이 없어서 노드가 늘수록 느려졌다.
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

        // 하이어아키의 챕터 줄 우클릭 — 챕터 목록 줄의 우클릭(이름 바꾸기 · 제거)을 잇는다.
        ChapterGraph.HierarchyView.RowMenuRequested += (anchor, row) =>
        {
            if (row.Kind == HierarchyKind.Chapter && row.ChapterId is { } chapterId)
            {
                UiGuard.Run(_session, "챕터 메뉴", () => ShowChapterContextFlyout(anchor, chapterId));
            }
        };
        ChapterGraph.ChapterAddButton.Click += (_, _) =>
            UiGuard.Run(_session, "새 챕터", () => ShowAddChapterFlyout(ChapterGraph.ChapterAddButton));
        // 챕터가 하나도 없을 때 판 한가운데 서는 [＋ 챕터] — 뜻이 같으니 배선도 같다.
        // 플라이아웃만 누른 그 단추에 붙는다(화면 반대편에서 열리면 눈이 길을 잃는다).
        ChapterGraph.ChapterAddCenterButton.Click += (_, _) =>
            UiGuard.Run(_session, "새 챕터", () => ShowAddChapterFlyout(ChapterGraph.ChapterAddCenterButton));
        // 프로젝트가 아직 없을 때는 [새 프로젝트]가 먼저다 (2026-08-26 소유자) —
        // 툴바의 [새로 만들기]와 같은 길 하나를 지난다.
        ChapterGraph.ProjectNewCenterButton.Click += OnNewClick;
        NewButton.Click += OnNewClick;
        OpenButton.Click += OnOpenClick;
        SaveButton.Click += OnSaveClick;
        SaveAsButton.Click += OnSaveAsClick;
        UndoButton.Click += (_, _) => _session.Editor.Undo();
        RedoButton.Click += (_, _) => _session.Editor.Redo();
        OpenExportFolderButton.Click += (_, _) =>
            UiGuard.Run(_session, "내보낸 폴더 열기", OpenExportFolder);

        Opened += OnOpened;

        // 저장 단축키 (W49) — 어디에 포커스가 있어도 Ctrl+S가 저장이다.
        //
        // ⛔ <b>Ctrl+Z가 없었다</b> (2026-09-18에 알았다). 되돌리기는 <b>단추로만</b> 됐는데,
        //    그 단추는 창 위쪽 띠에 있어서 트리에서 실수로 지운 사람이 찾아 올라가야 했다.
        //    지우는 단추를 세우면서 소유자가 짚은 조건이 *"ctrl+z로 롤백이 가능해야"*이고,
        //    그것이 이미 되는 줄 알았던 것이 문제였다.
        AddHandler(KeyDownEvent, (_, args) =>
        {
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

                // ⚠ 글 입력칸에서는 <b>가로채지 않는다</b> — 거기서 Ctrl+Z는 <b>타이핑</b>을
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

        // 10분 자동 저장 (W49) — 저장 경로가 있고 바뀐 것이 있을 때만, 상태줄로 알린다.
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
    /// (⛔ 연출 노드 편집기 · 에셋 탐색기는 2026-10-29에 무대와 함께 ked-story-compositor로 갔다.)
    /// </summary>
    private Control BuildSidePanel()
    {
        return new Panel
        {
            Children = { DialogueEditor, EmptyText }
        };
    }

    /// <summary>
    /// 검증용 손잡이 — 최근 프로젝트 복원을 끈다. 헤드리스 테스트의 창은 "이 컴퓨터가
    /// 마지막에 연 프로젝트"를 물려받으면 안 된다: 복원된 챕터의 감시자가 부르는 에피소드
    /// 동기화가 테스트가 세운 노드를 솎아 내고 판을 다시 그려, 부하에 따라 매번 다른
    /// 테스트가 흔들렸다(2026-08-26 — 더블클릭 넷 사이에 판이 재건되면 제스처도 끊긴다).
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

                // 카드 글 칸에서 고친 글이 곁기둥에도 바로 비친다(2026-10-30). 곁기둥에서 쓰는 중이면(초점이 그 안에 있다)
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
    /// [＋ 챕터]. ⛔ <b>창구도 규칙도 여기 없다</b> — <see cref="ChapterAddFlyout"/>와
    /// <see cref="AuthoringSession.CreateChapter"/>가 갖는다 (2026-09-16에 [대본] 탭에도
    /// 같은 단추가 서면서 한 벌로 모았다). 여기 남은 것은 <b>만든 뒤 이 화면이 할 일</b>뿐이다.
    /// </summary>
    private void ShowAddChapterFlyout(Control anchor) =>
        ChapterAddFlyout.ShowAt(anchor, _session, chapterId =>
        {
            ChapterGraph.Refresh();
            ChapterGraph.SelectChapter(chapterId);
        });

    /// <summary>
    /// 하이어아키를 다시 세운다 (2026-10-30 — 챕터 목록 자리를 이었다, docs/plans/hierarchy.md). 원천은 챕터 그래프 뷰가 읽은
    /// 챕터 목록 하나다. 고른 것은 하이어아키가 들고 있다가 다시 세운 뒤에도 잇는다. 챕터가 없는 판은 하이어아키에 없다 —
    /// 소유자 확정("완전 교체").
    /// </summary>
    private void RebuildFileList()
    {
        ChapterGraph.HierarchyView.Rebuild(ProjectDisplayTitle(), _chapters);
    }

    /// <summary>창 머리와 하이어아키 맨 위 줄이 같은 이름을 쓴다.</summary>
    private string ProjectDisplayTitle() => _session.ProjectPath is null
        ? _session.Project.Title
        : ProjectDisplayName(_session.ProjectPath);

    /// <summary>
    /// 챕터 행 우클릭 — 이름 바꾸기와 <b>제거</b>. 둘 다 챕터·판·조건 배관을
    /// 함께 옮기거나 걷는다(<see cref="ChapterRenamer"/> · <see cref="ChapterDeleter"/>).
    ///
    /// ⛔ 제거는 2026-08-25에 생겼다. 그전에는 "폴더에서 사람이 지우세요"였는데, 그 길은
    /// <b>절반만 지운다</b> — 파일은 사라져도 판과 노드가 프로젝트에 남아 이름이 겹치는
    /// 번들로 뒤늦게 터졌다. 사람이 손으로 못 지우는 쪽이 남는 것이 문제였다.
    /// </summary>
    private void ShowChapterContextFlyout(Control anchor, string chapterId)
    {
        var panel = new StackPanel { Spacing = 4, MinWidth = 220 };

        var name = new TextBox { Text = chapterId, FontSize = 11 };
        panel.Children.Add(name);

        var flyout = new Flyout { Content = panel };

        var rename = new Button { Content = "이름 바꾸기", HorizontalAlignment = HorizontalAlignment.Stretch };
        rename.Click += (_, _) => UiGuard.Run(_session, "챕터 이름 바꾸기", () =>
        {
            string newId = name.Text?.Trim() ?? string.Empty;

            if (newId.Length == 0 || string.Equals(newId, chapterId, StringComparison.Ordinal))
            {
                flyout.Hide();
                return;
            }

            // ⚠ 개명은 <b>여러 자리를 함께</b> 옮기는 한 동작이다(챕터·판·조건 배관). 셸이 그중
            // 일부만 알던 시절에 나머지가 뒤처졌다 — 그 주인은 이제 ChapterRenamer 하나다
            // (2026-08-24 소유자 보고).
            ChapterRenamer.Result result =
                ChapterRenamer.Rename(_session.Editor, _session.ProjectPath, chapterId, newId);

            if (!result.Renamed)
            {
                _session.SetStatus(result.Failure!);
                return;
            }

            ChapterGraph.Refresh();
            ChapterGraph.SelectChapter(newId);
            flyout.Hide();

            _session.SetStatus(
                $"챕터 '{chapterId}' → '{newId}'로 바꿨습니다" +
                (result.SupplyRenamed ? "(조건 노드도 함께)." : ".") +
                (result.StaleExport is { } stale
                    ? $" ⚠ 옛 이름의 내보내기 '{stale}'가 남아 있습니다 — 지우고 다시 내보내 주세요."
                    : " 내보낸 JSON이 있었다면 다시 내보내 주세요."));
        });
        panel.Children.Add(rename);
        panel.Children.Add(MenuSeparator());
        panel.Children.Add(BuildChapterRemoveButton(flyout, chapterId));

        flyout.ShowAt(anchor);
        name.SelectAll();
        name.Focus();
    }

    /// <summary>
    /// [챕터 제거] — <b>두 번 눌러야 지워진다</b>. 첫 누름은 무엇이 함께 사라지는지를
    /// 그 자리에서 말하고, 그 문장을 읽은 손이 한 번 더 누른다.
    ///
    /// 확인 창을 따로 띄우지 않는 이유는 창이 사실을 <b>덮기</b> 때문이다 — 이 판에는
    /// 방금 읽던 챕터 이름과 이름 칸이 함께 있어서, 여기서 묻는 편이 무엇을 지우는지
    /// 더 분명하다. 되돌릴 자리(되돌리기)도 같은 문장에서 말한다.
    /// </summary>
    private Control BuildChapterRemoveButton(Flyout flyout, string chapterId)
    {
        var remove = new Button
        {
            Content = "챕터 제거…",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 60, 60))
        };

        var caution = new TextBlock
        {
            Text = $"'{chapterId}'의 에피소드와 연출 그래프의 노드가 함께 사라집니다. " +
                   "되돌리기(Ctrl+Z)로 돌아옵니다.",
            FontSize = 10,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
            Margin = new Thickness(0, 2, 0, 0)
        };

        bool armed = false;

        remove.Click += (_, _) =>
        {
            if (!armed)
            {
                armed = true;
                remove.Content = "정말 제거 — 한 번 더";
                caution.IsVisible = true;
                return;
            }

            UiGuard.Run(_session, "챕터 제거", () =>
            {
                ChapterDeleter.Result result =
                    ChapterDeleter.Delete(_session.Editor, _session.ProjectPath, chapterId);

                if (!result.Deleted)
                {
                    _session.SetStatus(result.Failure!);
                    return;
                }

                ChapterGraph.Refresh();
                RefreshShell();
                flyout.Hide();

                _session.SetStatus(
                    $"챕터 '{chapterId}'를 지웠습니다(연출 노드 {result.NodesRemoved}개도 함께). " +
                    "되돌리기(Ctrl+Z)로 돌아옵니다." +
                    (result.StaleExport is { } stale
                        ? $" ⚠ 내보낸 '{stale}'가 남아 있습니다 — 지우지 않으면 게임이 " +
                          "없는 챕터를 계속 싣습니다."
                        : string.Empty));
            });
        };

        return new StackPanel { Spacing = 2, Children = { remove, caution } };
    }

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _session.NewProject();

            // 새 프로젝트는 곧장 한 번 저장한다 (W49) — 저장돼야 프로젝트 폴더가 확정되고
            // 에셋 폴더·기본 튜닝 살림(W48)이 준비된다.
            await SaveAsAsync();

            if (_session.ProjectPath is null)
            {
                _session.SetStatus("저장을 건너뛰었습니다 — 저장하면 에셋 폴더와 기본 튜닝이 준비됩니다.");
            }
        }
        catch (Exception exception)
        {
            Report("새 프로젝트", exception);
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            IStorageProvider? storage = GetTopLevel(this)?.StorageProvider;

            if (storage is null || !storage.CanOpen)
            {
                _session.SetStatus("이 환경에서는 파일 선택 창을 열 수 없습니다.");
                return;
            }

            IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "VnTool 프로젝트 열기",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { ProjectFileType() }
                });

            if (files.Count > 0)
            {
                _session.Open(files[0].Path.LocalPath);
            }
        }
        catch (Exception exception)
        {
            Report("프로젝트 열기", exception);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // 카드 글 칸에 넣지 않은 글이 있으면 먼저 넣는다 — Ctrl+S도 넣는 때다(D4).
            Graph.FlushWriting();

            if (_session.ProjectPath is null)
            {
                await SaveAsAsync();
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

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Graph.FlushWriting();
            await SaveAsAsync();
        }
        catch (Exception exception)
        {
            Report("저장", exception);
        }
    }

    private async Task SaveAsAsync()
    {
        IStorageProvider? storage = GetTopLevel(this)?.StorageProvider;

        if (storage is null || !storage.CanSave)
        {
            _session.SetStatus("이 환경에서는 저장 창을 열 수 없습니다.");
            return;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "VnTool 프로젝트 저장",
                SuggestedFileName = "project" + ProjectManifestJson.FileExtension,
                FileTypeChoices = new[] { ProjectFileType() }
            });

        if (file is not null)
        {
            _session.Save(file.Path.LocalPath);
            RefreshShell();
        }
    }

    // ── 내보내기 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 진행 JSON이 나가는 <c>exported/</c>를 연다 (2026-08-25).
    ///
    /// 이 파일들은 <b>사람 손을 안 기다리고 저절로 나간다</b>(2026-08-17). 그래서 사람이
    /// 할 일은 만드는 것이 아니라 <b>가서 꺼내 오는 것</b>뿐인데, 그 자리를 몰라 못 꺼내
    /// 가는 것이 실제 걸림돌이었다 — 비개발자 작가에게 건네는 준비의 일부다.
    ///
    /// ⚠ 폴더를 <b>대신 만들지 않는다.</b> 없다는 것은 아직 한 챕터도 안 나갔다는 뜻이고,
    /// 빈 폴더를 열어 주면 "나갔는데 비었다"로 읽힌다 — 사유를 말하는 편이 낫다.
    /// </summary>
    private void OpenExportFolder()
    {
        if (_session?.ProjectPath is not { } projectPath)
        {
            _session?.SetStatus("프로젝트를 먼저 저장해야 내보낼 자리가 정해집니다.");
            return;
        }

        string folder = Path.Combine(
            Path.GetDirectoryName(projectPath)!, ChapterExportService.ExportFolderName);

        if (!Directory.Exists(folder))
        {
            _session.SetStatus(
                $"아직 나간 진행 JSON이 없습니다 — 검증을 통과한 챕터가 생기면 저절로 " +
                $"여기 나갑니다: {folder}");
            return;
        }

        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private static FilePickerFileType ProjectFileType()
    {
        return new FilePickerFileType("VnTool 프로젝트")
        {
            // 판 파일(*.vnstory.json)은 열 수 없다 — 프로젝트는 매니페스트로 연다.
            Patterns = new[] { "*.vnproject.json", "*.json" }
        };
    }

    // ── 화면 ────────────────────────────────────────────────────────────────



    private static Control MenuSeparator() => new Border
    {
        Height = 1,
        Margin = new Thickness(4, 2),
        Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128))
    };

    // [편집 자료] 현황판(RebuildResourcePanel)은 2026-08-21에 사라졌다 (소유자) —
    // 대본·발행 결과·연출 공급을 읽기 전용으로 나열하던 판인데, 발행·배선이 자동이
    // 된 뒤로는 볼 이유가 없다. 대본은 노드 카드가, 짝은 내보내기가 말한다.

    private void ShowSelectedNode()
    {
        StoryNode? node = _session.SelectedNode;

        DialogueEditor.IsVisible = node is DialogueNode;
        EmptyText.IsVisible = node is null;
        EmptyText.Text = "노드를 선택하면 여기서 편집합니다.";

        if (node is DialogueNode)
        {
            DialogueEditor.Show(node.Id);
        }
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

        UndoButton.IsEnabled = _session.Editor.CanUndo;
        RedoButton.IsEnabled = _session.Editor.CanRedo;

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
