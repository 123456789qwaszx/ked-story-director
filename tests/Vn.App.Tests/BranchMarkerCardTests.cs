using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>「조건 분기」 카드 — 줄 사이에 낀다</b> (R7 P-5 · `docs/plans/R7.md`).
///
/// ⛔ <b>내용이 없다.</b> 연출 그래프는 "어디서 갈라지는가"만 짚는다 (2026-09-17 소유자) —
/// 조건식도 선택지 문구도 여기서 정하지 않는다. 조건이 성립하든 말든 다녀오고, 다녀온
/// 에피소드가 제 첫머리에서 보고 아니면 곧바로 돌아온다.
/// </summary>
public sealed class BranchMarkerCardTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-branch-card", Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

    public BranchMarkerCardTests()
    {
        Directory.CreateDirectory(_directory);
        ProjectStore.Save(ManifestPath, new StoryProject { Title = "조건 분기" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 표식이_없으면_카드도_없다() => HeadlessUi.Run(() =>
    {
        (DialogueNodeEditor editor, _, _, string lineId) = Show();

        Assert.Null(Card(editor, lineId));
    });

    [Fact]
    public void 표식을_두면_그_줄_앞에_카드가_낀다() => HeadlessUi.Run(() =>
    {
        // ⚠ <b>앞</b>이다 — 그 줄에 붙은 것이 아니라 그 줄 앞에서 갈라진다는 표식이다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode made = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Border card = Assert.IsType<Border>(Card(editor, lineId));

        Assert.True(
            editor.LineHost.Children.IndexOf(card) <
            editor.LineHost.Children.IndexOf(LineCardAfter(editor, card)));

        Assert.Contains(Texts(card), text => text.Contains(made.Name, StringComparison.Ordinal));
    });

    [Fact]
    public void 카드에는_조건이_없다() => HeadlessUi.Run(() =>
    {
        // ⛔ 조건식을 여기서 적게 하면 연출 그래프가 "갈지 말지"까지 정하게 된다 —
        //    소유자가 명시적으로 뒤로 미룬 일이다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Border card = Assert.IsType<Border>(Card(editor, lineId));

        Assert.Empty(card.GetVisualDescendants().OfType<ComboBox>());
        Assert.Empty(card.GetVisualDescendants().OfType<TextBox>());
    });

    [Fact]
    public void 분기_지우기는_표식과_그_에피소드를_함께_걷는다() => HeadlessUi.Run(() =>
    {
        // 2026-10-29 소유자 — "조건 분기를 추가는 가능한데, 현재는 지우는게 불가능 합니다." 예전 단추는 표식만 걷고
        // 「분기 추가」가 세운 에피소드를 판에 남겼다. 이제 「분기 추가」의 반대다(되돌리기 한 번에 돌아온다).
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode made = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Button remove = Assert.IsType<Border>(Card(editor, lineId))
            .GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.Contains('✕') == true);

        remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(Node(session, nodeId).FindExtension(lineId)?.DetourTargetNodeIds ?? []);
        Assert.Null(session.Project.FindNode(made.Id));
        Assert.Contains(made.Name, session.StatusMessage, StringComparison.Ordinal);

        Rebuild(editor, nodeId);
        Assert.Null(Card(editor, lineId));
    });

    [Fact]
    public void 한_줄에_분기가_둘이면_카드_둘이_다녀오는_차례로_낀다() => HeadlessUi.Run(() =>
    {
        // 2026-10-29 소유자: "하나의 줄에 여러개의 분기가 있을 수 있게" — 위에서부터 차례로 다녀온다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode first = session.Editor.AddBranchMarker(nodeId, lineId);
        DialogueNode second = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Control firstCard = Card(editor, lineId, first.Id)!;
        Control secondCard = Card(editor, lineId, second.Id)!;
        int at = editor.LineHost.Children.IndexOf(firstCard);

        Assert.Equal(at + 1, editor.LineHost.Children.IndexOf(secondCard));
        Assert.DoesNotContain("branch:", editor.LineHost.Children[at + 2].Tag as string ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(Texts(firstCard), text => text.Contains("1/2", StringComparison.Ordinal));
        Assert.Contains(Texts(secondCard), text => text.Contains("2/2", StringComparison.Ordinal));
        Assert.Contains(Texts(firstCard), text => text.Contains(first.Name, StringComparison.Ordinal));
        Assert.Contains(Texts(secondCard), text => text.Contains(second.Name, StringComparison.Ordinal));
        Assert.Equal(
            2,
            NodeConnections.PortsOf(Node(session, nodeId), session.Project).Count(port => port.Kind == ExitPortKind.Detour));

        // 첫 카드의 ✕는 그 분기 하나만 걷는다.
        firstCard.GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.Contains('✕') == true)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal([second.Id], Node(session, nodeId).FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Null(session.Project.FindNode(first.Id));
    });

    [Fact]
    public void 분기_카드를_우클릭하면_그_분기만_지워진다() => HeadlessUi.Run(() =>
    {
        // 2026-10-30 소유자: "그곳에서 생성된 조건 분기를 우클릭해서, 분기를 제거할 수 있게" — 연출 그래프에서 분기 선을
        // 우클릭하는 것과 같은 손놀림이다. 진짜 마우스로 잰다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();
        DialogueNode first = session.Editor.AddBranchMarker(nodeId, lineId);
        DialogueNode second = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        var window = (Window)TopLevel.GetTopLevel(editor)!;
        Control card = Card(editor, lineId, second.Id)!;
        Point spot = card.TranslatePoint(new Point(12, card.Bounds.Height / 2), window)!.Value;
        window.MouseDown(spot, MouseButton.Right);
        window.MouseUp(spot, MouseButton.Right);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal([first.Id], Node(session, nodeId).FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Null(session.Project.FindNode(second.Id));
        Assert.Contains(second.Name, session.StatusMessage, StringComparison.Ordinal);

        // 되돌리기 한 번이면 돌아온다.
        session.Editor.Undo();
        Assert.Equal([first.Id, second.Id], Node(session, nodeId).FindExtension(lineId)!.DetourTargetNodeIds);
    });

    [Fact]
    public void 분기_카드의_색은_연출_그래프의_그_분기_포트_색이다() => HeadlessUi.Run(() =>
    {
        // 2026-10-30 소유자: "모두 보라색으로 표시되는데, 에피소드 노드에서 생기는 색상과 동일하게 해서 구분을".
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();
        DialogueNode first = session.Editor.AddBranchMarker(nodeId, lineId);
        DialogueNode second = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        IReadOnlyList<ExitPort> ports = NodeConnections.PortsOf(Node(session, nodeId), session.Project);
        Color ColorOf(DialogueNode target) =>
            ((Avalonia.Media.SolidColorBrush)BranchPalette.Accent(
                ports.Single(port => port.Kind == ExitPortKind.Detour && port.TargetNodeId == target.Id).PaletteIndex)).Color;
        Color Border(DialogueNode target) =>
            ((Avalonia.Media.SolidColorBrush)((Border)Card(editor, lineId, target.Id)!).BorderBrush!).Color;

        Assert.Equal(ColorOf(first), Border(first));
        Assert.Equal(ColorOf(second), Border(second));
        Assert.NotEqual(Border(first), Border(second));
    });

    [Fact]
    public void 카드가_포트_하나를_뚫는다() => HeadlessUi.Run(() =>
    {
        // 화면과 판이 같은 것을 말해야 한다 — 카드가 보이는데 포트가 없으면 이을 자리가 없다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Assert.NotNull(Card(editor, lineId));
        Assert.Single(
            NodeConnections.PortsOf(Node(session, nodeId), session.Project),
            port => port.Kind == ExitPortKind.Detour);
    });

    [Fact]
    public void 뚫린_포트가_그래프_카드에_실제로_선다() => HeadlessUi.Run(() =>
    {
        // ⛔ 여기가 소유자 그림의 핵심이다 — "연출그래프의 카드에 포트만 뚫리는데".
        //    판 투영이 에피소드 노드의 포트를 걸러 내면서 표식을 함께 떨어뜨리고 있었다
        //    (2026-09-17에 잡았다): 카드는 보이는데 이을 자리가 <b>없는</b> 상태였고,
        //    P-6 뒤로는 판의 카드가 전부 에피소드라 <b>한 자리도 안 보였다</b>.
        (_, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode made = session.Editor.AddBranchMarker(nodeId, lineId);

        Vn.Authoring.Graph.ExpandedNodeProjection card = Vn.Authoring.Graph.GraphProjectionBuilder
            .Build(session.Project, new HashSet<string>(session.Project.Files.Select(file => file.Id)))
            .Items.OfType<Vn.Authoring.Graph.ExpandedNodeProjection>()
            .Single(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));

        Vn.Authoring.Graph.GraphOutputPortProjection port = Assert.Single(
            card.OutputPorts,
            item => item.ExecutionPort?.Kind == ExitPortKind.Detour);

        // 이미 이어져 있다 — 사람이 한 번 더 끌 일이 없다.
        Assert.True(port.IsConnected);
        Assert.Equal(made.Id, port.ExecutionPort!.TargetNodeId);
    });

    [Fact]
    public void 포트_열쇠는_서로_겹치지_않는다() => HeadlessUi.Run(() =>
    {
        // ⚠ 표식과 IF 갈래는 <b>둘 다 열쇠가 LineId다</b>. 한 줄이 갈래도 열고 표식도 지면
        //    열쇠가 겹쳐 두 포트가 한 자리를 다투고 하나가 조용히 사라진다 — 그래서 표식은
        //    제 접두(`:detour:`)를 진다.
        (_, AuthoringSession session, string nodeId, string lineId) = Show();

        session.Editor.AddBranchMarker(nodeId, lineId);

        IReadOnlyList<Vn.Authoring.Graph.GraphOutputPortProjection> ports =
            Vn.Authoring.Graph.GraphProjectionBuilder
                .Build(session.Project, new HashSet<string>(session.Project.Files.Select(file => file.Id)))
                .Items.OfType<Vn.Authoring.Graph.ExpandedNodeProjection>()
                .Single(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal))
                .OutputPorts;

        Assert.Equal(
            ports.Count,
            ports.Select(port => port.Key).Distinct(StringComparer.Ordinal).Count());

        Assert.Contains(
            ports,
            port => port.ExecutionPort?.Kind == ExitPortKind.Detour &&
                    port.Key.Contains(":detour:", StringComparison.Ordinal));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Node(AuthoringSession session, string nodeId) =>
        (DialogueNode)session.Project.FindNode(nodeId)!;

    /// <summary>그 줄 앞의 첫 분기 카드. 없으면 <c>null</c>이다.</summary>
    private static Control? Card(DialogueNodeEditor editor, string lineId) =>
        editor.LineHost.Children.FirstOrDefault(child =>
            (child.Tag as string)?.StartsWith($"branch:{lineId}>", StringComparison.Ordinal) == true);

    private static Control? Card(DialogueNodeEditor editor, string lineId, string targetId) =>
        editor.LineHost.Children.FirstOrDefault(child =>
            Equals(child.Tag, DialogueNodeEditor.BranchMarkerTag(lineId, targetId)));

    /// <summary>카드 바로 다음에 오는 줄 카드 — 낀 자리가 <b>앞</b>인지 재는 데 쓴다.</summary>
    private static Control LineCardAfter(DialogueNodeEditor editor, Control card) =>
        editor.LineHost.Children[editor.LineHost.Children.IndexOf(card) + 1];

    private static IEnumerable<string> Texts(Control card) =>
        card.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty);

    private static void Rebuild(DialogueNodeEditor editor, string nodeId)
    {
        editor.Show(nodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>대사 한 줄짜리 에피소드 노드를 편집기에 띄운다.</summary>
    private (DialogueNodeEditor Editor, AuthoringSession Session, string NodeId, string LineId) Show()
    {
        var session = new AuthoringSession();
        session.Open(ManifestPath);

        session.Editor.EnsureChapter("ch01");
        string fileId = session.EnsureChapterBoard("ch01");

        DialogueNode node = session.Editor.AddDialogueNode(fileId, 0, 0, "root");
        string lineId = node.Script.ActiveLines.First().Id;

        var editor = new DialogueNodeEditor();
        var window = new Window { Width = 1200, Height = 800, Content = editor };
        window.Show();
        editor.Attach(session);
        editor.Show(node.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (editor, session, node.Id, lineId);
    }
}
