using Avalonia;
using Avalonia.Controls;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;
using Path = System.IO.Path;

namespace Vn.App.Tests;

/// <summary>
/// 챕터 화면이 <b>얼마나 일하는가</b> (2026-08-18 성능).
///
/// 소유자 보고 — "노드가 조금만 많아져도 엄청 렉이 심하고". 재 보니 노드 60개에서 첫
/// 화면이 58초였고, 그중 판을 그리는 값은 2.6ms였다. 나머지는 전부 <b>같은 일을 여러 번</b>
/// 한 값이었다. 시간(ms)은 기계마다 달라 고정할 수 없으므로, 여기서는 <b>일의 횟수</b>를
/// 건다 — 느려지는 회귀는 언제나 "몇 번 하는가"가 먼저 늘어난다.
/// </summary>
public sealed class ChapterGraphWorkAmountTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-work-amount", Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

    public ChapterGraphWorkAmountTests()
    {
        Directory.CreateDirectory(_directory);

        var editor = new ProjectEditor(new StoryProject { Title = "일의 양" });
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));

        string previous = string.Empty;

        for (int index = 0; index < 6; index++)
        {
            string id = $"ep{index}";
            editor.AddEpisode("ch01", id, title: $"제목{index}", index, 0);

            if (previous.Length > 0)
            {
                editor.AddEdge("ch01", previous, id);
            }

            previous = id;
        }

        ProjectStore.Save(ManifestPath, editor.Project);
    }

    /// <summary>이 클래스가 띄운 화면. 폴더를 지우기 <b>전에</b> 닫는다.</summary>
    private readonly OpenChapterViews _ui = new();

    public void Dispose()
    {
        _ui.CloseAll();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 상태줄_한_줄이_프로젝트_변경으로_방송되지_않는다() => HeadlessUi.Run(() =>
    {
        // 이것이 58초의 뿌리였다: 상태를 적으면 프로젝트가 바뀌었다고 방송됐고, 챕터
        // 화면은 그 신호에 워크북을 전부 다시 읽었다. 그 재읽기가 다시 상태를 적었다.
        var session = new AuthoringSession();
        session.Open(ManifestPath);

        int projectChanges = 0;
        int statusChanges = 0;
        session.Changed += (_, _) => projectChanges++;
        session.StatusChanged += (_, _) => statusChanges++;

        session.SetStatus("에피소드 3개를 반영했습니다.");

        Assert.Equal(0, projectChanges);
        Assert.Equal(1, statusChanges);
        Assert.Equal("에피소드 3개를 반영했습니다.", session.StatusMessage);
    });

    [Fact]
    public void 몰려_오는_변경은_재읽기_한_번으로_합쳐진다() => HeadlessUi.Run(() =>
    {
        // 일 한 번이 프로젝트 변경을 수십 개 낸다(옛 엑셀 동기화가 그랬다). 예전에는 그
        // 하나하나가 전체 재읽기를 예약해서, 마지막 한 번 말고는 전부 버려질 그림을 그렸다.
        (ChapterGraphView view, AuthoringSession session, Window window) = Show();

        int reloads = 0;
        view.EntriesReloaded += _ => reloads++;

        for (int index = 0; index < 20; index++)
        {
            session.Editor.AddStoryFile($"판{index}");
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, reloads);

        window.Close();
    });

    [Fact]
    public void 모델이_그대로면_다시_증명하지_않는다() => HeadlessUi.Run(() =>
    {
        // 검증은 상태공간을 훑는다 — 챕터 하나에 200ms 가까이. 모델이 그대로면 결과가
        // 달라질 수 없다.
        (ChapterGraphView view, _, Window window) = Show();

        int before = view.ValidationComputeCount;

        for (int index = 0; index < 5; index++)
        {
            view.Refresh();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(before, view.ValidationComputeCount);

        window.Close();
    });

    [Fact]
    public void 모델이_바뀌면_다시_증명한다() => HeadlessUi.Run(() =>
    {
        // 지문이 낡은 결과를 붙들면 "고쳤는데 오류가 그대로"가 된다 — 캐시가 만드는
        // 가장 나쁜 거짓말이다. 지문은 모델의 내용이다 — 다시 증명하게 하는 것은 프로젝트의
        // 챕터를 고치는 일이다.
        (ChapterGraphView view, AuthoringSession session, Window window) = Show();

        int before = view.ValidationComputeCount;

        session.Editor.AddEpisode("ch01", "새에피소드", title: "새것", 9, 9);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(
            view.ValidationComputeCount > before,
            "모델이 바뀌었으면 다시 증명해야 한다");

        window.Close();
    });

    [Fact]
    public void 같은_대사를_다시_반영하면_다시_그리라고_방송하지_않는다() => HeadlessUi.Run(() =>
    {
        // ⛔ 이 방송이 열려 있는 편집 화면을 통째로 다시 만든다 — 그 순간 사람이 타이핑
        // 하던 칸이 파괴된다. 바꿀 것이 없는 반영은 손대지 않아야 한다
        // (ProjectEditor.ApplyScriptSync의 "바꿀 것이 없으면 손대지 않는다").
        //
        // (2026-10-29 · D2: 예전에는 엑셀 대본 동기화가 감시자가 깰 때마다 이 길을 지났다.
        // 동기화는 걷혔고, 같은 길을 카드 안 글 칸의 넣기가 지난다.)
        (ChapterGraphView view, AuthoringSession session, Window window) = Show();

        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", "ep0")!;
        const string text = "라루: 이미 쓰여 있던 대사";

        // ⚠ <b>대본에 글이 있어야 이 자리에 닿는다.</b> 빈 대본에 처음 넣는 한 번은 진짜로
        // 바뀌므로 방송이 맞다 — 그 뒤부터가 고정점이다. 빈 판으로 재면 아무것도 안 재는 것이다.
        session.Editor.ApplyScenarioText(card.Id, text, session.Definition, confirmDeletes: true);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        int broadcasts = 0;
        session.Changed += (_, _) => broadcasts++;

        // 이제 대본도 글도 그대로인 채 다시 반영한다.
        for (int index = 0; index < 3; index++)
        {
            session.Editor.ApplyScenarioText(card.Id, text, session.Definition, confirmDeletes: true);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(0, broadcasts);

        window.Close();
    });

    [Fact]
    public void 대사가_실제로_바뀌면_다시_그리라고_방송한다() => HeadlessUi.Run(() =>
    {
        // 눈금이 반대로 굳으면 "고쳤는데 툴이 그대로"가 된다 — 위 최적화가 삼켜서는 안 되는
        // 것이 이것이다.
        (ChapterGraphView view, AuthoringSession session, Window window) = Show();

        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", "ep0")!;
        session.Editor.ApplyScenarioText(card.Id, "라루: 이미 쓰여 있던 대사", session.Definition, confirmDeletes: true);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        int broadcasts = 0;
        session.Changed += (_, _) => broadcasts++;

        session.Editor.ApplyScenarioText(card.Id, "라루: 새로 쓴 대사", session.Definition, confirmDeletes: true);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(broadcasts > 0, "대사가 바뀌었으면 열려 있는 화면이 따라와야 한다");

        window.Close();
    });

    [Fact]
    public void 손을_뗀_뷰는_더_이상_일하지_않는다() => HeadlessUi.Run(() =>
    {
        // 흔들리던 `ChapterGraphEditingTests`의 뿌리 (2026-08-18). 어셈블리 하나가
        // <b>디스패처 하나</b>를 나눠 쓰는데, 창을 안 닫고 끝낸 테스트의 뷰가 그대로 살아
        // 있었다. 그 뷰의 감시자는 이미 지워진 임시 폴더를 보고 있고, 거기서 난 사건이
        // <b>다음 테스트의</b> RunJobs()에서 깨어나 없는 프로젝트를 다시 읽었다.
        // 그래서 매번 다른 테스트가 깨졌고, 혼자 돌리면 통과했다.
        (ChapterGraphView view, AuthoringSession session, Window window) = Show();

        view.DetachSession();

        int drawsAfterDetach = view.CanvasDrawCount;
        int reloadsAfterDetach = 0;
        view.EntriesReloaded += _ => reloadsAfterDetach++;

        // 손을 뗐으니 세션이 무슨 말을 해도 이 뷰는 듣지 않는다.
        for (int index = 0; index < 10; index++)
        {
            session.Editor.AddStoryFile($"판{index}");
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(drawsAfterDetach, view.CanvasDrawCount);
        Assert.Equal(0, reloadsAfterDetach);

        window.Close();
    });

    [Fact]
    public void 두_번_붙였어도_한_번_떼면_완전히_떨어진다() => HeadlessUi.Run(() =>
    {
        // 붙기만 하고 뗄 줄 모르던 뷰라, 두 번 붙으면 구독이 두 벌 쌓였다. 한 벌만
        // 떼어지면 <b>뗐다고 믿는 뷰가 계속 듣는다</b> — 이 테스트가 막는 것이 그것이다.
        //
        // ⚠ "재읽기가 한 번만 돈다"로는 이걸 못 잡는다. QueueReload가 한 차례에 하나로
        // 합치기 때문에 구독이 둘이어도 재읽기는 하나다 — 합치기가 이중 구독을 가려 준다.
        // 그래서 <b>뗀 뒤에</b> 본다.
        (ChapterGraphView view, AuthoringSession session, Window window) = Show();

        view.Attach(session);
        view.DetachSession();

        int reloads = 0;
        view.EntriesReloaded += _ => reloads++;

        session.Editor.AddStoryFile("판");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, reloads);

        window.Close();
    });

    private (ChapterGraphView View, AuthoringSession Session, Window Window) Show()
    {
        var session = new AuthoringSession();
        session.Open(ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        view.Attach(session);

        window.Measure(new Size(900, 600));
        window.Arrange(new Rect(0, 0, 900, 600));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        _ui.Own(view, window);

        return (view, session, window);
    }
}
