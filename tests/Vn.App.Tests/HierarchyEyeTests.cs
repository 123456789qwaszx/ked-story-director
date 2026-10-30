using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Graph;

namespace Vn.App.Tests;

/// <summary>
/// <b>하이어아키의 눈</b> (2026-10-30 SF2 — <c>docs/plans/hierarchy.md</c> §3). 소유자: *"하이어 아키에서, 장면, 챕터 단위로 접는
/// 옵션을 추가해줘. 유니티의 하이어아키에서 보이게 하고 안보이게 하는거랑 비슷하게"*.
///
/// 연출 그래프 하이어아키의 챕터 · 장면 줄 맨 왼쪽에 눈이 선다. 챕터 눈은 판째 접고(지금의 판 상자), 장면 눈은 그 장면만 줄 목록
/// 상자로 간소화한다. 챕터 그래프에는 접을 판이 없어 눈 기둥이 없다. 하이어아키에서 고르는 것은 접힘을 풀지 않는다.
/// </summary>
public sealed class HierarchyEyeTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-eye", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 연출_그래프_하이어아키의_챕터와_장면_줄에만_눈이_서고_챕터_그래프에는_눈_기둥이_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, _) = Show();
        StoryHierarchy tree = Tree(window);
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(HierarchyEye.None, Row(tree, StoryHierarchy.ProjectKey).Eye);
        Assert.Equal(HierarchyEye.Open, Row(tree, StoryHierarchy.ChapterKey("ch01")).Eye);
        Assert.Equal(HierarchyEye.Open, Row(tree, StoryHierarchy.ChapterKey("ch02")).Eye);
        Assert.Equal(HierarchyEye.Open, Row(tree, StoryHierarchy.SceneKey("ch01", "hall")).Eye);
        Assert.NotNull(tree.EyeButton(StoryHierarchy.SceneKey("ch01", "hall")));

        // 챕터 그래프 — 같은 그루지만 눈 기둥이 없다.
        StoryHierarchy chapterTree = window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;
        Assert.All(chapterTree.Rows, row => Assert.Equal(HierarchyEye.None, row.Eye));
        Assert.Null(chapterTree.EyeButton(StoryHierarchy.ChapterKey("ch01")));

        window.Close();
    });

    [Fact]
    public void 장면의_눈을_누르면_그_장면만_간소화되고_눈이_감긴다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Show();
        StoryHierarchy tree = Tree(window);
        string board = Board(session, "ch01");
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        Dispatcher.UIThread.RunJobs();

        PressEye(tree, StoryHierarchy.SceneKey("ch01", "hall"));

        Assert.Contains(new GraphSceneKey(board, "hall"), session.CollapsedScenes);
        Assert.Contains(board, session.ExpandedFileIds);   // 판은 펼친 채다
        Assert.Contains(BoardCanvas(window).Children.OfType<Border>(), border =>
            border.Tag as string == GraphEditorView.SceneProxyTag + board + "/hall");
        Assert.Equal(HierarchyEye.Folded, Row(tree, StoryHierarchy.SceneKey("ch01", "hall")).Eye);
        Assert.Equal(HierarchyEye.Open, Row(tree, StoryHierarchy.SceneKey("ch01", "opening")).Eye);

        // 다시 누르면 편다.
        PressEye(tree, StoryHierarchy.SceneKey("ch01", "hall"));

        Assert.Empty(session.CollapsedScenes);
        Assert.Equal(HierarchyEye.Open, Row(tree, StoryHierarchy.SceneKey("ch01", "hall")).Eye);

        window.Close();
    });

    [Fact]
    public void 챕터의_눈은_판째_접고_가려진_장면의_눈을_누르면_챕터를_펴고_그_장면을_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Show();
        StoryHierarchy tree = Tree(window);
        string board = Board(session, "ch01");
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        Dispatcher.UIThread.RunJobs();

        // 장면 hall을 접어 둔 채 챕터를 접는다 — 지금의 판 상자(Tag = 판 Id)가 선다.
        PressEye(tree, StoryHierarchy.SceneKey("ch01", "hall"));
        PressEye(tree, StoryHierarchy.ChapterKey("ch01"));

        Assert.DoesNotContain(board, session.ExpandedFileIds);
        Assert.Contains(BoardCanvas(window).Children.OfType<Border>(), border => border.Tag as string == board);
        Assert.Equal(HierarchyEye.Folded, Row(tree, StoryHierarchy.ChapterKey("ch01")).Eye);
        Assert.Equal(HierarchyEye.UnderFolded, Row(tree, StoryHierarchy.SceneKey("ch01", "hall")).Eye);
        Assert.Equal(HierarchyEye.UnderFolded, Row(tree, StoryHierarchy.SceneKey("ch01", "opening")).Eye);

        // 가려진 hall의 눈 — 보이라고 누른 것이다: 챕터가 펴지고 hall도 펴진다.
        PressEye(tree, StoryHierarchy.SceneKey("ch01", "hall"));

        Assert.Contains(board, session.ExpandedFileIds);
        Assert.Empty(session.CollapsedScenes);
        Assert.Equal(HierarchyEye.Open, Row(tree, StoryHierarchy.SceneKey("ch01", "hall")).Eye);

        window.Close();
    });

    [Fact]
    public void 하이어아키에서_고르는_것은_접힘을_풀지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Show();
        StoryHierarchy tree = Tree(window);
        string board = Board(session, "ch01");
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "hall"), collapse: false);
        Dispatcher.UIThread.RunJobs();

        PressEye(tree, StoryHierarchy.ChapterKey("ch01"));

        // 챕터 줄을 골라도, 그 안의 에피소드를 골라도 판은 접힌 채다 — 보이기는 눈이 정한다(유니티처럼).
        Click(tree, StoryHierarchy.ChapterKey("ch01"));
        Assert.DoesNotContain(board, session.ExpandedFileIds);

        Click(tree, StoryHierarchy.EpisodeKey("ch01", "b"));
        Assert.Equal(EpisodeNaming.CardFor(session.Project, "ch01", "b")!.Id, session.SelectedNodeId);
        Assert.DoesNotContain(board, session.ExpandedFileIds);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session) Show() => HierarchyFixture.Open(_directory, GraphTab);

    private static StoryHierarchy Tree(MainWindow window) => window.FindControl<GraphEditorView>("Graph")!.HierarchyView;

    private static Canvas BoardCanvas(MainWindow window) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!;

    private static string Board(AuthoringSession session, string chapterId) =>
        EpisodeNaming.BoardOf(session.Project, chapterId)!.Id;

    private static HierarchyRow Row(StoryHierarchy tree, string key) =>
        tree.Rows.Single(row => string.Equals(row.Key, key, StringComparison.Ordinal));

    /// <summary>눈을 누른다 — 셸이 판을 바꾸는 일은 다음 차례로 미뤄지므로(OnFileGraphStateChanged) 그것까지 돌린다.</summary>
    private static void PressEye(StoryHierarchy tree, string key)
    {
        Border eye = tree.EyeButton(key) ?? throw new Xunit.Sdk.XunitException($"{key}의 눈이 없습니다");
        eye.RaiseEvent(new PointerPressedEventArgs(
            eye, new Pointer(0, PointerType.Mouse, true), eye, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(StoryHierarchy tree, string key)
    {
        tree.RowButton(key)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
