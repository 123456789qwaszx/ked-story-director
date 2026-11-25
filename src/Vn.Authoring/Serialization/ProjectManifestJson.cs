using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Text;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Serialization;

/// <summary>
/// 프로젝트 메타데이터와 <b>부속 파일의 목차</b>를 담는 manifest 형식.
///
/// 본문은 어디에도 없다. 카드와 그 대본은 <c>files</c>가 가리키는 판 파일에 있다.
/// manifest가 커지지 않아야 파일을 나눈 이유가 남는다.
///
/// <b>연출은 여기 없다</b>.
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

        // 화자 (2026-11-17 — definition-split S3). 없으면 쓰지 않는다.
        if (WriteSpeakers(project.Speakers) is { } speakers)
        {
            root["speakers"] = speakers;
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
        JsonObject root = JsonSupport.ParseObject(json, Msg.File_ProjectManifest);
        int version = (int?)root["formatVersion"] ?? 0;

        if (version > CurrentFormatVersion)
        {
            throw new InvalidDataException(
                string.Format(Msg.File_ThisProjectManifestIsFormat, version, CurrentFormatVersion));
        }

        // 앞의 형식은 옮기지 않는다(2026-11-17). 거절만 분명히 한다 — 아니면 「manifest가 아닙니다」로 엉뚱하게 거절된다.
        if (version > 0 && version < CurrentFormatVersion)
        {
            throw new InvalidDataException(
                string.Format(Msg.File_ProjectsOfFormatVersionAren, version, CurrentFormatVersion));
        }

        if (version != CurrentFormatVersion || root["files"] is not JsonArray files)
        {
            throw new InvalidDataException(Msg.File_ThisIsnTProjectManifest);
        }

        HashSet<string> allPaths = new(StringComparer.OrdinalIgnoreCase);

        var references = new List<ProjectStoryFileReference>();
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (JsonNode? item in files)
        {
            if (item is not JsonObject file)
            {
                throw new InvalidDataException(Msg.File_FilesEntryOfProjectManifest);
            }

            string id = JsonSupport.RequiredText(file["id"], Msg.File_IdOfFileEntryIn);
            string name = JsonSupport.RequiredText(file["name"], string.Format(Msg.File_NameOfStoryFile, id));
            string path = ProjectStore.NormalizeRelativeStoryPath(JsonSupport.RequiredText(file["path"], string.Format(Msg.File_PathOfStoryFile, id)));

            if (!ids.Add(id))
            {
                throw new InvalidDataException(string.Format(Msg.File_StoryFileIdIsDuplicatedIn, id));
            }

            if (!allPaths.Add(path))
            {
                throw new InvalidDataException(string.Format(Msg.File_AttachedFilePathIsDuplicated, path));
            }

            references.Add(new ProjectStoryFileReference(id, name, path));
        }

        List<ChapterDocument> chapters = ChapterDocumentJson.Read(root["chapters"]);

        return new ProjectManifest(
            JsonSupport.RequiredString(root["title"], Msg.File_TitleOfProjectManifest),
            JsonSupport.OptionalString(root["startNode"], Msg.File_StartNodeOfProjectManifest),
            references,
            chapters,
            ReadSpeakers(root["speakers"]),
            ReadBoard(root["board"]));
    }

    /// <summary>
    /// 화자 목록 — manifest와 되돌리기 스냅숏이 같은 것을 쓴다. 비었으면 null(쓰지 않는다). 각 항목은 <c>{ "name", "characterId" }</c>이고
    /// 키가 비었어도 칸을 쓴다(늘 같은 모양이 낫다).
    /// </summary>
    internal static JsonArray? WriteSpeakers(IReadOnlyList<Speaker> speakers)
    {
        if (speakers.Count == 0)
        {
            return null;
        }

        var array = new JsonArray();

        foreach (Speaker speaker in speakers)
        {
            var item = new JsonObject { ["name"] = speaker.Name, ["characterId"] = speaker.CharacterId };

            // 이름 색(작가용 · WF-D15) — 정했을 때만 적는다.
            if (speaker.Color is { } color)
            {
                item["color"] = color.Hex;
            }

            // 이름 바탕색(작가용 · WF-D22) — 정했을 때만 적는다.
            if (speaker.Fill is { } fill)
            {
                item["fill"] = fill.Hex;
            }

            array.Add(item);
        }

        return array;
    }

    /// <summary>
    /// 없으면 빈 목록. 쓰는 모양만 받는다 — 빈 · 겹친 이름은 저장 검증이 거절한다(2026-11-18 · old-shapes O8 — ⛔ 그 전에는 손으로 고친 파일을
    /// 견뎌 빈 이름을 건너뛰고 같은 이름은 처음 것만 받았다).
    /// </summary>
    internal static List<Speaker> ReadSpeakers(JsonNode? node) =>
        JsonSupport.Objects(node, "speakers")
            .Select(item => new Speaker(
                JsonSupport.RequiredString(item["name"], Msg.File_NameOfSpeaker),
                JsonSupport.RequiredString(item["characterId"], Msg.File_CharacterIdOfSpeaker),
                // 이름 색은 너그럽게 — 읽지 못하면 색 없이 선다(작가용 표시라 프로젝트를 막지 않는다).
                item["color"] is JsonValue color && color.TryGetValue(out string? hex) ? WriterRgb.Parse(hex) : null,
                item["fill"] is JsonValue fill && fill.TryGetValue(out string? fillHex) ? WriterRgb.Parse(fillHex) : null))
            .ToList();

    /// <summary>
    /// [대본] 판 크기 — manifest와 되돌리기 스냅숏이 같은 것을 쓴다(스냅숏에 안 실으면 되돌리기가 크기를 잃는다). 기본이면 null(쓰지 않는다).
    /// </summary>
    internal static JsonObject? WriteBoard(BoardSize board) =>
        board.IsDefault ? null : new JsonObject { ["width"] = board.Width, ["height"] = board.Height };

    /// <summary>
    /// 없으면 기본. 있으면 두 변이 범위(<see cref="BoardSize.Minimum"/> ~ <see cref="BoardSize.Maximum"/>) 안의 정수여야 한다 — 툴은 고른 크기를
    /// 그렇게 맞춰 쓴다(2026-11-18 · old-shapes O8 — ⛔ 그 전에는 숫자가 아니면 기본으로, 범위 밖이면 범위 안으로 들였다).
    /// </summary>
    internal static BoardSize ReadBoard(JsonNode? node)
    {
        if (node is null)
        {
            return BoardSize.Default;
        }

        JsonObject board = JsonSupport.RequiredObject(node, "board");

        static double Side(JsonNode? value, string what) =>
            JsonSupport.RequiredNumber(value, what) is var side && side == BoardSize.ClampSide(side, side)
                ? side
                : throw new InvalidDataException(string.Format(Msg.File_OfMustBeIntegerWithin, what, side, Josa.은는(side), BoardSize.Minimum, BoardSize.Maximum));

        return new BoardSize(Side(board["width"], Msg.File_BoardSizeWidth), Side(board["height"], Msg.File_BoardSizeHeight));
    }
}

public sealed record ProjectManifest(
    string Title,
    string? StartNodeId,
    IReadOnlyList<ProjectStoryFileReference> Files,
    IReadOnlyList<ChapterDocument> Chapters,
    IReadOnlyList<Speaker> Speakers,
    BoardSize Board);

public sealed record ProjectStoryFileReference(
    string Id,
    string Name,
    string RelativePath);
