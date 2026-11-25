using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <c>samples/RuntimeSamples</c> — ked-presentation-runtime의 예제 챕터(연출 데모)의 <b>원고</b>다 (2026-11-04). ⛔ 축제 전날
/// (<c>festival_eve</c>)은 2026-11-18에 예제에서 걷혔다(소유자 — 불필요) — 원고는 시험 재료로 <c>tests/fixtures/festival-eve</c>에 남았다.
/// 소유자: <i>"목표는 런타임 예제를 손 대본 대신 두 툴로 내는 것입니다."</i> 이 툴이 진행 JSON을 낸다.
///
/// 이 테스트는 두 가지를 잠근다 — 원고가 <b>검증 오류 없이</b> 읽히고, 다시 내보낸 진행 JSON이
/// 저장소에 실린 <c>exported/</c>와 <b>바이트까지 같다</b>(원고를 고치고 내보내기를 잊으면 운다).
/// </summary>
public sealed class RuntimeSampleTests
{
    private static string ManifestOf(string folder, string chapterId) =>
        Path.GetFullPath($"../../../../../samples/RuntimeSamples/{folder}/{chapterId}.vnproject.json");

    private static string Lf(string text) => text.Replace("\r\n", "\n");

    [Theory]
    [InlineData("PresentationDemo", "presentation_demo")]
    public void 원고는_검증_오류_없이_읽힌다(string folder, string chapterId)
    {
        StoryProject project = ProjectStore.Load(ManifestOf(folder, chapterId)).Project;
        ChapterDocument chapter = Assert.Single(project.Chapters);
        Assert.Equal(chapterId, chapter.ChapterId);

        ChapterValidationResult validation = ChapterValidator.Validate(chapter.ToGraphModel(chapterId), project);

        Assert.False(validation.HasErrors, string.Join("\n", validation.Diagnostics.Select(item => item.Message)));

        // 에피소드마다 카드가 있고, 카드에는 대사가 있다 — 빈 카드는 런타임에서 들어갔다 곧장 나온다.
        foreach (ChapterEpisode episode in chapter.Episodes)
        {
            DialogueNode card = EpisodeNaming.CardFor(project, chapterId, episode.EpisodeId)
                ?? throw new Xunit.Sdk.XunitException($"{episode.EpisodeId}의 카드가 없습니다.");

            Assert.NotEmpty(card.Script.ActiveLines);
        }
    }

    [Theory]
    [InlineData("PresentationDemo", "presentation_demo")]
    public void 다시_내보낸_진행_JSON이_실린_것과_같다(string folder, string chapterId)
    {
        string manifest = ManifestOf(folder, chapterId);
        StoryProject project = ProjectStore.Load(manifest).Project;
        ChapterGraphModel model = Assert.Single(project.Chapters).ToGraphModel(chapterId);

        ChapterExportResult export = ChapterProgressionExporter.ExportValidated(
            model, ChapterValidator.Validate(model, project), project);

        Assert.NotNull(export.Json);
        Assert.Equal(
            Lf(File.ReadAllText(ChapterExportService.ExportPathFor(manifest, chapterId))),
            Lf(export.Json!));
    }
}
