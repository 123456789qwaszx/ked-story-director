using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>캐릭터 키가 없는 화자</b> → 검증 보고의 경고(2026-11-18 소유자 — 「구성탭에서 캐릭터 키가 없을 경우, 검증보고에서 오류 알림을」 ·
/// <c>docs/work-orders/writer-comfort-orders.md</c> WC6). 그 챕터의 대사에 나오는 화자마다 한 줄 — 지문은 세지 않고, 내보내기를 막지 않는다.
/// </summary>
public sealed class ChapterSpeakerKeyTests
{
    private const string Ch = "ch01";

    /// <summary>에피소드 둘 — 첫째에 샤통(키 없음) 두 줄 · 지문 · 베니토(키 있음), 둘째에 샤통 한 줄과 목록에 없는 「???」.</summary>
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter(Ch);
        editor.EnsureChapterBoard(Ch);
        editor.AddEpisode(Ch, "ep01", "치즈 나초 한 접시", 0, 0, sceneId: "식당");
        editor.AddEpisode(Ch, "ep02", "사냥", 300, 0, sceneId: "사막");
        editor.AddSpeaker("샤통");
        editor.SetSpeakerCharacterId("샤통", null);   // 키를 비운 화자 — 더하면 임시 키가 선다(UX29)
        editor.AddSpeaker("베니토");
        editor.SetSpeakerCharacterId("베니토", "benito");

        Write(editor, "ep01", ("샤통", "배고파."), ("샤통", "나초 먹자."), ("", "두 사람은 식당으로 들어간다."), ("베니토", "좋아."));
        Write(editor, "ep02", ("샤통", "사냥이다."), ("???", "누구냐."));
        return editor;
    }

    /// <summary>그 에피소드 카드에 줄들을 쓴다 — 새 카드의 빈 줄 하나를 첫 줄로 채우고 나머지를 그 뒤에 더한다.</summary>
    private static void Write(ProjectEditor editor, string episodeId, params (string Speaker, string Text)[] lines)
    {
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Ch, episodeId)!;
        editor.SetScriptLineText(card.Id, card.Script.ActiveLines.First().Id, lines[0].Speaker, lines[0].Text);

        foreach ((string speaker, string text) in lines.Skip(1))
        {
            editor.SetScriptLineText(card.Id, editor.InsertScriptLine(card.Id).Id, speaker, text);   // 끝에 더한다
        }
    }

    private static List<ChapterDiagnostic> Missing(ProjectEditor editor) =>
        ChapterValidator.Validate(editor.FindChapter(Ch)!.ToGraphModel(Ch), editor.Project).Diagnostics
            .Where(item => item.Code == ChapterDiagnosticCode.SpeakerCharacterKeyMissing)
            .ToList();

    [Fact]
    public void 키가_없는_화자마다_경고_하나이고_줄_수와_처음_나오는_곳을_말한다()
    {
        List<ChapterDiagnostic> missing = Missing(Editor());

        Assert.Equal(2, missing.Count);   // 샤통 · ??? — 키가 있는 베니토와 지문은 없다
        Assert.All(missing, item =>
        {
            Assert.Equal(ChapterDiagnosticSeverity.Warning, item.Severity);
            Assert.Equal(ChapterSheetNames.Speakers, item.Sheet);
            Assert.Null(item.EpisodeId);
        });

        Assert.Contains("화자 '샤통'의 캐릭터 키가 없습니다", missing[0].Message);
        Assert.Contains("대사 3줄(처음 나오는 곳 — 에피소드 「치즈 나초 한 접시」)", missing[0].Message);
        Assert.Contains("「화자」 탭에서 키를 적어", missing[0].Message);

        // 목록에 없는 이름 — 어디서 더하는지부터 말한다.
        Assert.Contains("화자 '???'는 화자 목록에 없어", missing[1].Message);
        Assert.Contains("에피소드 「사냥」", missing[1].Message);
    }

    [Fact]
    public void 임시_키인_화자는_짚지_않는다()
    {
        // 더할 때 선 임시 키(temp_ + 로마자 · usability UX29)는 정한 키다 — 소유자(UX30): 「임시 키의 경고를 없애고 싶어서 그런 거다」.
        ProjectEditor editor = Editor();
        editor.SetSpeakerCharacterId("샤통", "temp_syatong");

        Assert.DoesNotContain(Missing(editor), item => item.Message.Contains("'샤통'", StringComparison.Ordinal));
    }

    [Fact]
    public void 키를_적으면_경고가_걷히고_검증_캐시도_따라간다()
    {
        ProjectEditor editor = Editor();
        var export = new ChapterExportService();
        ChapterEntry Entry() => new(Ch, editor.FindChapter(Ch)!.ToGraphModel(Ch));

        Assert.Contains(export.ValidationFor(Entry(), editor.Project).Diagnostics,
            item => item.Code == ChapterDiagnosticCode.SpeakerCharacterKeyMissing && item.Message.Contains("'샤통'"));

        // 키는 챕터 모델 밖(프로젝트의 화자 목록)이다 — 지문이 그것을 싣지 않으면 옛 경고가 남는다.
        editor.SetSpeakerCharacterId("샤통", "chatong");

        Assert.DoesNotContain(export.ValidationFor(Entry(), editor.Project).Diagnostics,
            item => item.Code == ChapterDiagnosticCode.SpeakerCharacterKeyMissing && item.Message.Contains("'샤통'"));
    }

    [Fact]
    public void 키가_없는_화자는_검증_오류가_아니고_내보내기를_막지_않는다()
    {
        // 검증 오류 없이 내보내지는 견본(축제 전날)의 화자에게서 키를 걷는다.
        string manifest = Path.GetFullPath("../../../../../tests/fixtures/festival-eve/festival_eve.vnproject.json");
        var editor = new ProjectEditor(ProjectStore.Load(manifest).Project);
        string speaker = editor.Project.Speakers.First(item => item.CharacterId is { Length: > 0 }).Name;
        editor.SetSpeakerCharacterId(speaker, null);

        ChapterGraphModel model = editor.FindChapter("festival_eve")!.ToGraphModel("festival_eve");
        ChapterValidationResult validation = ChapterValidator.Validate(model, editor.Project);

        Assert.Contains(validation.Diagnostics, item => item.Code == ChapterDiagnosticCode.SpeakerCharacterKeyMissing && item.Message.Contains($"'{speaker}'"));
        Assert.False(validation.HasErrors, string.Join("\n", validation.Diagnostics.Select(item => item.Message)));
        Assert.NotNull(ChapterProgressionExporter.ExportValidated(model, validation, editor.Project).Json);
    }
}
