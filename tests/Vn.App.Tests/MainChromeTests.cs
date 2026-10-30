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
/// 탭 순서·이름은 2026-08-18 팀장 미팅에서 정해졌다: **챕터 그래프가 첫 탭**이고 그다음이
/// **연출 그래프**(옛 "시나리오 그래프")다. 왼쪽에서 오른쪽으로 밟으면 챕터가 완성된다.
/// </summary>
public sealed class MainChromeTests
{
    [Fact]
    public void 챕터_그래프가_첫_탭이고_그다음이_연출_그래프다() => HeadlessUi.Run(() =>
    {
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var tabs = window.FindControl<TabControl>("MainTabs")!;

        Assert.Equal("챕터 그래프", ((TabItem)tabs.Items[0]!).Header);
        Assert.Equal("연출 그래프", ((TabItem)tabs.Items[1]!).Header);
        Assert.Equal(2, tabs.Items.Count);   // [무대 프리뷰]는 compositor로 갔다(2026-10-29)

        window.Close();
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
        // 2026-10-29 — 게임용 Yarn과 문서(CSV)는 ked-story-compositor만 낸다(한 파일에는 내는 쪽이
        // 하나다). 이 툴이 내는 것은 챕터 진행 JSON이고, 그것은 저절로 나가므로 꺼내 가는 단추만 있다.
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(window.FindControl<Button>("OpenExportFolderButton"));
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

        SelectTab(window, "ChapterTabItem"); // 챕터 그래프 — 없다
        Assert.Empty(Live<DialogueNodeEditor>(window));

        SelectTab(window, "GraphTabItem");
        Assert.Single(Live<DialogueNodeEditor>(window));

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

        // 새 자리는 챕터 그래프 뷰 안이고, 셸이 그 손잡이로 하이어아키(2026-10-30 — 챕터 목록을 이었다)를 짓는다.
        var chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Contains(chapterGraph, chapterGraph.HierarchyView.GetVisualAncestors());
        Assert.Contains(chapterGraph, chapterGraph.ChapterAddButton.GetVisualAncestors());

        window.Close();
    });

    /// <summary>
    /// 2026-08-22 소유자 보고 — 챕터 목록 스플리터가 "위로만 움직이고 아래쪽으로는
    /// 동작을 안 하는" 결함. 스플리터 <b>바로 아래가 잠금 배너(Auto)</b>였던 것이 정체다:
    /// 아래로 끌려면 그 행을 줄여야 하는데 Auto는 이미 내용 높이라 줄 것이 없었고,
    /// 위로 끌 때만 그 행이 늘며 움직였다. 배너와 탭을 안쪽 격자로 묶어 스플리터의
    /// 상대를 <b>별(*) 행</b>으로 만들었다.
    /// </summary>
    [Fact]
    public void 챕터_목록_스플리터_아래는_별_행이다() => HeadlessUi.Run(() =>
    {
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        GridSplitter splitter = Assert.Single(
            chapterGraph.GetVisualDescendants().OfType<GridSplitter>(),
            item => item.ResizeDirection == GridResizeDirection.Rows);

        var grid = (Grid)splitter.GetVisualParent()!;
        int row = Grid.GetRow(splitter);

        // 위: 사람이 끄는 절대 높이. 0까지 접히면 다시 잡을 손잡이가 없으므로 하한이 있다.
        Assert.True(grid.RowDefinitions[row - 1].Height.IsAbsolute);
        Assert.True(grid.RowDefinitions[row - 1].MinHeight > 0);

        // 아래: ⚠ 여기가 Auto면 아래로 끌 때 줄일 것이 없어 스플리터가 한쪽으로만 움직인다.
        Assert.True(
            grid.RowDefinitions[row + 1].Height.IsStar,
            "스플리터 아래는 별 행이어야 위아래로 다 끌린다");
        Assert.Equal(row + 2, grid.RowDefinitions.Count);

        window.Close();
    });
}
