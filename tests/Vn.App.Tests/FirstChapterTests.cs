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
        (MainWindow window, AuthoringSession session) = OpenEmpty(tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        StackPanel empty = graph.FindControl<StackPanel>("EmptyBoardPanel")!;
        Assert.True(empty.IsVisible);
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

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
