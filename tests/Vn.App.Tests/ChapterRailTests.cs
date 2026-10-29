using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// T1 — 시나리오 그래프의 철도 배선 (소유자 그림). 챕터 간선이 챕터 판에 미러되고,
/// 선택지 칸은 문구만 보인다(T-D4 — 수치는 챕터 그래프의 책임). 선택은 항상 에피소드 끝이므로
/// 분기점은 출발 카드당 하나다.
///
/// 챕터는 프로젝트에 둔다 — 연출 그래프는 살아 있는 프로젝트의 챕터만 본다. ⛔ 전에는 셸이 공급한 챕터
/// 목록을 가짜로 넣어 시험했고, 그 길(스냅샷 대체)과 그 길에서만 서던 옛 칩은 2026-10-29에 걷혔다
/// (legacy-cleanup.md L3).
/// </summary>
public sealed class ChapterRailTests
{
    [Fact]
    public void 챕터_간선이_철도_배선으로_미러되고_칸은_문구만_보인다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string fileId) = ShowBoard("ch01");

        // 에피소드 카드 셋 — 챕터의 에피소드와 이름·표식이 짝이다.
        AddEpisodeCard(session, fileId, "EP00");
        AddEpisodeCard(session, fileId, "EP01");
        AddEpisodeCard(session, fileId, "EP02");

        Chapter(session, "ch01",
            episodes: ["EP00", "EP01", "EP02"],
            edges:
            [
                ("EP00", "EP01", "라루를 믿는다", "trust +1"),
                ("EP00", "EP02", "의심한다", "suspicion +1")
            ]);

        graph.Rebuild();

        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;

        // 철도 선이 섰다 — 줄기·가지·진입 (간선 2개면 선이 여럿이다).
        List<Line> rails = canvas.Children.OfType<Line>().ToList();
        Assert.True(rails.Count >= 4, $"철도 선이 {rails.Count}개뿐입니다");

        // 칸 — 문구만. 스탯변화·조건 문자열은 기본 화면에 없다(T-D4).
        List<string> labels = Rows(canvas, "EP00")
            .SelectMany(row => row.Children.OfType<TextBlock>())
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains("라루를 믿는다", labels);
        Assert.Contains("의심한다", labels);
        Assert.DoesNotContain(labels, label => label.Contains("trust") || label.Contains("suspicion"));

        // 같은 출발의 가지들은 같은 분기점 X(줄기)를 공유한다 — 단일 분기점.
        Assert.Single(Rows(canvas, "EP00").Select(Canvas.GetLeft).Distinct());

        // 도착 포트 — 들어오는 가지가 있는 카드(EP01·EP02)의 위 가운데에 접점이 서고(D3),
        // 가지들은 카드에 직접 붙지 않고 그 접점으로 모인다 (소유자 보고로 고정).
        List<Ellipse> ports = canvas.Children.OfType<Ellipse>()
            .Where(port => port.Width == 10)
            .ToList();
        Assert.Equal(2, ports.Count);

        foreach (Ellipse port in ports)
        {
            double junctionX = Canvas.GetLeft(port) + 5;
            double junctionY = Canvas.GetTop(port) + 5;
            Assert.Contains(canvas.Children.OfType<Line>(), line =>
                Math.Abs(line.EndPoint.X - junctionX) < 0.5 &&
                (Math.Abs(line.EndPoint.Y - junctionY) < 0.5 || Math.Abs(line.StartPoint.X - junctionX) < 0.5));
        }
    });

    [Fact]
    public void 챕터가_아닌_판에는_철도가_생기지_않는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string fileId) = ShowBoard("일반판");

        session.Editor.AddDialogueNode(fileId, name: "자유A");
        session.Editor.AddDialogueNode(fileId, name: "자유B");
        graph.Rebuild();

        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Assert.Empty(canvas.Children.OfType<Line>());
        Assert.DoesNotContain(canvas.Children.OfType<StackPanel>(), row => row.Tag is string);
    });

    [Fact]
    public void 카드가_없는_도착은_스텁으로_보인다() => HeadlessUi.Run(() =>
    {
        // 없는 노드로 선을 그으면 거짓말이지만, 가지를 통째로 숨기면 선택지가 사라져
        // 고장으로 보인다(소유자 보고 2026-08-15). 가지는 "카드 없음" 스텁에서 멈춘다.
        (GraphEditorView graph, AuthoringSession session, string fileId) = ShowBoard("ch01");

        AddEpisodeCard(session, fileId, "EP00"); // EP01은 아직 없다

        Chapter(session, "ch01",
            episodes: ["EP00", "EP01"],
            edges: [("EP00", "EP01", "라루를 믿는다", null)]);
        graph.Rebuild();

        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Assert.Contains(canvas.Children.OfType<TextBlock>(), block =>
            (block.Text ?? "").Contains("EP01", StringComparison.Ordinal) &&
            (block.Text ?? "").Contains("카드 없음", StringComparison.Ordinal));
    });

    [Fact]
    public void 챕터_밖의_카드는_한눈에_갈린다() => HeadlessUi.Run(() =>
    {
        // 소유자 보고 (2026-08-15) — "대본노드와 엑셀노드가 똑같이 생겨서 구분이 어렵다."
        // 종별 시각 언어: 에피소드 = 각진 미색 서류(📄), 그 밖 = 둥근 흰 원고(✎).
        //
        // ⚠ 뜻이 좁아졌다 (R7 P-6 · 결정 ⑤ · 2026-09-17). 전에는 <b>엑셀노드/자유 씬</b>을
        //    갈랐는데 챕터 판의 카드가 전부 에피소드가 됐다 — 그래서 남는 구분은
        //    <b>챕터에 실린 카드인가</b> 하나뿐이고, 판 위 카드끼리는 이제 다 같아 보인다
        //    (소유자: 조건 노드 카드도 "종류가 하나뿐이니 가를 것이 없다").
        (GraphEditorView graph, AuthoringSession session, string fileId) = ShowBoard("ch01");

        DialogueNode episode = AddEpisodeCard(session, fileId, "EP00");
        DialogueNode free = session.Editor.AddDialogueNode(fileId, name: "자유씬");
        graph.Rebuild();

        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Border episodeCard = canvas.Children.OfType<Border>()
            .Single(border => Equals(border.Tag, episode.Id));
        Border freeCard = canvas.Children.OfType<Border>()
            .Single(border => Equals(border.Tag, free.Id));

        Assert.NotEqual(freeCard.CornerRadius, episodeCard.CornerRadius);
        Assert.NotEqual(
            ((Avalonia.Media.SolidColorBrush)freeCard.Background!).Color,
            ((Avalonia.Media.SolidColorBrush)episodeCard.Background!).Color);
    });

    [Fact]
    public void 챕터에서_지운_에피소드_노드에는_레일이_없다() => HeadlessUi.Run(() =>
    {
        // 챕터 밖의 노드에 진행·종료를 그리는 것은 거짓말이다 — 동기화 보고가 알릴 일이다.
        (GraphEditorView graph, AuthoringSession session, string fileId) = ShowBoard("ch01");

        AddEpisodeCard(session, fileId, "EP99"); // 챕터에는 없는 에피소드

        Chapter(session, "ch01", episodes: ["EP00"], edges: []);
        graph.Rebuild();

        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Assert.Empty(canvas.Children.OfType<Line>());
        Assert.Empty(Rows(canvas, "EP99"));
    });

    [Fact]
    public void 에피소드_카드의_선택지는_칸으로_서고_진행은_맨_아래다() => HeadlessUi.Run(() =>
    {
        // T2 — 에피소드 카드의 선택지 · 기본 출구 포트는 카드 아래 칸으로 이사했다. 칸의 주인은 챕터 간선이다.
        (GraphEditorView graph, AuthoringSession session, string fileId) = ShowBoard("ch01");

        DialogueNode ep00 = AddEpisodeCard(session, fileId, "EP00");
        ScriptDocument script = session.Project.FindScript(ep00.ScriptId)!;
        session.Editor.SetScriptLineText(script.Id, script.ActiveLines.First().Id, "윌로", "첫 줄");
        AddEpisodeCard(session, fileId, "EP01").Layout.X = 1200;

        // 진행 간선을 일부러 먼저 둔다 — 그래도 화면은 선택지가 위여야 한다(소유자 보고: 반대면 헷갈린다).
        Chapter(session, "ch01",
            episodes: ["EP00", "EP01"],
            edges:
            [
                ("EP00", "EP01", null, null),
                ("EP00", "EP01", "라루를 믿는다", "trust +1"),
                ("EP00", "EP01", "의심한다", null)
            ]);
        graph.Rebuild();

        // ① 에피소드 카드에는 기본 출구 · 선택지 포트가 없다 — 칸으로 이사했다(IF 갈래만 남는다).
        ExpandedNodeProjection projected = GraphProjectionBuilder
            .Build(session.Project, session.Project.Files.Select(file => file.Id)
                .ToHashSet(StringComparer.Ordinal))
            .Items.OfType<ExpandedNodeProjection>()
            .Single(item => item.NodeId == ep00.Id);

        Assert.DoesNotContain(projected.OutputPorts,
            port => port.Kind == GraphOutputPortKind.ExecutionDefault);
        Assert.DoesNotContain(projected.OutputPorts, port => port.ExecutionPort is { IsChoice: true });

        // ② 칸의 차례 — 선택지(간선 순서)가 위, 문구 없는 진행이 그 아래다.
        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        List<string> order = Rows(canvas, "EP00")
            .Take(3)
            .Select(row => row.Children.OfType<TextBlock>().Single().Text ?? string.Empty)
            .ToList();

        Assert.Equal("라루를 믿는다", order[0]);
        Assert.Equal("의심한다", order[1]);
        Assert.DoesNotContain(order[2], new[] { "라루를 믿는다", "의심한다" });
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>그 에피소드의 선택지 칸 줄들 — Tag가 <c>{에피소드}#{칸}</c>이고, 칸 번호 차례다.</summary>
    private static List<StackPanel> Rows(Canvas canvas, string episodeId) =>
        canvas.Children.OfType<StackPanel>()
            .Where(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true)
            .OrderBy(row => (row.Tag as string)![(episodeId.Length + 1)..], StringComparer.Ordinal)
            .ToList();

    private static DialogueNode AddEpisodeCard(AuthoringSession session, string fileId, string episodeId)
    {
        DialogueNode node = session.Editor.AddDialogueNode(fileId, name: episodeId);
        node.MarkedEpisodeId = episodeId;
        return node;
    }

    /// <summary>그 챕터에 에피소드와 간선을 바로 적는다 — 카드는 세우지 않는다(카드 없음 · 챕터 밖 카드를 꾸미려고).</summary>
    private static void Chapter(
        AuthoringSession session,
        string chapterId,
        IReadOnlyList<string> episodes,
        IReadOnlyList<(string From, string To, string? Label, string? Stats)> edges)
    {
        ChapterDocument chapter = session.Editor.EnsureChapter(chapterId);

        chapter.Episodes.AddRange(episodes
            .Select((id, index) => new ChapterEpisode(
                id, id, "01", id, index * 100, 0, null)));

        chapter.Edges.AddRange(edges
            .Select((edge, index) => new ChapterEdge(
                edge.From, edge.To, edge.Label, null, null)
            {
                StatChanges = edge.Stats is null
                    ? []
                    : [new StatDelta(edge.Stats.Split(' ')[0], int.Parse(edge.Stats.Split('+')[1]))]
            }));
    }

    private static (GraphEditorView Graph, AuthoringSession Session, string FileId) ShowBoard(string name)
    {
        var session = new AuthoringSession();
        string directory = Path.Combine(
            Path.GetTempPath(), "vn-rail", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string manifest = Path.Combine(directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "철도 검증" });
        session.Open(manifest);

        string fileId = session.EnsureChapterBoard(name);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (graph, session, fileId);
    }
}
