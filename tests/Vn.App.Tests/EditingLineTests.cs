using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>인스펙터에서 쓰는 줄</b>(2026-11-26 소유자) — 긴 대사는 칸 안에서 아래로 줄을 바꾸고(「줄의 뒷부분이 화면 바깥으로 넘어가면 아예 안 보이는데」),
/// 줄의 칸에 초점이 있는 동안 [대본] 판의 그 카드 왼쪽 바깥에 그 대사의 높이로 표시가 선다(「노드 왼쪽으로 현재 수정 중인 줄을 화살표로든 혹은
/// 파란색 막대로든」). 초점이 줄 표를 떠나면 표시가 걷힌다.
/// </summary>
public sealed class EditingLineTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-editing-line", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 긴_대사는_인스펙터의_칸_안에서_아래로_줄을_바꾼다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNodeEditor editor, DialogueNode _) = Show(
            "라루: " + string.Concat(Enumerable.Repeat("아주 길게 이어지는 대사입니다. ", 12)));

        TextBox body = Bodies(editor).Single();
        Assert.Equal(TextWrapping.Wrap, body.TextWrapping);
        Assert.False(body.AcceptsReturn);   // Enter는 줄을 넣지 않는다 — 한 줄 = 대사 하나
        Assert.True(body.Bounds.Height > 40, $"긴 대사가 여러 줄로 섰다 — 높이 {body.Bounds.Height}");

        window.Close();
    });

    [Fact]
    public void 대사_칸에_초점이_들어오면_판의_그_카드_왼쪽에_그_줄의_표시가_서고_떠나면_걷힌다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNodeEditor editor, DialogueNode card) = Show("라루: 첫 줄\n라루: 둘째 줄");
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        IReadOnlyList<string> lineIds = card.Script.ActiveLines.Select(line => line.Id).ToList();

        Assert.Null(Marker(graph));   // 쓰는 줄이 없으면 표시가 없다

        Bodies(editor)[1].Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(lineIds[1], editor.EditingLineId);
        Control marker = Assert.IsAssignableFrom<Control>(Marker(graph));
        Assert.True(marker.IsVisible);
        Assert.True(marker.Margin.Left < 0, "카드 테두리 바깥 왼쪽에 선다");
        double second = marker.Margin.Top;

        // 다른 줄로 옮기면 표시도 옮긴다 — 첫 줄은 둘째 줄보다 위다.
        Bodies(editor)[0].Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(lineIds[0], editor.EditingLineId);
        Assert.True(marker.Margin.Top < second, $"첫 줄 {marker.Margin.Top} < 둘째 줄 {second}");

        // 줄 표를 떠나면(이름 칸) 걷힌다.
        editor.FindControl<TextBox>("NameBox")!.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(editor.EditingLineId);
        Assert.False(marker.IsVisible);

        window.Close();
    });

    [Fact]
    public void 판의_카드_글_칸에서_커서를_옮기면_인스펙터의_고른_줄이_따라온다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「그래프 뷰에서 특정 줄을 수정하는데, 그것과 별개로 우측 인스펙터에서는 다른 줄을 파랗게 강조하니 되게 헷갈립니다 … 그래프 뷰의
        // 텍스트 공간에서 입력하는 것과 동기화」.
        (MainWindow window, AuthoringSession session, DialogueNodeEditor editor, DialogueNode card) = Show("라루: 첫 줄\n라루: 둘째 줄\n라루: 셋째 줄");
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        IReadOnlyList<string> lineIds = card.Script.ActiveLines.Select(line => line.Id).ToList();
        TextBox box = WritingBox(graph, card.Id);
        string text = box.Text!;
        Assert.Equal(lineIds[0], editor.SelectedLineId);   // 처음에는 첫 줄

        box.Focus();
        box.CaretIndex = text.IndexOf("셋째", StringComparison.Ordinal);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(lineIds[2], graph.CaretLineId);
        Assert.Equal(lineIds[2], editor.SelectedLineId);
        Assert.True(box.IsFocused);                         // 인스펙터는 초점을 가져가지 않는다 — 계속 친다

        box.CaretIndex = text.IndexOf("둘째", StringComparison.Ordinal);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(lineIds[1], editor.SelectedLineId);

        // 끝의 빈 줄(새 줄을 칠 자리)이면 그 위의 가장 가까운 대본 줄이다.
        box.CaretIndex = text.Length;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(lineIds[2], editor.SelectedLineId);

        window.Close();
    });

    [Fact]
    public void 고르지_않은_카드의_글_칸을_처음_눌러도_인스펙터가_그_줄까지_내려간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「처음 에피소드 노드 내부의 특정 줄을 처음 클릭했을 때, 이 경우에는 우측 인스펙터에서 강조자체는 제대로 되는데 그 줄로 바로
        // 이동하는게 안 되는 경우가 있네. 그 이후로는 정상적으로 잘 동작해」. 처음 누르면 카드가 골라지며 줄 표를 새로 세우는데, 막 세운 줄은 아직 자리가
        // 없어 곧바로 내리면 아무 일도 없었다.
        string script = string.Join("\n", Enumerable.Range(1, 40).Select(index => $"라루: {index}번째 줄 — 인스펙터 줄 표를 넘칠 만큼 길게 이어진다."));
        (MainWindow window, AuthoringSession session, DialogueNodeEditor editor, DialogueNode card) = Show(script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string last = card.Script.ActiveLines.Last().Id;

        session.Select(null);   // 아직 고르지 않은 카드다
        Dispatcher.UIThread.RunJobs();

        TextBox box = WritingBox(graph, card.Id);
        box.Focus();
        box.CaretIndex = box.Text!.IndexOf("40번째", StringComparison.Ordinal);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(last, editor.SelectedLineId);
        ScrollViewer scroll = editor.FindControl<StackPanel>("LineHost")!.FindAncestorOfType<ScrollViewer>()!;
        Assert.True(scroll.Offset.Y > 0, "줄 표가 그 줄까지 내려갔다");
        double top = Bodies(editor).Last().TranslatePoint(default, scroll)!.Value.Y;
        Assert.InRange(top, 0, scroll.Viewport.Height);

        window.Close();
    });

    [Fact]
    public void 다른_줄의_빈_곳을_누르면_그_줄의_대사_칸으로_초점이_간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 「대본탭의 인스펙터에서 다른 줄을 클릭했는데도, 여전히 화자칸이 열린채 깜빡이는」. 줄의 번호 · 칸 사이를 누르면 줄만
        // 골라지고 앞서 쓰던 줄의 화자 칸이 초점을 쥔 채 남았다.
        (MainWindow window, AuthoringSession session, DialogueNodeEditor editor, DialogueNode card) = Show("라루: 첫 줄\n라루: 둘째 줄\n라루: 셋째 줄");
        IReadOnlyList<string> lineIds = card.Script.ActiveLines.Select(line => line.Id).ToList();
        StackPanel host = editor.FindControl<StackPanel>("LineHost")!;

        TextBox speaker = host.GetVisualDescendants().OfType<AutoCompleteBox>().First().GetVisualDescendants().OfType<TextBox>().First();
        speaker.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(lineIds[0], editor.EditingLineId);

        // 셋째 줄의 번호를 누른다 — 초점을 받지 않는 자리다.
        TextBlock index = host.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "3" && text.FindAncestorOfType<TextBox>() is null);
        var top = (Visual)TopLevel.GetTopLevel(index)!;
        index.RaiseEvent(new PointerPressedEventArgs(index, new Pointer(0, PointerType.Mouse, true), top, index.TranslatePoint(new Point(2, 2), top)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(lineIds[2], editor.SelectedLineId);
        Assert.False(speaker.IsFocused);                 // 첫 줄의 화자 칸은 초점을 놓았다
        Assert.True(Bodies(editor)[2].IsFocused);        // 셋째 줄의 대사 칸에서 이어 쓴다
        Assert.Equal(Bodies(editor)[2].Text!.Length, Bodies(editor)[2].CaretIndex);
        Assert.Equal(lineIds[2], editor.EditingLineId);   // 판의 쓰는 줄 표시도 그 줄이다

        window.Close();
    });

    // ── 기반 ───────────────────────────────────────────────────────────────

    /// <summary>[대본] 탭 — ch01의 root 카드에 글을 넣고 고른다. 인스펙터가 그 카드의 줄 표를 세운다.</summary>
    private (MainWindow Window, AuthoringSession Session, DialogueNodeEditor Editor, DialogueNode Card) Show(string script)
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        string cardId = EpisodeNaming.CardFor(session.Project, "ch01", "root")!.Id;
        session.Editor.ApplyScenarioText(cardId, script);
        session.Select(cardId);
        Dispatcher.UIThread.RunJobs();

        DialogueNodeEditor editor = window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();
        return (window, session, editor, session.Project.FindDialogue(cardId)!);
    }

    /// <summary>줄 표의 대사 칸들 — 위에서 아래 차례(화자 자동완성 안의 글 칸은 뺀다).</summary>
    private static List<TextBox> Bodies(DialogueNodeEditor editor) =>
        editor.FindControl<StackPanel>("LineHost")!.GetVisualDescendants().OfType<TextBox>()
            .Where(box => box.FindAncestorOfType<AutoCompleteBox>() is null)
            .ToList();

    /// <summary>판의 그 카드 글 칸.</summary>
    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    /// <summary>판의 쓰는 줄 표시 — 카드 안의 한 겹에 얹혀 있다.</summary>
    private static Control? Marker(GraphEditorView graph) =>
        graph.FindControl<Canvas>("GraphCanvas")!.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.Tag as string == GraphEditorView.EditingMarkerTag);
}
