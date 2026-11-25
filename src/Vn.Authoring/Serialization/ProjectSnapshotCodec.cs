using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Serialization;

/// <summary>
/// Undo/Redo와 dirty 비교를 위한 디스크 독립 aggregate 스냅샷.
///
/// manifest 경로를 따라 파일을 읽지 않으며 StoryProject 전체를 문자열 하나로 왕복한다.
/// 되돌리기 한 번에 여러 실제 파일을 다시 조립할 이유가 없고, 디스크 배치가 또 바뀌어도
/// 편집 기록은 이 문자열 하나로 독립되어 있어야 한다.
/// </summary>
public static class ProjectSnapshotCodec
{
    /// <summary>3 — 카드의 대본이 카드 안에 든다(2026-10-29 · 프로젝트 형식 4). 스냅샷은 메모리에만 산다.</summary>
    public const int CurrentSnapshotVersion = 3;

    public static string Encode(StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        JsonSupport.ValidateProject(project);

        var files = new JsonArray();

        foreach (StoryFile file in project.Files)
        {
            JsonObject item = StoryFileJson.WriteObject(file);
            item.Remove("formatVersion");
            item["path"] = ProjectStore.NormalizeRelativeStoryPath(file.RelativePath);
            files.Add(item);
        }

        var root = new JsonObject
        {
            ["snapshotVersion"] = CurrentSnapshotVersion,
            ["projectFormatVersion"] = StoryProject.CurrentFormatVersion,
            ["title"] = project.Title
        };

        if (project.StartNodeId is not null)
        {
            root["startNode"] = project.StartNodeId;
        }

        root["files"] = files;

        if (ChapterDocumentJson.Write(project.Chapters) is { } chapters)
        {
            root["chapters"] = chapters;
        }

        // 화자 — 프로젝트의 것이라 되돌리기가 함께 든다.
        if (ProjectManifestJson.WriteSpeakers(project.Speakers) is { } speakers)
        {
            root["speakers"] = speakers;
        }

        // 판 크기 — 안 실으면 되돌리기가 카드 자리는 되돌리고 크기는 잃는다.
        if (ProjectManifestJson.WriteBoard(project.Board) is { } board)
        {
            root["board"] = board;
        }

        return JsonSupport.ToDeterministicText(root);
    }

    public static StoryProject Decode(string snapshot)
    {
        JsonObject root = JsonSupport.ParseObject(snapshot, Msg.File_ProjectSnapshot);
        int version = (int?)root["snapshotVersion"] ?? 0;

        if (version != CurrentSnapshotVersion || root["files"] is not JsonArray files)
        {
            throw new InvalidDataException(Msg.File_UnsupportedProjectSnapshot);
        }

        int projectFormatVersion = JsonSupport.RequiredInt(root["projectFormatVersion"], Msg.File_ProjectFormatVersionOfSnapshot);

        if (projectFormatVersion != StoryProject.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                string.Format(Msg.File_ProjectFormatVersionOfSnapshot2, projectFormatVersion));
        }

        var project = new StoryProject
        {
            Title = JsonSupport.RequiredString(root["title"], Msg.File_TitleOfSnapshot),
            StartNodeId = JsonSupport.OptionalString(root["startNode"], Msg.File_StartNodeOfSnapshot),
            Speakers = ProjectManifestJson.ReadSpeakers(root["speakers"]),
            Board = ProjectManifestJson.ReadBoard(root["board"])
        };

        foreach (JsonNode? item in files)
        {
            if (item is not JsonObject fileObject)
            {
                throw new InvalidDataException(Msg.File_FilesEntryOfProjectSnapshot);
            }

            var storyRoot = new JsonObject
            {
                ["formatVersion"] = StoryFileJson.CurrentFormatVersion,
                ["fileId"] = fileObject["fileId"]?.DeepClone(),
                ["name"] = fileObject["name"]?.DeepClone(),
                ["nodes"] = fileObject["nodes"]?.DeepClone()
            };

            StoryFile file = StoryFileJson.ReadObject(storyRoot);   // 카드의 대본은 카드 안에 든다
            file.RelativePath = ProjectStore.NormalizeRelativeStoryPath(
                JsonSupport.RequiredText(fileObject["path"], string.Format(Msg.File_PathOfFileInSnapshot, file.Id)));
            project.Files.Add(file);
        }

        project.Chapters.AddRange(ChapterDocumentJson.Read(root["chapters"]));

        JsonSupport.ValidateProject(project);
        return project;
    }
}
