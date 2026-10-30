using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>장면의 루트 카드는 색이 조금 다르다</b> (2026-10-30 SR — 소유자: *"장면의 Root 노드는 색깔을 조금 다르게 표시해줄래?"*).
///
/// 칠하는 카드는 하이어아키가 ⌂를 다는 그것이다 — 장면마다 처음 들어오는 에피소드. 등뼈 · 바탕은 챕터 그래프의 「SCENE ROOT」
/// 표와 같은 파랑 쪽, 아이콘은 ⌂. 장면ID를 하나도 안 적은 챕터는 칠하지 않는다(모든 카드가 루트라 소음이다).
/// </summary>
public sealed class SceneRootCardTests : IDisposable
{
    private static readonly Color RootBackground = Color.FromRgb(0xEF, 0xF4, 0xFC);
    private static readonly Color EpisodeBackground = Color.FromRgb(0xFB, 0xF6, 0xEA);

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-scene-root-card", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 장면마다_처음_들어오는_카드만_다른_색이고_장면ID가_없는_챕터는_칠하지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();

        // ch01: opening(root → a) → hall(b). 루트는 root(챕터 시작)와 b(밖에서 처음 들어온다).
        Assert.Equal(RootBackground, Background(graph, Card(session, "ch01", "root")));
        Assert.Equal(RootBackground, Background(graph, Card(session, "ch01", "b")));
        Assert.Equal(EpisodeBackground, Background(graph, Card(session, "ch01", "a")));
        Assert.Equal("⌂", Icon(graph, Card(session, "ch01", "b")));
        Assert.Equal("📄", Icon(graph, Card(session, "ch01", "a")));

        // ch02: 장면ID가 없다 — 칠하지 않는다.
        Assert.Equal(EpisodeBackground, Background(graph, Card(session, "ch02", "x")));
        Assert.Equal(EpisodeBackground, Background(graph, Card(session, "ch02", "y")));

        // 장면이 바뀌면 따라간다 — a를 hall로 옮기면 a가 hall의 루트가 되고 b는 아니다.
        session.Editor.UpdateEpisode("ch01", "a", sceneId: "hall");
        graph.Rebuild();

        Assert.Equal(RootBackground, Background(graph, Card(session, "ch01", "a")));
        Assert.Equal(EpisodeBackground, Background(graph, Card(session, "ch01", "b")));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "장면 루트" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");
        session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "root", "root", 0, 0, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "a", "a", 0, 400, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "b", "b", 0, 800, sceneId: "hall");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "다음");

        session.Editor.EnsureChapter("ch02");
        session.EnsureChapterBoard("ch02");
        session.Editor.AddEpisode("ch02", "x", "x", 0, 0);
        session.Editor.AddEpisode("ch02", "y", "y", 0, 400);
        session.Editor.AddEdge("ch02", "x", "y", optionLabel: "가자");

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        return (graph, session);
    }

    private static DialogueNode Card(AuthoringSession session, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!;

    private static Border CardOf(GraphEditorView graph, DialogueNode node) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(border => border.Tag as string == node.Id);

    private static Color Background(GraphEditorView graph, DialogueNode node) =>
        ((ISolidColorBrush)CardOf(graph, node).Background!).Color;

    /// <summary>카드 머리의 아이콘 — 머리 줄의 첫 글자 칸.</summary>
    private static string? Icon(GraphEditorView graph, DialogueNode node) =>
        CardOf(graph, node).GetLogicalDescendants().OfType<TextBlock>().First().Text;
}
