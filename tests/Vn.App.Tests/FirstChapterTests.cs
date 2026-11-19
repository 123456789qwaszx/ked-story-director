using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>챕터가 없으면 판 한가운데에 [＋ 첫 챕터 만들기]가 선다</b>(2026-11-18 소유자 — 「구성에서는 대본탭으로 보내기 + 챕터 생성하는 버튼을
/// 중앙에 두시고, 대본탭에서도 중앙에 챕터를 생성하는 버튼을」 · <c>docs/work-orders/first-chapter-orders.md</c>). 누르면 하이어아키 「챕터 추가」와
/// 같다 — 「새 챕터」가 장면 · 에피소드와 함께 서고 골라진다. [구성]의 단추는 [대본]으로 데려가 거기서 만든다.
/// </summary>
public sealed class FirstChapterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-first-chapter", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 대본_빈_판의_단추가_첫_챕터를_세우고_사라진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = OpenEmpty(tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        StackPanel empty = graph.FindControl<StackPanel>("EmptyBoardPanel")!;
        Assert.True(empty.IsVisible);
        Assert.False(graph.FindControl<StackPanel>("EmptyProjectButtons")!.IsVisible);   // 프로젝트는 있다 — [열기…] · [새 프로젝트…]는 없다
        Assert.False(graph.FindControl<Button>("AddEpisodeButton")!.IsEnabled);   // [＋ 에피소드]는 꺼져 있다 — 이 단추가 첫걸음이다
        long before = session.Editor.Revision;

        Click(graph.FindControl<Button>("FirstChapterButton")!);

        // 하이어아키 「챕터 추가」와 같다 — 한 되돌리기 칸에 챕터 · 장면 · 에피소드.
        ChapterDocument chapter = Assert.Single(session.Project.Chapters);
        Assert.Equal(NewNames.Chapter, chapter.DisplayName);
        Assert.Single(chapter.Episodes);
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Equal(new HierarchyPick(HierarchyKind.Chapter, chapter.ChapterId), graph.HierarchyView.Selection);
        Assert.False(empty.IsVisible);

        // 되돌리면 다시 선다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(session.Project.Chapters);
        Assert.True(empty.IsVisible);

        window.Close();
    });

    [Fact]
    public void 구성_빈_판의_단추는_대본_탭으로_데려가_첫_챕터를_세운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = OpenEmpty(tab: MainTab.Structure);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Button first = view.FindControl<Button>("EmptyFirstChapterButton")!;
        Assert.True(first.IsVisible);
        Assert.False(view.ProjectNewCenterButton.IsVisible);   // 프로젝트는 이미 있다
        Assert.Equal("프로젝트", view.FindControl<TextBlock>("InspectorTitle")!.Text);   // 챕터가 없으면 인스펙터는 프로젝트

        Click(first);

        Assert.Same(window.FindControl<TabItem>("GraphTabItem"), window.FindControl<TabControl>("MainTabs")!.SelectedItem);
        ChapterDocument chapter = Assert.Single(session.Project.Chapters);
        Assert.Single(chapter.Episodes);
        Assert.Equal(new HierarchyPick(HierarchyKind.Chapter, chapter.ChapterId),
            window.FindControl<GraphEditorView>("Graph")!.HierarchyView.Selection);

        // [구성]으로 돌아오면 판이 서 있다 — 가려진 동안의 그리기는 탭이 보일 때 한다(finishing FX3).
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Structure;
        Dispatcher.UIThread.RunJobs();
        Assert.False(first.IsVisible);

        window.Close();
    });

    [Fact]
    public void 챕터가_모두_없어지면_구성_인스펙터에_사라진_챕터가_남지_않는다() => HeadlessUi.Run(() =>
    {
        // ⛔ 2026-11-18까지는 판을 그릴 챕터가 없으면 그리기가 일찍 돌아 인스펙터를 다시 세우지 않았다 — 지운 챕터의 이름 · 챕터ID가 남았다(소유자 그림).
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(Path.Combine(_directory, "two"), tab: MainTab.Structure);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.Inspect(new HierarchyPick(HierarchyKind.Chapter, "ch01"));
        view.ShowSideTab(SideTab.Inspector);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("챕터 · ", view.FindControl<TextBlock>("InspectorTitle")!.Text, StringComparison.Ordinal);

        session.Editor.RemoveChapterWithBoard("ch01");
        session.Editor.RemoveChapterWithBoard("ch02");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("프로젝트", view.FindControl<TextBlock>("InspectorTitle")!.Text);
        Assert.False(view.FindControl<Grid>("ChapterIdGrid")!.IsVisible);
        Assert.True(view.FindControl<Button>("EmptyFirstChapterButton")!.IsVisible);

        window.Close();
    });

    [Fact]
    public void 처음_켜서_프로젝트가_없으면_대본_빈_판에_열기와_새_프로젝트가_선다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「처음에 프로젝트 자체도 없을 때 대본탭에서 뭘 생성시켜 준다고는 하는데 아무 일도 안 일어나니까 헷갈릴 것 같아.
        // 아래쪽 알림에서 프로젝트 생성부터 하라고 안내는 해 주는데 그거 확인하기가 힘들 것 같아」(usability UX35).
        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AuthoringSession session = window.SessionProbe;
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Assert.Null(session.ProjectPath);

        Assert.True(graph.FindControl<StackPanel>("EmptyBoardPanel")!.IsVisible);
        Assert.StartsWith("아직 프로젝트가 없습니다.", graph.FindControl<TextBlock>("EmptyBoardText")!.Text, StringComparison.Ordinal);
        Assert.False(graph.FindControl<Button>("FirstChapterButton")!.IsVisible);   // 눌러도 만들 수 없는 단추는 서지 않는다
        Assert.True(graph.ProjectOpenCenterButton.IsEffectivelyVisible);
        Assert.True(graph.ProjectNewCenterButton.IsEffectivelyVisible);
        Assert.Equal(AuthoringSession.NoProjectStatus, session.StatusMessage);
        Assert.Contains(HierarchyTexts(graph), text => text.StartsWith("프로젝트를 열거나 만들면", StringComparison.Ordinal));

        // 두 단추는 File 메뉴와 같은 길이다 — 시험 자리에는 폴더 고르는 창이 없어 그 까닭을 말한다(곧 단추가 그 길에 걸려 있다).
        Click(graph.ProjectOpenCenterButton);
        Assert.Contains("폴더 선택 창", session.StatusMessage, StringComparison.Ordinal);
        session.SetStatus(AuthoringSession.NoProjectStatus);
        Click(graph.ProjectNewCenterButton);
        Assert.Contains("폴더 선택 창", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 새_프로젝트를_만들면_빈_판이_첫_챕터_단추로_바뀐다() => HeadlessUi.Run(() =>
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AuthoringSession session = window.SessionProbe;
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Directory.CreateDirectory(_directory);

        window.CreateProjectIn(_directory);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(session.ProjectPath);
        Assert.Equal("아직 챕터가 없습니다.", graph.FindControl<TextBlock>("EmptyBoardText")!.Text);
        Assert.True(graph.FindControl<Button>("FirstChapterButton")!.IsEffectivelyVisible);
        Assert.False(graph.FindControl<StackPanel>("EmptyProjectButtons")!.IsVisible);
        Assert.Contains("[＋ 첫 챕터 만들기]", session.StatusMessage, StringComparison.Ordinal);
        Assert.Contains(HierarchyTexts(graph), text => text.StartsWith("챕터가 없습니다.", StringComparison.Ordinal));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>챕터 없는 프로젝트를 저장하고 창을 띄워 연다.</summary>
    private (MainWindow Window, AuthoringSession Session) OpenEmpty(int tab)
    {
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "빈 프로젝트" });

        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = tab;
        Dispatcher.UIThread.RunJobs();
        return (window, session);
    }

    /// <summary>하이어아키에 선 글들 — 빈 목록의 안내를 본다.</summary>
    private static IEnumerable<string> HierarchyTexts(GraphEditorView graph) =>
        graph.HierarchyView.FindControl<StackPanel>("RowHost")!.Children.OfType<TextBlock>().Select(block => block.Text ?? string.Empty);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
