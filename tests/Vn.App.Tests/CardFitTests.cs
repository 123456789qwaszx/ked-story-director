using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Media;
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
/// 이미 꼭 맞으면 단추가 「줄이기」가 되고 누르면 처음 크기로 돌아간다(usability UX33).
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
    public void 고친_글에_다시_누르면_다시_맞추고_짧아지면_기본_크기로_돌아간다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Write(graph, session, root, [Long, "윌로: 응."]);
        Fit(graph, root);
        double first = session.Project.FindNode(root)!.Layout.Width!.Value;

        // 더 긴 줄을 쓰고 누르면 그만큼 넓어진다(꼭 맞던 카드라도 글이 바뀌었으면 「줄이기」가 아니라 다시 맞춘다).
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
    public void 카드의_글자_크기와_줄_간격으로_잰다() => HeadlessUi.Run(() =>
    {
        // 「현재 글꼴과 글 크기를 고려해서」 — 카드의 처음 글자 크기 · 줄 간격(카드를 만들 때의 설정 · writer-formatting WF-D13 · WF-D19)으로 잰다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        string[] lines = [Long, "윌로: 응.", "라루: 가자.", "윌로: 어디로?", "라루: 바다로.", "윌로: 좋아."];
        Write(graph, session, root, lines);
        Fit(graph, root);
        (double Width, double Height) small = (session.Project.FindNode(root)!.Layout.Width!.Value, session.Project.FindNode(root)!.Layout.WritingHeight!.Value);

        ScriptFont.Set(null, 20, 180);

        try
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();

            // 서 있는 카드는 설정을 따르지 않는다(WF-D19) — 맞추기 단추는 이미 꼭 맞은 카드를 줄이므로 크기만 본다.
            Assert.Equal(small, (session.Project.FindNode(root)!.Layout.Width!.Value, session.Project.FindNode(root)!.Layout.WritingHeight!.Value));
            Assert.Equal(small.Height, WritingBox(graph, root).Height);

            // 카드의 줄 간격을 서식 도구로 넓히면 — 같은 폭에서 더 높게 맞춰진다.
            session.Editor.SetWriterLineSpacing(root, 180);
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
            Fit(graph, root);
            Assert.Equal(small.Width, session.Project.FindNode(root)!.Layout.Width);
            Assert.True(session.Project.FindNode(root)!.Layout.WritingHeight > small.Height);
            Assert.Equal(lines.Length, VisualLines(graph, root));
            AssertAllVisible(graph, root);

            // 지금 만든 카드는 새 글자 크기로 선다 — 같은 글이 더 넓고 높게 맞춰진다.
            session.Editor.AddEpisode("ch01", "big", title: "big", 0, 600, sceneId: "opening");
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
            string big = CardId(session, "big");
            Write(graph, session, big, lines);
            Fit(graph, big);

            Assert.True(session.Project.FindNode(big)!.Layout.Width > small.Width);
            Assert.True(session.Project.FindNode(big)!.Layout.WritingHeight > session.Project.FindNode(root)!.Layout.WritingHeight);
            Assert.Equal(lines.Length, VisualLines(graph, big));
            AssertAllVisible(graph, big);
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

    [Fact]
    public void 단추는_누르기_좋은_크기이고_제목_줄_높이는_그대로다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「엄청 자주 쓰게 될 텐데 조금만 크기를 키워 줘」(usability UX32 · ⛔ 그 전에는 16 × 18 · 글자 12).
        // 제목 줄은 높이를 지킨다 — 분기 점의 어림 자리와 카드 높이가 그 값으로 센다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        Dispatcher.UIThread.RunJobs();
        Button fit = FitButton(graph, CardId(session, "root"));

        Assert.Equal((24d, 24d), (fit.Bounds.Width, fit.Bounds.Height));
        Assert.Equal((16d, 16d), (Glyph(fit).Width, Glyph(fit).Height));   // 그림(바깥 화살표 · 안쪽 화살표 — UX33)
        Assert.Equal(20d, ((Grid)fit.Parent!).Bounds.Height);
    });

    [Fact]
    public void 꼭_맞으면_단추가_줄이기가_되고_누르면_처음_크기로_돌아가며_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「이미 대본 글자에 정확히 맞춰진 상태라면 버튼의 아이콘이 축소로 바뀌고, 그 상태에서 누르면 최소 크기로」(usability UX33).
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Write(graph, session, root, [Long, "윌로: 응.", "라루: 가자."]);
        Assert.False(Fitted(graph, root));
        Geometry expand = Glyph(FitButton(graph, root)).Data!;

        Fit(graph, root);
        (double Width, double Height) fitted = (Card(graph, root).Width, WritingBox(graph, root).Height);
        Assert.True(Fitted(graph, root));
        Assert.NotSame(expand, Glyph(FitButton(graph, root)).Data);   // 그림이 「줄이기」로 바뀌었다
        Assert.Contains("줄이기", ToolTip.GetTip(FitButton(graph, root)) as string, StringComparison.Ordinal);

        long revision = session.Editor.Revision;
        Fit(graph, root);

        Assert.Equal(revision + 1, session.Editor.Revision);
        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal((DefaultWidth, DefaultHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));
        Assert.False(Fitted(graph, root));

        // 되돌리면 꼭 맞던 크기로 — 다시 지은 카드도 꼭 맞음을 잰다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(fitted, (Card(graph, root).Width, WritingBox(graph, root).Height));
        Assert.True(Fitted(graph, root));
    });

    [Fact]
    public void 짧은_글은_처음_크기가_곧_맞춘_크기라_줄이기가_서지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Write(graph, session, root, ["라루: 짧다."]);
        long revision = session.Editor.Revision;

        Fit(graph, root);

        Assert.Equal(revision, session.Editor.Revision);   // 같은 크기 — 적지 않는다
        Assert.False(Fitted(graph, root));
    });

    [Fact]
    public void 꼭_맞던_카드의_글을_고치면_다시_글에_맞춤이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Write(graph, session, root, [Long, "윌로: 응."]);
        Fit(graph, root);
        Assert.True(Fitted(graph, root));

        Write(graph, session, root, [Long + " 그래도 참고 걸었다.", "윌로: 응."]);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Fitted(graph, root));
    });

    [Fact]
    public void 다시_연_프로젝트에서도_꼭_맞은_카드는_줄이기다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Write(graph, session, root, [Long, "윌로: 응."]);
        Fit(graph, root);
        session.Save();

        session.Open(session.ProjectPath!);
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Fitted(graph, root));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
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

    private static bool Fitted(GraphEditorView graph, string nodeId) => FitButton(graph, nodeId).Classes.Contains(GraphEditorView.FittedClass);

    private static Avalonia.Controls.Shapes.Path Glyph(Button button) => Assert.IsType<Avalonia.Controls.Shapes.Path>(button.Content);

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
