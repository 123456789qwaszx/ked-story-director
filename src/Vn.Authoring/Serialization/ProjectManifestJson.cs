using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.Authoring.Serialization;

/// <summary>
/// 프로젝트 메타데이터와 <b>부속 파일의 목차</b>를 담는 manifest 형식.
///
/// 본문은 어디에도 없다. 카드와 그 대본은 <c>files</c>가 가리키는 판 파일에 있다.
/// manifest가 커지지 않아야 파일을 나눈 이유가 남는다.
///
/// <b>연출은 여기 없다</b> (2026-10-29 — <c>ked-story-compositor</c>의 것이다).
/// </summary>
public static class ProjectManifestJson
{
    public const int CurrentFormatVersion = StoryProject.CurrentFormatVersion;
    public const string FileExtension = ".vnproject.json";

    public static string Write(StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        JsonSupport.ValidateProject(project);

        var files = new JsonArray();

        foreach (StoryFile file in project.Files)
        {
            files.Add(new JsonObject
            {
                ["id"] = file.Id,
                ["name"] = file.Name,
                ["path"] = ProjectStore.NormalizeRelativeStoryPath(file.RelativePath)
            });
        }

        var root = new JsonObject
        {
            ["formatVersion"] = CurrentFormatVersion,
            ["title"] = project.Title
        };

        if (project.StartNodeId is not null)
        {
            root["startNode"] = project.StartNodeId;
        }

        root["files"] = files;

        // 기획자의 챕터 — 에피소드 구조·간선·선택지·조건·스탯 (R-F). 판 파일로 나가는 카드와
        // 달리 여기 그대로 산다: 한 챕터가 수십 행이라 파일을 가를 만한
        // 부피가 아니고, 가르면 매니페스트만 보고는 판이 몇 개인지도 모른다.
        if (ChapterDocumentJson.Write(project.Chapters) is { } chapters)
        {
            root["chapters"] = chapters;
        }

        // [대본] 판 크기 (2026-11-17) — 기본이면 쓰지 않는다.
        if (WriteBoard(project.Board) is { } board)
        {
            root["board"] = board;
        }

        return JsonSupport.ToDeterministicText(root);
    }

    public static ProjectManifest Read(string json)
    {
        JsonObject root = JsonSupport.ParseObject(json, "프로젝트 manifest");
        int version = (int?)root["formatVersion"] ?? 0;

        if (version > CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"이 프로젝트 manifest는 형식 버전 {version}입니다. " +
                $"현재 VnTool은 {CurrentFormatVersion}까지 읽을 수 있습니다.");
        }

        // 앞의 형식은 옮기지 않는다(2026-11-17). 거절만 분명히 한다 — 아니면 「manifest가 아닙니다」로 엉뚱하게 거절된다.
        if (version > 0 && version < CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"형식 버전 {version} 프로젝트는 열지 않습니다 — 이 툴은 형식 {CurrentFormatVersion}만 엽니다.");
        }

        if (version != CurrentFormatVersion || root["files"] is not JsonArray files)
        {
            throw new InvalidDataException("VnTool 프로젝트 manifest가 아닙니다.");
        }

        HashSet<string> allPaths = new(StringComparer.OrdinalIgnoreCase);

        var references = new List<ProjectStoryFileReference>();
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (JsonNode? item in files)
        {
            if (item is not JsonObject file)
            {
                throw new InvalidDataException("프로젝트 manifest의 files 항목이 객체가 아닙니다.");
            }

            string id = (string?)file["id"]
                ?? throw new InvalidDataException("프로젝트 manifest의 파일 항목에 id가 없습니다.");
            string name = (string?)file["name"] ?? "이름 없는 파일";
            string path = ProjectStore.NormalizeRelativeStoryPath(
                (string?)file["path"]
                ?? throw new InvalidDataException($"StoryFile '{id}'에 path가 없습니다."));

            if (!ids.Add(id))
            {
                throw new InvalidDataException($"StoryFile Id '{id}'가 manifest에서 중복됩니다.");
            }

            if (!allPaths.Add(path))
            {
                throw new InvalidDataException($"부속 파일 경로 '{path}'가 manifest에서 중복됩니다.");
            }

            references.Add(new ProjectStoryFileReference(id, name, path));
        }

        List<ChapterDocument> chapters = ChapterDocumentJson.Read(root["chapters"]);

        return new ProjectManifest(
            (string?)root["title"] ?? "제목 없음",
            (string?)root["startNode"],
            references,
            chapters,
            ReadBoard(root["board"]));
    }

    /// <summary>
    /// [대본] 판 크기 — manifest와 되돌리기 스냅숏이 같은 것을 쓴다(스냅숏에 안 실으면 되돌리기가 크기를 잃는다). 기본이면 null(쓰지 않는다).
    /// </summary>
    internal static JsonObject? WriteBoard(BoardSize board) =>
        board.IsDefault ? null : new JsonObject { ["width"] = board.Width, ["height"] = board.Height };

    /// <summary>없거나 숫자가 아니면 기본, 범위 밖이면 범위 안으로.</summary>
    internal static BoardSize ReadBoard(JsonNode? node)
    {
        if (node is not JsonObject board)
        {
            return BoardSize.Default;
        }

        static double Side(JsonNode? value, double fallback) =>
            value is JsonValue number && number.TryGetValue(out double side) ? BoardSize.ClampSide(side, fallback) : fallback;

        return new BoardSize(Side(board["width"], BoardSize.Default.Width), Side(board["height"], BoardSize.Default.Height));
    }
}

public sealed record ProjectManifest(
    string Title,
    string? StartNodeId,
    IReadOnlyList<ProjectStoryFileReference> Files,
    IReadOnlyList<ChapterDocument> Chapters,
    BoardSize Board);

public sealed record ProjectStoryFileReference(
    string Id,
    string Name,
    string RelativePath);
