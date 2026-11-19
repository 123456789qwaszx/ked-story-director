using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>카드에 새 줄을 더하면 그 카드만 다시 짓는다</b> (2026-11-19 소유자 — 속도 개선 ① · usability UX13). ⛔ 판을 통째로 다시 지었다 —
/// 에피소드 160개에서 새 줄 하나에 2초. 다른 카드 · 선이 달라지면(분기가 걸린 줄을 지웠다) 지금처럼 통째로 짓는다.
/// </summary>
public sealed class CardLinesRebuildTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-lines", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 새_줄은_그_카드만_다시_짓고_다른_카드와_철도는_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string root = Card(session, "root");
        Border other = CardVisual(graph, Card(session, "a"));
        int rails = graph.RailDrawCount;
        int cards = graph.CardRebuildCount;

        session.Editor.ApplyScenarioText(root, "라루: 첫 줄.\n윌로: 둘째 줄.");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(cards + 1, graph.CardRebuildCount);
        Assert.Equal(rails, graph.RailDrawCount);                          // 판을 통째로 짓지 않았다
        Assert.Same(other, CardVisual(graph, Card(session, "a")));        // 다른 카드는 그 카드 그대로다
        Assert.Equal("라루: 첫 줄.\n윌로: 둘째 줄.", WritingBox(graph, root).Text);
        Assert.Single(graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>(), border => border.Tag as string == root);

        // 되돌리기는 판을 통째로 짓는다(되돌리면 객체가 모두 새것이다).
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(rails + 1, graph.RailDrawCount);

        window.Close();
    });

    [Fact]
    public void 다른_카드나_선이_달라지면_그_카드만_짓지_않는다() => HeadlessUi.Run(() =>
    {
        // 그 카드만 다시 지어도 되는가 — 선(연결)이 생기거나 사라지거나, 다른 카드의 포트 · 이름 · 자리가 바뀌면 아니다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        string root = Card(session, "root");
        GraphProjection Now() => GraphProjectionBuilder.Build(session.Project, session.ExpandedFileIds, session.CollapsedScenes);

        GraphProjection before = Now();
        session.Editor.ApplyScenarioText(root, "라루: 첫 줄.\n윌로: 둘째 줄.");
        Assert.True(GraphEditorView.OnlyCardChanged(before, Now(), root));          // 그 카드의 줄만

        before = Now();
        DialogueNode node = session.Project.FindDialogue(root)!;
        session.Editor.AddBranchMarker(root, node.Script.ActiveLines.Last().Id);
        Assert.False(GraphEditorView.OnlyCardChanged(before, Now(), root));         // 선이 생겼다(분기 카드도)

        before = Now();
        session.Editor.MoveNode(Card(session, "a"), 900, 900);
        Assert.False(GraphEditorView.OnlyCardChanged(before, Now(), root));         // 다른 카드가 움직였다

        window.Close();
    });

    private static string Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Border CardVisual(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        CardVisual(graph, nodeId).GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);
}
