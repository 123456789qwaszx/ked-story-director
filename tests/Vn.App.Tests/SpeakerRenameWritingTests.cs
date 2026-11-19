using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>화자 이름을 바꾸면 모든 카드의 글 칸이 새 이름을 보인다</b> (2026-11-19 · usability UX18). ⛔ 고른 카드의 칸만 다시 채웠다 — 다른 카드는 옛
/// 이름을 보였고, 그 칸을 고쳐 넣으면 옛 이름이 그 카드의 줄에 도로 들어갔다.
/// </summary>
public sealed class SpeakerRenameWritingTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-speaker-rename-writing", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 화자_이름을_바꾸면_고르지_않은_카드의_칸도_새_이름이고_쓰던_칸은_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string root = Card(session, "root");
        string a = Card(session, "a");
        string b = Card(session, "b");
        session.AddSpeakerName("라루");
        session.Editor.ApplyScenarioText(root, "라루: 처음.");
        session.Editor.ApplyScenarioText(a, "라루: 골목 끝.\n윌로: 응.");
        session.Editor.ApplyScenarioText(b, "라루: 복도.");
        graph.Rebuild();
        session.Select(root);
        Dispatcher.UIThread.RunJobs();

        // b는 넣지 않은 글을 든 채다 — 그 글은 건드리지 않는다.
        Writing(graph, b).Text = "라루: 아직 안 넣은 글.";
        int rails = graph.RailDrawCount;

        Assert.NotNull(session.RenameSpeakerName("라루", "하린"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("하린: 처음.", Writing(graph, root).Text);
        Assert.Equal("하린: 골목 끝.\n윌로: 응.", Writing(graph, a).Text);   // 고르지 않은 카드도
        Assert.Equal("라루: 아직 안 넣은 글.", Writing(graph, b).Text);       // 넣지 않은 글은 그대로
        Assert.Equal(rails, graph.RailDrawCount);                             // 판은 다시 짓지 않는다

        window.Close();
    });

    private static string Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static TextBox Writing(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId)
            .GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);
}
