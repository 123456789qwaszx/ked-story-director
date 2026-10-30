using Avalonia.Controls;
using Avalonia.Interactivity;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>챕터 그래프의 [자동 정렬]은 연출 그래프의 모양을 본뜬다</b> (2026-10-30 소유자: *"챕터 그래프의 자동 정렬 버튼을
/// 눌렀을 때, 연출 그래프에 배치된 노드들을 참고해서 그와 거의 같은 형태로 노드가 자동으로 배치되도록"*).
///
/// 에피소드마다 그 카드가 연출 그래프(챕터의 판)에 선 자리를 챕터 그래프의 간격으로 줄인다 — 연출 그래프의 열 하나(320)가
/// 챕터의 옆 칸(220), 줄 하나(400)가 깊이 한 칸(170)이다. 줄이는 규칙은 <c>ChapterBranchPlannerTests</c>가 본다.
/// </summary>
public sealed class ChapterAutoArrangeTests
{
    [Fact]
    public void 자동_정렬은_연출_그래프의_카드_자리를_본떠_세우고_되돌리기_한_번이면_돌아온다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        // 연출 그래프에서 root 아래 왼쪽에 a, 오른쪽에 b를 놓았다.
        session.Editor.MoveNode(Card(session, "root").Id, 0, 0);
        session.Editor.MoveNode(Card(session, "a").Id, -320, 400);
        session.Editor.MoveNode(Card(session, "b").Id, 320, 400);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Dictionary<string, (double X, double Y)> before = Places(session);

        view.FindControl<Button>("AutoArrangeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Dictionary<string, (double X, double Y)> after = Places(session);
        Assert.Equal((220, 0), after["root"]);
        Assert.Equal((0, 170), after["a"]);
        Assert.Equal((440, 170), after["b"]);
        Assert.Contains("연출 그래프의 모양대로", session.StatusMessage, StringComparison.Ordinal);

        // 한 번의 변경이라 한 번에 물러난다.
        session.Editor.Undo();
        Assert.Equal(before, Places(session));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    private static Dictionary<string, (double X, double Y)> Places(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.Episodes.ToDictionary(
            episode => episode.EpisodeId, episode => (episode.X, episode.Y), StringComparer.Ordinal);

    private static (ChapterGraphView View, AuthoringSession Session) Show(TempProject project)
    {
        var session = new AuthoringSession();
        session.Open(project.ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1400, Height = 800, Content = view };
        window.Show();
        view.Attach(session);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        project.Ui.Own(view, window);

        return (view, session);
    }

    /// <summary>에피소드 셋(root → a, root → b)을 둔 챕터 하나.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "vn-chapter-arrange", Guid.NewGuid().ToString("N"));

        public OpenChapterViews Ui { get; } = new();

        public TempProject()
        {
            Directory.CreateDirectory(_directory);

            var editor = new ProjectEditor(new StoryProject { Title = "자동 정렬" });
            editor.EnsureChapter("ch01");
            editor.EnsureChapterBoard("ch01");
            editor.AddEpisode("ch01", "root", title: "root", 0, 0);
            editor.AddEpisode("ch01", "a", title: "a", 0, 0);
            editor.AddEpisode("ch01", "b", title: "b", 0, 0);

            ManifestPath = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
            ProjectStore.Save(ManifestPath, editor.Project);
        }

        public string ManifestPath { get; }

        public void Dispose()
        {
            Ui.CloseAll();

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
