using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 카드의 「글에 맞춤」</b> (2026-11-19 소유자 — 「현재 글꼴과 글 크기를 고려해서 줄바꿈이 일어나지 않도록 가로의 길이를 자동으로
/// 늘리고, 세로길이역시 모든 줄이 바로 보이도록 … 다시 누른다면 변경된 내용에 맞춰 다시」 · usability UX5).
///
/// 제목 줄 오른쪽 끝의 단추가 글을 재서 폭(줄이 꺾이지 않게) · 글 칸 높이(모든 줄이 보이게)를 맞춘다 — 되돌리기 한 칸, 같으면 적지 않는다.
/// </summary>
public sealed class CardFitTests : IDisposable
{
    private const double DefaultWidth = 210;
    private const double DefaultHeight = 88;

    /// <summary>기본 폭에서는 몇 번 꺾이는 대사 — 서른 글자 남짓.</summary>
    private const string Long = "라루: 오늘따라 바람이 유난히 차가워서 겉옷을 챙겨 오지 않은 것을 몹시 후회했다.";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-fit", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 맞추면_줄이_꺾이지_않게_넓어지고_모든_줄이_보이게_늘며_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        string[] lines = [Long, .. Enumerable.Range(1, 11).Select(index => $"윌로: {index}번째 대답이다.")];
        Write(graph, session, root, lines);

        Assert.True(VisualLines(graph, root) > lines.Length, "기본 폭에서는 꺾여야 한다");
        long before = session.Editor.Revision;

        Fit(graph, root);

        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.True(session.Project.FindNode(root)!.Layout.Width > DefaultWidth);
        Assert.True(session.Project.FindNode(root)!.Layout.WritingHeight > DefaultHeight);
        Assert.Equal(lines.Length, VisualLines(graph, root));   // 한 줄이 한 줄이다 — 꺾이지 않는다
        AssertAllVisible(graph, root);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);
    });

    [Fact]
    public void 다시_누르면_고친_글에_맞춰_다시_맞추고_그대로면_적지_않으며_짧아지면_기본_크기로_돌아간다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Write(graph, session, root, [Long, "윌로: 응."]);
        Fit(graph, root);
        double first = session.Project.FindNode(root)!.Layout.Width!.Value;

        // 그대로면 적지 않는다.
        long revision = session.Editor.Revision;
        Fit(graph, root);
        Assert.Equal(revision, session.Editor.Revision);

        // 더 긴 줄을 쓰고 누르면 그만큼 넓어진다.
        Write(graph, session, root, [Long + " 그래도 참고 걸었다.", "윌로: 응."]);
        Fit(graph, root);
        Assert.True(session.Project.FindNode(root)!.Layout.Width > first);
        Assert.Equal(2, VisualLines(graph, root));

        // 짧아지면 기본 크기로 돌아간다(기본보다 작게는 줄이지 않는다).
        Write(graph, session, root, ["라루: 짧다.", "윌로: 응."]);
        Assert.Equal("라루: 짧다.\n윌로: 응.", WritingBox(graph, root).Text);
        Fit(graph, root);
        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal((DefaultWidth, DefaultHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));
    });

    [Fact]
    public void 설정의_글자_크기와_줄_간격으로_잰다() => HeadlessUi.Run(() =>
    {
        // 「현재 글꼴과 글 크기를 고려해서」 — 설정 「대본 글 칸」을 키우면 같은 글이 더 넓고 높게 맞춰진다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        string[] lines = [Long, "윌로: 응.", "라루: 가자."];
        Write(graph, session, root, lines);
        Fit(graph, root);
        (double Width, double Height) small = (session.Project.FindNode(root)!.Layout.Width!.Value, session.Project.FindNode(root)!.Layout.WritingHeight ?? DefaultHeight);

        ScriptFont.Set(null, 20, 180);

        try
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
            Fit(graph, root);

            Assert.True(session.Project.FindNode(root)!.Layout.Width > small.Width);
            Assert.True(session.Project.FindNode(root)!.Layout.WritingHeight > small.Height);
            Assert.Equal(lines.Length, VisualLines(graph, root));
            AssertAllVisible(graph, root);
        }
        finally
        {
            ScriptFont.Reset();   // 이 PC의 앱 설정은 시험 프로세스가 함께 쓴다
        }
    });

    [Fact]
    public void 아주_긴_줄은_끝에서_꺾고_그_폭에서_모든_줄이_보이게_늘린다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Write(graph, session, root, [string.Concat(Enumerable.Repeat("아주 길게 이어지는 지문이다. ", 40))]);

        Fit(graph, root);

        Assert.Equal(GraphEditorView.MaxCardWidth, session.Project.FindNode(root)!.Layout.Width);
        Assert.True(VisualLines(graph, root) > 1);
        AssertAllVisible(graph, root);
    });

    [Fact]
    public void 단추는_글_칸이_있는_카드마다_제목_줄_오른쪽_끝에_서고_초점을_가져가지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();

        foreach (string episodeId in new[] { "root", "a", "b" })
        {
            string nodeId = CardId(session, episodeId);
            Button fit = FitButton(graph, nodeId);
            Assert.False(fit.Focusable);
            Assert.Contains("글에 맞춤", ToolTip.GetTip(fit) as string, StringComparison.Ordinal);
            Assert.Equal(3, Grid.GetColumn(fit));   // 제목 줄의 맨 오른쪽 칸
        }
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        return (window.FindControl<GraphEditorView>("Graph")!, session);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static void Write(GraphEditorView graph, AuthoringSession session, string nodeId, string[] lines)
    {
        session.Editor.ApplyScenarioText(nodeId, string.Join("\n", lines));
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
    }

    private static void Fit(GraphEditorView graph, string nodeId)
    {
        FitButton(graph, nodeId).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
    }

    private static Border Card(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static Button FitButton(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Button>().Single(button => button.Tag as string == GraphEditorView.FitButtonTag + nodeId);

    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    /// <summary>글 칸에 실제로 그려진 줄 수 — 꺾인 줄은 둘로 센다.</summary>
    private static int VisualLines(GraphEditorView graph, string nodeId) =>
        WritingBox(graph, nodeId).GetVisualDescendants().OfType<TextPresenter>().Single().TextLayout.TextLines.Count;

    /// <summary>글 칸이 스크롤 없이 모든 줄을 보인다.</summary>
    private static void AssertAllVisible(GraphEditorView graph, string nodeId)
    {
        ScrollViewer scroll = WritingBox(graph, nodeId).GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroll.Extent.Height <= scroll.Viewport.Height + 0.5, $"글 높이 {scroll.Extent.Height} · 보이는 높이 {scroll.Viewport.Height}");
    }
}
