using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>스탯 키를 바꾸면 대사의 <c>{stat("키")}</c>도 따라간다</b>(2026-11-23 소유자 — 「키를 바꿀 때 대사 글의 같은 스탯도 바꾸는게 맞고」 ·
/// <c>docs/work-orders/stat-read-rename-orders.md</c> S1–S3). 지우기의 「쓰는 곳」에 대사 줄이 들고(「모두 비우고 지우기」는 대사의 읽기를 남긴다 —
/// 2026-11-25 소유자 「남겨 두고 검증이 짚기」), 없는 스탯을 읽는 대사는 검증 보고가 경고로 짚는다(소유자 「🟡 경고로 짚기」).
/// </summary>
public sealed class StatReadRenameTests
{
    private const string Ch = "ch01";

    /// <summary>스탯 trust · fatigue, 에피소드 둘 — 첫째에 trust를 읽는 두 줄과 안 읽는 한 줄, 둘째에 trust를 읽는 한 줄.</summary>
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        ChapterDocument chapter = editor.EnsureChapter(Ch);
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        chapter.Stats.Add(new ChapterStat("fatigue", 0, 0, 10));
        editor.EnsureChapterBoard(Ch);
        editor.AddEpisode(Ch, "ep01", "식당", 0, 0, sceneId: "식당");
        editor.AddEpisode(Ch, "ep02", "사냥", 300, 0, sceneId: "사막");

        Write(editor, Ch, "ep01",
            ("샤통", "신뢰가 {stat(\"trust\")}이다."),
            ("", "피로 {stat(\"fatigue\")} · 신뢰 { stat( \"trust\" ) + 1 }"),
            ("샤통", "배고파."));
        Write(editor, Ch, "ep02", ("베니토", "{stat(\"trust\")}"));
        return editor;
    }

    private static void Write(ProjectEditor editor, string chapterId, string episodeId, params (string Speaker, string Text)[] lines)
    {
        DialogueNode card = Card(editor, chapterId, episodeId);
        editor.SetScriptLineText(card.Id, card.Script.ActiveLines.First().Id, lines[0].Speaker, lines[0].Text);

        foreach ((string speaker, string text) in lines.Skip(1))
        {
            editor.SetScriptLineText(card.Id, editor.InsertScriptLine(card.Id).Id, speaker, text);
        }
    }

    private static DialogueNode Card(ProjectEditor editor, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(editor.Project, chapterId, episodeId)!;

    private static List<string> Texts(ProjectEditor editor, string episodeId, string chapterId = Ch)
    {
        DialogueNode card = Card(editor, chapterId, episodeId);
        return card.Script.ActiveLines.Select(line => card.Script.Text(line.Id).Text).ToList();
    }

    private static List<ChapterDiagnostic> Reads(ProjectEditor editor) =>
        ChapterValidator.Validate(editor.FindChapter(Ch)!.ToGraphModel(Ch), editor.Project).Diagnostics
            .Where(item => item.Code == ChapterDiagnosticCode.StatReadUnknown)
            .ToList();

    // ── 글 ──────────────────────────────────────────────────────────────────

    [Fact]
    public void 중괄호_안의_읽기만_보고_닮은_키와_닮은_함수는_안_건드린다()
    {
        Assert.Equal(["trust", "fatigue"], ChapterStatReads.KeysIn("{stat(\"trust\")} {stat(\"fatigue\") * 2} {stat(\"trust\")}"));
        Assert.Empty(ChapterStatReads.KeysIn("stat(\"trust\") — 중괄호 밖은 그냥 글"));
        Assert.Empty(ChapterStatReads.KeysIn("{mystat(\"trust\")} {a.stat(\"trust\")}"));
        Assert.Equal(["a\"b"], ChapterStatReads.KeysIn("{stat(\"a\\\"b\")}"));

        Assert.Equal(
            "{stat(\"favor\")} {stat(\"trustworthy\")} stat(\"trust\") {mystat(\"trust\")}",
            ChapterStatReads.Replace("{stat(\"trust\")} {stat(\"trustworthy\")} stat(\"trust\") {mystat(\"trust\")}", "trust", "favor"));
    }

    // ── S1 — 키 바꾸기 ──────────────────────────────────────────────────────

    [Fact]
    public void 키를_바꾸면_그_챕터_대사의_읽기가_따라가고_상태줄이_줄_수를_센다()
    {
        ProjectEditor editor = Editor();

        StatRenameOutcome outcome = editor.RenameChapterStat(Ch, "trust", "favor");

        Assert.True(outcome.Applied);
        Assert.Equal(3, outcome.DialogueLines);
        Assert.Equal(
            ["신뢰가 {stat(\"favor\")}이다.", "피로 {stat(\"fatigue\")} · 신뢰 { stat(\"favor\") + 1 }", "배고파."],
            Texts(editor, "ep01"));
        Assert.Equal(["{stat(\"favor\")}"], Texts(editor, "ep02"));
    }

    [Fact]
    public void 줄의_개정판은_그대로이고_번역_칸도_따라간다()
    {
        // 화자 개명과 같은 규칙 — 키는 줄이 말하는 내용이 아니라서 번역 · 녹음이 낡지 않는다(RenameCarriesReferencesTests).
        ProjectEditor editor = Editor();
        DialogueNode card = Card(editor, Ch, "ep01");
        ScriptLine first = card.Script.ActiveLines.First();
        card.Script.RequireLocale("ja-JP").Entries[first.Id] = new LocalizedLine("シャトン", "信頼は{stat(\"trust\")}だ。");
        int revision = first.Revision;

        editor.RenameChapterStat(Ch, "trust", "favor");

        card = Card(editor, Ch, "ep01");
        first = card.Script.ActiveLines.First();
        Assert.Equal(revision, first.Revision);
        Assert.Equal("信頼は{stat(\"favor\")}だ。", card.Script.Text(first.Id, "ja-JP").Text);
        Assert.Equal("シャトン", card.Script.Text(first.Id, "ja-JP").Speaker);
    }

    [Fact]
    public void 다른_챕터의_같은_키는_그_챕터의_스탯이라_안_건드린다()
    {
        ProjectEditor editor = Editor();
        editor.EnsureChapter("ch02").Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.EnsureChapterBoard("ch02");
        editor.AddEpisode("ch02", "ep01", "다른 챕터", 0, 0, sceneId: "길");
        Write(editor, "ch02", "ep01", ("샤통", "{stat(\"trust\")}"));

        editor.RenameChapterStat(Ch, "trust", "favor");

        Assert.Equal(["{stat(\"trust\")}"], Texts(editor, "ep01", "ch02"));
    }

    [Fact]
    public void 되돌리기_한_번에_대사도_함께_돌아간다()
    {
        ProjectEditor editor = Editor();

        editor.RenameChapterStat(Ch, "trust", "favor");
        editor.Undo();

        Assert.Contains(editor.FindChapter(Ch)!.Stats, stat => stat.Key == "trust");
        Assert.Equal("신뢰가 {stat(\"trust\")}이다.", Texts(editor, "ep01")[0]);
        Assert.Equal(["{stat(\"trust\")}"], Texts(editor, "ep02"));
    }

    [Fact]
    public void 읽는_줄만_센다()
    {
        ProjectEditor editor = Editor();

        Assert.Equal(1, editor.RenameChapterStat(Ch, "fatigue", "tired").DialogueLines);
        Assert.Equal(0, editor.RenameChapterStat(Ch, "tired", "tired").DialogueLines);   // 같은 이름 — 거절

        ProjectEditor bare = new(new StoryProject());
        bare.EnsureChapter(Ch).Stats.Add(new ChapterStat("trust", 0, 0, 10));
        Assert.Equal(0, bare.RenameChapterStat(Ch, "trust", "favor").DialogueLines);   // 판이 없어도 바꾼다
    }

    // ── S2 — 지우기 ─────────────────────────────────────────────────────────

    [Fact]
    public void 쓰는_곳에_대사_줄이_들고_지우기를_막는다()
    {
        ProjectEditor editor = Editor();

        IReadOnlyList<ChapterUse> uses = editor.FindStatUses(Ch, "trust");

        Assert.Equal(3, uses.Count(use => use.Kind == ChapterUseKind.DialogueRead));
        ChapterUse first = uses.First(use => use.Kind == ChapterUseKind.DialogueRead);
        Assert.Equal("대사 에피소드 「식당」 1번째 줄", first.Where);
        Assert.Equal("신뢰가 {stat(\"trust\")}이다.", first.Detail);

        StatRemoveOutcome refused = editor.RemoveChapterStat(Ch, "trust");
        Assert.False(refused.Applied);
        Assert.Contains(editor.FindChapter(Ch)!.Stats, stat => stat.Key == "trust");
    }

    [Fact]
    public void 모두_비우고_지우기는_대사의_읽기를_남기고_검증이_짚는다()
    {
        ProjectEditor editor = Editor();
        Assert.Empty(Reads(editor));

        StatRemoveOutcome done = editor.RemoveChapterStat(Ch, "trust", clearUses: true);

        Assert.True(done.Applied);
        Assert.Equal(3, done.DialogueReadsLeft);
        Assert.Equal("신뢰가 {stat(\"trust\")}이다.", Texts(editor, "ep01")[0]);

        ChapterDiagnostic warning = Assert.Single(Reads(editor));
        Assert.Equal(ChapterDiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(ChapterSheetNames.Stats, warning.Sheet);
        Assert.Contains("스탯 'trust'가 이 챕터에 없습니다(대사 3줄 · 처음 나오는 곳 — 에피소드 「식당」)", warning.Message, StringComparison.Ordinal);
    }

    // ── S3 — 검증 보고 ──────────────────────────────────────────────────────

    [Fact]
    public void 없는_키를_읽는_대사는_키마다_경고_하나이고_번역_칸만_읽어도_짚는다()
    {
        ProjectEditor editor = Editor();
        Write(editor, Ch, "ep02", ("베니토", "{stat(\"mood\")} {stat(\"mood\")}"));
        DialogueNode card = Card(editor, Ch, "ep01");
        card.Script.RequireLocale("ja-JP").Entries[card.Script.ActiveLines.Last().Id] = new LocalizedLine("", "{stat(\"hunger\")}");

        List<ChapterDiagnostic> reads = Reads(editor);

        Assert.Equal(2, reads.Count);
        Assert.Contains("스탯 'hunger'", reads[0].Message, StringComparison.Ordinal);
        Assert.Contains("스탯 'mood'가 이 챕터에 없습니다(대사 1줄 · 처음 나오는 곳 — 에피소드 「사냥」)", reads[1].Message, StringComparison.Ordinal);
        Assert.False(ChapterValidator.Validate(editor.FindChapter(Ch)!.ToGraphModel(Ch), editor.Project).HasErrors);
    }

    [Fact]
    public void 대사를_고치면_검증_캐시도_따라간다()
    {
        ProjectEditor editor = Editor();
        var export = new ChapterExportService();
        ChapterEntry Entry() => new(Ch, editor.FindChapter(Ch)!.ToGraphModel(Ch));

        Assert.DoesNotContain(export.ValidationFor(Entry(), editor.Project).Diagnostics, item => item.Code == ChapterDiagnosticCode.StatReadUnknown);

        // 대사 글은 챕터 모델 밖이다 — 지문이 그것을 싣지 않으면 새 읽기에 경고가 안 선다.
        DialogueNode card = Card(editor, Ch, "ep02");
        editor.SetScriptLineText(card.Id, card.Script.ActiveLines.First().Id, "베니토", "{stat(\"mood\")}");

        Assert.Contains(export.ValidationFor(Entry(), editor.Project).Diagnostics, item => item.Code == ChapterDiagnosticCode.StatReadUnknown);
    }
}
