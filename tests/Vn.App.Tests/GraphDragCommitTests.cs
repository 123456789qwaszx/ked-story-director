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
/// <b>연출 그래프의 끌기는 놓을 때 한 번 적는다</b> (D3 · 2026-10-29, <c>docs/design/two-graphs.md</c> §6-2).
///
/// 예전에는 포인터가 움직일 때마다 <c>MoveNode</c>로 프로젝트에 적었다 — 되돌리기는 없고, 움직일 때마다
/// dirty 판정과 챕터 그래프 재읽기가 돌았다. 이제 끄는 동안은 화면만 움직이고, 놓을 때
/// <c>MoveNodes</c> 한 번(되돌리기 한 칸)이다. 챕터 그래프의 끌기와 같은 손놀림이다.
///
/// 진짜 마우스(누름 → 움직임 → 놓음)로 잰다 — 처리기를 직접 부르면 "화면만"이 정말 화면만인지 모른다.
/// </summary>
public sealed class GraphDragCommitTests
{
    private const int GraphTab = 1;

    [Fact]
    public void 끄는_동안은_화면만_움직이고_놓을_때_한_번_적는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode dialogue) = Stage();
        int changes = 0;
        session.Changed += (_, _) => changes++;

        Point grip = CardGrip(window, dialogue.Id);
        window.MouseDown(grip, MouseButton.Left);
        window.MouseMove(grip + new Point(150, 120), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        // 화면은 따라왔는데 프로젝트는 그대로다.
        Assert.Equal((190, 160), CardSpot(window, dialogue.Id));
        Assert.Equal((40, 40), Layout(session, dialogue.Id));
        Assert.Equal(0, changes);

        window.MouseMove(grip + new Point(200, 300), RawInputModifiers.LeftMouseButton);
        window.MouseUp(grip + new Point(200, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((240, 340), Layout(session, dialogue.Id));
        Assert.Equal(1, changes);

        // 되돌리기 한 칸이면 끌기 전 자리다 — 카드도 함께 돌아간다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((40, 40), Layout(session, dialogue.Id));
        Assert.Equal((40, 40), CardSpot(window, dialogue.Id));

        window.Close();
    });

    [Fact]
    public void 눌렀다_놓기만_하면_되돌리기에_남지_않는다() => HeadlessUi.Run(() =>
    {
        // 카드를 고르는 클릭이 되돌리기 한 칸을 먹으면, 그 앞의 편집은 Ctrl+Z를 두 번 눌러야 돌아온다.
        (MainWindow window, AuthoringSession session, DialogueNode dialogue) = Stage();
        string lineId = dialogue.Script.ActiveLines.First().Id;
        session.Editor.SetScriptLineText(dialogue.Id, lineId, "라루", "고친 줄");

        Point grip = CardGrip(window, dialogue.Id);
        window.MouseDown(grip, MouseButton.Left);
        window.MouseUp(grip, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        session.Editor.Undo();

        // 되돌리기는 프로젝트를 새 객체로 갈아 끼운다 — 지금 프로젝트에서 다시 찾는다.
        Assert.Equal("첫 줄", session.Project.FindDialogue(dialogue.Id)!.Script.Text(lineId).Text);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static (double X, double Y) Layout(AuthoringSession session, string nodeId)
    {
        // 되돌리기는 스냅샷을 새로 푼다 — 노드는 Id로 다시 찾는다.
        StoryNode node = session.Project.FindNode(nodeId)!;
        return (node.Layout.X, node.Layout.Y);
    }

    private static Border Card(MainWindow window, string nodeId) =>
        window.FindControl<GraphEditorView>("Graph")!
            .FindControl<Canvas>("GraphCanvas")!
            .Children.OfType<Border>()
            .Single(border => (border.Tag as string) == nodeId);

    private static (double X, double Y) CardSpot(MainWindow window, string nodeId)
    {
        Border card = Card(window, nodeId);
        return (Canvas.GetLeft(card), Canvas.GetTop(card));
    }

    /// <summary>카드의 머리(이름 줄) — 여기를 잡으면 끌기다. 포트나 칸은 다른 일을 한다.</summary>
    private static Point CardGrip(MainWindow window, string nodeId)
    {
        Border card = Card(window, nodeId);
        return card.TranslatePoint(new Point(card.Bounds.Width / 2, 12), window)!.Value;
    }

    /// <summary>연출 그래프 탭에 선 창 하나 + (40, 40)에 선 대사 노드 하나.</summary>
    private static (MainWindow Window, AuthoringSession Session, DialogueNode Dialogue) Stage()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;

        DialogueNode dialogue = session.Editor.AddDialogueNode(
            session.ActiveFile!.Id, name: "본문");
        ScriptLine line = dialogue.Script.ActiveLines.First();   // 카드는 빈 첫 줄을 들고 선다
        session.Editor.SetScriptLineText(dialogue.Id, line.Id, "라루", "첫 줄");
        session.Editor.MoveNode(dialogue.Id, 40, 40);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        return (window, session, dialogue);
    }
}
