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
}
