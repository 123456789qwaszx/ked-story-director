using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 인스펙터의 글 칸은 [대본]과 같은 규칙이다</b> — Enter 넣기 · 다른 곳 누르기 넣기 · Esc 넣기 전 값으로(2026-11-18 소유자 ·
/// <c>docs/work-orders/inspector-boxes-pan-orders.md</c> J1). ⛔ 그 전에는 ID 칸이 Enter로만 바뀌었고 Esc 되돌리기가 없었다.
///
/// 못 박는 것: ① ID 칸은 다른 곳을 누르면 바뀐다 ② Esc는 지금 값으로 돌린다 ③ 판에서 다른 것을 고르기 전에 적던 ID가 원래 에피소드에
/// 들어간다 — 같은 에피소드를 다시 누르면 새 ID로 고른다 ④ 받지 않으면 칸이 지금 값으로 돌아온다 ⑤ 챕터ID 칸 · 조건 식 칸도 같다.
/// </summary>
public sealed class ChapterInspectorBoxesTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-inspector-boxes", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 에피소드ID_칸은_다른_곳을_누르면_바뀌고_Esc는_지금_값으로_돌린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, ChapterGraphView view) = Show();
        view.SelectEpisode("a");
        TextBox id = view.FindControl<TextBox>("IdBox")!;

        // Esc — 적던 것을 걷는다. 바뀌지 않는다.
        Type(window, id, "a2");
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("a", id.Text);
        Assert.Same(id, window.FocusManager?.GetFocusedElement());   // 초점은 그대로다
        Assert.NotNull(Episode(session, "a"));

        // 다른 곳을 누른다(하이어아키) — 바뀐다.
        Type(window, id, "a2");
        long before = session.Editor.Revision;
        view.HierarchyView.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Episode(session, "a2"));
        Assert.Null(Episode(session, "a"));
        Assert.Equal(before + 1, session.Editor.Revision);

        window.Close();
    });

    [Fact]
    public void 판에서_다른_것을_고르기_전에_적던_ID가_원래_에피소드에_들어간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, ChapterGraphView view) = Show();
        view.SelectEpisode("a");
        TextBox id = view.FindControl<TextBox>("IdBox")!;

        // 판의 카드는 초점을 가져가지 않는다 — 고르기가 먼저 낸다. 적던 ID는 b가 아니라 a에 들어간다.
        Type(window, id, "a3");
        view.SelectEpisode("b");
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Episode(session, "a3"));
        Assert.NotNull(Episode(session, "b"));
        Assert.Equal("b", id.Text);

        // 같은 에피소드를 다시 누르면 새 ID로 고른다.
        Type(window, id, "b2");
        view.SelectEpisode("b");
        Dispatcher.UIThread.RunJobs();
        Assert.Null(Episode(session, "b"));
        Assert.Equal("b2", id.Text);
        Assert.True(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);

        // 받지 않으면(같은 ID가 있다) 칸이 지금 값으로 돌아오고 까닭을 말한다.
        Type(window, id, "root");
        view.HierarchyView.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("b2", id.Text);
        Assert.NotNull(Episode(session, "b2"));
        Assert.False(string.IsNullOrEmpty(session.StatusMessage));

        window.Close();
    });

    [Fact]
    public void 챕터ID_칸과_조건_식_칸도_다른_곳을_누르면_넣고_Esc는_돌린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, ChapterGraphView view) = Show();
        session.Editor.AddChapterCondition("ch01", "친해짐", "trust >= 3");
        Dispatcher.UIThread.RunJobs();

        // 챕터ID — 인스펙터 탭의 챕터 보기.
        view.Inspect(new HierarchyPick(HierarchyKind.Chapter, "ch01"));
        view.ShowSideTab(SideTab.Inspector);
        Dispatcher.UIThread.RunJobs();
        TextBox chapterId = view.FindControl<TextBox>("ChapterIdBox")!;
        Type(window, chapterId, "ch09");
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ch01", chapterId.Text);

        Type(window, chapterId, "ch09");
        view.HierarchyView.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(session.Editor.FindChapter("ch09"));
        Assert.Null(session.Editor.FindChapter("ch01"));

        // 조건 식 — 「조건 및 스탯」 탭. Esc는 지금 식으로 돌리고, 다른 곳을 누르면 넣는다.
        view.ShowSideTab(SideTab.Conditions);
        Dispatcher.UIThread.RunJobs();
        TextBox expression = view.FindControl<StackPanel>("ConditionListPanel")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(box => (string?)box.Tag == "condition-expression:친해짐");
        Type(window, expression, "trust >= 9");
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("trust >= 3", expression.Text);
        Assert.Equal("trust >= 3", session.Editor.FindChapter("ch09")!.Conditions.Single().Expression);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, ChapterGraphView View) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        return (window, session, window.FindControl<ChapterGraphView>("ChapterGraph")!);
    }

    private static Vn.Authoring.Chapters.ChapterEpisode? Episode(AuthoringSession session, string episodeId) =>
        session.Editor.FindChapter("ch01")!.Episodes.FirstOrDefault(episode => episode.EpisodeId == episodeId);

    /// <summary>그 칸에 초점을 주고 글을 적는다(넣지는 않는다).</summary>
    private static void Type(MainWindow window, TextBox box, string text)
    {
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Same(box, window.FocusManager?.GetFocusedElement());
        box.Text = text;
    }
}
