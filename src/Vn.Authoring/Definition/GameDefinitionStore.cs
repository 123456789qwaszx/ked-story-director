using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vn.Authoring.Definition;

/// <summary>
/// <c>game.definition.json</c>에 쓰는 유일한 경로 — <c>speakers</c>(통째로)를 쓴다 (X5, D-4). ⛔ <c>variables</c>(더하기만 — 검증 보고의
/// [등록])는 2026-11-17에 걷혔다(<c>docs/work-orders/definition-split-orders.md</c> S1).
///
/// 챕터 그래프의 [화자] 탭은 편집 창구일 뿐이고 저장은 언제나 이 파일이다.
/// 도구 안에 사본을 두면 원천이 둘이 되어, 파일을 손으로 고친 날 둘 다 오류 없이
/// 어긋난다. 다른 키(커맨드 카탈로그 등)는 그대로 보존하고 쓰는 키만 바꾼다.
/// </summary>
public static class GameDefinitionStore
{
    /// <summary>
    /// 없으면 뼈대를 깐다. 있는 파일은 불가침이다.
    /// </summary>
    public static bool EnsureBeside(string projectPath)
    {
        string path = GameDefinition.PathFor(projectPath);

        if (File.Exists(path))
        {
            return false;
        }

        Serialization.JsonSupport.WriteAtomic(path,
            """
            {
              "_안내": "게임이 아는 어휘입니다. 화자(speakers)는 [대본] · [구성] 탭이 고칩니다. 스탯은 챕터의 것입니다.",
              "speakers": []
            }
            """ + "\n");

        return true;
    }

    public static void SaveSpeakers(string projectPath, IReadOnlyList<SpeakerSpec> speakers)
    {
        ArgumentNullException.ThrowIfNull(speakers);

        string path = GameDefinition.PathFor(projectPath);
        JsonObject root = ReadRoot(path, "speakers");

        var array = new JsonArray();

        foreach (SpeakerSpec speaker in speakers)
        {
            if (string.IsNullOrWhiteSpace(speaker.Name))
            {
                continue;
            }

            array.Add(new JsonObject
            {
                ["name"] = speaker.Name.Trim(),
                ["characterId"] = speaker.CharacterId.Trim()
            });
        }

        root["speakers"] = array;
        WriteRoot(path, root);
    }

    private static JsonObject ReadRoot(string path, string keyBeingWritten)
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
                        }) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (JsonException)
        {
            // 깨진 파일 위에 덮어써서 다른 키(카탈로그 등)를 잃는 것보다 거부가 낫다.
            throw new InvalidDataException(
                $"{GameDefinition.FileName}이 올바른 JSON이 아니라 {keyBeingWritten}를 쓸 수 없습니다. " +
                "파일을 먼저 고쳐 주세요.");
        }
    }

    private static void WriteRoot(string path, JsonObject root)
    {
        Serialization.JsonSupport.WriteAtomic(
            path,
            root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }) + "\n");
    }
}
