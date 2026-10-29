using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.App.Tests;

/// <summary>
/// <b>연출 그래프에서 분기 선을 지우면 분기가 지워진다</b> (2026-10-29 소유자: *"조건 분기를 추가는 가능한데,
/// 현재는 지우는게 불가능 합니다. 지울 수 있도록 해주시고"*).
///
/// 선 우클릭(삭제)은 예전에 표식만 걷고 「분기 추가」가 세운 카드를 판에 남겼다 — 판에는 그 카드가 그대로라
/// 사람은 지워지지 않았다고 본다. 이제 분기 카드의 [✕ 분기 지우기]와 같은 길(<c>RemoveBranchMarker</c>)이다.
/// 진짜 마우스로 잰다.
/// </summary>
public sealed class GraphBranchEdgeTests
{
    private const int GraphTab = 1;

    [Fact]
    public void 분기_선을_우클릭하면_표식과_다녀가던_카드가_함께_지워진다() => HeadlessUi.Run(() =>
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        DialogueNode source = session.Editor.AddDialogueNode(
            session.ActiveFile!.Id, name: "본문");
        ScriptLine line = source.Script.ActiveLines.First();   // 카드는 빈 첫 줄을 들고 선다
        session.Editor.SetScriptLineText(source.Id, line.Id, "라루", "첫 줄");
        DialogueNode made = session.Editor.AddBranchMarker(source.Id, line.Id);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        // 선의 이름표(「분기」)가 판에 직접 선다 — 카드 안 포트의 글자와 달리 캔버스의 자식이다.
        Border label = window.FindControl<GraphEditorView>("Graph")!
            .FindControl<Canvas>("GraphCanvas")!
            .Children.OfType<Border>()
            .Single(border => border.IsVisible && border.Child is TextBlock { Text: "분기" });

        Point spot = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
        window.MouseDown(spot, MouseButton.Right);
        window.MouseUp(spot, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(((DialogueNode)session.Project.FindNode(source.Id)!).FindExtension(line.Id)?.DetourTargetNodeIds ?? []);
        Assert.Null(session.Project.FindNode(made.Id));

        // 되돌리기 한 번이면 둘이 함께 돌아온다.
        session.Editor.Undo();
        Assert.NotNull(session.Project.FindNode(made.Id));
        Assert.Equal([made.Id], ((DialogueNode)session.Project.FindNode(source.Id)!).FindExtension(line.Id)?.DetourTargetNodeIds ?? []);

        window.Close();
    });

    [Fact]
    public void 같은_줄의_분기_카드를_끌어도_다른_분기_선이_제자리에_남는다() => HeadlessUi.Run(() =>
    {
        // 2026-10-29 소유자 보고: "생성된 분기를 드래그 할 경우에, 여러개의 분기 중에 가장 아래껏을 제외하고는
        // 끊기는 버그". 끄는 동안 화면은 선을 열쇠로 다시 찾는데, 같은 줄의 분기 선들이 한 열쇠(줄)를 나눠 가져
        // 모두 첫 선의 경로를 받았다.
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        DialogueNode source = session.Editor.AddDialogueNode(session.ActiveFile!.Id, name: "본문");
        ScriptLine line = source.Script.ActiveLines.First();
        session.Editor.SetScriptLineText(source.Id, line.Id, "라루", "첫 줄");
        session.Editor.AddBranchMarker(source.Id, line.Id);
        DialogueNode second = session.Editor.AddBranchMarker(source.Id, line.Id);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!;
        (double X, double Y)[] before = BranchLabels(canvas);
        Assert.Equal(2, before.Distinct().Count());

        Border card = canvas.Children.OfType<Border>().Single(border => (border.Tag as string) == second.Id);
        Point grip = card.TranslatePoint(new Point(card.Bounds.Width / 2, 12), window)!.Value;
        window.MouseDown(grip, MouseButton.Left);
        window.MouseMove(grip + new Point(120, 160), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        // 끄는 동안: 선 둘이 각자 제 카드로 간다 — 끌지 않은 분기의 선은 그대로다.
        (double X, double Y)[] during = BranchLabels(canvas);
        Assert.Equal(2, during.Distinct().Count());
        Assert.Contains(before[0], during);

        window.MouseUp(grip + new Point(120, 160), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, BranchLabels(canvas).Distinct().Count());
        Assert.Equal(
            2,
            ((DialogueNode)session.Project.FindNode(source.Id)!).FindExtension(line.Id)!.DetourTargetNodeIds.Count);

        window.Close();
    });

    /// <summary>판에 선 분기 선 이름표들의 자리, 위에서부터.</summary>
    private static (double X, double Y)[] BranchLabels(Canvas canvas) =>
        canvas.Children.OfType<Border>()
            .Where(border => border.IsVisible && border.Child is TextBlock { Text: "분기" })
            .Select(border => (Canvas.GetLeft(border), Canvas.GetTop(border)))
            .OrderBy(spot => spot.Item2)
            .ToArray();
}
