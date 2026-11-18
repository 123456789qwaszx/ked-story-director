using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 탭에서 조건 이름을 더하고 고치고 지운다</b> (2026-11-16 · <c>docs/work-orders/writer-fields-orders.md</c> W4). 하이어아키의 챕터 줄을
/// 고르면 스탯 표 아래에 조건 목록이 선다. 식은 [구성] 탭이 정한다 — 새 조건은 「식 미정」(항상 통과)으로 선다.
///
/// 못 박는 것: ① 더하면 식 미정으로 서고 되돌리기 한 칸이다 ② 같은 이름 · 빈 이름은 서지 않는다 ③ 이름을 고치면 간선이 따라간다
/// ④ 쓰는 곳이 있으면 지우기를 거절하고 어디인지 말한다 ⑤ Esc는 넣지 않은 글을 걷는다.
/// </summary>
public sealed class DirectorConditionsTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-conditions", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 더하면_식_미정으로_서고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickChapter(window);

        TextBox add = Box(graph, "condition-add-name");
        add.Text = " 하린과 친해졌을 때 ";
        Key(add, Avalonia.Input.Key.Enter);

        ChapterCondition condition = Assert.Single(Chapter(session).Conditions);
        Assert.Equal(("하린과 친해졌을 때", string.Empty), (condition.Label, condition.Expression));
        Assert.Contains("식 미정", ((TextBlock)Control(graph, "condition-expression:하린과 친해졌을 때")).Text);
        Assert.Equal("하린과 친해졌을 때", Box(graph, "condition-label:하린과 친해졌을 때").Text);
        Assert.Same(Box(graph, "condition-add-name"), window.FocusManager?.GetFocusedElement());

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(Chapter(session).Conditions);
        Assert.Null(Find(graph, "condition-label:하린과 친해졌을 때"));

        window.Close();
    });

    [Fact]
    public void 같은_이름과_빈_이름은_더하지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.AddChapterCondition("ch01", "친해짐", "trust >= 3");
        GraphEditorView graph = PickChapter(window);

        Box(graph, "condition-add-name").Text = "친해짐";
        Click(Control(graph, "condition-add"));
        Assert.Contains("이미 있습니다", session.StatusMessage);

        Box(graph, "condition-add-name").Text = "  ";
        Click(Control(graph, "condition-add"));
        Assert.Contains("적어 주세요", session.StatusMessage);

        Assert.Equal(["친해짐"], Chapter(session).Conditions.Select(item => item.Label));
        Assert.Equal("식 trust >= 3", ((TextBlock)Control(graph, "condition-expression:친해짐")).Text);

        window.Close();
    });

    [Fact]
    public void 이름을_고치면_간선이_따라가고_식은_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        session.Editor.UpdateEdge("ch01", "root", "a", conditionLabel: "신뢰높음", matchOptionLabel: "믿는다");
        GraphEditorView graph = PickChapter(window);

        TextBox name = Box(graph, "condition-label:신뢰높음");
        name.Text = "하린과 친해졌을 때";
        Key(name, Avalonia.Input.Key.Enter);

        ChapterCondition condition = Assert.Single(Chapter(session).Conditions);
        Assert.Equal(("하린과 친해졌을 때", "trust >= 3"), (condition.Label, condition.Expression));
        Assert.Equal("하린과 친해졌을 때", Chapter(session).Edges.Single(edge => edge.ToEpisodeId == "a").ConditionLabel);
        Assert.Contains("간선 1개가 따라갔습니다", session.StatusMessage);
        Assert.Equal("하린과 친해졌을 때", Box(graph, "condition-label:하린과 친해졌을 때").Text);

        window.Close();
    });

    [Fact]
    public void 이미_있는_이름이나_빈_이름으로는_바꾸지_않고_칸이_돌아온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.AddChapterCondition("ch01", "친해짐", "trust >= 3");
        session.Editor.AddChapterCondition("ch01", "멀어짐", "trust < 3");
        GraphEditorView graph = PickChapter(window);

        TextBox far = Box(graph, "condition-label:멀어짐");
        far.Text = "친해짐";
        Key(far, Avalonia.Input.Key.Enter);
        Assert.Contains("이미 있습니다", session.StatusMessage);
        Assert.Equal("멀어짐", far.Text);

        far.Text = " ";
        Key(far, Avalonia.Input.Key.Enter);
        Assert.Contains("비울 수 없습니다", session.StatusMessage);
        Assert.Equal("멀어짐", far.Text);

        Assert.Equal(["친해짐", "멀어짐"], Chapter(session).Conditions.Select(item => item.Label));

        window.Close();
    });

    [Fact]
    public void 쓰는_곳이_있으면_지우기를_거절하고_어디인지_말한다_없으면_지운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        session.Editor.AddChapterCondition("ch01", "안쓰는것", string.Empty);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        session.Editor.UpdateEdge("ch01", "root", "a", visibleConditionLabel: "신뢰높음", matchOptionLabel: "믿는다");
        GraphEditorView graph = PickChapter(window);

        Click(Control(graph, "condition-remove:신뢰높음"));
        Assert.Contains("쓰고 있습니다", session.StatusMessage);
        Assert.Contains("root → a", session.StatusMessage);

        Click(Control(graph, "condition-remove:안쓰는것"));
        Assert.Equal(["신뢰높음"], Chapter(session).Conditions.Select(item => item.Label));
        Assert.Null(Find(graph, "condition-label:안쓰는것"));

        window.Close();
    });

    [Fact]
    public void Esc는_넣지_않은_이름을_걷는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.AddChapterCondition("ch01", "친해짐", "trust >= 3");
        GraphEditorView graph = PickChapter(window);

        TextBox name = Box(graph, "condition-label:친해짐");
        name.Text = "친해짐이다";
        Key(name, Avalonia.Input.Key.Escape);

        Assert.Equal("친해짐", name.Text);
        Assert.Equal("친해짐", Assert.Single(Chapter(session).Conditions).Label);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static GraphEditorView PickChapter(MainWindow window)
    {
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ch01", graph.InspectorHeaderText);   // 챕터 요약 — 머리는 이름만(2026-11-18)
        return graph;
    }

    private static ChapterDocument Chapter(AuthoringSession session) => session.Editor.FindChapter("ch01")!;

    private static IEnumerable<Control> Side(GraphEditorView graph) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Control>().Distinct()
            .Where(control => control.IsEffectivelyVisible);

    private static Control? Find(GraphEditorView graph, string tag) =>
        Side(graph).SingleOrDefault(control => (string?)control.Tag == tag);

    private static Control Control(GraphEditorView graph, string tag) =>
        Find(graph, tag) ?? throw new Xunit.Sdk.XunitException($"'{tag}'가 없습니다.");

    private static TextBox Box(GraphEditorView graph, string tag) => (TextBox)Control(graph, tag);

    private static void Click(Control button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(TextBox box, Key key)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = box });
        Dispatcher.UIThread.RunJobs();
    }
}
