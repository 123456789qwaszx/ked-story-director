using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Editing;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>바깥에서 바뀐 프로젝트를 덮어쓰지 않는다</b> (2026-11-19 소유자 — 작가와 프로젝트를 Git으로 주고받는다 · writer-release WR1). 앱을 켜 둔 채
/// GitHub Desktop으로 받으면(Pull) 파일이 바뀐다 — ⛔ 그 전에는 다음 저장 · 10분 자동 저장이 받은 것을 말없이 덮었다. 이제 —
/// 여기서 고친 것이 없으면 창으로 돌아올 때 다시 읽고, 있으면 위 띠가 [다시 열기] · [내 것으로 덮어쓰기]를 고르게 하며 그때까지 저장하지 않는다.
/// </summary>
public sealed class OutsideChangeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-outside-change", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 세션은_바깥에서_바뀐_파일_위에_저장하지_않고_덮어쓰기로_정하면_쓴다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        Assert.False(session.ChangedOutside);

        PullSpeaker(session.ProjectPath!, "받은화자");
        session.Editor.AddSpeaker("내화자");

        Assert.True(session.ChangedOutside);
        Assert.Throws<ProjectChangedOutsideException>(() => session.Save());
        Assert.Contains("받은화자", File.ReadAllText(session.ProjectPath!));   // 받은 것이 그대로다
        Assert.DoesNotContain("내화자", File.ReadAllText(session.ProjectPath!));

        session.Save(overwriteOutsideChanges: true);
        Assert.Contains("내화자", File.ReadAllText(session.ProjectPath!));
        Assert.False(session.ChangedOutside);

        window.Close();
    });

    [Fact]
    public void 고친_것이_없으면_창으로_돌아올_때_다시_읽는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);

        PullSpeaker(session.ProjectPath!, "받은화자");
        window.CheckOutsideChanges();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(session.Project.FindSpeaker("받은화자"));
        Assert.False(session.ChangedOutside);
        Assert.False(Bar(window).IsVisible);
        Assert.Contains("다시 읽었습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 고친_것이_있으면_띠가_서고_저장을_멈추며_덮어쓰기와_다시_열기를_고른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);

        PullSpeaker(session.ProjectPath!, "받은화자");
        session.Editor.AddSpeaker("내화자");
        window.CheckOutsideChanges();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Bar(window).IsVisible);
        Assert.Null(session.Project.FindSpeaker("받은화자"));   // 다시 읽지 않았다 — 고친 것이 남아 있다

        // 저장(Ctrl+S · File → 저장)은 쓰지 않고 띠를 가리킨다.
        window.FindControl<MenuItem>("SaveMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain("내화자", File.ReadAllText(session.ProjectPath!));
        Assert.Contains("위 띠에서", session.StatusMessage, StringComparison.Ordinal);

        // [다시 열기] — 받은 것을 읽고 여기서 고친 것은 버린다.
        Click(window, "OutsideReloadButton");
        Assert.NotNull(session.Project.FindSpeaker("받은화자"));
        Assert.Null(session.Project.FindSpeaker("내화자"));
        Assert.False(Bar(window).IsVisible);

        // 다시 — 이번에는 [내 것으로 덮어쓰기].
        PullSpeaker(session.ProjectPath!, "또받은화자");
        session.Editor.AddSpeaker("또내화자");
        window.CheckOutsideChanges();
        Dispatcher.UIThread.RunJobs();
        Assert.True(Bar(window).IsVisible);

        Click(window, "OutsideOverwriteButton");
        string saved = File.ReadAllText(session.ProjectPath!);
        Assert.Contains("또내화자", saved);
        Assert.DoesNotContain("또받은화자", saved);
        Assert.False(Bar(window).IsVisible);
        Assert.False(session.IsDirty);

        window.Close();
    });

    [Fact]
    public void 바뀐_것이_없으면_아무_일도_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        session.Editor.AddSpeaker("내화자");
        string status = session.StatusMessage;

        window.CheckOutsideChanges();
        Dispatcher.UIThread.RunJobs();

        Assert.False(Bar(window).IsVisible);
        Assert.NotNull(session.Project.FindSpeaker("내화자"));
        Assert.Equal(status, session.StatusMessage);

        window.Close();
    });

    /// <summary>다른 곳(다른 PC → Git으로 받기)이 프로젝트에 화자 하나를 더해 저장했다.</summary>
    private static void PullSpeaker(string manifest, string name)
    {
        var other = new ProjectEditor(ProjectStore.Load(manifest).Project);
        other.AddSpeaker(name);
        ProjectStore.Save(manifest, other.Project);
    }

    private static Border Bar(MainWindow window) => window.FindControl<Border>("OutsideChangeBar")!;

    private static void Click(MainWindow window, string name)
    {
        window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
