using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// [구성] 탭 오른쪽 칼럼의 빈 곳을 누르면 Enter처럼 — 간선 패널의 글자 칸(문구 · 잠금 안내문 · 스탯변화 숫자)에 적던 것이
/// 반영되고 칸이 놓인다. 빈 곳(글 · 여백)은 초점을 받지 않아, 전에는 칸이 초점을 쥔 채 남고 반영되지 않았다.
/// </summary>
public sealed class ChapterInspectorReleaseTests
{
    [Fact]
    public void 문구를_적고_칼럼_빈_곳을_누르면_반영되고_칸이_놓인다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (Window window, ChapterGraphView view, AuthoringSession session) = Show(project);

        TextBox label = view.FindControl<TextBox>("EdgeLabelEditBox")!;
        label.Focus();
        label.Text = "카스미에게 맡긴다";
        PressEmpty(view);

        Assert.Equal("카스미에게 맡긴다", Edge(session).OptionLabel);
        Assert.False(label.IsFocused);

        window.Close();
    });

    [Fact]
    public void 잠금_안내문을_적고_칼럼_빈_곳을_누르면_반영되고_칸이_놓인다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (Window window, ChapterGraphView view, AuthoringSession session) = Show(project);

        TextBox message = view.FindControl<TextBox>("EdgeLockedMsgBox")!;
        message.Focus();
        message.Text = "신뢰가 부족하다";
        PressEmpty(view);

        Assert.Equal("신뢰가 부족하다", Edge(session).LockedMessage);
        Assert.False(message.IsFocused);

        window.Close();
    });

    [Fact]
    public void 스탯변화_숫자를_적고_칼럼_빈_곳을_누르면_반영되고_칸이_놓인다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (Window window, ChapterGraphView view, AuthoringSession session) = Show(project);

        StatChangeEditor stats = view.FindControl<StackPanel>("EdgeStatsHost")!.Children.OfType<StatChangeEditor>().Single();
        stats.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        TextBox amount = stats.Children.OfType<Grid>().Last().Children.OfType<TextBox>().Single();
        amount.Focus();
        amount.Text = "3";
        PressEmpty(view);

        Assert.Equal(new StatDelta("trust", 3), Assert.Single(Edge(session).StatChanges));
        Assert.False(amount.IsFocused);

        window.Close();
    });

    [Fact]
    public void 적던_칸_안을_누르면_놓지_않는다() => HeadlessUi.Run(() =>
    {
        // 칸 안을 누르는 것은 글자 자리를 옮기는 손짓이다 — 거기서 반영하고 칸을 놓으면 쓰던 글이 끊긴다.
        using var project = new TempProject();
        (Window window, ChapterGraphView view, AuthoringSession session) = Show(project);

        TextBox label = view.FindControl<TextBox>("EdgeLabelEditBox")!;
        label.Focus();
        label.Text = "쓰는 중";
        Press(label);

        Assert.True(label.IsFocused);
        Assert.Equal("계속", Edge(session).OptionLabel);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>간선 패널의 빈 곳 — 글자 칸도 단추도 아닌 패널 자체.</summary>
    private static void PressEmpty(ChapterGraphView view) => Press(view.FindControl<StackPanel>("EdgePanel")!);

    private static void Press(Control target)
    {
        target.RaiseEvent(new PointerPressedEventArgs(
            target, new Pointer(0, PointerType.Mouse, true), target, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static ChapterEdge Edge(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.ToGraphModel("ch01").Edges.Single(edge =>
            edge.FromEpisodeId == "new01" && edge.ToEpisodeId == "new02");

    private static (Window Window, ChapterGraphView View, AuthoringSession Session) Show(TempProject project)
    {
        var session = new AuthoringSession();
        session.Open(project.ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1400, Height = 800, Content = view };
        window.Show();
        view.Attach(session);
        Dispatcher.UIThread.RunJobs();

        EpisodeAdder.Add(session, "ch01", after: null, sceneId: null);
        EpisodeAdder.Add(session, "ch01", after: "new01", sceneId: null);
        view.Refresh();
        Dispatcher.UIThread.RunJobs();

        view.SelectEdgeKey("new01", "new02", "계속");
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        return (window, view, session);
    }

    /// <summary>툴이 [＋ 챕터]로 만드는 것과 같은 빈 챕터 — 스탯 둘.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory;

        public TempProject()
        {
            _directory = Path.Combine(Path.GetTempPath(), "vn-inspector-release", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);

            var editor = new ProjectEditor(new StoryProject { Title = "칼럼 빈 곳 누르기" });
            ChapterDocument chapter = editor.EnsureChapter("ch01");
            chapter.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 100));
            chapter.Stats.Add(new ChapterStat("anger", "분노", 0, 0, 100));
            editor.EnsureChapterBoard("ch01");

            ManifestPath = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
            ProjectStore.Save(ManifestPath, editor.Project);
        }

        public string ManifestPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
