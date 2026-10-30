using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    Episode
}

/// <summary>
/// 하이어아키에서 고른 것 — 인스펙터가 이것을 보인다. 장면 · 에피소드는 제 챕터를 함께 든다(Id는 챕터 안에서만 유일하다).
/// </summary>
internal sealed record HierarchyPick(
    HierarchyKind Kind,
    string? ChapterId = null,
    string? SceneId = null,
    string? EpisodeId = null)
{
    public static HierarchyPick Project { get; } = new(HierarchyKind.Project);

    /// <summary>그 줄의 신원 — <see cref="HierarchyRow.Key"/>와 같은 열쇠.</summary>
    public string Key => Kind switch
    {
        HierarchyKind.Project => StoryHierarchy.ProjectKey,
        HierarchyKind.Chapter => StoryHierarchy.ChapterKey(ChapterId!),
        HierarchyKind.Scene => StoryHierarchy.SceneKey(ChapterId!, SceneId!),
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
    bool HasWarning)
{
    public HierarchyPick Pick => new(Kind, ChapterId, SceneId, EpisodeId);
}

/// <summary>
/// <b>하이어아키</b> (2026-10-30 소유자: *"유니티 하이어아키처럼 챕터를 부모로 하는 장면이 있고, 이 장면에 포함된
/// 에피소드들을 모으는 방식으로"* — <c>docs/plans/hierarchy.md</c>). 프로젝트 → 챕터 → 장면 → 에피소드를 접었다 펴는
/// 한 그루이고, 어느 줄이든 고르면 <see cref="Picked"/>가 알린다 — 인스펙터가 그것을 보인다.
///
/// ⛔ <b>장면은 여기서도 엔티티가 아니다</b> — 장면 줄은 <see cref="ChapterSceneGrouping"/>이 그릴 때마다 묶는 투영이고,
/// 이 컨트롤이 든 상태는 <b>접힘과 선택 둘뿐</b>이다. 장면ID를 하나도 안 적은 챕터는 장면 단을 생략한다(에피소드마다 제
/// 장면이라 줄이 에피소드 수만큼 서면 노이즈다).
///
/// ⚠ <b>고르는 것으로는 줄을 다시 세우지 않는다</b>(칠만 한다 — <see cref="Paint"/>). 다시 세우면 더블클릭의 둘째 누름이
/// 새 컨트롤에 떨어진다. 보이는 줄이 바뀔 때(접기 · 펴기 · 밖에서 고른 것을 드러낼 때)만 다시 세운다.
///
/// ⚠ <b>편집하지 않는다</b> — 챕터 줄 우클릭은 <see cref="RowMenuRequested"/>로 알리기만 한다(개명 · 제거 규칙은 셸에
/// 한 벌이다).
/// </summary>
public partial class StoryHierarchy : UserControl
{
    internal const string ProjectKey = "project";

    internal static string ChapterKey(string chapterId) => "ch:" + chapterId;

    internal static string SceneKey(string chapterId, string sceneId) => "sc:" + chapterId + "/" + sceneId;

    internal static string EpisodeKey(string chapterId, string episodeId) => "ep:" + chapterId + "/" + episodeId;

    /// <summary>사람이 편 마디들.</summary>
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);

    /// <summary>
    /// 사람이 접은 마디들 — 편 것과 따로 든다. 펼침만 기억하면 "고른 것이 든 마디는 저절로 펴진다"가 사람의 접기를
    /// 이겨 접을 수가 없다(옛 탐색기가 테스트로 잡은 자리).
    /// </summary>
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);

    private readonly List<HierarchyRow> _rows = [];

    private string _projectTitle = string.Empty;
    private IReadOnlyList<ChapterEntry> _chapters = [];
    private bool _hasProject;

    public StoryHierarchy()
    {
        InitializeComponent();
    }

    /// <summary>어느 줄을 골랐다(사람이 눌렀을 때만 — <see cref="Show"/>로 밖에서 고른 것은 알리지 않는다).</summary>
    internal event Action<HierarchyPick>? Picked;

    /// <summary>줄을 우클릭했다 — 그 줄의 컨트롤(차림표의 과녁)과 함께.</summary>
    internal event Action<Control, HierarchyRow>? RowMenuRequested;

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
    internal void Rebuild(string? projectTitle, IReadOnlyList<ChapterEntry> chapters)
    {
        _hasProject = projectTitle is not null;
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

        foreach (string key in PathOf(pick))
        {
            _collapsed.Remove(key);
        }

        Refresh();
    }

    // ── 줄 세우기 ───────────────────────────────────────────────────────────

    private List<HierarchyRow> BuildRows()
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
        bool flat = scenes.All(scene => scene.IsDefault);

        rows.Add(new HierarchyRow(
            HierarchyKind.Chapter, key, chapterId, null, null, chapterId,
            Detail: $"에피소드 {chapter.Model.Episodes.Count}" + (flat && scenes.Count > 0 ? " · 장면 미지정" : string.Empty),
            Depth: 1, IsSceneRoot: false, HasWarning: chapter.HasErrors));

        if (!IsExpanded(key))
        {
            return;
        }

        foreach (ChapterScene scene in scenes)
        {
            if (flat)
            {
                // 장면 미지정 — 에피소드마다 제 장면이라 모두가 루트다. 모든 줄에 ⌂를 다는 것은 표식이 아니라 소음이다.
                foreach (ChapterEpisode episode in scene.Episodes)
                {
                    rows.Add(EpisodeRow(chapterId, scene, episode, depth: 2) with { IsSceneRoot = false });
                }

                continue;
            }

            string sceneKey = SceneKey(chapterId, scene.SceneId);

            rows.Add(new HierarchyRow(
                HierarchyKind.Scene, sceneKey, chapterId, scene.SceneId, null, scene.DisplayName,
                Detail: null, Depth: 2, IsSceneRoot: false, HasWarning: scene.HasSplitEntry));

            if (!IsExpanded(sceneKey))
            {
                continue;
            }

            foreach (ChapterEpisode episode in scene.Episodes)
            {
                rows.Add(EpisodeRow(chapterId, scene, episode, depth: 3));
            }
        }
    }

    private static HierarchyRow EpisodeRow(string chapterId, ChapterScene scene, ChapterEpisode episode, int depth) =>
        new(
            HierarchyKind.Episode,
            EpisodeKey(chapterId, episode.EpisodeId),
            chapterId,
            scene.SceneId,
            episode.EpisodeId,
            episode.EpisodeId,
            Detail: string.IsNullOrWhiteSpace(episode.Title) ||
                    string.Equals(episode.Title.Trim(), episode.EpisodeId, StringComparison.Ordinal)
                ? null
                : episode.Title.Trim(),
            depth,
            IsSceneRoot: string.Equals(scene.RootEpisodeId, episode.EpisodeId, StringComparison.Ordinal),
            HasWarning: false);

    /// <summary>
    /// 이 마디가 펴져 있는가 — 사람이 접지 않았고, 사람이 폈거나 <b>고른 것이 그 안에 있을 때</b>다(처음 열면 고른 것이
    /// 든 챕터 · 장면만 펴진다 — 챕터 수십 개가 다 펴져 있으면 하이어아키가 아니라 목록이다). 프로젝트 줄은 처음부터 펴져
    /// 있다. 고른 마디 <b>자신</b>은 펴지 않는다 — 챕터를 골랐다고 그 챕터가 펼쳐지면 고르는 손짓이 줄을 다시 세운다.
    /// </summary>
    private bool IsExpanded(string key) =>
        !_collapsed.Contains(key) &&
        (key == ProjectKey || _expanded.Contains(key) || PathOf(Selection).Contains(key));

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

        if (pick.Kind == HierarchyKind.Episode && pick.EpisodeId is { } episodeId &&
            _chapters.FirstOrDefault(chapter => string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal))
                ?.Model.FindEpisode(episodeId) is { } episode)
        {
            path.Add(SceneKey(chapterId, episode.EffectiveSceneId));
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
                string.Equals(episode.EffectiveSceneId, pick.SceneId, StringComparison.Ordinal)) == true,
            _ => chapter?.Model.FindEpisode(pick.EpisodeId ?? string.Empty) is not null
        };
    }

    /// <summary>보이는 줄이 그대로면(이름 · 표식까지) 칠만 하고, 바뀌었으면 다시 세운다.</summary>
    private void Refresh()
    {
        List<HierarchyRow> rows = BuildRows();

        if (rows.Count > 0 && rows.SequenceEqual(_rows) && RowHost.Children.Count == _rows.Count)
        {
            Paint();
            return;
        }

        Draw(rows);
    }

    private void Draw(List<HierarchyRow>? rows = null)
    {
        _rows.Clear();
        _rows.AddRange(rows ?? BuildRows());
        RowHost.Children.Clear();

        foreach (HierarchyRow row in _rows)
        {
            RowHost.Children.Add(Build(row));
        }

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
                Text = "챕터가 없습니다. ＋로 만드세요.",
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

        if (row.Kind == HierarchyKind.Episode)
        {
            line.Children.Add(new TextBlock
            {
                Text = row.IsSceneRoot ? "⌂" : "·",
                Width = 12,
                TextAlignment = TextAlignment.Center,
                Opacity = row.IsSceneRoot ? 0.8 : 0.35,
                VerticalAlignment = VerticalAlignment.Center,
                [ToolTip.TipProperty] = row.IsSceneRoot
                    ? "장면 루트 — 롤백이 되돌아가고 이어하기가 재개하는 자리입니다."
                    : null
            });
        }

        // 종류가 한눈에 갈려야 한다 — 들여쓰기만으로는 같은 글자로 보인다. 크기와 무게를 함께 준다.
        (double size, FontWeight weight, double dim) = row.Kind switch
        {
            HierarchyKind.Project => (13.0, FontWeight.Bold, 1.0),
            HierarchyKind.Chapter => (13.0, FontWeight.SemiBold, 1.0),
            HierarchyKind.Scene => (12.0, FontWeight.SemiBold, 0.8),
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
                [ToolTip.TipProperty] = row.Kind == HierarchyKind.Scene
                    ? "밖에서 들어오는 자리가 둘입니다 — 장면은 한 자리에서만 시작해야 합니다."
                    : "이 챕터에 오류가 있습니다 — 아래 검증 보고를 보세요."
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

        button.AddHandler(PointerPressedEvent, (_, args) =>
        {
            if (args.GetCurrentPoint(button).Properties.IsRightButtonPressed)
            {
                args.Handled = true;
                RowMenuRequested?.Invoke(button, row);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        var host = new DockPanel { Margin = new Thickness(row.Depth * 14, 0, 0, 0) };
        Control arrow = Arrow(row);
        DockPanel.SetDock(arrow, Dock.Left);
        host.Children.Add(arrow);
        host.Children.Add(button);

        return host;
    }

    /// <summary>접기 손잡이. 에피소드는 펼 자식이 없어 자리만 비운다(줄이 들쭉날쭉하지 않게).</summary>
    private Border Arrow(HierarchyRow row)
    {
        bool leaf = row.Kind == HierarchyKind.Episode;
        var arrow = new Border
        {
            Width = 16,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch,
            Tag = "fold:" + row.Key,
            Child = new TextBlock
            {
                Text = leaf ? " " : IsExpanded(row.Key) ? "▼" : "▶",
                FontSize = 10,
                Opacity = 0.75,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        if (leaf)
        {
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

    /// <summary>마디를 접거나 편다.</summary>
    internal void Fold(string key, bool collapse)
    {
        if (collapse)
        {
            _collapsed.Add(key);
            _expanded.Remove(key);
        }
        else
        {
            _collapsed.Remove(key);
            _expanded.Add(key);
        }

        Draw();
    }

    private void Press(HierarchyRow row)
    {
        Selection = row.Pick;
        Paint();
        Picked?.Invoke(row.Pick);
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
            }
        }
    }
}
