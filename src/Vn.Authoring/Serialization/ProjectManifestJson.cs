using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.Authoring.Serialization;

/// <summary>
/// 프로젝트 메타데이터와 <b>부속 파일의 목차</b>를 담는 manifest 형식.
///
/// 본문은 어디에도 없다. 카드와 그 대본은 <c>files</c>가 가리키는 판 파일에 있다(형식 4 — 형식 3까지는 대본이
/// 따로 선 파일이었고 <c>scripts</c>가 가리켰다. 그 목록은 옮길 때만 읽는다).
/// manifest가 커지지 않아야 파일을 나눈 이유가 남는다. 조건 공급 link는 파일을 넘나드는 관계라서 여기에 둔다.
///
/// ⛔ <b>연출 키는 쓰지 않고 읽지도 않는다</b> (2026-10-29) — <see cref="PresentationKeys"/>의 키와 연출 배선은
/// <c>ked-story-compositor</c>의 것이다. 옛 파일에 있으면 <see cref="ProjectManifest.CarriesPresentation"/>만
/// 켠다 — 저장하면 사라지므로 <see cref="ProjectStore"/>가 그 전에 원본을 남긴다.
/// </summary>
public static class ProjectManifestJson
{
    public const int CurrentFormatVersion = StoryProject.CurrentFormatVersion;
    public const string FileExtension = ".vnproject.json";

    /// <summary>
    /// 연출 층이 manifest에 두던 키 — 발행 결과 파일 · 결과 조합 · 곡선 · 빠른 명령 · 최근 명령 · 에셋 폴더.
    /// </summary>
    public static IReadOnlyList<string> PresentationKeys { get; } =
        ["results", "compositions", "easeCurves", "quickCommands", "recentCommands", "assetRoots"];

    /// <summary>연출 층의 배선 종류 — 명령 공급 · 연출 공급, 그리고 형식 2 시절의 실시간 연출 link.</summary>
    public static IReadOnlyList<string> PresentationLinkKinds { get; } =
        ["commandSupply", "presentationSupply", "presentation"];

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

        var links = new JsonArray();

        foreach (NodeLink link in project.Links)
        {
            links.Add(WriteLink(link));
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

        // ⛔ exportFormats(내보내기 양식) · outputPath(라이브 Yarn 출력 폴더)는 2026-10-29에 걷혔다 — Yarn과
        //    문서는 ked-story-compositor가 낸다. 옛 파일의 두 키는 읽지 않고 지나간다.

        root["files"] = files;

        if (links.Count > 0)
        {
            root["links"] = links;
        }

        // 기획자의 챕터 — 에피소드 구조·간선·선택지·조건·스탯 (R-F). 대본(scripts)이 따로
        // 파일로 나가는 것과 달리 여기 그대로 산다: 한 챕터가 수십 행이라 파일을 가를 만한
        // 부피가 아니고, 가르면 매니페스트만 보고는 판이 몇 개인지도 모른다.
        if (ChapterDocumentJson.Write(project.Chapters) is { } chapters)
        {
            root["chapters"] = chapters;
        }

        // 작가가 더한 화자 (2026-08-17) — 정의 파일이 아니라 여기 산다(정의 파일은 기획자 전용).
        // <b>이름이 빈 줄은 파일에 안 쓴다</b> — 편집 중인 빈 자리는 메모리에서는 살아 있어야
        // 하지만(안 그러면 만들자마자 사라진다) 저장물에 남길 이유는 없다.
        if (WriteWriterSpeakers(project.WriterSpeakers) is { } writerSpeakers)
        {
            root["writerSpeakers"] = writerSpeakers;
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

        if (version > 0 && version <= StoryProject.LastUnsupportedFormatVersion)
        {
            throw new InvalidDataException(
                $"형식 버전 {version} 프로젝트는 더 이상 열 수 없습니다. " +
                "그 형식에서는 화자·대사를 대사 노드가 직접 소유했고 연출이 편집 중인 노드를 " +
                "실시간으로 읽었습니다. 지금 구조로 자동 변환하면 어느 문장이 어느 LineId인지 " +
                "도구가 임의로 정하게 되므로, 덮어써서 원고를 잃는 대신 열지 않습니다. " +
                "새 프로젝트를 만들고 대사를 카드 글 칸에 붙여 넣으세요.");
        }

        if ((version != CurrentFormatVersion && version != StoryProject.LegacyScriptFilesFormatVersion) ||
            root["files"] is not JsonArray files)
        {
            throw new InvalidDataException("VnTool 프로젝트 manifest가 아닙니다.");
        }

        var scripts = new List<ScriptFileReference>();
        HashSet<string> scriptIds = new(StringComparer.Ordinal);
        HashSet<string> allPaths = new(StringComparer.OrdinalIgnoreCase);

        // 따로 선 대본 목록은 형식 3에서만 읽는다 — 저장할 때 그 파일들을 지우므로(옮겨 갔다), 형식 4에 손으로 적힌
        // `scripts`가 남의 파일을 지우게 두지 않는다.
        JsonArray? legacyScripts = version == StoryProject.LegacyScriptFilesFormatVersion
            ? root["scripts"]?.AsArray()
            : null;

        foreach (JsonNode? item in legacyScripts ?? new JsonArray())
        {
            if (item is not JsonObject script)
            {
                throw new InvalidDataException("프로젝트 manifest의 scripts 항목이 객체가 아닙니다.");
            }

            string id = (string?)script["id"]
                ?? throw new InvalidDataException("프로젝트 manifest의 대본 항목에 id가 없습니다.");
            string path = ProjectStore.NormalizeRelativePath(
                (string?)script["path"]
                    ?? throw new InvalidDataException($"대본 '{id}'에 path가 없습니다."),
                ScriptDocumentJson.LegacyFileExtension);

            if (!scriptIds.Add(id))
            {
                throw new InvalidDataException($"대본 Id '{id}'가 manifest에서 중복됩니다.");
            }

            if (!allPaths.Add(path))
            {
                throw new InvalidDataException($"부속 파일 경로 '{path}'가 manifest에서 중복됩니다.");
            }

            scripts.Add(new ScriptFileReference(id, (string?)script["name"] ?? "이름 없는 대본", path));
        }

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

        var links = new List<NodeLink>();
        HashSet<string> linkIds = new(StringComparer.Ordinal);
        bool carriesPresentation = PresentationKeys.Any(key => root[key] is not null);

        foreach (JsonNode? item in root["links"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject linkObject)
            {
                throw new InvalidDataException("프로젝트 manifest의 links 항목이 객체가 아닙니다.");
            }

            if (ReadLink(linkObject) is not { } link)
            {
                carriesPresentation = true; // 연출 배선 — 읽지 않는다
                continue;
            }

            if (!linkIds.Add(link.Id))
            {
                throw new InvalidDataException($"NodeLink Id '{link.Id}'가 manifest에서 중복됩니다.");
            }

            links.Add(link);
        }

        List<ChapterDocument> chapters = ChapterDocumentJson.Read(root["chapters"]);
        List<WriterSpeaker> writerSpeakers = ReadWriterSpeakers(root["writerSpeakers"]);

        return new ProjectManifest(
            (string?)root["title"] ?? "제목 없음",
            (string?)root["startNode"],
            scripts,
            references,
            links,
            chapters,
            writerSpeakers,
            carriesPresentation);
    }

    /// <summary>
    /// 작가 화자 직렬화 — manifest와 undo 스냅샷 코덱이 같은 것을 쓴다. 코덱이 이걸 안 실어
    /// <b>undo가 작가 화자를 지우는 잠복 버그</b>가 있었다(곡선 undo를 세우다 발견, W67 후속).
    /// </summary>
    internal static JsonArray? WriteWriterSpeakers(IReadOnlyList<WriterSpeaker> speakers)
    {
        List<WriterSpeaker> named = speakers
            .Where(speaker => !string.IsNullOrWhiteSpace(speaker.Name))
            .ToList();

        if (named.Count == 0)
        {
            return null;
        }

        var array = new JsonArray();

        foreach (WriterSpeaker speaker in named)
        {
            var entry = new JsonObject { ["name"] = speaker.Name };

            if (speaker.CharacterId.Length > 0)
            {
                entry["characterId"] = speaker.CharacterId;
            }

            array.Add(entry);
        }

        return array;
    }

    internal static List<WriterSpeaker> ReadWriterSpeakers(JsonNode? node)
    {
        var speakers = new List<WriterSpeaker>();

        foreach (JsonNode? item in node?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject speaker)
            {
                throw new InvalidDataException("writerSpeakers 항목이 객체가 아닙니다.");
            }

            speakers.Add(new WriterSpeaker
            {
                Name = (string?)speaker["name"] ?? string.Empty,
                CharacterId = (string?)speaker["characterId"] ?? string.Empty
            });
        }

        return speakers;
    }

    internal static JsonObject WriteLink(NodeLink link)
    {
        var result = new JsonObject
        {
            ["id"] = link.Id,
            ["kind"] = link.Kind switch
            {
                NodeLinkKind.Settings => "settings",
                _ => throw new InvalidDataException($"지원하지 않는 NodeLink 종류 '{link.Kind}'입니다.")
            },
            ["source"] = link.SourceNodeId,
            ["target"] = link.TargetNodeId,
            ["order"] = link.Order
        };

        if (!link.IsEnabled)
        {
            result["enabled"] = false;
        }

        return result;
    }

    /// <summary>연출 배선(<see cref="PresentationLinkKinds"/>)이면 null — 그 배선은 compositor의 것이다.</summary>
    internal static NodeLink? ReadLink(JsonObject json)
    {
        string id = (string?)json["id"]
            ?? throw new InvalidDataException("NodeLink에 id가 없습니다.");
        string? kindName = (string?)json["kind"];

        if (kindName is not null && PresentationLinkKinds.Contains(kindName))
        {
            return null;
        }

        NodeLinkKind kind = kindName switch
        {
            "settings" => NodeLinkKind.Settings,
            { } unknown => throw new InvalidDataException($"지원하지 않는 NodeLink 종류 '{unknown}'입니다."),
            _ => throw new InvalidDataException($"NodeLink '{id}'에 kind가 없습니다.")
        };
        string source = (string?)json["source"]
            ?? throw new InvalidDataException($"NodeLink '{id}'에 source가 없습니다.");
        string target = (string?)json["target"]
            ?? throw new InvalidDataException($"NodeLink '{id}'에 target이 없습니다.");

        return new NodeLink(id, kind, source, target)
        {
            IsEnabled = (bool?)json["enabled"] ?? true,
            Order = (int?)json["order"] ?? 0
        };
    }

}

/// <param name="CarriesPresentation">
/// 연출 데이터(<see cref="ProjectManifestJson.PresentationKeys"/>의 키나 연출 배선)가 있었다 — 읽지 않았다.
/// </param>
/// <param name="LegacyScripts">형식 3 매니페스트가 가리키던 따로 선 대본 파일들. 형식 4는 비어 있다.</param>
public sealed record ProjectManifest(
    string Title,
    string? StartNodeId,
    IReadOnlyList<ScriptFileReference> LegacyScripts,
    IReadOnlyList<ProjectStoryFileReference> Files,
    IReadOnlyList<NodeLink> Links,
    IReadOnlyList<ChapterDocument> Chapters,
    IReadOnlyList<WriterSpeaker> WriterSpeakers,
    bool CarriesPresentation);

/// <summary>형식 3의 따로 선 대본 파일 하나 — 옮길 때만 읽는다.</summary>
public sealed record ScriptFileReference(
    string Id,
    string Name,
    string RelativePath);

public sealed record ProjectStoryFileReference(
    string Id,
    string Name,
    string RelativePath);
