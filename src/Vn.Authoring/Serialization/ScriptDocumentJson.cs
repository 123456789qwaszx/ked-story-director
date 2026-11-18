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
/// </summary>
public static class ScriptDocumentJson
{
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
        // 쓰는 모양만 받는다(2026-11-18 · old-shapes O8) — ⛔ 그 전에는 빠진 locale · 판 · 줄 판에 대신 값을 넣고 객체 아닌 항목은 건너뛰었다.
        var document = new ScriptDocument
        {
            PrimaryLocale = JsonSupport.RequiredText(root["primaryLocale"], $"'{owner}'의 대본 primaryLocale"),
            SourceRevision = JsonSupport.RequiredInt(root["sourceRevision"], $"'{owner}'의 대본 sourceRevision"),
            SourceContentHash = JsonSupport.OptionalString(root["sourceContentHash"], $"'{owner}'의 대본 sourceContentHash")
        };

        HashSet<string> lineIds = new(StringComparer.Ordinal);

        foreach (JsonObject lineJson in JsonSupport.RequiredObjects(root["lines"], $"'{owner}'의 대본 lines"))
        {
            string lineId = JsonSupport.RequiredText(lineJson["id"], $"'{owner}'의 대본 줄 id");

            if (!lineIds.Add(lineId))
            {
                throw new InvalidDataException($"'{owner}'의 대본에서 LineId '{lineId}'가 중복됩니다.");
            }

            document.Lines.Add(new ScriptLine(
                lineId,
                JsonSupport.RequiredInt(lineJson["revision"], $"'{owner}'의 대본 줄 '{lineId}'의 revision"),
                JsonSupport.Flag(lineJson["retired"], $"'{owner}'의 대본 줄 '{lineId}'의 retired")));
        }

        foreach (JsonObject localeJson in JsonSupport.RequiredObjects(root["locales"], $"'{owner}'의 대본 locales"))
        {
            string locale = JsonSupport.RequiredText(localeJson["locale"], $"'{owner}'의 대본 locale 이름");

            if (document.FindLocale(locale) is not null)
            {
                throw new InvalidDataException($"'{owner}'의 대본에서 locale '{locale}'이 중복됩니다.");
            }

            var target = new ScriptLocale(locale);

            foreach ((string lineId, JsonNode? entry) in JsonSupport.RequiredObject(localeJson["entries"], $"'{owner}'의 대본 locale '{locale}'의 entries"))
            {
                if (!lineIds.Contains(lineId))
                {
                    throw new InvalidDataException(
                        $"'{owner}'의 대본 locale '{locale}'이 없는 LineId '{lineId}'를 가리킵니다.");
                }

                JsonObject text = JsonSupport.RequiredObject(entry, $"'{owner}'의 대본 locale '{locale}'의 줄 '{lineId}'");
                target.Entries[lineId] = new LocalizedLine(
                    JsonSupport.OptionalString(text["speaker"], $"'{owner}'의 대본 줄 '{lineId}'의 speaker") ?? string.Empty,
                    JsonSupport.RequiredString(text["text"], $"'{owner}'의 대본 줄 '{lineId}'의 text"));
            }

            document.Locales.Add(target);
        }

        return document;
    }
}
