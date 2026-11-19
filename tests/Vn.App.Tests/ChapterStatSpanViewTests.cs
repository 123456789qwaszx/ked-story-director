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
/// 노드 카드의 스탯 줄 — 간선을 따라 왔을 때의 스탯 변화량을 에피소드 노드에 보이고,
/// 여러 루트가 있으면 최소·최대를 적는다.
///
/// 증명이 낸 폭이 정말 카드 글자가 되는지를 본다 — 프루버 단위 테스트만으로는 화면에
/// 안 붙는 배선을 못 잡는다.
/// </summary>
public sealed class ChapterStatSpanViewTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-stat-span", Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);

    /// <summary>판을 짓는 손. 화면을 띄울 때(<see cref="Render"/>) 그때 모습 그대로 저장한다.</summary>
    private readonly ProjectEditor _editor = new(new StoryProject { Title = "스탯 폭 검증" });

    public ChapterStatSpanViewTests()
    {
        ChapterDocument chapter = _editor.EnsureChapter("ch01");
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        chapter.Stats.Add(new ChapterStat("anger", 0, 0, 10));

        // ep1 ─(+1)─ 싼길 ─┐
        //   └─(+3)─ 비싼길 ─┴─ 합류      (분노는 챕터 어디에서도 움직이지 않는다)
        _editor.AddEpisode("ch01", "ep1", title: "", 0, 0, sceneId: "ep1");
        _editor.AddEpisode("ch01", "싼길", title: "", 1, 0, sceneId: "싼길");
        _editor.AddEpisode("ch01", "비싼길", title: "", 1, 1, sceneId: "비싼길");
        _editor.AddEpisode("ch01", "합류", title: "", 2, 0, sceneId: "합류");

        _editor.AddEdge("ch01", "ep1", "싼길", statChanges: "trust +1");
        _editor.AddEdge("ch01", "ep1", "비싼길", statChanges: "trust +3");
        _editor.AddEdge("ch01", "싼길", "합류");
        _editor.AddEdge("ch01", "비싼길", "합류");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 한_길로만_오는_노드는_값_하나를_단다() => HeadlessUi.Run(() =>
    {
        Canvas canvas = Render();

        Assert.Equal("trust 1", StatLine(canvas, "싼길"));
        Assert.Equal("trust 3", StatLine(canvas, "비싼길"));
    });

    [Fact]
    public void 여러_길이_들어오는_노드는_최소_최대로_벌어진다() => HeadlessUi.Run(() =>
    {
        Canvas canvas = Render();

        Assert.Equal("trust 1~3", StatLine(canvas, "합류"));
    });

    [Fact]
    public void 챕터_어디에서도_안_움직이는_스탯은_카드에_안_뜬다() => HeadlessUi.Run(() =>
    {
        // 분노는 어느 간선도 건드리지 않는다 — 모든 카드에 `분노 0`이 붙으면 정작
        // 움직이는 신뢰가 그 줄에 묻힌다.
        Canvas canvas = Render();

        Assert.DoesNotContain("anger", StatLine(canvas, "합류"));
        Assert.DoesNotContain("anger", StatLine(canvas, "ep1"));
    });

    [Fact]
    public void 시작_노드는_초기값을_그대로_단다() => HeadlessUi.Run(() =>
    {
        Canvas canvas = Render();

        Assert.Equal("trust 0", StatLine(canvas, "ep1"));
    });

    [Fact]
    public void 증감이_하나도_없는_챕터는_거르지_않고_초기값을_보여_준다() => HeadlessUi.Run(() =>
    {
        // 증감을 아직 안 적은 판에서 카드가 비어 있으면 기능이
        // 없는 것처럼 읽힌다. 거를 것이 없을 때는 거르지 않는다.
        _editor.UpdateEdge("ch01", "ep1", "싼길", statChanges: string.Empty);
        _editor.UpdateEdge("ch01", "ep1", "비싼길", statChanges: string.Empty);

        Canvas canvas = Render();

        Assert.Equal("trust 0 · anger 0", StatLine(canvas, "합류"));
    });

    [Fact]
    public void 스탯은_하나에_한_줄씩_아래로_펼치고_카드가_그만큼_자라며_선택지_점이_따라간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「여러개의 스탯이 있을 경우 우측으로 ... 형태로 잘려서 표기 자체가 안 되는 문제」 · 「스탯 당 1줄씩만 차지하게 하며,
        // 아래로 펼쳐지게」(usability UX3). ⛔ 그 전에는 「키 값 · 키 값」 한 줄이 카드 폭에서 잘렸다.
        _editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("kasimira_trust_with_a_long_key", 0, 0, 10));
        _editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("belle", 0, 0, 10));
        _editor.UpdateEdge("ch01", "ep1", "싼길", statChanges: "trust +1; anger +2; kasimira_trust_with_a_long_key +1; belle +1");
        _editor.AddEpisode("ch01", "끝", title: "", 3, 0, sceneId: "끝");
        _editor.AddEdge("ch01", "합류", "끝", optionLabel: "마친다");   // 합류에서 나가는 선택지 — 점이 카드 아래변에 선다

        Canvas canvas = Render();
        Border card = canvas.Children.OfType<Border>().Single(border => border.Tag as string == "싼길");

        var rows = ChapterCardStats.Rows(card);
        Assert.Equal(["trust", "anger", "kasimira_trust_with_a_long_key", "belle"], rows.Select(row => row.Key));
        Assert.Equal(["1", "2", "1", "1"], rows.Select(row => row.Value));

        // 줄마다 따로 선다 — 위아래로 겹치지 않고 한 줄 높이씩 내려간다.
        double[] tops = rows.Select(row => Avalonia.VisualExtensions.TranslatePoint(row.KeyBlock, default, card)!.Value.Y).ToArray();
        Assert.All(tops.Zip(tops.Skip(1)), pair => Assert.Equal(ChapterGraphView.StatRowHeight, pair.Second - pair.First, 1));

        // 긴 키는 키만 줄고 값은 늘 카드 안에 다 보인다.
        foreach (var row in rows)
        {
            double right = Avalonia.VisualExtensions.TranslatePoint(row.ValueBlock, new Avalonia.Point(row.ValueBlock.Bounds.Width, 0), card)!.Value.X;
            Assert.True(right <= card.Bounds.Width, $"값 「{row.Value}」의 오른쪽 {right} · 카드 {card.Bounds.Width}");
            Assert.True(row.ValueBlock.Bounds.Width >= row.ValueBlock.DesiredSize.Width - row.ValueBlock.Margin.Left - 0.5,
                $"값 「{row.Value}」이 줄었다 — 폭 {row.ValueBlock.Bounds.Width} · 바라는 폭 {row.ValueBlock.DesiredSize.Width} · 키 {row.KeyBlock.Bounds.Width}");
        }

        // 카드는 줄 수만큼 자라고(스탯이 하나 이하인 카드는 74 그대로) 마지막 줄까지 카드 안이다.
        Assert.Equal(ChapterGraphView.CardHeightFor(4), card.Height);
        Assert.True(card.Height > 74);
        double lastBottom = Avalonia.VisualExtensions.TranslatePoint(rows[^1].KeyBlock, default, card)!.Value.Y + ChapterGraphView.StatRowHeight;
        Assert.True(lastBottom <= card.Height, $"마지막 줄 아래 {lastBottom} · 카드 {card.Height}");
        Assert.Equal(74, ChapterGraphView.CardHeightFor(0));   // 스탯이 하나 이하면 지금 높이 그대로
        Assert.Equal(74, ChapterGraphView.CardHeightFor(1));
        Assert.Equal(ChapterGraphView.StatRowHeight, ChapterGraphView.CardHeightFor(5) - ChapterGraphView.CardHeightFor(4));   // 그 뒤로는 한 줄씩

        // 선택지 점은 자란 카드의 아래변에 선다.
        Border join = canvas.Children.OfType<Border>().Single(border => border.Tag as string == "합류");
        Assert.True(join.Height > 74);
        double bottom = Canvas.GetTop(join) + join.Height;
        Assert.Contains(canvas.Children.OfType<Avalonia.Controls.Shapes.Ellipse>(),
            dot => Math.Abs(Canvas.GetTop(dot) + (dot.Height / 2) - bottom) < 0.6);
    });

    [Fact]
    public void 이름은_14로_스탯은_11로_진하게_선다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「에피소드 제목이랑 이런 스탯 표기가 엄청 중요한 정보인데도 가독성이 떨어지는」(UX3). ⛔ 이름 12 · 스탯 9 · 흐리게 75%.
        Canvas canvas = Render();
        Border card = canvas.Children.OfType<Border>().Single(border => border.Tag as string == "싼길");

        TextBlock name = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(card).OfType<TextBlock>()
            .Single(text => text.Tag as string == ChapterGraphView.CardNameTag);
        Assert.Equal((14, Avalonia.Media.FontWeight.Bold), (name.FontSize, name.FontWeight));

        var row = Assert.Single(ChapterCardStats.Rows(card));
        Assert.Equal((11, 11), (row.KeyBlock.FontSize, row.ValueBlock.FontSize));
        Assert.Equal(Avalonia.Media.FontWeight.Bold, row.ValueBlock.FontWeight);
        Assert.Equal(1, row.KeyBlock.Opacity);
        Assert.Equal(1, ((Control)row.KeyBlock.Parent!.Parent!).Opacity);
    });

    [Fact]
    public void 활성_비활성_스탯은_카드와_선_위에서_활성_비활성으로_읽는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「모두 활성 / 비활성으로 맞춰」(usability UX12). ⛔ 카드는 「참/거짓/참·거짓」, 선 위는 「→ 예/아니오」였다.
        _editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("met", 0, 0, 1, ChapterStatType.Bool));
        _editor.UpdateEdge("ch01", "ep1", "싼길", statChanges: "trust +1; met true");

        Canvas canvas = Render();

        Assert.Contains("met 활성", StatLine(canvas, "싼길"));
        Assert.Contains("met 비활성", StatLine(canvas, "비싼길"));
        Assert.Contains("met 활성·비활성", StatLine(canvas, "합류"));

        List<ChapterStat> stats = _editor.FindChapter("ch01")!.Stats;
        Assert.Equal("met → 활성", ChapterGraphView.DeltaText(stats, new StatDelta("met", 1, StatChangeKind.Set)));
        Assert.Equal("met → 비활성", ChapterGraphView.DeltaText(stats, new StatDelta("met", 0, StatChangeKind.Set)));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>그 카드에 붙은 스탯 줄 — 없으면 빈 글자.</summary>
    private static string StatLine(Canvas canvas, string episodeId)
    {
        Border card = canvas.Children.OfType<Border>()
            .Single(border => border.Tag as string == episodeId);

        return ChapterCardStats.Of(card);
    }

    private Canvas Render()
    {
        Directory.CreateDirectory(_directory);
        ProjectStore.Save(ManifestPath, _editor.Project);

        var session = new AuthoringSession();
        session.Open(ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        view.Attach(session);

        window.Measure(new Avalonia.Size(1280, 800));
        window.Arrange(new Avalonia.Rect(0, 0, 1280, 800));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return view.FindControl<Canvas>("GraphCanvas")!;
    }
}
