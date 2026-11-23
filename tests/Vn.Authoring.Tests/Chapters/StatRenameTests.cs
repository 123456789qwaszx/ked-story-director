using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>스탯 개명이 참조를 끌고 간다</b> (2026-09-17 — 계층을 하나로 합치기로 한 뒤).
///
/// ⛔ <b>개명이 쉬워지는 것이 곧 위험이다.</b> 키가 엑셀에 살 때는 고치기가 무서워 아무도
/// 안 고쳤다. 툴이 정의를 쥐면 한 번에 쉬워지는데 — 쉬운데 안전하지 않은 것이 어려운데
/// 안전하지 않은 것보다 나쁘다.
/// </summary>
public sealed class StatRenameTests
{
    [Fact]
    public void 정의_행과_표시이름이_함께_간다()
    {
        ProjectEditor editor = World();

        Assert.True(editor.RenameChapterStat("ch01", "trust", "favor").Applied);

        ChapterStat stat = Assert.Single(Chapter(editor).Stats,
            item => string.Equals(item.Key, "favor", StringComparison.Ordinal));

        Assert.DoesNotContain(Chapter(editor).Stats, item => item.Key == "trust");
    }

    [Fact]
    public void 간선의_스탯변화가_따라간다()
    {
        ProjectEditor editor = World();

        StatRenameOutcome outcome = editor.RenameChapterStat("ch01", "trust", "favor");

        Assert.Equal(1, outcome.Edges);
        Assert.Contains(
            Chapter(editor).Edges.SelectMany(edge => edge.StatChanges),
            delta => string.Equals(delta.Key, "favor", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Chapter(editor).Edges.SelectMany(edge => edge.StatChanges),
            delta => string.Equals(delta.Key, "trust", StringComparison.Ordinal));
    }

    [Fact]
    public void 챕터_조건의_식이_따라간다()
    {
        ProjectEditor editor = World();

        StatRenameOutcome outcome = editor.RenameChapterStat("ch01", "trust", "favor");

        Assert.Equal(1, outcome.Conditions);
        Assert.Equal("favor >= 3", Condition(editor, "신뢰높음").Expression);
    }

    [Fact]
    public void 옛_해석은_안_들고_간다()
    {
        // ⚠ 옛 해석을 들고 있으면 검증이 <b>고치기 전 값</b>으로 참을 말한다.
        ProjectEditor editor = World();

        editor.RenameChapterStat("ch01", "trust", "favor");

        Assert.False(Condition(editor, "신뢰높음").IsValid);
        Assert.Empty(Condition(editor, "신뢰높음").Parsed);
    }

    [Fact]
    public void 이름이_닮은_다른_키는_안_갈린다()
    {
        // ⛔ 단순 문자열 치환이면 `trustworthy`의 앞부분까지 갈린다.
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "닮은것", "trustworthy >= 1");

        editor.RenameChapterStat("ch01", "trust", "favor");

        Assert.Equal("trustworthy >= 1", Condition(editor, "닮은것").Expression);
    }

    [Fact]
    public void 복합식은_그_항만_갈린다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "둘", "trust >= 3; fatigue <= 2");

        editor.RenameChapterStat("ch01", "trust", "favor");

        Assert.Equal("favor >= 3; fatigue <= 2", Condition(editor, "둘").Expression);
    }

    [Fact]
    public void 픽스처의_시작값이_따라간다()
    {
        // ⚠ 시작값은 <b>스탯 키로 든 사전</b>이다. 안 갈면 그 값이 조용히 사라지고 워커가
        //    초기값으로 되돌아간다 — 재생루트가 달라 보이는데 이유가 안 보인다.
        //    (2026-09-17에 이 자리를 한 번 빠뜨렸다.)
        ProjectEditor editor = World();
        Chapter(editor).Fixtures.Add(new ChapterFixture(
            "높은신뢰", IsActive: true,
            Stats: new Dictionary<string, int> { ["trust"] = 7, ["fatigue"] = 1 },
            Choices: []));

        StatRenameOutcome outcome = editor.RenameChapterStat("ch01", "trust", "favor");

        Assert.Equal(1, outcome.Fixtures);

        IReadOnlyDictionary<string, int> stats = Chapter(editor).Fixtures.Single().Stats;
        Assert.Equal(7, stats["favor"]);
        Assert.False(stats.ContainsKey("trust"));
        Assert.Equal(1, stats["fatigue"]);
    }

    // ── 공급된 조건 — 빠뜨리면 갈래가 고아가 된다 ──────────────────────────

    [Fact]
    public void 판에_공급된_조건이_Id를_지킨_채_따라간다()
    {
        // ⛔ 여기가 이 명령이 존재하는 진짜 이유다. 줄에 매달린 전환은 조건의 <b>Id</b>로
        //    잇는데, `ChapterBoardSupply.RenameSuppliedCondition`은 <b>식으로 짝을 찾는다</b> —
        //    식이 달라지면 짝을 못 찾고 다음 동기화가 <b>새 Id로 다시 만든다</b>.
        (ProjectEditor editor, ConditionDefinition supplied) = WorldWithBoard();
        string id = supplied.Id;

        StatRenameOutcome outcome = editor.RenameChapterStat("ch01", "trust", "favor");

        Assert.Equal(1, outcome.SuppliedConditions);
        Assert.Equal(id, supplied.Id);
        Assert.Equal("stat(\"favor\") >= 3", supplied.Expression);
    }

    [Fact]
    public void 다시_동기화해도_조건이_새로_안_생긴다()
    {
        // 위의 결과를 <b>끝까지</b> 확인한다 — 식이 맞아야 동기화가 있는 것을 알아본다.
        (ProjectEditor editor, ConditionDefinition supplied) = WorldWithBoard();
        editor.RenameChapterStat("ch01", "trust", "favor");

        // 개명된 식을 해석해야 번역이 서므로 조건을 다시 저장한다(사람이 하는 길과 같다).
        editor.UpdateChapterCondition("ch01", "신뢰높음", "favor >= 3");

        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            editor,
            editor.Project.Files.Single(file => file.Name == "ch01").Id,
            editor.FindChapter("ch01")!.ToGraphModel("ch01"));

        SetNode supply = editor.Project.EnumerateNodes().OfType<SetNode>()
            .Single();   // 설정 노드는 판의 공급 노드뿐이다

        Assert.Equal(supplied.Id, Assert.Single(supply.Conditions).Id);
    }

    // ── 거절 ────────────────────────────────────────────────────────────────

    [Fact]
    public void 이미_있는_키로는_못_바꾼다()
    {
        ProjectEditor editor = World();

        StatRenameOutcome outcome = editor.RenameChapterStat("ch01", "trust", "fatigue");

        Assert.False(outcome.Applied);
        Assert.Contains("이미 있습니다", outcome.Refusal!, StringComparison.Ordinal);
        Assert.Contains(Chapter(editor).Stats, stat => stat.Key == "trust");
    }

    [Fact]
    public void 한글_빈칸_기호가_든_키로는_바꾸지_않고_옛_한글_키는_영어로_고친다()
    {
        // 2026-11-23 소유자 — 「스탯 역시, 이건 Yarn쪽에 바로 들어가는 statKey이므로 한글을 허용하지 않고 영어로 작성해야 한다는 제한을 분명히」
        // (StatKeyRule). ⛔ 2026-09-17(런타임 회신 §6.1)부터는 `stat("키")`의 인자가 문자열 리터럴이라 「호감 도」 · 「호감_도」 모두 받았다.
        ProjectEditor editor = World();
        string before = Vn.Authoring.Serialization.ProjectSnapshotCodec.Encode(editor.Project);

        StatRenameOutcome korean = editor.RenameChapterStat("ch01", "trust", "호감");
        Assert.False(korean.Applied);
        Assert.Contains("한글이 들었습니다", korean.Refusal!, StringComparison.Ordinal);
        Assert.False(editor.RenameChapterStat("ch01", "trust", "favor level").Applied);
        Assert.False(editor.RenameChapterStat("ch01", "trust", "2favor").Applied);
        Assert.Equal(before, Vn.Authoring.Serialization.ProjectSnapshotCodec.Encode(editor.Project));

        // 옛 파일의 한글 키는 영어로 고친다 — 참조가 따라간다.
        Chapter(editor).Stats.Add(new ChapterStat("호감", 0, 0, 10));
        Assert.True(editor.RenameChapterStat("ch01", "호감", "favor_level").Applied);
        Assert.Contains(Chapter(editor).Stats, stat => stat.Key == "favor_level");
    }

    [Fact]
    public void 없는_스탯은_거절한다()
    {
        Assert.False(World().RenameChapterStat("ch01", "missing", "favor").Applied);
    }

    [Fact]
    public void 거절하면_한_글자도_안_건드린다()
    {
        // ⛔ 반쯤 바꾸고 거절하면 되돌릴 자리가 없다 — 재기 전에 전부 재고, 재고 나서 한 번에 바꾼다.
        ProjectEditor editor = World();
        string before = Vn.Authoring.Serialization.ProjectSnapshotCodec.Encode(editor.Project);

        Assert.False(editor.RenameChapterStat("ch01", "trust", "fatigue").Applied);

        Assert.Equal(
            before,
            Vn.Authoring.Serialization.ProjectSnapshotCodec.Encode(editor.Project));
    }

    [Fact]
    public void 되돌리기_한_번에_넷이_함께_돌아간다()
    {
        // ⛔ 갈라 두면 정의만 바뀌고 간선은 옛 키를 가리키는 <b>반쪽</b>이 생긴다.
        (ProjectEditor editor, _) = WorldWithBoard();

        editor.RenameChapterStat("ch01", "trust", "favor");
        editor.Undo();

        // ⚠ `Restore`는 `Project`를 통째로 갈아 끼운다 — 되돌린 뒤에 다시 읽는다.
        Assert.Contains(Chapter(editor).Stats, stat => stat.Key == "trust");
        Assert.Equal("trust >= 3", Condition(editor, "신뢰높음").Expression);
        Assert.Contains(
            Chapter(editor).Edges.SelectMany(edge => edge.StatChanges),
            delta => delta.Key == "trust");
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static ChapterDocument Chapter(ProjectEditor editor) => editor.FindChapter("ch01")!;

    private static ChapterCondition Condition(ProjectEditor editor, string label) =>
        Chapter(editor).Conditions.Single(item =>
            string.Equals(item.Label, label, StringComparison.Ordinal));

    /// <summary>`trust`·`fatigue` 두 스탯, 그 중 하나를 쓰는 간선 하나와 조건 하나.</summary>
    private static ProjectEditor World()
    {
        var editor = new ProjectEditor(new StoryProject());
        ChapterDocument chapter = editor.EnsureChapter("ch01");

        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        chapter.Stats.Add(new ChapterStat("fatigue", 0, 0, 10));

        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "a");
        editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다", statChanges: "trust +1");
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

        return editor;
    }

    /// <summary>위에 더해, 판과 공급된 조건까지 선 상태.</summary>
    private static (ProjectEditor Editor, ConditionDefinition Supplied) WorldWithBoard()
    {
        ProjectEditor editor = World();
        string fileId = editor.EnsureChapterBoard("ch01");

        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            editor,
            fileId,
            editor.FindChapter("ch01")!.ToGraphModel("ch01"));

        SetNode supply = editor.Project.EnumerateNodes().OfType<SetNode>()
            .Single();   // 설정 노드는 판의 공급 노드뿐이다

        return (editor, Assert.Single(supply.Conditions));
    }
}
