using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>스탯 키를 바꾸면 [대본] 카드의 글 칸도 새 키를 보인다</b>(2026-11-25 · stat-read-rename S1). 화자 개명 때 겪은 것(usability UX18 — 다시 채우지 않은
/// 칸을 고쳐 넣으면 옛 이름이 줄에 도로 들어갔다)이 대사의 <c>{stat("키")}</c>에도 없어야 한다 — 쓰던 칸(초점이 든 칸)도.
/// </summary>
public sealed class StatRenameWritingTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-stat-rename-writing", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 키를_바꾸면_카드의_글_칸이_새_키이고_쓰던_칸에_이어_써도_옛_키가_안_돌아온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string root = Card(session, "root");
        string a = Card(session, "a");
        session.Editor.AddChapterStat("ch01", "trust");
        session.Editor.ApplyScenarioText(root, "라루: 신뢰 {stat(\"trust\")}.");
        session.Editor.ApplyScenarioText(a, "윌로: {stat(\"trust\")}");
        graph.Rebuild();
        session.Select(root);
        Dispatcher.UIThread.RunJobs();

        // a는 쓰던 칸이다 — 초점이 든 채 키가 바뀐다.
        Writing(graph, a).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, session.Editor.RenameChapterStat("ch01", "trust", "favor").DialogueLines);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("라루: 신뢰 {stat(\"favor\")}.\n", Writing(graph, root).Text);
        Assert.Equal("윌로: {stat(\"favor\")}\n", Writing(graph, a).Text);

        // 그 칸에 이어 쓰고 나온다 — 옛 키가 줄에 도로 들어가지 않는다.
        TextBox box = Writing(graph, a);
        box.Focus();
        box.Text = "윌로: {stat(\"favor\")}\n윌로: 더.\n";
        Dispatcher.UIThread.RunJobs();
        graph.CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();
        graph.FindControl<Border>("WritingFocusSink")!.Focus();
        Dispatcher.UIThread.RunJobs();

        DialogueNode card = (DialogueNode)session.Project.FindNode(a)!;
        Assert.Equal(
            ["{stat(\"favor\")}", "더."],
            card.Script.ActiveLines.Select(line => card.Script.Text(line.Id).Text).ToList());

        window.Close();
    });

    private static string Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static TextBox Writing(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId)
            .GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);
}
