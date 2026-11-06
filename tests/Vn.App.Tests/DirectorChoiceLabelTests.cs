using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 탭 선 설명의 「문구」 칸</b> — 선택지 레인을 고르면 인스펙터에서 그 문구를 고친다(Enter · 딴 데를 누르면 넣고 Esc는
/// 되돌린다). 자동 길(AUTO)에 문구를 적으면 선택지가 되고, 그 에피소드의 하나뿐인 선택지 문구를 지우면 자동 길이 된다. 길이
/// 둘 이상이거나 장면을 넘는 길이면 문구를 비우지 않는다.
/// </summary>
public sealed class DirectorChoiceLabelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-choice-label", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 문구를_고치고_Enter를_누르면_선택지_문구가_바뀌고_그_선을_계속_본다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        Pick(graph, "a", "가자");

        TextBox box = LabelBox(window)!;
        Assert.Equal("가자", box.Text);

        box.Text = "왼쪽으로";
        Enter(box);

        Assert.Equal("왼쪽으로", Edge(session, "a").OptionLabel);
        Assert.False(Edge(session, "a").Auto);
        Assert.Equal("왼쪽으로", LabelBox(window)!.Text);   // 바뀐 신원으로 그 선을 다시 골랐다

        window.Close();
    });

    [Fact]
    public void 자동_길에_문구를_적으면_선택지가_되고_하나뿐인_문구를_지우면_자동_길이_된다() => HeadlessUi.Run(() =>
    {
        // root · a는 같은 장면(opening)이다 — 자동 길이 설 수 있다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: null, auto: true);
        Pick(graph, "a", string.Empty);

        TextBox box = LabelBox(window)!;
        Assert.Equal(string.Empty, box.Text);

        box.Text = "가자";
        Enter(box);
        Assert.False(Edge(session, "a").Auto);
        Assert.Equal("가자", Edge(session, "a").OptionLabel);

        box = LabelBox(window)!;
        box.Text = string.Empty;
        Enter(box);
        Assert.True(Edge(session, "a").Auto);
        Assert.True(Edge(session, "a").HasNoOptionLabel);
        Assert.Equal(string.Empty, LabelBox(window)!.Text);

        window.Close();
    });

    [Fact]
    public void 길이_둘_이상이거나_장면을_넘으면_문구를_비우지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();

        // 장면을 넘는 하나뿐인 길 — root(opening) → b(hall).
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "복도로");
        Pick(graph, "b", "복도로");
        TextBox box = LabelBox(window)!;
        box.Text = string.Empty;
        Enter(box);
        Assert.Equal("복도로", Edge(session, "b").OptionLabel);
        Assert.False(Edge(session, "b").Auto);
        Assert.Contains("같은 장면", session.StatusMessage, StringComparison.Ordinal);

        // 길이 둘 — 문구 없는 선택지는 오류다.
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        Pick(graph, "a", "가자");
        box = LabelBox(window)!;
        box.Text = string.Empty;
        Enter(box);
        Assert.Equal("가자", Edge(session, "a").OptionLabel);
        Assert.Equal("가자", LabelBox(window)!.Text);   // 칸도 원래 문구로 돌아왔다

        window.Close();
    });

    [Fact]
    public void 딴_데를_누르면_Enter처럼_넣고_Esc는_되돌린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        Pick(graph, "a", "가자");

        // Esc — 칸만 되돌린다.
        TextBox box = LabelBox(window)!;
        box.Focus();
        box.Text = "안 쓸 문구";
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("가자", box.Text);

        // 초점이 떠나면 넣는다.
        box.Text = "옆으로";
        graph.FindControl<Border>("WritingFocusSink")!.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("옆으로", Edge(session, "a").OptionLabel);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Open()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    /// <summary>root에서 그 도착으로 가는 선택지 레인을 판에서 고른다.</summary>
    private static void Pick(GraphEditorView graph, string to, string label)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.True(graph.SelectChoiceRail("ch01", "root", to, label));
        Dispatcher.UIThread.RunJobs();
    }

    private static ChapterEdge Edge(AuthoringSession session, string to) =>
        session.Editor.FindChapter("ch01")!.ToGraphModel("ch01").Edges
            .Single(edge => edge.FromEpisodeId == "root" && edge.ToEpisodeId == to);

    /// <summary>인스펙터 선 설명의 「문구」 칸 — 선을 고르지 않았으면 null.</summary>
    private static TextBox? LabelBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<TextBox>().Distinct()
            .SingleOrDefault(box => box.IsEffectivelyVisible && box.Tag as string == "summary-label");

    private static void Enter(TextBox box)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();
    }
}
