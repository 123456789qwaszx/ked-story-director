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
/// 시나리오 그래프의 철도 배선. 챕터 간선이 챕터 판에 미러되고,
/// 선택지 칸은 문구만 보인다(수치는 챕터 그래프의 책임). 선택은 항상 에피소드 끝이므로
/// 분기점은 출발 카드당 하나다.
///
/// 챕터는 프로젝트에 둔다 — 연출 그래프는 살아 있는 프로젝트의 챕터만 본다.
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

        // 같은 출발의 칸들은 한 가로 줄에 나란히 매달린다(2026-11-26 — ⛔ 그 전에는 줄기에 세로로 쌓여 왼쪽이 같았다) — 높이가 같고 옆으로 차례대로다.
        List<StackPanel> chips = Rows(canvas, "EP00").ToList();
        Assert.Single(chips.Select(chip => Math.Round(Canvas.GetTop(chip) + (chip.DesiredSize.Height / 2), 1)).Distinct());
        Assert.Equal(chips.Count, chips.Select(Canvas.GetLeft).Distinct().Count());

        // 도착 포트 — 들어오는 가지가 있는 카드(EP01·EP02)의 위 가운데에 접점이 서고,
        // 가지들은 카드에 직접 붙지 않고 그 접점으로 모인다.
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
    public void 카드가_없는_도착은_스텁으로_보인다() => HeadlessUi.Run(() =>
    {
        // 없는 노드로 선을 그으면 거짓말이지만, 가지를 통째로 숨기면 선택지가 사라져
        // 고장으로 보인다. 가지는 "카드 없음" 스텁에서 멈춘다.
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
    public void 에피소드_카드의_선택지는_칸으로_서고_문구가_빈_선택지도_간선_순서_그대로다() => HeadlessUi.Run(() =>
    {
        // 에피소드 카드의 선택지는 카드 아래 칸에 선다. 칸의 주인은 챕터 간선이다.
        (GraphEditorView graph, AuthoringSession session, string fileId) = ShowBoard("ch01");

        DialogueNode ep00 = AddEpisodeCard(session, fileId, "EP00");
        ScriptDocument script = ep00.Script;
        session.Editor.SetScriptLineText(ep00.Id, script.ActiveLines.First().Id, "윌로", "첫 줄");
        AddEpisodeCard(session, fileId, "EP01").Layout.X = 1200;

        // 문구 없는 간선을 먼저 둔다 — 화면도 그 차례다(= 런타임 OptionIndex · 2026-11-26 소유자 — 「선택지의 순서를 뒤섞지 말고 유지」). ⛔ 그 전에는 문구
        // 없는 길을 맨 아래로 내렸다(「진행이 선택지 위에 서니 헷갈린다」 — 자동 길이 선택지와 함께 서던 때). 문구 없는 길은 이제 문구를 적을 선택지다.
        Chapter(session, "ch01",
            episodes: ["EP00", "EP01"],
            edges:
            [
                ("EP00", "EP01", null, null),
                ("EP00", "EP01", "라루를 믿는다", "trust +1"),
                ("EP00", "EP01", "의심한다", null)
            ]);
        graph.Rebuild();

        // 칸의 차례 — 간선 순서 그대로다.
        var canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        List<string> order = Rows(canvas, "EP00")
            .Take(3)
            .Select(row => row.Children.OfType<TextBlock>().SingleOrDefault()?.Text ??
                           (row.Children.OfType<Border>().Single().Child as TextBlock)!.Text ?? string.Empty)
            .ToList();

        // 문구 없는 길(자동이 아닌 이은 길) — 빨갛게 「문구를 적어 주세요」다(2026-11-25 auto-to-choice A2 · ⛔ 2026-11-18부터는 회색 「빈 선택지」).
        Assert.Equal(GraphEditorView.SlotNeedsLabelText, order[0]);
        Assert.Equal("라루를 믿는다", order[1]);
        Assert.Equal("의심한다", order[2]);
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
        DialogueNode node = session.Editor.AddDialogueNode(fileId, episodeId, name: episodeId);
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
                id, id, "01", index * 100, 0, null)));

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

        session.Editor.EnsureChapter(name);   // 판은 챕터가 세운다
        string fileId = session.EnsureChapterBoard(name);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (graph, session, fileId);
    }
}
