using Avalonia.Controls;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// 사람이 툴에서 만든 <b>새 챕터</b>에서 간선과 선택지 문구가 실제 순서 그대로 도는지 —
/// 툴이 길을 놓으면 기본 문구가 함께 들어가고, 문구는 간선 패널에서 고친다.
/// </summary>
public sealed class ChapterEdgeLabelTests
{
    [Fact]
    public void 툴이_이은_길에는_기본_문구가_들어간다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        // 사람이 하는 그대로: 노드 하나 → 그 자식 하나 (간선이 함께 생긴다).
        view.AddEpisodeFromToolbar();
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.SelectEpisode("new01");
        view.AddEpisodeFromToolbar();
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ChapterEdge fresh = Assert.Single(Chapter(session).Edges,
            edge => edge.FromEpisodeId == "new01" && edge.ToEpisodeId == "new02");
        // v12 — 툴이 길을 놓으면 문구가 함께 들어간다. 빈 문구는 이제 오류다.
        Assert.Equal("계속", fresh.OptionLabel);
        Assert.False(fresh.HasNoOptionLabel);
    });

    [Fact]
    public void 간선_패널의_에피소드를_누르면_그_에피소드_편집으로_간다() => HeadlessUi.Run(() =>
    {
        // 소유자 보고 — 간선을 클릭한 뒤 거기 보이는 에피소드 이름을 직접 고치려 했다.
        // 그 자리는 읽기 전용이었다. 이제 누르면 그 에피소드의 속성 패널(Id [개명])로 간다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.AddEpisodeFromToolbar();
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.SelectEpisode("new01");
        view.AddEpisodeFromToolbar();
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.SelectEdgeKey("new01", "new02", "계속");
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        // 도착 쪽 고리를 누른다 (출발 → 화살표 → 도착 순).
        var links = view.FindControl<StackPanel>("EdgeFromToPanel")!;
        var target = (TextBlock)links.Children[2];
        Assert.Contains("new02", target.Text);

        target.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            target, new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            target, default, 0,
            new Avalonia.Input.PointerPointProperties(
                Avalonia.Input.RawInputModifiers.LeftMouseButton,
                Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
            Avalonia.Input.KeyModifiers.None));

        // 이제 에피소드 편집 자리다 — 여기서 [개명]으로 이름을 바꾼다.
        Assert.True(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.Equal("new02", view.FindControl<TextBox>("IdBox")!.Text);

        view.FindControl<TextBox>("IdBox")!.Text = "ep_left";
        view.RenameSelectedEpisode();

        ChapterGraphModel reread = Chapter(session);
        Assert.NotNull(reread.FindEpisode("ep_left"));
        Assert.Contains(reread.Edges, edge =>
            edge.FromEpisodeId == "new01" && edge.ToEpisodeId == "ep_left");
    });

    [Fact]
    public void 다시_읽기가_끼어들어도_적던_잠금_안내문이_사라지지_않는다() => HeadlessUi.Run(() =>
    {
        // 판은 언제든 다시 선다(프로젝트의 어느 변경이든 다시 읽기를 부른다). 그 사이에 적어 둔
        // 값이 조용히 모델 값으로 되돌아가면, 사람 눈에는 "적용을 눌러도 안 바뀐다"로 보인다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.AddEpisodeFromToolbar();
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.SelectEpisode("new01");
        view.AddEpisodeFromToolbar();
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.SelectEdgeKey("new01", "new02", "계속");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.FindControl<TextBox>("EdgeLockedMsgBox")!.Text = "아직 이르다";

        // 적어 둔 값에 포커스가 없는 상태에서 다시 읽기가 끼어든다 — 모델 값으로 되돌아가면 안 된다.
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.ApplyEdgeFromPanel();

        ChapterEdge saved = Chapter(session).Edges.Single(edge =>
            edge.FromEpisodeId == "new01" && edge.ToEpisodeId == "new02");

        Assert.Equal("아직 이르다", saved.LockedMessage);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static ChapterGraphModel Chapter(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.ToGraphModel("ch01");

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

    /// <summary>툴이 [＋ 챕터]로 만드는 것과 같은 빈 챕터 — 견본이 아니다.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory;

        public TempProject()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "vn-edge-label", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_directory);

            // [＋ 챕터](AuthoringSession.CreateChapter)와 같은 순서 — 챕터 · 스탯 · 판.
            var editor = new ProjectEditor(new StoryProject { Title = "간선 이름 검증" });
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
