using Avalonia;
using Avalonia.Controls;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>장면 프레임</b> — 챕터 박스 안에 한 겹 더 (R6 S-3b · R6-explorer §5).
///
/// 같은 장면의 카드가 한 영역으로 보여야 작가가 <b>어디까지가 한 수명</b>인지 안다 —
/// 장면 안에서는 모든 게 물릴 수 있고, 장면이 끝나면 확정된다.
/// </summary>
public sealed class SceneFrameTests
{
    [Fact]
    public void 장면마다_이름표가_붙은_영역이_카드_뒤에_깔린다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();

        Chapter(session, ("opening", "ep01"), ("opening", "ep02"), ("classroom", "ep03"));
        graph.Rebuild();

        // 「⌄」 — 누르면 그 장면을 접는다(2026-10-30 SF1 · 챕터 이름표와 같은 손짓).
        Assert.Contains("장면 opening ⌄", SceneLabels(graph));
        Assert.Contains("장면 classroom ⌄", SceneLabels(graph));

        // 장면 영역(테두리)은 챕터 프레임과 같은 규율 — 카드보다 앞 인덱스(= 뒤에 깔림)다. 이름표는 판이 아니라 판 위의 화면 겹에 선다
        // (design/board-zoom.md 4절 — 줄여도 겹치지 않는 자리를 화면 기준으로 찾는다).
        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Rect area = graph.SceneAreaOf("opening")!.Value;
        Border frame = canvas.Children.OfType<Border>().First(border =>
            border.Child is null && Canvas.GetLeft(border) == area.X && Canvas.GetTop(border) == area.Y);
        Border card = canvas.Children.OfType<Border>().First(border =>
            border.Tag is string tag && session.Project.FindNode(tag) is not null);

        Assert.True(
            canvas.Children.IndexOf(frame) < canvas.Children.IndexOf(card),
            "장면 영역은 노드 카드 뒤에 깔려야 한다");
        Assert.DoesNotContain(canvas.Children.OfType<Border>(), border => border.Tag as string == GraphEditorView.SceneLabelTag + "opening");
    });

    [Fact]
    public void 장면이_하나뿐이어도_그린다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 테두리가 있어야 그 장면을 눌러 고르고 접는다(전에는 챕터 프레임과 겹친다고 걷었다).
        (GraphEditorView graph, AuthoringSession session) = Show();

        Chapter(session, ("opening", "ep01"), ("opening", "ep02"));
        graph.Rebuild();

        Assert.Single(SceneLabels(graph));
    });

    [Fact]
    public void 장면이_하나뿐인_챕터도_장면_테두리_바깥에_챕터의_빈_띠가_넉넉하다() => HeadlessUi.Run(() =>
    {
        // 2026-11-25 소유자 — 「챕터 하나에 장면만 있을 때 챕터와 장면 사이의 공간이 너무 좁아 우클릭으로 장면을 추가하기 불편」.
        // 그 띠가 챕터 안 · 장면 밖 = 우클릭 「장면 추가」 자리다(⛔ 전에는 14, 위 20).
        (GraphEditorView graph, AuthoringSession session) = Show();

        Chapter(session, ("opening", "ep01"), ("opening", "ep02"));
        graph.Rebuild();

        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Rect FrameOf(double corner)
        {
            Border frame = canvas.Children.OfType<Border>()
                .Single(border => border.Child is null && !border.IsHitTestVisible && border.CornerRadius.TopLeft == corner);
            return new Rect(Canvas.GetLeft(frame), Canvas.GetTop(frame), frame.Width, frame.Height);
        }

        Rect chapter = FrameOf(12);
        Rect scene = FrameOf(8);

        Assert.True(scene.X - chapter.X >= 40, $"왼쪽 띠 {scene.X - chapter.X}");
        Assert.True(chapter.Right - scene.Right >= 40, $"오른쪽 띠 {chapter.Right - scene.Right}");
        Assert.True(chapter.Bottom - scene.Bottom >= 40, $"아래 띠 {chapter.Bottom - scene.Bottom}");
        Assert.True(scene.Y - chapter.Y >= 40, $"위 띠 {scene.Y - chapter.Y}");   // 챕터 이름표가 그 위에 앉는다
    });

    [Fact]
    public void 들어오는_자리가_둘인_장면은_이름표가_짚어_준다() => HeadlessUi.Run(() =>
    {
        // 거부는 내보내기 관문의 일이다 — 화면은 <b>그려 놓고 짚는다</b>(묶음 규율과 같다).
        (GraphEditorView graph, AuthoringSession session) = Show();

        ChapterDocument chapter = Chapter(
            session, ("opening", "root"), ("shared", "a"), ("shared", "b"));

        chapter.Edges.Add(new ChapterEdge("root", "a", "A로", null, null));
        chapter.Edges.Add(new ChapterEdge("root", "b", "B로", null, null));

        graph.Rebuild();

        Assert.Contains("장면 shared ⚠ ⌄", SceneLabels(graph));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static List<string> SceneLabels(GraphEditorView graph) =>
        graph.FindControl<Canvas>("FrameLabelLayer")!.Children.OfType<Border>()
            .Select(border => (border.Child as TextBlock)?.Text)
            .Where(text => text is not null && text.StartsWith("장면 ", StringComparison.Ordinal))
            .Select(text => text!)
            .ToList();

    /// <summary>챕터 하나와 그 판의 카드들 — 판 이름이 챕터 Id인 것이 둘을 잇는 규약이다.</summary>
    private static ChapterDocument Chapter(
        AuthoringSession session, params (string SceneId, string EpisodeId)[] episodes)
    {
        ChapterDocument chapter = session.Editor.EnsureChapter("ch01");

        for (int index = 0; index < episodes.Length; index++)
        {
            (string sceneId, string episodeId) = episodes[index];

            // 에피소드와 카드가 한 칸에 선다 — 카드는 가로로 늘어놓는다.
            session.Editor.AddEpisode("ch01", episodeId, episodeId, index * 260, 0, sceneId);
            session.Editor.MoveNode(EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id, index * 260, 0);
        }

        return chapter;
    }

    private static (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        string directory = Path.Combine(
            Path.GetTempPath(), "vn-scene-frame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string manifest = Path.Combine(directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "장면 박스 검증" });
        session.Open(manifest);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (graph, session);
    }
}
