using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

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
    public const int CurrentSnapshotVersion = 2;

    public static string Encode(StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        JsonSupport.ValidateProject(project);

        var scripts = new JsonArray();

        foreach (ScriptDocument script in project.Scripts)
        {
            JsonObject item = ScriptDocumentJson.WriteObject(script);
            item.Remove("formatVersion");
            scripts.Add(item);
        }

        var files = new JsonArray();

        foreach (StoryFile file in project.Files)
        {
            JsonObject item = StoryFileJson.WriteObject(file);
            item.Remove("formatVersion");
            item["path"] = ProjectStore.NormalizeRelativeStoryPath(file.RelativePath);
            files.Add(item);
        }

        var links = new JsonArray();

        foreach (NodeLink link in project.Links)
        {
            links.Add(ProjectManifestJson.WriteLink(link));
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

        root["scripts"] = scripts;
        root["files"] = files;

        if (links.Count > 0)
        {
            root["links"] = links;
        }

        // 작가 화자 — manifest와 같은 직렬화 하나를 쓴다. 예전엔 스냅샷에
        // 안 실려 <b>undo가 작가 화자를 지우는 잠복 버그</b>가 있었다(W67 후속에서 발견).
        if (ChapterDocumentJson.Write(project.Chapters) is { } chapters)
        {
            root["chapters"] = chapters;
        }

        if (ProjectManifestJson.WriteWriterSpeakers(project.WriterSpeakers) is { } writerSpeakers)
        {
            root["writerSpeakers"] = writerSpeakers;
        }

        return JsonSupport.ToDeterministicText(root);
    }

    public static StoryProject Decode(string snapshot)
    {
        JsonObject root = JsonSupport.ParseObject(snapshot, "프로젝트 스냅샷");
        int version = (int?)root["snapshotVersion"] ?? 0;

        if (version != CurrentSnapshotVersion || root["files"] is not JsonArray files)
        {
            throw new InvalidDataException("지원하지 않는 프로젝트 스냅샷입니다.");
        }

        int projectFormatVersion = (int?)root["projectFormatVersion"] ?? StoryProject.CurrentFormatVersion;

        if (projectFormatVersion > StoryProject.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"스냅샷의 프로젝트 형식 버전 {projectFormatVersion}은 지원하지 않습니다.");
        }

        var project = new StoryProject
        {
            FormatVersion = projectFormatVersion,
            Title = (string?)root["title"] ?? "제목 없음",
            StartNodeId = (string?)root["startNode"]
        };

        foreach (JsonNode? item in root["scripts"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject scriptObject)
            {
                throw new InvalidDataException("프로젝트 스냅샷의 scripts 항목이 객체가 아닙니다.");
            }

            JsonObject scriptRoot = scriptObject.DeepClone().AsObject();
            scriptRoot["formatVersion"] = ScriptDocumentJson.CurrentFormatVersion;
            project.Scripts.Add(ScriptDocumentJson.ReadObject(scriptRoot));
        }

        foreach (JsonNode? item in files)
        {
            if (item is not JsonObject fileObject)
            {
                throw new InvalidDataException("프로젝트 스냅샷의 files 항목이 객체가 아닙니다.");
            }

            var storyRoot = new JsonObject
            {
                ["formatVersion"] = StoryFileJson.CurrentFormatVersion,
                ["fileId"] = fileObject["fileId"]?.DeepClone(),
                ["name"] = fileObject["name"]?.DeepClone(),
                ["nodes"] = fileObject["nodes"]?.DeepClone()
            };

            StoryFile file = StoryFileJson.ReadObject(storyRoot);
            file.RelativePath = ProjectStore.NormalizeRelativeStoryPath(
                (string?)fileObject["path"] ?? ProjectStore.DefaultRelativePath(file.Id));
            project.Files.Add(file);
        }

        foreach (JsonNode? item in root["links"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject linkObject)
            {
                throw new InvalidDataException("프로젝트 스냅샷의 links 항목이 객체가 아닙니다.");
            }

            // 스냅샷은 이 툴이 쓴 것이라 연출 배선이 없다 — 있으면 깨진 스냅샷이다.
            project.Links.Add(ProjectManifestJson.ReadLink(linkObject)
                ?? throw new InvalidDataException("프로젝트 스냅샷에 연출 배선이 있습니다."));
        }

        project.Chapters.AddRange(ChapterDocumentJson.Read(root["chapters"]));
        project.WriterSpeakers.AddRange(ProjectManifestJson.ReadWriterSpeakers(root["writerSpeakers"]));

        JsonSupport.ValidateProject(project);
        return project;
    }
}
