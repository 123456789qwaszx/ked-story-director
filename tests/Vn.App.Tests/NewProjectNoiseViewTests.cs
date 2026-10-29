using Avalonia.Controls;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// 소유자 보고 — "완전 새 프로젝트인데 검증 보고가 동기화 거부·경고를 계속 띄운다."
/// 툴이 방금 만든 빈 에피소드를 툴 스스로 탓하면 안 된다.
///
/// (2026-10-29 · D2: 그때 탓하던 것은 엑셀 대본 동기화였고, 그 동기화는 걷혔다. 지금 거부할 수
/// 있는 것은 진행 JSON 내보내기다 — 빈 카드를 든 새 에피소드가 그것을 막거나 보고에 오르면
/// 같은 소음이다.)
/// </summary>
public sealed class NewProjectNoiseViewTests
{
    [Fact]
    public void 방금_만든_빈_에피소드는_거부로_보고되지_않는다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        // [＋ 에피소드] — 카드가 함께 서고, 그 카드의 대본은 아직 비어 있다.
        view.AddEpisodeFromToolbar();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        string episodeId = Assert.Single(session.Project.Chapters.Single().Episodes).EpisodeId;

        var panel = view.FindControl<StackPanel>("DiagnosticsPanel")!;
        string[] shown = panel.Children.OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)
            .ToArray();

        // 진행 JSON이 나갔다 — 막혔으면 이 줄 대신 그 결론이 맨 위에 선다.
        Assert.Contains(shown, text => text.StartsWith("진행 JSON SHA-256", StringComparison.Ordinal));

        // 보고의 어느 줄도 방금 만든 에피소드를 탓하지 않는다.
        Assert.DoesNotContain(shown, text => text.Contains(episodeId, StringComparison.Ordinal));
    });

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

    private sealed class TempProject : IDisposable
    {
        private readonly string _directory;

        public TempProject()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "vn-new-noise", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_directory);

            // 갓 만든 챕터 — 스탯 둘만 있고 에피소드는 아직 없다.
            var chapter = new ChapterDocument { ChapterId = "ch01" };
            chapter.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
            chapter.Stats.Add(new ChapterStat("anger", "분노", 0, 0, 10));

            var story = new StoryProject { Title = "새 프로젝트" };
            story.Chapters.Add(chapter);

            ManifestPath = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
            ProjectStore.Save(ManifestPath, story);
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
