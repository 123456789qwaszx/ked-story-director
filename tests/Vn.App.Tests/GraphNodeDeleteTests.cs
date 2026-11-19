using Avalonia.Controls;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;
using Path = System.IO.Path;

namespace Vn.App.Tests;

/// <summary>
/// <b>[연출 그래프]에서 카드를 지우는 일</b> — 카드 지우기는 에피소드 지우기와 같다.
///
/// 챕터 판의 카드는 에피소드라, 여기서 <b>카드만</b> 걷으면 대본 탭의 트리에는 줄이 남고 글만
/// 빠진 반쪽이 된다. 만들기와 지우기는 한 길이다.
///
/// 주의: 그래서 <b>되돌릴 수 있는 단추가 아니다</b> — 에피소드 삭제는 원고를 <c>.bak</c>으로
/// 민다. 그래서 여기서만 한 번 더 묻고, 단추 이름이 누르기 전에 무엇이 사라지는지 말한다.
/// </summary>
public sealed class GraphNodeDeleteTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-graph-delete", Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

    public GraphNodeDeleteTests()
    {
        Directory.CreateDirectory(_directory);
        ProjectStore.Save(ManifestPath, new StoryProject { Title = "카드 지우기" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 챕터_판의_카드를_지우면_에피소드도_함께_사라진다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        DialogueNode card = Card(session, "ep01");
        Card(session, "ep02");   // 챕터의 마지막 에피소드는 지우지 않는다(ChapterMinimum)

        session.Select(card.Id);
        Click(graph);

        // 첫 걸음은 묻기만 한다 — 원고가 걸려 있고 되돌리기가 못 되돌린다.
        Assert.NotNull(graph.DeleteConfirmButton);
        Assert.Contains("ep01", session.Editor.FindChapter("ch01")!.Episodes.Select(e => e.EpisodeId));

        graph.DeleteConfirmButton!.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("ep01", session.Editor.FindChapter("ch01")!.Episodes.Select(e => e.EpisodeId));
        Assert.Null(session.Project.FindNode(card.Id));
    });

    [Fact]
    public void 단추는_고른_것의_이름을_말하고_고른_것이_없으면_꺼진_삭제다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 기본은 회색(꺼짐) 「삭제」, 챕터 · 장면 · 에피소드를 고르면 「챕터 삭제」 · 「장면 삭제」 · 「에피소드 삭제」.
        // 다른 이름이 적힌 단추가 무엇을 지우면 그것은 <b>단추가 거짓말</b>이다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        Button delete = DeleteButton(graph);
        Assert.Equal(("삭제", false), ((string?)delete.Content, delete.IsEnabled));

        DialogueNode card = Card(session, "ep01");   // 장면 ep01에 선다
        session.Select(card.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 챕터의 마지막 에피소드 · 장면 — 꺼진 「삭제 불가」, 툴팁이 까닭을 말한다(2026-11-18 소유자 · ChapterMinimum).
        Assert.Equal(("삭제 불가", false), ((string?)delete.Content, delete.IsEnabled));
        Assert.Equal(ChapterMinimum.LastEpisodeReason, ToolTip.GetTip(delete));
        session.Select(null);
        graph.SummaryPick = new HierarchyPick(HierarchyKind.Scene, "ch01", SceneId: "ep01");
        Assert.Equal(("삭제 불가", false), ((string?)delete.Content, delete.IsEnabled));
        Assert.Equal(ChapterMinimum.LastSceneReason, ToolTip.GetTip(delete));
        graph.SummaryPick = null;

        Card(session, "ep02");   // 장면 ep02의 에피소드 — 이제 ep01은 마지막이 아니다
        session.Select(card.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(("에피소드 삭제", true), ((string?)delete.Content, delete.IsEnabled));

        // 하이어아키 · 판의 빈 곳에서 고른 줄 — 카드 선택이 풀리고 셸이 요약의 줄을 비춘다.
        session.Select(null);
        graph.SummaryPick = new HierarchyPick(HierarchyKind.Chapter, "ch01");
        Assert.Equal(("챕터 삭제", true), ((string?)delete.Content, delete.IsEnabled));

        graph.SummaryPick = new HierarchyPick(HierarchyKind.Scene, "ch01", SceneId: "ep01");
        Assert.Equal(("장면 삭제", true), ((string?)delete.Content, delete.IsEnabled));

        // 프로젝트 줄 · 사라진 것 · 고른 것 없음은 지울 것이 없다.
        graph.SummaryPick = HierarchyPick.Project;
        Assert.Equal(("삭제", false), ((string?)delete.Content, delete.IsEnabled));
        graph.SummaryPick = new HierarchyPick(HierarchyKind.Scene, "ch01", SceneId: "없는 장면");
        Assert.Equal(("삭제", false), ((string?)delete.Content, delete.IsEnabled));
        graph.SummaryPick = new HierarchyPick(HierarchyKind.Chapter, "ch09");
        Assert.Equal(("삭제", false), ((string?)delete.Content, delete.IsEnabled));
        graph.SummaryPick = null;
        Assert.Equal(("삭제", false), ((string?)delete.Content, delete.IsEnabled));
    });

    /// <summary>그 에피소드의 카드 — `AddEpisode`가 함께 세운 것이다.</summary>
    private static DialogueNode Card(AuthoringSession session, string episodeId)
    {
        session.Editor.AddEpisode("ch01", episodeId, title: string.Empty, 0, 0, sceneId: episodeId);

        return session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => EpisodeNaming.EpisodeIdOf(node) == episodeId);
    }

    private static Button DeleteButton(GraphEditorView graph) =>
        graph.FindControl<Button>("DeleteButton")!;

    private static void Click(GraphEditorView graph)
    {
        DeleteButton(graph).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        session.Open(ManifestPath);
        session.Editor.EnsureChapter("ch01");
        session.EnsureChapterBoard("ch01");

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (graph, session);
    }
}
