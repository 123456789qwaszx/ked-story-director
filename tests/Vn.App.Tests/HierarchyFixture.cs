using Avalonia.Controls;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// 하이어아키 시험들이 함께 여는 프로젝트 (2026-10-30 HY) — 챕터 둘: ch01은 장면 둘(opening: root · a, hall: b), ch02는
/// 장면ID 없이 x · y.
/// </summary>
internal static class HierarchyFixture
{
    /// <summary>그 폴더에 프로젝트를 저장하고 창을 띄워 연다. <paramref name="tab"/>은 처음 보일 탭(0 챕터 · 1 연출).</summary>
    public static (MainWindow Window, AuthoringSession Session) Open(string directory, int tab = 0)
    {
        Directory.CreateDirectory(directory);

        var editor = new ProjectEditor(new StoryProject { Title = "하이어아키" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "a", title: "a", 220, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "b", title: "b", 440, 0, sceneId: "hall");
        editor.EnsureChapter("ch02");
        editor.EnsureChapterBoard("ch02");
        editor.AddEpisode("ch02", "x", title: "x", 0, 0);
        editor.AddEpisode("ch02", "y", title: "y", 220, 0);

        string manifest = Path.Combine(directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = tab;
        Dispatcher.UIThread.RunJobs();

        return (window, session);
    }
}
