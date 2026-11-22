using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>작가용 글 서식</b> (2026-11-22 소유자 · <c>docs/plans/writer-formatting.md</c>) — [대본] 위쪽 줄의 도구 막대로 카드 글 칸의 고른 글자에 굵게 · 기울임 ·
/// 취소선 · 글자색 · 바탕색 · 글꼴 · 크기를, 카드에 정렬을 입힌다. 글 칸은 Avalonia의 TextBox 그대로이고(한글 조합 · 커서 · 되돌리기를 손대지 않는다) 글자를
/// 놓는 겹만 서식을 그린다. 판을 다시 짓지 않고 [구성]도 다시 읽지 않는다 — 서식은 내보내기에 나가지 않는다.
/// </summary>
public sealed class WriterFormattingTests
{
    [Fact]
    public void 고른_글자에_굵게를_입히면_대사_글_기준으로_적히고_칸에_그려지며_판은_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);
        Assert.False(graph.FormatToolbarEnabled);

        WriterTextBox box = Box(window, card.Id);
        int rails = graph.RailDrawCount;
        int reloads = window.FindControl<ChapterGraphView>("ChapterGraph")!.ReloadCount;

        // 「긴 하루」를 고른다 — 칸의 글에서는 「하린: 」 뒤라 8..12.
        Select(box, "긴 하루");
        Assert.True(graph.FormatToolbarEnabled);
        Click(window, "굵게");

        DialogueNode now = session.Project.FindDialogue(card.Id)!;
        Assert.Equal([new WriterSpan(4, 4, new WriterLook(Bold: true))], now.WriterMarks[ids[0]]);
        Assert.Equal([new BoxLook(8, 4, new WriterLook(Bold: true))], box.Looks);
        Assert.Equal((true, false, false), (graph.FormatToolbarState.Bold, graph.FormatToolbarState.Italic, graph.FormatToolbarState.Strike));

        // 판도 칸도 그대로다 — 철도를 다시 긋지 않았고 칸이 같은 객체로 초점과 고른 범위를 쥐고 있다. [구성]은 다시 읽지 않았다.
        Assert.Same(box, Box(window, card.Id));
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Equal(reloads, window.FindControl<ChapterGraphView>("ChapterGraph")!.ReloadCount);
        Assert.True(box.IsFocused);
        Assert.Equal((8, 12), (box.SelectionStart, box.SelectionEnd));

        // 굵은 글자는 실제로 더 넓게 놓인다 — 글자 배치가 서식을 입었다(커서 · 누른 자리도 이 배치를 쓴다).
        TextPresenterWidths(box, out double boldWidth);
        Assert.True(boldWidth > 0);

        // 모두 굵다 — 한 번 더 누르면 푼다.
        Click(window, "굵게");
        Assert.False(session.Project.FindDialogue(card.Id)!.WriterMarks.ContainsKey(ids[0]));
        Assert.Empty(box.Looks);

        window.Close();
    });

    [Fact]
    public void 색_바탕색_글꼴_크기_정렬을_입히고_되돌리기가_돌린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어");
        GraphEditorView graph = Graph(window);
        WriterTextBox box = Box(window, card.Id);

        Select(box, "오늘은");
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Color, Color: WriterColor.Red));
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Fill, Color: WriterColor.Yellow));
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Font, Font: "Noto Serif"));
        Click(window, "글자 크게");
        Click(window, "글자 크게");
        Click(window, "가운데 정렬");
        Dispatcher.UIThread.RunJobs();

        var expected = new WriterLook(Color: WriterColor.Red, Fill: WriterColor.Yellow, Font: "Noto Serif", Grow: 2);
        Assert.Equal([new WriterSpan(0, 3, expected)], session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]]);
        Assert.Equal(WriterAlign.Center, session.Project.FindDialogue(card.Id)!.WriterAlign);
        Assert.Equal(TextAlignment.Center, box.TextAlignment);
        Assert.Equal($"{ScriptFont.Size + 2:0}", graph.FormatToolbarState.Size);
        Assert.Equal("Noto Serif", graph.FormatToolbarState.Font);

        // 되돌리기 — 정렬 · 크기 두 번 · 글꼴 · 바탕색을 차례로 걷는다. 칸의 꾸밈이 모델을 따라온다.
        for (int step = 0; step < 5; step++)
        {
            session.Editor.Undo();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(TextAlignment.Left, Box(window, card.Id).TextAlignment);
        Assert.Equal([new BoxLook(4, 3, new WriterLook(Color: WriterColor.Red))], Box(window, card.Id).Looks);

        session.Editor.Redo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([new BoxLook(4, 3, new WriterLook(Color: WriterColor.Red, Fill: WriterColor.Yellow))], Box(window, card.Id).Looks);

        window.Close();
    });

    [Fact]
    public void 쓰는_동안_서식이_같은_글자를_따라가고_넣으면_모델도_같다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        WriterTextBox box = Box(window, card.Id);
        Select(box, "긴 하루");
        Click(window, "굵게");

        // 앞에 「아, 」를 끼우고 굵은 「하루」 끝에 「들」을 이어 친다 — 넣기 전에도 칸의 꾸밈이 따라간다.
        box.Text = "하린: 아, 오늘은 긴 하루들였어\n문이 열린다.";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([new BoxLook(11, 5, new WriterLook(Bold: true))], box.Looks);

        // 넣으면(쓰다 멈춤) 모델도 같은 글자다.
        Graph(window).CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("아, 오늘은 긴 하루들였어", session.Project.FindDialogue(card.Id)!.Script.Text(ids[0]).Text);
        Assert.Equal([new WriterSpan(7, 5, new WriterLook(Bold: true))], session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]]);
        Assert.Equal([new BoxLook(11, 5, new WriterLook(Bold: true))], Box(window, card.Id).Looks);

        // 위에 줄을 새로 넣어 카드를 다시 지어도 서식은 그 줄을 따라간다.
        Box(window, card.Id).Text = "새 줄\n하린: 아, 오늘은 긴 하루들였어\n문이 열린다.";
        Graph(window).CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();
        WriterTextBox rebuilt = Box(window, card.Id);
        Assert.Equal([new BoxLook(4 + 11, 5, new WriterLook(Bold: true))], rebuilt.Looks);

        window.Close();
    });

    [Fact]
    public void 넣지_않은_글이_있으면_먼저_넣고_서식을_입히며_화자와_여러_줄을_가른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 첫 줄", "둘째 줄");
        WriterTextBox box = Box(window, card.Id);

        // 새 줄을 쓰고(아직 안 넣었다) 처음부터 끝까지 고른다 — 화자 「하린: 」은 빠지고 세 줄의 대사 글에 입는다.
        box.Focus();
        box.Text = "하린: 첫 줄\n둘째 줄\n셋째 줄";
        Dispatcher.UIThread.RunJobs();
        box.SelectionStart = 0;
        box.SelectionEnd = box.Text!.Length;
        Dispatcher.UIThread.RunJobs();
        Click(window, "취소선");

        DialogueNode now = session.Project.FindDialogue(card.Id)!;
        string[] ids = now.Script.ActiveLines.Select(line => line.Id).ToArray();
        Assert.Equal(3, ids.Length);
        Assert.Equal(["첫 줄", "둘째 줄", "셋째 줄"], ids.Select(id => now.Script.Text(id).Text));

        foreach (string id in ids)
        {
            Assert.Equal([new WriterSpan(0, now.Script.Text(id).Text.Length, new WriterLook(Strike: true))], now.WriterMarks[id]);
        }

        // 되돌리기 한 번이면 서식만 걷힌다(넣기는 그 전의 한 칸이다).
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(session.Project.FindDialogue(card.Id)!.WriterMarks);
        Assert.Equal(3, session.Project.FindDialogue(card.Id)!.Script.ActiveLines.Count());

        window.Close();
    });

    [Fact]
    public void 글_칸_밖을_누르면_도구_막대가_꺼지고_고른_것이_없으면_입히지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어");
        GraphEditorView graph = Graph(window);
        WriterTextBox box = Box(window, card.Id);

        box.Focus();
        box.CaretIndex = 3;
        Dispatcher.UIThread.RunJobs();
        Assert.True(graph.FormatToolbarEnabled);

        // 고른 글자가 없다 — 입히지 않고 상태줄이 말한다.
        Click(window, "굵게");
        Assert.Empty(session.Project.FindDialogue(card.Id)!.WriterMarks);
        Assert.Contains("먼저 고르세요", session.StatusMessage, StringComparison.Ordinal);

        // 판의 빈 곳(초점 받이)으로 — 꺼진다.
        session.Select(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(graph.FormatToolbarEnabled);

        window.Close();
    });

    [Fact]
    public void 서식은_그림에도_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 오늘은 정말 긴 하루였어", "문이 열리고 낯선 바람이 들어온다.", "윌로: OK, the quick brown fox");
        GraphEditorView graph = Graph(window);
        WriterTextBox box = Box(window, card.Id);

        Select(box, "정말");
        Click(window, "굵게");
        Select(box, "긴 하루");
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Color, Color: WriterColor.Red));
        Select(box, "낯선 바람");
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Fill, Color: WriterColor.Yellow));
        Select(box, "열리고");
        Click(window, "취소선");
        Select(box, "quick");
        Click(window, "기울임");
        Select(box, "fox");
        Click(window, "글자 크게");
        Click(window, "글자 크게");
        Click(window, "글자 크게");
        Click(window, "글자 크게");
        Dispatcher.UIThread.RunJobs();

        Directory.CreateDirectory("/tmp/ae");
#pragma warning disable CS0618
        window.CaptureRenderedFrame()!.Save("/tmp/ae/writer.png");
#pragma warning restore CS0618
        Point at = box.TranslatePoint(new Point(0, 0), window)!.Value;
        File.WriteAllText("/tmp/ae/writer.txt", $"{at.X} {at.Y} {box.Bounds.Width} {box.Bounds.Height}");
        Assert.Equal(6, box.Looks.Count);

        window.Close();
    });

    // ── 도움 ──────────────────────────────────────────

    private static void TextPresenterWidths(WriterTextBox box, out double width) =>
        width = box.Presenter!.TextLayout.WidthIncludingTrailingWhitespace;

    private static GraphEditorView Graph(MainWindow window) => window.FindControl<GraphEditorView>("Graph")!;

    private static WriterTextBox Box(MainWindow window, string nodeId) =>
        Graph(window).FindControl<Canvas>("GraphCanvas")!
            .GetLogicalDescendants().OfType<WriterTextBox>()
            .Single(box => (box.Tag as string) == "writing:" + nodeId);

    /// <summary>칸에 초점을 주고 그 글자들을 고른다.</summary>
    private static void Select(WriterTextBox box, string text)
    {
        box.Focus();
        int start = box.Text!.IndexOf(text, StringComparison.Ordinal);
        Assert.True(start >= 0, text);
        box.SelectionStart = start;
        box.SelectionEnd = start + text.Length;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>도구 막대의 단추를 누른다 — 풍선 글의 머리로 찾는다.</summary>
    private static void Click(MainWindow window, string tipHead)
    {
        StackPanel bar = Graph(window).FindControl<StackPanel>("WriterToolbar")!;
        Button button = bar.GetVisualDescendants().OfType<Button>()
            .Single(item => (ToolTip.GetTip(item) as string)?.StartsWith(tipHead, StringComparison.Ordinal) == true);
        Assert.False(button.Focusable);

        if (button is ToggleButton toggle)
        {
            toggle.IsChecked = !toggle.IsChecked;
        }

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static (MainWindow Window, AuthoringSession Session, DialogueNode Card, string[] Ids) Stage(params string[] lines)
    {
        var window = new MainWindow { Width = 1600, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        session.Editor.AddSpeaker("하린");
        session.Editor.AddSpeaker("윌로");
        DialogueNode card = session.Editor.AddDialogueNode(TestChapter.Board(session).Id, "본문", name: "본문");
        Assert.True(session.Editor.ApplyScenarioText(card.Id, string.Join('\n', lines)).Applied);
        session.Editor.MoveNode(card.Id, 60, 60);
        session.Editor.ResizeNode(card.Id, 420, 160);
        session.Select(card.Id);
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Script;
        Dispatcher.UIThread.RunJobs();

        return (window, session, card, session.Project.FindDialogue(card.Id)!.Script.ActiveLines.Select(line => line.Id).ToArray());
    }
}
