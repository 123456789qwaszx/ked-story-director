using Avalonia;
using Avalonia.Controls;
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
