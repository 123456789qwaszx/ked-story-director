using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>하이어아키에서 여러 줄 고르기</b> (2026-11-23 소유자 — 「여러개를 shift + 우클릭으로 한번에 선택하는게 가능할까요?」 → 고른 답 「Ctrl+클릭 · Shift+클릭」 ·
/// 「섞어 고를 수 있음」 · 「마지막 줄을 따르고 [삭제]는 끔」 · hierarchy §5). 마우스로 줄을 눌러(보조 키와 함께) 본다.
/// </summary>
public sealed class HierarchyMultiSelectTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-multi", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Ctrl_클릭은_더하고_빼고_판과_인스펙터는_마지막_줄을_따르며_삭제는_꺼진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph, StoryHierarchy tree) = Open();
        string a = StoryHierarchy.EpisodeKey("ch01", "a");
        string ch02 = StoryHierarchy.ChapterKey("ch02");
        string hall = StoryHierarchy.SceneKey("ch01", "hall");

        Press(window, tree, a);
        Assert.Equal([a], Keys(tree));
        Assert.Equal(Card(session, "ch01", "a"), session.SelectedNodeId);
        Assert.True(DeleteButton(graph).IsEnabled);

        // Ctrl+클릭 — 챕터를 더한다(종류가 섞여도 된다). 판 · 인스펙터는 그 챕터를 따르고 [삭제]는 꺼진다.
        Press(window, tree, ch02, RawInputModifiers.Control);
        Assert.Equal([a, ch02], Keys(tree));
        Assert.Equal(ch02, tree.Selection?.Key);
        Assert.Equal("ch02", session.ActiveFile?.Name);
        Assert.False(DeleteButton(graph).IsEnabled);
        Assert.Contains("여러 개", (string)ToolTip.GetTip(DeleteButton(graph))!, StringComparison.Ordinal);

        Press(window, tree, hall, RawInputModifiers.Control);
        Assert.Equal([a, ch02, hall], Keys(tree));
        Assert.All(new[] { a, ch02, hall }, key => Assert.True(Painted(tree, key), key));
        Assert.False(Painted(tree, StoryHierarchy.EpisodeKey("ch01", "b")));

        // 마지막 줄이 아닌 줄을 빼면 판은 그대로다. 마지막 줄을 빼면 그 앞의 줄이 마지막이 된다.
        Press(window, tree, ch02, RawInputModifiers.Control);
        Assert.Equal([a, hall], Keys(tree));
        Assert.Equal(hall, tree.Selection?.Key);
        Press(window, tree, hall, RawInputModifiers.Control);
        Assert.Equal([a], Keys(tree));
        Assert.Equal(Card(session, "ch01", "a"), session.SelectedNodeId);
        Assert.True(DeleteButton(graph).IsEnabled);

        // 하나 남은 줄은 빼지 않는다.
        Press(window, tree, a, RawInputModifiers.Control);
        Assert.Equal([a], Keys(tree));

        window.Close();
    });

    [Fact]
    public void Shift_클릭은_기준_줄부터_그_줄까지_고르고_그냥_누르면_다시_한_줄이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph, StoryHierarchy tree) = Open();
        string root = StoryHierarchy.EpisodeKey("ch01", "root");
        string b = StoryHierarchy.EpisodeKey("ch01", "b");

        Press(window, tree, root);
        Press(window, tree, b, RawInputModifiers.Shift);

        // 보이는 줄 차례로 root부터 b까지 — 사이의 장면 줄도 든다. 누른 줄이 마지막이다.
        List<string> between = tree.Rows.SkipWhile(row => row.Key != root).TakeWhile(row => row.Key != b).Select(row => row.Key).Append(b).ToList();
        Assert.Contains(StoryHierarchy.SceneKey("ch01", "hall"), between);
        Assert.Equal(between.OrderBy(key => key, StringComparer.Ordinal), Keys(tree).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(b, tree.Selection?.Key);
        Assert.Equal(Card(session, "ch01", "b"), session.SelectedNodeId);
        Assert.False(DeleteButton(graph).IsEnabled);

        // Delete 키도 지우지 않는다 — 까닭을 말한다.
        int episodes = session.Editor.FindChapter("ch01")!.Episodes.Count;
        Assert.True(graph.DeleteSelectedCard());
        Assert.Equal(episodes, session.Editor.FindChapter("ch01")!.Episodes.Count);
        Assert.Contains("여러 개", session.StatusMessage, StringComparison.Ordinal);

        // 기준 줄은 그대로 — Shift로 범위를 다시 잡는다.
        string a = StoryHierarchy.EpisodeKey("ch01", "a");
        Press(window, tree, a, RawInputModifiers.Shift);
        Assert.Equal([root, a], Keys(tree));

        // 그냥 누르면 한 줄. 판에서 다른 카드를 고르면(밖에서) 그것 한 줄.
        Press(window, tree, b);
        Assert.Equal([b], Keys(tree));
        Press(window, tree, a, RawInputModifiers.Control);
        session.Select(Card(session, "ch02", "x"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([StoryHierarchy.EpisodeKey("ch02", "x")], Keys(tree));
        Assert.True(DeleteButton(graph).IsEnabled);

        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「글자 서식의 크기 줄 간격 숫자 입력역시 에피소드가 아니라 텍스트 박스를 정확히 눌러야 활성화 되다보니 현재는 조정이 사실상
    /// 불가능한 상태입니다」 → 고른 답 「카드의 기본 크기」 · 「도구 막대 전부」(writer-formatting WF-D21). 하이어아키에서 카드만 골라도(여러 줄 · 챕터째도)
    /// 서식 도구가 켜지고, 고른 줄들에 든 카드 모두에 입힌다 — 굵게는 글 전체, 크기는 카드의 기본 크기(일부만 키운 글자는 차를 지킨다), 줄 간격 · 정렬은 카드
    /// 마다. 되돌리기 한 번에 모두 돌아온다.
    /// </summary>
    [Fact]
    public void 카드만_골라도_서식_도구가_켜지고_고른_카드들_모두에_입힌다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph, StoryHierarchy tree) = Open();
        string a = Card(session, "ch01", "a");
        string b = Card(session, "ch01", "b");
        string x = Card(session, "ch02", "x");
        string y = Card(session, "ch02", "y");

        foreach (string card in new[] { a, b, x })
        {
            Assert.True(session.Editor.ApplyScenarioText(card, "문이 열린다.\n바람이 분다.").Applied);
        }

        session.Editor.ApplyWriterStyle(a, [new WriterRange(session.Project.FindDialogue(a)!.Script.ActiveLines.First().Id, 0, 2)],
            new WriterStyleChange(WriterStyleKind.Grow, Step: 3));   // 「문이」만 +3
        Dispatcher.UIThread.RunJobs();

        // 에피소드 줄 하나 — 글 칸을 누르지 않아도 켜진다.
        Press(window, tree, StoryHierarchy.EpisodeKey("ch01", "a"));
        Assert.True(Bar(graph, "굵게").IsEnabled);
        Assert.Equal("11", graph.FormatToolbarState.Size);
        Assert.Equal($"{ScriptFont.ShownLineSpacing(session.Project.FindDialogue(a)!.WriterBase)}%", graph.FormatLineSpacingText);

        // 에피소드 b와 챕터 ch02(x · y)를 더한다 — 카드 넷.
        Press(window, tree, StoryHierarchy.EpisodeKey("ch01", "b"), RawInputModifiers.Control);
        Press(window, tree, StoryHierarchy.ChapterKey("ch02"), RawInputModifiers.Control);
        Assert.Equal([a, b, x, y], graph.PickedCards().Select(card => card.Id));
        Assert.True(Bar(graph, "굵게").IsEnabled);

        // 굵게 — 글이 든 카드 셋의 글 전체.
        Bar(graph, "굵게").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.All(new[] { a, b, x }, card => Assert.All(Lines(session, card), line =>
            Assert.True(WriterMarkMath.IsAll(line.Spans, line.Length, 0, line.Length, look => look.Bold))));
        Assert.True(graph.FormatToolbarState.Bold);
        Assert.Contains("4장", session.StatusMessage, StringComparison.Ordinal);

        // 크기 16 — 카드의 기본 크기. 「문이」는 +3 그대로.
        TypeInto(window, graph.FormatValueEntries.Size!, "16");
        Assert.All(new[] { a, b, x, y }, card => Assert.Equal(16, session.Project.FindDialogue(card)!.WriterBase!.Size));
        Assert.Equal(3, session.Project.FindDialogue(a)!.WriterMarks.Values.SelectMany(spans => spans).Max(span => span.Look.Grow));
        Assert.Equal(16, WritingBox(graph, a).FontSize);
        Assert.Equal("16", graph.FormatToolbarState.Size);
        Assert.False(graph.FormatValueEntries.Size!.IsFocused);   // 하이어아키로 돌아갔다 — 다른 카드를 고르면 칸이 따라온다

        // 크기 − · 줄 간격 · 정렬 — 카드마다.
        Bar(graph, "글자 작게").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TypeInto(window, graph.FormatValueEntries.LineSpacing!, "180");
        Bar(graph, "가운데 정렬").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.All(new[] { a, b, x, y }, card =>
        {
            DialogueNode node = session.Project.FindDialogue(card)!;
            Assert.Equal((15d, 180, WriterAlign.Center), (node.WriterBase!.Size, node.WriterBase.LineSpacing!.Value, node.WriterAlign));
        });

        // 되돌리기 — 한 번에 한 가지씩, 카드 모두(정렬 → 줄 간격 → 크기 − → 크기 16 → 굵게).
        session.Editor.Undo();
        Assert.All(new[] { a, b, x, y }, card => Assert.Equal(WriterAlign.Left, session.Project.FindDialogue(card)!.WriterAlign));
        session.Editor.Undo();
        session.Editor.Undo();
        session.Editor.Undo();
        Assert.All(new[] { a, b, x, y }, card => Assert.Equal(11, session.Project.FindDialogue(card)!.WriterBase!.Size));
        Assert.All(new[] { a, b, x }, card => Assert.True(Lines(session, card).All(line => WriterMarkMath.IsAll(line.Spans, line.Length, 0, line.Length, look => look.Bold))));
        session.Editor.Undo();
        Assert.All(new[] { b, x }, card => Assert.Empty(session.Project.FindDialogue(card)!.WriterMarks));

        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「빈 선택지 숨기기를 하더라도, 그 마지막 것만 빈 선택지 숨기기 체크가 되는데, 선택 된것들 모두 일괄적으로 키고 꺼지도록」. 인스펙터의
    /// 체크가 고른 줄들에 든 에피소드 카드 모두를 켜고 끈다. 섞였으면 반쯤이고 누르면 모두 켠다. 그냥 누르면 다시 그 카드 하나다.
    /// </summary>
    [Fact]
    public void 빈_선택지_숨기기는_고른_에피소드_모두를_한꺼번에_켜고_끈다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph, StoryHierarchy tree) = Open();
        string root = Card(session, "ch01", "root");
        string a = Card(session, "ch01", "a");
        string b = Card(session, "ch01", "b");

        Press(window, tree, StoryHierarchy.EpisodeKey("ch01", "root"));
        Press(window, tree, StoryHierarchy.SceneKey("ch01", "hall"), RawInputModifiers.Control);   // 장면 hall = b
        Press(window, tree, StoryHierarchy.EpisodeKey("ch01", "a"), RawInputModifiers.Control);
        CheckBox hide = HideBox(window);
        Assert.False(hide.IsChecked);

        ClickBox(window, hide);
        Assert.All(new[] { root, a, b }, card => Assert.Contains(card, session.HiddenChoiceNodeIds));
        Assert.True(hide.IsChecked);

        // 하나만 풀면 섞인다 — 반쯤. 누르면 모두 켠다.
        session.SetHideEmptyChoices(root, false);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(hide.IsChecked);
        ClickBox(window, hide);
        Assert.All(new[] { root, a, b }, card => Assert.Contains(card, session.HiddenChoiceNodeIds));

        // 끄면 모두 끈다.
        ClickBox(window, hide);
        Assert.All(new[] { root, a, b }, card => Assert.DoesNotContain(card, session.HiddenChoiceNodeIds));

        // 그냥 누르면 그 카드 하나다.
        Press(window, tree, StoryHierarchy.EpisodeKey("ch01", "a"));
        ClickBox(window, HideBox(window));
        Assert.Contains(a, session.HiddenChoiceNodeIds);
        Assert.DoesNotContain(b, session.HiddenChoiceNodeIds);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static CheckBox HideBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single().FindControl<CheckBox>("HideEmptyChoicesBox")!;

    private static void ClickBox(MainWindow window, CheckBox box)
    {
        Assert.True(box.IsEffectivelyVisible);
        box.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        Point center = box.TranslatePoint(new Point(8, box.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>도구 막대의 단추 — 풍선 글의 머리로 찾는다.</summary>
    private static Button Bar(GraphEditorView graph, string tipHead) =>
        graph.FindControl<StackPanel>("WriterToolbar")!.GetVisualDescendants().OfType<Button>()
            .First(item => (ToolTip.GetTip(item) as string)?.StartsWith(tipHead, StringComparison.Ordinal) == true);

    /// <summary>도구 막대의 값 칸을 마우스로 눌러 글을 적고 Enter.</summary>
    private static void TypeInto(MainWindow window, TextBox entry, string text)
    {
        Point center = entry.TranslatePoint(new Point(entry.Bounds.Width / 2, entry.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(entry.IsFocused);
        entry.Text = text;
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    /// <summary>카드의 줄마다 — 대사 글 길이 · 서식 구간 · 그 줄의 가장 큰 크기 차.</summary>
    private static List<(int Length, IReadOnlyList<WriterSpan> Spans, int Grow)> Lines(AuthoringSession session, string nodeId)
    {
        DialogueNode node = session.Project.FindDialogue(nodeId)!;
        return node.Script.ActiveLines.Select(line =>
        {
            IReadOnlyList<WriterSpan> spans = node.WriterMarks.GetValueOrDefault(line.Id) ?? [];
            return (node.Script.Text(line.Id).Text.Length, spans, spans.Count == 0 ? 0 : spans.Max(span => span.Look.Grow));
        }).ToList();
    }


    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph, StoryHierarchy Tree) Open()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        StoryHierarchy tree = graph.HierarchyView;

        foreach (string key in new[]
                 {
                     StoryHierarchy.ChapterKey("ch01"), StoryHierarchy.SceneKey("ch01", "opening"), StoryHierarchy.SceneKey("ch01", "hall"),
                     StoryHierarchy.ChapterKey("ch02")
                 })
        {
            tree.Fold(key, collapse: false);
        }

        Dispatcher.UIThread.RunJobs();
        return (window, session, graph, tree);
    }

    /// <summary>그 줄을 마우스로 누른다 — 보조 키와 함께.</summary>
    private static void Press(MainWindow window, StoryHierarchy tree, string key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Button button = tree.RowButton(key)!;
        button.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        Point center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left, modifiers);
        window.MouseUp(center, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Keys(StoryHierarchy tree) => tree.Selections.Select(pick => pick.Key).ToList();

    private static bool Painted(StoryHierarchy tree, string key) =>
        tree.RowButton(key)!.Background is ISolidColorBrush { Color.A: > 0 };

    private static Button DeleteButton(GraphEditorView graph) => graph.FindControl<Button>("DeleteButton")!;

    private static string Card(AuthoringSession session, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!.Id;
}
