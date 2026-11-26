using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.Authoring.Chapters;
using Vn.Authoring.Text;
using Vn.App.Strings;

namespace Vn.App.Views;

/// <summary>하이어아키 한 줄의 종류.</summary>
internal enum HierarchyKind
{
    Project,
    Chapter,
    Scene,
    Episode,

    /// <summary>분기 카드 — 부르는 에피소드 밑의 「⑂ 이름」 줄(2026-11-17). 에피소드가 아니다 — 고르면 그 카드(칩)로 간다.</summary>
    Branch
}

/// <summary>
/// 줄의 눈 — 판에서 보이는가 (hierarchy §3). 유니티 하이어아키의 보이기 기둥과 같은 자리다.
/// 연출 그래프의 챕터 · 장면 줄에만 선다(챕터 그래프에는 접을 판이 없다).
/// </summary>
internal enum HierarchyEye
{
    /// <summary>눈이 없는 줄 — 프로젝트 · 에피소드, 또는 눈 기둥이 없는 하이어아키.</summary>
    None,

    /// <summary>펼쳐 보인다.</summary>
    Open,

    /// <summary>접었다 — 챕터는 판 상자, 장면은 줄 목록 상자로 간소화.</summary>
    Folded,

    /// <summary>제 상태와 상관없이 위(챕터)가 접혀 가려졌다 — 흐린 눈.</summary>
    UnderFolded
}

/// <summary>
/// 하이어아키 줄의 우클릭 차림표에서 하자고 한 일 —<b>하는 것은 이 컨트롤이 아니다</b>(셸이 편집기 규칙
/// 한 벌로 한다).
/// </summary>
internal enum HierarchyCommand
{
    /// <summary>새 챕터 — 프로젝트에(프로젝트 줄 · 빈 곳).</summary>
    AddChapter,

    /// <summary>새 장면 — 누른 챕터 줄의 챕터에, 첫 에피소드와 함께(장면은 에피소드가 들어야 있다).</summary>
    AddScene,

    /// <summary>
    /// 새 에피소드 — 누른 장면 줄의 장면에, 길 없이(2026-11-18). ⛔ 챕터 줄의 「에피소드 추가」(장면 없는 에피소드)와 에피소드 줄의 것(그 뒤에 길로
    /// 이어 붙이기)은 걷었다.
    /// </summary>
    AddEpisode,

    /// <summary>
    /// 에피소드 줄 — 그 에피소드를 지운다. ⛔ 챕터 줄의 「챕터 지우기」 · 장면 줄의 「장면 지우기」는 2026-11-18에 걷혔다(소유자 — 줄을 고르고 [대본]
    /// 툴바의 [삭제]로 지운다 · <c>GraphEditorView.DeleteButton</c>).
    /// </summary>
    DeleteEpisode

}

/// <summary>
/// 하이어아키의 접힘 — 사람이 편 마디와 접은 마디.<b>두 탭의 하이어아키가 이 하나를 함께 쓴다</b> — 한
/// 그루가 두 자리에 보이는 것이라 한쪽에서 접으면 다른 쪽도 접힌다. 프로젝트별로 사용자 설정에 기억한다(프로젝트 파일은 팀의
/// 것이라 거기 안 쓴다 — 내가 접은 것이 남의 화면을 접으면 안 된다).
/// </summary>
internal sealed class HierarchyFolds
{
    /// <summary>사람이 편 마디들.</summary>
    public HashSet<string> Expanded { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 사람이 접은 마디들 — 편 것과 따로 든다. 펼침만 기억하면 "고른 것이 든 마디는 저절로 펴진다"가 사람의 접기를
    /// 이겨 접을 수가 없다.
    /// </summary>
    public HashSet<string> Collapsed { get; } = new(StringComparer.Ordinal);

    /// <summary>접힘을 기억하는 프로젝트. 없으면 세션 안에서만 산다.</summary>
    private string? _projectPath;

    /// <summary>
    /// 이 프로젝트의 접힘을 잇는다 — 프로젝트가 바뀌면 들고 있던 것을 놓고 새로 읽는다(앞 프로젝트의 열쇠가 이름이 같은
    /// 다른 챕터를 접으면 안 된다). 같은 프로젝트면 아무것도 안 한다(다시 그릴 때마다 지나가는 자리다).
    /// </summary>
    public void Remember(string? projectPath)
    {
        if (string.Equals(_projectPath, projectPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _projectPath = projectPath;
        Expanded.Clear();
        Collapsed.Clear();

        if (projectPath is null)
        {
            return;
        }

        (IReadOnlyList<string> expanded, IReadOnlyList<string> collapsed) = AppSettingsService.LoadExplorerState(projectPath);
        Expanded.UnionWith(expanded);
        Collapsed.UnionWith(collapsed);
    }

    /// <summary>사람이 접거나 폈다 — 기억한다(프로젝트가 저장된 적이 없으면 세션 안에서만).</summary>
    public void Save()
    {
        if (_projectPath is not null)
        {
            AppSettingsService.SaveExplorerState(_projectPath, Expanded, Collapsed);
        }
    }
}

/// <summary>
/// 하이어아키에서 고른 것 — 인스펙터가 이것을 보인다. 장면 · 에피소드는 제 챕터를 함께 든다(Id는 챕터 안에서만 유일하다).
/// </summary>
/// <param name="NodeId">분기 카드 줄이면 그 카드 — 그때 <paramref name="EpisodeId"/>는 그 집(부르는 에피소드)이다.</param>
internal sealed record HierarchyPick(
    HierarchyKind Kind,
    string? ChapterId = null,
    string? SceneId = null,
    string? EpisodeId = null,
    string? NodeId = null)
{
    public static HierarchyPick Project { get; } = new(HierarchyKind.Project);

    /// <summary>그 줄의 신원 — <see cref="HierarchyRow.Key"/>와 같은 열쇠.</summary>
    public string Key => Kind switch
    {
        HierarchyKind.Project => StoryHierarchy.ProjectKey,
        HierarchyKind.Chapter => StoryHierarchy.ChapterKey(ChapterId!),
        HierarchyKind.Scene => StoryHierarchy.SceneKey(ChapterId!, SceneId!),
        HierarchyKind.Branch => StoryHierarchy.BranchKey(ChapterId!, NodeId!),
        _ => StoryHierarchy.EpisodeKey(ChapterId!, EpisodeId!)
    };
}

/// <summary>
/// 하이어아키에 보이는 줄 하나 — <b>검증의 손잡이</b>이기도 하다(그려진 컨트롤이 아니라 이 목록으로 구조를 잰다).
/// </summary>
/// <param name="Key">그 줄의 신원 — 접힘과 선택을 다시 그린 뒤에도 같은 줄에 잇는다(줄 번호는 편집 한 번에 바뀐다).</param>
/// <param name="Find">찾기가 견주는 글 — ID와 이름(줄마다 하나 · <c>\n</c>으로 가른다). 보이는 글이 ID든 이름이든 둘 다로 찾는다. null이면 보이는 글.</param>
internal sealed record HierarchyRow(
    HierarchyKind Kind,
    string Key,
    string? ChapterId,
    string? SceneId,
    string? EpisodeId,
    string Text,
    string? Detail,
    int Depth,
    bool IsSceneRoot,
    bool HasWarning,
    HierarchyEye Eye = HierarchyEye.None,
    string? NodeId = null,
    string? Find = null)
{
    public HierarchyPick Pick => new(Kind, ChapterId, SceneId, EpisodeId, NodeId);
}

/// <summary>
/// <b>하이어아키</b> — 유니티 하이어아키처럼 챕터 아래 장면, 장면 아래 그 장면의 에피소드들을 모은다
/// (hierarchy). 프로젝트 → 챕터 → 장면 → 에피소드를 접었다 펴는
/// 한 그루이고, 어느 줄이든 고르면 <see cref="Picked"/>가 알린다 — 인스펙터가 그것을 보인다.
///
/// <b>장면은 여기서도 엔티티가 아니다</b> — 장면 줄은 <see cref="ChapterSceneGrouping"/>이 그릴 때마다 묶는 투영이고,
/// 이 컨트롤이 든 상태는 <b>접힘과 선택 둘뿐</b>이다. 에피소드는 늘 장면에 속하므로 챕터 밑에는 늘 장면 줄이 서고 에피소드는 그
/// 밑에 선다(⛔ 장면ID를 하나도 안 적은 챕터의 장면 단을 생략하던 것은 장면 없는 에피소드와 함께 걷혔다).
///
/// 주의: <b>고르는 것으로는 줄을 다시 세우지 않는다</b>(칠만 한다 — <see cref="Paint"/>). 다시 세우면 더블클릭의 둘째 누름이
/// 새 컨트롤에 떨어진다. 보이는 줄이 바뀔 때(접기 · 펴기 · 밖에서 고른 것을 드러낼 때)만 다시 세운다.
///
/// 주의: <b>편집하지 않는다</b> — 우클릭 차림표(<see cref="CommandRequested"/>) · 두 번 눌러 이름 고치기(<see cref="RenameRequested"/>) ·
/// 끌어 다른 장면으로(<see cref="Dropped"/>)는 알리기만 한다(만들기 · 지우기 · 개명 규칙은 셸 쪽 편집기 한 벌이다). 접힘은 두
/// 탭이 함께 쓰고 프로젝트별로 기억한다(<see cref="HierarchyFolds"/>). 키보드 ↑↓←→로 고르고 접는다.
///
/// ⭐ <b>여러 줄 고르기</b>(2026-11-23 소유자 — 「여러개를 shift + 우클릭으로 한번에 선택하는게 가능할까요?」 → 고른 답 「Ctrl+클릭 · Shift+클릭」 ·
/// 「섞어 고를 수 있음」 · 「마지막 줄을 따르고 [삭제]는 끔」 · hierarchy §5). Ctrl(맥 ⌘)+클릭은 그 줄을 더하고 빼고, Shift+클릭은
/// 기준 줄(마지막으로 그냥 · Ctrl로 누른 줄)부터 그 줄까지 보이는 줄을 모두 고른다. 챕터 · 장면 · 에피소드가 섞여도 된다. 판 · 인스펙터는 마지막에 누른 줄
/// (<see cref="Selection"/>)만 따르고, 고른 줄 모두는 <see cref="Selections"/>다 — 나중에 「고른 것에 든 에피소드」를 한꺼번에 바꾸는 데 쓴다(소유자 — 빈 선택지
/// 숨기기 · 글 서식). 그냥 누르기 · 화살표 · 밖에서 고른 다른 것은 다시 한 줄이다.
///
/// ⭐ <b>찾기</b>(2026-11-23 소유자 — 「구성과 대본의 하이어아키에 검색기능만 추가합시다. 이게 생각보다 에피소드가 많을 때, 특정 에피소드 찾는게 힘들더라고요」 ·
/// hierarchy §6). 위의 찾기 칸에 치는 동안 <b>맞는 줄과 그 위 줄(챕터 · 장면)만</b> 남긴다 — 접어 둔 챕터 · 장면 안도 찾는다(찾는 동안은
/// 접힘을 보지 않고, 접힘은 그대로 둔다). 챕터 · 장면 · 에피소드 · 분기 카드를 ID와 이름 둘 다로, 화자 칸 자동완성과 같은 규칙(<see cref="Hangul.Matches"/> — 들어
/// 있으면 · 자음만 쳐도)으로 찾는다. 줄을 누르면 늘 그대로 고른다(판이 따른다). Enter · ↓는 첫 맞는 줄로, Esc는 지운다 — 지우면 고른 줄이 보이게 돌아간다.
/// </summary>
public partial class StoryHierarchy : UserControl
{
    internal const string ProjectKey = "project";

    internal static string ChapterKey(string chapterId) => "ch:" + chapterId;

    internal static string SceneKey(string chapterId, string sceneId) => "sc:" + chapterId + "/" + sceneId;

    internal static string EpisodeKey(string chapterId, string episodeId) => "ep:" + chapterId + "/" + episodeId;

    internal static string BranchKey(string chapterId, string nodeId) => "br:" + chapterId + "/" + nodeId;

    /// <summary>접힘 — 셸이 두 탭의 하이어아키에 같은 것을 준다(<see cref="HierarchyFolds"/>).</summary>
    internal HierarchyFolds Folds { get; set; } = new();

    /// <summary>
    /// 줄마다의 눈 — 셸이 준다(연출 그래프의 하이어아키만). null이면 눈 기둥이 없다. 눈의 상태는 판 쪽(세션)이
    /// 들고 이 컨트롤은 그리기만 한다 — 줄을 세울 때마다 묻는다.
    /// </summary>
    internal Func<HierarchyRow, HierarchyEye>? EyeOf { get; set; }

    /// <summary>눈을 눌렀다 — 셸이 그 판이나 장면을 접거나 편다.</summary>
    internal event Action<HierarchyRow>? EyeToggled;

    private readonly List<HierarchyRow> _rows = [];

    private string _projectTitle = string.Empty;
    private IReadOnlyList<ChapterEntry> _chapters = [];
    private bool _hasProject;

    /// <summary>프로젝트가 저장돼 있는가(<see cref="Rebuild"/>) — 빈 목록의 안내를 고른다.</summary>
    private bool _saved = true;
    private readonly ContextMenu _emptyAreaMenu;
    private bool _readOnly;

    public StoryHierarchy()
    {
        InitializeComponent();

        // 줄을 누르면 초점이 하이어아키로 와야 화살표(↑↓←→)가 여기로 온다(Press의 Focus). 줄 단추는 초점을 받지 않으므로 이 컨트롤이
        // 받는다. ⛔ 2026-11-18까지는 초점을 받지 못해 실제 앱에서 화살표가 듣지 않았다 — 시험이 키 알림을 직접 쏴서 몰랐다(side-fold K2).
        Focusable = true;

        // 빈 자리의 차림표 — 줄 위에서 우클릭하면 그 줄의 차림표가 먼저 뜨고, 빈 데서 눌렀을 때만 여기까지 온다.
        _emptyAreaMenu = new ContextMenu
        {
            ItemsSource = new[] { MenuItemFor(Ui.Board_AddChapter, HierarchyCommand.AddChapter, row: null, anchor: TreeScroll) }
        };
        TreeScroll.ContextMenu = _emptyAreaMenu;

        SearchBox.TextChanged += (_, _) => UiGuard.Run(null, Ui.Hierarchy_FindInHierarchy, () => Search(SearchBox.Text));
        SearchBox.KeyDown += (_, args) => UiGuard.Run(null, Ui.Hierarchy_FindInHierarchy, () => SearchKey(args));
    }

    // ── 찾기 ────────────────────────────────────────────────────────────────

    private string _query = string.Empty;

    /// <summary>찾는 중인가 — 찾기 칸에 글이 있다.</summary>
    internal bool Searching => _query.Length > 0;

    /// <summary>지금 찾는 말에 맞는 줄 수(그 위 줄은 빼고) — 찾지 않으면 0.</summary>
    internal int SearchHits { get; private set; }

    private void Search(string? text)
    {
        string query = (text ?? string.Empty).Trim();

        if (string.Equals(query, _query, StringComparison.Ordinal))
        {
            return;
        }

        bool ended = Searching && query.Length == 0;
        _query = query;
        Refresh();

        // 찾기를 걷으면 고른 줄이 보이게 — 찾다가 누른 줄이 접힘이 돌아온 그루 어디에 섰는지 바로 보인다.
        if (ended && Selection is { } pick)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => RowButton(pick.Key)?.BringIntoView());
        }
    }

    /// <summary>찾기 칸의 키 — Enter · ↓는 첫 맞는 줄을 고르고(초점이 하이어아키로 와 ↑↓로 이어 고른다), Esc는 지운다.</summary>
    private void SearchKey(Avalonia.Input.KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Avalonia.Input.Key.Enter or Avalonia.Input.Key.Down when Searching:
                args.Handled = true;

                if (_rows.FirstOrDefault(Matches) is { } first)
                {
                    Press(first);
                    RowButton(first.Key)?.BringIntoView();
                }

                break;

            case Avalonia.Input.Key.Escape when Searching:
                args.Handled = true;
                SearchBox.Text = string.Empty;
                break;
        }
    }

    /// <summary>그 줄이 찾는 말에 맞는가 — ID와 이름(<see cref="HierarchyRow.Find"/>)과 보이는 글 가운데 하나라도. 프로젝트 줄은 늘 서는 뿌리라 세지 않는다.</summary>
    private bool Matches(HierarchyRow row) =>
        Searching && row.Kind != HierarchyKind.Project &&
        (row.Find ?? row.Text).Split('\n').Append(row.Text).Any(text => Hangul.Matches(text, _query));

    /// <summary>
    /// 찾는 동안 남길 줄 — 맞는 줄과 그 위 줄(그 줄을 품은 챕터 · 장면 · 에피소드)만, 차례 그대로. 줄은 깊이 차례(앞 → 뒤)로 서 있으므로 뒤에서부터 「아래에
    /// 남는 줄이 있는가」를 깊이마다 들고 온다. 프로젝트 줄은 늘 남는다.
    /// </summary>
    private List<HierarchyRow> Filtered(List<HierarchyRow> rows)
    {
        SearchHits = 0;

        if (!Searching || rows.Count == 0)
        {
            return rows;
        }

        var below = new bool[rows.Max(row => row.Depth) + 2];
        var keep = new bool[rows.Count];

        for (int index = rows.Count - 1; index >= 0; index--)
        {
            int depth = rows[index].Depth;
            bool hit = Matches(rows[index]);
            SearchHits += hit ? 1 : 0;
            keep[index] = rows[index].Kind == HierarchyKind.Project || hit || below[depth + 1];
            Array.Clear(below, depth + 1, below.Length - depth - 1);
            below[depth] |= keep[index];
        }

        return rows.Where((_, index) => keep[index]).ToList();
    }

    /// <summary>
    /// 고르기 · 접기 · 눈만 되는 하이어아키 — 우클릭 차림표(추가 · 지우기 · 이름 바꾸기) · 두 번 눌러 이름 고치기 · 끌어 장면
    /// 옮기기가 없다. [구성] 탭의 하이어아키다(챕터 · 장면 · 에피소드는 [대본] 탭에서 더하고 지우고 고친다).
    /// </summary>
    internal bool ReadOnly
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            TreeScroll.ContextMenu = value ? null : _emptyAreaMenu;
            _renamingKey = null;
            Draw();
        }
    }

    /// <summary>어느 줄을 골랐다(사람이 눌렀을 때만 — <see cref="Show"/>로 밖에서 고른 것은 알리지 않는다).</summary>
    internal event Action<HierarchyPick>? Picked;

    /// <summary>줄의 우클릭 차림표에서 무엇을 하자고 했다 — 그 줄과, 차림을 띄울 과녁(그 줄의 컨트롤)과 함께.</summary>
    internal event Action<HierarchyCommand, HierarchyRow, Control>? CommandRequested;

    /// <summary>
    /// 이름을 고쳐 달라고 했다(줄을 두 번 눌러 새 이름을 적고 Enter). 여기서 고치지 않는다 — 챕터 개명은 판 · 조건 배관을,
    /// 에피소드 개명은 간선 · 카드를 함께 끌고 간다(규칙은 셸 쪽 한 벌).
    /// </summary>
    internal event Action<HierarchyRow, string>? RenameRequested;

    /// <summary>
    /// 줄을 보이는 이름(챕터 표시 이름 · 장면 이름 · 에피소드 제목)으로 세우고, 이름 고치기도 그 이름을 고친다 — [대본] 탭의 하이어아키다.
    /// 끄면 ID로 세우고 이름은 곁글로 붙는다([구성] 탭 — 「ID로 보기」를 끄면 [구성]도 켠다 · 2026-11-18). 바뀌면 줄을 다시 세운다.
    /// </summary>
    internal bool ShowEpisodeTitles
    {
        get => _showEpisodeTitles;
        set
        {
            if (_showEpisodeTitles != value)
            {
                _showEpisodeTitles = value;
                Refresh();
            }
        }
    }

    private bool _showEpisodeTitles;

    /// <summary>이름을 고치는 중인 칸 — 화면 시험의 손잡이이자 실제 입력칸이다. 없으면 null.</summary>
    internal TextBox? RenameBox { get; private set; }

    /// <summary>지금 이름을 고치고 있는 줄.</summary>
    private string? _renamingKey;

    /// <summary>
    /// 줄을 세우는 중인가. 세우다가 초점을 쥔 이름 칸이 판에서 빠지면 「딴 데를 누르면 없던 일로」(<c>LostFocus</c>)가 그 자리에서 불린다 —
    /// 거기서 또 세우면 안쪽이 줄을 다 세운 뒤 바깥이 이어서 한 벌을 더 붙인다(2026-11-18 소유자 보고 — 이름 칸을 연 채로 다른 에피소드를
    /// 누르면 프로젝트가 둘로 보였다). 그때는 이름 칸만 닫고 세우기는 진행 중인 쪽에 맡긴다.
    /// </summary>
    private bool _drawing;

    /// <summary>지금 고른 것 — 여러 줄을 골랐으면 마지막에 누른 줄(판 · 인스펙터가 따르는 것). 없으면 null.</summary>
    internal HierarchyPick? Selection { get; private set; }

    /// <summary>고른 줄 모두 — 고른 차례이고 <see cref="Selection"/>이 마지막이다. 한 줄만 골랐으면 그 하나, 없으면 빈 목록.</summary>
    internal IReadOnlyList<HierarchyPick> Selections => _picks;

    private readonly List<HierarchyPick> _picks = [];

    /// <summary>Shift+클릭의 기준 줄 — 마지막으로 그냥 · Ctrl로 누른 줄.</summary>
    private string? _anchorKey;

    /// <summary>줄 단추를 누를 때의 보조 키 — 누름(터널)에서 적고 Click이 읽는다(Click은 보조 키를 싣지 않는다).</summary>
    private Avalonia.Input.KeyModifiers _pressModifiers;

    /// <summary>고른 줄들이 바뀌었다(수 · 무엇) — 셸이 [삭제]를 맞춘다. 사람이 눌렀을 때도, 밖에서 고른 것을 비출 때도.</summary>
    internal event Action? SelectionsChanged;

    /// <summary>고른 줄들을 한 줄로(없으면 비운다) — 바뀌었으면 알린다.</summary>
    private void PickOnly(HierarchyPick? pick)
    {
        bool changed = _picks.Count != (pick is null ? 0 : 1) ||
                       (pick is not null && !string.Equals(_picks[0].Key, pick.Key, StringComparison.Ordinal));
        _picks.Clear();

        if (pick is not null)
        {
            _picks.Add(pick);
        }

        Selection = pick;

        if (changed)
        {
            SelectionsChanged?.Invoke();
        }
    }

    /// <summary>지금 보이는 줄들.</summary>
    internal IReadOnlyList<HierarchyRow> Rows => _rows;

    /// <summary>그 줄의 컨트롤(이름 단추) — 화면 시험이 누를 자리.</summary>
    internal Button? RowButton(string key)
    {
        int index = _rows.FindIndex(row => string.Equals(row.Key, key, StringComparison.Ordinal));

        return index >= 0 && index < RowHost.Children.Count && RowHost.Children[index] is DockPanel host
            ? host.Children.OfType<Button>().FirstOrDefault()
            : null;
    }

    /// <summary>
    /// 프로젝트를 읽어 줄을 다시 세운다 — 보이는 줄이 그대로면 칠만 한다(챕터를 고르면 판 전환이 이 자리를 부르는데, 그때
    /// 누른 줄이 사라지면 더블클릭이 끊긴다). 고른 것이 사라졌으면 선택도 놓는다(없는 것을 고른 채로 두면 인스펙터가 빈
    /// 것인지 고장인지 못 가린다).
    /// </summary>
    /// <param name="projectTitle">프로젝트 이름. null이면 프로젝트가 없다(빈 하이어아키).</param>
    /// <param name="chapters">챕터들 — 챕터 그래프가 읽은 그 목록 하나(두 곳이 따로 읽지 않는다).</param>
    /// <param name="saved">프로젝트가 저장돼 있는가 — 아니면(처음 켰을 때) 챕터를 만들 수 없어 빈 목록의 안내가 다르다(usability UX35).</param>
    internal void Rebuild(string? projectTitle, IReadOnlyList<ChapterEntry> chapters, bool saved = true)
    {
        _hasProject = projectTitle is not null;
        _saved = saved;
        _projectTitle = projectTitle ?? string.Empty;
        _chapters = chapters;

        if (Selection is { } pick && !Exists(pick))
        {
            PickOnly(null);
        }
        else if (_picks.RemoveAll(other => !Exists(other)) > 0)
        {
            SelectionsChanged?.Invoke();   // 함께 고른 줄 가운데 사라진 것만 놓는다
        }

        Refresh();
    }

    /// <summary>
    /// 밖에서 고른 것을 비춘다(그래프에서 카드를 골랐을 때 등) — 알리지 않는다. 가는 길의 마디를 편다(사람이 접어 둔
    /// 것이어도 — 고른 것이 안 보이면 하이어아키가 거짓말을 한다).
    /// </summary>
    internal void Show(HierarchyPick? pick)
    {
        if (Equals(Selection, pick))
        {
            return;
        }

        // 사람이 누른 것을 셸이 처리하는 동안(Picked) 잠깐 비우는 것(챕터 줄 — 카드 선택을 놓는다)은 받지 않는다 — 받으면 함께 고른 줄들이 풀린다.
        if (pick is null && _pressing)
        {
            return;
        }

        // 마지막에 누른 그 줄이면(셸이 같은 것을 다시 비춘다 — 열쇠는 같고 장면 칸만 다를 수 있다) 함께 고른 줄들을 지킨다. 다른 것이면 한 줄이다.
        if (pick is not null && Selection is not null && string.Equals(pick.Key, Selection.Key, StringComparison.Ordinal) && _picks.Count > 0)
        {
            _picks[^1] = pick;
            Selection = pick;
        }
        else
        {
            PickOnly(pick);
        }

        // 드러낸 길은 편 채로 남긴다 — 고른 것을 따라 저절로 펴진 마디가 고르기를 부모로 옮기는 순간 다시 접히면, 사람
        // 눈에는 줄이 제멋대로 사라진다(유니티도 드러낸 길을 편 채로 둔다). 기억(Save)은 사람이 접고 펼 때만 한다.
        foreach (string key in PathOf(pick))
        {
            Folds.Collapsed.Remove(key);
            Folds.Expanded.Add(key);
        }

        Refresh();
    }

    // ── 줄 세우기 ───────────────────────────────────────────────────────────

    /// <summary>보일 줄들 — 눈 기둥이 있으면 줄마다 눈을 물어 담는다(눈이 바뀌어도 줄이 다시 선다).</summary>
    private List<HierarchyRow> BuildRows()
    {
        List<HierarchyRow> rows = Filtered(BuildTree());

        if (EyeOf is { } eyeOf)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                rows[index] = rows[index] with { Eye = eyeOf(rows[index]) };
            }
        }

        return rows;
    }

    private List<HierarchyRow> BuildTree()
    {
        var rows = new List<HierarchyRow>();

        if (!_hasProject)
        {
            return rows;
        }

        rows.Add(new HierarchyRow(
            HierarchyKind.Project, ProjectKey, null, null, null,
            string.IsNullOrWhiteSpace(_projectTitle) ? Ui.Side_Project : _projectTitle,
            Detail: Ui.Board_Speakers, Depth: 0, IsSceneRoot: false, HasWarning: false));

        if (!IsExpanded(ProjectKey))
        {
            return rows;
        }

        foreach (ChapterEntry chapter in _chapters)
        {
            AddChapter(rows, chapter);
        }

        return rows;
    }

    private void AddChapter(List<HierarchyRow> rows, ChapterEntry chapter)
    {
        string chapterId = chapter.ChapterId;
        string key = ChapterKey(chapterId);
        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(chapter.Model);

        rows.Add(new HierarchyRow(
            // [대본] 탭은 이름(표시 이름, 비면 ID), [구성] 탭은 ID에 곁글로 이름이다(에피소드처럼 — 2026-11-18 소유자). ⛔ [구성] 곁글 「챕터 조건
            // 스탯」 · 「에피소드 N」은 같은 날 걷혔다.
            HierarchyKind.Chapter, key, chapterId, null, null, ShowEpisodeTitles ? chapter.Model.ShownName : chapterId,
            Detail: ShowEpisodeTitles ? null : NameBeside(chapterId, chapter.Model.DisplayName),
            Depth: 1, IsSceneRoot: false, HasWarning: chapter.HasErrors,
            Find: Findable(chapterId, chapter.Model.DisplayName, chapter.Model.ShownName)));

        if (!IsExpanded(key))
        {
            return;
        }

        // 에피소드는 늘 장면에 속한다 — 장면 줄 밑에 선다(⛔ 장면ID가 없는 에피소드를 세우던 「챕터 직속」 줄은 2026-11-18에 걷혔다).
        foreach (ChapterScene scene in scenes)
        {
            string sceneKey = SceneKey(chapterId, scene.SceneId);

            rows.Add(new HierarchyRow(
                HierarchyKind.Scene, sceneKey, chapterId, scene.SceneId, null, ShowEpisodeTitles ? scene.DisplayName : scene.SceneId,
                // [구성] 탭은 ID에 곁글로 이름(에피소드처럼 — 2026-11-18 소유자). 장면 메모나 에피소드 지시가 있으면 글자로 알린다(색만으로 알리지
                // 않는다 — §6-3).
                Detail: string.Join(" · ", new[]
                {
                    ShowEpisodeTitles ? null : NameBeside(scene.SceneId, scene.DisplayName),
                    ChapterSceneNotes.HasMemo(chapter.Model.SceneNotes, scene) ? Ui.Hierarchy_Note : null
                }.OfType<string>()) is { Length: > 0 } sceneDetail ? sceneDetail : null,
                Depth: 2, IsSceneRoot: false, HasWarning: scene.HasSplitEntry,
                Find: Findable(scene.SceneId, scene.DisplayName)));

            if (!IsExpanded(sceneKey))
            {
                continue;
            }

            foreach (ChapterEpisode episode in scene.Episodes)
            {
                rows.Add(EpisodeRow(chapterId, scene, episode, depth: 3));
                AddBranches(rows, chapter, scene, episode, depth: 4);
            }
        }
    }

    /// <summary>
    /// 그 에피소드가 부르는 분기 카드들 — 에피소드 밑에 「⑂ 이름」 줄로, 대본 차례로(2026-11-17 소유자 — 두 탭 같다). 에피소드 줄은 접지
    /// 않으므로 늘 보인다. 분기 카드가 부르는 분기 카드도 같은 집이라 같은 자리에 선다. 이름은 에피소드 줄과 같은 규칙이다 — [대본] 탭은 제목,
    /// [구성] 탭(「ID로 보기」)은 분기 카드 ID(= Yarn 노드 이름 · <c>DialogueNode.Name</c>)에 곁글로 제목(2026-11-23 소유자 — 「구성탭에서 분기 노드의
    /// 경우 하이어아키에서 ID로 보기 체크여부랑 상관없이 항상 DisplayName으로만 표기가 되네요」 · ⛔ 그 전에는 두 탭 모두 늘 제목이었다).
    /// </summary>
    private void AddBranches(List<HierarchyRow> rows, ChapterEntry chapter, ChapterScene scene, ChapterEpisode episode, int depth)
    {
        foreach (BranchCardHome home in chapter.BranchCards.Where(home =>
                     string.Equals(home.EpisodeId, episode.EpisodeId, StringComparison.Ordinal)))
        {
            string title = EpisodeNaming.BranchTitleOf(home.Card);

            rows.Add(new HierarchyRow(
                HierarchyKind.Branch,
                BranchKey(chapter.ChapterId, home.Card.Id),
                chapter.ChapterId,
                scene.SceneId,
                episode.EpisodeId,
                "⑂ " + (ShowEpisodeTitles ? title : home.Card.Name),
                Detail: ShowEpisodeTitles ? null : NameBeside(home.Card.Name, home.Card.Title),
                depth,
                IsSceneRoot: false,
                HasWarning: false,
                NodeId: home.Card.Id,
                Find: Findable(home.Card.Name, title, home.Card.Id)));
        }
    }

    /// <summary>찾기가 견줄 글들 — 빈 것은 뺀다(<see cref="HierarchyRow.Find"/>).</summary>
    private static string Findable(params string?[] texts) =>
        string.Join('\n', texts.Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text!.Trim()).Distinct(StringComparer.Ordinal));

    /// <summary>ID로 선 줄의 곁글 이름 — 비었거나 ID와 같으면 없다(에피소드 제목과 같은 규칙).</summary>
    private static string? NameBeside(string id, string? name) =>
        string.IsNullOrWhiteSpace(name) || string.Equals(name.Trim(), id, StringComparison.Ordinal) ? null : name.Trim();

    private HierarchyRow EpisodeRow(string chapterId, ChapterScene scene, ChapterEpisode episode, int depth) =>
        new(
            HierarchyKind.Episode,
            EpisodeKey(chapterId, episode.EpisodeId),
            chapterId,
            scene.SceneId,
            episode.EpisodeId,
            ShowEpisodeTitles ? episode.ShownTitle : episode.EpisodeId,
            Detail: ShowEpisodeTitles ? null : NameBeside(episode.EpisodeId, episode.Title),
            depth,
            IsSceneRoot: string.Equals(scene.RootEpisodeId, episode.EpisodeId, StringComparison.Ordinal),
            HasWarning: false,
            Find: Findable(episode.EpisodeId, episode.Title, episode.ShownTitle));

    /// <summary>
    /// 이 마디가 펴져 있는가 — 사람이 접지 않았고, 사람이 폈거나 <b>고른 것이 그 안에 있을 때</b>다(처음 열면 고른 것이
    /// 든 챕터 · 장면만 펴진다 — 챕터 수십 개가 다 펴져 있으면 하이어아키가 아니라 목록이다). 프로젝트 줄은 처음부터 펴져
    /// 있다. 고른 마디 <b>자신</b>은 펴지 않는다 — 챕터를 골랐다고 그 챕터가 펼쳐지면 고르는 손짓이 줄을 다시 세운다.
    /// </summary>
    private bool IsExpanded(string key) =>
        Searching ||   // 찾는 동안은 접힘을 보지 않는다 — 접어 둔 챕터 · 장면 안도 찾는다
        !Folds.Collapsed.Contains(key) &&
        (key == ProjectKey || Folds.Expanded.Contains(key) || PathOf(Selection).Contains(key));

    /// <summary>고른 것까지 가는 길의 마디들(고른 것 자신은 빼고).</summary>
    private List<string> PathOf(HierarchyPick? pick)
    {
        var path = new List<string>();

        if (pick is null || pick.Kind == HierarchyKind.Project)
        {
            return path;
        }

        path.Add(ProjectKey);

        if (pick.Kind == HierarchyKind.Chapter || pick.ChapterId is not { } chapterId)
        {
            return path;
        }

        path.Add(ChapterKey(chapterId));

        // 에피소드는 장면 마디 밑에 선다. 분기 카드 줄은 그 집(부르는 에피소드)의 길이다.
        if (pick.Kind is HierarchyKind.Episode or HierarchyKind.Branch && pick.EpisodeId is { } episodeId &&
            _chapters.FirstOrDefault(chapter => string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal))
                ?.Model.FindEpisode(episodeId) is { } episode)
        {
            path.Add(SceneKey(chapterId, episode.SceneId));
        }

        return path;
    }

    private bool Exists(HierarchyPick pick)
    {
        if (pick.Kind == HierarchyKind.Project)
        {
            return _hasProject;
        }

        ChapterEntry? chapter = _chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, pick.ChapterId, StringComparison.Ordinal));

        return pick.Kind switch
        {
            HierarchyKind.Chapter => chapter is not null,
            HierarchyKind.Scene => chapter?.Model.Episodes.Any(episode =>
                string.Equals(episode.SceneId, pick.SceneId, StringComparison.Ordinal)) == true,
            HierarchyKind.Branch => chapter?.BranchCards.Any(home =>
                string.Equals(home.Card.Id, pick.NodeId, StringComparison.Ordinal)) == true,
            _ => chapter?.Model.FindEpisode(pick.EpisodeId ?? string.Empty) is not null
        };
    }

    /// <summary>보이는 줄이 그대로면(이름 · 표식까지) 칠만 하고, 바뀌었으면 다시 세운다.</summary>
    private void Refresh()
    {
        List<HierarchyRow> rows = BuildRows();

        if (rows.Count > 0 && rows.SequenceEqual(_rows) && RowHost.Children.Count == _rows.Count && _renamingKey is null)
        {
            Paint();
            return;
        }

        Draw(rows);
    }

    private void Draw(List<HierarchyRow>? rows = null)
    {
        _drawing = true;

        try
        {
            _rows.Clear();
            _rows.AddRange(rows ?? BuildRows());
            RowHost.Children.Clear();   // ⚠ 초점을 쥔 이름 칸이 여기서 빠지면 그 자리에서 「없던 일로」가 불린다(_drawing)

            RenameBox = null;

            foreach (HierarchyRow row in _rows)
            {
                RowHost.Children.Add(string.Equals(_renamingKey, row.Key, StringComparison.Ordinal) ? RenameRow(row) : Build(row));
            }
        }
        finally
        {
            _drawing = false;
        }

        RenameBox?.Focus();
        RenameBox?.SelectAll();

        if (_hasProject && _chapters.Count > 0 && Searching && SearchHits == 0)
        {
            RowHost.Children.Add(new TextBlock
            {
                Text = string.Format(Ui.Hierarchy_NothingMatches, _query),
                Margin = new Thickness(26, 6),
                Opacity = Skin.Dim(0.55),
                FontSize = Skin.Font(11),
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!_hasProject)
        {
            RowHost.Children.Add(new TextBlock
            {
                Text = Ui.Hierarchy_NoProject,
                Margin = new Thickness(8, 10),
                Opacity = Skin.Dim(0.55),
                FontSize = Skin.Font(11)
            });
        }
        else if (_chapters.Count == 0)
        {
            RowHost.Children.Add(new TextBlock
            {
                Text = !_saved
                    ? ReadOnly
                        ? Ui.Hierarchy_OpenOrCreateProjectAnd
                        : Ui.Hierarchy_OpenOrCreateProjectAnd2
                    : ReadOnly
                        ? Ui.Hierarchy_NoChaptersCreateOneIn
                        : Ui.Hierarchy_NoChaptersCreateOneWith,
                Margin = new Thickness(26, 6),
                Opacity = Skin.Dim(0.55),
                FontSize = Skin.Font(11),
                TextWrapping = TextWrapping.Wrap
            });
        }

        Paint();
    }

    // ── 그리기 ──────────────────────────────────────────────────────────────

    /// <summary>줄 하나 = [삼각형][이름 단추]. 접기는 삼각형만 한다.</summary>
    private Control Build(HierarchyRow row)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        // 종류가 한눈에 갈려야 한다 — 들여쓰기만으로는 같은 글자로 보인다. 크기와 무게를 함께 준다.
        (double size, FontWeight weight, double dim) = row.Kind switch
        {
            HierarchyKind.Project => (13.0, FontWeight.Bold, 1.0),
            HierarchyKind.Chapter => (13.0, FontWeight.SemiBold, 1.0),
            HierarchyKind.Scene => (12.0, FontWeight.SemiBold, 0.8),
            HierarchyKind.Branch => (11.0, FontWeight.Normal, 0.75),
            _ => (12.0, FontWeight.Normal, 1.0)
        };

        line.Children.Add(new TextBlock
        {
            Text = row.Text,
            FontSize = size,
            FontWeight = weight,
            Opacity = dim,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (row.Detail is { } detail)
        {
            line.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.5),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (row.HasWarning)
        {
            line.Children.Add(new TextBlock
            {
                Text = "⚠",
                FontSize = Skin.Font(10),
                Foreground = Skin.WarningMark.Brush(),
                VerticalAlignment = VerticalAlignment.Center,
                [ToolTip.TipProperty] = row.Kind switch
                {
                    HierarchyKind.Scene => Ui.Hierarchy_ThereAreTwoEntrancesFrom,
                    _ => Ui.Hierarchy_ThisChapterHasErrorsSee
                }
            });
        }

        var button = new Button
        {
            Content = line,
            Padding = new Thickness(4, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = false,
            Tag = row.Key
        };

        // 누를 때의 보조 키(Ctrl · ⌘ · Shift)를 적어 둔다 — 여러 줄 고르기(Press).
        button.AddHandler(PointerPressedEvent, (_, args) => _pressModifiers = args.KeyModifiers,
            Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        button.Click += (_, _) => UiGuard.Run(null, Ui.Keys_Hierarchy, () =>
        {
            Avalonia.Input.KeyModifiers modifiers = _pressModifiers;
            _pressModifiers = Avalonia.Input.KeyModifiers.None;
            Press(row, modifiers);
        });

        // 분기 카드 줄은 고르기만 한다 — 만들고 지우고 이름을 고치는 것은 [대본] 판의 일이다.
        if (!ReadOnly && row.Kind != HierarchyKind.Branch)
        {
            button.ContextMenu = Menu(row, button);
            WireDrag(button, row);

            // 두 번 눌러 이름 고치기 — 고르기가 줄을 다시 세우지 않으므로(칠만 한다) 둘째 누름이 같은 단추에 떨어진다.
            button.DoubleTapped += (_, args) => UiGuard.Run(null, Ui.Hierarchy_Rename, () =>
            {
                args.Handled = true;
                BeginRename(row.Key);
            });
        }

        var host = new DockPanel();

        // 눈 기둥은 들여쓰기 <b>바깥</b> 맨 왼쪽이다 — 유니티처럼 깊이와 상관없이 한 줄로 선다.
        if (EyeOf is not null)
        {
            Control eye = Eye(row);
            DockPanel.SetDock(eye, Dock.Left);
            host.Children.Add(eye);
        }

        var indent = new Border { Width = row.Depth * 14 };
        DockPanel.SetDock(indent, Dock.Left);
        host.Children.Add(indent);

        Control arrow = Arrow(row);
        DockPanel.SetDock(arrow, Dock.Left);
        host.Children.Add(arrow);
        host.Children.Add(button);

        return host;
    }

    // ── 눈 (유니티 하이어아키의 보이기 기둥) ──────────────────────────────

    /// <summary>그 줄의 눈 — 화면 시험이 누를 자리. 눈이 없는 줄이나 눈 기둥이 없으면 null.</summary>
    internal Border? EyeButton(string key)
    {
        int index = _rows.FindIndex(row => string.Equals(row.Key, key, StringComparison.Ordinal));

        return index >= 0 && index < RowHost.Children.Count && _rows[index].Eye != HierarchyEye.None &&
               RowHost.Children[index] is DockPanel host
            ? host.Children.OfType<Border>().FirstOrDefault(border => border.Tag as string == "eye:" + key)
            : null;
    }

    /// <summary>
    /// 눈 칸 — 누르면 접고 편다. 펼친 줄의 눈은 옅게(유니티처럼 보이는 것은 조용하다), 접은 줄의 감은 눈은 또렷하게, 챕터가
    /// 접혀 가려진 장면은 흐리게 선다. 눈이 없는 줄도 같은 폭을 비운다(줄이 들쭉날쭉하지 않게).
    /// </summary>
    private Border Eye(HierarchyRow row)
    {
        var cell = new Border
        {
            Width = 20,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch,
            Tag = "eye:" + row.Key
        };

        if (row.Eye == HierarchyEye.None)
        {
            return cell;
        }

        cell.Child = EyeGlyph(row.Eye);
        cell.Cursor = new Cursor(StandardCursorType.Hand);
        cell[ToolTip.TipProperty] = (row.Kind, row.Eye) switch
        {
            (HierarchyKind.Chapter, HierarchyEye.Open) => Ui.Hierarchy_FoldThisChapterOnBoard,
            (HierarchyKind.Chapter, _) => Ui.Hierarchy_UnfoldThisChapterOnBoard,
            (_, HierarchyEye.Open) => Ui.Hierarchy_FoldJustThisSceneOn,
            (_, HierarchyEye.Folded) => Ui.Hierarchy_UnfoldThisSceneOnBoard,
            _ => Ui.Hierarchy_ChapterIsFoldedClickTo
        };

        cell.PointerPressed += (_, args) => UiGuard.Run(null, Ui.Side_Show, () =>
        {
            args.Handled = true;
            EyeToggled?.Invoke(row);
        });

        return cell;
    }

    /// <summary>눈 모양 — 뜬 눈(테두리 + 눈동자) 또는 감은 눈(아래로 굽은 눈꺼풀 + 속눈썹).</summary>
    private static Control EyeGlyph(HierarchyEye eye)
    {
        IBrush ink = Skin.NeutralDark.Brush();
        var glyph = new Canvas
        {
            Width = 14,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = eye switch
            {
                HierarchyEye.Open => 0.45,
                HierarchyEye.Folded => 0.95,
                _ => 0.25
            }
        };

        if (eye == HierarchyEye.Folded)
        {
            glyph.Children.Add(new ShapePath
            {
                Data = StreamGeometry.Parse("M1,3 C3.5,7.5 10.5,7.5 13,3 M3.5,6 L2.5,8.5 M7,7 L7,9.5 M10.5,6 L11.5,8.5"),
                Stroke = ink,
                StrokeThickness = 1.3
            });

            return glyph;
        }

        glyph.Children.Add(new ShapePath
        {
            Data = StreamGeometry.Parse("M1,5 C3.5,0.5 10.5,0.5 13,5 C10.5,9.5 3.5,9.5 1,5 Z"),
            Stroke = ink,
            StrokeThickness = 1.2
        });

        var pupil = new Ellipse { Width = 4, Height = 4, Fill = ink };
        Canvas.SetLeft(pupil, 5);
        Canvas.SetTop(pupil, 3);
        glyph.Children.Add(pupil);

        return glyph;
    }

    // ── 우클릭 차림표 (줄마다 제 일만 — [구성] 탭의 하이어아키에는 없다) ──────────

    /// <summary>
    /// 줄의 차림표 — 줄마다 제 일만 선다(2026-11-18 소유자): 프로젝트 줄 = 챕터 추가, 챕터 줄 = 장면 추가 · 이름 바꾸기, 장면 줄 = 에피소드 추가 ·
    /// 이름 바꾸기, 에피소드 줄 = 이름 바꾸기 · 에피소드 지우기. 챕터 · 장면은 줄을 고르고 [대본] 툴바의 [삭제]로 지운다(⛔ 차림표의 「챕터 지우기」 ·
    /// 「장면 지우기」는 같은 날 걷혔다). ⛔ 그 전에는 「챕터 추가 · 장면 추가」가 어느 줄에서나 같은 자리에 섰다.
    /// </summary>
    private ContextMenu Menu(HierarchyRow row, Control anchor)
    {
        List<Control> items = row.Kind switch
        {
            HierarchyKind.Project => [MenuItemFor(Ui.Board_AddChapter, HierarchyCommand.AddChapter, row, anchor)],
            HierarchyKind.Chapter =>
            [
                MenuItemFor(Ui.Board_AddScene, HierarchyCommand.AddScene, row, anchor),
                new Separator(),
                RenameItem(row)
            ],

            // 에피소드는 늘 장면에 속한다 — 장면 줄에서만 더한다(그 장면에 길 없이 선다).
            HierarchyKind.Scene =>
            [
                MenuItemFor(Ui.Board_AddEpisode, HierarchyCommand.AddEpisode, row, anchor),
                new Separator(),
                RenameItem(row)
            ],
            HierarchyKind.Episode =>
            [
                RenameItem(row),
                LastEpisodeKept(MenuItemFor(Ui.Board_DeleteEpisode2, HierarchyCommand.DeleteEpisode, row, anchor), row)
            ],
            _ => []
        };

        return new ContextMenu { ItemsSource = items };
    }

    private MenuItem MenuItemFor(string header, HierarchyCommand command, HierarchyRow? row, Control anchor)
    {
        var item = new MenuItem { Header = header, FontSize = Skin.Font(11), Tag = command };
        item.Click += (_, _) => UiGuard.Run(null, Ui.Side_HierarchyMenu, () =>
            CommandRequested?.Invoke(command, row ?? ProjectRow(), anchor));

        return item;
    }

    /// <summary>챕터의 마지막 에피소드면 「에피소드 지우기」를 끈다 — 챕터에는 에피소드 하나가 늘 남는다(2026-11-18 · <see cref="ChapterMinimum"/>).</summary>
    private MenuItem LastEpisodeKept(MenuItem item, HierarchyRow row)
    {
        if (_chapters.FirstOrDefault(chapter => string.Equals(chapter.ChapterId, row.ChapterId, StringComparison.Ordinal)) is { } entry &&
            ChapterMinimum.IsLastEpisode(entry.Model.Episodes, row.EpisodeId!))
        {
            item.IsEnabled = false;
            ToolTip.SetTip(item, ChapterMinimum.LastEpisodeReason);
            ToolTip.SetShowOnDisabled(item, true);
        }

        return item;
    }

    private MenuItem RenameItem(HierarchyRow row)
    {
        var item = new MenuItem { Header = Ui.Board_Rename, FontSize = Skin.Font(11), Tag = "rename" };
        item.Click += (_, _) => UiGuard.Run(null, Ui.Hierarchy_Rename, () => BeginRename(row.Key));

        return item;
    }

    private HierarchyRow ProjectRow() =>
        _rows.FirstOrDefault(row => row.Kind == HierarchyKind.Project)
        ?? new HierarchyRow(HierarchyKind.Project, ProjectKey, null, null, null, _projectTitle, null, 0, false, false);

    // ── 이름 고치기 ─────────────────────────────────────────────────────────

    /// <summary>그 줄이 지고 있는 이름 — 화면 글월이 아니라 Id다.</summary>
    internal static string NameOf(HierarchyRow row) => row.Kind switch
    {
        HierarchyKind.Chapter => row.ChapterId ?? string.Empty,
        HierarchyKind.Scene => row.SceneId ?? string.Empty,
        HierarchyKind.Episode => row.EpisodeId ?? string.Empty,
        _ => row.Text
    };

    /// <summary>이름 칸에 처음 적힐 이름 — 제목으로 세운 에피소드 줄이면 그 제목, 아니면 <see cref="NameOf"/>.</summary>
    private string CurrentNameOf(HierarchyRow row) => TitleRow(row) ? row.Text : NameOf(row);

    /// <summary>
    /// 이름으로 세운 줄인가 — [대본] 탭의 챕터 · 장면 · 에피소드 줄. 그 줄의 이름 고치기는 보이는 이름(표시 이름 · 장면 이름 · 제목)만
    /// 고치고 ID는 그대로다. 비울 수 있다(ID로 물러선다).
    /// </summary>
    private bool TitleRow(HierarchyRow row) =>
        ShowEpisodeTitles && row.Kind is HierarchyKind.Episode or HierarchyKind.Scene or HierarchyKind.Chapter;

    /// <summary>그 줄의 이름 칸을 연다(프로젝트 줄은 이름을 여기서 고치지 않는다).</summary>
    internal void BeginRename(string key)
    {
        if (ReadOnly ||
            _rows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.Ordinal)) is not { } row ||
            row.Kind is HierarchyKind.Project or HierarchyKind.Branch)
        {
            return;
        }

        // 이미 그 줄의 이름 칸이 열려 있으면 그대로 둔다 — 다시 세우면 그 칸이 판에서 빠지며 「없던 일로」가 되어 닫힌다.
        if (string.Equals(_renamingKey, key, StringComparison.Ordinal) && RenameBox is not null)
        {
            RenameBox.Focus();
            return;
        }

        _renamingKey = key;
        Draw();
    }

    /// <summary>이름 칸 — Enter는 넣고, Esc나 딴 데를 누르면 없던 일로(고치다 만 이름이 남으면 무엇이 진짜인지 흐려진다).</summary>
    private TextBox RenameRow(HierarchyRow row)
    {
        string current = CurrentNameOf(row);
        var box = new TextBox
        {
            Text = current,
            FontSize = Skin.Font(11),
            Padding = new Thickness(4, 1),
            Margin = new Thickness((row.Depth * 14) + 16, 0, 0, 0),
            Tag = "rename:" + row.Key
        };

        void Commit(bool keep)
        {
            _renamingKey = null;
            RenameBox = null;
            string wanted = box.Text?.Trim() ?? string.Empty;

            // 줄을 세우는 도중이면(그 세우기가 이 칸을 판에서 뺐다) 다시 세우지 않는다 — 진행 중인 세우기가 이름 칸 없이 마저 세운다.
            if (!_drawing)
            {
                Draw();
            }

            // 이름은 비울 수 있다(ID로 물러선다). [구성] 탭의 장면 줄도 비울 수 있었다(장면이 풀린다) — 지금은 읽기 전용이다.
            if (keep && !string.Equals(wanted, current, StringComparison.Ordinal) &&
                (wanted.Length > 0 || row.Kind == HierarchyKind.Scene || TitleRow(row)))
            {
                RenameRequested?.Invoke(row, wanted);
            }
        }

        box.KeyDown += (_, args) => UiGuard.Run(null, Ui.Hierarchy_Rename, () =>
        {
            if (args.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Escape)
            {
                args.Handled = true;
                Commit(args.Key == Avalonia.Input.Key.Enter);
            }
        });

        box.LostFocus += (_, _) => UiGuard.Run(null, Ui.Hierarchy_Rename, () =>
        {
            if (string.Equals(_renamingKey, row.Key, StringComparison.Ordinal))
            {
                Commit(keep: false);
            }
        });

        RenameBox = box;
        return box;
    }

    /// <summary>
    /// 접기 손잡이. 에피소드는 펼 자식이 없어 그 자리에 표식(장면 루트 ⌂ · 그 밖 ·)을 세운다 — 그래서 <b>같은 깊이의 이름이 한 줄로
    /// 선다</b>. 빈 손잡이 칸 옆에 표식을 따로 붙이면 에피소드 이름이 한 칸 더 들어간다.
    /// </summary>
    private Border Arrow(HierarchyRow row)
    {
        bool leaf = row.Kind is HierarchyKind.Episode or HierarchyKind.Branch;
        var arrow = new Border
        {
            Width = 16,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch,
            Tag = "fold:" + row.Key,
            Child = new TextBlock
            {
                Text = leaf ? row.IsSceneRoot ? "⌂" : "·" : IsExpanded(row.Key) ? "▼" : "▶",
                FontSize = leaf && row.IsSceneRoot ? 12 : 10,
                Opacity = leaf ? row.IsSceneRoot ? 0.8 : 0.35 : 0.75,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        if (leaf)
        {
            arrow[ToolTip.TipProperty] = row.IsSceneRoot
                ? Ui.Hierarchy_SceneStartWhereRollbackReturns
                : null;

            return arrow;
        }

        arrow[ToolTip.TipProperty] = Searching ? Ui.Hierarchy_WhileSearchingEverythingIsExpanded : Ui.Hierarchy_CollapseExpand;
        arrow.PointerPressed += (_, args) => UiGuard.Run(null, Ui.Hierarchy_Collapse, () =>
        {
            args.Handled = true;

            if (!Searching)
            {
                Fold(row.Key, collapse: IsExpanded(row.Key));
            }
        });

        return arrow;
    }

    /// <summary>사람이 접거나 폈다 — 셸이 다른 탭의 하이어아키를 다시 그린다(접힘은 함께 쓴다).</summary>
    internal event Action? FoldChanged;

    /// <summary>마디를 접거나 편다 — 기억하고 알린다.</summary>
    internal void Fold(string key, bool collapse)
    {
        if (collapse)
        {
            Folds.Collapsed.Add(key);
            Folds.Expanded.Remove(key);
        }
        else
        {
            Folds.Collapsed.Remove(key);
            Folds.Expanded.Add(key);
        }

        Folds.Save();
        Draw();
        FoldChanged?.Invoke();
    }

    /// <summary>접힘이 다른 자리에서 바뀌었다 — 보이는 줄을 맞춘다.</summary>
    internal void RefreshFolds() => Refresh();

    /// <summary>
    /// 줄을 눌렀다 — 보조 키가 없으면 그 줄 하나, Ctrl(⌘)이면 그 줄을 더하거나 빼고, Shift면 기준 줄부터 그 줄까지(Ctrl+Shift는 그 범위를 더한다).
    /// 판 · 인스펙터는 마지막에 누른 줄을 따른다(<see cref="Picked"/>) — Ctrl로 뺀 줄이 마지막 줄이 아니었으면 알리지 않는다.
    /// </summary>
    private void Press(HierarchyRow row, Avalonia.Input.KeyModifiers modifiers = Avalonia.Input.KeyModifiers.None)
    {
        Focus();   // 키보드(↑↓←→)가 하이어아키로 온다

        bool toggle = modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control) || modifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta);
        int anchor = modifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift) && _anchorKey is { } anchorKey
            ? _rows.FindIndex(item => string.Equals(item.Key, anchorKey, StringComparison.Ordinal))
            : -1;
        HierarchyPick? before = Selection;

        if (anchor >= 0)
        {
            // 범위 — 보이는 줄 차례로 기준 줄부터 누른 줄까지. 누른 줄이 마지막(판이 따른다)이다. 기준 줄은 그대로다(Shift로 범위를 다시 잡는다).
            int at = _rows.IndexOf(row);
            IEnumerable<HierarchyPick> range = _rows.Skip(Math.Min(anchor, at)).Take(Math.Abs(at - anchor) + 1).Select(item => item.Pick);

            if (!toggle)
            {
                _picks.Clear();
            }

            foreach (HierarchyPick pick in range.Where(pick => !string.Equals(pick.Key, row.Key, StringComparison.Ordinal)))
            {
                _picks.RemoveAll(other => string.Equals(other.Key, pick.Key, StringComparison.Ordinal));
                _picks.Add(pick);
            }

            _picks.RemoveAll(other => string.Equals(other.Key, row.Key, StringComparison.Ordinal));
            _picks.Add(row.Pick);
            Selection = row.Pick;
        }
        else if (toggle && _picks.Exists(other => string.Equals(other.Key, row.Key, StringComparison.Ordinal)))
        {
            // 빼기 — 하나 남은 줄은 빼지 않는다(아무것도 안 고른 하이어아키는 인스펙터가 빈다). 마지막 줄을 뺐으면 그 앞의 줄이 마지막이 된다.
            if (_picks.Count > 1)
            {
                _picks.RemoveAll(other => string.Equals(other.Key, row.Key, StringComparison.Ordinal));
                Selection = _picks[^1];
            }

            _anchorKey = row.Key;
        }
        else if (toggle)
        {
            _picks.Add(row.Pick);
            Selection = row.Pick;
            _anchorKey = row.Key;
        }
        else
        {
            _picks.Clear();
            _picks.Add(row.Pick);
            Selection = row.Pick;
            _anchorKey = row.Key;
        }

        Paint();
        SelectionsChanged?.Invoke();

        if (!toggle || !string.Equals(before?.Key, Selection?.Key, StringComparison.Ordinal))
        {
            _pressing = true;

            try
            {
                Picked?.Invoke(Selection!);
            }
            finally
            {
                _pressing = false;
            }
        }
    }

    /// <summary>사람이 누른 것을 알리는 중인가(<see cref="Picked"/> 안) — 그사이 셸이 잠깐 비우는 것을 받지 않는다(<see cref="Show"/>).</summary>
    private bool _pressing;

    // ── 키보드 ───────────────────────────────────

    /// <summary>
    /// ↑↓ = 보이는 줄 사이로 고르기, ← = 접기 · 이미 접혔으면 부모로, → = 펴기 · 이미 폈으면 첫 자식으로. 유니티처럼 화살표가
    /// 곧 고르기다 — 인스펙터가 따라온다.
    /// </summary>
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 찾기 칸의 키는 그 칸의 것이다(Enter · ↓ · Esc는 SearchKey가 받는다).
        if (e.Handled || _renamingKey is not null || _rows.Count == 0 ||
            e.Source is Avalonia.Visual source && Avalonia.VisualTree.VisualExtensions.GetSelfAndVisualAncestors(source).Contains(SearchBox))
        {
            return;
        }

        int index = Selection is { } pick
            ? _rows.FindIndex(row => string.Equals(row.Key, pick.Key, StringComparison.Ordinal))
            : -1;
        HierarchyRow? current = index >= 0 ? _rows[index] : null;
        bool expandable = current is not null && current.Kind is not (HierarchyKind.Episode or HierarchyKind.Branch);

        HierarchyRow? next = e.Key switch
        {
            Avalonia.Input.Key.Down => _rows[Math.Min(_rows.Count - 1, index + 1)],
            Avalonia.Input.Key.Up => _rows[Math.Max(0, index - 1)],
            Avalonia.Input.Key.Left when expandable && !Searching && IsExpanded(current!.Key) => Folding(current, collapse: true),
            Avalonia.Input.Key.Left when current is not null => ParentOf(index),
            Avalonia.Input.Key.Right when expandable && !IsExpanded(current!.Key) => Folding(current, collapse: false),
            Avalonia.Input.Key.Right when current is not null && index + 1 < _rows.Count &&
                                          _rows[index + 1].Depth > current.Depth => _rows[index + 1],
            _ => null
        };

        if (e.Key is Avalonia.Input.Key.Up or Avalonia.Input.Key.Down or Avalonia.Input.Key.Left or Avalonia.Input.Key.Right)
        {
            e.Handled = true;
        }

        if (next is not null && !ReferenceEquals(next, current))
        {
            UiGuard.Run(null, Ui.Keys_Hierarchy, () =>
            {
                Press(next);
                RowButton(next.Key)?.BringIntoView();
            });
        }
    }

    /// <summary>접거나 펴고 그 줄에 머문다(고를 줄이 없다).</summary>
    private HierarchyRow? Folding(HierarchyRow row, bool collapse)
    {
        Fold(row.Key, collapse);
        return null;
    }

    /// <summary>그 줄의 부모 — 위로 올라가 처음 만나는 더 얕은 줄.</summary>
    private HierarchyRow? ParentOf(int index)
    {
        for (int above = index - 1; above >= 0; above--)
        {
            if (_rows[above].Depth < _rows[index].Depth)
            {
                return _rows[above];
            }
        }

        return null;
    }

    // ── 끌어 다른 장면으로 ───────────────────────────────────────────
    //
    // Avalonia의 DragDrop을 안 쓴다 — 창 밖으로 나가는 끌기가 아니라 한 컨트롤 안에서 줄을 옮기는 일이고, 누름 · 놓임만으로
    //    충분하다. 놓은 자리는 <b>좌표로</b> 찾는다 — 단추는 눌리는 순간 포인터를 잡아서 손을 어디서 떼든 놓임이 누른 단추로
    //    온다.

    /// <summary>끌어다 놓았다 — 에피소드를 다른 장면에. 셸이 그 에피소드의 장면ID를 바꾼다.</summary>
    internal event Action<HierarchyRow, HierarchyRow>? Dropped;

    /// <summary>
    /// 받을 자리(<see cref="Accepts"/>)인데도 옮기지 못하는 까닭 — 옮겨도 되면 null. 셸이 건다(<c>SceneMoveGuard</c> — 장면 입구가 둘이 되게 하는
    /// 선을 끊어도 입구가 둘인 이동: 챕터 시작을 시작점이 있는 장면으로 · 2026-11-26. ⛔ 2026-11-22(usability UX37)부터 그날까지는 입구가 둘이 되는
    /// 이동을 모두 거절했다). 없으면 받을 자리는 모두 받는다.
    /// </summary>
    internal Func<HierarchyRow, HierarchyRow, string?>? MoveRefusal { get; set; }

    /// <summary>못 옮기는 자리 위를 지나거나 거기 놓았다 — 까닭. 셸이 상태줄에도 말한다.</summary>
    internal event Action<string>? MoveRefused;

    /// <summary>
    /// 받는 자리에서 옮기면 함께 일어나는 일 — 장면 입구가 둘이 되지 않게 끊을 선(2026-11-26 · <c>SceneMoveGuard.CutNote</c>). 없으면 null. 셸이 건다.
    /// 끄는 동안 그 장면 줄 옆 풍선으로 말하고(<see cref="MoveNoted"/>로 상태줄에도), 놓으면 셸이 끊고 옮긴다.
    /// </summary>
    internal Func<HierarchyRow, HierarchyRow, string?>? MoveNote { get; set; }

    /// <summary>옮기면 선이 끊기는 자리 위를 지난다 — 그 말. 셸이 상태줄에도 말한다.</summary>
    internal event Action<string>? MoveNoted;

    /// <summary>테스트의 손잡이 — 지금 떠 있는 「못 옮기는 까닭」 풍선의 글. 없으면 null.</summary>
    internal string? RefusalTip => _refusalTipOn is null || !_tipRefuses ? null : _refusalTipText;

    /// <summary>테스트의 손잡이 — 지금 떠 있는 「옮기면 끊을 선」 풍선의 글. 없으면 null.</summary>
    internal string? NoteTip => _refusalTipOn is null || _tipRefuses ? null : _refusalTipText;

    /// <summary>풍선을 띄워 둔 줄 단추 — 못 옮기는 까닭이거나(<see cref="_tipRefuses"/>) 옮기면 끊을 선이다.</summary>
    private Button? _refusalTipOn;
    private string? _refusalTipText;
    private bool _tipRefuses;

    /// <summary>끌고 있는 줄.</summary>
    private string? _dragKey;

    // ── 끄는 잔상 (2026-11-22 소유자 — 「마야 · 유니티처럼 반투명한 잔상을 포인터에」 · usability UX38) ──
    //
    // 누른 채 조금(DragThreshold) 움직이면 그 줄의 이름을 든 반투명 딱지가 포인터를 따라다니고 끌던 줄은 흐려진다 — 「지금 끄는 중」이 보인다.
    // 딱지의 테두리가 놓을 자리를 말한다: 받는 장면 줄 위면 파랑, 못 받는 줄(장면 입구가 둘이 된다) 위면 빨강, 그 밖은 회색.
    // 창의 덧겹(OverlayLayer)에 서서 하이어아키 밖으로 나가도 잘리지 않고, 히트를 받지 않는다. 놓거나 포인터를 잃으면 걷는다.

    /// <summary>이만큼 움직여야 끌기다 — 그냥 누르기(고르기)에 잔상이 번쩍이지 않게.</summary>
    private const double DragThreshold = 4;

    private Point _dragStart;
    private Border? _dragGhost;
    private TextBlock? _dragGhostText;

    /// <summary>테스트의 손잡이 — 지금 포인터를 따라다니는 잔상의 글. 없으면 null.</summary>
    internal string? DragGhostText => _dragGhost is null ? null : _dragGhostText?.Text;

    /// <summary>테스트의 손잡이 — 잔상의 테두리 붓.</summary>
    internal IBrush? DragGhostBorder => _dragGhost?.BorderBrush;

    private static IBrush GhostIdle => Skin.NeutralLight.Brush();
    private static IBrush GhostAccept => Skin.Accent.Brush();
    private static IBrush GhostRefuse => Skin.Danger.Brush();

    /// <summary>끄는 동안 포인터를 따라간다 — 문턱을 넘으면 잔상을 세운다.</summary>
    private void FollowGhost(HierarchyRow row, Avalonia.Input.PointerEventArgs args)
    {
        if (_dragGhost is null)
        {
            Point now = args.GetPosition(this);

            if (Math.Abs(now.X - _dragStart.X) < DragThreshold && Math.Abs(now.Y - _dragStart.Y) < DragThreshold)
            {
                return;
            }

            if (Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(this) is not { } layer)
            {
                return;
            }

            _dragGhostText = new TextBlock { Text = row.Text, FontSize = Skin.Font(12), FontWeight = FontWeight.SemiBold };
            _dragGhost = new Border
            {
                Child = _dragGhostText,
                Padding = new Thickness(8, 3),
                CornerRadius = new CornerRadius(4),
                Background = Skin.Surface.Brush(235),
                BorderBrush = GhostIdle,
                BorderThickness = new Thickness(1.5),
                Opacity = Skin.Dim(0.8),
                IsHitTestVisible = false
            };
            layer.Children.Add(_dragGhost);
            Paint();   // 끌던 줄을 흐리게
        }

        if (_dragGhost.Parent is Visual host)
        {
            Point at = args.GetPosition(host);
            Canvas.SetLeft(_dragGhost, at.X + 12);
            Canvas.SetTop(_dragGhost, at.Y + 10);
        }
    }

    private void HideGhost()
    {
        if (_dragGhost is null)
        {
            return;
        }

        (_dragGhost.Parent as Panel)?.Children.Remove(_dragGhost);
        _dragGhost = null;
        _dragGhostText = null;
    }

    /// <summary>끌고 지나가는 중인 줄 — 받아 줄 자리면 테두리가 뜬다.</summary>
    private string? _hoverKey;

    /// <summary>
    /// 놓을 수 있는 자리인가 — 에피소드를 다른 장면 줄에(그 장면으로). 다른 챕터의 장면 줄도 받는다(2026-11-26 소유자 — 그 에피소드에 닿은 선은
    /// 모두 끊긴다 · <c>ChapterMoveGuard</c> · ⛔ 그 전에는 같은 챕터의 장면 줄만). ⛔ 챕터 줄에 놓아 장면에서 빼던 것은 걷었다(2026-11-17 —
    /// 에피소드는 늘 장면에 속한다).
    /// </summary>
    internal static bool Accepts(HierarchyRow source, HierarchyRow target) =>
        source.Kind == HierarchyKind.Episode &&
        target.Kind == HierarchyKind.Scene &&
        (!string.Equals(source.ChapterId, target.ChapterId, StringComparison.Ordinal) ||
         !string.Equals(source.SceneId, target.SceneId, StringComparison.Ordinal));

    /// <summary>화면 없이 끌기를 재는 자리 — 누름 · 놓임 두 번을 대신한다.</summary>
    internal void Drag(string sourceKey, string targetKey)
    {
        if (ReadOnly)
        {
            return;
        }

        _dragKey = sourceKey;
        Release(_rows.FirstOrDefault(row => string.Equals(row.Key, targetKey, StringComparison.Ordinal)));
    }

    private HierarchyRow? DragSource() =>
        _dragKey is null ? null : _rows.FirstOrDefault(row => string.Equals(row.Key, _dragKey, StringComparison.Ordinal));

    private void Release(HierarchyRow? target)
    {
        HierarchyRow? source = DragSource();
        _dragKey = null;
        _hoverKey = null;
        HideGhost();

        if (source is null || target is null || !Accepts(source, target))
        {
            Paint();
            return;
        }

        // 옮기면 장면 입구가 둘이 되는 자리 — 옮기지 않고 까닭을 말한다(끄는 동안 붉은 테두리와 풍선으로 미리 말한 그대로).
        if (MoveRefusal?.Invoke(source, target) is { } refusal)
        {
            Paint();
            MoveRefused?.Invoke(refusal);
            return;
        }

        Paint();   // 흐려 둔 끌던 줄을 되돌린다 — 옮긴 뒤 줄이 다시 서지 않는 경우에도
        Dropped?.Invoke(source, target);
    }

    /// <summary>그 자리에 있는 줄 — 좌표는 <see cref="RowHost"/> 기준이다.</summary>
    private HierarchyRow? RowAt(Point point)
    {
        for (int index = 0; index < RowHost.Children.Count && index < _rows.Count; index++)
        {
            if (RowHost.Children[index].Bounds.Contains(point))
            {
                return _rows[index];
            }
        }

        return null;
    }

    /// <summary>줄 단추에 끌기 손을 단다 — 누름 · 움직임 · 놓임(단추가 먼저 삼켜도 받는다).</summary>
    private void WireDrag(Button button, HierarchyRow row)
    {
        var both = Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble;

        button.AddHandler(PointerPressedEvent, (_, args) =>
        {
            _dragKey = row.Kind == HierarchyKind.Episode && args.GetCurrentPoint(button).Properties.IsLeftButtonPressed
                ? row.Key
                : null;
            _hoverKey = null;
            _dragStart = args.GetPosition(this);
            HideGhost();
        }, both, handledEventsToo: true);

        button.AddHandler(PointerMovedEvent, (_, args) => UiGuard.Run(null, Ui.Hierarchy_Move, () =>
        {
            if (_dragKey is null)
            {
                return;
            }

            FollowGhost(row, args);

            string? wanted = RowAt(args.GetPosition(RowHost))?.Key;

            if (!string.Equals(_hoverKey, wanted, StringComparison.Ordinal))
            {
                _hoverKey = wanted;
                Paint();
            }
        }), both, handledEventsToo: true);

        button.AddHandler(PointerReleasedEvent, (_, args) => UiGuard.Run(null, Ui.Hierarchy_Move, () =>
        {
            if (_dragKey is not null)
            {
                Release(RowAt(args.GetPosition(RowHost)) ?? row);
            }
        }), both, handledEventsToo: true);

        // 놓음을 못 받고 포인터를 잃었다(창을 떠났다) — 잔상이 화면에 남지 않게 걷고 끌기를 그만둔다.
        button.PointerCaptureLost += (_, _) => UiGuard.Run(null, Ui.Hierarchy_Move, () =>
        {
            if (_dragGhost is null)
            {
                return;
            }

            _dragKey = null;
            _hoverKey = null;
            HideGhost();
            Paint();
        });
    }

    /// <summary>고른 줄만 다시 칠한다 — 줄을 다시 세우지 않는다.</summary>
    private void Paint()
    {
        var selected = new HashSet<string>(_picks.Select(pick => pick.Key), StringComparer.Ordinal);

        if (Selection is { } primary)
        {
            selected.Add(primary.Key);
        }

        Button? refusedOn = null;
        string? refusedWhy = null;
        Button? notedOn = null;
        string? noted = null;
        bool accepting = false;

        for (int index = 0; index < _rows.Count && index < RowHost.Children.Count; index++)
        {
            if (RowHost.Children[index] is DockPanel host && host.Children.OfType<Button>().FirstOrDefault() is { } button)
            {
                button.Background = selected.Contains(_rows[index].Key)
                    ? Skin.RowSelected.Brush()
                    : Brushes.Transparent;

                // 끌고 지나가는 동안 받아 줄 자리에 테두리 — 아무 표시가 없으면 놓아도 되는지 모른 채 손을 놓는다.
                bool landing = string.Equals(_rows[index].Key, _hoverKey, StringComparison.Ordinal) &&
                               DragSource() is { } dragged && Accepts(dragged, _rows[index]);

                // 받을 자리지만 옮기면 장면 입구가 둘이 된다 — 붉은 테두리, 까닭은 풍선으로(아래).
                string? why = landing ? MoveRefusal?.Invoke(DragSource()!, _rows[index]) : null;

                if (why is not null)
                {
                    refusedOn = button;
                    refusedWhy = why;
                }
                else if (landing && MoveNote?.Invoke(DragSource()!, _rows[index]) is { } note)
                {
                    // 받는 자리지만 옮기면 선이 끊긴다 — 테두리는 파랗고, 끊을 선을 풍선으로 미리 말한다(2026-11-26).
                    notedOn = button;
                    noted = note;
                }

                accepting |= landing && why is null;

                // 끄는 중인 줄은 흐리게 — 잔상이 그 줄을 들고 나간 것처럼 보인다.
                host.Opacity = _dragGhost is not null && string.Equals(_rows[index].Key, _dragKey, StringComparison.Ordinal) ? 0.4 : 1;

                button.BorderThickness = new Thickness(landing ? 1 : 0);
                button.BorderBrush = !landing
                    ? Brushes.Transparent
                    : why is null
                        ? Skin.Accent.Brush(220)
                        : Skin.Danger.Brush();
            }
        }

        if (_dragGhost is not null)
        {
            _dragGhost.BorderBrush = refusedWhy is not null ? GhostRefuse : accepting ? GhostAccept : GhostIdle;
        }

        if (refusedWhy is not null)
        {
            ShowRefusalTip(refusedOn, refusedWhy, refuses: true);
        }
        else
        {
            ShowRefusalTip(notedOn, noted, refuses: false);
        }
    }

    /// <summary>
    /// 못 옮기는 까닭을 그 장면 줄 옆에 풍선으로 띄운다 — 붉은 테두리만으로는 왜인지 모른다(2026-11-22 소유자 — 「그냥 안 되게 하기보다 왜 안
    /// 되는지 설명을」). 자리를 떠나거나 놓으면 걷는다. 줄 단추에는 본디 풍선이 없다.
    /// </summary>
    /// <param name="refuses">참이면 못 옮기는 까닭(✕), 거짓이면 옮기면 함께 일어나는 일(끊을 선 · ↪).</param>
    private void ShowRefusalTip(Button? on, string? why, bool refuses)
    {
        if (ReferenceEquals(on, _refusalTipOn) && string.Equals(why, _refusalTipText, StringComparison.Ordinal) && refuses == _tipRefuses)
        {
            return;
        }

        if (_refusalTipOn is { } old)
        {
            ToolTip.SetIsOpen(old, false);
            ToolTip.SetTip(old, null);
        }

        _refusalTipOn = on;
        _refusalTipText = why;
        _tipRefuses = refuses;

        if (on is null || why is null)
        {
            return;
        }

        ToolTip.SetTip(on, new TextBlock { Text = (refuses ? "✕ " : "↪ ") + why, TextWrapping = TextWrapping.Wrap, MaxWidth = 380, FontSize = Skin.Font(12) });
        ToolTip.SetPlacement(on, PlacementMode.Right);
        ToolTip.SetIsOpen(on, true);

        if (refuses)
        {
            MoveRefused?.Invoke(why);
        }
        else
        {
            MoveNoted?.Invoke(why);
        }
    }
}
