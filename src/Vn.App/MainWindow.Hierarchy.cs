using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
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
/// <b>두 그래프의 하이어아키와 연출 그래프의 인스펙터</b>.
///
/// 하이어아키는 탭마다 하나씩 서고(컨트롤 하나는 부모가 하나다) 내용은 이 셸이 한 원천(챕터 그래프가 읽은 챕터 목록)에서
/// 채운다. <b>선택은 함께 움직인다</b>: 어느 탭에서 에피소드를 고르든 세션의 선택(그 카드)이 되고, 두 하이어아키와 챕터
/// 그래프가 그것을 비춘다.
///
/// 연출 그래프의 인스펙터는 <b>제 일</b>을 한다:카드를 고르면 대사 편집기. 하이어아키의 프로젝트 · 챕터 · 장면
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

    /// <summary>[대본] 「화자」 탭의 칸 — 화자 이름 목록(side-tabs V3). 늘 서 있다.</summary>
    private readonly StackPanel DirectorSpeakers = new() { Margin = new Thickness(12), Spacing = 6 };

    /// <summary>[대본] 「조건 및 스탯」 탭의 칸 — 지금 챕터(활성 판)의 스탯 표 · 조건 이름(side-tabs V3). 늘 서 있다.</summary>
    private readonly StackPanel DirectorConditions = new() { Margin = new Thickness(12), Spacing = 6 };

    /// <summary>
    /// 요약 조각(제목 · 안내 · 표 · 목록)이 들어갈 칸 — 보통은 인스펙터의 <see cref="DirectorSummary"/>이고, 화자 · 조건 탭을 짓는 동안만
    /// 그 탭의 칸이다(<see cref="FillSheet"/>). 같은 조각을 세 자리가 함께 쓴다.
    /// </summary>
    private StackPanel _sheet;

    /// <summary>칸 하나를 비우고 다시 짓는다 — 짓는 동안 요약 조각이 그 칸에 선다.</summary>
    private void FillSheet(StackPanel panel, Action fill)
    {
        StackPanel was = _sheet;
        _sheet = panel;
        panel.Children.Clear();

        try
        {
            fill();
        }
        finally
        {
            _sheet = was;
        }
    }

    /// <summary>「화자」 탭을 다시 짓는다 — 화자 이름을 더하고 고치고 지운다. 캐릭터 키는 [구성] 탭이다.</summary>
    private void FillSpeakerTab() => FillSheet(DirectorSpeakers, () =>
    {
        SpeakerList();
        OpenInChapterGraph(HierarchyPick.Project, "[구성] 탭에서 캐릭터 키 잇기");
    });

    /// <summary>「조건 및 스탯」 탭이 보이는 챕터 — 활성 판의 챕터다(툴바 콤보 · 탭의 작은 드롭다운과 같은 값).</summary>
    private ChapterEntry? ConditionTabChapter =>
        _session.ActiveFile?.Name is { } name
            ? _chapters.FirstOrDefault(entry => string.Equals(entry.ChapterId, name, StringComparison.Ordinal))
            : null;

    /// <summary>「조건 및 스탯」 탭을 다시 짓는다 — 지금 챕터의 스탯 표(대사에 붙여 쓰기)와 조건 이름. 식 · 스탯은 [구성] 탭에서 정한다.</summary>
    private void FillConditionTab() => FillSheet(DirectorConditions, () =>
    {
        if (ConditionTabChapter is not { } chapter)
        {
            Guide("챕터를 고르면 그 챕터의 조건 · 스탯이 여기 섭니다.", "고른 챕터가 없습니다.");
            return;
        }

        Guide("스탯과 조건의 식은 [구성] 탭에서 정합니다.");
        StatTable(chapter.Model.Stats);

        // 조건 이름은 여기서 더하고 고치고 지운다(writer-fields W4). 식은 [구성] 탭이다.
        ConditionList(chapter.ChapterId);
        OpenInChapterGraph(new HierarchyPick(HierarchyKind.Chapter, chapter.ChapterId), "[구성] 탭에서 조건 · 스탯 열기");
    });

    /// <summary>화자 · 조건 탭을 다시 짓는다 — 구조가 바뀌었거나 지금 챕터가 바뀌었다.</summary>
    private void FillSideTabs()
    {
        FillSpeakerTab();
        FillConditionTab();
    }

    /// <summary>한 탭의 선택을 다른 쪽에 옮기는 중 — 되돌아오는 알림이 다시 옮기지 않게.</summary>
    private bool _syncingSelection;

    /// <summary>두 탭의 하이어아키가 함께 쓰는 접힘 — 한 그루가 두 자리에 보인다(HY4b).</summary>
    private readonly HierarchyFolds _folds = new();

    /// <summary>하이어아키 배선 — 생성자가 한 번 부른다.</summary>
    private void WireHierarchies()
    {
        ChapterGraph.HierarchyView.Folds = _folds;
        Graph.HierarchyView.Folds = _folds;

        // [대본] 탭은 에피소드를 보이는 제목으로 보이고 고친다 — EpisodeId는 [구성] 탭의 인스펙터에서 정한다.
        Graph.HierarchyView.ShowEpisodeTitles = true;

        // [구성] 탭 하이어아키는 고르기만 한다 — 챕터 · 장면 · 에피소드는 [대본] 탭에서 더하고 지우고 고친다.
        ChapterGraph.HierarchyView.ReadOnly = true;

        // 한쪽에서 접거나 펴면 다른 쪽도 맞춘다.
        ChapterGraph.HierarchyView.FoldChanged += () => Graph.HierarchyView.RefreshFolds();
        Graph.HierarchyView.FoldChanged += () => ChapterGraph.HierarchyView.RefreshFolds();

        // 기둥째 접기(HC)도 두 탭이 함께 — 하나를 접으면 다른 탭도 접히고, 다음에 열 때도 접혀 있다.
        bool folded = AppSettingsService.LoadHierarchyFolded();
        ChapterGraph.HierarchyDock.Fold(folded);
        Graph.HierarchyDock.Fold(folded);
        ChapterGraph.HierarchyDock.Changed += value => OnHierarchyDockChanged(Graph.HierarchyDock, value);
        Graph.HierarchyDock.Changed += value => OnHierarchyDockChanged(ChapterGraph.HierarchyDock, value);

        // 오른쪽 기둥(탭 셋)도 두 탭이 함께 접히고 기억된다 — 하이어아키와는 따로다(side-fold K1).
        bool sideFolded = AppSettingsService.LoadSideFolded();
        ChapterGraph.SideDock.Fold(sideFolded);
        Graph.SideDock.Fold(sideFolded);
        ChapterGraph.SideDock.Changed += value => OnSideDockChanged(Graph.SideDock, value);
        Graph.SideDock.Changed += value => OnSideDockChanged(ChapterGraph.SideDock, value);

        Graph.HierarchyView.Picked += pick => UiGuard.Run(_session, "하이어아키", () =>
        {
            OnDirectorHierarchyPicked(pick);

            // 하이어아키의 챕터 줄은 「그 챕터로 가자」다 — 화면을 그리로 옮긴다. 판의 빈 곳을 눌러 챕터를 고른 것(AreaPicked)은
            // 이미 그 자리를 보고 있으므로 옮기지 않는다 — 그래서 OnDirectorHierarchyPicked 안이 아니라 여기다.
            if (pick.Kind == HierarchyKind.Chapter && pick.ChapterId is { } chapterId)
            {
                Graph.GoToBoard(_session.EnsureChapterBoard(chapterId));
            }
        });

        // 판에서 고른 선 — 인스펙터가 그 선을 설명한다(MainWindow.LineInspector.cs).
        Graph.LineSelected += line => UiGuard.Run(_session, "선 고르기", () => OnDirectorLinePicked(line));

        // 눈 기둥은 연출 그래프의 하이어아키에만 — 접을 판이 거기 있다(SF2 · docs/plans/hierarchy.md §3).
        Graph.HierarchyView.EyeOf = DirectorEyeOf;
        Graph.HierarchyView.EyeToggled += row => UiGuard.Run(_session, "보이기", () => OnDirectorEyeToggled(row));

        // 두 하이어아키의 차림표 · 이름 고치기는 같은 규칙 한 벌로 한다(HY4 — MainWindow.HierarchyEdit.cs).
        foreach (StoryHierarchy tree in new[] { ChapterGraph.HierarchyView, Graph.HierarchyView })
        {
            tree.CommandRequested += (command, row, anchor) =>
                UiGuard.Run(_session, "하이어아키 차림표", () => OnHierarchyCommand(command, row, anchor));
            tree.RenameRequested += (row, name) =>
                UiGuard.Run(_session, "이름 바꾸기", () => OnHierarchyRename(tree, row, name));
            tree.Dropped += (source, target) =>
                UiGuard.Run(_session, "장면 옮기기", () => OnHierarchyDropped(source, target));
        }

        // 카드를 두 번 누르면 다른 탭의 같은 에피소드로 간다.
        ChapterGraph.EpisodeOpened += (chapterId, episodeId) =>
            UiGuard.Run(_session, "[대본] 탭으로", () => OpenInDirector(chapterId, episodeId));
        ChapterGraph.BranchCardOpened += nodeId => UiGuard.Run(_session, "[대본] 탭으로", () => OpenBranchInDirector(nodeId));
        Graph.CardOpened += nodeId => UiGuard.Run(_session, "[구성] 탭으로", () => OpenInChapterGraph(nodeId));

        // 판의 빈 곳을 누르면 하이어아키에서 그 줄을 고른 것과 같다(2026-11-17) — 장면 영역은 메모 공간, 챕터 영역은 스탯 · 조건,
        // 챕터 밖의 빈 판은 화자.
        Graph.AreaPicked += pick => UiGuard.Run(_session, "판", () => PickInDirector(pick));

        // [대본] 툴바의 [삭제]가 고른 챕터 · 장면을 지우자고 한다 — 확인 칸은 셸이 띄운다(챕터 · 장면 지우기는 셸의 일이다).
        Graph.RemoveRequested += (pick, anchor) => UiGuard.Run(_session, "삭제", () => OnDirectorRemoveRequested(pick, anchor));

        // [대본] 판의 챕터 밖 빈 곳 우클릭 「챕터 추가」 — 하이어아키의 「챕터 추가」와 같다(세우고 고른다).
        Graph.ChapterAddRequested += at => UiGuard.Run(_session, "챕터 추가", () => CreateChapterNow(at));
        Graph.FirstChapterRequested += () => UiGuard.Run(_session, "챕터 추가", () => CreateChapterNow(null));

        // [구성] 빈 판의 단추는 [대본] 탭으로 데려가 거기서 만든다 — 만드는 일은 [대본]의 것이다(first-chapter F2).
        ChapterGraph.FirstChapterRequested += () => UiGuard.Run(_session, "챕터 추가", () =>
        {
            MainTabs.SelectedItem = GraphTabItem;
            CreateChapterNow(null);
        });

        // 선택지 간선도 두 번 누르면 다른 탭의 같은 간선으로 간다.
        ChapterGraph.ChoiceOpened += (chapterId, from, to, label) =>
            UiGuard.Run(_session, "[대본] 탭으로", () => OpenChoiceInDirector(chapterId, from, to, label));
        Graph.ChoiceOpened += (chapterId, from, to, label) =>
            UiGuard.Run(_session, "[구성] 탭으로", () => OpenChoiceInChapterGraph(chapterId, from, to, label));

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

    /// <summary>
    /// [구성] 판에서 에피소드 카드를 두 번 눌렀다 — [대본] 탭으로 넘어가 그 챕터의 판을 열고 그 카드를 고르고 보이게 한다.
    /// </summary>
    private void OpenInDirector(string chapterId, string episodeId)
    {
        if (EpisodeNaming.CardFor(_session.Project, chapterId, episodeId) is not { } card)
        {
            return;
        }

        MainTabs.SelectedItem = GraphTabItem;
        OnDirectorHierarchyPicked(new HierarchyPick(HierarchyKind.Episode, chapterId, EpisodeId: episodeId));
        _session.Select(card.Id);
    }

    /// <summary>
    /// [구성] 판에서 분기 카드의 칩을 두 번 눌렀다 — [대본] 탭으로 넘어가 그 챕터의 판을 열고 그 분기 카드를 고르고 보이게 한다
    /// (하이어아키의 분기 카드 줄을 누른 것과 같은 길).
    /// </summary>
    private void OpenBranchInDirector(string nodeId)
    {
        if (PickOf(_session.Project.FindNode(nodeId)) is not { Kind: HierarchyKind.Branch } pick)
        {
            return;
        }

        MainTabs.SelectedItem = GraphTabItem;
        OnDirectorHierarchyPicked(pick);
    }

    /// <summary>
    /// [대본] 판에서 카드를 두 번 눌렀다 — [구성] 탭으로 넘어가 그 챕터를 열고, 에피소드 카드면 그 에피소드를, 분기 카드면 그 칩을
    /// 고르고 보이게 한다(칩은 집 에피소드 곁에 선다). ⛔ 2026-11-17까지 분기 카드는 집 에피소드를 골랐다.
    /// </summary>
    private void OpenInChapterGraph(string nodeId)
    {
        if (PickOf(_session.Project.FindNode(nodeId)) is not { ChapterId: { } chapterId, EpisodeId: { } episodeId } pick)
        {
            return;
        }

        MainTabs.SelectedItem = ChapterTabItem;
        ChapterGraph.SelectChapter(chapterId);

        if (pick is { Kind: HierarchyKind.Branch, NodeId: { } branchId })
        {
            ChapterGraph.SelectBranchCard(branchId);
        }
        else
        {
            ChapterGraph.SelectEpisode(episodeId);
        }

        Dispatcher.UIThread.Post(() => ChapterGraph.RevealEpisode(episodeId), DispatcherPriority.Background);
    }

    /// <summary>
    /// [구성] 판에서 선택지 간선을 두 번 눌렀다 — [대본] 탭으로 넘어가 그 챕터의 판을 열고 그 선택지 레인을 고른다(인스펙터가
    /// 그 선을 설명한다). 판을 다시 그린 뒤에 레인이 서므로 고르기는 미룬다.
    /// </summary>
    private void OpenChoiceInDirector(string chapterId, string from, string to, string label)
    {
        MainTabs.SelectedItem = GraphTabItem;
        _session.SelectFile(_session.EnsureChapterBoard(chapterId), reveal: false);
        Dispatcher.UIThread.Post(
            () => UiGuard.Run(_session, "[대본] 탭으로", () => Graph.SelectChoiceRail(chapterId, from, to, label)),
            DispatcherPriority.Background);
    }

    /// <summary>[대본] 판에서 선택지 레인을 두 번 눌렀다 — [구성] 탭으로 넘어가 그 챕터를 열고 그 간선을 고른다.</summary>
    private void OpenChoiceInChapterGraph(string chapterId, string from, string to, string label)
    {
        MainTabs.SelectedItem = ChapterTabItem;
        ChapterGraph.SelectChapter(chapterId);
        ChapterGraph.SelectEdgeKey(from, to, label);
        Dispatcher.UIThread.Post(() => ChapterGraph.RevealEpisode(from), DispatcherPriority.Background);
    }

    /// <summary>한 탭에서 하이어아키 기둥을 접거나 폈다 — 다른 탭을 맞추고 기억한다.</summary>
    private static void OnHierarchyDockChanged(ColumnDock other, bool folded)
    {
        other.Fold(folded);
        AppSettingsService.SaveHierarchyFolded(folded);
    }

    /// <summary>한 탭에서 오른쪽 기둥을 접거나 폈다 — 다른 탭을 맞추고 기억한다.</summary>
    private static void OnSideDockChanged(ColumnDock other, bool folded)
    {
        other.Fold(folded);
        AppSettingsService.SaveSideFolded(folded);
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
    /// 주의: <b>고르는 것으로는 접힘을 풀지 않는다</b> —유니티처럼 보이기는 눈이 정한다. 접힌 판의 에피소드를 고르면 그 표의
    ///   줄이 칠해진다.
    /// </summary>
    private void OnDirectorHierarchyPicked(HierarchyPick pick)
    {
        _directorLine = null;
        Graph.ReleaseLine();

        // 고른 것을 보이는 탭으로 간다 — 프로젝트는 화자, 그 밖은 인스펙터(챕터 · 장면은 이름 칸이 인스펙터 머리다 — side-tabs V3).
        Graph.ShowSideTab(pick.Kind == HierarchyKind.Project ? SideTab.Speakers : SideTab.Inspector);

        if (pick.ChapterId is { } chapterId)
        {
            _session.SelectFile(_session.EnsureChapterBoard(chapterId), reveal: false);
        }

        // 분기 카드 줄 — 그 카드를 고르고 보이게 한다(에피소드 카드와 같다).
        if (pick.Kind == HierarchyKind.Branch && pick.NodeId is { } branchId && _session.Project.FindNode(branchId) is not null)
        {
            _directorSummary = null;
            _session.Select(branchId);
            ShowSelectedNode();
            Dispatcher.UIThread.Post(Graph.RevealSelected, DispatcherPriority.Background);
            return;
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

    /// <summary>세션이 고른 카드를 하이어아키의 줄로 — 에피소드 카드면 에피소드 줄, 분기 카드면 집 밑의 그 줄. 그 밖이면 null.</summary>
    private HierarchyPick? PickOf(StoryNode? node)
    {
        if (node is not DialogueNode dialogue)
        {
            return null;
        }

        ChapterDocument chapter = EpisodeNaming.ChapterOf(_session.Project, dialogue);

        if (dialogue.IsBranchCard)
        {
            return ChapterBranchCards.HomesOf(_session.Project, chapter.ChapterId)
                .FirstOrDefault(home => home.Card.Id == dialogue.Id) is { } home
                ? new HierarchyPick(HierarchyKind.Branch, chapter.ChapterId, EpisodeId: home.EpisodeId, NodeId: dialogue.Id)
                : null;
        }

        return new HierarchyPick(HierarchyKind.Episode, chapter.ChapterId, EpisodeId: EpisodeNaming.EpisodeIdOf(dialogue));
    }

    /// <summary>
    /// 에피소드 카드의 인스펙터 머리글 — 「{챕터} - {장면}」(2026-11-16 — 이름이 들어가므로 「챕터」 꼬리를 뗐다). 챕터는 하이어아키에 보이는 챕터 Id다.
    /// 제목은 바로 아래 이름 칸이 보인다.
    /// </summary>
    private string EpisodeHeader(DialogueNode card)
    {
        ChapterDocument chapter = EpisodeNaming.ChapterOf(_session.Project, card);

        // 이름으로 — 챕터는 표시 이름(비면 ID), 장면은 장면 이름(비면 장면ID). [대본] 탭은 ID를 보이지 않는다(display-names N3).
        string head = chapter.ShownName;

        return EpisodeNaming.EpisodeFor(chapter, card) is { } episode
            ? $"{head} - {ChapterSceneNotes.NameOf(chapter.SceneNotes, episode.SceneId) ?? episode.SceneId}"
            : head;
    }

    /// <summary>
    /// 세션의 선택이 바뀌었다 — 카드를 고르면 요약이 풀리고, 연출 그래프의 하이어아키와 챕터 그래프가 그 에피소드를 비춘다.
    /// </summary>
    private void SyncSelection()
    {
        if (_session.SelectedNodeId is not null)
        {
            _directorSummary = null;
            _directorLine = null;
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
        bool summary = (_directorSummary is not null || _directorLine is not null) && node is null;
        DirectorSummary.IsVisible = summary;

        if (summary && _directorLine is { } line)
        {
            FillLineSummary(line);
        }
        else if (summary)
        {
            FillDirectorSummary(_directorSummary!);
        }
        else if (node is DialogueNode dialogue)
        {
            // 에피소드 카드는 「{챕터} - {장면}」이다. 부제는 두지 않는다.
            Graph.SetInspectorHeader(
                PickOf(dialogue) is not null ? EpisodeHeader(dialogue) : $"대사 노드 · {dialogue.Name}",
                string.Empty);
        }
        else
        {
            Graph.SetInspectorHeader("인스펙터", "하이어아키나 판에서 고르면 여기서 편집합니다");
        }

        // 요약이 선 줄(프로젝트 · 챕터 · 장면) — [삭제]가 무엇을 지울지 본다(2026-11-18 소유자).
        Graph.SummaryPick = summary && _directorLine is null ? _directorSummary : null;

        // 장면 요약이 서 있는 동안 [＋ 에피소드]는 그 장면에 세운다(2026-11-17).
        Graph.NewNodeScene = summary && _directorLine is null &&
                             _directorSummary is { Kind: HierarchyKind.Scene, ChapterId: { } sceneChapter, SceneId: { } pickedScene }
            ? (sceneChapter, pickedScene)
            : null;

        Graph.HierarchyView.Show(summary ? _directorSummary : PickOf(node));
    }

    /// <summary>
    /// 프로젝트 · 챕터 · 장면 줄의 인스펙터 요약. 머리에는 이름만 둔다 — 챕터 · 장면은 그 이름을 고치는 이름 칸이다(⛔ 2026-11-18 소유자 —
    /// 「챕터 · 」 · 「장면 · 」 머리말과 화자 수 · 에피소드 · 장면 수의 부제를 걷었다: 쓸모없는 정보. 같은 날 이름 글을 이름 칸으로 — 에피소드와
    /// 마찬가지로 여기서 고친다). 화자 · 스탯 · 조건은 제 탭에 늘 서 있다(side-tabs V3) — 프로젝트는 그 안내, 챕터는 이름 칸 · 떠도는 메모.
    /// </summary>
    private void FillDirectorSummary(HierarchyPick pick)
    {
        DirectorSummary.Children.Clear();
        ChapterEntry? chapter = _chapters.FirstOrDefault(entry =>
            string.Equals(entry.ChapterId, pick.ChapterId, StringComparison.Ordinal));

        switch (pick.Kind)
        {
            case HierarchyKind.Project:
            {
                // 화자는 「화자」 탭, 조건 · 스탯은 「조건 및 스탯」 탭에 늘 서 있다(side-tabs V3) — 여기는 그 안내뿐이다.
                Graph.SetInspectorHeader("프로젝트", string.Empty);
                Guide("화자는 「화자」 탭, 조건 · 스탯은 「조건 및 스탯」 탭(챕터마다)에서 다룹니다.");
                break;
            }

            case HierarchyKind.Chapter when chapter is not null:
            {
                // 머리는 이름 칸이다 — 여기서 고친다(2026-11-18 소유자 — 에피소드와 마찬가지로).
                string chapterId = chapter.ChapterId;
                Graph.SetInspectorNameEditor(
                    StoryHierarchy.ChapterKey(chapterId),
                    chapter.Model.ShownName,
                    "챕터 이름 (비우면 챕터 ID)",
                    $"게임에 보이는 챕터 이름입니다. 고치고 Enter. 비우면 챕터 ID({chapterId})가 보입니다. ID는 [구성] 탭에서 정합니다.",
                    name => RenameChapter(chapterId, name));
                // 스탯 표 · 조건 이름은 「조건 및 스탯」 탭에 산다(side-tabs V3).
                Guide("이 챕터의 스탯 · 조건은 「조건 및 스탯」 탭에 있습니다.");
                OrphanSceneNotes(chapter.ChapterId);

                // 장면 · 에피소드 목록은 세우지 않는다 — 왼쪽 하이어아키가 같은 것을 보인다(2026-11-16 소유자).
                break;
            }

            case HierarchyKind.Scene when chapter is not null &&
                                         ChapterSceneGrouping.Of(chapter.Model).FirstOrDefault(item =>
                                             string.Equals(item.SceneId, pick.SceneId, StringComparison.Ordinal)) is { } scene:
            {
                // 머리는 이름 칸이다 — 여기서 고친다(2026-11-18 소유자). ⛔ 「장면 이름은 하이어아키에서 고칩니다」 안내는 걷었다.
                string sceneChapterId = chapter.ChapterId;
                string sceneId = scene.SceneId;
                Graph.SetInspectorNameEditor(
                    StoryHierarchy.SceneKey(sceneChapterId, sceneId),
                    scene.DisplayName,
                    "장면 이름 (비우면 장면 ID)",
                    $"장면 이름입니다. 고치고 Enter. 비우면 장면 ID({sceneId})가 보입니다. 같은 이름의 장면이 있어도 합쳐지지 않습니다.",
                    name => RenameScene(sceneChapterId, sceneId, name));

                // 에피소드 목록은 세우지 않는다 — 왼쪽 하이어아키가 같은 것을 보인다(2026-11-16 소유자). 메모 공간의 [＋ 에피소드]가
                // 그 장면의 에피소드를 고르는 자리다.
                // 「[구성] 탭에서 장면 열기」도 두지 않는다(2026-11-16 소유자) — 장면 요약은 메모 공간이다.
                SceneNoteSpace(chapter.ChapterId, scene);
                break;
            }

            default:
                Graph.SetInspectorHeader("인스펙터", "고른 것이 사라졌습니다 — 하이어아키에서 다시 고르세요");
                break;
        }
    }

    /// <summary>요약 칸의 작은 제목 — 챕터 요약의 「스탯」 · 「장면」.</summary>
    private void Heading(string text) => _sheet.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 8, 0, 0)
    });

    /// <summary>
    /// 챕터의 스탯 표 — 스탯 · 범위(처음 값) · 대본에 쓰는 글(<see cref="ChapterStatScript.InlineRead"/>) · [복사]. 작가가 대사에
    /// 스탯의 지금 값을 넣을 때 키를 외우거나 문법을 칠 필요 없이 그대로 붙여 쓴다. 글은 끌어 골라도 된다.
    /// </summary>
    private void StatTable(IReadOnlyList<ChapterStat> stats)
    {
        Heading("스탯 — 대사에 붙여 쓰기");

        if (stats.Count == 0)
        {
            Guide("이 챕터에는 아직 스탯이 없습니다 — [구성] 탭에서 더합니다.", "이 챕터에는 아직 스탯이 없습니다.");
            return;
        }

        var table = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 3
        };

        void Cell(Control cell, int row, int column)
        {
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            table.Children.Add(cell);
        }

        TextBlock Head(string text) => new() { Text = text, FontSize = 10, Opacity = 0.6 };

        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Cell(Head("스탯"), 0, 0);
        Cell(Head("범위"), 0, 1);
        Cell(Head("대본에 쓰는 글"), 0, 2);

        int row = 1;

        foreach (ChapterStat stat in stats)
        {
            string snippet = ChapterStatScript.InlineRead(stat);
            string name = stat.Key;   // 스탯은 키만 있다(writer-fields W2)
            string range = stat.Type == ChapterStatType.Bool
                ? "참/거짓(1/0) · 처음 " + (stat.Initial != 0 ? "참" : "거짓")
                : $"{stat.Minimum}~{stat.Maximum} · 처음 {stat.Initial}";

            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Cell(new TextBlock { Text = name, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, row, 0);
            Cell(new TextBlock { Text = range, FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center }, row, 1);
            Cell(new SelectableTextBlock
            {
                Text = snippet,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Tag = "stat-snippet:" + stat.Key
            }, row, 2);

            var copy = new Button
            {
                Content = "복사",
                FontSize = 10,
                Padding = new Thickness(6, 1),
                Tag = "stat-copy:" + stat.Key
            };
            ToolTip.SetTip(copy, $"{snippet}를 복사합니다 — 대사에 붙이면 그 자리에 {name}의 지금 값이 나옵니다.");
            copy.Click += async (_, _) => await UiGuard.RunAsync(_session, "스탯 복사", () => CopySnippetAsync(snippet, name));
            Cell(copy, row, 3);

            row++;
        }

        _sheet.Children.Add(table);
    }

    private async Task CopySnippetAsync(string snippet, string name)
    {
        if (Clipboard is not { } clipboard)
        {
            _session.SetStatus("클립보드에 접근할 수 없습니다 — 글을 끌어 골라 복사하세요.");
            return;
        }

        await clipboard.SetTextAsync(snippet);
        _session.SetStatus($"{snippet}를 복사했습니다 — 대사에 붙이면 그 자리에 {name}의 지금 값이 나옵니다.");
    }

    /// <summary>요약 칸에서 고른 것 — 연출 그래프 하이어아키의 그 줄을 누른 것과 같다.</summary>
    private void PickInDirector(HierarchyPick pick)
    {
        OnDirectorHierarchyPicked(pick);
        Graph.HierarchyView.Show(pick);
    }

    private void Note(string text) => _sheet.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 11,
        Opacity = 0.65,
        TextWrapping = TextWrapping.Wrap
    });

    /// <summary>
    /// 안내글 한 줄 — <see cref="Note"/>와 같은 모양이지만 설명 간소화를 따른다(<see cref="GuideLine"/> · brief-guides G2). 짧은 꼴이 없으면 간소화 때
    /// 숨는다(쓰는 법 풀이). 상태를 말하고 쓰는 법을 덧붙인 줄은 짧은 꼴에 상태만 둔다. 상태 · 경고만 말하는 줄은 <see cref="Note"/>다.
    /// </summary>
    private void Guide(string full, string? brief = null) => _sheet.Children.Add(new GuideLine
    {
        Full = full,
        Brief = brief,
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
        _sheet.Children.Add(button);
    }

    /// <summary>「챕터 그래프에서 열기」 — 챕터 그래프 탭으로 넘어가 그 인스펙터에 같은 줄을 세운다.</summary>
    private void OpenInChapterGraph(HierarchyPick pick, string text) => ChapterTabButton(text, () =>
    {
        MainTabs.SelectedItem = ChapterTabItem;
        ChapterGraph.Inspect(pick);
    });
}
