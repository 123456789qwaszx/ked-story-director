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
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", 0, 0, 100));
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

        // 키 · 처음 값 · 최소 · 최대 — 키도 칸이다(writer-comfort WC4 — ⛔ 그 전에는 읽기만 하고 [✎]로 바꿨다), 종류는 콤보다.
        // 표시 이름 칸은 걷혔다(writer-fields W2).
        IReadOnlyList<TextBox> boxes = Boxes(Row(view, "stat:trust"));
        Assert.Equal(4, boxes.Count);
        Assert.Equal(("stat-key:trust", "trust"), ((string?)boxes[0].Tag, boxes[0].Text));
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
    public void 성립하지_않는_식을_적으면_검증_보고가_까닭과_고치는_법을_보인다() => HeadlessUi.Run(() =>
    {
        // 2026-11-23 소유자 — 「식을 잘못 적었을 경우, 구성탭에서 검증 보고쪽에 식이 성립하지 않는 다고 띄워주고, 구체적인 예시까지 보기좋게 설명」.
        (ChapterGraphView view, AuthoringSession session) = Show();
        var panel = view.FindControl<StackPanel>("DiagnosticsPanel")!;

        Commit(Expression(view, "신뢰높음"), "trust => 5");

        Assert.Contains("오류", (string)view.FindControl<Expander>("DiagnosticsExpander")!.Header!, StringComparison.Ordinal);
        TextBlock line = panel.Children.OfType<TextBlock>().Single(block =>
            block.Text?.Contains("조건 '신뢰높음' — 식이 성립하지 않습니다: 'trust => 5' — '=>'는 순서가 거꾸로입니다. '>='로 씁니다.", StringComparison.Ordinal) == true);

        // 바로 아래에 고치는 법 — 이 챕터의 스탯(trust 0~100)으로 든 예시가 고정폭 글꼴의 상자에 선다.
        Border help = Assert.IsType<Border>(panel.Children[panel.Children.IndexOf(line) + 1]);
        Assert.Equal(ChapterGraphView.DiagnosticHelpTag, help.Tag);
        string text = ((TextBlock)help.Child!).Text!;
        Assert.Contains("trust >= 50", text, StringComparison.Ordinal);
        Assert.Contains("trust 값이 50 이상일 때", text, StringComparison.Ordinal);
        Assert.Contains("Mono", ((TextBlock)help.Child!).FontFamily.Name, StringComparison.Ordinal);

        // 고치면 둘 다 걷힌다.
        Commit(Expression(view, "신뢰높음"), "trust >= 5");
        Assert.DoesNotContain(panel.Children.OfType<TextBlock>(), block => block.Text?.Contains("식이 성립하지 않습니다", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(panel.Children.OfType<Border>(), border => Equals(border.Tag, ChapterGraphView.DiagnosticHelpTag));
    });

    [Fact]
    public void 조건_식을_고치면_모델에_써진다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();

        Commit(Expression(view, "신뢰높음"), "trust >= 5");

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
        session.Editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        session.Editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "a");
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
        session.Editor.AddChapterStat("ch01", "spare");
        Redraw(view);

        Press(Buttons(Row(view, "stat:spare")).Last());

        Assert.DoesNotContain(session.Editor.FindChapter("ch01")!.Stats, stat => stat.Key == "spare");
    });

    [Fact]
    public void 아무_데서도_안_쓰는_조건은_X로_지워지고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「조건을 추가는 할 수 있는데, 지울 수 없다는 게 조금 아쉽고」.
        (ChapterGraphView view, AuthoringSession session) = Show();

        Press(Assert.Single(Buttons(Row(view, "condition:신뢰높음"))));

        Assert.Empty(session.Editor.FindChapter("ch01")!.Conditions);
        Assert.Contains("지웠습니다", session.StatusMessage, StringComparison.Ordinal);

        session.Editor.Undo();
        Assert.Equal(["신뢰높음"], session.Editor.FindChapter("ch01")!.Conditions.Select(item => item.Label));
    });

    [Fact]
    public void 길에서_쓰는_조건은_어디인지_보이고_모두_떼고_지운다() => HeadlessUi.Run(() =>
    {
        // 한 번에 안 지우고 <b>보여 주고 묻는다</b> — 스탯의 「모두 비우고 지우기」와 같은 결. 떼기는 그 길의 표시조건만 「없음」으로 한다.
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        session.Editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "a");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        session.Editor.UpdateEdge("ch01", "root", "a", visibleConditionLabel: "신뢰높음", matchOptionLabel: "믿는다");
        Redraw(view);

        Press(Assert.Single(Buttons(Row(view, "condition:신뢰높음"))));

        Assert.Contains(session.Editor.FindChapter("ch01")!.Conditions, item => item.Label == "신뢰높음");
        Assert.Contains("root → a", session.StatusMessage, StringComparison.Ordinal);   // 상태줄에도 같은 목록

        Control confirm = ConditionConfirm(view);
        List<string> shown = confirm.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();
        Assert.Contains(shown, text => text.Contains("1곳", StringComparison.Ordinal));
        Assert.Contains(shown, text => text.Contains("root → a", StringComparison.Ordinal));

        Press(confirm.GetVisualDescendants().OfType<Button>().Single(button => (button.Content as string) == "모두 떼고 지우기"));

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        Assert.DoesNotContain(chapter.Conditions, item => item.Label == "신뢰높음");
        Assert.Null(chapter.Edges.Single().VisibleConditionLabel);
        Assert.Equal("믿는다", chapter.Edges.Single().OptionLabel);   // 길은 남는다
        Assert.Contains("1개", session.StatusMessage, StringComparison.Ordinal);

        // 되돌리기 한 칸에 조건과 건 자리가 함께 돌아온다.
        session.Editor.Undo();
        chapter = session.Editor.FindChapter("ch01")!;
        Assert.Contains(chapter.Conditions, item => item.Label == "신뢰높음");
        Assert.Equal("신뢰높음", chapter.Edges.Single().VisibleConditionLabel);
    });

    [Fact]
    public void 키_칸에_들어서면_세이브가_안_따라온다고_먼저_말한다() => HeadlessUi.Run(() =>
    {
        // <b>키는 세이브의 열쇠다</b> (런타임 회신 §6.2). 저쪽 `LocalSaveFile.Stats`가 이
        //    글자로 저장하고, 복원은 키가 없으면 <b>처음 값</b>으로 떨어진다 — 경고도 없다.
        //    개명이 끌고 가는 다섯 자리는 프로젝트 안이고 <b>디스크의 세이브는 그 다섯에
        //    없다</b>. 그래서 경고가 <b>적기 전에</b> 서야 한다 — 키 칸에 들어서는 순간 그 줄에 선다(writer-comfort WC4).
        (ChapterGraphView view, _) = Show();
        Panel row = Row(view, "stat:trust");
        TextBlock caution = row.GetLogicalDescendants().OfType<TextBlock>().Single(block => Equals(block.Tag, "stat-key-caution"));
        Assert.False(caution.IsVisible);

        KeyBox(view, "trust").Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(caution.IsVisible);
        Assert.Contains("세이브의 열쇠", caution.Text, StringComparison.Ordinal);
        Assert.Contains("처음 값", caution.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("표시 이름", caution.Text, StringComparison.Ordinal);
    });

    [Fact]
    public void 키_칸에서_고치면_참조가_따라가고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        // 「다른 화자이름 같은것처럼 편하게 고칠 수 있도록」(2026-11-18 소유자 · writer-comfort WC4) — 고치고 Enter.
        (ChapterGraphView view, AuthoringSession session) = Show();
        long before = session.Editor.Revision;

        Commit(KeyBox(view, "trust"), " faith ");

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        Assert.Equal(["faith"], chapter.Stats.Select(stat => stat.Key));
        Assert.Equal("faith >= 3", chapter.Conditions.Single().Expression);   // 조건 식이 따라갔다
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Contains("따라갔습니다", session.StatusMessage, StringComparison.Ordinal);

        // 표가 새 키로 다시 선다.
        Redraw(view);
        Assert.Equal("faith", KeyBox(view, "faith").Text);

        session.Editor.Undo();
        Assert.Equal(["trust"], session.Editor.FindChapter("ch01")!.Stats.Select(stat => stat.Key));
    });

    [Fact]
    public void 키_칸은_같은_키_빈_키_있는_키를_넣지_않고_Esc는_그만둔다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddChapterStat("ch01", "anger");
        Redraw(view);
        long before = session.Editor.Revision;

        // 같은 키 — 앞뒤 공백만 걷고 아무 일도 없다.
        Commit(KeyBox(view, "trust"), " trust ");
        Assert.Equal("trust", KeyBox(view, "trust").Text);

        // 있는 키 — 거절하고 옛 키로 돌아온다. 상태줄이 까닭을 말한다.
        Commit(KeyBox(view, "trust"), "anger");
        Assert.Equal("trust", KeyBox(view, "trust").Text);
        Assert.Contains("이미 있습니다", session.StatusMessage, StringComparison.Ordinal);

        // 빈 키 — 지우기가 아니다.
        Commit(KeyBox(view, "trust"), "  ");
        Assert.Equal("trust", KeyBox(view, "trust").Text);
        Assert.Contains("비어 있습니다", session.StatusMessage, StringComparison.Ordinal);

        // Esc — 적던 글을 걷는다.
        TextBox key = KeyBox(view, "trust");
        key.Text = "용기";
        key.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Equal("trust", key.Text);

        Assert.Equal(before, session.Editor.Revision);
        Assert.Equal(["trust", "anger"], session.Editor.FindChapter("ch01")!.Stats.Select(stat => stat.Key));
    });

    [Fact]
    public void 스탯_표_아래_더하기_줄은_늘_서_있고_키만으로_숫자_스탯을_더한다() => HeadlessUi.Run(() =>
    {
        // 「ID를 쓰는 공간이 항상 있고, 그 오른쪽에 '+추가'를 눌러서」(2026-11-18 소유자 · writer-comfort WC4) — [대본] 탭의 화자 더하기와 같다.
        (ChapterGraphView view, AuthoringSession session) = Show();
        StackPanel table = view.FindControl<StackPanel>("StatListPanel")!;
        long before = session.Editor.Revision;

        Grid addRow = Assert.IsType<Grid>(table.Children[^1]);
        TextBox add = AddBox(view, "stat-add-name");
        Button button = addRow.Children.OfType<Button>().Single();
        Assert.Same(addRow, add.Parent);
        Assert.Equal(("＋ 추가", "stat-add"), (button.Content as string, (string?)button.Tag));
        Assert.True(Grid.GetColumn(button) > Grid.GetColumn(add));   // 칸의 오른쪽

        add.Text = " courage ";
        Press(button);

        ChapterStat made = session.Editor.FindChapter("ch01")!.Stats.Single(stat => stat.Key == "courage");
        Assert.Equal((ChapterStatType.Int, 0, 0, 100), (made.Type, made.Initial, made.Minimum, made.Maximum));
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Contains("더했습니다", session.StatusMessage, StringComparison.Ordinal);

        // 표에 그 줄이 서고, 더하기 줄은 늘 맨 아래 — 새 칸에 초점이 가 잇달아 더한다(Enter도 같다).
        Assert.NotNull(Row(view, "stat:courage"));
        Assert.IsType<Grid>(table.Children[^1]);
        TextBox again = AddBox(view, "stat-add-name");
        Assert.Same(again, TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement());
        Commit(again, "anger");
        Assert.Contains(session.Editor.FindChapter("ch01")!.Stats, stat => stat.Key == "anger");

        // 빈 키 · 있는 키는 더하지 않는다.
        long now = session.Editor.Revision;
        Commit(AddBox(view, "stat-add-name"), "  ");
        Assert.Contains("적어 주세요", session.StatusMessage, StringComparison.Ordinal);
        Commit(AddBox(view, "stat-add-name"), "trust");
        Assert.Contains("이미 있습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(now, session.Editor.Revision);

        // 한글 · 빈칸이 든 키도 더하지 않는다 — 까닭을 말하고 적은 글은 칸에 그대로다(2026-11-23 소유자 — 「Yarn쪽에 바로 들어가는 statKey이므로
        // 한글을 허용하지 않고 영어로 작성해야 한다는 제한을 분명히」).
        Commit(AddBox(view, "stat-add-name"), "호감");
        Assert.Contains("한글이 들었습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("영어로 적습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("호감", AddBox(view, "stat-add-name").Text);
        Commit(AddBox(view, "stat-add-name"), "favor level");
        Assert.Contains("쓸 수 없습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(now, session.Editor.Revision);

        // 칸과 키 칸이 그 제한을 먼저 말한다.
        Assert.Contains("영어로", AddBox(view, "stat-add-name").PlaceholderText, StringComparison.Ordinal);
        Assert.Contains("영어로 적습니다", (string)ToolTip.GetTip(Row(view, "stat:trust")!.GetLogicalDescendants().OfType<TextBox>()
            .First(box => Equals(box.Tag, "stat-key:trust")))!, StringComparison.Ordinal);
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
        Assert.Equal(["숫자(정수)", "활성/비활성"], type.ItemsSource!.Cast<string>());   // ⛔ 「숫자」(2026-11-23 소유자 — 「숫자(정수)라고」)
        Assert.Equal("숫자(정수)", type.SelectedItem);

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
    public void 조건_줄은_이름_칸과_식_칸과_지우기이고_식_칸이_남은_폭을_채운다() => HeadlessUi.Run(() =>
    {
        // 줄 끝에 [✕]가 선다 — 스탯 줄과 같다(⛔ 2026-11-26까지 지우기는 [대본] 탭의 일이었다 · writer-fields W4). 이름은 칸에서 고친다
        // (writer-comfort WC4 — ⛔ 그 전에는 읽기만 했다). 더하기는 표 아래 늘 서 있는 줄이다(⛔ 2026-11-18 side-tabs V2의 [＋ 조건] 플라이아웃은 WC4로 걷혔다).
        (ChapterGraphView view, _) = Show();
        Panel row = Row(view, "condition:신뢰높음");
        StackPanel table = view.FindControl<StackPanel>("ConditionListPanel")!;

        Button remove = Assert.Single(Buttons(row));
        Assert.Equal("✕", remove.Content);
        AssertInside(remove, table, "[✕]");
        Assert.Equal("＋ 추가", Assert.IsType<Grid>(table.Children[^1]).Children.OfType<Button>().Single().Content);
        Assert.Equal(["신뢰높음", "trust >= 3"], Boxes(row).Select(box => box.Text));
        TextBox expression = Expression(view, "신뢰높음");
        expression.Text = string.Empty;
        Assert.Equal("⚠ 식 미정 — 항상 통과합니다", expression.PlaceholderText);
        AssertInside(expression, table, "식 칸");
        Assert.True(expression.Bounds.Width >= 120, $"식 칸이 너무 좁다: {expression.Bounds.Width}");

        // 칸에 다 안 들어가는 이름은 마우스를 올리면 다 보인다.
        Assert.StartsWith("신뢰높음", (string)ToolTip.GetTip(LabelBox(view, "신뢰높음"))!, StringComparison.Ordinal);
    });

    [Fact]
    public void 조건_이름_칸에서_고치면_건_간선이_따라가고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        session.Editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "a");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        session.Editor.UpdateEdge("ch01", "root", "a", visibleConditionLabel: "신뢰높음", matchOptionLabel: "믿는다");
        Redraw(view);
        long before = session.Editor.Revision;

        Commit(LabelBox(view, "신뢰높음"), "하린과 친해졌을 때");

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        Assert.Equal(["하린과 친해졌을 때"], chapter.Conditions.Select(item => item.Label));
        Assert.Equal("하린과 친해졌을 때", chapter.Edges.Single().VisibleConditionLabel);
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Contains("간선 1개가 따라갔습니다", session.StatusMessage, StringComparison.Ordinal);

        // 같은 이름 · Esc는 넣지 않는다.
        Redraw(view);
        Commit(LabelBox(view, "하린과 친해졌을 때"), "하린과 친해졌을 때 ");
        TextBox label = LabelBox(view, "하린과 친해졌을 때");
        label.Text = "멀어짐";
        label.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Equal("하린과 친해졌을 때", label.Text);
        Assert.Equal(before + 1, session.Editor.Revision);

        session.Editor.Undo();
        Assert.Equal("신뢰높음", session.Editor.FindChapter("ch01")!.Edges.Single().VisibleConditionLabel);
    });

    [Fact]
    public void 조건_표_아래_더하기_줄은_이름만으로_식_미정_조건을_더하고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        // 「추가할 때도 대본 탭에서 화자를 추가하는 것처럼 편하게」(2026-11-18 소유자 · writer-comfort WC4) — 식은 그 줄에서 채운다.
        (ChapterGraphView view, AuthoringSession session) = Show();
        long before = session.Editor.Revision;

        Commit(AddBox(view, "condition-add-name"), "  하린과 친해졌을 때 ");

        ChapterCondition made = session.Editor.FindChapter("ch01")!.Conditions.Single(item => item.Label == "하린과 친해졌을 때");
        Assert.Equal(string.Empty, made.Expression);
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Contains("식 미정", session.StatusMessage, StringComparison.Ordinal);

        // 표에 그 줄이 서고 더하기 줄은 늘 맨 아래 — 새 칸에 초점이 간다.
        Assert.Equal(string.Empty, Expression(view, "하린과 친해졌을 때").Text);
        Assert.IsType<Grid>(view.FindControl<StackPanel>("ConditionListPanel")!.Children[^1]);
        Assert.Same(AddBox(view, "condition-add-name"), TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement());

        // [＋ 추가]도 같다.
        AddBox(view, "condition-add-name").Text = "아직 모름";
        Press(view.FindControl<StackPanel>("ConditionListPanel")!.GetLogicalDescendants().OfType<Button>()
            .Single(button => Equals(button.Tag, "condition-add")));
        Assert.Contains(session.Editor.FindChapter("ch01")!.Conditions, item => item.Label == "아직 모름");

        // 되돌리기 한 번에 사라진다.
        session.Editor.Undo();
        Assert.DoesNotContain(session.Editor.FindChapter("ch01")!.Conditions, item => item.Label == "아직 모름");
    });

    [Fact]
    public void 빈_이름과_이미_있는_이름은_조건_더하기가_받지_않는다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        long before = session.Editor.Revision;

        Commit(AddBox(view, "condition-add-name"), "   ");
        Assert.Contains("적어 주세요", session.StatusMessage, StringComparison.Ordinal);

        Commit(AddBox(view, "condition-add-name"), "신뢰높음");
        Assert.Contains("이미 있습니다", session.StatusMessage, StringComparison.Ordinal);

        Assert.Equal(before, session.Editor.Revision);
        Assert.Equal(["신뢰높음"], session.Editor.FindChapter("ch01")!.Conditions.Select(item => item.Label));
    });

    [Fact]
    public void 긴_조건_이름은_넓힌_칸_안에_서고_칸_어디에_손을_올려도_전체가_보인다() => HeadlessUi.Run(() =>
    {
        const string longLabel = "성실 3 미만 · 요령 3 미만 · 신뢰 높음 · 그리고 아주 긴 꼬리";
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.Editor.AddChapterCondition("ch01", longLabel, "trust < 3");
        Redraw(view);

        var row = (Grid)Row(view, "condition:" + longLabel);
        Assert.Equal(ChapterGraphView.ConditionLabelWidth, row.ColumnDefinitions[0].Width.Value);
        Assert.True(ChapterGraphView.ConditionLabelWidth >= 150);

        // 이름 칸은 그 폭 안에 서고, 풍선이 전체를 말한다.
        TextBox label = LabelBox(view, longLabel);
        Assert.True(label.Bounds.Width <= ChapterGraphView.ConditionLabelWidth + 0.5);
        Assert.StartsWith(longLabel, (string)ToolTip.GetTip(label)!, StringComparison.Ordinal);

        // 칸 어디에 손을 올려도 이름 칸이 받는다 — 글자 획 사이 · 끝의 빈자리도.
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();   // 손 닿는 자리는 그린 장면으로 잰다
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // 창이 그 자리에서 고른 것이 이름 칸(또는 그 안)이다 — 풍선은 이름 칸에 붙어 있다.
        TopLevel top = TopLevel.GetTopLevel(view)!;
        foreach (Point corner in new[] { new Point(label.Bounds.Width - 2, 2), new Point(2, label.Bounds.Height - 2), new Point(label.Bounds.Width / 2, label.Bounds.Height / 2) })
        {
            var hit = (Visual)top.InputHitTest(label.TranslatePoint(corner, top)!.Value)!;
            Assert.Contains(label, hit.GetSelfAndVisualAncestors());
        }
    });

    [Fact]
    public void 예_아니오로_바꾸면_범위_칸이_걷히고_처음_값을_예_아니오로_고른다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();

        TypeCombo(Row(view, "stat:trust")).SelectedIndex = 1;
        Redraw(view);

        Panel row = Row(view, "stat:trust");
        Assert.Equal(ChapterStatType.Bool, Stat(session).Type);
        Assert.Equal(["stat-key:trust"], Boxes(row).Select(box => (string?)box.Tag));   // 키 칸뿐 — 최소 · 최대가 없다(표시 이름 칸은 걷혔다)
        ComboBox initial = row.GetLogicalDescendants().OfType<ComboBox>().Single(combo => Equals(combo.Tag, "stat-initial"));
        Assert.Equal(["비활성", "활성"], initial.ItemsSource!.Cast<string>());

        initial.SelectedIndex = 1;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, Stat(session).Initial);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static ChapterStat Stat(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.Stats.Single(stat => stat.Key == "trust");

    private static ComboBox TypeCombo(Panel row) =>
        row.GetLogicalDescendants().OfType<ComboBox>().Single(combo => Equals(combo.Tag, "stat-type"));

    /// <summary>표의 한 줄 — Tag가 「stat:키」나 「condition:라벨」이다.</summary>
    private static Panel Row(ChapterGraphView view, string tag) =>
        view.GetVisualDescendants().OfType<Panel>().Single(panel => Equals(panel.Tag, tag));

    /// <summary>그 줄의 글자 칸들 — 스탯이면 키 · 처음 값 · 최소 · 최대 차례(두 줄에 걸쳐 선다), 조건이면 이름 · 식.</summary>
    private static IReadOnlyList<TextBox> Boxes(Panel row) =>
        row.GetLogicalDescendants().OfType<TextBox>().ToList();

    /// <summary>그 줄의 단추들 — 스탯이든 조건이든 [✕] 하나.</summary>
    private static IReadOnlyList<Button> Buttons(Panel row) =>
        row.GetLogicalDescendants().OfType<Button>().ToList();

    /// <summary>칸의 오른쪽 끝이 표 폭 안에 서는가 — 넘으면 인스펙터가 그 끝을 잘라 먹는다.</summary>
    private static void AssertInside(Control cell, StackPanel table, string name)
    {
        Point end = cell.TranslatePoint(new Point(cell.Bounds.Width, 0), table)!.Value;
        Assert.True(end.X <= table.Bounds.Width + 0.5, $"{name}이(가) 표 밖으로 밀렸다: {end.X} > {table.Bounds.Width}");
    }

    /// <summary>스탯 키 칸 · 조건 이름 칸 · 식 칸 · 더하기 칸 — 「조건 및 스탯」 탭에서 Tag로 찾는다.</summary>
    private static TextBox SheetBox(ChapterGraphView view, string tag) =>
        view.FindControl<StackPanel>("ConditionSection")!.GetLogicalDescendants().OfType<TextBox>().Single(box => Equals(box.Tag, tag));

    private static TextBox KeyBox(ChapterGraphView view, string key) => SheetBox(view, "stat-key:" + key);

    private static TextBox LabelBox(ChapterGraphView view, string label) => SheetBox(view, "condition-label:" + label);

    private static TextBox Expression(ChapterGraphView view, string label) => SheetBox(view, "condition-expression:" + label);

    private static TextBox AddBox(ChapterGraphView view, string tag) => SheetBox(view, tag);

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

    /// <summary>열려 있는 조건 [✕] 확인 플라이아웃의 내용 — 목록과 「모두 떼고 지우기」가 산다.</summary>
    private static Control ConditionConfirm(ChapterGraphView view) =>
        (TopLevel.GetTopLevel(view) as Window)!.GetVisualDescendants()
            .OfType<StackPanel>()
            .Single(panel => Equals(panel.Tag, ChapterGraphView.ConditionRemovePanelTag));

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
