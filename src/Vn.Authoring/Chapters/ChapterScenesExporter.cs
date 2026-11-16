using System.Text.Json;
using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>장면 단위 내보내기</b> — <c>exported/{챕터}.scenes.json</c> (2026-11-16 · <c>docs/work-orders/scene-notes-orders.md</c> S5).
///
/// AI가 장면 하나씩 연출 초안을 낼 때 읽는 파일이다: 장면마다 작가의 장면 메모, 에피소드마다 지시(<see cref="ChapterEpisode.Memo"/>)와
/// 대본의 살아 있는 줄(LineId · 화자 · 글). 줄마다 연출을 거는 열쇠가 LineId라 그대로 싣는다.
///
/// <b>산출물이다</b> — 편집 가능한 대사 사본이 아니다(§6-0). 원고는 프로젝트이고, 이 파일은 내보낼 때마다 모델에서 다시 짓는다.
/// 런타임은 읽지 않는다. 진행 JSON이 나갈 때 함께 나간다(<see cref="ChapterExportService.ExportAll"/>).
///
/// <b>결정적이다</b> — 같은 입력이면 같은 바이트(<see cref="ChapterExportBytes"/> 규칙: UTF-8 BOM 없음 · LF). 차례는
/// 장면은 하이어아키 차례(<see cref="ChapterSceneGrouping.Of"/>), 에피소드는 장면 안 차례, 줄은 대본 차례, 주인 없는 메모는 열쇠 차례.
/// 빈 칸은 쓰지 않는다.
/// </summary>
public static class ChapterScenesExporter
{
    /// <summary>형식 이름 — 모양을 바꾸면 올린다.</summary>
    public const string Format = "ked-scenes/1";

    /// <summary>파일 이름 규칙 — <c>{챕터}.scenes.json</c>, 진행 JSON 옆.</summary>
    public const string FileSuffix = ".scenes.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>그 챕터의 장면 파일이 놓일 자리 — 진행 JSON과 같은 <c>exported/</c>.</summary>
    public static string PathFor(string projectPath, string chapterId) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(projectPath))!,
        ChapterExportService.ExportFolderName,
        chapterId + FileSuffix);

    /// <param name="project">대본(카드)을 읽을 프로젝트. 없거나 카드가 없는 에피소드는 <c>lines</c>가 빈 목록이다.</param>
    public static string Export(ChapterGraphModel chapter, StoryProject? project)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var scenes = new JsonArray();

        foreach (ChapterScene scene in ChapterSceneGrouping.Of(chapter))
        {
            var json = new JsonObject
            {
                ["sceneId"] = scene.SceneId,
                ["displayName"] = scene.DisplayName,
                ["rootEpisodeId"] = scene.RootEpisodeId
            };

            if (ChapterSceneNotes.Of(chapter.SceneNotes, scene.SceneId) is { } note)
            {
                json["note"] = note.Text;
            }

            var episodes = new JsonArray();

            foreach (ChapterEpisode episode in scene.Episodes)
            {
                episodes.Add(Episode(chapter.ChapterId, episode, project));
            }

            json["episodes"] = episodes;
            scenes.Add(json);
        }

        var root = new JsonObject
        {
            ["format"] = Format,
            ["generatedBy"] = "ked-story-director — 산출물이다. 원고는 프로젝트에서 고친다",
            ["chapterId"] = chapter.ChapterId,
            ["displayName"] = string.IsNullOrWhiteSpace(chapter.DisplayName) ? chapter.ChapterId : chapter.DisplayName,
            ["scenes"] = scenes
        };

        List<ChapterSceneNote> orphans = ChapterSceneNotes.Orphans(chapter.Episodes, chapter.SceneNotes);

        if (orphans.Count > 0)
        {
            var array = new JsonArray();

            foreach (ChapterSceneNote orphan in orphans)
            {
                array.Add(new JsonObject { ["sceneId"] = orphan.SceneId, ["text"] = orphan.Text });
            }

            root["orphanNotes"] = array;
        }

        return ChapterExportBytes.Normalize(root.ToJsonString(Options)) + "\n";
    }

    private static JsonObject Episode(string chapterId, ChapterEpisode episode, StoryProject? project)
    {
        var json = new JsonObject
        {
            ["episodeId"] = episode.EpisodeId,
            ["title"] = episode.ShownTitle
        };

        if (!string.IsNullOrWhiteSpace(episode.Memo))
        {
            json["note"] = episode.Memo;
        }

        var lines = new JsonArray();

        if (project is not null && EpisodeNaming.CardFor(project, chapterId, episode.EpisodeId) is { } card)
        {
            ScriptDocument script = card.Script;

            foreach (ScriptLine line in script.ActiveLines)
            {
                LocalizedLine text = script.Text(line.Id);
                var item = new JsonObject { ["lineId"] = line.Id };

                if (!string.IsNullOrEmpty(text.Speaker))
                {
                    item["speaker"] = text.Speaker;
                }

                item["text"] = text.Text;
                lines.Add(item);
            }
        }

        json["lines"] = lines;
        return json;
    }
}
