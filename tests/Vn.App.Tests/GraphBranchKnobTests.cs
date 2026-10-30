using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>분기 점은 글 칸에서 그 대사 옆에 서고, 한 대사의 분기들은 공통 점 하나로 접힌다</b> (2026-10-30 소유자: *"어떤
/// 대사에서 분기가 시작되는지, 대사의 정확한 위치에 분기 점이 생성되도록"* → *"특정 글자 옆쪽에 분기점 표시 하나만
/// 공통으로 남기고, 그걸 클릭하면, 선택지 분기를 갈라주는 것처럼 펼쳐지면서 개별 분기점을 끌어다 놓을 수 있게"*).
///
/// 분기가 하나면 그 점이 대사 옆, 카드 가장자리에 선다. 둘 이상이면 분기 수를 적은 공통 점 하나만 서고 선들도 거기서
/// 나간다. 공통 점을 누르면 가지가 갈라져 개별 점들이 카드 밖에 겹치지 않게 세로로 서고, 선은 저마다 제 점에서 나간다 —
/// 그 점을 끌어 다른 카드에 놓으면 그 분기만 다시 잇는다. 줄이 접히고 스크롤해도 실제 글자 배치로 잰다.
/// (⛔ 분기마다 대사 위에 빈 줄을 끼우던 것은 같은 날 걷혔다.)
/// </summary>
public sealed class GraphBranchKnobTests
{
    private const int GraphTab = 1;

    [Fact]
    public void 분기_하나의_점은_그_대사_옆_카드_가장자리에_서고_선도_거기서_나간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) =
            Stage("첫 줄", "둘째 줄", "셋째 줄", "넷째 줄");
        session.Editor.AddBranchMarker(card.Id, lines[2]);
        ShowGraph(window);

        Canvas canvas = Canvas(window);
        Ellipse knob = Assert.Single(Knobs(canvas, card.Id));
        Point center = Center(knob);

        Assert.True(knob.IsVisible);
        Assert.Equal(RowMiddle(window, card.Id, "셋째 줄"), center.Y, 1.5);
        Assert.Equal(CardLeft(canvas, card.Id) + CardWidthOf(canvas, card.Id), center.X, 1.5);
        Assert.Empty(Hubs(canvas, card.Id));
        AssertEdgeLeaves(canvas, center, PortRadius);

        // 글 칸은 분기가 있어도 커지지 않는다 — 줄 사이에 자리를 끼우지 않는다.
        Assert.Equal(88, WritingBox(window, card.Id).Bounds.Height, 0.5);
        Assert.DoesNotContain("\n\n", WritingBox(window, card.Id).Text, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 한_대사의_분기_둘은_공통_점_하나로_접히고_선도_거기서_나간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) =
            Stage("첫 줄", "둘째 줄", "셋째 줄");
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        ShowGraph(window);

        Canvas canvas = Canvas(window);
        Border hub = Assert.Single(Hubs(canvas, card.Id));
        Point center = Center(hub);

        Assert.Equal("2", Assert.IsType<TextBlock>(hub.Child).Text);
        Assert.Equal(RowMiddle(window, card.Id, "둘째 줄"), center.Y, 1.5);
        Assert.Equal(CardLeft(canvas, card.Id) + CardWidthOf(canvas, card.Id), center.X, 1.5);

        // 개별 점은 숨고, 두 선 모두 공통 점에서 나간다.
        Assert.All(Knobs(canvas, card.Id), knob => Assert.False(knob.IsVisible));
        Assert.Equal(2, ExecutionEdges(canvas).Count(path => Leaves(path, center, HubRadius)));

        window.Close();
    });

    [Fact]
    public void 공통_점을_누르면_개별_점이_카드_밖으로_갈라져_서고_다시_누르면_접힌다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) =
            Stage("첫 줄", "둘째 줄", "셋째 줄");
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        ShowGraph(window);

        Canvas canvas = Canvas(window);
        Point hub = Center(Assert.Single(Hubs(canvas, card.Id)));
        Click(window, canvas, hub);

        Point[] knobs = Knobs(canvas, card.Id).Select(Center).OrderBy(point => point.Y).ToArray();
        Assert.All(Knobs(canvas, card.Id), knob => Assert.True(knob.IsVisible));
        Assert.Equal(3, knobs.Length);

        // 카드 밖에 세로로 — 가운데 점이 공통 점의 높이이고, 점끼리 겹치지 않는다(지름 12).
        Assert.All(knobs, knob => Assert.True(knob.X > hub.X + 20, "카드 밖으로 갈라진다"));
        Assert.Equal(hub.Y, knobs[1].Y, 1.5);
        Assert.True(knobs[1].Y - knobs[0].Y >= 16 && knobs[2].Y - knobs[1].Y >= 16, "점이 겹치지 않는다");

        // 공통 점에서 가지가 갈라져 개별 점으로 간다 · 선은 저마다 제 점에서 나간다.
        Assert.True(canvas.Children.OfType<Path>().Single(path =>
            (path.Tag as string) == $"{GraphEditorView.BranchHubTag}{card.Id}:{lines[1]}:fan").IsVisible);
        Assert.All(knobs, knob => Assert.Single(ExecutionEdges(canvas), path => Leaves(path, knob, PortRadius)));

        // 판을 다시 그려도(점 · 공통 점이 새로 선다) 펼친 채로 남는다.
        Ellipse before = Knobs(canvas, card.Id).First();
        window.FindControl<GraphEditorView>("Graph")!.Rebuild();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(before, Knobs(canvas, card.Id));
        Assert.All(Knobs(canvas, card.Id), knob => Assert.True(knob.IsVisible));

        // 다시 누르면 접힌다.
        Click(window, canvas, Center(Assert.Single(Hubs(canvas, card.Id))));
        Assert.All(Knobs(canvas, card.Id), knob => Assert.False(knob.IsVisible));

        window.Close();
    });

    [Fact]
    public void 펼친_점을_끌어_다른_카드에_놓으면_그_분기만_다시_잇는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) =
            Stage("첫 줄", "둘째 줄");
        DialogueNode first = session.Editor.AddBranchMarker(card.Id, lines[1]);
        DialogueNode second = session.Editor.AddBranchMarker(card.Id, lines[1]);
        DialogueNode elsewhere = session.Editor.AddDialogueNode(session.ActiveFile!.Id, name: "딴 곳");
        session.Editor.MoveNode(elsewhere.Id, 60, 520);
        ShowGraph(window);

        Canvas canvas = Canvas(window);
        Click(window, canvas, Center(Assert.Single(Hubs(canvas, card.Id))));

        // 아래 점(두 번째 분기)을 끌어 딴 카드에 놓는다.
        Ellipse lower = Knobs(canvas, card.Id).OrderBy(knob => Center(knob).Y).Last();
        Assert.EndsWith(second.Id, (string)lower.Tag!, StringComparison.Ordinal);
        Point from = canvas.TranslatePoint(Center(lower), window)!.Value;
        Border target = canvas.Children.OfType<Border>().Single(border => border.Tag as string == elsewhere.Id);
        Point to = target.TranslatePoint(new Point(target.Bounds.Width / 2, 12), window)!.Value;

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([first.Id, elsewhere.Id], card.FindExtension(lines[1])!.DetourTargetNodeIds);

        window.Close();
    });

    [Fact]
    public void 칸_밖으로_스크롤된_대사의_분기_점은_칸_끝에_붙고_스크롤하면_따라간다() => HeadlessUi.Run(() =>
    {
        // 글 칸은 다섯 줄 남짓이라 긴 대본은 스크롤한다 — 안 보이는 줄의 점이 칸 밖(다른 카드 위)에 서면 안 된다.
        string[] texts = Enumerable.Range(1, 14).Select(number => $"{number}번째 줄").ToArray();
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) = Stage(texts);
        session.Editor.AddBranchMarker(card.Id, lines[12]);
        ShowGraph(window);

        Canvas canvas = Canvas(window);
        TextBox box = WritingBox(window, card.Id);
        double boxBottom = box.TranslatePoint(new Point(0, box.Bounds.Height), canvas)!.Value.Y;
        Ellipse knob = Assert.Single(Knobs(canvas, card.Id));

        Assert.True(Center(knob).Y <= boxBottom && Center(knob).Y > boxBottom - 12, "칸 아래 끝에 붙는다");
        Assert.True(knob.Opacity < 1, "칸 밖의 대사라 흐리다");

        box.FindDescendantOfType<ScrollViewer>()!.Offset = new Vector(0, 10_000);
        Dispatcher.UIThread.RunJobs();

        knob = Assert.Single(Knobs(canvas, card.Id));
        Assert.Equal(RowMiddle(window, card.Id, "13번째 줄"), Center(knob).Y, 1.5);
        Assert.Equal(1, knob.Opacity);

        window.Close();
    });

    [Fact]
    public void 카드를_끄는_동안_분기_점도_함께_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) =
            Stage("첫 줄", "둘째 줄");
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        ShowGraph(window);

        Canvas canvas = Canvas(window);
        Point before = Center(Assert.Single(Knobs(canvas, card.Id)));
        Border visual = canvas.Children.OfType<Border>().Single(border => border.Tag as string == card.Id);
        Point grip = visual.TranslatePoint(new Point(visual.Bounds.Width / 2, 12), window)!.Value;

        window.MouseDown(grip, MouseButton.Left);
        window.MouseMove(grip + new Point(150, 90), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Point during = Center(Assert.Single(Knobs(canvas, card.Id)));
        Assert.Equal(before.X + 150, during.X, 1.5);
        Assert.Equal(before.Y + 90, during.Y, 1.5);

        window.MouseUp(grip + new Point(150, 90), MouseButton.Left);
        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>연출 그래프에 대사 카드 하나 — 줄마다 글을 채운다.</summary>
    private static (MainWindow Window, AuthoringSession Session, DialogueNode Card, List<string> Lines) Stage(
        params string[] texts)
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        DialogueNode card = session.Editor.AddDialogueNode(session.ActiveFile!.Id, name: "본문");
        var lines = new List<string> { card.Script.ActiveLines.Single().Id };   // 카드는 빈 첫 줄을 들고 선다

        while (lines.Count < texts.Length)
        {
            lines.Add(session.Editor.InsertScriptLine(card.Id).Id);
        }

        for (int index = 0; index < texts.Length; index++)
        {
            session.Editor.SetScriptLineText(card.Id, lines[index], string.Empty, texts[index]);
        }

        session.Editor.MoveNode(card.Id, 60, 60);
        return (window, session, card, lines);
    }

    private static void ShowGraph(MainWindow window)
    {
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();
    }

    private static Canvas Canvas(MainWindow window) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!;

    private static IEnumerable<Ellipse> Knobs(Canvas canvas, string nodeId) =>
        canvas.Children.OfType<Ellipse>().Where(knob => (knob.Tag as string)?.StartsWith(
            $"{GraphEditorView.LineKnobTag}execution:{nodeId}:detour:", StringComparison.Ordinal) == true);

    private static IEnumerable<Border> Hubs(Canvas canvas, string nodeId) =>
        canvas.Children.OfType<Border>().Where(hub => (hub.Tag as string)?.StartsWith(
            $"{GraphEditorView.BranchHubTag}{nodeId}:", StringComparison.Ordinal) == true);

    private const double PortRadius = 6;
    private const double HubRadius = 8;

    private static Point Center(Control dot) =>
        new(Avalonia.Controls.Canvas.GetLeft(dot) + (dot.Width / 2), Avalonia.Controls.Canvas.GetTop(dot) + (dot.Height / 2));

    /// <summary>실행 선(굵기 2) — 분기 선과 IF 갈래 선.</summary>
    private static IEnumerable<Path> ExecutionEdges(Canvas canvas) =>
        canvas.Children.OfType<Path>().Where(path => path.IsVisible && path.StrokeThickness == 2);

    /// <summary>그 선이 점(반지름 <paramref name="radius"/>)의 가운데 높이에서 바깥으로 가로로 나가는가.</summary>
    private static bool Leaves(Path path, Point center, double radius) =>
        path.Data?.StrokeContains(new Pen(Brushes.Black, 2), new Point(center.X + radius + 2, center.Y)) == true;

    private static void AssertEdgeLeaves(Canvas canvas, Point center, double radius) =>
        Assert.Contains(ExecutionEdges(canvas), path => Leaves(path, center, radius));

    /// <summary>판 위의 그 자리를 왼쪽 단추로 누른다.</summary>
    private static void Click(MainWindow window, Canvas canvas, Point onCanvas)
    {
        Point spot = canvas.TranslatePoint(onCanvas, window)!.Value;
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static double CardLeft(Canvas canvas, string nodeId) =>
        Avalonia.Controls.Canvas.GetLeft(canvas.Children.OfType<Border>().Single(border => border.Tag as string == nodeId));

    private static double CardWidthOf(Canvas canvas, string nodeId) =>
        canvas.Children.OfType<Border>().Single(border => border.Tag as string == nodeId).Bounds.Width;

    private static TextBox WritingBox(MainWindow window, string nodeId) =>
        Canvas(window).Children.OfType<Border>().Single(border => border.Tag as string == nodeId)
            .GetVisualDescendants().OfType<TextBox>().Single();

    /// <summary>글 칸에서 그 글이 선 화면 줄의 가운데 높이 — 판 좌표.</summary>
    private static double RowMiddle(MainWindow window, string nodeId, string text)
    {
        TextBox box = WritingBox(window, nodeId);
        TextPresenter presenter = box.FindDescendantOfType<TextPresenter>()!;
        int at = box.Text!.IndexOf(text, StringComparison.Ordinal);
        Rect caret = presenter.TextLayout.HitTestTextPosition(at);

        return presenter.TranslatePoint(new Point(0, caret.Y + (caret.Height / 2)), Canvas(window))!.Value.Y;
    }
}
