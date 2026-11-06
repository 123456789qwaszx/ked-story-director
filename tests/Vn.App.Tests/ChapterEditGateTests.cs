using Avalonia.Controls;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>간선 패널의 자동 저장</b> — 간선을 편집할 때 [적용]을 누르지 않아도
/// 바로 변화가 반영된다.
/// </summary>
public sealed class ChapterEditGateTests
{
    // ── 간선 패널은 고치는 순간 저장된다 ───────────────────────────────────

    [Fact]
    public void 조건을_고르면_적용_없이_간선에_써진다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.FindControl<ComboBox>("EdgeVisibleCombo")!.SelectedItem = "지쳐있음";

        Assert.Equal("지쳐있음", Edge(session).VisibleConditionLabel);
    });

    [Fact]
    public void 문구를_바꾼_직후에_고른_조건도_써진다() => HeadlessUi.Run(() =>
    {
        // 문구는 간선의 신원이다 — 한 번 고치면 손에 든 모델이 낡는다. 그 상태로 다음
        // 저장을 부르면 간선을 못 찾아 조건이 조용히 안 써졌다(이 테스트가 잡았다).
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 문구를 고쳐 Enter로 저장한다 — 문구는 간선의 신원이라 다음 저장이 그 간선을 새 열쇠로 찾아야 한다.
        var label = view.FindControl<TextBox>("EdgeLabelEditBox")!;
        label.Text = "혼자 문을 연다";
        label.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Enter
        });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.FindControl<ComboBox>("EdgeVisibleCombo")!.SelectedItem = "지쳐있음";

        ChapterEdge reread = Edge(session);

        Assert.Equal("혼자 문을 연다", reread.OptionLabel);
        Assert.Equal("지쳐있음", reread.VisibleConditionLabel);
    });

    [Fact]
    public void 잠금_안내문은_초점을_잃을_때_써진다() => HeadlessUi.Run(() =>
    {
        // 자판 하나마다 저장하면 글자마다 되돌리기 한 칸이 쌓이고, 그 변경이 판을 다시 세워
        // 쓰던 글을 끊는다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        string? before = Edge(session).LockedMessage;

        var box = view.FindControl<TextBox>("EdgeLockedMsgBox")!;
        box.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        box.Text = "아직 이르다";

        Assert.Equal(before, Edge(session).LockedMessage);   // 치는 동안은 안 나간다

        // 초점을 진짜로 옮긴다 — LostFocus는 FocusChangedEventArgs를 요구한다.
        view.FindControl<ComboBox>("EdgeVisibleCombo")!.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("아직 이르다", Edge(session).LockedMessage);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>견본의 그 길 — 프로젝트의 챕터에서 다시 읽는다.</summary>
    private static ChapterEdge Edge(AuthoringSession session) =>
        session.Editor.FindChapter("ch05")!.Edges
            .Single(edge => edge.ToEpisodeId == "branch05.02A");

    private static (ChapterGraphView View, AuthoringSession Session) Show(TempProject project)
    {
        var session = new AuthoringSession();
        session.Open(project.ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1400, Height = 800, Content = view };
        window.Show();
        view.Attach(session);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (view, session);
    }

    /// <summary>견본 챕터(ch05 복도)를 든 프로젝트.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "vn-edit-gate", Guid.NewGuid().ToString("N"));

        public TempProject()
        {
            ManifestPath = SampleProject.Corridor(_directory, "편집 스위치 검증", project =>
                project.Files.Add(new StoryFile("sf_main", "본편", "story/main.vnstory.json")));
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
