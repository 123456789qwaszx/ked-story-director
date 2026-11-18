using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// [대본] 탭 — 챕터를 고른 채 「인스펙터」 탭으로 가면 <b>그 챕터에 쓴 글자 수</b>가 선다(2026-11-18 소유자 — 「자기의 작업량을 알 수 있으니까요」 ·
/// <c>docs/work-orders/writer-comfort-orders.md</c> WC7). 고르면 「조건 및 스탯」 탭이 열리는 것은 그대로다(chapter-pick Y1).
/// </summary>
public sealed class DirectorChapterTextAmountTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-text-amount", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 챕터를_고르고_인스펙터_탭으로_가면_쓴_글자_수가_서고_글을_쓰면_따라간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", "a")!;
        session.Editor.SetScriptLineText(card.Id, card.Script.ActiveLines.First().Id, string.Empty, "오늘은 바다에 가자.");
        Dispatcher.UIThread.RunJobs();

        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(SideTab.Conditions, graph.ShownSideTab);   // 고르면 「조건 및 스탯」 — 그대로다

        graph.ShowSideTab(SideTab.Inspector);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("11자", Text(graph, "chapter-characters"));
        Assert.Equal("공백 제외 9자 · 1줄", Text(graph, "chapter-characters-detail"));

        // 글을 더 쓰면 요약이 다시 서며 따라간다.
        session.Editor.SetScriptLineText(card.Id, session.Editor.InsertScriptLine(card.Id).Id, string.Empty, "좋아.");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("14자", Text(graph, "chapter-characters"));
        Assert.Equal("공백 제외 12자 · 2줄", Text(graph, "chapter-characters-detail"));

        window.Close();
    });

    private static string? Text(GraphEditorView graph, string tag) =>
        graph.ShownSideHost.GetLogicalDescendants().OfType<TextBlock>()
            .Single(block => block.IsEffectivelyVisible && Equals(block.Tag, tag)).Text;
}
