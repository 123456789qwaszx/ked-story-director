using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vn.Authoring.Serialization;

/// <summary>
/// <b>게임 폴더</b> — 프로젝트 폴더의 <c>runtime.target.json</c> 하나 (2026-11-04).
///
/// 두 툴(ked-story-director · ked-story-compositor)이 <b>같은 파일을 읽고</b>, 각자 제 산출물을 그 폴더에 쓴다 —
/// director는 진행 JSON(<c>{챕터}.progression.json</c>), compositor는 Yarn. 한 파일에는 내는 쪽이 하나(K4)이므로
/// 서로의 파일은 건드리지 않는다. articy:draft가 "Unity Assets 안으로 내보내기"를 하는 것과 같은 자리다 — 사람이
/// 중간 폴더에서 손으로 나르던 일(2026-11-04 실사례)을 없앤다.
///
/// 경로는 프로젝트 폴더 기준 상대 또는 절대. 저장소가 나란히 있으면(<c>GitHub/ked-story-director</c> ·
/// <c>GitHub/ked-presentation-runtime</c>) 상대 경로가 PC 사이에서도 통한다.
///
/// <b>없거나 가리키는 폴더가 없으면 아무것도 안 한다</b> — 편의가 저작을 막지 않는다. 상태줄이 그 사실을 말한다.
/// </summary>
public sealed record RuntimeTarget(string GameFolder)
{
    public const string FileName = "runtime.target.json";

    private const string GameFolderKey = "gameFolder";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>프로젝트(manifest) 옆의 설정 파일 자리.</summary>
    public static string PathFor(string manifestPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, FileName);

    /// <summary>없거나 깨졌으면 null — 예외를 밖으로 내지 않는다(설정 하나 때문에 프로젝트를 못 열면 안 된다).</summary>
    public static RuntimeTarget? LoadBeside(string manifestPath)
    {
        string path = PathFor(manifestPath);

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            JsonNode? root = JsonNode.Parse(File.ReadAllText(path));
            string? folder = root?[GameFolderKey]?.GetValue<string>();

            return string.IsNullOrWhiteSpace(folder) ? null : new RuntimeTarget(folder.Trim());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
                                              or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    public static void SaveBeside(string manifestPath, RuntimeTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var root = new JsonObject
        {
            ["_안내"] = "게임 폴더 — 두 툴(director 진행 JSON · compositor Yarn)이 저장할 때마다 산출물을 여기로도 씁니다. 프로젝트 폴더 기준 상대 경로 또는 절대 경로.",
            [GameFolderKey] = target.GameFolder
        };

        JsonSupport.WriteAtomic(PathFor(manifestPath), root.ToJsonString(WriteOptions) + "\n");
    }

    /// <summary>프로젝트 폴더 기준으로 푼 절대 경로.</summary>
    public string Resolve(string manifestPath)
    {
        if (Path.IsPathRooted(GameFolder))
        {
            return Path.GetFullPath(GameFolder);
        }

        string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;

        return Path.GetFullPath(Path.Combine(root, GameFolder));
    }

    /// <summary>
    /// 풀린 게임 폴더가 실제로 있으면 그 경로, 없으면 null. 폴더를 <b>만들지 않는다</b> — 상대 경로가 다른 PC에서
    /// 엉뚱한 자리를 가리킬 때 거기에 폴더를 세우면 산출물이 조용히 아무 데나 쌓인다.
    /// </summary>
    public string? ResolveExisting(string manifestPath)
    {
        string resolved = Resolve(manifestPath);

        return Directory.Exists(resolved) ? resolved : null;
    }

    /// <summary>절대 경로를 프로젝트 폴더 기준 상대 경로로 — 같은 드라이브면 상대, 아니면 절대 그대로.</summary>
    public static RuntimeTarget FromAbsolute(string manifestPath, string absoluteGameFolder)
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        string full = Path.GetFullPath(absoluteGameFolder);

        if (!string.Equals(Path.GetPathRoot(root), Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimeTarget(full);
        }

        string relative = Path.GetRelativePath(root, full).Replace('\\', '/');

        return new RuntimeTarget(relative);
    }
}
