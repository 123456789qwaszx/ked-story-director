using System.Text.Json.Nodes;
using Vn.Authoring.Script;

namespace Vn.Authoring.Serialization;

/// <summary>
/// 카드의 대본 하나를 결정적으로 직렬화한다 — 카드 JSON의 <c>script</c> 칸이다(형식 4).
///
/// 한 덩이 안에 두 산출물이 함께 들어간다.
/// <code>
/// lines    줄 정체성과 현재 순서 (산출물 1)
/// locales  locale별 화자·대사     (산출물 2)
/// </code>
/// 나누어 저장하면 둘의 버전이 어긋날 수 있고, 어긋난 순간 LineId가 어느 문장을 가리키는지
/// 알 수 없게 된다. 하나의 덩이로 함께 움직이게 한다.
///
/// ⛔ 형식 3까지는 대본마다 따로 선 파일(<c>script/{Id}.vnscript.json</c>)이었다 — Id · 이름을 든. 그 파일은
/// <see cref="ReadLegacyFile"/>로만 읽는다(옮기기 · <c>docs/plans/script-merge.md</c>).
/// </summary>
public static class ScriptDocumentJson
{
    /// <summary>형식 3의 대본 파일 확장자. 그 파일을 읽어서 옮길 때만 쓴다.</summary>
    public const string LegacyFileExtension = ".vnscript.json";

    private const int LegacyFileFormatVersion = 1;

    /// <summary>카드 JSON의 <c>script</c> 칸 — Id도 이름도 없다. 카드가 곧 신원이다.</summary>
    internal static JsonObject WriteBody(ScriptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var lines = new JsonArray();

        foreach (ScriptLine line in document.Lines)
        {
            var item = new JsonObject
            {
                ["id"] = line.Id,
                ["revision"] = line.Revision
            };

            if (line.IsRetired)
            {
                item["retired"] = true;
            }

            lines.Add(item);
        }

        var locales = new JsonArray();

        // locale 이름 순으로 쓴다. 언어를 추가한 순서가 파일 diff를 흔들지 않게 한다.
        foreach (ScriptLocale locale in document.Locales.OrderBy(item => item.Locale, StringComparer.Ordinal))
        {
            var entries = new JsonObject();

            // 줄 순서대로 쓴다. 사전 순으로 쓰면 사람이 파일을 읽을 수 없다.
            foreach (ScriptLine line in document.Lines)
            {
                if (!locale.Entries.TryGetValue(line.Id, out LocalizedLine? text))
                {
                    continue;
                }

                var entry = new JsonObject();

                if (text.Speaker.Length > 0)
                {
                    entry["speaker"] = text.Speaker;
                }

                entry["text"] = text.Text;
                entries[line.Id] = entry;
            }

            locales.Add(new JsonObject
            {
                ["locale"] = locale.Locale,
                ["entries"] = entries
            });
        }

        var root = new JsonObject
        {
            ["primaryLocale"] = document.PrimaryLocale,
            ["sourceRevision"] = document.SourceRevision
        };

        if (document.SourceContentHash is not null)
        {
            root["sourceContentHash"] = document.SourceContentHash;
        }

        root["lines"] = lines;
        root["locales"] = locales;

        return root;
    }

    /// <param name="owner">문제를 알릴 때 짚을 이름 — 그 카드의 Id.</param>
    internal static ScriptDocument ReadBody(JsonObject root, string owner)
    {
        var document = new ScriptDocument
        {
            PrimaryLocale = (string?)root["primaryLocale"] ?? ScriptDocument.DefaultLocale,

            // ⛔ `sourcePath`(가져온 작가 파일의 경로)는 대본 가져오기와 함께 걷혔다
            //    (2026-10-29 · L5). 옛 파일의 그 키는 읽지 않고 지나간다.
            SourceRevision = (int?)root["sourceRevision"] ?? 0,
            SourceContentHash = (string?)root["sourceContentHash"]
        };

        HashSet<string> lineIds = new(StringComparer.Ordinal);

        foreach (JsonNode? item in root["lines"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject lineJson)
            {
                continue;
            }

            string lineId = (string?)lineJson["id"]
                ?? throw new InvalidDataException($"'{owner}'의 대본 줄에 id가 없습니다.");

            if (!lineIds.Add(lineId))
            {
                throw new InvalidDataException($"'{owner}'의 대본에서 LineId '{lineId}'가 중복됩니다.");
            }

            document.Lines.Add(new ScriptLine(
                lineId,
                (int?)lineJson["revision"] ?? 1,
                (bool?)lineJson["retired"] ?? false));
        }

        foreach (JsonNode? item in root["locales"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject localeJson)
            {
                continue;
            }

            string locale = (string?)localeJson["locale"]
                ?? throw new InvalidDataException($"'{owner}'의 대본 locale에 이름이 없습니다.");

            if (document.FindLocale(locale) is not null)
            {
                throw new InvalidDataException($"'{owner}'의 대본에서 locale '{locale}'이 중복됩니다.");
            }

            var target = new ScriptLocale(locale);

            foreach ((string lineId, JsonNode? entry) in localeJson["entries"]?.AsObject()
                         ?? new JsonObject())
            {
                if (entry is not JsonObject text)
                {
                    continue;
                }

                if (!lineIds.Contains(lineId))
                {
                    throw new InvalidDataException(
                        $"'{owner}'의 대본 locale '{locale}'이 없는 LineId '{lineId}'를 가리킵니다.");
                }

                target.Entries[lineId] = new LocalizedLine(
                    (string?)text["speaker"] ?? string.Empty,
                    (string?)text["text"] ?? string.Empty);
            }

            document.Locales.Add(target);
        }

        return document;
    }

    /// <summary>
    /// 형식 3의 대본 파일 하나 — 그 대본의 Id와 내용. 이름(<c>name</c>)은 읽지 않는다(카드 이름이 곧 이름이다).
    /// </summary>
    internal static (string Id, ScriptDocument Script) ReadLegacyFile(string json)
    {
        JsonObject root = JsonSupport.ParseObject(json, "대본");
        int version = (int?)root["formatVersion"] ?? 0;

        if (version != LegacyFileFormatVersion)
        {
            throw new InvalidDataException($"대본 형식 버전 {version}은 지원하지 않습니다.");
        }

        string id = (string?)root["scriptId"]
            ?? throw new InvalidDataException("대본에 scriptId가 없습니다.");

        return (id, ReadBody(root, id));
    }

    /// <summary>같은 내용의 사본 — 형식 3에서 대본 하나를 두 카드가 가리켰을 때 둘째가 받는다.</summary>
    internal static ScriptDocument Copy(ScriptDocument document) =>
        ReadBody(WriteBody(document), "사본");
}
