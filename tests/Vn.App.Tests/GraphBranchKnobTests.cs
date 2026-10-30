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
/// <b>분기 점은 글 칸의 그 대사 높이에 선다</b> (2026-10-30 소유자: *"현재는 분기가 대사 적는 공간 위쪽에서 분기를 표시하는
/// 데, 대신에 어떤 대사에서 분기가 시작되는지, 대사의 정확한 위치에 분기 점이 생성되도록"*).
///
/// 전에는 「분기」 포트가 카드 머리 밑 포트 줄에 서서, 카드만 봐서는 어느 대사 앞에서 갈라지는지 알 수 없었다. 이제 점이
/// 카드 가장자리, 글 칸에서 <b>그 대사가 선 줄</b>의 높이에 서고 선도 거기서 나간다. 줄이 접히고 스크롤해도 실제 글자
/// 배치로 잰다.
/// </summary>
public sealed class GraphBranchKnobTests
{
    private const int GraphTab = 1;

    [Fact]
    public void 분기_점은_그_대사_줄의_높이_카드_가장자리에_서고_선도_거기서_나간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) =
            Stage("첫 줄", "둘째 줄", "셋째 줄", "넷째 줄");
        session.Editor.AddBranchMarker(card.Id, lines[2]);
        ShowGraph(window);

        Canvas canvas = Canvas(window);
        Ellipse knob = Assert.Single(Knobs(canvas, card.Id));
        Point center = Center(knob);

        Assert.Equal(RowMiddle(window, card.Id, "셋째 줄"), center.Y, 1.5);
        Assert.Equal(CardLeft(canvas, card.Id) + CardWidthOf(canvas, card.Id), center.X, 1.5);

        // 선은 점의 가운데에서 가로로 나간다.
        var pen = new Pen(Brushes.Black, 2);
        Assert.Contains(
            canvas.Children.OfType<Path>(),
            path => path.StrokeThickness == 2 && path.Data?.StrokeContains(pen, new Point(center.X + 3, center.Y)) == true);

        window.Close();
    });

    [Fact]
    public void 같은_줄의_분기_둘은_그_줄을_가운데로_위아래에_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, List<string> lines) =
            Stage("첫 줄", "둘째 줄", "셋째 줄");
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        session.Editor.AddBranchMarker(card.Id, lines[1]);
        ShowGraph(window);

        double[] ys = Knobs(Canvas(window), card.Id).Select(knob => Center(knob).Y).Order().ToArray();

        Assert.Equal(2, ys.Length);
        Assert.True(ys[1] - ys[0] >= 10, "두 점이 겹치지 않게 비켜 선다");
        Assert.Equal(RowMiddle(window, card.Id, "둘째 줄"), (ys[0] + ys[1]) / 2, 1.5);

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

    private static Point Center(Ellipse knob) =>
        new(Avalonia.Controls.Canvas.GetLeft(knob) + (knob.Width / 2), Avalonia.Controls.Canvas.GetTop(knob) + (knob.Height / 2));

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
        Rect caret = presenter.TextLayout.HitTestTextPosition(box.Text!.IndexOf(text, StringComparison.Ordinal));

        return presenter.TranslatePoint(new Point(0, caret.Y + (caret.Height / 2)), Canvas(window))!.Value.Y;
    }
}
