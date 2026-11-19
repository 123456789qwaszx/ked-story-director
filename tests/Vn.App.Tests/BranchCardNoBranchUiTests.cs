using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>분기 카드는 분기를 내지 않는다 — 화면</b> (2026-11-19 소유자 · usability UX27). 분기 카드의 줄 ＋ 차림에는 [＋ 분기 추가]가 없고 까닭을
/// 말한다. 갈래 출구 레일(「→ 분기 이후」)은 글이다 — ⛔ 분기 카드에서는 단추였고 누르면 대상 고르기가 열려 아무 카드에나 이어졌다. 판에서 그
/// 전의 파일에 남은 분기 카드의 분기를 다른 카드로 끌어 놓으면 잇지 않고 상태줄이 까닭을 말한다.
/// </summary>
public sealed class BranchCardNoBranchUiTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-branch-card-no-branch", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 분기_카드의_줄_차림에는_분기_추가가_없고_까닭을_말한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        DialogueNode root = Card(session, "root");
        DialogueNode branch = session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);

        // 에피소드 카드에는 선다.
        session.Select(root.Id);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Buttons(OpenPlus(window)), text => text == "＋ 분기 추가");

        // 분기 카드에는 서지 않고 까닭을 말한다.
        session.Select(branch.Id);
        Dispatcher.UIThread.RunJobs();
        StackPanel panel = OpenPlus(window);
        Assert.DoesNotContain(Buttons(panel), text => text == "＋ 분기 추가");
        Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Text?.StartsWith("분기 카드 안에서는 분기를 내지 않습니다", StringComparison.Ordinal) == true);

        window.Close();
    });

    [Fact]
    public void 갈래_출구_레일은_누를_수_없는_글이다() => HeadlessUi.Run(() =>
    {
        // 그 전의 파일 — 분기 카드의 첫머리 조건 갈래가 다른 분기 카드로 나간다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        (DialogueNode branch, DialogueNode other) = LegacyExit(session);
        session.Select(branch.Id);
        Dispatcher.UIThread.RunJobs();

        TextBlock rail = Assert.Single(Editor(window).LineHost.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == $"→ 분기 이후: {other.Name}");
        Assert.Null(rail.FindAncestorOfType<Button>());
        Assert.Contains("Delete", (string)ToolTip.GetTip(rail)!, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 판에서_분기_카드의_분기를_다른_카드로_끌어_놓으면_잇지_않고_까닭을_말한다() => HeadlessUi.Run(() =>
    {
        // 그 전의 파일 — 분기 카드의 첫 줄 앞에 다른 분기 카드로 가는 분기점.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        DialogueNode root = Card(session, "root");
        string first = root.Script.ActiveLines.First().Id;
        DialogueNode outer = session.Editor.AddBranchMarker(root.Id, first);
        DialogueNode inner = session.Editor.AddBranchMarker(root.Id, first);
        DialogueNode third = session.Editor.AddBranchMarker(root.Id, first);
        outer.RequireExtension(outer.Script.ActiveLines.First().Id).DetourTargetNodeIds.Add(inner.Id);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.RebuildFresh();
        Dispatcher.UIThread.RunJobs();
        long before = session.Editor.Revision;

        DragKnob(graph, outer.Id, onto: third.Id);

        Assert.Equal(before, session.Editor.Revision);
        Assert.Equal([inner.Id], outer.FindExtension(outer.Script.ActiveLines.First().Id)!.DetourTargetNodeIds);
        Assert.StartsWith(ProjectEditor.BranchCardBranchesRefusal, session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    private static DialogueNodeEditor Editor(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();

    private static StackPanel OpenPlus(MainWindow window)
    {
        Editor(window).LineHost.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "＋"))
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        return Editor(window).LineMetaPanel!;
    }

    private static IEnumerable<string?> Buttons(StackPanel panel) =>
        panel.GetLogicalDescendants().OfType<Button>().Select(button => button.Content as string);

    /// <summary>분기 카드(첫머리 조건 「신뢰높음」)의 그 갈래가 다른 분기 카드로 나간다 — 편집기는 만들지 않으니 모델에 바로 적는다.</summary>
    private static (DialogueNode Branch, DialogueNode Other) LegacyExit(AuthoringSession session)
    {
        DialogueNode root = Card(session, "root");
        DialogueNode branch = session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        DialogueNode other = session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        Assert.Null(BranchEntryCondition.Set(session.Editor, branch.Id, "신뢰높음"));
        branch.BranchExits[branch.Script.ActiveLines.First().Id] = other.Id;
        session.Editor.SetScriptLineText(branch.Id, branch.Script.ActiveLines.First().Id, "하린", "한 줄");   // 다시 그리게
        Dispatcher.UIThread.RunJobs();
        return (branch, other);
    }

    /// <summary>그 카드의 분기 점을 눌러 끌고, 다른 카드 위에 놓는다 — 사람이 하는 길 그대로.</summary>
    private static void DragKnob(GraphEditorView graph, string nodeId, string onto)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        Ellipse knob = canvas.Children.OfType<Ellipse>().Single(item =>
            (item.Tag as string)?.StartsWith($"{GraphEditorView.LineKnobTag}execution:{nodeId}:detour:", StringComparison.Ordinal) == true);

        knob.RaiseEvent(new PointerPressedEventArgs(
            knob, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        Border card = canvas.Children.OfType<Border>().First(border => border.Tag as string == onto);
        var on = new Point(Canvas.GetLeft(card) + 20, Canvas.GetTop(card) + 10);

        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, canvas.TranslatePoint(on, root)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }
}
