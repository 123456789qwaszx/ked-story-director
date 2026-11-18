using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>두 판이 얼마나 일하는가</b> (2026-11-18 소유자 — 「수십 수백개의 에피소드들을 한번에 잡고 옮길 때 렉은 없을지. 또 텍스트를 쓰고 그거
/// 인스펙터 창에 반영되거나 하는 지연을 조금 더 줄이는」 · <c>docs/work-orders/finishing-orders.md</c> FX3). 시간은 기계마다 달라 고정할 수
/// 없으므로 <see cref="ChapterGraphWorkAmountTests"/>처럼 <b>일의 횟수</b>를 건다.
///
/// 못 박는 것: ① 카드 글 칸의 글을 넣어도 [대본] 판의 철도를 다시 긋지 않는다(⛔ [구성]이 다시 읽을 때마다 한 번 더 그었다) ② 가려진
/// [구성] 판은 바뀔 때마다 그리지 않고 보일 때 한 번 그린다.
/// </summary>
public sealed class BoardWorkAmountTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-board-work", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 글을_넣어도_대본_판의_철도는_다시_긋지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string cardId = EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id;

        TextBox Box()
        {
            TextBox box = graph.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
                .Single(item => item.Tag as string == GraphEditorView.WritingTag + cardId);
            box.Focus();
            Dispatcher.UIThread.RunJobs();
            return box;
        }

        // 첫 줄은 줄이 느는 것이라 판이 다시 선다 — 쓰는 중의 일은 있는 줄을 고치는 것이다.
        TextBox box = Box();
        box.Text = "오늘은 바다에 가자.";
        graph.CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();

        box = Box();
        int rails = graph.RailDrawCount;
        long revision = session.Editor.Revision;
        box.Text = "오늘은 산에 가자.";
        graph.CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(revision + 1, session.Editor.Revision);   // 글은 들어갔다
        Assert.Equal(rails, graph.RailDrawCount);

        window.Close();
    });

    [Fact]
    public void 가려진_구성_판은_보일_때_한_번_그린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        TabControl tabs = window.FindControl<TabControl>("MainTabs")!;
        session.SetShowIds(false);   // [구성] 카드가 제목을 보인다
        Dispatcher.UIThread.RunJobs();

        int draws = view.CanvasDrawCount;
        session.Editor.UpdateEpisode("ch01", "a", title: "첫째");
        Dispatcher.UIThread.RunJobs();
        session.Editor.UpdateEpisode("ch01", "a", title: "둘째");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(draws, view.CanvasDrawCount);   // 가려진 동안은 그리지 않는다

        tabs.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(draws + 1, view.CanvasDrawCount);   // 보이면 한 번 — 마지막 모습으로
        Assert.Contains(view.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Text == "둘째");

        // 바뀐 것이 없으면 탭을 오가도 다시 그리지 않는다.
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        tabs.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(draws + 1, view.CanvasDrawCount);

        window.Close();
    });
}
