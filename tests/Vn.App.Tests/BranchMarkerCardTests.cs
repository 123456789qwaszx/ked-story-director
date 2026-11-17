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
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>「조건 분기」 카드 — 줄 사이에 낀다</b>.
///
/// <b>내용이 없다.</b> 연출 그래프는 "어디서 갈라지는가"만 짚는다 —
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
        // <b>앞</b>이다 — 그 줄에 붙은 것이 아니라 그 줄 앞에서 갈라진다는 표식이다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode made = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Border card = Assert.IsType<Border>(Card(editor, lineId));

        Assert.True(
            editor.LineHost.Children.IndexOf(card) <
            editor.LineHost.Children.IndexOf(LineCardAfter(editor, card)));

        // 분기 카드는 제목으로 보인다(처음 「새 분기」 — ID는 [구성]에서 본다, 2026-11-17).
        Assert.Contains(Texts(card), text => text.Contains(NewNames.Branch, StringComparison.Ordinal));
        Assert.DoesNotContain(Texts(card), text => text.Contains(made.Name, StringComparison.Ordinal));
    });

    [Fact]
    public void 부르는_쪽_분기_상자에는_고르는_칸이_없고_조건은_분기_카드_위쪽_한_칸에서_고른다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 분기는 무조건 들른 뒤 그 카드에서 if로 보므로 조건은 분기 카드에서 고른다(대본 위쪽 한 칸). 부르는 쪽 분기
        // 상자는 지금 조건을 글로만 보인다. 식을 적는 칸은 어디에도 없다 — 식은 [구성] 탭이다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode made = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Border card = Assert.IsType<Border>(Card(editor, lineId));
        Assert.Empty(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(card).OfType<ComboBox>());
        Assert.False(editor.BranchConditionHost.IsVisible);   // 부르는 쪽은 분기 카드가 아니다

        Rebuild(editor, made.Id);

        Assert.True(editor.BranchConditionHost.IsVisible);
        Assert.Single(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(editor.BranchConditionHost).OfType<ComboBox>());
        Assert.Empty(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(editor.BranchConditionHost).OfType<TextBox>());
    });

    [Fact]
    public void 분기_지우기는_분기만_걷고_그_에피소드는_남긴다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 분기 지우기는 분기점과 선만 걷는다. 「분기 추가」가 세운 카드도 남고, 상태줄이 그렇게 말한다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode made = session.Editor.AddBranchMarker(nodeId, lineId);
        Rebuild(editor, nodeId);

        Button remove = Assert.IsType<Border>(Card(editor, lineId))
            .GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.Contains('✕') == true);

        remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(Node(session, nodeId).FindExtension(lineId)?.DetourTargetNodeIds ?? []);
        Assert.NotNull(session.Project.FindNode(made.Id));
        Assert.Contains("판에 남아 있습니다", session.StatusMessage, StringComparison.Ordinal);

        Rebuild(editor, nodeId);
        Assert.Null(Card(editor, lineId));
    });

    [Fact]
    public void 한_줄에_분기가_둘이면_카드_둘이_다녀오는_차례로_낀다() => HeadlessUi.Run(() =>
    {
        // 한 줄에 분기가 여럿 있을 수 있다 — 위에서부터 차례로 다녀온다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId, string lineId) = Show();

        DialogueNode first = session.Editor.AddBranchMarker(nodeId, lineId);
        DialogueNode second = session.Editor.AddBranchMarker(nodeId, lineId);
        session.Editor.SetBranchCardTitle(first.Id, "첫 갈래");
        session.Editor.SetBranchCardTitle(second.Id, "둘째 갈래");
        Rebuild(editor, nodeId);

        Control firstCard = Card(editor, lineId, first.Id)!;
        Control secondCard = Card(editor, lineId, second.Id)!;
        int at = editor.LineHost.Children.IndexOf(firstCard);

        Assert.Equal(at + 1, editor.LineHost.Children.IndexOf(secondCard));
        Assert.DoesNotContain("branch:", editor.LineHost.Children[at + 2].Tag as string ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(Texts(firstCard), text => text.Contains("1/2", StringComparison.Ordinal));
        Assert.Contains(Texts(secondCard), text => text.Contains("2/2", StringComparison.Ordinal));
        Assert.Contains(Texts(firstCard), text => text.Contains("첫 갈래", StringComparison.Ordinal));
        Assert.Contains(Texts(secondCard), text => text.Contains("둘째 갈래", StringComparison.Ordinal));
        Assert.Equal(
            2,
            NodeConnections.PortsOf(Node(session, nodeId), session.Project).Count(port => port.Kind == ExitPortKind.Detour));

        // 첫 카드의 ✕는 그 분기 하나만 걷는다.
        firstCard.GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.Contains('✕') == true)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal([second.Id], Node(session, nodeId).FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.NotNull(session.Project.FindNode(first.Id));   // 카드는 남는다(2026-11-17)
    });

    [Fact]
    public void 분기_카드를_우클릭하면_그_분기만_지워진다() => HeadlessUi.Run(() =>
    {
        // 대사 편집기의 분기 카드를 우클릭해 그 분기를 지운다 — 연출 그래프에서 분기 선을
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
        Assert.NotNull(session.Project.FindNode(second.Id));   // 카드는 남는다(2026-11-17)
        Assert.Contains("판에 남아 있습니다", session.StatusMessage, StringComparison.Ordinal);

        // 되돌리기 한 번이면 돌아온다.
        session.Editor.Undo();
        Assert.Equal([first.Id, second.Id], Node(session, nodeId).FindExtension(lineId)!.DetourTargetNodeIds);
    });

    [Fact]
    public void 분기_카드의_색은_연출_그래프의_그_분기_포트_색이다() => HeadlessUi.Run(() =>
    {
        // 카드마다 한 색으로 칠하지 않고, 에피소드 노드에 생기는 그 분기 포트의 색과 같게 칠해 구분한다.
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
        // 여기가 핵심이다 — 연출 그래프의 카드에는 분기 포트가 뚫려야 한다.
        //    판 투영이 에피소드 노드의 포트를 걸러 낼 때 표식까지 떨어뜨리면 카드는 보이는데
        //    이을 자리가 <b>없다</b>. 판의 카드는 전부 에피소드라 <b>한 자리도 안 보이게 된다</b>.
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
        // 주의: 표식과 IF 갈래는 <b>둘 다 열쇠가 LineId다</b>. 한 줄이 갈래도 열고 표식도 지면
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
