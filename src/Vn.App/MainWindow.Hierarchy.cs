using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App;

/// <summary>
/// <b>두 그래프의 하이어아키와 연출 그래프의 인스펙터</b> (2026-10-30 HY3 — <c>docs/plans/hierarchy.md</c>).
///
/// 하이어아키는 탭마다 하나씩 서고(컨트롤 하나는 부모가 하나다) 내용은 이 셸이 한 원천(챕터 그래프가 읽은 챕터 목록)에서
/// 채운다. <b>선택은 함께 움직인다</b>: 어느 탭에서 에피소드를 고르든 세션의 선택(그 카드)이 되고, 두 하이어아키와 챕터
/// 그래프가 그것을 비춘다.
///
/// 연출 그래프의 인스펙터는 <b>제 일</b>을 한다(HY-D2): 카드를 고르면 대사 편집기. 하이어아키의 프로젝트 · 챕터 · 장면
/// 줄은 요약과 「챕터 그래프에서 열기」를 보인다 — 화자 · 조건 · 스탯 · 장면 이름을 고치는 자리는 챕터 그래프에 한 벌이다.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// 연출 그래프 인스펙터가 하이어아키의 프로젝트 · 챕터 · 장면 줄을 보이는 중이면 그 줄. 카드를 고르면 풀린다.
    /// </summary>
    private HierarchyPick? _directorSummary;

    /// <summary>연출 그래프 인스펙터의 요약 칸 — 대사 편집기와 빈 안내 사이에 겹쳐 선다(하나만 보인다).</summary>
    private readonly StackPanel DirectorSummary = new() { Margin = new Thickness(12), Spacing = 6, IsVisible = false };

    /// <summary>한 탭의 선택을 다른 쪽에 옮기는 중 — 되돌아오는 알림이 다시 옮기지 않게.</summary>
    private bool _syncingSelection;

    /// <summary>두 탭의 하이어아키가 함께 쓰는 접힘 — 한 그루가 두 자리에 보인다(HY4b).</summary>
    private readonly HierarchyFolds _folds = new();

    /// <summary>하이어아키 배선 — 생성자가 한 번 부른다.</summary>
    private void WireHierarchies()
    {
        ChapterGraph.HierarchyView.Folds = _folds;
        Graph.HierarchyView.Folds = _folds;

        // 한쪽에서 접거나 펴면 다른 쪽도 맞춘다.
        ChapterGraph.HierarchyView.FoldChanged += () => Graph.HierarchyView.RefreshFolds();
        Graph.HierarchyView.FoldChanged += () => ChapterGraph.HierarchyView.RefreshFolds();

        Graph.HierarchyView.Picked += pick => UiGuard.Run(_session, "하이어아키", () => OnDirectorHierarchyPicked(pick));

        // 눈 기둥은 연출 그래프의 하이어아키에만 — 접을 판이 거기 있다(SF2 · docs/plans/hierarchy.md §3).
        Graph.HierarchyView.EyeOf = DirectorEyeOf;
        Graph.HierarchyView.EyeToggled += row => UiGuard.Run(_session, "보이기", () => OnDirectorEyeToggled(row));

        // 두 하이어아키의 차림표 · 이름 고치기는 같은 규칙 한 벌로 한다(HY4 — MainWindow.HierarchyEdit.cs).
        foreach (StoryHierarchy tree in new[] { ChapterGraph.HierarchyView, Graph.HierarchyView })
        {
            tree.CommandRequested += (command, row, anchor) =>
                UiGuard.Run(_session, "하이어아키 차림표", () => OnHierarchyCommand(command, row, anchor));
            tree.RenameRequested += (row, name) =>
                UiGuard.Run(_session, "이름 바꾸기", () => OnHierarchyRename(row, name));
            tree.Dropped += (source, target) =>
                UiGuard.Run(_session, "장면 옮기기", () => OnHierarchyDropped(source, target));
        }

        // 챕터 그래프에서 고른 에피소드가 세션의 선택(그 카드)이 된다 — 연출 그래프로 넘어가도 같은 것을 본다.
        ChapterGraph.EpisodePicked += (chapterId, episodeId) => UiGuard.Run(_session, "선택 맞추기", () =>
        {
            if (_syncingSelection || EpisodeNaming.CardFor(_session.Project, chapterId, episodeId) is not { } card)
            {
                return;
            }

            _syncingSelection = true;

            try
            {
                _session.Select(card.Id);
            }
            finally
            {
                _syncingSelection = false;
            }
        });
    }

    /// <summary>두 하이어아키를 다시 세운다 — 원천은 챕터 그래프가 읽은 챕터 목록 하나다.</summary>
    private void RebuildHierarchies()
    {
        _folds.Remember(_session.ProjectPath);   // 프로젝트가 바뀌었으면 그 프로젝트의 접힘을 읽는다
        string title = ProjectDisplayTitle();
        ChapterGraph.HierarchyView.Rebuild(title, _chapters);
        Graph.HierarchyView.Rebuild(title, _chapters);
    }

    /// <summary>
    /// 연출 그래프의 하이어아키에서 줄을 골랐다 — 그 챕터의 판을 열고, 에피소드면 그 카드를 고른다(대사 편집기). 프로젝트 ·
    /// 챕터 · 장면이면 인스펙터에 요약을 세운다.
    ///
    /// ⚠ <b>고르는 것으로는 접힘을 풀지 않는다</b>(SF2) — 유니티처럼 보이기는 눈이 정한다. 접힌 판의 에피소드를 고르면 그 표의
    ///   줄이 칠해진다.
    /// </summary>
    private void OnDirectorHierarchyPicked(HierarchyPick pick)
    {
        if (pick.ChapterId is { } chapterId)
        {
            _session.SelectFile(_session.EnsureChapterBoard(chapterId), reveal: false);
        }

        if (pick.Kind == HierarchyKind.Episode &&
            EpisodeNaming.CardFor(_session.Project, pick.ChapterId!, pick.EpisodeId!) is { } card)
        {
            _directorSummary = null;
            _session.Select(card.Id);
            ShowSelectedNode();
            Dispatcher.UIThread.Post(Graph.RevealSelected, DispatcherPriority.Background);
            return;
        }

        // 카드 선택을 놓는다 — 그래야 다음에 카드를 누르는 것이 선택의 변화가 되어 요약이 풀린다.
        _session.Select(null);
        _directorSummary = pick;
        ShowSelectedNode();
    }

    /// <summary>
    /// 연출 그래프 하이어아키의 눈 (SF2) — 챕터 줄은 그 판이 펼쳐졌는가, 장면 줄은 그 장면을 접었는가. 판이 아직 없는 챕터는
    /// 판에 보일 것이 없어 눈이 없다.
    /// </summary>
    private HierarchyEye DirectorEyeOf(HierarchyRow row)
    {
        if (row.Kind is not (HierarchyKind.Chapter or HierarchyKind.Scene) ||
            row.ChapterId is not { } chapterId ||
            EpisodeNaming.BoardOf(_session.Project, chapterId) is not { } board)
        {
            return HierarchyEye.None;
        }

        bool chapterOpen = _session.ExpandedFileIds.Contains(board.Id);

        return row.Kind == HierarchyKind.Chapter
            ? chapterOpen ? HierarchyEye.Open : HierarchyEye.Folded
            : !chapterOpen
                ? HierarchyEye.UnderFolded
                : _session.CollapsedScenes.Contains(new GraphSceneKey(board.Id, row.SceneId!))
                    ? HierarchyEye.Folded
                    : HierarchyEye.Open;
    }

    /// <summary>
    /// 눈을 눌렀다 — 챕터는 판째 접고 편다(판 이름표 · 판 상자와 같은 상태), 장면은 그 장면만 간소화한다. 챕터가 접혀 가려진
    /// 장면의 눈을 누르면 챕터를 펴고 그 장면을 보인다(보이라고 누른 것이다).
    /// </summary>
    private void OnDirectorEyeToggled(HierarchyRow row)
    {
        if (row.ChapterId is not { } chapterId || EpisodeNaming.BoardOf(_session.Project, chapterId) is not { } board)
        {
            return;
        }

        switch (DirectorEyeOf(row))
        {
            case HierarchyEye.Open when row.Kind == HierarchyKind.Chapter:
                _session.SetFileExpanded(board.Id, expanded: false);
                break;

            case HierarchyEye.Folded when row.Kind == HierarchyKind.Chapter:
                _session.SetFileExpanded(board.Id, expanded: true);
                break;

            case HierarchyEye.Open:
                _session.SetSceneCollapsed(board.Id, row.SceneId!, collapsed: true);
                break;

            case HierarchyEye.Folded:
                _session.SetSceneCollapsed(board.Id, row.SceneId!, collapsed: false);
                break;

            case HierarchyEye.UnderFolded:
                _session.SetSceneCollapsed(board.Id, row.SceneId!, collapsed: false);
                _session.SetFileExpanded(board.Id, expanded: true);
                break;
        }
    }

    /// <summary>세션이 고른 카드를 하이어아키의 줄로 — 챕터 판의 에피소드 카드가 아니면 null.</summary>
    private HierarchyPick? PickOf(StoryNode? node) =>
        node is DialogueNode dialogue && EpisodeNaming.ChapterOf(_session.Project, dialogue) is { } chapter
            ? new HierarchyPick(HierarchyKind.Episode, chapter.ChapterId, EpisodeId: EpisodeNaming.EpisodeIdOf(dialogue))
            : null;

    /// <summary>
    /// 세션의 선택이 바뀌었다 — 카드를 고르면 요약이 풀리고, 연출 그래프의 하이어아키와 챕터 그래프가 그 에피소드를 비춘다.
    /// </summary>
    private void SyncSelection()
    {
        if (_session.SelectedNodeId is not null)
        {
            _directorSummary = null;
        }

        if (_syncingSelection || PickOf(_session.SelectedNode) is not { } pick)
        {
            return;
        }

        _syncingSelection = true;

        try
        {
            ChapterGraph.ShowEpisode(pick.ChapterId!, pick.EpisodeId!);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>연출 그래프 인스펙터의 머리글과 요약 칸 — <see cref="ShowSelectedNode"/>가 부른다.</summary>
    private void ShowDirectorInspector(StoryNode? node)
    {
        bool summary = _directorSummary is not null && node is null;
        DirectorSummary.IsVisible = summary;

        if (summary)
        {
            FillDirectorSummary(_directorSummary!);
        }
        else if (node is DialogueNode dialogue)
        {
            Graph.SetInspectorHeader(
                PickOf(dialogue) is { } pick ? $"에피소드 · {pick.EpisodeId}" : $"대사 노드 · {dialogue.Name}",
                "대사 줄 — 화자 · 대사 · 분기");
        }
        else
        {
            Graph.SetInspectorHeader("인스펙터", "하이어아키나 판에서 고르면 여기서 편집합니다");
        }

        Graph.HierarchyView.Show(summary ? _directorSummary : PickOf(node));
    }

    /// <summary>프로젝트 · 챕터 · 장면 줄의 요약 — 고치는 자리는 챕터 그래프다(「챕터 그래프에서 열기」).</summary>
    private void FillDirectorSummary(HierarchyPick pick)
    {
        DirectorSummary.Children.Clear();
        ChapterEntry? chapter = _chapters.FirstOrDefault(entry =>
            string.Equals(entry.ChapterId, pick.ChapterId, StringComparison.Ordinal));

        switch (pick.Kind)
        {
            case HierarchyKind.Project:
            {
                IReadOnlyList<Vn.Authoring.Definition.SpeakerSpec> speakers = _session.Definition.Speakers;
                Graph.SetInspectorHeader("화자", $"프로젝트 전체의 캐스트 — {speakers.Count}명");
                Note(speakers.Count == 0 ? "아직 화자가 없습니다." : string.Join(" · ", speakers.Select(speaker => speaker.Name)));
                OpenInChapterGraph(pick, "챕터 그래프에서 화자 고치기");
                break;
            }

            case HierarchyKind.Chapter when chapter is not null:
            {
                IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(chapter.Model);
                Graph.SetInspectorHeader(
                    $"챕터 · {chapter.ChapterId}", $"에피소드 {chapter.Model.Episodes.Count} · 장면 {scenes.Count}");
                Note("조건 · 스탯은 챕터 그래프에서 고칩니다.");

                foreach (ChapterScene scene in scenes)
                {
                    Link($"{scene.DisplayName}  ({scene.Episodes.Count})", "summary-scene:" + scene.SceneId, () =>
                        PickInDirector(new HierarchyPick(HierarchyKind.Scene, chapter.ChapterId, scene.SceneId)));
                }

                OpenInChapterGraph(pick, "챕터 그래프에서 조건 · 스탯 열기");
                break;
            }

            case HierarchyKind.Scene when chapter is not null &&
                                         ChapterSceneGrouping.Of(chapter.Model).FirstOrDefault(item =>
                                             string.Equals(item.SceneId, pick.SceneId, StringComparison.Ordinal)) is { } scene:
            {
                Graph.SetInspectorHeader($"장면 · {scene.DisplayName}", $"에피소드 {scene.Episodes.Count}");
                Note("에피소드를 누르면 그 카드의 대사를 씁니다. 장면 이름은 챕터 그래프에서 고칩니다.");

                foreach (ChapterEpisode episode in scene.Episodes)
                {
                    string episodeId = episode.EpisodeId;
                    bool root = string.Equals(scene.RootEpisodeId, episodeId, StringComparison.Ordinal);
                    Link((root ? "⌂ " : "· ") + episodeId, "summary-episode:" + episodeId, () =>
                        PickInDirector(new HierarchyPick(HierarchyKind.Episode, chapter.ChapterId, scene.SceneId, episodeId)));
                }

                OpenInChapterGraph(pick, "챕터 그래프에서 장면 열기");
                break;
            }

            default:
                Graph.SetInspectorHeader("인스펙터", "고른 것이 사라졌습니다 — 하이어아키에서 다시 고르세요");
                break;
        }
    }

    /// <summary>요약 칸에서 고른 것 — 연출 그래프 하이어아키의 그 줄을 누른 것과 같다.</summary>
    private void PickInDirector(HierarchyPick pick)
    {
        OnDirectorHierarchyPicked(pick);
        Graph.HierarchyView.Show(pick);
    }

    private void Note(string text) => DirectorSummary.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 11,
        Opacity = 0.65,
        TextWrapping = TextWrapping.Wrap
    });

    private void Link(string text, string tag, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 11,
            Padding = new Thickness(6, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Tag = tag
        };
        button.Click += (_, _) => UiGuard.Run(_session, "요약", onClick);
        DirectorSummary.Children.Add(button);
    }

    /// <summary>「챕터 그래프에서 열기」 — 챕터 그래프 탭으로 넘어가 그 인스펙터에 같은 줄을 세운다.</summary>
    private void OpenInChapterGraph(HierarchyPick pick, string text)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0),
            Tag = "summary-open"
        };
        button.Click += (_, _) => UiGuard.Run(_session, "챕터 그래프에서 열기", () =>
        {
            MainTabs.SelectedItem = ChapterTabItem;
            ChapterGraph.Inspect(pick);
        });
        DirectorSummary.Children.Add(button);
    }
}
