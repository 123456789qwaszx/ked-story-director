using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 카드의 가로 · 세로 글 맞춤</b> (2026-11-25 소유자 — 「계속 글을 쓸때마다 저 단추를 계속 클릭하면서 에피소드를 맞춰줘야하는게 불편 … 가로넓이는
/// 유지한 채, 세로로만 확장시키고 싶은데 … 가로 글 맞춤, 세로 글 맞춤 OnOff 버튼을 둔 다음, 이 버튼을 킨 상태라면 글을 쓰면서 자동으로 노드의 넓이가
/// 맞춰지도록」). 고른 것: 카드마다 기억 · 새 카드는 세로만 켜짐 · 손으로 끈 쪽은 꺼진다.
///
/// 제목 줄 오른쪽 끝의 단추 둘이 켜고 끈다. 켠 쪽은 글을 넣을 때마다(쓰다 멈추거나 칸을 나올 때) 글을 재서 맞는다 — 폭은 줄이 꺾이지 않게, 글 칸 높이는
/// 모든 줄이 보이게(처음 크기보다 작게는 줄이지 않는다). 맞춘 크기는 그 넣기의 되돌리기 칸에 든다. ⛔ 그 전에는 「글에 맞춤 / 줄이기」 단추 하나를 눌러야
/// 맞췄다(2026-11-19 · usability UX5 · UX33).
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
    public void 새_카드는_세로_글_맞춤만_켜고_서며_단추_둘은_제목_줄_끝에서_초점을_가져가지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        Dispatcher.UIThread.RunJobs();

        foreach (string episodeId in new[] { "root", "a", "b" })
        {
            string nodeId = CardId(session, episodeId);
            Assert.False(session.Project.FindNode(nodeId)!.Layout.FitWidth);
            Assert.True(session.Project.FindNode(nodeId)!.Layout.FitHeight);
            Assert.False(IsOn(graph, nodeId, width: true));
            Assert.True(IsOn(graph, nodeId, width: false));

            foreach (bool width in new[] { true, false })
            {
                Button toggle = Toggle(graph, nodeId, width);
                Assert.False(toggle.Focusable);
                Assert.Contains(width ? "가로 글 맞춤" : "세로 글 맞춤", ToolTip.GetTip(toggle) as string, StringComparison.Ordinal);
                Assert.Equal(3, Grid.GetColumn((Control)toggle.Parent!));   // 제목 줄의 맨 오른쪽 칸
                Assert.Equal((22d, 24d), (toggle.Bounds.Width, toggle.Bounds.Height));
            }

            Assert.Equal(20d, ((Grid)Toggle(graph, nodeId, true).Parent!.Parent!).Bounds.Height);   // 제목 줄 높이는 그대로다
        }
    });

    [Fact]
    public void 세로만_켜져_있으면_쓰는_대로_글_칸이_늘고_폭은_그대로이며_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        string[] lines = [Long, .. Enumerable.Range(1, 11).Select(index => $"윌로: {index}번째 대답이다.")];

        Type(graph, root, string.Join('\n', lines));

        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);
        Assert.True(session.Project.FindNode(root)!.Layout.WritingHeight > DefaultHeight);
        Assert.True(VisualLines(graph, root) > lines.Length, "폭은 그대로라 긴 줄은 꺾인다");
        AssertAllVisible(graph, root);

        // 넣기와 맞춘 크기가 한 칸이다 — 한 번 되돌리면 글도 크기도 처음이다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.DoesNotContain(session.Project.FindDialogue(root)!.Script.ActiveLines,
            line => session.Project.FindDialogue(root)!.Script.Text(line.Id).Text.Contains("번째 대답", StringComparison.Ordinal));
    });

    [Fact]
    public void 가로를_켜면_곧바로_줄이_꺾이지_않게_넓어지고_켜기와_맞춤이_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        string[] lines = [Long, .. Enumerable.Range(1, 11).Select(index => $"윌로: {index}번째 대답이다.")];
        Write(graph, session, root, lines);
        Assert.True(VisualLines(graph, root) > lines.Length, "기본 폭에서는 꺾여야 한다");

        Click(graph, root, width: true);

        Assert.True(session.Project.FindNode(root)!.Layout.FitWidth);
        Assert.True(IsOn(graph, root, width: true));
        Assert.True(session.Project.FindNode(root)!.Layout.Width > DefaultWidth);
        Assert.True(session.Project.FindNode(root)!.Layout.WritingHeight > DefaultHeight);
        Assert.Equal(lines.Length + 1, VisualLines(graph, root));   // 한 줄이 한 줄이다 — 꺾이지 않는다(+ 끝의 이어 쓰기 빈 줄)
        AssertAllVisible(graph, root);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.False(session.Project.FindNode(root)!.Layout.FitWidth);
        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal(DefaultWidth, Card(graph, root).Width);
        Assert.False(IsOn(graph, root, width: true));
    });

    [Fact]
    public void 맞춤을_끄면_크기는_그대로고_더_써도_따라오지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Type(graph, root, string.Join('\n', Enumerable.Range(1, 10).Select(index => $"윌로: {index}번째 대답이다.")));
        double height = WritingBox(graph, root).Height;
        Assert.True(height > DefaultHeight);

        Click(graph, root, width: false);

        Assert.False(session.Project.FindNode(root)!.Layout.FitHeight);
        Assert.False(IsOn(graph, root, width: false));
        Assert.Equal(height, WritingBox(graph, root).Height);
        Assert.Equal(height, session.Project.FindNode(root)!.Layout.WritingHeight);

        Type(graph, root, string.Join('\n', Enumerable.Range(1, 20).Select(index => $"윌로: {index}번째 대답이다.")));
        Assert.Equal(height, session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal(height, WritingBox(graph, root).Height);
    });

    [Fact]
    public void 글이_짧아지면_처음_크기로_돌아가고_처음보다_작게는_줄지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Click(graph, root, width: true);
        Type(graph, root, string.Join('\n', [Long, .. Enumerable.Range(1, 9).Select(index => $"윌로: {index}번째 대답이다.")]));
        Assert.True(session.Project.FindNode(root)!.Layout.Width > DefaultWidth);

        // 줄을 지운 글은 칸을 나올 때 들어간다(쓰는 중에는 넣지 않는다) — 그때 맞춘다.
        Type(graph, root, "라루: 짧다.\n윌로: 응.");
        Leave(graph);

        Assert.Null(session.Project.FindNode(root)!.Layout.Width);
        Assert.Null(session.Project.FindNode(root)!.Layout.WritingHeight);
        Assert.Equal((DefaultWidth, DefaultHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));
    });

    [Fact]
    public void 카드의_글자_크기와_줄_간격으로_잰다() => HeadlessUi.Run(() =>
    {
        // 카드의 처음 글자 크기 · 줄 간격(카드를 만들 때의 설정 · writer-formatting WF-D13 · WF-D19)으로 잰다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        string[] lines = [Long, "윌로: 응.", "라루: 가자.", "윌로: 어디로?", "라루: 바다로.", "윌로: 좋아."];
        Click(graph, root, width: true);
        Type(graph, root, string.Join('\n', lines));
        (double Width, double Height) small = (session.Project.FindNode(root)!.Layout.Width!.Value, session.Project.FindNode(root)!.Layout.WritingHeight!.Value);

        ScriptFont.Set(null, 20, 180);

        try
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();

            // 서 있는 카드는 설정을 따르지 않는다(WF-D19).
            Assert.Equal(small, (session.Project.FindNode(root)!.Layout.Width!.Value, session.Project.FindNode(root)!.Layout.WritingHeight!.Value));

            // 카드의 줄 간격을 서식 도구로 넓히고 다시 쓰면 — 같은 폭에서 더 높게 맞춰진다.
            session.Editor.SetWriterLineSpacing(root, 180);
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
            Type(graph, root, string.Join('\n', lines) + "\n라루: 끝.");
            Assert.Equal(small.Width, session.Project.FindNode(root)!.Layout.Width);
            Assert.True(session.Project.FindNode(root)!.Layout.WritingHeight > small.Height);
            AssertAllVisible(graph, root);

            // 지금 만든 카드는 새 글자 크기로 선다 — 같은 글이 더 넓고 높게 맞춰진다.
            session.Editor.AddEpisode("ch01", "big", title: "big", 0, 600, sceneId: "opening");
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
            string big = CardId(session, "big");
            Click(graph, big, width: true);
            Type(graph, big, string.Join('\n', lines));

            Assert.True(session.Project.FindNode(big)!.Layout.Width > small.Width);
            Assert.True(session.Project.FindNode(big)!.Layout.WritingHeight > small.Height);
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
        Click(graph, root, width: true);

        Type(graph, root, string.Concat(Enumerable.Repeat("아주 길게 이어지는 지문이다. ", 40)));

        Assert.Equal(GraphEditorView.MaxCardWidth, session.Project.FindNode(root)!.Layout.Width);
        Assert.True(VisualLines(graph, root) > 1);
        AssertAllVisible(graph, root);
    });

    [Fact]
    public void 맞춤은_저장하고_다시_열어도_남고_열기만_해서는_크기가_바뀌지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        Click(graph, root, width: true);
        Type(graph, root, string.Join('\n', [Long, "윌로: 응."]));
        NodeLayout before = session.Project.FindNode(root)!.Layout;
        (double? Width, double? Height) size = (before.Width, before.WritingHeight);
        session.Save();

        session.Open(session.ProjectPath!);
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        long revision = session.Editor.Revision;
        Dispatcher.UIThread.RunJobs();

        NodeLayout after = session.Project.FindNode(root)!.Layout;
        Assert.True(after.FitWidth);
        Assert.True(after.FitHeight);
        Assert.Equal(size, (after.Width, after.WritingHeight));
        Assert.True(IsOn(graph, root, width: true));
        Assert.Equal(revision, session.Editor.Revision);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        return (window.FindControl<GraphEditorView>("Graph")!, session);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    /// <summary>편집기로 바로 넣는다 — 글 칸을 지나지 않아 맞추지 않는다.</summary>
    private static void Write(GraphEditorView graph, AuthoringSession session, string nodeId, string[] lines)
    {
        session.Editor.ApplyScenarioText(nodeId, string.Join("\n", lines));
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
    }

    /// <summary>글 칸에 쓰고 멈춘다 — 사람이 쓰는 길 그대로(쓰다 멈추면 넣고, 켠 쪽을 맞춘다).</summary>
    private static void Type(GraphEditorView graph, string nodeId, string text)
    {
        TextBox box = WritingBox(graph, nodeId);
        box.Focus();
        box.Text = text;
        Dispatcher.UIThread.RunJobs();   // 글이 바뀐 알림이 돈다(쓰는 중인 칸을 안다)
        graph.CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
    }

    /// <summary>글 칸을 나온다 — 넣지 않은 글(지운 줄도)이 들어간다.</summary>
    private static void Leave(GraphEditorView graph)
    {
        Dispatcher.UIThread.RunJobs();
        graph.FindControl<Border>("WritingFocusSink")!.Focus();
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
    }

    private static void Click(GraphEditorView graph, string nodeId, bool width)
    {
        Toggle(graph, nodeId, width).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        graph.UpdateLayout();
    }

    private static Border Card(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static bool IsOn(GraphEditorView graph, string nodeId, bool width) =>
        Toggle(graph, nodeId, width).Classes.Contains(GraphEditorView.FitOnClass);

    private static Button Toggle(GraphEditorView graph, string nodeId, bool width) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Button>().Single(button =>
            button.Tag as string == (width ? GraphEditorView.FitWidthToggleTag : GraphEditorView.FitHeightToggleTag) + nodeId);

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
