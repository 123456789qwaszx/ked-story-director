using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 챕터의 에피소드가 <b>어느 대사 노드로 재생되는가</b> — 계약의 <c>DialogueEntryId</c>는 EpisodeId에서 짓는다(2026-11-18 · old-shapes O5 —
/// ⛔ 그 전에는 판 카드의 이름에서 지었고 `대사엔트리` 칸이 따로 있었다).
///
/// ⛔ 이 파일은 한때 <b>간선에 매달린 연출 씬</b>이 계약의 `ViaNodeId`로 나가는지를 쟀다.
/// 그 칸은 2026-09-17에 <b>양쪽에서 걷혔고</b>(같은 재생 순서를 에피소드 한 칸으로 말할
/// 수 있다), 옛 프로젝트 호환도 안 본다(소유자: *"이전의 프로젝트들은 모두 버리고"*).
/// 남은 것은 빈 에피소드를 막는 검사와 이름 규칙이다.
/// </summary>
public sealed class ChapterDialogueEntryTests
{
    [Fact]
    public void 비어_있는_에피소드도_오류로_막는다()
    {
        // 소유자 보고 (2026-08-25): "YarnProject 자체가 대사노드가 텅 비어있으면 아예
        // 넣지를 않네." 그래서 빈 에피소드는 재생 시작 자체를 막는다 — 챕터 그래프에서
        // 세워야 유니티까지 안 간다.
        ChapterGraphModel chapter = BuildChapter();

        var project = new StoryProject();
        var file = new StoryFile(name: chapter.ChapterId);

        foreach (ChapterEpisode episode in chapter.Episodes)
        {
            // 판에는 노드가 다 있는데 대본이 하나도 없는 상태.
            file.Nodes.Add(new DialogueNode(name: episode.EpisodeId)
            {
                MarkedEpisodeId = episode.EpisodeId
            });
        }

        project.Files.Add(file);

        ChapterValidationResult validation =
            ChapterValidator.Validate(chapter, project);

        // 에피소드마다 제 행에서 선다 — 세 개가 비었으면 세 줄이다.
        Assert.Equal(
            3,
            validation.All.Count(item => item.Code == ChapterDiagnosticCode.EpisodeSceneEmpty));

        ChapterDiagnostic problem = Assert.Single(
            validation.All,
            item => item.Code == ChapterDiagnosticCode.EpisodeSceneEmpty &&
                item.Message.Contains("'시작'의", StringComparison.Ordinal));

        Assert.Equal(ChapterDiagnosticSeverity.Error, problem.Severity);
        Assert.Equal(ChapterSheetNames.Episodes, problem.Sheet);
        Assert.DoesNotContain("YarnProject", problem.Message);   // 사정은 코드 주석의 것이다
        Assert.Contains("[대본] 탭의 그 카드", problem.Message);   // 고치는 법을 말한다
    }

    [Fact]
    public void DialogueEntryId는_EpisodeId에서_짓는다()
    {
        // 판을 안 봐도 같다 — 챕터 모델만으로 부르는 자리(CLI·테스트)도 같은 이름을 낸다(⛔ 2026-11-18까지는 판 카드의 이름에서 지었다 ·
        // old-shapes O5).
        ChapterExportResult result =
            ChapterProgressionExporter.Export(BuildChapter());

        using var document = System.Text.Json.JsonDocument.Parse(result.Json!);

        Assert.Equal(
            "시작",
            document.RootElement
                .GetProperty("Nodes").EnumerateArray()
                .Single(node => node.GetProperty("EpisodeId").GetString() == "시작")
                .GetProperty("DialogueEntryId").GetString());
    }

    /// <summary>에피소드 셋(<c>시작</c> → <c>믿는길</c>/<c>혼자길</c>, 둘이 서로 오간다)짜리 챕터.</summary>
    private static ChapterGraphModel BuildChapter() => new ChapterDocument
    {
        ChapterId = "ch01",
        Episodes =
        [
            new ChapterEpisode("시작", "복도", string.Empty, 0, 0, null),
            new ChapterEpisode("믿는길", "믿는다", string.Empty, 1, 0, null),
            new ChapterEpisode("혼자길", "혼자 간다", string.Empty, 1, 1, null)
        ],
        Edges =
        [
            new ChapterEdge("시작", "믿는길", "믿는다", null, null),
            new ChapterEdge("시작", "혼자길", "혼자 간다", null, null),
            new ChapterEdge("믿는길", "혼자길", "계속", null, null),
            new ChapterEdge("혼자길", "믿는길", "계속", null, null)
        ]
    }.ToGraphModel("ch01");
}
