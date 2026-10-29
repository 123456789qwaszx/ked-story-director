using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vn.Authoring.Definition;

/// <summary>
/// 게임마다 달라지는 것들. 프로젝트 옆의 <c>game.definition.json</c>에서 읽는다.
///
/// VnTool은 <c>favor</c>나 <c>route</c> 같은 이름을 하나도 알지 못한다.
/// 알게 되는 순간 이 도구는 그 게임 전용이 되고, 다음 게임에서는 코드를 고쳐야 한다.
/// 그래서 "무엇을 고를 수 있는가"는 전부 이 파일이 공급하고, 도구는 목록을 보여 주기만 한다.
///
/// 파일이 없어도 저작은 계속된다. 그때는 자동완성 후보가 없을 뿐이고,
/// 작가가 직접 입력한 이름을 그대로 쓴다. 편의 기능이 없다고 원고를 못 쓰게 하지 않는다.
/// </summary>
public sealed class GameDefinition
{
    public const string FileName = "game.definition.json";

    public static GameDefinition Empty { get; } = new();

    [JsonPropertyName("variables")]
    public List<VariableSpec> Variables { get; init; } = new();

    [JsonPropertyName("events")]
    public List<EventSpec> Events { get; init; } = new();

    /// <summary>
    /// 프로젝트와 무관하게 게임이 언제나 제공하는 조건. 설정 노드가 만드는 조건에 더해진다.
    /// 여러 게임에서 반복되는 조건(예: 난이도, 언어)을 프로젝트마다 다시 만들지 않아도 된다.
    /// </summary>
    [JsonPropertyName("conditions")]
    public List<ConditionSpec> Conditions { get; init; } = new();

    /// <summary>
    /// 대본의 화자명 ↔ 초상화 characterId 매핑. 이 툴은 챕터 그래프의 화자 목록으로 관리하고 등록 안 된
    /// 화자를 알아보는 데 쓴다 — 초상화로 화자를 강조하는 것은 compositor의 무대다.
    ///
    /// 런타임의 화자 정책 DBSO는 화자명→표시명/대사창만 알고 캐릭터 키를 모르므로
    /// 이 매핑은 어차피 신규 저작 데이터다. 게임별 어휘는 정의 파일이 공급한다는
    /// 원칙 그대로 여기 둔다. 매핑이 없는 화자는 이름만 표시된다 — 오류가 아니다.
    /// </summary>
    [JsonPropertyName("speakers")]
    public List<SpeakerSpec> Speakers { get; init; } = new();

    /// <summary>화자명에 매핑된 characterId. 없으면 null — 프리뷰는 이름만 보여 준다.</summary>
    public string? FindSpeakerCharacterId(string? speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return null;
        }

        return Speakers
            .FirstOrDefault(speaker => string.Equals(speaker.Name, speakerName.Trim(), StringComparison.Ordinal))
            ?.CharacterId;
    }

    public static string PathFor(string projectPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? Environment.CurrentDirectory;

        return Path.Combine(directory, FileName);
    }

    /// <summary>
    /// 없거나 깨졌으면 빈 정의를 돌려준다. 예외를 밖으로 내보내지 않는다.
    /// 편의를 위한 파일 하나 때문에 프로젝트를 못 열게 되어서는 안 된다.
    /// </summary>
    public static GameDefinition LoadBeside(string projectPath)
    {
        string path = PathFor(projectPath);

        try
        {
            if (!File.Exists(path))
            {
                return Empty;
            }

            return Parse(File.ReadAllText(path, new UTF8Encoding(false))) ?? Empty;
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                JsonException or
                NotSupportedException or
                ArgumentException)
        {
            return Empty;
        }
    }

    /// <summary>정의 JSON 파싱 규칙의 단일 구현. 파일과 내장 기본 카탈로그가 같은 길을 지난다.</summary>
    public static GameDefinition? Parse(string json)
    {
        return JsonSerializer.Deserialize<GameDefinition>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
    }
}

public sealed class VariableSpec
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>게임이 해석하는 타입 이름. VnTool은 그대로 보여 주기만 한다.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class EventSpec
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class ConditionSpec
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("expression")]
    public string Expression { get; init; } = string.Empty;
}

public sealed class SpeakerSpec
{
    /// <summary>대본에 적히는 화자명 그대로. Ordinal 정확 일치로 찾는다.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>초상화 매니페스트의 characterId.</summary>
    [JsonPropertyName("characterId")]
    public string CharacterId { get; init; } = string.Empty;
}
