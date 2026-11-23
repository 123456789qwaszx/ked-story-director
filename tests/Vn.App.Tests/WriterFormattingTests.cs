using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
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
    private static readonly WriterRgb Red = new(0xD3, 0x2F, 0x2F);
    private static readonly WriterRgb Yellow = new(0xFF, 0xF5, 0x9D);
    private static readonly WriterRgb Blue = new(0x15, 0x65, 0xC0);

    [Fact]
    public void 고른_글자에_굵게를_입히면_대사_글_기준으로_적히고_칸에_그려지며_판은_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);
        Assert.False(graph.FormatToolbarEnabled);

        // 서식 없는 카드는 보통 TextBox다(dev와 같은 칸).
        TextBox plain = Box(window, card.Id);
        Assert.IsNotType<WriterTextBox>(plain);
        int rails = graph.RailDrawCount;
        int reloads = window.FindControl<ChapterGraphView>("ChapterGraph")!.ReloadCount;

        // 「긴 하루」를 고른다 — 칸의 글에서는 「하린: 」 뒤라 8..12.
        Select(plain, "긴 하루");
        Assert.True(graph.FormatToolbarEnabled);
        Click(window, "굵게");

        DialogueNode now = session.Project.FindDialogue(card.Id)!;
        Assert.Equal([new WriterSpan(4, 4, new WriterLook(Bold: true))], now.WriterMarks[ids[0]]);

        // 첫 서식 — 그 카드만 서식을 그리는 칸으로 다시 지었다. 초점과 고른 범위는 새 칸이 이어 쥔다.
        TextBox box = Box(window, card.Id);
        Assert.IsType<WriterTextBox>(box);
        Assert.Equal([new BoxLook(8, 4, new WriterLook(Bold: true))], Looks(window, card.Id));
        Assert.True(box.IsFocused);
        Assert.Equal((8, 12), (box.SelectionStart, box.SelectionEnd));
        Assert.Equal((true, false, false), (graph.FormatToolbarState.Bold, graph.FormatToolbarState.Italic, graph.FormatToolbarState.Strike));

        // 판은 그대로다 — 철도를 통째로 다시 긋지 않았다. [구성]은 다시 읽지 않았다.
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Equal(reloads, window.FindControl<ChapterGraphView>("ChapterGraph")!.ReloadCount);

        // 이제 서식 칸이다 — 더 입혀도 칸이 그대로다.
        Select(box, "문이");
        Click(window, "기울임");
        Assert.Same(box, Box(window, card.Id));
        Assert.True(box.IsFocused);

        // 굵은 글자는 실제로 더 넓게 놓인다 — 글자 배치가 서식을 입었다(커서 · 누른 자리도 이 배치를 쓴다).
        TextPresenterWidths(box, out double boldWidth);
        Assert.True(boldWidth > 0);

        // 모두 굵다 — 한 번 더 누르면 푼다.
        Select(box, "긴 하루");
        Click(window, "굵게");
        Assert.False(session.Project.FindDialogue(card.Id)!.WriterMarks.ContainsKey(ids[0]));
        Assert.DoesNotContain(Looks(window, card.Id), look => look.Look.Bold);

        // 서식을 다 걷으면 다시 보통 TextBox다.
        Select(Box(window, card.Id), "문이");
        Click(window, "기울임");
        Assert.Empty(session.Project.FindDialogue(card.Id)!.WriterMarks);
        Assert.IsNotType<WriterTextBox>(Box(window, card.Id));

        window.Close();
    });

    [Fact]
    public void 색_바탕색_글꼴_크기_정렬을_입히고_되돌리기가_돌린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어");
        GraphEditorView graph = Graph(window);
        Select(Box(window, card.Id), "오늘은");
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Color, Color: Red));
        Dispatcher.UIThread.RunJobs();
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Fill, Color: Yellow));
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Font, Font: "Noto Serif"));
        Click(window, "글자 크게");
        Click(window, "글자 크게");
        Click(window, "가운데 정렬");
        Dispatcher.UIThread.RunJobs();

        var expected = new WriterLook(Color: Red, Fill: Yellow, Font: "Noto Serif", Grow: 2);
        Assert.Equal([new WriterSpan(0, 3, expected)], session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]]);
        Assert.Equal(WriterAlign.Center, session.Project.FindDialogue(card.Id)!.WriterAlign);
        Assert.Equal(TextAlignment.Center, Box(window, card.Id).TextAlignment);
        Assert.Equal($"{ScriptFont.Size + 2:0}", graph.FormatToolbarState.Size);
        Assert.Equal("Noto Serif", graph.FormatToolbarState.Font);

        // 되돌리기 — 정렬 · 크기 두 번 · 글꼴 · 바탕색을 차례로 걷는다. 칸의 꾸밈이 모델을 따라온다.
        for (int step = 0; step < 5; step++)
        {
            session.Editor.Undo();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.NotEqual(TextAlignment.Center, Box(window, card.Id).TextAlignment);
        Assert.Equal([new BoxLook(4, 3, new WriterLook(Color: Red))], Looks(window, card.Id));

        session.Editor.Redo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([new BoxLook(4, 3, new WriterLook(Color: Red, Fill: Yellow))], Looks(window, card.Id));

        window.Close();
    });

    [Fact]
    public void 쓰는_동안_서식이_같은_글자를_따라가고_넣으면_모델도_같다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        Select(Box(window, card.Id), "긴 하루");
        Click(window, "굵게");
        TextBox box = Box(window, card.Id);

        // 앞에 「아, 」를 끼우고 굵은 「하루」 끝에 「들」을 이어 친다 — 넣기 전에도 칸의 꾸밈이 따라간다.
        box.Text = "하린: 아, 오늘은 긴 하루들였어\n문이 열린다.";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([new BoxLook(11, 5, new WriterLook(Bold: true))], Looks(window, card.Id));

        // 넣으면(쓰다 멈춤) 모델도 같은 글자다.
        Graph(window).CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("아, 오늘은 긴 하루들였어", session.Project.FindDialogue(card.Id)!.Script.Text(ids[0]).Text);
        Assert.Equal([new WriterSpan(7, 5, new WriterLook(Bold: true))], session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]]);
        Assert.Equal([new BoxLook(11, 5, new WriterLook(Bold: true))], Looks(window, card.Id));

        // 위에 줄을 새로 넣어 카드를 다시 지어도 서식은 그 줄을 따라간다.
        Box(window, card.Id).Text = "새 줄\n하린: 아, 오늘은 긴 하루들였어\n문이 열린다.";
        Graph(window).CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([new BoxLook(4 + 11, 5, new WriterLook(Bold: true))], Looks(window, card.Id));

        window.Close();
    });

    [Fact]
    public void 넣지_않은_글이_있으면_먼저_넣고_서식을_입히며_화자와_여러_줄을_가른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 첫 줄", "둘째 줄");
        TextBox box = Box(window, card.Id);

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
        TextBox box = Box(window, card.Id);

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

        Select(Box(window, card.Id), "정말");
        Click(window, "굵게");
        TextBox box = Box(window, card.Id);
        Select(box, "긴 하루");
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Color, Color: Red));
        Select(box, "낯선 바람");
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Fill, Color: Yellow));
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
        Assert.Equal(6, Looks(window, card.Id).Count);

        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「여러 글자를 드래그 한 뒤에 글꼴을 바꾸면, 드래그 했던게 모두 풀리는 버그」. 글꼴 고르기는 펼칠 때 초점을 가져가고, 초점을 잃은 칸은
    /// 고른 범위를 지운다. 윈도에서는 목록 창이 닫히며 생기는 초점 알림이 입력으로 늦게 와서, 그보다 먼저 되돌린 초점 · 범위를 다시 가져갔다 — 그 늦은 알림을
    /// 입력 차례의 일로 끼워 흉내 낸다. 펼친 동안에도 범위는 남고(옅게 칠한 채), 고른 뒤에는 칸이 초점과 그 범위를 다시 쥔다.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 여러_글자를_고른_채_글꼴을_골라도_고른_범위가_남는다(bool formatted) => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);

        if (formatted)
        {
            session.Editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[1], 0, 2)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
            Dispatcher.UIThread.RunJobs();
        }

        TextBox box = Box(window, card.Id);
        Select(box, "긴 하루");
        Assert.Equal((8, 12), (box.SelectionStart, box.SelectionEnd));

        // 글꼴 고르기를 누른다 — 목록이 초점을 가져가도 칸의 범위는 남는다.
        ComboBox fonts = Graph(window).FindControl<StackPanel>("WriterToolbar")!.GetVisualDescendants().OfType<ComboBox>().Single();
        Point center = fonts.TranslatePoint(new Point(fonts.Bounds.Width / 2, fonts.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(fonts.IsDropDownOpen);
        Assert.False(box.IsFocused);
        Assert.Equal((8, 12), (box.SelectionStart, box.SelectionEnd));
        Assert.True(graph.HoldsFormatSelection);

        // 글꼴 하나를 누른다 — 목록의 누르기는 고르고 나서 목록을 닫는다(그 두 걸음). 목록 창이 닫히며 생기는 초점 알림은 입력 차례로 늦게 온다.
        // (헤드리스의 마우스 흉내는 누를 때마다 밀린 일을 다 돌려 그 틈이 없다 — 두 걸음을 바로 밟는다.)
        string picked = (string)((ComboBoxItem)fonts.ContainerFromIndex(1)!).Content!;
        fonts.SelectedItem = picked;
        fonts.IsDropDownOpen = false;
        Dispatcher.UIThread.Post(() => graph.FindControl<Border>("WritingFocusSink")!.Focus(), DispatcherPriority.Input);
        Dispatcher.UIThread.RunJobs();

        Assert.False(fonts.IsDropDownOpen);
        Assert.Equal(picked, session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]].Single(span => span.Look.Font is not null).Look.Font);
        TextBox now = Box(window, card.Id);
        Assert.IsType<WriterTextBox>(now);
        Assert.True(now.IsFocused);
        Assert.Equal((8, 12), (now.SelectionStart, now.SelectionEnd));

        // 다 썼다 — 붙든 범위를 놓았다. 이제 칸을 떠나면 보통 칸처럼 범위가 걷힌다.
        Assert.False(graph.HoldsFormatSelection);
        Assert.True(now.ClearSelectionOnLostFocus);

        window.Close();
    });

    /// <summary>크기 숫자는 그 카드의 처음 크기(writer-formatting WF-D13)에 서식의 크기 차를 더한 것이다 — 설정의 지금 크기가 아니다.</summary>
    [Fact]
    public void 크기_숫자는_그_카드의_처음_크기에서_센다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어");
        GraphEditorView graph = Graph(window);
        session.Project.FindDialogue(card.Id)!.WriterBase = new WriterBase(string.Empty, 16);
        graph.RebuildCard(card.Id);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(16, Box(window, card.Id).FontSize);

        Select(Box(window, card.Id), "긴 하루");
        Assert.Equal("16", graph.FormatToolbarState.Size);
        Click(window, "글자 크게");
        Assert.Equal("17", graph.FormatToolbarState.Size);
        Assert.Equal(16, Box(window, card.Id).FontSize);   // 칸의 바탕 크기는 그대로 — 고른 글자만 커졌다

        window.Close();
    });

    /// <summary>글꼴 고르기를 펼쳤다가 고르지 않고 닫으면 칸이 초점과 범위를 다시 쥐고, 그 칸을 떠나면 범위가 걷힌다(붙든 채 남지 않는다).</summary>
    [Fact]
    public void 글꼴_고르기를_그냥_닫으면_범위가_돌아오고_칸을_떠나면_걷힌다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);
        TextBox box = Box(window, card.Id);
        Select(box, "긴 하루");

        ComboBox fonts = graph.FindControl<StackPanel>("WriterToolbar")!.GetVisualDescendants().OfType<ComboBox>().Single();
        Point center = fonts.TranslatePoint(new Point(fonts.Bounds.Width / 2, fonts.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(fonts.IsDropDownOpen);

        fonts.IsDropDownOpen = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Same(box, Box(window, card.Id));
        Assert.True(box.IsFocused);
        Assert.Equal((8, 12), (box.SelectionStart, box.SelectionEnd));
        Assert.False(graph.HoldsFormatSelection);
        Assert.Empty(session.Project.FindDialogue(card.Id)!.WriterMarks);

        // 칸을 떠난다 — 보통 칸처럼 범위가 걷힌다.
        graph.FindControl<Border>("WritingFocusSink")!.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(box.SelectionStart, box.SelectionEnd);

        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「글자 색깔과 바탕색의 종류가 조금 제한 적인게 아쉬운데, 사용한 색깔과 더불어서 RGB를 원하는 걸 선택할 수 있는 것도」(writer-formatting
    /// WF-D14). 색 차림에서 RGB를 직접 골라 입히고, 그 색이 「이 프로젝트에 쓴 색」에 서며(정해 둔 열 가지는 서지 않는다), 그 견본으로 다른 글자에 또 입힌다.
    /// </summary>
    [Fact]
    public void 색_차림에서_RGB를_골라_입히고_그_색이_이_프로젝트에_쓴_색에_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);
        GraphEditorView.ColorMenu menu = graph.ColorMenuOf(WriterStyleKind.Color);

        // 정해 둔 빨강을 한 군데 입혀 둔다 — 「이 프로젝트에 쓴 색」에는 서지 않는다.
        Select(Box(window, card.Id), "문이");
        graph.ApplyFormat(new WriterStyleChange(WriterStyleKind.Color, Color: Red));
        Dispatcher.UIThread.RunJobs();

        Select(Box(window, card.Id), "긴 하루");
        OpenColorMenu(window, "글자색");
        Assert.True(menu.Flyout.IsOpen);
        Assert.False(menu.Chooser.UsedTitle.IsVisible);   // 정해 둔 색만 썼다
        Assert.Equal((8, 12), (Box(window, card.Id).SelectionStart, Box(window, card.Id).SelectionEnd));   // 펼친 동안에도 범위가 남는다

        // 16진수를 적으면 막대와 미리 보기가 따르고, 막대를 옮기면 16진수가 따른다.
        menu.Chooser.Hex.Text = "#123456";
        Dispatcher.UIThread.RunJobs();   // 글 바뀜 알림은 한 박자 뒤에 온다(Avalonia)
        Assert.Equal([0x12, 0x34, 0x56], menu.Chooser.Channels.Select(channel => (int)channel.Value));
        menu.Chooser.Channels[0].Value = 0x9A;
        Assert.Equal("#9A3456", menu.Chooser.Hex.Text);

        menu.Chooser.Apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        var picked = new WriterRgb(0x9A, 0x34, 0x56);
        Assert.False(menu.Flyout.IsOpen);
        Assert.Equal(new WriterSpan(4, 4, new WriterLook(Color: picked)), session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]].Single());
        Assert.True(Box(window, card.Id).IsFocused);
        Assert.Equal((8, 12), (Box(window, card.Id).SelectionStart, Box(window, card.Id).SelectionEnd));

        // 다시 열면 그 색이 「이 프로젝트에 쓴 색」에 서고, 직접 고르기는 그 색에서 시작한다 — 그 견본으로 다른 글자에 입힌다.
        Select(Box(window, card.Id), "열린다");
        OpenColorMenu(window, "글자색");
        Assert.True(menu.Chooser.UsedTitle.IsVisible);
        Button reuse = Assert.Single(menu.Chooser.Used.Children.OfType<Button>());
        Assert.Equal(picked, reuse.Tag);
        Assert.Equal("#9A3456", menu.Chooser.Hex.Text);
        reuse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(session.Project.FindDialogue(card.Id)!.WriterMarks[ids[1]], span => span.Look.Color == picked);

        window.Close();
    });

    // ── 도움 ──────────────────────────────────────────

    /// <summary>도구 막대의 색 단추를 마우스로 눌러 색 차림을 연다 — 풍선 글의 머리로 찾는다.</summary>
    private static void OpenColorMenu(MainWindow window, string tipHead)
    {
        StackPanel bar = Graph(window).FindControl<StackPanel>("WriterToolbar")!;
        Button button = bar.GetVisualDescendants().OfType<Button>()
            .Single(item => (ToolTip.GetTip(item) as string)?.StartsWith(tipHead, StringComparison.Ordinal) == true);
        Point center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// 2026-11-23 소유자 — 「서식도구에 줄 간격 조절을 새로 넣는게 맞습니다」 · 「줄 간격을 새 카드의 기본값으로 바꿉시다」(writer-formatting WF-D19). 글자를
    /// 고르지 않아도 그 칸에 커서가 있으면 된다. 그 카드의 처음 값만 바뀌고(서식이 없는 카드는 보통 칸 그대로 · 같은 칸이 새 줄 간격을 입는다), 다른 카드는
    /// 그대로이며, 되돌리기 한 번에 한 칸씩 돌아간다. 설정의 범위(100 ~ 250%)에서 멈춘다.
    /// </summary>
    [Fact]
    public void 서식_도구의_줄_간격은_그_카드만_바꾸고_되돌리기가_돌린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);
        DialogueNode other = session.Editor.AddDialogueNode(TestChapter.Board(session).Id, "다른", name: "다른");
        Dispatcher.UIThread.RunJobs();
        int stored = session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing!.Value;   // 만들 때의 설정
        Assert.Equal(ScriptFont.LineSpacing, stored);
        int start = ScriptFont.ShownLineSpacing(session.Project.FindDialogue(card.Id)!.WriterBase);   // 보이는 값 — 그 글꼴에서 가장 좁은 값 아래로는 안 간다(WF-D23)
        double otherHeight = Box(window, other.Id).LineHeight;
        Assert.Equal($"{start}%", graph.FormatLineSpacingText);   // 카드를 골랐다 — 글 칸을 누르지 않아도 그 카드다(WF-D21)

        TextBox box = Box(window, card.Id);
        Assert.IsNotType<WriterTextBox>(box);
        box.Focus();
        box.SelectionStart = box.SelectionEnd = 3;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal($"{start}%", graph.FormatLineSpacingText);

        Click(window, "줄 간격 넓게");
        Click(window, "줄 간격 넓게");

        Assert.Equal(start + 20, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Assert.Same(box, Box(window, card.Id));   // 서식 없는 카드는 보통 칸 그대로 — 같은 칸이 새 줄 간격을 입는다
        Assert.Equal(Math.Round(box.FontSize * (start + 20) / 100.0, 1), box.LineHeight);
        Assert.Equal($"{start + 20}%", graph.FormatLineSpacingText);
        Assert.True(box.IsFocused);
        Assert.Equal(otherHeight, Box(window, other.Id).LineHeight);
        Assert.Equal(stored, session.Project.FindDialogue(other.Id)!.WriterBase!.LineSpacing);

        // 되돌리기 — 한 칸씩.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(start + 10, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Assert.Equal($"{start + 10}%", graph.FormatLineSpacingText);
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(stored, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);

        // 끝에서 멈춘다 — 되돌리기 칸도 쌓이지 않는다.
        session.Editor.SetWriterLineSpacing(card.Id, ScriptFont.MaxLineSpacing);
        Dispatcher.UIThread.RunJobs();
        Box(window, card.Id).Focus();
        Dispatcher.UIThread.RunJobs();
        Click(window, "줄 간격 넓게");
        Assert.Equal(ScriptFont.MaxLineSpacing, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        session.Editor.Undo();
        Assert.Equal(stored, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「줄 사이 간격을 일정 수준 이하로 줄일 떄는, 숫자만 내려가고 동작 변화가 없는데」 → 「숫자가 최소값에서 멈추게 해주십시오」
    /// (writer-formatting WF-D23). 그 카드의 글꼴 · 크기에서 가장 좁은 값(그 %의 줄 높이가 그 글꼴의 가장 낮은 줄 높이 이상인 가장 작은 정수)에서 −도 숫자도
    /// 멈추고 상태줄이 말한다. 그 아래로 적힌 옛 값도 그 값으로 보인다. 그 값의 한 칸 아래는 화면이 같다 — 멈추는 자리가 맞다.
    /// </summary>
    [Fact]
    public void 줄_간격은_그_글꼴에서_가장_좁은_값에서_멈춘다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);
        WriterBase basis = session.Project.FindDialogue(card.Id)!.WriterBase!;
        int narrowest = ScriptFont.MinLineSpacingOf(basis);
        Assert.InRange(narrowest, ScriptFont.MinLineSpacing + 1, ScriptFont.MaxLineSpacing);   // 한글 한 줄은 100 %보다 높다

        // 멈추는 자리가 맞다 — 그 값에서 줄 높이가 바뀌기 시작한다.
        TextBox box = Box(window, card.Id);
        var typeface = new Typeface(box.FontFamily, box.FontStyle, box.FontWeight);
        Assert.Equal(ScriptFont.LineHeightFor(typeface, box.FontSize, narrowest - 1), ScriptFont.LineHeightFor(typeface, box.FontSize, ScriptFont.MinLineSpacing));
        Assert.True(ScriptFont.LineHeightFor(typeface, box.FontSize, narrowest + 1) > ScriptFont.LineHeightFor(typeface, box.FontSize, narrowest - 1));

        // 그 아래로 적힌 옛 값은 그 값으로 보인다.
        session.Editor.SetWriterLineSpacing(card.Id, ScriptFont.MinLineSpacing);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal($"{narrowest}%", graph.FormatLineSpacingText);

        // − 는 거기서 멈추고 말한다 — 적힌 값은 보이는 값으로 맞춰진다.
        box = Box(window, card.Id);
        box.Focus();
        box.SelectionStart = box.SelectionEnd = 2;
        Dispatcher.UIThread.RunJobs();
        Click(window, "줄 간격 좁게");
        Assert.Equal(narrowest, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Assert.Equal("최소 줄 간격입니다.", session.StatusMessage);   // 2026-11-23 소유자 — 「그냥 최소 줄 간격입니다로 고쳐줘」

        // 위로 한 칸 뒤 다시 −는 그 값으로. 숫자로 더 좁게 적어도 그 값이다.
        Click(window, "줄 간격 넓게");
        Click(window, "줄 간격 좁게");
        Assert.Equal(narrowest, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Type(window, graph.FormatValueEntries.LineSpacing!, "100");
        Assert.Equal(narrowest, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Assert.Equal($"{narrowest}%", graph.FormatLineSpacingText);
        Assert.Equal("최소 줄 간격입니다.", session.StatusMessage);
        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「특정 에피소드내에서 글자 크기를 작게 줄여놨는데도, 줄 간격은 처음 생성될때 기준으로 가장 좁은 수치가 결정되어있다보니, 엄청 답답한
    /// 감이 있네」(writer-formatting WF-D24). 대사를 줄인 카드는 줄 간격 −가 처음 크기의 가장 좁은 값을 지나 더 내려가고, 줄인 글자만 든 줄이 실제로 낮아진다.
    /// 화자 이름(서식이 닿지 않아 처음 크기)이 든 줄은 제 높이에서 멈춘다. 그 카드의 가장 좁은 값에서 멈추고 「최소 줄 간격입니다」. 줄인 글자를 되돌리면
    /// 처음 크기의 가장 좁은 값으로 보인다.
    /// </summary>
    [Fact]
    public void 글자를_줄인_카드는_줄_간격을_그_글자에_맞춰_더_좁힐_수_있다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.", "바람이 분다.");
        GraphEditorView graph = Graph(window);
        WriterBase basis = session.Project.FindDialogue(card.Id)!.WriterBase!;
        int baseNarrowest = ScriptFont.MinLineSpacingOf(basis);
        session.Editor.SetWriterLineSpacing(card.Id, baseNarrowest);

        // 대사 셋을 모두 4pt 줄인다(화자 이름은 그대로).
        session.Editor.ApplyWriterStyle(card.Id, ids.Select(id => new WriterRange(id, 0, session.Project.FindDialogue(card.Id)!.Script.Text(id).Text.Length)).ToList(),
            new WriterStyleChange(WriterStyleKind.Grow, Step: -4));
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        var box = Assert.IsType<WriterTextBox>(Box(window, card.Id));
        double[] before = box.Presenter!.TextLayout.TextLines.Select(line => line.Height).ToArray();
        Assert.Equal($"{baseNarrowest}%", graph.FormatLineSpacingText);

        // − 가 처음 크기의 가장 좁은 값 아래로 내려간다 — 줄인 글자만 든 줄(둘째 · 셋째)이 낮아지고, 화자 이름이 든 첫 줄은 그대로다.
        box.Focus();
        box.SelectionStart = box.SelectionEnd = box.Text!.IndexOf('문');
        Dispatcher.UIThread.RunJobs();
        Click(window, "줄 간격 좁게");
        int spacing = session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing!.Value;
        Assert.Equal(baseNarrowest - 10, spacing);
        Assert.Equal($"{spacing}%", graph.FormatLineSpacingText);
        box = Assert.IsType<WriterTextBox>(Box(window, card.Id));
        var typeface = new Typeface(box.FontFamily, box.FontStyle, box.FontWeight);
        double plain = ScriptFont.PlainLine(typeface, box.FontSize).Height;   // 화자 이름이 든 줄 — 처음 크기 글자의 자연 높이(가장 낮은 줄 높이와 거의 같다)
        double[] after = box.Presenter!.TextLayout.TextLines.Select(line => line.Height).ToArray();
        Assert.InRange(after[0], plain - 0.05, before[0] + 0.05);
        Assert.Equal(Math.Round(box.FontSize * spacing / 100.0, 1), after[1], 1);
        Assert.Equal(after[1], after[2], 2);
        // 또렷이 낮아졌다 — 10%는 11pt에서 1.1px이지만 줄 높이를 0.1 단위로 반올림해 1.0px일 수 있다(Windows 135 → 125%: 14.8 → 13.8 · ⛔ 「1px 넘게」는
        // 리눅스에서만 통과했다 — 2026-11-23 CI).
        Assert.True(after[1] < before[1] - 0.5, $"줄인 줄 {before[1]:0.0} → {after[1]:0.0}");

        // 그 카드의 가장 좁은 값(줄인 글자의 가장 낮은 줄 높이)에서 멈춘다 — 숫자로 더 좁게 적어도.
        int cardNarrowest = ScriptFont.MinLineSpacingOf(basis, ScriptFont.MinLineHeight(typeface, box.FontSize - 4));
        Assert.True(cardNarrowest < baseNarrowest - 10);
        Type(window, graph.FormatValueEntries.LineSpacing!, "40");
        Assert.Equal(cardNarrowest, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Assert.Equal($"{cardNarrowest}%", graph.FormatLineSpacingText);
        Assert.Equal("최소 줄 간격입니다.", session.StatusMessage);
        box = Assert.IsType<WriterTextBox>(Box(window, card.Id));
        after = box.Presenter!.TextLayout.TextLines.Select(line => line.Height).ToArray();
        Assert.InRange(after[0], plain - 0.05, before[0] + 0.05);
        Assert.True(after[1] >= ScriptFont.MinLineHeight(typeface, box.FontSize - 4) - 1e-6);

        // 줄인 글자를 되돌리면 처음 크기의 가장 좁은 값으로 보이고, 줄도 그 높이다.
        session.Editor.Undo();   // 숫자로 적은 줄 간격
        session.Editor.Undo();   // −
        session.Editor.Undo();   // 줄인 글자
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal($"{baseNarrowest}%", graph.FormatLineSpacingText);
        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「현재는 글자서식에서 글자 크기와 줄을 +, -로 밖에 조절 못하는데 정확히 텍스트 입력을 해서 특정값으로 변경하는 것도 필요하겠습니다」
    /// (writer-formatting WF-D20). 크기 칸 · 줄 간격 칸을 마우스로 눌러 숫자를 적고 Enter — 고른 글자가 그 크기가, 카드가 그 줄 간격이 된다. 칸은 고른 범위를
    /// 지킨 채 초점을 되찾는다. Esc · 숫자가 아닌 글은 아무것도 바꾸지 않고, 범위 밖은 끝으로 당기며 상태줄이 말한다.
    /// </summary>
    [Fact]
    public void 크기와_줄_간격을_숫자로_적어_입힌다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        GraphEditorView graph = Graph(window);
        (TextBox size, TextBox spacing) = (graph.FormatValueEntries.Size!, graph.FormatValueEntries.LineSpacing!);
        TextBox box = Box(window, card.Id);
        double cardSize = box.FontSize;

        // 크기 — 「긴 하루」를 20pt로.
        Select(box, "긴 하루");
        Type(window, size, "20");
        Assert.Equal([new WriterSpan(4, 4, new WriterLook(Grow: 20 - (int)cardSize))], session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]]);
        box = Box(window, card.Id);
        Assert.True(box.IsFocused);
        Assert.Equal((8, 12), (box.SelectionStart, box.SelectionEnd));
        Assert.Equal("20", graph.FormatToolbarState.Size);

        // 범위 밖 — 끝으로 당기고 말한다. 숫자가 아니면 그대로다.
        Type(window, size, "200");
        Assert.Equal(WriterMarkMath.MaxGrow, session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]].Single().Look.Grow);
        Assert.Contains("맞췄습니다", session.StatusMessage, StringComparison.Ordinal);
        Type(window, Graph(window).FormatValueEntries.Size!, "크게");
        Assert.Contains("크기가 아닙니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(WriterMarkMath.MaxGrow, session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]].Single().Look.Grow);
        Assert.Equal($"{cardSize + WriterMarkMath.MaxGrow:0}", graph.FormatToolbarState.Size);   // 적은 글은 버리고 지금 값

        // Esc — 그만둔다.
        Type(window, size, "12", Key.Escape);
        Assert.Equal(WriterMarkMath.MaxGrow, session.Project.FindDialogue(card.Id)!.WriterMarks[ids[0]].Single().Look.Grow);

        // 줄 간격 — 「175%」 · 「175」 모두. 범위 밖은 끝으로.
        Type(window, spacing, "175%");
        Assert.Equal(175, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Assert.Equal("175%", graph.FormatLineSpacingText);
        Type(window, spacing, "300");
        Assert.Equal(ScriptFont.MaxLineSpacing, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        Assert.True(Box(window, card.Id).IsFocused);

        // 되돌리기 — 한 번에 한 칸.
        session.Editor.Undo();
        Assert.Equal(175, session.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        window.Close();
    });

    [Theory]
    [InlineData("12", 12)]
    [InlineData(" 150% ", 150)]
    [InlineData("12pt", 12)]
    [InlineData("12.6", 13)]
    [InlineData("크게", null)]
    [InlineData("", null)]
    public void 적은_글에서_숫자를_읽는다(string typed, int? expected) => Assert.Equal(expected, GraphEditorView.TypedNumber(typed));

    /// <summary>도구 막대의 값 칸을 마우스로 눌러(칸의 고른 범위를 붙든다) 글을 적고 그 키를 누른다.</summary>
    private static void Type(MainWindow window, TextBox entry, string text, Key key = Key.Enter)
    {
        Point center = entry.TranslatePoint(new Point(entry.Bounds.Width / 2, entry.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(entry.IsFocused);
        entry.Text = text;
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(key, RawInputModifiers.None, key == Key.Enter ? PhysicalKey.Enter : PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static void TextPresenterWidths(TextBox box, out double width) =>
        width = ((WriterTextBox)box).Presenter!.TextLayout.WidthIncludingTrailingWhitespace;

    private static GraphEditorView Graph(MainWindow window) => window.FindControl<GraphEditorView>("Graph")!;

    private static TextBox Box(MainWindow window, string nodeId) =>
        Graph(window).FindControl<Canvas>("GraphCanvas")!
            .GetLogicalDescendants().OfType<TextBox>()
            .Single(box => (box.Tag as string) == "writing:" + nodeId);

    /// <summary>그 카드 글 칸의 꾸밈 — 서식 없는 카드는 보통 TextBox라 없다.</summary>
    private static IReadOnlyList<BoxLook> Looks(MainWindow window, string nodeId) =>
        (Box(window, nodeId) as WriterTextBox)?.Looks ?? [];

    /// <summary>칸에 초점을 주고 그 글자들을 고른다.</summary>
    private static void Select(TextBox box, string text)
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
