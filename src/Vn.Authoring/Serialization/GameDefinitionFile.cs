using System.Text.Json;
using System.Text.Json.Nodes;
using Vn.Authoring.Model;

namespace Vn.Authoring.Serialization;

/// <summary>
/// 프로젝트 옆 <c>game.definition.json</c>에서 <b>이 툴이 쓰던 칸을 걷는</b> 자리(2026-11-17 소유자 · <c>docs/work-orders/definition-split-orders.md</c> S3).
///
/// 이 툴은 이 파일을 더 읽지도 만들지도 않는다 — 스탯은 챕터의 것(S1), 전역 조건은 걷혔고(S2), 화자는 프로젝트의 것이다(S3). 남은 일은 둘이다.
/// <list type="bullet">
/// <item><b>옮김</b> — 열 때 프로젝트에 화자가 없으면 이 파일의 <c>speakers</c>를 들인다(<see cref="SpeakersBeside"/>). 소유자의 프로젝트를 잃지 않으려는
///   한 번짜리 길이다 — 옮겨진 뒤 걷는다(빚).</item>
/// <item><b>걷기</b> — 저장할 때 <c>variables</c> · <c>conditions</c> · <c>speakers</c>와 이 툴이 깔던 안내(<c>_안내</c>)를 걷는다
///   (<see cref="StripBeside"/>). 다른 칸(compositor의 연출 어휘)은 그대로 두고, 남는 것이 없으면 파일을 지운다.</item>
/// </list>
/// 런타임은 이 파일을 읽지 않는다(<c>ked-presentation-runtime</c> · <c>ked-vn-workspace/runtime</c>를 찾았다).
/// </summary>
public static class GameDefinitionFile
{
    public const string FileName = "game.definition.json";

    /// <summary>이 툴이 쓰던 칸 — 저장할 때 걷는다.</summary>
    private static readonly string[] ToolKeys = ["variables", "conditions", "speakers"];

    /// <summary>이 툴이 뼈대에 깔던 안내의 첫머리 — 이 글로 시작하는 <c>_안내</c>만 이 툴의 것이다.</summary>
    private const string GuidanceKey = "_안내";
    private const string ToolGuidancePrefix = "게임이 아는 어휘";

    public static string PathFor(string projectPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? Environment.CurrentDirectory;

        return Path.Combine(directory, FileName);
    }

    /// <summary>
    /// 이 파일의 <c>speakers</c> — 옮김에 쓴다. 없거나 깨졌으면 빈 목록이다(예외를 내지 않는다 — 파일 하나 때문에 프로젝트를 못 열면 안 된다).
    /// 읽는 규칙은 매니페스트의 화자와 같다(빈 이름은 건너뛰고 같은 이름은 처음 것만).
    /// </summary>
    public static IReadOnlyList<Speaker> SpeakersBeside(string projectPath) =>
        ReadRoot(PathFor(projectPath)) is { } root ? ProjectManifestJson.ReadSpeakers(root["speakers"]) : [];

    /// <summary>
    /// 이 툴이 쓰던 칸을 걷는다. 걷을 것이 없거나 파일이 없거나 깨졌으면 아무것도 안 한다(깨진 파일 위에 덮어써 남의 칸을 잃는 것보다 낫다).
    /// </summary>
    /// <returns>걷었으면 true(파일을 다시 썼거나 지웠다).</returns>
    public static bool StripBeside(string projectPath)
    {
        string path = PathFor(projectPath);

        if (ReadRoot(path) is not { } root)
        {
            return false;
        }

        bool stripped = false;

        foreach (string key in ToolKeys)
        {
            stripped |= root.Remove(key);
        }

        if (root[GuidanceKey] is JsonValue guidance &&
            guidance.TryGetValue(out string? text) &&
            text.StartsWith(ToolGuidancePrefix, StringComparison.Ordinal))
        {
            stripped |= root.Remove(GuidanceKey);
        }

        if (!stripped)
        {
            return false;
        }

        if (root.Count == 0)
        {
            File.Delete(path);
            return true;
        }

        JsonSupport.WriteAtomic(path, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) + "\n");

        return true;
    }

    private static JsonObject? ReadRoot(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonNode.Parse(
                    File.ReadAllText(path),
                    documentOptions: new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    }) as JsonObject
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
