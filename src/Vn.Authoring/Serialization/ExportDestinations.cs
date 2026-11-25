using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Serialization;

/// <summary>내보내는 형식 — <c>exported/</c>에는 늘 나가고, 보낼 곳을 정하면 저장할 때 거기로도 간다.</summary>
public enum ExportFormat
{
    /// <summary>진행 JSON <c>{챕터}.progression.json</c> — 런타임이 싣는다. 보낼 곳이 곧 게임 폴더다.</summary>
    Progression,

    /// <summary>대사 Yarn <c>{챕터}_{장면}.yarn</c> — 연출 없는 대사만.</summary>
    DialogueYarn
}

/// <summary>
/// <b>보낼 곳</b> — 형식마다 <c>exported/</c> 밖에 한 벌 더 낼 폴더. 프로젝트 폴더의 <c>runtime.target.json</c> 하나다
/// (2026-11-17 · export-destinations).
///
/// 진행 JSON의 보낼 곳은 <c>gameFolder</c> 칸(게임 폴더), 대사 Yarn의 보낼 곳은 <c>dialogueYarnFolder</c> 칸이다. 저장할 때
/// <b>모르는 칸은 지킨다</b>(다른 툴의 것일 수 있다). ⛔ 장면 파일의 <c>scenesFolder</c>는 2026-11-18에 장면 파일과 함께 걷혔다 — 적혀 있으면 모르는
/// 칸으로 지킬 뿐 읽지 않는다(scenes-file-removal).
///
/// 경로는 프로젝트 폴더 기준 상대 또는 절대. 저장소가 나란히 있으면 상대 경로가 PC 사이에서도 통한다. ⛔ 2026-11-17까지 이 파일은 게임 폴더
/// 하나(<c>RuntimeTarget</c>)였고 위 띠의 [🎮 게임 폴더…]가 정했다 — 이제 [⚙ 설정]의 「내보내기」가 정한다.
/// </summary>
public sealed record ExportDestinations(string? Progression, string? DialogueYarn)
{
    public const string FileName = "runtime.target.json";

    private const string ProgressionKey = "gameFolder";
    private const string DialogueYarnKey = "dialogueYarnFolder";

    /// <summary>안내 칸의 이름 — 적을 때의 화면 언어로(「_안내」 · 「_note」 · english-ui E8). 읽을 때는 보지 않는다.</summary>
    private static string NoteKey => Msg.Export_NoteKey;

    /// <summary>
    /// 두 언어의 안내 칸 이름 — 다시 적을 때 둘 다 걷는다. 다른 언어로 적힌 옛 칸이 「모르는 칸」으로 남아 지켜지면 안내가 둘이 되고, 아무 곳도
    /// 안 정했을 때 파일이 걷히지 않는다.
    /// </summary>
    private static IEnumerable<string> NoteKeys =>
        new[] { "ko", "en" }
            .Select(language => Msg.ResourceManager.GetString(nameof(Msg.Export_NoteKey), CultureInfo.GetCultureInfo(language)))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>아무 곳도 정하지 않았다 — <c>exported/</c>에만 낸다.</summary>
    public static ExportDestinations None { get; } = new(null, null);

    public static IReadOnlyList<ExportFormat> Formats { get; } = [ExportFormat.Progression, ExportFormat.DialogueYarn];

    public bool IsEmpty => Formats.All(format => FolderOf(format) is null);

    /// <summary>그 형식의 보낼 곳(적힌 그대로 — 상대 또는 절대). 정하지 않았으면 null.</summary>
    public string? FolderOf(ExportFormat format) => format switch
    {
        ExportFormat.Progression => Progression,
        _ => DialogueYarn
    };

    /// <summary>그 형식의 보낼 곳을 바꾼 사본 — 비우면 끈다.</summary>
    public ExportDestinations With(ExportFormat format, string? folder)
    {
        string? clean = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();

        return format switch
        {
            ExportFormat.Progression => this with { Progression = clean },
            _ => this with { DialogueYarn = clean }
        };
    }

    /// <summary>사람이 읽는 형식 이름 — 설정 창 · 보고가 같은 말을 쓴다.</summary>
    public static string LabelOf(ExportFormat format) => format switch
    {
        ExportFormat.Progression => Msg.Export_ProgressionJSON,
        _ => Msg.Export_YarnDialogue
    };

    /// <summary>프로젝트(manifest) 옆의 설정 파일 자리.</summary>
    public static string PathFor(string manifestPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, FileName);

    /// <summary>없거나 깨졌으면 <see cref="None"/> — 예외를 밖으로 내지 않는다(설정 하나 때문에 프로젝트를 못 열면 안 된다).</summary>
    public static ExportDestinations LoadBeside(string manifestPath)
    {
        try
        {
            JsonObject? root = ReadRoot(PathFor(manifestPath));

            return root is null
                ? None
                : new ExportDestinations(Text(root, ProgressionKey), Text(root, DialogueYarnKey));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
                                              or InvalidOperationException or FormatException)
        {
            return None;
        }
    }

    /// <summary>
    /// 적는다 — 이 툴의 두 칸만 고치고 <b>모르는 칸은 지킨다</b>. 남는 칸이 없으면(아무 곳도 안 정했다) 파일을 걷는다.
    /// </summary>
    public static void SaveBeside(string manifestPath, ExportDestinations destinations)
    {
        ArgumentNullException.ThrowIfNull(destinations);

        string path = PathFor(manifestPath);
        JsonObject root;

        try
        {
            root = ReadRoot(path) ?? new JsonObject();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            root = new JsonObject();   // 깨진 파일은 고쳐 쓴다 — 읽을 때도 없는 것으로 봤다
        }

        foreach (string key in NoteKeys)
        {
            root.Remove(key);
        }
        Put(root, ProgressionKey, destinations.Progression);
        Put(root, DialogueYarnKey, destinations.DialogueYarn);

        if (root.Count == 0)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        var written = new JsonObject
        {
            [NoteKey] = Msg.Export_TargetNote
        };

        foreach ((string key, JsonNode? value) in root.ToList())
        {
            root.Remove(key);
            written[key] = value;
        }

        JsonSupport.WriteAtomic(path, written.ToJsonString(WriteOptions) + "\n");
    }

    /// <summary>그 형식의 보낼 곳을 프로젝트 폴더 기준으로 푼 절대 경로 — 정하지 않았으면 null.</summary>
    public string? Resolve(string manifestPath, ExportFormat format) =>
        FolderOf(format) is { } folder ? ResolveFolder(manifestPath, folder) : null;

    /// <summary>
    /// 풀린 보낼 곳이 실제로 있으면 그 경로, 없으면 null. 폴더를 <b>만들지 않는다</b> — 상대 경로가 다른 PC에서 엉뚱한 자리를 가리킬 때
    /// 거기에 폴더를 세우면 산출물이 조용히 아무 데나 쌓인다.
    /// </summary>
    public string? ResolveExisting(string manifestPath, ExportFormat format) =>
        Resolve(manifestPath, format) is { } resolved && Directory.Exists(resolved) ? resolved : null;

    /// <summary>적힌 경로(상대 또는 절대)를 프로젝트 폴더 기준으로 푼다.</summary>
    public static string ResolveFolder(string manifestPath, string folder)
    {
        if (Path.IsPathRooted(folder))
        {
            return Path.GetFullPath(folder);
        }

        string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;

        return Path.GetFullPath(Path.Combine(root, folder));
    }

    /// <summary>절대 경로를 적을 글로 — 같은 드라이브면 프로젝트 폴더 기준 상대 경로, 아니면 절대 그대로.</summary>
    public static string Relativize(string manifestPath, string absoluteFolder)
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        string full = Path.GetFullPath(absoluteFolder);

        return string.Equals(Path.GetPathRoot(root), Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, full).Replace('\\', '/')
            : full;
    }

    private static JsonObject? ReadRoot(string path) =>
        File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;

    private static string? Text(JsonObject root, string key) =>
        root[key]?.GetValue<string>() is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static void Put(JsonObject root, string key, string? value)
    {
        if (value is null)
        {
            root.Remove(key);
        }
        else
        {
            root[key] = value;
        }
    }
}
