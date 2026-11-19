using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Vn.App.Services;
using Vn.Authoring.Chapters;

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
/// 줄의 눈 — 판에서 보이는가 (<c>docs/plans/hierarchy.md</c> §3). 유니티 하이어아키의 보이기 기둥과 같은 자리다.
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
    string? NodeId = null)
{
    public HierarchyPick Pick => new(Kind, ChapterId, SceneId, EpisodeId, NodeId);
}

/// <summary>
/// <b>하이어아키</b> — 유니티 하이어아키처럼 챕터 아래 장면, 장면 아래 그 장면의 에피소드들을 모은다
/// (<c>docs/plans/hierarchy.md</c>). 프로젝트 → 챕터 → 장면 → 에피소드를 접었다 펴는
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
            ItemsSource = new[] { MenuItemFor("챕터 추가", HierarchyCommand.AddChapter, row: null, anchor: TreeScroll) }
        };
        TreeScroll.ContextMenu = _emptyAreaMenu;
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

    /// <summary>지금 고른 것. 없으면 null.</summary>
    internal HierarchyPick? Selection { get; private set; }

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
            Selection = null;
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

        Selection = pick;

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
        List<HierarchyRow> rows = BuildTree();

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
            string.IsNullOrWhiteSpace(_projectTitle) ? "프로젝트" : _projectTitle,
            Detail: "화자", Depth: 0, IsSceneRoot: false, HasWarning: false));

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
            Depth: 1, IsSceneRoot: false, HasWarning: chapter.HasErrors));

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
                    ChapterSceneNotes.HasMemo(chapter.Model.SceneNotes, scene) ? "메모" : null
                }.OfType<string>()) is { Length: > 0 } sceneDetail ? sceneDetail : null,
                Depth: 2, IsSceneRoot: false, HasWarning: scene.HasSplitEntry));

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
    /// 않으므로 늘 보인다. 분기 카드가 부르는 분기 카드도 같은 집이라 같은 자리에 선다.
    /// </summary>
    private static void AddBranches(List<HierarchyRow> rows, ChapterEntry chapter, ChapterScene scene, ChapterEpisode episode, int depth)
    {
        foreach (BranchCardHome home in chapter.BranchCards.Where(home =>
                     string.Equals(home.EpisodeId, episode.EpisodeId, StringComparison.Ordinal)))
        {
            rows.Add(new HierarchyRow(
                HierarchyKind.Branch,
                BranchKey(chapter.ChapterId, home.Card.Id),
                chapter.ChapterId,
                scene.SceneId,
                episode.EpisodeId,
                "⑂ " + EpisodeNaming.BranchTitleOf(home.Card),
                Detail: null,
                depth,
                IsSceneRoot: false,
                HasWarning: false,
                NodeId: home.Card.Id));
        }
    }

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
            HasWarning: false);

    /// <summary>
    /// 이 마디가 펴져 있는가 — 사람이 접지 않았고, 사람이 폈거나 <b>고른 것이 그 안에 있을 때</b>다(처음 열면 고른 것이
    /// 든 챕터 · 장면만 펴진다 — 챕터 수십 개가 다 펴져 있으면 하이어아키가 아니라 목록이다). 프로젝트 줄은 처음부터 펴져
    /// 있다. 고른 마디 <b>자신</b>은 펴지 않는다 — 챕터를 골랐다고 그 챕터가 펼쳐지면 고르는 손짓이 줄을 다시 세운다.
    /// </summary>
    private bool IsExpanded(string key) =>
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

        if (!_hasProject)
        {
            RowHost.Children.Add(new TextBlock
            {
                Text = "프로젝트가 없습니다.",
                Margin = new Thickness(8, 10),
                Opacity = 0.55,
                FontSize = 11
            });
        }
        else if (_chapters.Count == 0)
        {
            RowHost.Children.Add(new TextBlock
            {
                Text = !_saved
                    ? ReadOnly
                        ? "프로젝트를 열거나 만들면 여기에 챕터가 섭니다."
                        : "프로젝트를 열거나 만들면 여기에 챕터가 섭니다 — 판 가운데의 [열기…] · [새 프로젝트…]."
                    : ReadOnly
                        ? "챕터가 없습니다. [대본] 탭에서 만드세요."
                        : "챕터가 없습니다. 우클릭 「챕터 추가」나 판 가운데의 [＋ 첫 챕터 만들기]로 만드세요.",
                Margin = new Thickness(26, 6),
                Opacity = 0.55,
                FontSize = 11,
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
                FontSize = 10,
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (row.HasWarning)
        {
            line.Children.Add(new TextBlock
            {
                Text = "⚠",
                FontSize = 10,
                Foreground = Brushes.DarkOrange,
                VerticalAlignment = VerticalAlignment.Center,
                [ToolTip.TipProperty] = row.Kind switch
                {
                    HierarchyKind.Scene => "밖에서 들어오는 자리가 둘입니다 — 장면은 한 자리에서만 시작해야 합니다.",
                    _ => "이 챕터에 오류가 있습니다 — 아래 검증 보고를 보세요."
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

        button.Click += (_, _) => UiGuard.Run(null, "하이어아키", () => Press(row));

        // 분기 카드 줄은 고르기만 한다 — 만들고 지우고 이름을 고치는 것은 [대본] 판의 일이다.
        if (!ReadOnly && row.Kind != HierarchyKind.Branch)
        {
            button.ContextMenu = Menu(row, button);
            WireDrag(button, row);

            // 두 번 눌러 이름 고치기 — 고르기가 줄을 다시 세우지 않으므로(칠만 한다) 둘째 누름이 같은 단추에 떨어진다.
            button.DoubleTapped += (_, args) => UiGuard.Run(null, "이름 고치기", () =>
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
            (HierarchyKind.Chapter, HierarchyEye.Open) => "판에서 이 챕터를 접습니다 — 표 하나가 되고 간선은 그 줄에 붙습니다.",
            (HierarchyKind.Chapter, _) => "판에서 이 챕터를 펼칩니다.",
            (_, HierarchyEye.Open) => "판에서 이 장면만 접습니다 — 줄 목록으로 간소화되고 간선은 그 줄에 붙습니다.",
            (_, HierarchyEye.Folded) => "판에서 이 장면을 펼칩니다.",
            _ => "챕터가 접혀 있습니다 — 누르면 챕터를 펴고 이 장면을 보입니다."
        };

        cell.PointerPressed += (_, args) => UiGuard.Run(null, "보이기", () =>
        {
            args.Handled = true;
            EyeToggled?.Invoke(row);
        });

        return cell;
    }

    /// <summary>눈 모양 — 뜬 눈(테두리 + 눈동자) 또는 감은 눈(아래로 굽은 눈꺼풀 + 속눈썹).</summary>
    private static Control EyeGlyph(HierarchyEye eye)
    {
        IBrush ink = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));
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
            HierarchyKind.Project => [MenuItemFor("챕터 추가", HierarchyCommand.AddChapter, row, anchor)],
            HierarchyKind.Chapter =>
            [
                MenuItemFor("장면 추가", HierarchyCommand.AddScene, row, anchor),
                new Separator(),
                RenameItem(row)
            ],

            // 에피소드는 늘 장면에 속한다 — 장면 줄에서만 더한다(그 장면에 길 없이 선다).
            HierarchyKind.Scene =>
            [
                MenuItemFor("에피소드 추가", HierarchyCommand.AddEpisode, row, anchor),
                new Separator(),
                RenameItem(row)
            ],
            HierarchyKind.Episode =>
            [
                RenameItem(row),
                LastEpisodeKept(MenuItemFor("에피소드 지우기", HierarchyCommand.DeleteEpisode, row, anchor), row)
            ],
            _ => []
        };

        return new ContextMenu { ItemsSource = items };
    }

    private MenuItem MenuItemFor(string header, HierarchyCommand command, HierarchyRow? row, Control anchor)
    {
        var item = new MenuItem { Header = header, FontSize = 11, Tag = command };
        item.Click += (_, _) => UiGuard.Run(null, "하이어아키 차림표", () =>
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
        var item = new MenuItem { Header = "이름 바꾸기", FontSize = 11, Tag = "rename" };
        item.Click += (_, _) => UiGuard.Run(null, "이름 고치기", () => BeginRename(row.Key));

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
            FontSize = 11,
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

        box.KeyDown += (_, args) => UiGuard.Run(null, "이름 고치기", () =>
        {
            if (args.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Escape)
            {
                args.Handled = true;
                Commit(args.Key == Avalonia.Input.Key.Enter);
            }
        });

        box.LostFocus += (_, _) => UiGuard.Run(null, "이름 고치기", () =>
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
                ? "장면 시작점 — 롤백이 되돌아가고 이어하기가 재개하는 자리입니다."
                : null;

            return arrow;
        }

        arrow[ToolTip.TipProperty] = "접었다 폅니다";
        arrow.PointerPressed += (_, args) => UiGuard.Run(null, "접기", () =>
        {
            args.Handled = true;
            Fold(row.Key, collapse: IsExpanded(row.Key));
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

    private void Press(HierarchyRow row)
    {
        Focus();   // 키보드(↑↓←→)가 하이어아키로 온다
        Selection = row.Pick;
        Paint();
        Picked?.Invoke(row.Pick);
    }

    // ── 키보드 ───────────────────────────────────

    /// <summary>
    /// ↑↓ = 보이는 줄 사이로 고르기, ← = 접기 · 이미 접혔으면 부모로, → = 펴기 · 이미 폈으면 첫 자식으로. 유니티처럼 화살표가
    /// 곧 고르기다 — 인스펙터가 따라온다.
    /// </summary>
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || _renamingKey is not null || _rows.Count == 0)
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
            Avalonia.Input.Key.Left when expandable && IsExpanded(current!.Key) => Folding(current, collapse: true),
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
            UiGuard.Run(null, "하이어아키", () =>
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

    /// <summary>끌고 있는 줄.</summary>
    private string? _dragKey;

    /// <summary>끌고 지나가는 중인 줄 — 받아 줄 자리면 테두리가 뜬다.</summary>
    private string? _hoverKey;

    /// <summary>
    /// 놓을 수 있는 자리인가 — 에피소드를 <b>같은 챕터의</b> 다른 장면 줄에(그 장면으로). ⛔ 챕터 줄에 놓아 장면에서 빼던 것은
    /// 걷었다(2026-11-17 — 에피소드는 늘 장면에 속한다).
    /// </summary>
    internal static bool Accepts(HierarchyRow source, HierarchyRow target) =>
        source.Kind == HierarchyKind.Episode &&
        target.Kind == HierarchyKind.Scene &&
        string.Equals(source.ChapterId, target.ChapterId, StringComparison.Ordinal) &&
        !string.Equals(source.SceneId, target.SceneId, StringComparison.Ordinal);

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

        if (source is null || target is null || !Accepts(source, target))
        {
            Paint();
            return;
        }

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
        }, both, handledEventsToo: true);

        button.AddHandler(PointerMovedEvent, (_, args) => UiGuard.Run(null, "옮기기", () =>
        {
            if (_dragKey is null)
            {
                return;
            }

            string? wanted = RowAt(args.GetPosition(RowHost))?.Key;

            if (!string.Equals(_hoverKey, wanted, StringComparison.Ordinal))
            {
                _hoverKey = wanted;
                Paint();
            }
        }), both, handledEventsToo: true);

        button.AddHandler(PointerReleasedEvent, (_, args) => UiGuard.Run(null, "옮기기", () =>
        {
            if (_dragKey is not null)
            {
                Release(RowAt(args.GetPosition(RowHost)) ?? row);
            }
        }), both, handledEventsToo: true);
    }

    /// <summary>고른 줄만 다시 칠한다 — 줄을 다시 세우지 않는다.</summary>
    private void Paint()
    {
        string? selected = Selection?.Key;

        for (int index = 0; index < _rows.Count && index < RowHost.Children.Count; index++)
        {
            if (RowHost.Children[index] is DockPanel host && host.Children.OfType<Button>().FirstOrDefault() is { } button)
            {
                button.Background = string.Equals(_rows[index].Key, selected, StringComparison.Ordinal)
                    ? new SolidColorBrush(Color.FromArgb(48, 61, 123, 217))
                    : Brushes.Transparent;

                // 끌고 지나가는 동안 받아 줄 자리에 테두리 — 아무 표시가 없으면 놓아도 되는지 모른 채 손을 놓는다.
                bool landing = string.Equals(_rows[index].Key, _hoverKey, StringComparison.Ordinal) &&
                               DragSource() is { } dragged && Accepts(dragged, _rows[index]);
                button.BorderThickness = new Thickness(landing ? 1 : 0);
                button.BorderBrush = landing ? new SolidColorBrush(Color.FromArgb(220, 61, 123, 217)) : Brushes.Transparent;
            }
        }
    }
}
