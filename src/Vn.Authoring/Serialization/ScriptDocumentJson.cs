using System.Text.Json.Nodes;
using Vn.Authoring.Script;
using Vn.Authoring.Strings;

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
            PrimaryLocale = JsonSupport.RequiredText(root["primaryLocale"], string.Format(Msg.File_ScriptPrimaryLocaleOf, owner)),
            SourceRevision = JsonSupport.RequiredInt(root["sourceRevision"], string.Format(Msg.File_ScriptSourceRevisionOf, owner)),
            SourceContentHash = JsonSupport.OptionalString(root["sourceContentHash"], string.Format(Msg.File_ScriptSourceContentHashOf, owner))
        };

        HashSet<string> lineIds = new(StringComparer.Ordinal);

        foreach (JsonObject lineJson in JsonSupport.RequiredObjects(root["lines"], string.Format(Msg.File_ScriptLinesOf, owner)))
        {
            string lineId = JsonSupport.RequiredText(lineJson["id"], string.Format(Msg.File_ScriptLineIdOf, owner));

            if (!lineIds.Add(lineId))
            {
                throw new InvalidDataException(string.Format(Msg.File_LineIdIsDuplicatedInScript, owner, lineId));
            }

            document.Lines.Add(new ScriptLine(
                lineId,
                JsonSupport.RequiredInt(lineJson["revision"], string.Format(Msg.File_RevisionOfScriptLineOf, owner, lineId)),
                JsonSupport.Flag(lineJson["retired"], string.Format(Msg.File_RetiredOfScriptLineOf, owner, lineId))));
        }

        foreach (JsonObject localeJson in JsonSupport.RequiredObjects(root["locales"], string.Format(Msg.File_ScriptLocalesOf, owner)))
        {
            string locale = JsonSupport.RequiredText(localeJson["locale"], string.Format(Msg.File_ScriptLocaleNameOf, owner));

            if (document.FindLocale(locale) is not null)
            {
                throw new InvalidDataException(string.Format(Msg.File_LocaleIsDuplicatedInScript, owner, locale));
            }

            var target = new ScriptLocale(locale);

            foreach ((string lineId, JsonNode? entry) in JsonSupport.RequiredObject(localeJson["entries"], string.Format(Msg.File_EntriesOfScriptLocaleOf, owner, locale)))
            {
                if (!lineIds.Contains(lineId))
                {
                    throw new InvalidDataException(
                        string.Format(Msg.File_ScriptLocaleOfPointsTo, owner, locale, lineId));
                }

                JsonObject text = JsonSupport.RequiredObject(entry, string.Format(Msg.File_LineOfScriptLocaleOf, owner, locale, lineId));
                target.Entries[lineId] = new LocalizedLine(
                    JsonSupport.OptionalString(text["speaker"], string.Format(Msg.File_SpeakerOfScriptLineOf, owner, lineId)) ?? string.Empty,
                    JsonSupport.RequiredString(text["text"], string.Format(Msg.File_TextOfScriptLineOf, owner, lineId)));
            }

            document.Locales.Add(target);
        }

        return document;
    }
}
