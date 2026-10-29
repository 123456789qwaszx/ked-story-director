using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.App.Tests;

/// <summary>
/// <b>연출 그래프 카드의 머리는 제목 한 줄이다</b> (2026-10-29 소유자: *"노드 종류, 대본 이름, 줄 수가 표시되는데 이 정보는
/// 필요없으니까 치워"*).
///
/// 제목 아래에 「대사 · 📄 에피소드 · {대본} · k줄」 부제가 있었다. 걷으면서 머리 높이를 고정했다 — 포트 줄의 자리를 그 값으로
/// 세므로, 글꼴에 맡겨 두면 선 끝이 점에서 비껴 닿는다(실제로 부제가 있던 머리는 5px 어긋나 있었다).
/// </summary>
public sealed class GraphCardHeaderTests
{
    private const int GraphTab = 1;

    [Fact]
    public void 카드에는_노드_종류도_대본_이름도_줄_수도_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, _, DialogueNode source, _) = Show();
        Border card = Card(window, source.Id);

        IReadOnlyList<string> texts = card.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.FindAncestorOfType<TextBox>() is null)
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        Assert.Contains("본문", texts);
        Assert.DoesNotContain(texts, text => text.Contains("대본", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, text => text.Contains("줄", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, text => text.StartsWith("대사", StringComparison.Ordinal));

        window.Close();
    });

    [Fact]
    public void 포트에서_나가는_선은_점의_가운데를_지난다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, Canvas canvas, DialogueNode source, _) = Show();
        Border card = Card(window, source.Id);

        // 분기 포트의 점 — 이어져 있어 칠해져 있다.
        Ellipse knob = card.GetVisualDescendants().OfType<Ellipse>()
            .Single(dot => dot.FindAncestorOfType<Grid>()?.Children.OfType<TextBlock>()
                .Any(text => text.Text == "분기") == true);
        Point center = knob.TranslatePoint(new Point(knob.Bounds.Width / 2, knob.Bounds.Height / 2), canvas)!.Value;

        // 실행 선(굵기 2)은 카드 오른쪽 가장자리에서 점의 높이로 가로로 나간다 — 머리 높이를 잘못 세면 몇 px 아래로
        // 비껴 간다(부제가 있던 머리는 5px). 점 바로 오른쪽의 가로 마디로 잰다.
        var pen = new Pen(Brushes.Black, 2);
        var justRight = new Point(center.X + 3, center.Y);
        Assert.Contains(
            canvas.Children.OfType<Avalonia.Controls.Shapes.Path>(),
            path => path.StrokeThickness == 2 && path.Data?.StrokeContains(pen, justRight) == true);

        window.Close();
    });

    private static (MainWindow Window, Canvas Canvas, DialogueNode Source, DialogueNode Made) Show()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        DialogueNode source = session.Editor.AddDialogueNode(session.ActiveFile!.Id, name: "본문");
        ScriptLine line = source.Script.ActiveLines.First();
        session.Editor.SetScriptLineText(source.Id, line.Id, "라루", "첫 줄");
        DialogueNode made = session.Editor.AddBranchMarker(source.Id, line.Id);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!;

        return (window, canvas, source, made);
    }

    private static Border Card(MainWindow window, string nodeId) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!
            .Children.OfType<Border>()
            .Single(border => border.Tag as string == nodeId);
}
