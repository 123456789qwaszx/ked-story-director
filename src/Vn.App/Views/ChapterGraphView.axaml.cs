using System.Diagnostics;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using IoPath = System.IO.Path;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Definition;
using Vn.Authoring.Model;

namespace Vn.App.Views;

/// <summary>
/// 챕터·에피소드 그래프 뷰 (G4·G5). <b>별도 화면이고 기존 대사·연출 그래프는 손대지 않는다</b> (G-1).
///
/// ⛔ <b>~~편집은 전부 엑셀 셀 쓰기다 (G-2 v2)~~</b> — 두 번 뒤집혔다. R-F(2026-09-16)가
/// 원본을 엑셀에서 프로젝트로 옮겼고(워크북은 그 뒤로 산출물이다), v4(2026-09-18)가 자리를
/// 사람에게 돌려줬다. 이 화면의 편집은 <c>ProjectEditor</c>를 거치고, 되돌리기도 거기서 온다.
///
/// <b>자리의 주인은 에피소드 제 X·Y다 (v4, 2026-09-18 소유자:</b> *"챕터그래프도 연출그래프와
/// 동일하게 자유롭게 노드의 위치를 조절할 수 있게"*). <see cref="ChapterBranchPlanner"/>는
/// 이제 <b>그리는 자리가 아니라 첫 자리</b>를 낸다 — 자세한 것은 그쪽 주석에 있다.
///
/// 오류가 있어도 읽힌 데까지 그린다. 빈 화면 + "오류"보다, 그려진 그래프 옆에 무엇이 어디서
/// 잘못됐는지 세워 두는 편이 고칠 자리를 알려 준다(규칙 14).
/// </summary>
public partial class ChapterGraphView : UserControl
{
    private const double CardWidth = 190;
    private const double CardHeight = 74;
    private const double CanvasMargin = 60;

    private readonly List<ChapterEntry> _entries = new();

    /// <summary>선택된 챕터의 구조 검증 + 도달성 증명 결과 (G7). 다시 읽을 때 갱신된다.</summary>
    private ChapterValidationResult? _validation;

    /// <summary>`스탯변화` 줄 편집기 둘 — 간선 패널의 것과 에피소드 노드 폼 안의 것.</summary>
    private readonly StatChangeEditor _edgeStats = new();
    private readonly StatChangeEditor _formStats = new();

    private AuthoringSession? _session;
    private string? _selectedChapterId;
    private bool _updatingCombo;

    /// <summary>챕터 목록이 다시 읽혔다. 왼쪽 패널(MainWindow)이 이걸 듣고 목록을 다시 그린다.</summary>
    internal event Action<IReadOnlyList<ChapterEntry>>? EntriesReloaded;

    /// <summary>프로젝트를 다시 읽어 판을 다시 세운다. 새 챕터를 만든 직후 부른다.</summary>
    internal void Refresh() => Reload();

    /// <summary>
    /// 판을 실제로 다시 만든 횟수. <see cref="ValidationComputeCount"/>와 같은 종류의 창이다 —
    /// 그리기 자체는 싸지만, 다시 만드는 순간 <b>사람이 누르고 있던 카드가 사라진다</b>.
    /// </summary>
    internal int CanvasDrawCount { get; private set; }

    /// <summary>
    /// 챕터를 고른다 — <b>어디서 골랐든 이 길 하나를 지난다</b> (왼쪽 목록 클릭 · 위
    /// 드롭다운 · 코드).
    ///
    /// 2026-08-17 소유자 보고 둘의 뿌리가 여기였다. 드롭다운은 이 길을 안 지나고
    /// <c>Draw()</c>만 불렀다: ① 검증을 다시 안 돌려 <b>이전 챕터의 결과</b>로 그렸고 —
    /// 그래서 도달 불가 빨간 테두리가 남고 도착 스탯이 안 보였다(Ctrl+S가 세션을 흔들어
    /// 다시 읽히면 그제서야 맞았다) ② 판을 활성으로 바꾸지 않아 <b>왼쪽 챕터 목록의
    /// 강조가 따라오지 않았다</b>.
    /// </summary>
    internal void SelectChapter(string chapterId)
    {
        if (string.Equals(_selectedChapterId, chapterId, StringComparison.Ordinal))
        {
            return;
        }

        _selectedChapterId = chapterId;
        _updatingCombo = true;
        ChapterCombo.SelectedItem = chapterId;
        _updatingCombo = false;

        // 고른 챕터의 선택은 이전 챕터의 것이 아니다 — 들고 있으면 없는 간선을 가리킨다.
        _selectedEpisodeId = null;
        _selectedEdgeKey = null;
        HideEdgeForm();

        Validate();
        Draw();

        // ⛔ 여기서 대본을 따라잡던 호출은 2026-09-16에 걷혔다 (R-D). 챕터를 고르는 것은
        //    <b>보는 일</b>이지 들여오는 일이 아니다 — 고를 때마다 워크북을 다시 읽으면
        //    "임포트 뒤 다시 읽지 않는다"(§5.2)가 성립하지 않는다. (그 [대본 가져오기]도
        //    2026-10-29에 엑셀 연동과 함께 걷혔다 — 대본은 연출 그래프 카드의 글 칸에서 쓴다.)

        // 그 챕터의 판을 활성으로 — 왼쪽 목록의 강조가 이 값을 본다.
        ChapterSelected?.Invoke(chapterId);
    }

    /// <summary>
    /// 챕터가 골라졌다. 셸(MainWindow)이 듣고 그 챕터의 판을 활성으로 바꾼다 — 판을
    /// 만드는 일은 셸의 몫이라(<c>EnsureChapterBoard</c>) 이 뷰가 직접 하지 않는다.
    /// </summary>
    internal event Action<string>? ChapterSelected;

    // ── 챕터 목록 자리 (2026-08-22 소유자 — 창 맨 왼쪽 열에서 이 기둥 위로 이사) ──────
    //
    // 자리만 이 뷰가 내주고 <b>내용은 셸(MainWindow)이 짓는다</b>. 목록의 클릭 하나가
    // 세션 전환(EnsureChapterBoard)·우클릭 메뉴와 얽혀 있어 여기로 옮기면
    // 같은 배선이 두 벌이 된다 — 그 사본이야말로 이 코드베이스가 가장 경계하는 것이다.

    /// <summary>챕터 줄이 쌓이는 자리.</summary>
    internal Panel ChapterListHost => ChapterListPanel;

    /// <summary>[＋] 새 챕터 — 셸이 클릭을 받고 플라이아웃의 과녁으로도 쓴다.</summary>
    internal Button ChapterAddButton => AddChapterButton;

    /// <summary>
    /// 판 한가운데의 [＋ 챕터] — 프로젝트는 있는데 챕터가 하나도 없을 때만 보인다.
    /// 클릭의 뜻은 우측 기둥의 [＋]와 같으므로 배선도 같은 셸이 짓는다.
    /// </summary>
    internal Button ChapterAddCenterButton => EmptyAddChapterButton;

    /// <summary>
    /// 판 한가운데의 [새 프로젝트] — 프로젝트가 아직 없을 때만 보인다(챕터보다 먼저다).
    /// 클릭의 뜻은 툴바의 [새로 만들기]와 같으므로 배선도 같은 셸이 짓는다.
    /// </summary>
    internal Button ProjectNewCenterButton => EmptyNewProjectButton;


    /// <summary>머리글 아래 한 줄 — 지금 활성인 판의 요약.</summary>
    internal TextBlock ChapterSummaryText => ChapterSummary;

    public ChapterGraphView()
    {
        InitializeComponent();

        // ⛔ [다시 읽기] 단추는 2026-08-24에 없앴다 (소유자: 한 번도 안 썼다). 감시자가
        // 붙은 뒤로 화면이 스스로 따라오므로 손잡이가 할 일이 남지 않았다. <see cref="Reload"/>
        // 자체는 그대로다 — 부르는 곳이 사람 손에서 감시자와 챕터 전환으로 옮겨갔을 뿐이다.
        // ⛔ [폴더 열기]도 2026-08-26에 없앴다 (소유자) — 노드 더블클릭이 엑셀을 여는
        // 길이 이미 있어, 폴더로 가는 손잡이는 상시로 자리만 먹었다.

        // [화자] 탭 — 프로젝트 전체의 캐스트 (2026-08-23). 엔터로도 더한다: 이름을 여럿
        // 적을 때 손이 마우스로 갔다 오지 않는다.
        SpeakerAddButton.Click += (_, _) => UiGuard.Run(_session, "화자 추가", AddSpeaker);
        SpeakerNameBox.KeyDown += (_, args) => CommitOnEnter(args, AddSpeaker);
        SpeakerCharacterIdBox.KeyDown += (_, args) => CommitOnEnter(args, AddSpeaker);

        ChapterCombo.SelectionChanged += (_, _) =>
        {
            if (_updatingCombo)
            {
                return;
            }

            // 드롭다운도 왼쪽 목록 클릭과 같은 길을 지난다 (2026-08-17) — 여기서
            // Draw()만 부르던 것이 "빨간 테두리가 안 사라진다"의 정체였다.
            if (ChapterCombo.SelectedItem is string picked)
            {
                UiGuard.Run(_session, "챕터 선택", () => SelectChapter(picked));
            }
        };


        // 편집 — 전부 편집기 명령으로 프로젝트에 가고, 세션의 변경 알림이 다시 읽어 화면이 따라온다
        // (⛔ G-2 v2의 "엑셀 셀에 쓰고 저장 감시가 다시 읽는다"는 R-F · D2에 걷혔다).
        // 2026-08-16 소유자 — [개명]·[적용] 단추 폐지: Id는 Enter로 개명하고, 조건 콤보는
        // 고르는 순간 저장된다. 편집은 늘 열려 있다.
        IdBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                UiGuard.Run(_session, "에피소드 개명", RenameSelectedEpisode);
            }
        };
        SceneIdBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                UiGuard.Run(_session, "장면ID 저장", ApplyEpisodeFromPanel);
            }
        };
        ApplySceneGroupButton.Click += (_, _) =>
            UiGuard.Run(_session, "장면ID 일괄 저장", ApplySceneIdToGroup);
        // 판 다루기 — 휠 확대·축소, 가운데 단추 끌어 이동. 휠은 <b>터널</b>로 받는다:
        // 스크롤뷰가 먼저 먹어 버리면 휠이 배율이 아니라 그냥 스크롤이 된다.
        GraphScroll.AddHandler(
            PointerWheelChangedEvent, OnGraphWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        GraphScroll.PointerPressed += OnGraphPanPressed;
        GraphScroll.PointerMoved += OnGraphPanMoved;
        GraphScroll.PointerReleased += OnGraphPanReleased;
        ZoomResetButton.Click += (_, _) => ApplyZoom(1, null);

        // 스탯변화 줄 편집기를 두 자리에 꽂는다 — 간선 패널과 에피소드 노드의 선택지 폼.
        EdgeStatsHost.Children.Add(_edgeStats);
        EdgeFormStatsHost.Children.Add(_formStats);

        AddNextEdgeButton.Click += (_, _) => UiGuard.Run(_session, "선택지 추가", AddChoiceSlotFromPanel);
        AddEdgeButton.Click += (_, _) => UiGuard.Run(_session, "간선 연결·수정", SubmitEdgeForm);
        DeleteEpisodeButton.Click += (_, _) => UiGuard.Run(_session, "에피소드 삭제", DeleteSelectedEpisode);
        AddEpisodeButton.Click += (_, _) => UiGuard.Run(_session, "에피소드 추가", AddEpisodeFromToolbar);
        AutoArrangeButton.Click += (_, _) => UiGuard.Run(_session, "자동 정렬", AutoArrange);
        // 빈 판 한가운데의 [＋ 에피소드]도 같은 길이다 — 선택이 없으니 홀로 선다.
        EmptyAddEpisodeButton.Click += (_, _) => UiGuard.Run(_session, "에피소드 추가", AddEpisodeFromToolbar);
        EdgeDeleteButton.Click += (_, _) => UiGuard.Run(_session, "간선 삭제", DeleteSelectedEdge);

        // 간선 패널은 고르는 순간 저장된다 (2026-08-17 소유자: "굳이 적용을 누르지 않아도
        // 바로 반영되도록"). 에피소드 패널의 개명이 Enter로 확정되는 것과 같은 감각이다 —
        // [적용]이라는 문턱이 하나 더 있으면 "고쳤는데 왜 그대로지"가 생긴다.
        //
        // 글자 칸만 초점을 잃을 때 낸다. 자판 하나마다 저장하면 되돌리기가 글자 수만큼 쪼개지고,
        // 그 사이의 다시 그리기가 칸을 다시 채워 쓰던 글을 끊는다.
        EdgeLabelEditBox.SelectionChanged += (_, _) => AutoSaveEdge();
        EdgeAutoCheck.IsCheckedChanged += (_, _) => AutoSaveEdge();
        EdgeVisibleCombo.SelectionChanged += (_, _) => AutoSaveEdge();
        EdgeConditionCombo.SelectionChanged += (_, _) => AutoSaveEdge();
        BranchConditionCombo.SelectionChanged += (_, _) =>
            UiGuard.Run(_session, "분기 조건", SaveBranchCondition);

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
            await UiGuard.RunAsync(_session, "보고 복사", CopyDiagnosticsAsync);

        // 대사 접기 — 토글이 곧 제목이다(상자 없이). 삼각형이 상태를 말한다
        // (▸ 접힘 · ▾ 펼침).
        DialogueToggle.IsCheckedChanged += (_, _) =>
        {
            bool open = DialogueToggle.IsChecked == true;
            DialoguePreviewText.IsVisible = open;
            DialogueToggle.Content = open ? "▾  대사" : "▸  대사";
        };

        // 펼친 채로 시작한다 (2026-08-24 소유자: "대사 미리보기는, 펼쳐둔 걸 디폴트로").
        // ⚠ XAML에서 IsChecked를 켜지 않는다 — 그러면 위 핸들러가 붙기 <b>전에</b> 켜져
        // 글자와 본문이 접힌 모양 그대로 남는다. 여기서 켜야 셋이 한 번에 맞는다.
        DialogueToggle.IsChecked = true;

        // 처음 보이는 탭은 [편집]이다 (2026-08-16 소유자) — 손이 가는 곳은 지금 고른
        // 하나이지 챕터 전체 표가 아니다. 그 뒤로는 사람이 고른 탭이 그대로 유지된다.
        RightTabs.SelectedItem = EditTab;

        // 빈 판 클릭 = 선택 해제. 카드·간선·라벨 핸들러가 각자 누름을 소비하므로(e.Handled)
        // 여기까지 흘러오는 왼쪽 누름은 진짜 빈 공간뿐이다.
        GraphCanvas.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(GraphCanvas).Properties.IsLeftButtonPressed)
            {
                SelectEpisode(null);
            }
        };

        // 카드 끌기 (v4) — 누름은 카드가 받고(e.Handled), 움직임과 놓음은 여기로 올라온다.
        // 연출 그래프와 같은 자리에 같은 손놀림이다.
        GraphCanvas.PointerMoved += OnCanvasPointerMoved;
        GraphCanvas.PointerReleased += OnCanvasPointerReleased;
    }

    internal void Attach(AuthoringSession session)
    {
        DetachSession();   // 두 번 붙이면 구독도 감시도 두 벌이 된다

        _session = session;
        _sessionChanged = (_, _) => QueueReload();
        session.Changed += _sessionChanged;
        Reload();
    }

    /// <summary>세션 구독을 끊을 수 있도록 들고 있는다 — 람다를 바로 걸면 뗄 수가 없다.</summary>
    private EventHandler<Vn.Authoring.Editing.ProjectChangedEventArgs>? _sessionChanged;

    /// <summary>
    /// 세션에서 손을 뗀다 — 구독을 끊는다.
    ///
    /// <b>왜 필요한가</b> (2026-08-18): 이 뷰는 붙기만 하고 떨어질 줄을 몰랐다. 앱에서는
    /// 뷰가 프로세스와 수명이 같아 티가 안 나지만, 테스트에서는 한 어셈블리가 <b>디스패처
    /// 하나</b>를 나눠 쓴다. 창을 안 닫고 끝낸 테스트의 뷰가 그대로 살아 있고, 그 감시자는
    /// 이미 지워진 임시 폴더를 본다.
    ///
    /// 그 다음이 나쁘다 — 250ms 디바운스가 <b>타이머 스레드에서</b> 깨어나 없어진 디스패처에
    /// <c>Post</c>를 하면 <c>NullReferenceException</c>이 나고, 그 자리는 잡을 사람이 없어
    /// <b>프로세스가 내려간다.</b> 죽는 순간 돌고 있던 테스트가 실패로 찍히므로 매번 다른
    /// 이름이 나왔고, 혼자 돌리면 통과했다.
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

        _sessionChanged = null;
        _session = null;
    }

    /// <summary>이미 예약된 재읽기가 있는가. 한 번의 UI 차례에 하나만 돈다.</summary>
    private bool _reloadQueued;

    /// <summary>
    /// 재읽기 예약 — <b>몰려 오는 변경을 한 번으로 합친다</b> (2026-08-18).
    ///
    /// 동기화 한 번이 프로젝트 변경을 수십 개 낸다(에피소드마다 노드·줄이 붙는다).
    /// 예전에는 그 하나하나가 재읽기를 예약했고, 그 한 번이
    /// 챕터 전부를 다시 읽고 진행 JSON을 쓰고 판을 통째로 다시 그렸다.
    /// 노드 60개에서 <b>123번</b> 돌아 58초가 됐다 — 마지막 한 번 말고는 전부 버려질
    /// 그림이었다.
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

    // ⛔ 여기 있던 「읽기」 묶음 — [챕터 가져오기]·[대본 가져오기]·구판 프로젝트의 최초 1회 들여오기 ·
    //    구판 `화자` 시트 옮기기 — 은 2026-10-29(D2)에 엑셀 연동과 함께 걷혔다. 그 안에 숨어 있던
    //    엑셀 아닌 몫(빈 카드 · 조건 공급 · 착지 카드 출구 경고)은 그보다 먼저 제자리로 갔다
    //    (`ProjectEditor.AlignBoardsWithChapters` · 조건 편집 명령 · `BoardWarnings`).

    // ── [화자] 탭 = 프로젝트의 캐스트 (2026-08-23) ──────────────────────────
    //
    // 소유자: "챕터 엑셀을 눌러보면, 엑셀 내 어떤 것에서도 화자를 사용하지 않는다 … 화자는
    // 툴 내부에서, 직접 정의해서 쓰는 게 맞는 것이였다." 확인해 보니 그대로였다 — `조건`
    // 시트는 간선의 표시조건·해금조건이 실제로 가리키지만 `화자` 시트는 어느 시트도, 검증도,
    // 도달성 증명도, 내보내기도 안 봤다. 툴이 대본 드롭다운에 쓰려고 남의 파일에 얹어 둔
    // 사전이었고, 그래서 <b>챕터마다 따로 적어야 하는 값</b>만 치렀다.
    //
    // 이제 등록 창구는 이 탭 하나이고 값은 `game.definition.json`의 speakers에 산다
    // (초상화 매핑과 같은 배열 — 화자의 집이 하나라는 뜻이다).

    /// <summary>[화자] 탭을 지금 정의 파일의 목록으로 다시 그린다.</summary>
    private void RebuildSpeakerTab()
    {
        SpeakerListPanel.Children.Clear();

        if (_session is null)
        {
            return;
        }

        IReadOnlyList<SpeakerSpec> speakers = _session.Definition.Speakers;

        if (speakers.Count == 0)
        {
            SpeakerListPanel.Children.Add(new TextBlock
            {
                Text = "아직 없습니다. 위에 이름을 적고 [＋ 추가]를 누르면 모든 챕터의 대본에서 " +
                       "그 화자를 고를 수 있습니다.",
                FontSize = 10,
                Opacity = 0.55,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        for (int index = 0; index < speakers.Count; index++)
        {
            SpeakerListPanel.Children.Add(BuildSpeakerRow(index, speakers[index]));
        }
    }

    /// <summary>
    /// 화자 한 줄 — `[이름][캐릭터키][✕]`. 고치면 그 자리에서 정의 파일에 저장된다
    /// ([적용] 없음 — 챕터 편집 탭과 같은 규칙, 2026-08-17 소유자).
    /// </summary>
    private Control BuildSpeakerRow(int index, SpeakerSpec speaker)
    {
        var name = new TextBox { Text = speaker.Name, FontSize = 12 };
        var characterId = new TextBox
        {
            Text = speaker.CharacterId,
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 12,
            PlaceholderText = "캐릭터키"
        };

        void Commit()
        {
            List<SpeakerSpec> edited = _session!.Definition.Speakers.ToList();

            if (index >= edited.Count)
            {
                return; // 그 사이 목록이 바뀌었다 — 다음 그리기가 정본이다.
            }

            string typed = (name.Text ?? string.Empty).Trim();

            // 이름을 지워 빈칸으로 만드는 것은 삭제가 아니다 — 삭제는 ✕ 하나뿐이다.
            // 빈 이름을 저장하면 목록에서 조용히 사라져 되돌릴 손잡이가 없어진다.
            if (typed.Length == 0)
            {
                name.Text = edited[index].Name;
                _session.SetStatus("이름은 비울 수 없습니다 — 지우려면 ✕를 누르세요.");
                return;
            }

            string previous = edited[index].Name;

            // 같은 이름이 이미 있으면 개명이 아니라 <b>합치기</b>다 — 목록이 신원이라
            // 두 줄이 같은 이름을 가질 수 없다([＋ 추가]가 막는 것과 같은 규칙).
            if (!string.Equals(previous, typed, StringComparison.Ordinal) &&
                edited.Any(item => string.Equals(item.Name, typed, StringComparison.Ordinal)))
            {
                name.Text = previous;
                _session.SetStatus($"화자 '{typed}'은 이미 있습니다.");
                return;
            }

            // 개명이 먼저다 (2026-08-24) — 이미 쓰인 이름을 끌고 간 뒤 등록부를 간다.
            // 끌고 가는 것은 <b>프로젝트의 줄</b>뿐이다(워크북 재출력은 2026-10-29 D2에 엑셀 연동과
            // 함께 걷혔다).
            string? report = string.Equals(previous, typed, StringComparison.Ordinal)
                ? null
                : CarrySpeakerRename(previous, typed);

            edited[index] = new SpeakerSpec
            {
                Name = typed,
                CharacterId = (characterId.Text ?? string.Empty).Trim()
            };

            SaveSpeakers(edited);

            // 개명 보고는 저장 뒤에 세운다 — 어휘 밀기의 상태 줄이 덮어쓰지 않게.
            if (report is not null)
            {
                _session.SetStatus(report);
            }
        }

        name.LostFocus += (_, _) => UiGuard.Run(_session, "화자 수정", Commit);
        characterId.LostFocus += (_, _) => UiGuard.Run(_session, "화자 수정", Commit);
        name.KeyDown += (_, args) => CommitOnEnter(args, Commit);
        characterId.KeyDown += (_, args) => CommitOnEnter(args, Commit);

        var remove = new Button
        {
            Content = "✕",
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            [ToolTip.TipProperty] = $"'{speaker.Name}'를 목록에서 지웁니다. " +
                                    "이미 대본에 적힌 이름은 그대로 남습니다(화자 칸은 자유 입력)."
        };
        remove.Click += (_, _) => UiGuard.Run(_session, "화자 삭제", () =>
        {
            List<SpeakerSpec> edited = _session!.Definition.Speakers.ToList();

            if (index < edited.Count)
            {
                edited.RemoveAt(index);
                SaveSpeakers(edited);
            }
        });

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto") };
        Grid.SetColumn(name, 0);
        Grid.SetColumn(characterId, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(name);
        row.Children.Add(characterId);
        row.Children.Add(remove);

        return row;
    }

    /// <summary>
    /// 화자 개명을 <b>이미 쓰인 곳까지</b> 끌고 간다 — 프로젝트 대본의 줄들 (2026-08-24 소유자:
    /// "화자의 이름을 편집한 경우도 연결이 계속 이어지도록").
    ///
    /// 등록부만 갈면 그 이름을 쓰던 줄이 전부 <b>미등록</b>이 된다: 드롭다운에서 사라지고,
    /// 초상화 매핑이 끊기고, 공백 있는 이름은 파서가 산문으로 읽어 대사와 합쳐진다.
    /// </summary>
    /// <returns>
    /// 저장 뒤에 세울 상태 줄. 여기서 바로 쓰면 이어지는 어휘 밀기의 메시지가 덮는다.
    /// 말할 것이 없으면 null.
    /// </returns>
    private string? CarrySpeakerRename(string previous, string renamed)
    {
        if (_session is null)
        {
            return null;
        }

        string from = previous.Trim();
        string to = renamed.Trim();

        if (from.Length == 0 || to.Length == 0 || string.Equals(from, to, StringComparison.Ordinal))
        {
            return null;
        }

        int lines = _session.Editor.RenameSpeaker(from, to);

        return lines == 0
            ? null
            : $"화자 '{previous}' → '{renamed}' — 줄 {lines}개가 따라갔습니다.";
    }

    private static void CommitOnEnter(Avalonia.Input.KeyEventArgs args, Action commit)
    {
        if (args.Key == Avalonia.Input.Key.Enter)
        {
            commit();
            args.Handled = true;
        }
    }

    /// <summary>폼의 한 줄을 목록 끝에 더한다. 같은 이름은 거절한다(목록이 신원이다).</summary>
    private void AddSpeaker()
    {
        if (_session is null)
        {
            return;
        }

        string name = (SpeakerNameBox.Text ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            _session.SetStatus("화자 이름을 적어 주세요.");
            return;
        }

        List<SpeakerSpec> edited = _session.Definition.Speakers.ToList();

        if (edited.Any(item => string.Equals(item.Name, name, StringComparison.Ordinal)))
        {
            _session.SetStatus($"화자 '{name}'은 이미 있습니다.");
            return;
        }

        edited.Add(new SpeakerSpec
        {
            Name = name,
            CharacterId = (SpeakerCharacterIdBox.Text ?? string.Empty).Trim()
        });

        if (SaveSpeakers(edited))
        {
            SpeakerNameBox.Text = string.Empty;
            SpeakerCharacterIdBox.Text = string.Empty;
            SpeakerNameBox.Focus();
            _session.SetStatus($"화자 '{name}'을 더했습니다 — 모든 챕터의 대본에서 고를 수 있습니다.");
        }
    }

    /// <summary>목록을 정의 파일에 통째로 쓰고 [화자] 탭을 다시 그린다.</summary>
    private bool SaveSpeakers(IReadOnlyList<SpeakerSpec> speakers)
    {
        if (_session is null || !_session.SaveSpeakers(speakers))
        {
            return false;
        }

        RebuildSpeakerTab();

        return true;
    }

    private void Reload()
    {
        // 판은 프로젝트에서 온다 — 파일을 읽지 않는다 (R-F · 2026-09-16, 엑셀 연동은 2026-10-29에 걷혔다).
        _entries.Clear();
        _entries.AddRange(ChapterEntries());

        _updatingCombo = true;
        ChapterCombo.ItemsSource = _entries.Select(entry => entry.ChapterId).ToList();

        if (_selectedChapterId is null ||
            _entries.All(entry => entry.ChapterId != _selectedChapterId))
        {
            _selectedChapterId = _entries.FirstOrDefault()?.ChapterId;
        }

        ChapterCombo.SelectedItem = _selectedChapterId;
        _updatingCombo = false;

        RebuildSpeakerTab();

        AutoExport();        // 진행 JSON은 사람 손을 기다리지 않는다 (2026-08-17)
        Validate();
        Draw();              // 못 나갔으면 그 결론이 검증 보고 맨 위에 선다

        EntriesReloaded?.Invoke(_entries);
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
                chapter.ChapterId, chapter.ToGraphModel(chapter.ChapterId, _session.Definition)))
            .OrderBy(entry => entry.ChapterId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 구조 검증 + 도달성 증명 (G7). 모델을 다시 읽을 때마다 한 번 돌려 두고 화면은 그 결과를
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
    /// 증명과 내보내기 (2026-08-23에 이 파일에서 나갔다 — `Vn.Authoring.Chapters`).
    ///
    /// 캐시·지문·거부 장부가 전부 저쪽에 있다. 여기 남은 것은 <b>언제 부르나</b>뿐이고,
    /// 그것이 화면의 일이다. 규칙이 3,835줄 안에 살면 밖에서 보이지 않는다 — 실제로
    /// 물렸다: "동기화는 고른 챕터만, 내보내기는 전 챕터"라는 사실이 여기 묻혀 있어
    /// 저작 관문을 걸려던 시도가 뒤늦게 그것을 알았다.
    /// </summary>
    private readonly ChapterExportService _export = new();

    /// <summary>직전 내보내기에서 못 나간 챕터들.</summary>
    private ChapterExportRun _exportRun = ChapterExportRun.Empty;

    /// <summary>실제로 증명을 돌린 횟수 — 테스트가 "일을 몇 번 했는가"를 보는 창이다.</summary>
    internal int ValidationComputeCount => _export.ValidationComputeCount;

    private ChapterValidationResult ValidationFor(ChapterEntry entry) =>
        _export.ValidationFor(entry, _session?.Project);

    /// <summary>
    /// 툴이 길을 놓을 때 넣어 주는 문구 (v12). 빈 문구는 오류이므로 무언가는 있어야 하고,
    /// 기획자가 바로 고쳐 쓸 수 있는 가장 짧은 말이다.
    /// </summary>
    internal const string DefaultOptionLabel = "계속";

    // ── 판 다루기: 휠 확대·축소, 가운데 단추 끌어 이동 (2026-08-17 소유자) ──
    //
    // 연출 그래프(GraphEditorView)와 같은 손놀림·같은 산식이다. 판이 둘인데 다루는
    // 법이 다르면 손이 매번 헷갈린다.
    //
    // 2026-08-18 팀장 미팅에서 Ctrl 요구가 빠졌다 — 판 위에서 휠은 곧 배율이다.
    // 세로로 훑는 일은 가운데 단추 끌기가 맡는다.

    private const double MinZoom = 0.3;
    private const double MaxZoom = 2.5;

    private double _zoom = 1;
    private bool _panning;
    private Point _panStart;
    private Vector _panStartOffset;

    /// <summary>휠 — 누른 키와 무관하게 배율이다. 스크롤로 새지 않게 여기서 삼킨다.</summary>
    private void OnGraphWheel(object? sender, Avalonia.Input.PointerWheelEventArgs args)
    {
        ApplyZoom(_zoom * (args.Delta.Y > 0 ? 1.15 : 1 / 1.15), args.GetPosition(GraphScroll));
        args.Handled = true;
    }

    /// <summary>배율 적용 — 커서 아래의 내용이 그 자리에 남게 오프셋을 맞춘다.</summary>
    private void ApplyZoom(double zoom, Point? anchor)
    {
        Point pivot = anchor ?? new Point(
            GraphScroll.Viewport.Width / 2, GraphScroll.Viewport.Height / 2);

        // 지금 pivot 아래에 있는 캔버스 좌표.
        var content = new Point(
            (GraphScroll.Offset.X + pivot.X) / _zoom,
            (GraphScroll.Offset.Y + pivot.Y) / _zoom);

        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ZoomHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ZoomText.Text = $"{Math.Round(_zoom * 100)}%";
        ZoomResetButton.IsVisible = Math.Abs(_zoom - 1) > 0.001;
        ZoomHost.UpdateLayout(); // 새 크기를 알아야 오프셋 상한이 맞는다

        GraphScroll.Offset = new Vector(
            Math.Max(0, (content.X * _zoom) - pivot.X),
            Math.Max(0, (content.Y * _zoom) - pivot.Y));
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
    private void AutoExport() =>
        _exportRun = _export.ExportAll(_entries, _session?.ProjectPath, _session?.Project);

    /// <summary>검증 보고 맨 위에 세울 내보내기 결론 — 못 나갔을 때만 있다.</summary>
    private string? ExportNotice() => _exportRun.Notice;

    // ── 그리기 ──────────────────────────────────────────────────────────────

    private void Draw()
    {
        CanvasDrawCount++;

        GraphCanvas.Children.Clear();
        DiagnosticsPanel.Children.Clear();
        _cardById.Clear();
        _cardBase.Clear();
        _lineByEdge.Clear();
        _lineBase.Clear();

        // 그릴 것이 없으면 판도 없다. 이걸 지우지 않으면 큰 챕터를 보다가 못 읽는 챕터로
        // 넘어갔을 때 텅 빈 캔버스가 이전 크기 그대로 남아 스크롤만 넓어진다.
        GraphCanvas.Width = 0;
        GraphCanvas.Height = 0;

        ChapterEntry? entry = _entries.FirstOrDefault(item => item.ChapterId == _selectedChapterId);

        if (entry is null)
        {
            bool saved = _session?.ProjectPath is not null;

            EmptyText.IsVisible = true;
            EmptyText.Text = saved
                ? "아직 챕터가 없습니다.\n[＋ 챕터]로 첫 챕터를 만들면 여기에 판이 섭니다."
                : "아직 프로젝트가 없습니다.\n새 프로젝트를 만들고 저장하면 여기에 챕터 판이 섭니다.";

            // 빈 판의 첫걸음은 둘 중 하나만 선다 (2026-08-26 소유자: "새 프로젝트를
            // 만들지도 않았는데 +챕터버튼부터 나오니 오히려 헷갈려") — 순서가 곧 안내다:
            // 프로젝트가 먼저고 챕터는 그 다음이다. 챕터 단추는 챕터가 하나도 없을 때만이다.
            // 목록에는 있는데 아직 안 고른 경우는 아니다: 그때 할 일은 만들기가 아니라 고르기다.
            EmptyNewProjectButton.IsVisible = !saved;
            EmptyAddChapterButton.IsVisible = saved && _entries.Count == 0;
            EmptyAddEpisodeButton.IsVisible = false;

            DiagnosticsExpander.Header = "검증 보고";
            return;
        }

        EmptyText.IsVisible = false;
        EmptyNewProjectButton.IsVisible = false;
        EmptyAddChapterButton.IsVisible = false;
        EmptyAddEpisodeButton.IsVisible = false;

        ChapterGraphModel model = entry.Model;

        // 셋째 걸음 (2026-08-26 소유자: "챕터를 열었는데, 아무 에피소드가 없을 땐
        // + 에피소드 버튼이 나오도록") — 새 챕터의 에피소드 시트는 머리글뿐이라
        // 판이 그냥 텅 비어 있었다. 안내문과 단추만 세우고 나머지 그리기는 그대로
        // 지나간다 — 그릴 것이 없을 뿐 검증·보고는 여느 챕터와 같다.
        if (model.Episodes.Count == 0)
        {
            EmptyText.IsVisible = true;
            EmptyText.Text = $"'{entry.ChapterId}'에 에피소드가 없습니다.\n첫 에피소드를 세우면 여기에 그려집니다.";
            EmptyAddEpisodeButton.IsVisible = true;
        }

        DrawCanvas(model);

        DrawDiagnostics(model);
        RefreshPropertyPanel(preserveTyping: true);
    }

    /// <summary>
    /// <b>판만</b> 다시 그린다 — 카드·포트·간선. 검증 보고와 속성 패널은 안 건드린다.
    ///
    /// ⭐ <see cref="Draw"/>에서 떼어낸 이유는 <b>드래그</b>다 (v4, 2026-09-18). 카드를 끄는
    /// 동안 매 프레임 필요한 것은 판뿐인데, <c>Draw</c>를 부르면 검증 보고 패널을 통째로
    /// 다시 세우고 속성 패널의 타이핑까지 건드린다 — 끄는 중에 낼 비용도, 낼 이유도 없다.
    ///
    /// ⚠ 자리 계산이 여기 한 벌만 있어야 한다. 끄는 동안과 놓은 뒤가 다른 산식을 쓰면
    /// 놓는 순간 카드가 튄다.
    /// </summary>
    private void DrawCanvas(ChapterGraphModel model)
    {
        GraphCanvas.Children.Clear();
        _cardById.Clear();
        _cardBase.Clear();
        _lineByEdge.Clear();
        _lineBase.Clear();

        // 자리는 사람이 소유한다 (v4) — 에피소드가 제 X·Y를 지고 있고 드래그가 그것을
        // 고친다. 그리기는 <b>덮지 않는다</b>: 덮으면 옮긴 일이 아무 일도 아니게 된다.
        //
        // ⚠ 끄는 중인 카드 하나만 임시 자리(`_dragPosition`)를 쓴다 — 아직 프로젝트에
        //   안 적은 자리다. 놓을 때 한 번 적는다(되돌리기 한 번 = 드래그 한 번).
        _placed = DisplayPlacement(model).ToDictionary(
            pair => pair.Key,
            pair => string.Equals(pair.Key, _draggingEpisodeId, StringComparison.Ordinal) &&
                    _dragPosition is { } dragged
                ? (X: dragged.X + CanvasMargin, Y: dragged.Y + CanvasMargin)
                : (X: pair.Value.X + CanvasMargin, Y: pair.Value.Y + CanvasMargin),
            StringComparer.Ordinal);

        GraphCanvas.Width = _placed.Count == 0
            ? CanvasMargin * 2
            : _placed.Values.Max(position => position.X) + CardWidth + CanvasMargin;
        GraphCanvas.Height = _placed.Count == 0
            ? CanvasMargin * 2
            : _placed.Values.Max(position => position.Y) + CardHeight + CanvasMargin;


        // 에피소드별 보이는 선택지 = 문구가 붙은 나가는 간선들 (v9 — 길 하나가 곧 선택지
        // 하나). 포트 그리기와 간선 그리기가 같은 목록 하나를 본다.
        _optionsByEpisode.Clear();

        foreach (ChapterEpisode episode in model.Episodes)
        {
            // v12 (2026-08-24) — **나가는 길은 전부 포트를 받는다.** 예전에는 문구 없는
            // 간선("보이지 않는 기본")을 걸러 내고 카드 중앙에서 직행선으로 그렸는데,
            // 그 개념이 폐지됐다. 문구가 비어 있으면 그것은 종류가 아니라 **고칠 것**이고,
            // 판에서 그 자리를 보여 줘야 기획자가 고친다.
            _optionsByEpisode[episode.EpisodeId] = model.Edges
                .Where(edge =>
                    string.Equals(edge.FromEpisodeId, episode.EpisodeId, StringComparison.Ordinal))
                .ToList();
        }

        // 간선을 먼저 그려야 노드 카드 아래로 깔린다.
        DrawEpisodeRails(model);
        DrawBranchMarkers();

        foreach (ChapterEpisode episode in model.Episodes)
        {
            DrawEpisode(model, episode);
        }

        ApplySelectionVisuals();
    }

    /// <summary>
    /// <b>분기 표식</b>을 점선으로 그린다 (2026-09-18 소유자: *"챕터 그래프가 보면, 분기는
    /// 선택지로 안 이어 주고 있거든?"*).
    ///
    /// <b>선택지와 다른 종류의 선이다.</b> 문구도 스탯변화도 없고, 누를 수도 없다 — 뚫고 떼는
    /// 것은 연출 그래프의 일이므로 여기서는 <b>읽기만</b> 한다. 점선인 것이 그 뜻이다:
    /// 사람이 고르는 길(실선)이 아니라 <b>반드시 한 번 다녀오는 통로</b>다.
    ///
    /// ⚠ <b>오른쪽으로 나간다</b> (v5 · 2026-09-18 소유자: *"분기는 점선인 상태로, 현재
    /// 선택지처럼 우측으로 붙이되"*) — 연출 그래프와 같은 변이다. 선택지가 아래변의 칸을
    /// 쓰므로 자리도 겹치지 않는다(포트 하나 = 선택지 하나, v9).
    /// </summary>
    private void DrawBranchMarkers()
    {
        if (_session is null || _selectedChapterId is not { } chapterId)
        {
            return;
        }

        _branchByLine.Clear();

        var stroke = new SolidColorBrush(Color.Parse("#7A6FB0"));

        foreach (ChapterBranchMarkers.Marker marker in
                 ChapterBranchMarkers.For(_session.Editor.Project, chapterId))
        {
            if (!_placed.TryGetValue(marker.FromEpisodeId, out (double X, double Y) from) ||
                !_placed.TryGetValue(marker.ToEpisodeId, out (double X, double Y) to))
            {
                continue;
            }

            // 오른변 가운데에서 나가, 도착 카드의 <b>가까운 세로 변</b>으로 들어간다 —
            // 되돌아가는 분기(도착이 왼쪽)도 선이 카드를 관통하지 않는다.
            var start = new Point(from.X + CardWidth, from.Y + (CardHeight / 2));
            bool targetIsRight = start.X <= to.X;

            var line = new Line
            {
                StartPoint = start,
                EndPoint = new Point(
                    targetIsRight ? to.X : to.X + CardWidth,
                    to.Y + (CardHeight / 2)),
                Stroke = stroke,
                StrokeThickness = 1.6,
                StrokeDashArray = new AvaloniaList<double> { 5, 4 },
                IsHitTestVisible = false,
                // 화면 없는 검증이 "무엇이 그려졌는지"를 색·좌표로 역추론하지 않게 한다.
                Tag = $"분기:{marker.FromEpisodeId}->{marker.ToEpisodeId}"
            };

            GraphCanvas.Children.Add(line);
            _branchByLine[marker.LineId] = (marker, line);

            // 1.6px 점선은 사람이 못 누른다 — 간선과 같은 수법으로 굵은 히트 선을 겹친다.
            var hit = new Line
            {
                StartPoint = line.StartPoint,
                EndPoint = line.EndPoint,
                Stroke = Brushes.Transparent,
                StrokeThickness = 14,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            string lineId = marker.LineId;

            hit.PointerPressed += (_, e) =>
            {
                e.Handled = true; // 캔버스(빈 공간 = 선택 해제)까지 흘러가면 곧바로 풀린다
                UiGuard.Run(_session, "분기 선택", () => SelectBranch(lineId));
            };

            GraphCanvas.Children.Add(hit);
        }
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

    /// <summary>에피소드 → 보이는 선택지(문구 붙은 나가는 간선). Draw가 채우고 카드·간선이 함께 본다.</summary>
    private readonly Dictionary<string, List<ChapterEdge>> _optionsByEpisode = new(StringComparer.Ordinal);

    /// <summary>이 에피소드가 낼 칸 수 — 언제나 셋 이상이고, 간선이 더 많으면 그만큼이다.</summary>
    private static int SlotsFor(int optionCount) => Math.Max(ChoiceSlots, optionCount);

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

            int slots = SlotsFor(options.Count);

            for (int index = 0; index < options.Count; index++)
            {
                DrawPortEdge(options[index],
                    new Point(PortX(position.X, index, slots), position.Y + CardHeight + 5));
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
            ? new SolidColorBrush(Color.Parse("#8894A0"))
            : new SolidColorBrush(Color.Parse("#C08A3E"));
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
            UiGuard.Run(_session, "간선 선택", () => SelectEdgeKey(hitFrom, hitTo, hitLabel));
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
    /// 간선의 <b>문구</b>를 그 선 한가운데에 놓는다 — 사람이 간선을 누르려고 겨누는 바로
    /// 그 자리다. 누르면 그 간선이 선택된다.
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
        string option = edge.Auto ? "AUTO"
            : edge.HasNoOptionLabel ? "⚠ 문구 없음"
            : edge.OptionLabel!;

        string label = edge.ConditionLabel is { } gate ? $"{option} [{gate}]" : option;

        var text = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F0F4F6F8")),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse(
                    edge.Auto ? "#3D7BD9" : edge.HasNoOptionLabel ? "#C0392B" : "#5A646E"))
            },
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        string fromId = edge.FromEpisodeId;
        string toId = edge.ToEpisodeId;
        string labelKey = EdgeLabelKey(edge);

        // 히트 선보다 나중에 그려져 클릭을 삼키므로, 라벨 클릭도 간선 선택으로 잇는다.
        text.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Run(_session, "간선 선택", () => SelectEdgeKey(fromId, toId, labelKey));
        };

        text.Measure(Size.Infinity);
        Canvas.SetLeft(text, ((from.X + to.X) / 2) - (text.DesiredSize.Width / 2));
        Canvas.SetTop(text, ((from.Y + to.Y) / 2) - (text.DesiredSize.Height / 2));
        GraphCanvas.Children.Add(text);
    }

    private void RegisterEdgeLines(ChapterEdge edge, List<Line> segments, IBrush stroke, double thickness)
    {
        (string, string, string) key = (edge.FromEpisodeId, edge.ToEpisodeId, EdgeLabelKey(edge));
        _lineByEdge[key] = segments;
        _lineBase[key] = (stroke, thickness);
    }

    private void DrawEpisode(ChapterGraphModel model, ChapterEpisode episode)
    {
        // 모델의 오류든 도달 불가든 노드에는 같은 ⚠로 선다 — 기획자에게는 둘 다 "고칠 것"이다.
        bool hasError = model.EpisodeHasError(episode) || IsUnreachable(episode);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        // 시작 에피소드(첫 행)가 곧 Root다 — 도달성의 씨앗이자 분기 저작의 출발점.
        if (model.IsSceneRoot(episode))
        {
            var root = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#3D7BD9")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 0),
                Child = new TextBlock
                {
                    Text = "SCENE ROOT",
                    FontSize = 9,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White
                }
            };
            ToolTip.SetTip(root, ReferenceEquals(model.StartEpisode, episode)
                ? "시작 에피소드이자 장면 루트입니다. 도달성 증명과 내보내기의 StartEpisodeId가 됩니다."
                : "다른 장면에서 들어오는 장면 루트입니다. 이곳부터 저장·롤백 경계가 새로 시작됩니다.");
            header.Children.Add(root);
        }

        var scene = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#6B5CA5")),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 0),
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(episode.SceneId) ? "SCENE · 기본" : $"SCENE · {episode.SceneId.Trim()}",
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            }
        };
        ToolTip.SetTip(scene, string.IsNullOrWhiteSpace(episode.SceneId)
            ? "장면: 에피소드별 기본 장면"
            : $"장면: {episode.SceneId.Trim()} — 같은 장면ID끼리 저장·롤백 구간을 공유합니다.");
        header.Children.Add(scene);

        // 관문은 v8에서 길(간선)의 것이 됐다 — 카드는 "들어오는 길에 관문이 있다"를 표시한다.
        if (GatedIncoming(model, episode) is { Count: > 0 } gatedPaths)
        {
            var lockMark = new TextBlock { Text = "🔒", FontSize = 11 };
            ToolTip.SetTip(lockMark, GateSummary(gatedPaths));
            header.Children.Add(lockMark);
        }

        if (hasError)
        {
            header.Children.Add(new TextBlock { Text = "⚠", FontSize = 11, Foreground = Brushes.IndianRed });
        }

        bool hasTitle = !string.IsNullOrWhiteSpace(episode.Title);

        header.Children.Add(new TextBlock
        {
            Text = hasTitle ? episode.Title : episode.EpisodeId,
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var body = new StackPanel { Spacing = 1 };
        body.Children.Add(header);

        // 제목이 없으면 굵은 줄이 이미 Id다 — 같은 글자를 두 번 쓰지 않는다.
        if (hasTitle)
        {
            body.Children.Add(new TextBlock
            {
                Text = episode.EpisodeId,
                FontSize = 10,
                Opacity = 0.6,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        // 여기 도착했을 때의 스탯 (2026-08-17 소유자: "간선을 따라 왔을 때 스탯의 변화량이
        // 노드에 표시되도록. 여러 루트가 있을 때는 스탯의 최소최대량을 표기"). 도달성 증명이
        // 이미 (에피소드, 스탯 벡터)로 걸으므로 그 결과를 읽기만 한다 — 따로 세지 않는다.
        if (StatSpanText(episode) is { Length: > 0 } spans)
        {
            body.Children.Add(new TextBlock
            {
                Text = spans,
                FontSize = 9,
                Opacity = 0.75,
                Foreground = new SolidColorBrush(Color.Parse("#3E7B9B")),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Tag = StatLineTag // 검증이 이 줄을 글자 짐작 없이 집는다
            });
        }

        // 선택지 포트 (2026-08-15 소유자) — 연출 그래프의 조건 갈래 포트처럼 카드
        // 오른변에 뚫린다. 카드는 선택지 수만큼 아래로 자라고(많아야 3개), 문구·원은
        // 간선 그리기와 같은 PortY 산식으로 캔버스에 앉는다 — 줄과 선이 어긋날 수 없다.
        List<ChapterEdge> options = _optionsByEpisode.GetValueOrDefault(episode.EpisodeId) ?? [];

        var card = new Border
        {
            Width = CardWidth,
            // ⚠ 카드는 <b>안 자란다</b> (v5) — 포트가 아래변 위에 걸터앉으므로 칸 수만큼
            //   키를 늘릴 이유가 없다. v4까지는 선택지 줄마다 18px씩 자랐다.
            Height = CardHeight,
            Padding = new Thickness(9, 7),
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(hasError ? 2 : 1),
            BorderBrush = hasError
                ? Brushes.IndianRed
                : new SolidColorBrush(Color.Parse("#7F8A96")),
            Background = new SolidColorBrush(Color.Parse("#FAFBFCFD")),
            Child = body,
            // 노드 카드임을 EpisodeId로 표시한다. 간선 라벨도 Border라서, 표식이 없으면
            // 검증이 카드와 라벨을 구별하지 못한다.
            Tag = episode.EpisodeId
        };

        ToolTip.SetTip(card, Tooltip(model, episode) + StatSpanDetail(episode));

        if (GatedIncoming(model, episode).Count > 0)
        {
            card.BorderThickness = new Thickness(1.6);
            card.BorderBrush = new SolidColorBrush(Color.Parse("#C08A3E"));
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

        DrawOptionPorts(options, x, y);
    }

    /// <summary>
    /// 카드 오른변의 선택지 포트들 — 문구(카드 안 오른쪽 정렬) + 테두리 위의 원.
    /// v9에서는 포트 하나가 곧 간선 하나다(문구가 붙은 길). 클릭하면 그 간선이 선택된다.
    /// </summary>
    private void DrawOptionPorts(IReadOnlyList<ChapterEdge> options, double x, double y)
    {
        // 들어오는 길이 모이는 점 하나 — 아래로 나가서 위로 들어온다.
        GraphCanvas.Children.Add(Dot(
            EntryDot(x, y),
            new SolidColorBrush(Color.Parse("#7F8A96")),
            filled: false,
            tip: "이 에피소드로 들어오는 길이 여기로 모입니다."));

        int slots = SlotsFor(options.Count);

        for (int index = 0; index < slots; index++)
        {
            ChapterEdge? wired = index < options.Count ? options[index] : null;

            // v12 — 문구가 비면 그것은 길의 종류가 아니라 **고칠 것**이다. 판에서 그 자리를
            // 짚어 주지 않으면 기획자는 빈 칸을 못 찾는다. 이제 그 말을 <b>점의 색</b>이 한다:
            // 파랑=자동 · 빨강=문구 없음 · 주황=이어진 선택지 · 회색 빈 점=아직 안 쓴 칸.
            IBrush colour = new SolidColorBrush(Color.Parse(
                wired is null ? "#7F8A96"
                    : wired.Auto ? "#3D7BD9"
                    : wired.HasNoOptionLabel ? "#C0392B"
                    : "#C06A14"));

            Ellipse port = Dot(
                new Point(PortX(x, index, slots), y + CardHeight),
                colour,
                filled: wired is not null,
                tip: wired is null
                    ? "빈 선택지 칸입니다 — [연출 그래프]에서 문구를 적으면 살아납니다."
                    : PortTip(wired));

            if (wired is { } edge)
            {
                port.PointerPressed += (_, e) =>
                {
                    e.Handled = true;
                    UiGuard.Run(_session, "선택지 포트", () =>
                        SelectEdgeKey(edge.FromEpisodeId, edge.ToEpisodeId, EdgeLabelKey(edge)));
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
            Fill = filled ? colour : Brushes.White,
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
        string option = edge.Auto ? "자동 진행"
            : edge.HasNoOptionLabel ? "⚠ 문구가 없습니다 — 자동 진행이 아니면 고칠 자리입니다"
            : edge.OptionLabel!;

        string gate = edge.ConditionLabel is { } unlock ? $"\n해금조건: {unlock}" : string.Empty;
        string visible = edge.VisibleConditionLabel is { } show ? $"\n표시조건: {show}" : string.Empty;

        return $"{option} → {edge.ToEpisodeId}{visible}{gate}" +
               "\n\n클릭하면 이 간선의 조건·해금·스탯변화를 편집합니다.";
    }

    /// <summary>
    /// 카드에 세울 스탯 줄 — `신뢰 1~3 · 분노 0` (2026-08-17). 값은 <b>도착 직후</b>다
    /// (그 노드로 들어오는 간선의 증감까지 커밋한 뒤). 루트가 하나면 값 하나, 갈래가
    /// 여럿이면 최소~최대로 벌어진다.
    ///
    /// 닿을 수 없는 에피소드에는 아무것도 안 쓴다 — 도착이 없으니 도착 시점 값도 없다.
    /// bool 스탯은 숫자 대신 참/거짓으로 읽는다(0·1이 무슨 뜻인지 카드에서 알 수 없다).
    /// </summary>
    private string StatSpanText(ChapterEpisode episode)
    {
        if (_validation is not { } validation || SelectedModel is not { } model)
        {
            return string.Empty;
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

        if (spans.Count == 0)
        {
            return string.Empty;
        }

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

            return span.IsFixed ? (span.Minimum == 1 ? "참" : "거짓") : "참·거짓";
        }

        string body = string.Join(" · ", spans.Select(span =>
            $"{(span.DisplayName.Length > 0 ? span.DisplayName : span.Key)} {Value(span)}"));

        // 탐색이 상한에서 잘렸으면 이 폭은 "적어도 이만큼"이다 — 확정처럼 보이면 안 된다.
        return validation.Reachability.ExplorationComplete ? body : "≈ " + body;
    }

    /// <summary>카드의 도착 스탯 줄에 붙는 표식 — 검증이 그 줄을 집는 손잡이다.</summary>
    internal const string StatLineTag = "episode-stats";

    /// <summary>카드 툴팁의 스탯 절 — 좁은 카드 줄이 잘려도 여기서는 다 읽힌다.</summary>
    private string StatSpanDetail(ChapterEpisode episode)
    {
        string line = StatSpanText(episode);

        if (line.Length == 0)
        {
            return string.Empty;
        }

        bool partial = line.StartsWith("≈ ", StringComparison.Ordinal);

        return "\n여기 도착했을 때의 스탯 —\n  "
            + string.Join("\n  ", (partial ? line[2..] : line).Split(" · "))
            + "\n  (범위는 들어오는 루트가 여럿일 때 벌어집니다)"
            + (partial ? "\n  ≈ 탐색이 상한에서 멈춰 실제 폭은 더 넓을 수 있습니다." : string.Empty);
    }

    /// <summary>
    /// 도달성 증명이 "닿을 수 없다"고 한 에피소드인가 (G7). `도달불가 허용`이 켜진 노드는
    /// 의도된 것이므로 오류 표식을 달지 않는다 — 대신 그 사실이 진단 목록에 알림으로 선다(D3).
    /// </summary>
    private bool IsUnreachable(ChapterEpisode episode) =>
        _validation is { } validation &&
        !validation.Reachability.ReachableEpisodeIds.Contains(episode.EpisodeId) &&
        !episode.AllowUnreachable;

    // ── 편집 (v4: 자리는 사람이 소유한다 — 드래그가 에피소드의 X·Y를 고친다) ──────

    private string? _selectedEpisodeId;

    /// <summary>이번 그리기의 캔버스 배치 = 에피소드의 X·Y + 여백. <see cref="DrawCanvas"/>가 채운다.</summary>
    private IReadOnlyDictionary<string, (double X, double Y)> _placed =
        new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

    /// <summary>
    /// 고른 분기의 <b>줄</b> Id (2026-09-18). 한 에피소드가 분기를 여럿 가질 수 있어
    /// 출발·도착만으로는 못 가른다 — 신원은 표식이 붙은 그 줄이다.
    /// </summary>
    private string? _selectedBranchLineId;

    /// <summary>이번 그리기의 분기 선들 — 줄 Id로 찾는다(선택 강조와 패널이 같은 것을 본다).</summary>
    private readonly Dictionary<string, (ChapterBranchMarkers.Marker Marker, Line Line)> _branchByLine =
        new(StringComparer.Ordinal);

    /// <summary>지금 끌고 있는 카드. 없으면 <c>null</c>.</summary>
    private string? _draggingEpisodeId;

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
            ? chapter.ToGraphModel(id, _session.Definition)
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

            // 끌 채비 (v4). 아직 아무것도 안 적는다 — 움직여야 드래그다.
            Point position = e.GetPosition(GraphCanvas);

            _draggingEpisodeId = episode.EpisodeId;
            _dragOffset = new Point(
                position.X - Canvas.GetLeft(card), position.Y - Canvas.GetTop(card));
            _dragPosition = null;
            _dragMoved = false;
        };

        // ⛔ 카드 더블클릭이 대본 엑셀을 열던 배선은 2026-10-29(D2)에 엑셀 연동과 함께 걷혔다.
    }

    /// <summary>
    /// 끄는 중 — 카드의 임시 자리를 옮기고 판을 다시 그린다 (v4, 2026-09-18).
    ///
    /// ⚠ <b>프로젝트는 아직 안 고친다.</b> 놓을 때 한 번 적는다
    /// (<see cref="OnCanvasPointerReleased"/>) — 그래야 되돌리기 한 번이 드래그 한 번이다.
    /// </summary>
    private void OnCanvasPointerMoved(object? sender, Avalonia.Input.PointerEventArgs args)
    {
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

        // 여백 안쪽(음수)으로는 못 나간다 — 스크롤이 닿지 않는 자리에 카드를 두면 못 찾는다.
        _dragPosition = (
            X: Math.Max(0, position.X - _dragOffset.X - CanvasMargin),
            Y: Math.Max(0, position.Y - _dragOffset.Y - CanvasMargin));
        _dragMoved = true;

        QueueDragRedraw();
    }

    private void OnCanvasPointerReleased(
        object? sender, Avalonia.Input.PointerReleasedEventArgs args) => CommitDrag();

    /// <summary>
    /// 끌던 자리를 <b>한 번</b> 적는다. 안 움직였으면(그냥 누른 것) 아무것도 안 적는다 —
    /// 카드를 고르기만 해도 되돌리기가 쌓이면 Ctrl+Z가 아무 일도 안 하는 것처럼 보인다.
    ///
    /// ⭐ <b>아직 자리가 없던 챕터라면 여기서 판 전체가 함께 적힌다</b>
    /// (<see cref="DisplayPlacement"/>). 그때까지 보이던 자리는 빌린 것이라, 끈 카드 하나만
    /// 적으면 나머지는 다음 그리기에서 <b>도로 한 점에 포개진다</b> — 사람 눈에는 카드 하나를
    /// 옮겼더니 나머지가 사라진 것으로 보인다. 한 번의 변경이므로 되돌리기도 한 번이다.
    /// </summary>
    private void CommitDrag()
    {
        if (_draggingEpisodeId is not { } episodeId)
        {
            return;
        }

        (double X, double Y)? position = _dragPosition;
        bool moved = _dragMoved;

        _draggingEpisodeId = null;
        _dragPosition = null;
        _dragMoved = false;

        if (!moved || position is not { } dropped ||
            _selectedChapterId is not { } chapterId || SelectedModel is not { } model)
        {
            return;
        }

        Dictionary<string, (double X, double Y)> places =
            DisplayPlacement(model).ToDictionary(pair => pair.Key, pair => pair.Value,
                StringComparer.Ordinal);

        places[episodeId] = dropped;

        UiGuard.Run(_session, "에피소드 자리", () =>
            _session!.Editor.MoveEpisodes(chapterId, places));
    }

    /// <summary>
    /// <b>[자동 정렬]</b> — 흐름 순서(위에서 아래)로 전부 다시 세운다 (v4, 2026-09-18 · 방향은 2026-10-29 D3).
    ///
    /// 자리의 주인이 사람이 되면 <b>돌아올 길</b>이 있어야 한다. 그 길이 곧 v3까지 매번
    /// 돌던 깊이 배치다 — 없앤 것이 아니라 <b>부를 때만 도는 것</b>으로 바뀌었다.
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

        _session!.Editor.MoveEpisodes(chapterId, ChapterBranchPlanner.Layout(model));
        _session.SetStatus(
            $"'{chapterId}'의 카드 {model.Episodes.Count}장을 흐름 순서로 다시 세웠습니다. " +
            "되돌리기(Ctrl+Z)로 돌아옵니다.");
    }

    /// <summary>
    /// <b>이 챕터를 어디에 그리나</b> — 에피소드의 X·Y가 원칙이고, <b>아직 자리가 없으면</b>
    /// (카드가 한 점에 포개져 있으면) 깊이 배치를 빌려 쓴다 (v4, 2026-09-18).
    ///
    /// ⚠ <b>왜 빌리기만 하나:</b> v3까지 자리는 그릴 때마다 계산됐으므로 에피소드의 X·Y는
    /// 아무도 안 채웠다 — 엑셀에서 들여온 챕터는 전부 (0,0)이고, 그대로 그리면 판이
    /// <b>카드 한 장처럼</b> 보인다. 그렇다고 판을 열어 봤다는 이유로 프로젝트에 적으면,
    /// ① 사람이 안 한 변경이 저장 대기에 오르고 ② 그것이 되돌리기 목록 맨 위에 앉아
    /// <b>사람의 첫 Ctrl+Z가 카드를 도로 포갠다</b>. 둘 다 사람이 시킨 적 없는 일이다.
    ///
    /// ⭐ 그래서 <b>사람이 처음 카드를 옮길 때</b> 그 손과 함께 적힌다
    /// (<see cref="CommitDrag"/>). 되돌리기 한 번이면 그 손 이전으로 돌아간다.
    ///
    /// ⚠ "전부 0"이 아니라 "겹친다"로 묻는 것이라, 사람이 카드 둘을 <b>정확히</b> 포개 놓아도
    /// 빌린 배치로 보인다. 소수점 둘째 자리까지 맞아떨어져야 하니 손으로는 거의 안 일어나고,
    /// 일어나도 다음 옮기기 한 번으로 풀린다.
    ///
    /// ⛔ <b>음수 자리도 여기서 걷는다.</b> v3의 깊이 배치는 언제나 0 이상을 냈지만, 손으로
    /// 적은 워크북은 아니다 — 견본 챕터의 <c>branch05.02A</c>가 Y=−120이다. 그대로 그리면
    /// 카드가 캔버스 위쪽 바깥에 서고 <b>스크롤이 닿지 않아 아예 못 만진다</b>. 왼위가
    /// (0,0)이 되게 통째로 민다 — 서로의 거리는 그대로이므로 사람이 짠 모양은 안 바뀐다.
    ///
    /// ⚠ 미는 양은 <b>모델</b>에서 나온다(끄는 중의 임시 자리가 아니라). 그래야 끄는 동안
    /// 기준이 안 흔들려 판 전체가 출렁이지 않는다.
    /// </summary>
    private IReadOnlyDictionary<string, (double X, double Y)> DisplayPlacement(
        ChapterGraphModel model)
    {
        if (ChapterBranchPlanner.NeedsSeeding(model))
        {
            return ChapterBranchPlanner.Layout(model);
        }

        double left = model.Episodes.Count == 0 ? 0 : model.Episodes.Min(episode => episode.X);
        double top = model.Episodes.Count == 0 ? 0 : model.Episodes.Min(episode => episode.Y);

        // 왼위가 이미 안쪽이면 그대로 둔다 — 까닭 없이 판을 붙여 세우면 사람이 비워 둔
        // 여백이 사라진다. 미는 것은 바깥으로 나간 만큼뿐이다.
        double shiftX = Math.Min(0, left);
        double shiftY = Math.Min(0, top);

        return model.Episodes.ToDictionary(
            episode => episode.EpisodeId,
            episode => (X: episode.X - shiftX, Y: episode.Y - shiftY),
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

                if (_draggingEpisodeId is not null && SelectedModel is { } model)
                {
                    DrawCanvas(model);
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
        _selectedEpisodeId = episodeId;
        _selectedEdgeKey = null;
        _selectedBranchLineId = null;
        HideEdgeForm(); // 다른 노드로 넘어가면 열려 있던 연결 폼은 닫는다
        ApplySelectionVisuals();
        RefreshPropertyPanel();
    }

    internal void SelectEdgeKey(string fromEpisodeId, string toEpisodeId, string optionLabel = "")
    {
        _selectedEdgeKey = (fromEpisodeId, toEpisodeId, optionLabel.Trim());
        _selectedEpisodeId = null;
        _selectedBranchLineId = null;
        HideEdgeForm();
        ApplySelectionVisuals();
        RefreshPropertyPanel();
    }

    /// <summary>
    /// 분기 하나를 고른다 — <b>신원은 줄</b>이다 (2026-09-18). 한 에피소드가 분기를 여럿
    /// 가질 수 있어 출발·도착만으로는 못 가른다.
    /// </summary>
    internal void SelectBranch(string? lineId)
    {
        _selectedBranchLineId = lineId;
        _selectedEpisodeId = null;
        _selectedEdgeKey = null;
        HideEdgeForm();
        ApplySelectionVisuals();
        RefreshPropertyPanel();
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

        if (_selectedEpisodeId is { } episodeId && _cardById.TryGetValue(episodeId, out Border? selected))
        {
            selected.BorderBrush = new SolidColorBrush(Color.Parse("#3D7BD9"));
            selected.BorderThickness = new Thickness(2.4);
        }

        if (_selectedEdgeKey is { } edgeKey && _lineByEdge.TryGetValue(edgeKey, out List<Line>? selectedLines))
        {
            foreach (Line line in selectedLines)
            {
                line.Stroke = new SolidColorBrush(Color.Parse("#3D7BD9"));
                line.StrokeThickness = 3.4;
            }
        }

        // 분기는 점선인 채로 굵어진다 — 굵기만 바꾸고 점선을 안 풀어야 "이것은 선택지가
        // 아니다"가 고른 뒤에도 남는다.
        if (_selectedBranchLineId is { } branchLineId &&
            _branchByLine.TryGetValue(branchLineId, out (ChapterBranchMarkers.Marker _, Line Line) branch))
        {
            branch.Line.Stroke = new SolidColorBrush(Color.Parse("#3D7BD9"));
            branch.Line.StrokeThickness = 3.4;
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
    /// <param name="preserveTyping">저장 감시가 부른 다시 그리기라면 참.</param>
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

        ChapterBranchMarkers.Marker? branch =
            _selectedBranchLineId is { } branchLineId &&
            _branchByLine.TryGetValue(branchLineId, out (ChapterBranchMarkers.Marker Marker, Line _) found)
                ? found.Marker
                : null;

        PropertyPanel.IsVisible = episode is not null;
        EdgePanel.IsVisible = edge is not null;
        BranchPanel.IsVisible = branch is not null;
        NoSelectionText.IsVisible = episode is null && edge is null && branch is null;

        if (branch is not null)
        {
            RefreshBranchPanel(model, branch);
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
                SceneIdBox.Text = episode.SceneId ?? string.Empty;
            }


            SetItems(EdgeTargetCombo,
                model.Episodes
                    .Where(candidate => candidate.EpisodeId != episode.EpisodeId)
                    .Select(candidate => candidate.EpisodeId)
                    .ToList(),
                EdgeTargetCombo.SelectedItem as string); // 도착 고르기는 언제나 사람 소유

            // 문구 목록은 챕터 전체 사전이다 (v9) — 어느 에피소드에서든 다 고를 수 있다.
            SetItems(EdgeLabelBox, ChoiceLabelItems(model), EdgeLabelBox.SelectedItem as string);

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

        UiGuard.Run(_session, "간선 저장", ApplyEdgeFromPanel);
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
            DialoguePreviewHeader.Text = "에피소드를 선택하면 그 대사가 여기 보입니다.";
            DialoguePreviewText.Text = string.Empty;
            return;
        }

        string preview = DialogueOf(episodeId);

        DialoguePreviewHeader.Text = preview.Length > 0
            ? "읽기 전용 · 고치는 곳은 연출 그래프의 카드"
            : "아직 적힌 대사가 없습니다 — 연출 그래프의 카드에서 쓰세요";
        DialoguePreviewText.Text = preview;
    }

    /// <summary>그 에피소드 카드의 대사를 「화자: 대사」 줄로 편다. 카드나 대본이 없으면 빈 글이다.</summary>
    private string DialogueOf(string episodeId)
    {
        if (_session is null ||
            _selectedChapterId is not { } chapterId ||
            EpisodeNaming.CardFor(_session.Project, chapterId, episodeId) is not { } card ||
            _session.Project.FindScript(card.ScriptId) is not { } script)
        {
            return string.Empty;
        }

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

    // ── 선택지 목록 + 잇기 폼 (v9, 2026-08-17 소유자) ────────────────────────
    // 목록의 줄 = 이 에피소드에서 나가는 길 하나이고, 길 하나가 곧 선택지 하나다.
    // 문구는 챕터 전체의 사전에서 고른다 — "모든 선택지 중에서 자유자재로."

    /// <summary>문구 없는 길(보이지 않는 기본)을 드롭다운에서 가리키는 이름.</summary>
    private const string PlainAdvanceItem = "(문구 없음)";

    /// <summary>폼이 붙은 간선의 신원 (도착, 문구). null이면 <b>새 선택지</b>를 여는 중이다.</summary>
    private (string To, string Label)? _edgeFormEdge;

    /// <summary>목록 안에서 폼이 설 자리. -1 = 닫힘.</summary>
    private int _edgeFormIndex = -1;

    private void HideEdgeForm()
    {
        _edgeFormIndex = -1;
        _edgeFormEdge = null;
        EdgeFormHost.IsVisible = false;
        EdgeFormPanel.IsVisible = false;
    }

    /// <summary>[＋] — 빈 폼을 목록 끝에 연다. 문구와 도착을 고르면 그때 길이 생긴다.</summary>
    private void AddChoiceSlotFromPanel()
    {
        if (_selectedEpisodeId is null || SelectedModel is null)
        {
            _session?.SetStatus("에피소드를 먼저 골라 주세요.");
            return;
        }

        // 이미 새 선택지 폼이 열려 있으면 접는다 — 여는 손과 닫는 손이 같다.
        if (_edgeFormIndex >= 0 && _edgeFormEdge is null)
        {
            HideEdgeForm();
            RefreshPropertyPanel(preserveTyping: true);
            return;
        }

        OpenEdgeForm(edge: null, rowIndex: int.MaxValue - 1);
    }

    /// <summary>
    /// 줄 클릭 — 그 길의 문구·도착을 고치는 폼이 줄 바로 아래에 열린다. 문구 드롭다운은
    /// <b>챕터의 모든 문구</b>를 담는다 (2026-08-17 소유자: "자기 것만 고르는 게 아니라
    /// 모든 선택지 중에서 자유자재로"). <paramref name="edge"/>가 null이면 새 선택지다.
    /// </summary>
    internal void OpenEdgeForm(ChapterEdge? edge, int rowIndex)
    {
        _edgeFormEdge = edge is null ? null : (edge.ToEpisodeId, EdgeLabelKey(edge));
        _edgeFormIndex = rowIndex + 1;
        RefreshPropertyPanel(preserveTyping: true);

        _fillingPanel = true;

        try
        {
            EdgeTargetCombo.SelectedItem = edge?.ToEpisodeId;
            EdgeLabelBox.SelectedItem = edge is null
                ? null
                : edge.HasNoOptionLabel ? PlainAdvanceItem : edge.OptionLabel;
            EdgeFormAutoCheck.IsChecked = edge?.Auto == true;

            // 스탯변화도 여기서 만진다 (2026-08-17) — 새 길이면 빈 목록에서 시작한다.
            _formStats.Load(SelectedModel?.Stats ?? [], edge?.StatChanges ?? []);
        }
        finally
        {
            _fillingPanel = false;
        }
    }

    /// <summary>문구 드롭다운의 목록 — 챕터 사전 전체 + 보이지 않는 기본.</summary>
    private static List<string> ChoiceLabelItems(ChapterGraphModel model)
    {
        var items = new List<string> { PlainAdvanceItem };
        items.AddRange(model.ChoiceLabels);
        return items;
    }

    /// <summary>폼의 [잇기]/[수정] — 이 길의 문구와 도착을 한 저장으로 정한다.</summary>
    internal void SubmitEdgeForm()
    {
        if (_edgeFormIndex < 0 ||
            _selectedEpisodeId is not { } from ||
            _selectedChapterId is not { } chapterId)
        {
            _session?.SetStatus("선택지 줄을 다시 눌러 주세요. 선택이 풀렸습니다.");
            return;
        }

        if (EdgeTargetCombo.SelectedItem is not string to)
        {
            _session?.SetStatus("도착 에피소드를 골라 주세요.");
            return;
        }

        bool auto = EdgeFormAutoCheck.IsChecked == true;

        // 공백만으로 자동을 추측하지 않는다. 자동 체크가 명시된 경우에만 문구를 비운다.
        string label = EdgeLabelBox.SelectedItem as string is { } picked && picked != PlainAdvanceItem
            ? picked
            : string.Empty;

        // 배선과 증감이 한 변경으로 간다 — 두 번 쓰면 되돌리기가 둘로 쪼개져, 한 번 물렀을 때
        // 반쪽만 적힌 길이 남는다(two-graphs.md §5-7).
        string stats = auto ? string.Empty : _formStats.ToSheetText();
        if (auto)
        {
            label = string.Empty;
        }

        ChapterEdit(
            () =>
            {
                if (_edgeFormEdge is { } current)
                {
                    _session!.Editor.SetEdgeRoute(
                        chapterId, from, current.To, current.Label, to, label, stats, auto);
                }
                else
                {
                    _session!.Editor.AddEdge(
                        chapterId, from, to, optionLabel: label, statChanges: stats, auto: auto);
                }

                HideEdgeForm();
            },
            label.Length > 0
                ? $"'{label}' → {to} 로 이었습니다."
                : auto
                    ? $"{from} → {to} 를 자동 진행으로 이었습니다."
                    : $"{from} → {to} 를 문구 없는 일반 선택지로 이었습니다.");
    }

    private void RefreshEdgeList(ChapterGraphModel model, ChapterEpisode episode)
    {
        // 폼(EdgeFormPanel)은 axaml이 만든 단일 인스턴스다 — Clear는 트리에서 떼어낼 뿐이라
        // 아래에서 원하는 자리에 다시 꽂는다.
        EdgeListPanel.Children.Clear();

        List<ChapterEdge> edges = model.Edges
            .Where(candidate =>
                string.Equals(candidate.FromEpisodeId, episode.EpisodeId, StringComparison.Ordinal))
            .ToList();
        ChoiceHeaderText.Text = edges.Count > 0 ? $"선택지 ({edges.Count})" : "선택지";

        for (int rowIndex = 0; rowIndex < edges.Count; rowIndex++)
        {
            EdgeListPanel.Children.Add(BuildEdgeRow(edges[rowIndex], rowIndex));
        }

        if (edges.Count == 0)
        {
            EdgeListPanel.Children.Add(new TextBlock
            {
                Text = "나가는 길이 없습니다 — 여기서 챕터가 끝납니다. [＋]로 선택지를 엽니다.",
                FontSize = 10,
                Opacity = 0.5,
                TextWrapping = TextWrapping.Wrap
            });
        }

        // 폼을 제자리에 꽂는다 — 누른 줄 바로 아래(새 선택지면 목록 끝).
        bool formOpen = _edgeFormIndex >= 0;
        EdgeFormHost.IsVisible = formOpen;
        EdgeFormPanel.IsVisible = formOpen;

        if (formOpen)
        {
            // 라벨은 둘뿐이다 — 있는 길은 [수정], 새로 여는 길은 [잇기].
            AddEdgeButton.Content = _edgeFormEdge is null ? "잇기" : "수정";
            EdgeTargetCombo.IsEnabled = true;

            _fillingPanel = true;

            try
            {
                // 문구 목록은 폼이 열릴 때마다 새로 깐다 — 다른 곳(선택지 사전 · 연출 그래프의
                // 선택지 칸)에서 낱말을 더하면 다시 읽기 한 번으로 여기에 나타난다.
                SetItems(EdgeLabelBox, ChoiceLabelItems(model), EdgeLabelBox.SelectedItem as string);
            }
            finally
            {
                _fillingPanel = false;
            }

            EdgeListPanel.Children.Insert(
                Math.Min(_edgeFormIndex, EdgeListPanel.Children.Count), EdgeFormHost);
        }
        else
        {
            EdgeListPanel.Children.Add(EdgeFormHost); // 트리 밖에 두지 않는다 — 자리만 숨김
        }
    }

    /// <summary>
    /// 선택지 한 줄 — `문구 → 도착 [조건]`. 문구가 없으면 "(기본 · 보이지 않음)"이다.
    /// 클릭 = 그 줄 아래에서 문구·도착 고치기, 다시 클릭 = 접기.
    /// </summary>
    private Control BuildEdgeRow(ChapterEdge edge, int rowIndex)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        // 순번 — 화면에 뜨는 차례다(간선 시트의 행 순서). 신원이 아니라 자리 표시다.
        row.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse(edge.HasNoOptionLabel ? "#22808080" : "#22C06A14")),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = (rowIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = 10,
                Opacity = 0.8
            }
        });

        string label = edge.Auto ? "AUTO · 자동 진행" : edge.HasNoOptionLabel ? "⚠ 문구 없음" : edge.OptionLabel!;
        string condition = edge.ConditionLabel is { } gate ? $"  [{gate}]" : string.Empty;

        var text = new TextBlock
        {
            Text = $"{label}   → {edge.ToEpisodeId}{condition}",
            FontSize = 11,
            Opacity = edge.HasNoOptionLabel ? 0.7 : 1,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            // 배경 없는 TextBlock은 글자 획 위만 클릭된다 — 행 전체가 눌리게 깔아 둔다.
            Background = Brushes.Transparent,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        Grid.SetColumn(text, 1);

        // 툴팁 없음 (2026-08-16 소유자) — 목록 위에 뜨는 설명 상자가 화면을 가렸다.
        {
            (string To, string Label) key = (edge.ToEpisodeId, EdgeLabelKey(edge));
            ChapterEdge captured = edge;
            int capturedRow = rowIndex;
            // 같은 줄을 다시 누르면 접힌다 (2026-08-16 소유자) — 여는 손과 닫는 손이 같다.
            text.PointerPressed += (_, _) =>
                UiGuard.Run(_session, "선택지 잇기 폼", () =>
                {
                    if (_edgeFormEdge == key)
                    {
                        HideEdgeForm();
                        RefreshPropertyPanel(preserveTyping: true);
                        return;
                    }

                    OpenEdgeForm(captured, capturedRow);
                });
        }

        row.Children.Add(text);

        {
            string from = edge.FromEpisodeId;
            string to = edge.ToEpisodeId;
            string optionLabel = EdgeLabelKey(edge);
            var remove = new Button
            {
                Content = "✕",
                FontSize = 10,
                Padding = new Thickness(5, 1),
                [ToolTip.TipProperty] = "이 길을 지웁니다. 선택지 문구는 사전에 남습니다."
            };
            Grid.SetColumn(remove, 2);
            remove.Click += (_, _) => UiGuard.Run(_session, "선택지 삭제", () => ChapterEdit(
                () => _session!.Editor.RemoveEdge(_selectedChapterId!, from, to, optionLabel),
                $"{from} → {to} 를 지웠습니다."));
            row.Children.Add(remove);
        }

        return row;
    }

    /// <summary>
    /// 간선 편집 패널 — "이 분기는 언제 보이고 언제 열리는가"를 채운다.
    /// <paramref name="fill"/>이 거짓이면 사람이 적던 값을 지킨다(위 설명 참조).
    /// </summary>
    private void RefreshEdgePanel(ChapterGraphModel model, ChapterEdge edge, bool fill)
    {
        EdgeFromToPanel.Children.Clear();
        EdgeFromToPanel.Children.Add(EpisodeLink(model, edge.FromEpisodeId));
        EdgeFromToPanel.Children.Add(new TextBlock
        {
            Text = "→",
            FontSize = 11,
            Opacity = 0.5,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        EdgeFromToPanel.Children.Add(EpisodeLink(model, edge.ToEpisodeId));

        if (fill)
        {
            EdgeAutoCheck.IsChecked = edge.Auto;
            EdgeLockedMsgBox.Text = edge.LockedMessage ?? string.Empty;
            _edgeStats.Load(model.Stats, edge.StatChanges);
        }

        // 이 길의 문구 (v9) — 챕터의 모든 문구 중에서 고른다. 목록·저장이 선택지 목록의
        // 폼과 같은 규칙 하나를 쓴다(사본 금지): 안 고른 것과 "문구 없음"은 같은 뜻이다.
        SetItems(EdgeLabelEditBox, ChoiceLabelItems(model),
            fill
                ? edge.HasNoOptionLabel ? PlainAdvanceItem : edge.OptionLabel
                : EdgeLabelEditBox.SelectedItem as string);

        var labels = new List<string> { "(없음)" };
        labels.AddRange(model.Conditions.Select(condition => condition.Label));

        // 관문 둘 — v8에서 에피소드에서 여기로 옮겨 왔다.
        SetItems(EdgeVisibleCombo, labels,
            fill ? edge.VisibleConditionLabel ?? "(없음)" : EdgeVisibleCombo.SelectedItem as string);
        SetItems(EdgeConditionCombo, labels,
            fill ? edge.ConditionLabel ?? "(없음)" : EdgeConditionCombo.SelectedItem as string);
    }

    /// <summary>
    /// 분기 패널 — <b>조건 한 칸</b>이 전부다 (2026-09-18).
    ///
    /// 문구도 스탯변화도 없다(소유자: *"분기로 이어지는 것은 선택지와 다르게 라벨이나
    /// 스탯변화를 주지도 않을거야"*). 분기로는 <b>언제나 한 번 간다</b> — 이 조건이 막는
    /// 것은 다녀간 뒤 그 에피소드가 <b>돌아가는가</b>이고, 그래서 조건이 적히는 자리도
    /// 다녀갈 에피소드의 첫머리다(<see cref="BranchEntryCondition"/>).
    ///
    /// ⚠ 작가가 대사 편집기에서 지은 조건 구조는 <b>읽기만</b> 한다 — 덮으면 짝이 어긋난
    /// 대본이 된다. 그때는 칸을 잠그고 사유를 세운다.
    /// </summary>
    private void RefreshBranchPanel(ChapterGraphModel? model, ChapterBranchMarkers.Marker marker)
    {
        BranchFromToPanel.Children.Clear();

        if (model is not null)
        {
            BranchFromToPanel.Children.Add(EpisodeLink(model, marker.FromEpisodeId));
            BranchFromToPanel.Children.Add(new TextBlock
            {
                Text = "⤳",
                FontSize = 12,
                Opacity = 0.5,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            BranchFromToPanel.Children.Add(EpisodeLink(model, marker.ToEpisodeId));
        }

        BranchEntryCondition.State state = BranchEntryCondition.Read(
            _session!.Editor.Project, marker.ToNodeId, _session.Definition);

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

    /// <summary>조건을 안 건 분기 — 언제나 재생된다. 「안 고름」과 같은 뜻이다.</summary>
    private const string BranchAlwaysItem = "(언제나)";

    /// <summary>고른 분기의 조건을 적는다. 고르는 순간 나간다 — 간선 패널과 같은 감각이다.</summary>
    private void SaveBranchCondition()
    {
        if (_fillingPanel || _session is null || _selectedBranchLineId is not { } lineId ||
            !_branchByLine.TryGetValue(lineId, out (ChapterBranchMarkers.Marker Marker, Line _) branch))
        {
            return;
        }

        string? label = BranchConditionCombo.SelectedItem as string;

        string? failure = BranchEntryCondition.Set(
            _session.Editor,
            branch.Marker.ToNodeId,
            string.Equals(label, BranchAlwaysItem, StringComparison.Ordinal) ? null : label,
            _session.Definition);

        _session.SetStatus(failure ?? (label is null or BranchAlwaysItem
            ? $"'{branch.Marker.ToEpisodeId}'의 조건을 걷었습니다 — 언제나 재생됩니다."
            : $"'{branch.Marker.ToEpisodeId}'은 조건 '{label}'일 때 재생됩니다. 되돌리기(Ctrl+Z)로 돌아옵니다."));
    }

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
            FontSize = 11,
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.Parse("#3D7BD9")),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        ToolTip.SetTip(link, "이 에피소드를 선택합니다 — 이름·제목은 거기서 고칩니다.");

        link.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            UiGuard.Run(_session, "에피소드 선택", () => SelectEpisode(episodeId));
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
            _session?.SetStatus("간선을 다시 골라 주세요. 선택이 풀렸거나 그 간선이 사라졌습니다.");
            return;
        }

        string? Changed(string? boxValue, string? current)
        {
            string value = boxValue?.Trim() ?? string.Empty;
            return string.Equals(value, current ?? string.Empty, StringComparison.Ordinal) ? null : value;
        }

        string? Gate(ComboBox combo) =>
            combo.SelectedItem as string == "(없음)" ? string.Empty : combo.SelectedItem as string;

        // 문구도 이 저장에 실린다 (v9) — 신원의 일부라, 찾을 때는 고치기 전 값을 쓴다.
        bool auto = EdgeAutoCheck.IsChecked == true;
        string pickedLabel = EdgeLabelEditBox.SelectedItem as string is { } picked && picked != PlainAdvanceItem
            ? picked
            : string.Empty;
        if (auto)
        {
            pickedLabel = string.Empty;
        }

        ChapterEdit(
            () =>
            {
                _session!.Editor.UpdateEdge(
                    chapterId, key.From, key.To,
                    visibleConditionLabel: Changed(auto ? string.Empty : Gate(EdgeVisibleCombo), edge.VisibleConditionLabel ?? string.Empty),
                    conditionLabel: Changed(auto ? string.Empty : Gate(EdgeConditionCombo), edge.ConditionLabel ?? string.Empty),
                    lockedMessage: Changed(auto ? string.Empty : EdgeLockedMsgBox.Text, edge.LockedMessage ?? string.Empty),
                    statChanges: Changed(auto ? string.Empty : _edgeStats.ToSheetText(), StatChangesText(edge)),
                    matchOptionLabel: EdgeLabelKey(edge),
                    optionLabel: Changed(pickedLabel, EdgeLabelKey(edge)),
                    auto: auto == edge.Auto ? null : auto);

                // 문구를 바꿨으면 신원도 바뀌었다 — 선택이 풀리지 않게 열쇠를 따라 옮긴다.
                _selectedEdgeKey = (key.From, key.To, pickedLabel);
            },
            $"간선 {key.From}→{key.To}을 저장했습니다.");

        // ⛔ 여기 있던 `Reload()`는 2026-09-16에 걷혔다 (R-F). 손에 든 모델이 낡는 것을
        //    막으려던 줄인데, 이제 판이 프로젝트에서 오므로 편집이 곧 그 판이다 —
        //    낡을 사본이 없다.
    }

    /// <summary>간선 스탯변화를 시트 문법 그대로 — 패널 칸과 셀이 같은 글을 쓴다.</summary>
    private static string StatChangesText(ChapterEdge edge) => string.Join("; ", edge.StatChanges
        .Select(delta => $"{delta.Key} {(delta.Amount >= 0 ? "+" : "")}{delta.Amount}"));

    /// <summary>
    /// 에피소드 값 저장 — 지금 이 패널에서 툴이 쓰는 값은 없다. 제목·엔딩키·메모는 칸을
    /// 뺐고(2026-08-16), 표시·해금조건은 v8에서 길(간선)로 옮겨 갔다. 개명은 Enter가,
    /// 선택지는 목록이 맡는다. 라이터 경로는 살아 있으므로 필요하면 여기로 되돌아온다.
    /// </summary>
    internal void ApplyEpisodeFromPanel()
    {
        if (_selectedEpisodeId is not { } episodeId || _selectedChapterId is not { } chapterId)
        {
            _session?.SetStatus("에피소드를 다시 골라 주세요. 선택이 풀렸거나 그 에피소드가 사라졌습니다.");
            return;
        }

        ChapterEdit(
            () => _session!.Editor.UpdateEpisode(
                chapterId, episodeId, sceneId: SceneIdBox.Text?.Trim() ?? string.Empty),
            $"에피소드 '{episodeId}'의 장면ID를 저장했습니다.");
    }

    /// <summary>현재 장면을 이루는 모든 에피소드의 안정 ID를 한 저장으로 바꾼다.</summary>
    internal void ApplySceneIdToGroup()
    {
        if (_selectedEpisodeId is null || _selectedChapterId is not { } chapterId ||
            SelectedModel?.FindEpisode(_selectedEpisodeId) is not { } selected)
        {
            _session?.SetStatus("일괄 변경할 장면의 에피소드를 다시 골라 주세요.");
            return;
        }

        string sceneId = SceneIdBox.Text?.Trim() ?? string.Empty;
        string[] episodeIds = SelectedModel.Episodes
            .Where(episode => string.Equals(
                episode.EffectiveSceneId, selected.EffectiveSceneId, StringComparison.Ordinal))
            .Select(episode => episode.EpisodeId)
            .ToArray();

        ChapterEdit(
            () => _session!.Editor.UpdateEpisodeScenes(chapterId, episodeIds, sceneId),
            $"장면 에피소드 {episodeIds.Length}개의 장면ID를 저장했습니다.");
    }

    internal void DeleteSelectedEdge()
    {
        if (_selectedEdgeKey is not { } deleteKey || _selectedChapterId is not { } chapterId)
        {
            return;
        }

        ChapterEdit(
            () =>
            {
                _session!.Editor.RemoveEdge(chapterId, deleteKey.From, deleteKey.To, deleteKey.Label);
                _selectedEdgeKey = null;
            },
            $"간선 {deleteKey.From}→{deleteKey.To}을 지웠습니다.");
    }


    /// <summary>
    /// <summary>
    /// [챕터] 탭의 <b>스탯 표</b> — 2026-09-17부터 <b>편집 가능</b>하다 (소유자: *"이제 엑셀이
    /// 아닌 챕터그래프에서 직접 조건과 스탯을 정의"*).
    ///
    /// ⚠ 전에는 읽기 전용이었고 *"편집은 챕터 엑셀의 `스탯` 시트에서"*라고 적혀 있었다.
    /// 그 문장은 <b>R-F(2026-09-16)가 이미 낡게 만들었다</b> — 그날 워크북이 산출물이 되고
    /// 값의 주인이 <c>ChapterDocument</c>로 넘어왔는데 안내만 남아 있었다.
    ///
    /// ⚠ <b>키 칸은 없다.</b> 키를 바꾸는 것은 간선·조건·픽스처·공급 조건을 함께 끌고 가는
    /// 일이라 [✎]가 <see cref="ProjectEditor.RenameChapterStat"/>로 간다 — 칸으로 두면
    /// 글자를 고치는 사이의 중간 상태가 전부 진짜 개명이 된다.
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
            StatListPanel.Children.Add(new TextBlock
            {
                Text = "스탯이 없습니다 — 아래 [＋ 스탯]으로 만듭니다.",
                FontSize = 10,
                Opacity = 0.5
            });
        }

        StatListPanel.Children.Add(AddRowButton(
            "＋ 스탯", "스탯 추가", StatAddFlyout, StatListPanel));
    }

    /// <summary>스탯 한 줄 — 키(고정) · 표시이름 · 초기 · 최소 · 최대 · 타입 · 개명 · 삭제.</summary>
    private Control StatRow(ChapterStat stat)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Tag = $"stat:{stat.Key}"
        };

        row.Children.Add(new SelectableTextBlock
        {
            Text = stat.Key,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Width = 96,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        row.Children.Add(SheetBox(
            stat.DisplayName, 88, "표시이름 — 화면에만 쓴다. 세이브 열쇠는 왼쪽의 키다",
            value => _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, displayName: value)));

        row.Children.Add(NumberBox(stat.Initial, "초기",
            value => _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, initial: value)));
        row.Children.Add(NumberBox(stat.Minimum, "최소",
            value => _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, minimum: value)));
        row.Children.Add(NumberBox(stat.Maximum, "최대",
            value => _session!.Editor.UpdateChapterStat(_selectedChapterId!, stat.Key, maximum: value)));

        var type = new ComboBox
        {
            FontSize = 10,
            Width = 62,
            ItemsSource = new[] { "정수", "깃발" },
            SelectedIndex = stat.Type == ChapterStatType.Bool ? 1 : 0,
            VerticalAlignment = VerticalAlignment.Center
        };

        type.SelectionChanged += (_, _) => UiGuard.Run(_session, "스탯 타입", () =>
            _session!.Editor.UpdateChapterStat(
                _selectedChapterId!,
                stat.Key,
                type: type.SelectedIndex == 1 ? ChapterStatType.Bool : ChapterStatType.Int));

        row.Children.Add(type);

        var rename = new Button { Content = "✎", FontSize = 10, Padding = new Thickness(5, 1) };
        ToolTip.SetTip(
            rename,
            "키(세이브 열쇠)를 바꿉니다 — 간선·조건·픽스처의 참조가 함께 따라가지만, " +
            "이미 저장된 세이브는 못 따라옵니다. 표시 이름만 고치려면 [표시이름] 칸을 쓰세요.");
        rename.Click += (_, _) => ShowStatRenameFlyout(rename, stat.Key);
        row.Children.Add(rename);

        var remove = new Button { Content = "✕", FontSize = 10, Padding = new Thickness(5, 1) };
        ToolTip.SetTip(remove, "지웁니다 — 쓰는 곳이 있으면 어디인지 보여 주고 한 번 더 묻습니다.");
        remove.Click += (_, _) => UiGuard.Run(_session, "스탯 삭제", () => RemoveStat(remove, stat.Key));
        row.Children.Add(remove);

        return row;
    }

    /// <summary>
    /// [챕터] 탭의 <b>조건 표</b> — 스탯 표와 같은 날 편집 가능해졌다.
    /// 라벨과 식을 그 자리에서 고치고, [✕]가 <b>쓰는 곳을 세어</b> 거절한다.
    /// </summary>
    private void RefreshConditionList(ChapterGraphModel? snapshot)
    {
        RefreshChapterSheets(snapshot);

        ChapterGraphModel? model = LiveSheetModel ?? snapshot;

        ConditionListPanel.Children.Clear();

        foreach (ChapterCondition condition in model?.Conditions ?? Enumerable.Empty<ChapterCondition>())
        {
            ConditionListPanel.Children.Add(ConditionRow(condition));
        }

        if (ConditionListPanel.Children.Count == 0)
        {
            ConditionListPanel.Children.Add(new TextBlock
            {
                Text = "조건이 없습니다 — 아래 [＋ 조건]으로 만듭니다.",
                FontSize = 10,
                Opacity = 0.5
            });
        }

        ConditionListPanel.Children.Add(AddRowButton(
            "＋ 조건", "조건 추가", ConditionAddFlyout, ConditionListPanel));
    }

    /// <summary>조건 한 줄 — 라벨(고정) · 식 · 삭제.</summary>
    private Control ConditionRow(ChapterCondition condition)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Tag = $"condition:{condition.Label}"
        };

        row.Children.Add(new SelectableTextBlock
        {
            Text = condition.Label,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Width = 96,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        row.Children.Add(SheetBox(
            condition.Expression, 232, "식 (예: trust >= 3; 깃발 == true)",
            value => _session!.Editor.UpdateChapterCondition(_selectedChapterId!, condition.Label, value)));

        var remove = new Button { Content = "✕", FontSize = 10, Padding = new Thickness(5, 1) };
        ToolTip.SetTip(remove, "지웁니다 — 쓰는 곳이 있으면 어디인지 말하고 거절합니다.");
        remove.Click += (_, _) =>
            UiGuard.Run(_session, "조건 삭제", () => RemoveCondition(condition.Label));
        row.Children.Add(remove);

        return row;
    }

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
            FontSize = 10,
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

        return box;
    }

    /// <summary>숫자 칸 — 못 읽는 글자는 <b>조용히 버리지 않고</b> 옛 값으로 되돌린다.</summary>
    private TextBox NumberBox(int value, string hint, Action<int> commit)
    {
        TextBox box = SheetBox(
            value.ToString(System.Globalization.CultureInfo.InvariantCulture), 46, hint,
            text =>
            {
                if (!int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed))
                {
                    throw new InvalidOperationException($"{hint}에는 정수를 적습니다 — '{text}'는 못 읽습니다.");
                }

                commit(parsed);
            });

        return box;
    }

    /// <summary>표 아래의 [＋] 한 줄.</summary>
    private static Button AddRowButton(
        string caption, string action, Action<Button> open, Panel host)
    {
        var button = new Button
        {
            Content = caption,
            FontSize = 10,
            Padding = new Thickness(7, 2),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        _ = action;
        _ = host;
        button.Click += (_, _) => open(button);

        return button;
    }

    private void StatAddFlyout(Button anchor)
    {
        PromptFlyout(anchor, "스탯 키 (예: courage)", key =>
            UiGuard.Run(_session, "스탯 추가", () =>
            {
                _session!.Editor.AddChapterStat(_selectedChapterId!, key);
                Draw();
            }));
    }

    private void ConditionAddFlyout(Button anchor)
    {
        PromptFlyout(anchor, "조건 라벨 (예: 신뢰높음)", label =>
            UiGuard.Run(_session, "조건 추가", () =>
            {
                // 식은 빈 채로 만든다 — 진단이 "조건식이 비어 있습니다"로 짚어 주고,
                // 사람은 방금 생긴 줄에서 바로 적는다. 라벨만 정하면 되는 것이 첫 손짓이다.
                _session!.Editor.AddChapterCondition(_selectedChapterId!, label, string.Empty);
                Draw();
            }));
    }

    /// <summary>
    /// 스탯의 <b>키</b>를 바꾼다 — 표시 이름이 아니다.
    ///
    /// ⛔ <b>키는 세이브의 열쇠다</b> (2026-09-17, 런타임 회신 §6.2가 짚었다).
    /// 저쪽 <c>LocalSaveFile.Stats</c>가 이 글자로 저장하고, 복원은 키가 없으면 <b>초기값</b>으로
    /// 떨어진다 — 오류도 경고도 없다. 개명이 끌고 가는 다섯 자리는 <b>프로젝트 안</b>이고,
    /// <b>이미 디스크에 나간 세이브는 그 다섯에 없다.</b>
    ///
    /// ⚠ 그래서 경고가 <b>적기 전에</b> 선다. 표시 이름만 고치려는 사람이 이 문을 열면
    /// 되돌릴 수 없는 일을 하게 되므로, 옆 칸을 쓰라고 말해 준다.
    /// </summary>
    private void ShowStatRenameFlyout(Control anchor, string key)
    {
        PromptFlyout(
            anchor,
            $"'{key}'의 새 키",
            to => UiGuard.Run(_session, "스탯 키 바꾸기", () =>
            {
                Vn.Authoring.Editing.StatRenameOutcome outcome =
                    _session!.Editor.RenameChapterStat(_selectedChapterId!, key, to);

                if (!outcome.Applied)
                {
                    throw new InvalidOperationException(outcome.Refusal ?? "바꾸지 못했습니다.");
                }

                _session.SetStatus(
                    $"'{key}' → '{to}' — 간선 {outcome.Edges} · 조건 {outcome.Conditions} · " +
                    $"공급 조건 {outcome.SuppliedConditions} · 픽스처 {outcome.Fixtures}곳이 " +
                    "따라갔습니다. ⚠ 이미 저장된 세이브의 이 스탯은 초기값으로 떨어집니다.");

                Draw();
            }),
            caution:
                "⚠ 키는 세이브의 열쇠입니다 — 바꾸면 이미 저장된 회차에서 이 스탯이 " +
                "초기값으로 떨어집니다(경고 없이). 표시 이름만 고치려면 닫고 옆의 " +
                "[표시이름] 칸을 쓰세요.");
    }

    /// <summary>글자 하나를 받는 작은 플라이아웃 — [챕터 추가]와 같은 손버릇이다.</summary>
    private static void PromptFlyout(
        Control anchor, string placeholder, Action<string> accept, string? caution = null)
    {
        var box = new TextBox { PlaceholderText = placeholder, FontSize = 11, MinWidth = 200 };
        var panel = new StackPanel { Spacing = 4, MaxWidth = 340 };

        // ⚠ 경고가 <b>칸보다 위</b>에 선다 — 적고 나서 읽는 경고는 경고가 아니다.
        if (caution is { Length: > 0 })
        {
            panel.Children.Add(new TextBlock
            {
                Text = caution,
                FontSize = 10,
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap
            });
        }

        panel.Children.Add(box);

        var flyout = new Flyout { Content = panel };

        void Accept()
        {
            string text = (box.Text ?? string.Empty).Trim();

            if (text.Length == 0)
            {
                return;
            }

            flyout.Hide();
            accept(text);
        }

        box.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
            {
                args.Handled = true;
                Accept();
            }
        };

        flyout.ShowAt(anchor);
        box.Focus();
    }

    /// <summary>
    /// 스탯을 지운다 — 거절이 <b>어디서 쓰는지</b>를 들고 온다. 그 목록이 곧 정리 안내다.
    /// </summary>
    private void RemoveStat(Control anchor, string key)
    {
        Vn.Authoring.Editing.StatRemoveOutcome outcome = _session!.Editor.RemoveChapterStat(_selectedChapterId!, key);

        if (outcome.Applied)
        {
            _session.SetStatus($"스탯 '{key}'를 지웠습니다.");
            Draw();

            return;
        }

        if (outcome.Uses.Count == 0)
        {
            throw new InvalidOperationException(outcome.Refusal ?? "지우지 못했습니다.");
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
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (Vn.Authoring.Editing.ChapterUse use in refused.Uses)
        {
            panel.Children.Add(new SelectableTextBlock
            {
                Text = $"· {use.Where} — {use.Detail}",
                FontSize = 10,
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = "비우면 간선에서는 그 항만 빠지고, 조건은 식만 비고 남습니다 " +
                   "(검증이 빈 식을 짚어 줍니다).",
            FontSize = 10,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var clear = new Button
        {
            Content = "모두 비우고 지우기",
            FontSize = 10,
            Padding = new Thickness(7, 2),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var flyout = new Flyout { Content = panel };

        clear.Click += (_, _) =>
        {
            flyout.Hide();

            UiGuard.Run(_session, "스탯 비우고 삭제", () =>
            {
                Vn.Authoring.Editing.StatRemoveOutcome done =
                    _session!.Editor.RemoveChapterStat(_selectedChapterId!, key, clearUses: true);

                if (!done.Applied)
                {
                    throw new InvalidOperationException(done.Refusal ?? "지우지 못했습니다.");
                }

                _session.SetStatus(
                    $"스탯 '{key}'를 지웠습니다 — 간선 {done.EdgesCleared}곳의 항을 빼고 " +
                    $"조건 {done.ConditionsCleared}곳의 식을 비웠습니다.");

                Draw();
            });
        };

        panel.Children.Add(clear);
        flyout.ShowAt(anchor);
    }

    private void RemoveCondition(string label)
    {
        Vn.Authoring.Editing.ChapterConditionRemoveOutcome outcome =
            _session!.Editor.RemoveChapterCondition(_selectedChapterId!, label);

        if (!outcome.Applied)
        {
            throw new InvalidOperationException(Explain(outcome.Refusal, outcome.Uses));
        }

        _session.SetStatus($"조건 '{label}'을 지웠습니다.");
        Draw();
    }

    /// <summary>
    /// 거절 사유 + <b>쓰는 자리 목록</b>. ⛔ *"쓰이고 있습니다"*로 끝내면 사람이 챕터를
    /// 뒤져야 한다 — 어디인지 말해야 거절이 정리 안내가 된다.
    /// </summary>
    private static string Explain(string? refusal, IReadOnlyList<Vn.Authoring.Editing.ChapterUse> uses)
    {
        if (uses.Count == 0)
        {
            return refusal ?? "지우지 못했습니다.";
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

            return;
        }

        _selectedEpisodeId = newId;

        _session.SetStatus(
            $"'{oldId}' → '{newId}' 개명했습니다. 간선·픽스처·대사 노드가 함께 따라갔습니다.");
    }

    /// <summary>
    /// [에피소드 삭제] — 규율은 <see cref="EpisodeDeleter"/> 하나다.
    ///
    /// ⛔ <b>2026-09-18까지 그 규율이 여기 살았다</b>(원고 밀기·행 지우기·카드 거두기
    /// 100여 줄). 다른 화면에 같은 기능을 붙이려면 사본을 뜨는 수밖에 없었고, 사본은
    /// 갈린다 — 바로 앞의 「에피소드 만들기」가 그렇게 갈려 있었다. 화면에 남는 것은
    /// <b>무엇을 골랐는가</b>와 <b>결과를 어떻게 말하는가</b>뿐이다.
    /// </summary>
    internal void DeleteSelectedEpisode()
    {
        if (_selectedEpisodeId is not { } episodeId || _selectedChapterId is not { } chapterId)
        {
            // 조용한 무동작 금지 — 단추가 늘 떠 있으므로(2026-08-16) 대상이 없다는 것을 말한다.
            _session?.SetStatus("지울 에피소드를 판에서 먼저 골라 주세요.");
            return;
        }

        EpisodeDeleter.Result result = EpisodeDeleter.Delete(_session!.Editor, chapterId, episodeId);

        if (!result.Ok)
        {
            _session.SetStatus(result.Failure!);
            return;
        }

        _selectedEpisodeId = null;

        _session.SetStatus(result.Describe(episodeId));
    }

    /// <summary>
    /// [＋ 에피소드] — 에피소드가 선택돼 있으면 <b>그 자식으로</b> 만들어 간선까지 잇는다
    /// (자리 = 부모 깊이 + 1 열, v3 소유자 지시). 선택이 없으면 홀로 선 노드다.
    /// Id는 자동 발명하지 않되 빈 이름으로 둘 수는 없으니, 겹치지 않는 자리표시 Id를 주고
    /// 사람이 패널의 [이름] 칸에서 고쳐 정하게 한다(Enter로 확정).
    /// </summary>
    internal void AddEpisodeFromToolbar()
    {
        if (_selectedChapterId is not { } chapterId || SelectedModel is not { } model)
        {
            _session?.SetStatus("챕터를 먼저 선택해 주세요.");
            return;
        }

        // ⚠ Id 짓는 규칙은 편집기 하나다 — 화면마다 세면 하나가 다른 목록을 훑는다
        //    (여기는 <b>워크북 모델</b>을, 대본 탭은 <b>챕터 문서</b>를 보고 있었다).
        string episodeId = _session!.Editor.NextEpisodeId(chapterId);

        // ⭐ v12 (2026-08-24 소유자) — **에피소드를 더하면 간선이 함께 선다.** 고른 것이
        // 없으면 마지막 에피소드에서 잇는다: 떨어진 섬을 만들지 않는다(도달성 증명이 곧바로
        // 오류로 짚을 자리이기도 하다). 챕터가 비어 있을 때만 간선 없이 첫 카드가 선다.
        string? parent = _selectedEpisodeId is { } id && model.FindEpisode(id) is not null
            ? id
            : model.Episodes.Count > 0 ? model.Episodes[^1].EpisodeId : null;

        if (parent is not null)
        {
            (double x, double y) = ChapterBranchPlanner.SuggestPlacement(model, parent);
            // 문구를 함께 준다 — v12에서 모든 길은 선택지이고, 빈 문구는 오류다.
            _session!.Editor.AddNextEpisode(
                chapterId, parent, episodeId, title: string.Empty, x, y, optionLabel: DefaultOptionLabel);
        }
        else
        {
            double x = model.Episodes.Count == 0 ? 0 : model.Episodes.Max(episode => episode.X) + 220;
            _session!.Editor.AddEpisode(chapterId, episodeId, title: string.Empty, x, 0);
        }

        _selectedEpisodeId = episodeId;

        // ⛔ 여기 있던 `EnsureDialogueNodeFor`는 2026-09-18에 걷혔다. 화면이 에피소드를
        //    만든 <b>뒤에</b> 카드를 한 줄 더 세우던 자리인데, 그 한 줄을 대본 탭이
        //    잊어서 <b>작가의 탭에서 만든 에피소드만 쓸 자리가 없었다</b>.
        //    이제 `AddEpisode`·`AddNextEpisode`가 카드까지 함께 만든다 — 창구가 잊을
        //    자리 자체가 없어졌다.

        _session?.SetStatus($"'{episodeId}'을 더했습니다. Id와 대사엔트리를 패널에서 채워 주세요.");
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
            edge.VisibleConditionLabel is null ? null : $"표시: {edge.VisibleConditionLabel}",
            edge.ConditionLabel is null ? null : $"해금: {edge.ConditionLabel}"
        }.Where(part => part is not null))));

    private static string Tooltip(ChapterGraphModel model, ChapterEpisode episode)
    {
        var lines = new List<string>
        {
            episode.EpisodeId,
            $"대사엔트리: {episode.DialogueEntry}",
            $"위치: X={episode.X:0.##} Y={episode.Y:0.##}",
            $"선택지 {model.Edges.Count(edge => string.Equals(edge.FromEpisodeId, episode.EpisodeId, StringComparison.Ordinal))}개"
        };

        string gate = GateSummary(GatedIncoming(model, episode));

        if (gate.Length > 0)
        {
            lines.Add("들어오는 길의 관문 —");
            lines.Add(gate);
        }

        if (!string.IsNullOrWhiteSpace(episode.Memo))
        {
            lines.Add($"메모: {episode.Memo}");
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

    /// <summary>
    /// 판 수준 경고 (2단계 가드레일) — 진행이 착지하는 카드로 향하는 출구. 막지 않고 크게 말한다.
    ///
    /// ⚠ 예전에는 엑셀 [대본 가져오기]를 누를 때만 셌다(2026-10-29 · D2에서 옮겼다). 그래서 연출
    /// 그래프에서 출구를 고쳐도 가져오기를 다시 누를 때까지 경고가 남거나 안 떴다. 이제 그릴 때마다
    /// 판에서 센다 — 판의 노드를 한 번 훑을 뿐이라 가볍다.
    /// </summary>
    private IReadOnlyList<ChapterDiagnostic> BoardWarnings(ChapterGraphModel model) =>
        _session is not null && EpisodeNaming.BoardOf(_session.Project, model.ChapterId) is { } board
            ? ChapterBoardSupply.WarnExitsIntoLandingCards(_session.Editor, board.Id, model)
            : Array.Empty<ChapterDiagnostic>();

    private void DrawDiagnostics(ChapterGraphModel model)
    {
        // 모델 진단(구조 검사 포함) + 검증기(구조·교차·도달성) 진단을 한 목록으로 본다.
        // 같은 진단이 두 곳에서 나올 수 있으므로(모델이 낸 것을 검증기가 다시 담는다) 겹은 지운다.
        List<ChapterDiagnostic> all = model.Diagnostics
            .Concat(_validation?.All ?? Enumerable.Empty<ChapterDiagnostic>())
            .Concat(BoardWarnings(model))
            .Distinct()
            .ToList();

        int errors = all.Count(item => item.Severity == ChapterDiagnosticSeverity.Error);
        int warnings = all.Count(item => item.Severity == ChapterDiagnosticSeverity.Warning);

        string? exportNotice = ExportNotice();

        var header = new System.Text.StringBuilder("검증 보고");

        if (errors > 0)
        {
            header.Append($" · {ErrorMark} 오류 {errors}");
        }

        if (warnings > 0)
        {
            header.Append($" · {WarningMark} 경고 {warnings}");
        }

        if (exportNotice is not null)
        {
            // 접혀 있어도 보여야 한다 — 런타임으로 나갈 것이 안 나간 상태다.
            header.Append($" · {ErrorMark} 진행 JSON 미출력");
        }

        if (header.Length == "검증 보고".Length)
        {
            header.Append($" — 오류 없음 (알림 {all.Count}건)");
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
                DiagnosticLine(exportNotice, Brushes.IndianRed, dim: false, bold: true));
        }

        // 서버는 이 문자열이 아니라 **파일 바이트**를 해시한다. 내보내기와 같은
        // ChapterExportBytes 경로에서 계산한 값을 보여 줘야 서버 버전 목록과 대조할 수 있다.
        if (_selectedChapterId is { } selected &&
            _exportRun.Checksums?.TryGetValue(selected, out string? checksum) == true)
        {
            DiagnosticsPanel.Children.Add(DiagnosticLine(
                $"진행 JSON SHA-256: {checksum}", null, dim: true));
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
                incomplete.Message, Brushes.DarkGoldenrod, dim: false, bold: true));
        }

        // ⚠ 세는 것은 <b>말할 것이 있는</b> 보고뿐이다 (2026-08-24). 예전에는 보고가
        // 하나라도 있으면 여기를 지나쳤는데, 이제 대부분의 보고가 아무 줄도 내지 않으므로
        // 그대로 두면 <b>텅 빈 상자</b>가 선다("보고할 것이 없습니다"조차 없이).
        if (all.Count == 0 &&
            !(_selectedChapterId is { } id && _exportRun.Checksums?.ContainsKey(id) == true))
        {
            DiagnosticsPanel.Children.Add(new TextBlock
            {
                Text = "보고할 것이 없습니다.",
                FontSize = 11,
                Opacity = 0.55
            });

            return;
        }

        foreach (ChapterDiagnostic diagnostic in all
                     .OrderByDescending(item => item.Severity)
                     .ThenBy(item => item.Sheet, StringComparer.Ordinal)
                     .ThenBy(item => item.Row ?? 0))
        {
            DiagnosticsPanel.Children.Add(DiagnosticLine(
                diagnostic.Describe(),
                diagnostic.Severity switch
                {
                    ChapterDiagnosticSeverity.Error => Brushes.IndianRed,
                    ChapterDiagnosticSeverity.Warning => Brushes.DarkGoldenrod,
                    _ => null
                },
                dim: diagnostic.Severity == ChapterDiagnosticSeverity.Info));
        }

        OfferStatRegistration(model, all);
    }

    /// <summary>
    /// "스탯이 정의 파일에 없다" 경고 밑에 [등록] 단추를 세운다. 시트를 자동으로 믿고 쓰면
    /// 오타까지 게임 어휘가 되므로, 정의 파일 쓰기는 사람이 이 단추를 누른 순간에만 일어난다.
    /// </summary>
    private void OfferStatRegistration(ChapterGraphModel model, List<ChapterDiagnostic> all)
    {
        if (_session?.ProjectPath is null ||
            all.All(item => item.Code != ChapterDiagnosticCode.StatMissingFromGameDefinition))
        {
            return;
        }

        GameDefinition definition = _session.Definition;

        VariableSpec[] missing = model.Stats
            .Where(stat => !definition.Variables.Any(variable =>
                string.Equals(variable.Name, stat.Key, StringComparison.Ordinal)))
            .Select(stat => new VariableSpec
            {
                Name = stat.Key,
                Type = "number",
                Description = string.IsNullOrWhiteSpace(stat.DisplayName) ? null : stat.DisplayName
            })
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        var register = new Button
        {
            Content = $"이 스탯 {missing.Length}개를 {GameDefinition.FileName}에 등록",
            FontSize = 10,
            Padding = new Thickness(8, 2),
            Margin = new Thickness(0, 4, 0, 2)
        };

        ToolTip.SetTip(register,
            "정의 파일의 variables에 없는 이름만 더합니다. 값(초기/최소/최대)은 계속 스탯 시트가 소유합니다.");

        register.Click += (_, _) => UiGuard.Run(_session, "스탯 등록", () =>
        {
            if (_session.RegisterVariables(missing))
            {
                Reload(); // 새 정의로 다시 읽어 경고가 걷힌 화면을 보인다
            }
        });

        DiagnosticsPanel.Children.Add(register);
    }

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
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0));

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
        {
            _session?.SetStatus("클립보드에 접근할 수 없습니다.");
            return;
        }

        await clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
        _session?.SetStatus($"검증 보고 {lines.Count}줄을 복사했습니다.");
    }

    // SelectableTextBlock — [보고 복사]가 전체를 들고 가고, 드래그는 한 줄만 집어 간다.
    private static TextBlock DiagnosticLine(
        string text, IBrush? foreground, bool dim, bool bold = false) => new SelectableTextBlock
    {
        Text = text,
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Foreground = foreground,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        Opacity = dim ? 0.6 : 1
    };
}
