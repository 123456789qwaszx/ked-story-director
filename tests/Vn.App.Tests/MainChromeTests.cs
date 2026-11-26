using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// 화면과 소속의 일치 — <b>곁기둥은 제 화면 안에 산다</b>(2026-08-22): 챕터 목록은
/// 챕터 그래프에, 노드 편집기는 연출 그래프에. 탭마다 접었다 펴는 chrome은 이름이 겹치는
/// 내보내기 단추들만 남았다(챕터 툴바의 [내보내기]와 한 화면에 둘이면 헷갈린다).
/// ⛔ [무대 프리뷰] 탭 · 연출 노드 편집기 · 에셋 탐색기는 2026-10-29에 ked-story-compositor로 갔다.
///
/// 탭 순서가 작업 순서다: **[구성]이 첫 탭**(챕터 그래프 — 에피소드 · 분기 조건 · 스탯 · 선택지로 뼈대를 짠다)이고 그다음이
/// **[대본]**(연출 그래프 — 카드에 대사를 쓰고 선택지 · 분기를 잇는다)이다. 왼쪽에서 오른쪽으로 밟으면 챕터가 완성된다.
/// 창을 열면 [구성]이 서고, 새 프로젝트도 [구성]에서 시작한다.
/// </summary>
public sealed class MainChromeTests
{
    [Fact]
    public void 대본이_첫_탭이고_그다음이_구성이며_창을_열면_대본이_선다() => HeadlessUi.Run(() =>
    {
        // 탭 순서가 작업 순서다 — 먼저 [대본]에서 화를 만들고 쓰고 잇고, 그다음 [구성]에서 조건 · 스탯을 붙인다(2026-11-19 소유자 · usability UX23).
        // ⛔ [구성]이 첫째였다(이야기를 챕터 그래프에서 쓰던 때의 순서) — 작가는 켤 때마다 [대본]을 눌러야 했다.
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var tabs = window.FindControl<TabControl>("MainTabs")!;

        Assert.Equal("대본", ((TabItem)tabs.Items[0]!).Header);
        Assert.Equal("구성", ((TabItem)tabs.Items[1]!).Header);
        Assert.Equal(2, tabs.Items.Count);
        Assert.Same(window.FindControl<TabItem>("GraphTabItem"), tabs.SelectedItem);
        Assert.True(window.FindControl<GraphEditorView>("Graph")!.IsVisible);

        // 시험이 탭을 고르는 이름(MainTab)이 이 차례와 맞는다.
        Assert.Same(window.FindControl<TabItem>("GraphTabItem"), tabs.Items[MainTab.Script]);
        Assert.Same(window.FindControl<TabItem>("ChapterTabItem"), tabs.Items[MainTab.Structure]);

        window.Close();
    });

    [Fact]
    public void 탭을_바꿔도_두_판은_화면에_붙어_있고_고른_판만_보인다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 속도 개선 ②(usability UX14). ⛔ 판이 탭 안에 들어 있어, 탭을 바꾸면 안 보이는 판이 떨어졌다 다시 붙었다 —
        // [대본] 탭으로 돌아오는 데 카드 160장에서 1초가 넘었다.
        var window = new MainWindow { Width = 1400, Height = 820 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        TabControl tabs = window.FindControl<TabControl>("MainTabs")!;
        var chapter = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        var graph = window.FindControl<GraphEditorView>("Graph")!;
        int detached = 0;
        graph.DetachedFromVisualTree += (_, _) => detached++;
        chapter.DetachedFromVisualTree += (_, _) => detached++;

        Assert.Equal((false, true), (chapter.IsVisible, graph.IsVisible));   // 창을 열면 [대본]

        tabs.SelectedIndex = MainTab.Structure;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal((true, false), (chapter.IsVisible, graph.IsVisible));

        tabs.SelectedIndex = MainTab.Script;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal((false, true), (chapter.IsVisible, graph.IsVisible));

        Assert.Equal(0, detached);
        Assert.True(graph.IsAttachedToVisualTree() && chapter.IsAttachedToVisualTree());

        window.Close();
    });

    [Fact]
    public void File은_이웃_단추와_같은_모양이고_위_세_줄은_붙어_있다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「File은 흰 바탕에 흰 배경이라서 글자만 보입니다」 · 「줄이 너무 떨어져있는데 조금 붙여주십시오」(chrome-card-header U1).
        var window = new MainWindow { Width = 1400, Height = 820 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // File은 [⚙ 설정]과 같은 바탕이다(투명이 아니다) · 같은 높이다.
        MenuItem file = window.FindControl<MenuItem>("FileMenu")!;
        Button settings = window.FindControl<Button>("SettingsButton")!;
        var fileBrush = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(file.Background);
        var buttonBrush = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(settings.Background);
        Assert.NotEqual(0, fileBrush.Color.A);
        Assert.Equal(buttonBrush.Color, fileBrush.Color);
        Assert.Equal(settings.Bounds.Height, file.Bounds.Height, 1);

        // 이웃 단추가 글꼴 때문에 더 커져도(Windows의 ⚙ · 📂 — 이모지 글꼴의 줄 높이) File이 함께 따라간다(writer-app WA8 ·
        // ⛔ 메뉴가 32로 묶여 Windows에서만 File이 2px 낮았다).
        Button work = window.FindControl<Button>("WorkFolderButton")!;
        settings.MinHeight = file.Bounds.Height + 3;
        window.UpdateLayout();
        Assert.Equal(settings.Bounds.Height, file.Bounds.Height, 1);
        Assert.Equal(settings.Bounds.Height, work.Bounds.Height, 1);
        settings.ClearValue(Avalonia.Layout.Layoutable.MinHeightProperty);
        window.UpdateLayout();

        // 두 줄(위 띠 · 툴바) — 탭 머리는 위 띠의 File 옆에 선다(2026-11-26 — 디자인 점검 3번 · ⛔ 그 전에는 위 띠 · 탭 줄 · 툴바 세 줄이었다). 탭 머리는 34,
        // 툴바의 챕터 콤보는 창 위에서 70 안에 선다(⛔ 세 줄일 때 90 · 그 전에는 48 · 111).
        TabItem tab = window.FindControl<TabItem>("ChapterTabItem")!;
        Assert.Same(window.FindControl<Grid>("TopBar"), window.FindControl<TabControl>("MainTabs")!.Parent);
        Assert.Equal(18, tab.FontSize);
        Assert.True(tab.Bounds.Height <= 36, $"탭 머리 높이 {tab.Bounds.Height}");
        SelectTab(window, "ChapterTabItem");   // [구성] 판의 툴바를 잰다(창은 [대본]으로 열린다 — UX23)
        ComboBox combo = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<ComboBox>("ChapterCombo")!;
        double top = combo.TranslatePoint(new Avalonia.Point(0, 0), window)!.Value.Y;
        Assert.True(top <= 70, $"툴바가 창 위에서 {top}에 선다");

        // 판 안 오른쪽 기둥의 탭은 제 크기 그대로다(「>」 자식 고르기).
        Assert.Equal(12, window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<TabItem>("InspectorTabItem")!.FontSize);

        window.Close();
    });

    [Fact]
    public void 위_띠의_File_메뉴에_새_프로젝트_열기_저장이_있고_실행_취소_단추는_없다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 새 프로젝트 · 열기 · 저장은 File 메뉴(유니티처럼 눌러 목록에서 고른다), [실행 취소] · [다시 실행] 단추는
        // 걷는다(되돌리기는 Ctrl+Z · Ctrl+Y). 위 띠에 남는 단추는 작업 폴더(⛔ 11-18까지 「챕터진행」) · 설정이다(⛔ 게임 폴더는 같은 날 설정 창의 「내보내기」로 갔다).
        string folder = Path.Combine(Path.GetTempPath(), "vn-file-menu", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var window = new MainWindow();
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            MenuItem file = window.FindControl<MenuItem>("FileMenu")!;
            Assert.Equal("File", file.Header);
            // 되돌리기 · 다시 실행도 메뉴에 있다(2026-11-18 소유자 · finishing FX7 — 단추는 걷힌 그대로다).
            Assert.Equal(["새 프로젝트…", "열기…", "저장", "되돌리기", "다시 실행"], file.Items.OfType<MenuItem>().Select(item => item.Header as string));
            Assert.Equal("Ctrl+S", window.FindControl<MenuItem>("SaveMenuItem")!.InputGesture?.ToString());

            foreach (string gone in new[] { "NewButton", "OpenButton", "SaveButton", "UndoButton", "RedoButton" })
            {
                Assert.Null(window.FindControl<Button>(gone));
            }

            Assert.Equal(
                ["WorkFolderButton", "SettingsButton"],
                window.FindControl<Grid>("TopBar")!.Children.OfType<Button>().Select(button => button.Name));

            // 메뉴의 [저장]이 저장한다.
            window.CreateProjectIn(folder);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.SessionProbe.Editor.EnsureChapter("ch01");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(window.SessionProbe.IsDirty);

            window.FindControl<MenuItem>("SaveMenuItem")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(window.SessionProbe.IsDirty);

            window.Close();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });

    [Fact]
    public void 새_프로젝트는_구성_탭에_있다가_만들어도_대본에서_시작한다() => HeadlessUi.Run(() =>
    {
        // 탭 순서가 작업 순서다 — 화를 만들고 쓰는 [대본]이 첫걸음이다(usability UX23 · ⛔ [구성]에서 시작했다).
        string folder = Path.Combine(Path.GetTempPath(), "vn-new-project-tab", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var window = new MainWindow();
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            SelectTab(window, "ChapterTabItem");

            window.CreateProjectIn(folder);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.NotNull(window.SessionProbe.ProjectPath);
            Assert.Same(window.FindControl<TabItem>("GraphTabItem"), window.FindControl<TabControl>("MainTabs")!.SelectedItem);

            window.Close();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });

    [Fact]
    public void 상태줄은_곁눈으로_읽는_크기다() => HeadlessUi.Run(() =>
    {
        // 2026-08-24 소유자 — "저장했습니다, 로드했습니다 이런 문구의 크기를 줄여줘."
        // 기본 글자크기(14)로 두면 창 아래를 늘 크게 차지하는데, 여기 뜨는 말은 대부분
        // "됐다"이고 그것은 크게 볼 것이 아니다. 이 저장소가 곁정보에 쓰는 11로 맞춘다.
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(11, window.FindControl<TextBlock>("StatusText")!.FontSize);

        window.Close();
    });

    /// <summary>지금 화면에 <b>실제로 선</b> 컨트롤들 — 탭이 안 고른 판은 나무에 없다.</summary>
    private static T[] Live<T>(MainWindow window) where T : Control =>
        window.GetVisualDescendants().OfType<T>().ToArray();

    /// <summary>
    /// 탭을 <b>이름으로</b> 고른다. ⚠ 인덱스로 고르면 탭이 하나 늘 때마다 이 클래스가
    /// 통째로 낡는다 — 2026-09-16에 [대본] 탭이 서면서 실제로 그랬다.
    /// </summary>
    private static void SelectTab(MainWindow window, string tabName)
    {
        window.FindControl<TabControl>("MainTabs")!.SelectedItem =
            window.FindControl<TabItem>(tabName)!;

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void 위_띠에는_진행_JSON_폴더만_있고_Yarn_CSV_내보내기는_없다() => HeadlessUi.Run(() =>
    {
        // 이 툴이 내는 것은 챕터 진행 JSON과 대사 Yarn이고, 그것은 저절로 나가므로 꺼내 가는 단추만 있다.
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("📂 작업 폴더", window.FindControl<Button>("WorkFolderButton")!.Content);
        Assert.Null(window.FindControl<Button>("ExportButton"));
        Assert.Null(window.FindControl<Button>("CsvExportButton"));
        Assert.Null(window.FindControl<Button>("ExportFormatsButton"));

        window.Close();
    });

    [Fact]
    public void 편집_자료_현황판은_사라졌다() => HeadlessUi.Run(() =>
    {
        // 2026-08-21 소유자 — "이제 편집자료는 굳이 표시할 필요가 없는 것 같아".
        // 대본·발행 결과·연출 공급을 나열하던 현황판인데, 발행·배선이 자동이 된 뒤로는
        // 볼 이유가 없다.
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(window.FindControl<ScrollViewer>("ResourceScroll"));
        Assert.Null(window.FindControl<ToggleButton>("ResourceCollapseToggle"));

        window.Close();
    });

    [Fact]
    public void 곁기둥은_연출_그래프_안에만_선다() => HeadlessUi.Run(() =>
    {
        // 2026-08-22 소유자 — 대사편집·scriptPreview를 연출 그래프 안으로 넣어 "오직
        // 연출그래프에서만" 보이게. 자리가 곧 소속이라 접었다 펴는 chrome 코드가 사라졌다.
        // (⛔ 연출 노드 편집기 · 에셋 탐색기는 2026-10-29에 무대와 함께 compositor로 갔다.)
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 두 판은 늘 화면 나무에 붙어 있다(UX14) — 곁기둥은 연출 그래프 안에 하나만 서고, [구성]을 보는 동안에는 보이지 않는다.
        SelectTab(window, "ChapterTabItem"); // 챕터 그래프 — 안 보인다
        DialogueNodeEditor editor = Assert.Single(Live<DialogueNodeEditor>(window));
        Assert.False(editor.IsEffectivelyVisible);

        SelectTab(window, "GraphTabItem");
        Assert.Same(editor, Assert.Single(Live<DialogueNodeEditor>(window)));
        Assert.Contains(window.FindControl<GraphEditorView>("Graph")!, editor.GetVisualAncestors());
        Assert.DoesNotContain(window.FindControl<ChapterGraphView>("ChapterGraph")!, editor.GetVisualAncestors());

        window.Close();
    });

    [Fact]
    public void 챕터_목록은_맨_왼쪽_열을_떠나_챕터_그래프_안으로_갔다() => HeadlessUi.Run(() =>
    {
        // 2026-08-22 소유자 — 챕터를 고르는 일과 그 챕터를 편집하는 일이 한 기둥에 선다.
        // 창은 이제 탭 하나만 든다(곁기둥 둘이 제 화면 안으로 들어갔다).
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(window.FindControl<Grid>("CenterColumns"));

        // 옛 왼쪽 열의 이름들은 창에서 사라졌다 — 남아 있으면 두 자리에 같은 목록이 산다.
        Assert.Null(window.FindControl<StackPanel>("FileListPanel"));
        Assert.Null(window.FindControl<Button>("AddFileButton"));

        // 새 자리는 챕터 그래프 뷰 안이고, 셸이 그 손잡이로 하이어아키(2026-10-30 — 챕터 목록을 이었다)를 짓는다. 하이어아키는 보이는 탭의
        // 판에 붙는다 — [구성]을 보고 본다(창은 [대본]으로 열린다 — UX23).
        SelectTab(window, "ChapterTabItem");
        var chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Contains(chapterGraph, chapterGraph.HierarchyView.GetVisualAncestors());
        // 새 챕터 [＋]는 어디에도 없다 — [대본] 탭 하이어아키 우클릭 「챕터 추가」가 바로 세운다(ChapterAddTests).
        Assert.Null(chapterGraph.FindControl<Button>("AddChapterButton"));

        window.Close();
    });

    /// <summary>
    /// 하이어아키는 판 <b>왼쪽 기둥</b>에 선다 (2026-10-30 소유자: *"하이어아키를 그래프 왼쪽 Column으로"*) — 유니티처럼
    /// 하이어아키 · 판 · 인스펙터가 왼쪽에서 오른쪽으로. 두 그래프가 같다. 하이어아키 폭은 사람이 끄는 절대 폭이고 하한이
    /// 있다(0까지 접히면 다시 잡을 손잡이가 없다).
    ///
    /// (⛔ 전에 이 자리를 지키던 「챕터 목록 스플리터 아래는 별 행이다」(2026-08-22)는 하이어아키가 위 · 인스펙터가 아래로
    /// 서던 때의 것이라 같은 날 걷혔다.)
    /// </summary>
    [Fact]
    public void 하이어아키는_두_그래프_모두_판_왼쪽_기둥에_선다() => HeadlessUi.Run(() =>
    {
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        AssertLeftOfGraph(window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView,
            window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<ScrollViewer>("GraphScroll")!);
        AssertLeftOfGraph(window.FindControl<GraphEditorView>("Graph")!.HierarchyView,
            window.FindControl<GraphEditorView>("Graph")!.FindControl<ScrollViewer>("GraphScroll")!);

        window.Close();
    });

    /// <summary>하이어아키가 든 칸이 그 격자의 0열이고, 판은 그 오른쪽(2열)이며, 0열은 하한 있는 절대 폭이다.</summary>
    private static void AssertLeftOfGraph(StoryHierarchy hierarchy, ScrollViewer graphScroll)
    {
        Control column = hierarchy.GetVisualAncestors().OfType<Control>()
            .First(ancestor => ancestor.GetVisualParent() is Grid { ColumnDefinitions.Count: 5 });
        var grid = (Grid)column.GetVisualParent()!;

        Assert.Equal(0, Grid.GetColumn(column));
        Assert.True(grid.ColumnDefinitions[0].Width.IsAbsolute);
        Assert.True(grid.ColumnDefinitions[0].MinWidth > 0);
        Assert.Equal(GridResizeDirection.Columns,
            grid.Children.OfType<GridSplitter>().Single(splitter => Grid.GetColumn(splitter) == 1).ResizeDirection);

        Control graph = graphScroll.GetSelfAndVisualAncestors().OfType<Control>()
            .First(ancestor => ancestor.GetVisualParent() == grid);
        Assert.Equal(2, Grid.GetColumn(graph));
    }
}
