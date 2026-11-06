using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>[챕터] 탭의 스탯·조건 표가 편집 가능하다</b> — 조건과 스탯은 엑셀이 아니라
/// 챕터 그래프에서 직접 정의한다. 값의 주인은 <c>ChapterDocument</c>다.
/// </summary>
public sealed class ChapterSheetEditTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-sheet-edit", Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

    public ChapterSheetEditTests()
    {
        Directory.CreateDirectory(_directory);

        // [＋ 챕터](AuthoringSession.CreateChapter)와 같은 모양 — 챕터 · 스탯 `trust` · 판.
        var editor = new ProjectEditor(new StoryProject { Title = "챕터 시트 편집" });
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 100));
        editor.EnsureChapterBoard("ch01");

        ProjectStore.Save(ManifestPath, editor.Project);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 스탯_표에_값_칸이_선다() => HeadlessUi.Run(() =>
    {
        // 한 줄짜리 글자가 아니라 고칠 수 있는 칸이어야 한다.
        (ChapterGraphView view, _) = Show();

        // 표시 이름 · 처음 값 · 최소 · 최대 — 키는 라벨(개명은 [✎]), 종류는 콤보다.
        Assert.Equal(4, Boxes(Row(view, "stat:trust")).Count);
    });

    [Fact]
    public void 표시이름을_고치면_모델에_써진다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();

        Commit(Boxes(Row(view, "stat:trust"))[0], "신뢰도");

        Assert.Equal(
            "신뢰도",
            session.Editor.FindChapter("ch01")!.Stats.Single(stat => stat.Key == "trust").DisplayName);
    });

    [Fact]
    public void 숫자_칸에_글자를_적으면_조용히_안_버리고_말한다() => HeadlessUi.Run(() =>
    {
        // 조용히 버리면 사람은 적은 줄 알고 넘어간다.
        (ChapterGraphView view, AuthoringSession session) = Show();

        Commit(Boxes(Row(view, "stat:trust"))[1], "많이");

        Assert.Contains("정수", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(
            0,
            session.Editor.FindChapter("ch01")!.Stats.Single(stat => stat.Key == "trust").Initial);
    });

    [Fact]
    public void 조건_식을_고치면_모델에_써진다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();

        Commit(Boxes(Row(view, "condition:신뢰높음"))[0], "trust >= 5");

        Assert.Equal(
            "trust >= 5",
            session.Editor.FindChapter("ch01")!.Conditions
                .Single(item => item.Label == "신뢰높음").Expression);
    });

    [Fact]
    public void 쓰는_곳이_있는_스탯을_지우려_하면_어디인지_말한다() => HeadlessUi.Run(() =>
    {
        // *"쓰이고 있습니다"*로 끝내면 사람이 챕터를 뒤져야 한다 — 거절이 곧 정리 안내다.
        (ChapterGraphView view, AuthoringSession session) = Show();

        Press(Buttons(Row(view, "stat:trust")).Last());

        // 한 번에 안 지우고 <b>보여 주고 묻는다</b> — 무엇이 바뀔지 보고 나서 누르는 것과
        // 경고만 읽고 누르는 것은 다른 일이다.
        Assert.Contains(session.Editor.FindChapter("ch01")!.Stats, stat => stat.Key == "trust");

        List<string> shown = Confirm(view).GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty).ToList();

        Assert.Contains(shown, text => text.Contains("1곳", StringComparison.Ordinal));
        Assert.Contains(shown, text => text.Contains("신뢰높음", StringComparison.Ordinal));
    });

    [Fact]
    public void 한_번_더_누르면_비우고_지운다() => HeadlessUi.Run(() =>
    {
        // ⭐ 다 — 간선은 그 항만 빠지고, 조건은 <b>식만 비고 남는다</b>.
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddEpisode("ch01", "root", title: "root", 0, 0);
        session.Editor.AddEpisode("ch01", "a", title: "a", 0, 0);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다", statChanges: "trust +1");
        Redraw(view);

        Press(Buttons(Row(view, "stat:trust")).Last());
        Press(Confirm(view).GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string) == "모두 비우고 지우기"));

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;

        Assert.DoesNotContain(chapter.Stats, stat => stat.Key == "trust");
        Assert.Empty(chapter.Edges.Single().StatChanges);

        // 조건은 <b>남는다</b> — 통째로 지우면 그것을 쓰는 간선의 표시/해금이 함께 풀린다.
        Assert.Equal(string.Empty, chapter.Conditions.Single().Expression);
    });

    [Fact]
    public void 아무_데서도_안_쓰는_스탯은_지워진다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddChapterStat("ch01", "여분");
        Redraw(view);

        Press(Buttons(Row(view, "stat:여분")).Last());

        Assert.DoesNotContain(session.Editor.FindChapter("ch01")!.Stats, stat => stat.Key == "여분");
    });

    [Fact]
    public void 키_바꾸기는_세이브가_안_따라온다고_먼저_말한다() => HeadlessUi.Run(() =>
    {
        // <b>키는 세이브의 열쇠다</b> (런타임 회신 §6.2). 저쪽 `LocalSaveFile.Stats`가 이
        //    글자로 저장하고, 복원은 키가 없으면 <b>처음 값</b>으로 떨어진다 — 경고도 없다.
        //    개명이 끌고 가는 다섯 자리는 프로젝트 안이고 <b>디스크의 세이브는 그 다섯에
        //    없다</b>. 표시 이름만 고치려던 사람이 이 문을 열 수 있으므로, 경고가
        //    <b>적기 전에</b> 서야 한다.
        (ChapterGraphView view, _) = Show();

        Press(Buttons(Row(view, "stat:trust")).First());

        List<string> shown = Prompt(view).GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty).ToList();

        Assert.Contains(shown, text => text.Contains("세이브의 열쇠", StringComparison.Ordinal));
        Assert.Contains(shown, text => text.Contains("처음 값", StringComparison.Ordinal));
        Assert.Contains(shown, text => text.Contains("[표시 이름]", StringComparison.Ordinal));
    });

    [Fact]
    public void 값_칸마다_이름이_붙고_종류는_작가의_말이며_줄이_인스펙터_폭_안에_선다() => HeadlessUi.Run(() =>
    {
        // 처음 보는 사람이 숫자 칸 셋이 무엇인지 알아야 한다 — 칸마다 「처음 값 · 최소 · 최대」가 붙는다.
        (ChapterGraphView view, _) = Show();
        Panel row = Row(view, "stat:trust");

        List<string> labels = row.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();
        Assert.Contains("처음 값", labels);
        Assert.Contains("최소", labels);
        Assert.Contains("최대", labels);

        ComboBox type = TypeCombo(row);
        Assert.Equal(["숫자", "예/아니오"], type.ItemsSource!.Cast<string>());
        Assert.Equal("숫자", type.SelectedItem);

        // 잘리지 않는다 — 종류 칸이 제 내용만큼 넓고, 두 줄의 끝([✕] · [최대] 칸)이 표 폭 안에 선다(인스펙터 기본 폭).
        double wanted = type.DesiredSize.Width - type.Margin.Left - type.Margin.Right;
        Assert.True(type.Bounds.Width >= wanted - 0.5, $"종류 칸이 눌렸다: {type.Bounds.Width} < {wanted}");
        StackPanel table = view.FindControl<StackPanel>("StatListPanel")!;
        AssertInside(Buttons(row).Last(), table, "[✕]");

        TextBox maximum = Boxes(row).Last();
        AssertInside(maximum, table, "[최대] 칸");
        Assert.True(maximum.Bounds.Width >= 30, $"[최대] 칸이 너무 좁다: {maximum.Bounds.Width}");
    });

    [Fact]
    public void 조건_줄의_식_칸은_남은_폭을_채우고_지우기가_표_폭_안에_선다() => HeadlessUi.Run(() =>
    {
        // 식 칸이 고정 폭이면 인스펙터 기본 폭에서 줄 끝의 [✕]가 밖으로 밀려 조건을 지울 수 없다.
        (ChapterGraphView view, _) = Show();
        Panel row = Row(view, "condition:신뢰높음");
        StackPanel table = view.FindControl<StackPanel>("ConditionListPanel")!;

        AssertInside(Buttons(row).Single(), table, "[✕]");
        TextBox expression = Boxes(row).Single();
        AssertInside(expression, table, "식 칸");
        Assert.True(expression.Bounds.Width >= 120, $"식 칸이 너무 좁다: {expression.Bounds.Width}");

        // 잘린 라벨은 마우스를 올리면 다 보인다.
        SelectableTextBlock label = row.GetLogicalDescendants().OfType<SelectableTextBlock>().Single();
        Assert.Equal("신뢰높음", ToolTip.GetTip(label));
    });

    [Fact]
    public void 긴_조건_라벨은_넓힌_칸에서_잘리고_칸_어디에_손을_올려도_전체가_보인다() => HeadlessUi.Run(() =>
    {
        const string longLabel = "성실 3 미만 · 요령 3 미만 · 신뢰 높음 · 그리고 아주 긴 꼬리";
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddChapterCondition("ch01", longLabel, "trust < 3");
        Redraw(view);

        var row = (Grid)Row(view, "condition:" + longLabel);
        Assert.Equal(ChapterGraphView.ConditionLabelWidth, row.ColumnDefinitions[0].Width.Value);
        Assert.True(ChapterGraphView.ConditionLabelWidth >= 150);

        // 라벨은 칸 안에서 잘리고(…), 풍선이 전체를 말한다.
        SelectableTextBlock label = row.GetLogicalDescendants().OfType<SelectableTextBlock>().Single();
        Assert.True(label.Bounds.Width <= ChapterGraphView.ConditionLabelWidth + 0.5);
        Assert.Equal(longLabel, ToolTip.GetTip(label));

        // 칸 어디에 손을 올려도 라벨이 받는다 — 글자 획 사이 · 「…」 곁의 빈자리도(바탕 없는 글은 받지 않아 풍선이 안 떴다).
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();   // 손 닿는 자리는 그린 장면으로 잰다
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Same(label, label.InputHitTest(new Point(label.Bounds.Width - 1, 1)));
    });

    [Fact]
    public void 예_아니오로_바꾸면_범위_칸이_걷히고_처음_값을_예_아니오로_고른다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();

        TypeCombo(Row(view, "stat:trust")).SelectedIndex = 1;
        Redraw(view);

        Panel row = Row(view, "stat:trust");
        Assert.Equal(ChapterStatType.Bool, Stat(session).Type);
        Assert.Single(Boxes(row));                                   // 표시 이름만 — 최소 · 최대가 없다
        ComboBox initial = row.GetLogicalDescendants().OfType<ComboBox>().Single(combo => Equals(combo.Tag, "stat-initial"));
        Assert.Equal(["아니오", "예"], initial.ItemsSource!.Cast<string>());

        initial.SelectedIndex = 1;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, Stat(session).Initial);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static ChapterStat Stat(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.Stats.Single(stat => stat.Key == "trust");

    private static ComboBox TypeCombo(Panel row) =>
        row.GetLogicalDescendants().OfType<ComboBox>().Single(combo => Equals(combo.Tag, "stat-type"));

    /// <summary>
    /// 열린 플라이아웃의 칸 묶음 — 플라이아웃 틀(<see cref="FlyoutPresenter"/>) 안에서만 찾는다. 인스펙터의 안내문도 같은
    /// 낱말을 쓰므로 창 전체에서 찾으면 플라이아웃이 안 떠도 통과한다.
    /// </summary>
    private static Control Prompt(ChapterGraphView view) =>
        (TopLevel.GetTopLevel(view) as Window)!.GetVisualDescendants()
            .OfType<StackPanel>()
            .First(panel => panel.IsEffectivelyVisible
                && panel.FindAncestorOfType<FlyoutPresenter>() is not null
                && panel.Children.OfType<TextBox>().Any(box => !string.IsNullOrEmpty(box.PlaceholderText)));

    /// <summary>표의 한 줄 — Tag가 「stat:키」나 「condition:라벨」이다.</summary>
    private static Panel Row(ChapterGraphView view, string tag) =>
        view.GetVisualDescendants().OfType<Panel>().Single(panel => Equals(panel.Tag, tag));

    /// <summary>그 줄의 글자 칸들 — 스탯이면 표시 이름 · 처음 값 · 최소 · 최대 차례(두 줄에 걸쳐 선다), 조건이면 식 하나.</summary>
    private static IReadOnlyList<TextBox> Boxes(Panel row) =>
        row.GetLogicalDescendants().OfType<TextBox>().ToList();

    /// <summary>그 줄의 단추들 — 스탯이면 [✎] · [✕] 차례, 조건이면 [✕] 하나.</summary>
    private static IReadOnlyList<Button> Buttons(Panel row) =>
        row.GetLogicalDescendants().OfType<Button>().ToList();

    /// <summary>칸의 오른쪽 끝이 표 폭 안에 서는가 — 넘으면 인스펙터가 그 끝을 잘라 먹는다.</summary>
    private static void AssertInside(Control cell, StackPanel table, string name)
    {
        Point end = cell.TranslatePoint(new Point(cell.Bounds.Width, 0), table)!.Value;
        Assert.True(end.X <= table.Bounds.Width + 0.5, $"{name}이(가) 표 밖으로 밀렸다: {end.X} > {table.Bounds.Width}");
    }

    /// <summary>Enter로 확정한다 — 초점을 잃는 길과 같은 자리로 간다.</summary>
    private static void Commit(TextBox box, string text)
    {
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>열려 있는 확인 플라이아웃의 내용 — 목록과 「모두 비우고 지우기」가 산다.</summary>
    private static Control Confirm(ChapterGraphView view) =>
        (TopLevel.GetTopLevel(view) as Window)!.GetVisualDescendants()
            .OfType<StackPanel>()
            .First(panel => panel.GetVisualDescendants().OfType<Button>()
                .Any(button => (button.Content as string) == "모두 비우고 지우기"));

    private static void Redraw(ChapterGraphView view)
    {
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// 스탯 `trust`와 그것을 쓰는 조건 `신뢰높음`이 선 챕터.
    ///
    /// 챕터와 스탯은 프로젝트에 실어 연다(생성자). 조건은 그 뒤에 <b>편집기로</b> 더한다
    /// (= 사람이 이 탭에서 하는 길) — 표가 살아 있는 모델을 먼저 보는지까지 함께 재게 된다.
    /// </summary>
    private (ChapterGraphView View, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        session.Open(ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1400, Height = 800, Content = view };
        window.Show();
        view.Attach(session);

        // 인스펙터에 챕터를 세운다(하이어아키의 챕터 줄을 고른 것과 같다).
        view.Inspect(new HierarchyPick(HierarchyKind.Chapter, "ch01"));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        Redraw(view);

        return (view, session);
    }
}
