using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Tests.Fixtures;
using Path = System.IO.Path;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// `docs/ch01.progression.sample.json`을 <b>실물 출력으로</b> 붙들어 둔다.
///
/// 그 표본은 다른 저장소(`ked-progression`)가 로더의 첫 입력으로 쓰는 파일이다. 손으로
/// 만들어 두었더니 v11에서 엔딩키가 실리기 시작했는데도 <b>엔딩이 하나도 없는 옛 모양</b>
/// 그대로 남아 있었다 — 남에게 건넨 표본이 낡는 것은 계약서가 낡는 것과 같은 무게다.
///
/// 없으면 쓰고(첫 실행), 있으면 견준다. 규격을 바꿨다면 파일을 지우고 한 번 돌리면 된다.
///
/// 입력은 계약 견본 챕터다(<see cref="SampleChapters.Contract"/>). 2026-10-29(D2)까지는
/// 같은 내용의 엑셀 워크북(<c>docs/ch01.contract-sample.xlsx</c>)을 리더로 읽었고, 챕터 Id는
/// 그 파일 이름이었다 — 표본이 그 이름을 들고 있으므로 같은 Id로 세운다.
/// </summary>
public sealed class ProgressionSampleGoldenTests
{
    private const string ChapterId = "ch01.contract-sample";

    private static string SamplePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "docs", "ch01.progression.sample.json"));

    [Fact]
    public void 건네는_표본이_지금_나가는_출력과_같다()
    {
        ChapterGraphModel chapter = SampleChapters.Model(SampleChapters.Contract, ChapterId);
        ChapterValidationResult validation = ChapterValidator.Validate(chapter);
        ChapterExportResult result = ChapterProgressionExporter.ExportValidated(
            chapter, validation, BuildProject());

        Assert.False(result.Refused,
            string.Join(" / ", result.Validation.All.Select(item => item.Message)));

        string produced = result.Json!.ReplaceLineEndings("\n");

        if (!File.Exists(SamplePath))
        {
            File.WriteAllText(SamplePath, produced);
            return;
        }

        Assert.Equal(
            File.ReadAllText(SamplePath).ReplaceLineEndings("\n").TrimEnd(),
            produced.TrimEnd());
    }

    [Fact]
    public void 표본이_v14_이벤트키를_담고_엔딩키는_없다()
    {
        // 골든 비교만으로는 "둘 다 낡은 것"을 못 잡는다. 표본이 무엇을 보여 주기로 한
        // 파일인지를 따로 건다 — v14(2026-08-26): 간선의 `엔딩키`는 개념째 폐지됐고
        // (코어 DTO는 빈 값을 기본으로 받는다), 에피소드의 `이벤트키`가 `EventKey`로
        // 실린다(유니티 전용 패스스루 — 코어 DTO에 칸이 서기 전까지 로더는 무시한다).
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(SamplePath));

        Assert.All(
            document.RootElement.GetProperty("Nodes").EnumerateArray(),
            node =>
            {
                Assert.False(node.TryGetProperty("EndingKey", out _));
                Assert.False(node.TryGetProperty("IsChapterEndingCandidate", out _));
                // 에피소드 지시는 대사 Yarn의 주석으로 갔다 — 진행 JSON에는 칸이 없다(2026-11-17).
                Assert.False(node.TryGetProperty("DesignerNote", out _));
            });

        string[] eventKeys = document.RootElement
            .GetProperty("Nodes")
            .EnumerateArray()
            .Select(node => node.GetProperty("EventKey").GetString()!)
            .Where(key => key.Length > 0)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ch01_alone", "ch01_true"], eventKeys);

        JsonElement choice = document.RootElement.GetProperty("Nodes").EnumerateArray()
            .Single(node => node.GetProperty("EpisodeId").GetString() == "choice");
        JsonElement[] options = choice.GetProperty("NextOptions").EnumerateArray().ToArray();
        Assert.Equal(["trust_path", "alone_path"],
            options.Select(option => option.GetProperty("TargetEpisodeId").GetString()!).ToArray());
        // ⛔ `ViaNodeId`는 <b>계약에서 없어졌다</b> (2026-09-17 — 양쪽 완료). 길에 매달던
        //    연출은 같은 재생 순서의 <b>에피소드 한 칸</b>으로 편다:
        //    `A —문구→ 연출 —자동→ B`. 그래서 칸 자체가 안 나가야 한다.
        Assert.All(options, option =>
            Assert.False(option.TryGetProperty("ViaNodeId", out _)));

        JsonElement[] nodes = document.RootElement.GetProperty("Nodes").EnumerateArray().ToArray();
        Assert.Equal("scene_hall", nodes.Single(node => node.GetProperty("EpisodeId").GetString() == "root")
            .GetProperty("SceneId").GetString());
        Assert.True(nodes.Single(node => node.GetProperty("EpisodeId").GetString() == "root")
            .GetProperty("NextOptions")[0].GetProperty("Auto").GetBoolean());

        JsonElement gated = options[0];
        Assert.Single(gated.GetProperty("VisibleConditions").EnumerateArray());
        Assert.Single(gated.GetProperty("Conditions").EnumerateArray());
        Assert.Equal(["Add", "Set"], gated.GetProperty("StatChanges").EnumerateArray()
            .Select(change => change.GetProperty("Op").GetString()!).ToArray());
    }

    private static StoryProject BuildProject()
    {
        var project = new StoryProject();
        var file = new StoryFile(name: ChapterId);
        file.Nodes.Add(new DialogueNode(name: "Story_ch01_choice") { MarkedEpisodeId = "choice" });
        project.Files.Add(file);
        return project;
    }
}
