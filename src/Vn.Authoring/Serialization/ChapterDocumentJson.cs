using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Text;

namespace Vn.Authoring.Serialization;

/// <summary>
/// <see cref="ChapterDocument"/> ↔ 프로젝트 매니페스트의 <c>chapters</c> 배열
/// (R-F · 2026-09-16, 지시서 §1.1).
///
/// <b>비어 있는 값은 안 쓴다.</b> 이 저장소의 매니페스트 작법 그대로다 — 기본값을 전부
/// 적으면 사람이 파일을 열었을 때 무엇이 <i>실제로 정해진 값</i>인지 안 보이고, 기본값이
/// 바뀔 때 옛 파일이 옛 기본값을 굳혀 버린다.
///
/// <b>읽기는 쓰는 모양만 받는다</b>(2026-11-18 · old-shapes O8) — 늘 쓰는 칸이 빠졌거나 모양이 다르면 거절한다. 비어서 쓰지 않은
/// 칸만 없어도 된다(<see cref="JsonSupport"/>의 엄격한 읽기).
///
/// <b>파생값은 안 쓴다.</b> 조건의 <c>Parsed</c>·<c>IsValid</c>는 식 원문에서 다시 나온다
/// (<see cref="ChapterDocument.ToGraphModel"/>) — 담아 두면 식을 고쳤을 때 둘이 갈린다.
/// </summary>
internal static class ChapterDocumentJson
{
    internal static JsonArray? Write(IReadOnlyList<ChapterDocument> chapters)
    {
        if (chapters.Count == 0)
        {
            return null;
        }

        var array = new JsonArray();

        foreach (ChapterDocument chapter in chapters)
        {
            var json = new JsonObject { ["chapterId"] = chapter.ChapterId };

            Text(json, "displayName", chapter.DisplayName);

            Put(json, "episodes", chapter.Episodes, WriteEpisode);
            Put(json, "edges", chapter.Edges, WriteEdge);
            Put(json, "conditions", chapter.Conditions, WriteCondition);
            Put(json, "stats", chapter.Stats, WriteStat);
            Put(json, "choiceOptions", chapter.ChoiceOptions, WriteChoice);
            Put(json, "fixtures", chapter.Fixtures, WriteFixture);
            Put(json, "sceneNotes", ChapterSceneNotes.Ordered(chapter.Episodes, chapter.SceneNotes), WriteSceneNote);

            array.Add(json);
        }

        return array;
    }

    internal static List<ChapterDocument> Read(JsonNode? node)
    {
        var chapters = new List<ChapterDocument>();

        foreach (JsonObject json in JsonSupport.Objects(node, "chapters"))
        {
            chapters.Add(new ChapterDocument
            {
                ChapterId = JsonSupport.RequiredText(json["chapterId"], "챕터의 chapterId"),
                DisplayName = Omittable(json["displayName"], "챕터의 displayName"),
                Episodes = Take(json["episodes"], "episodes", ReadEpisode),
                Edges = Take(json["edges"], "edges", ReadEdge),
                Conditions = Take(json["conditions"], "conditions", ReadCondition),
                Stats = Take(json["stats"], "stats", ReadStat),
                ChoiceOptions = Take(json["choiceOptions"], "choiceOptions", ReadChoice),
                Fixtures = Take(json["fixtures"], "fixtures", ReadFixture),
                SceneNotes = ReadSceneNotes(json["sceneNotes"])
            });
        }

        return chapters;
    }

    // ── 에피소드 ────────────────────────────────────────────────────────────

    private static JsonObject WriteEpisode(ChapterEpisode episode)
    {
        var json = new JsonObject
        {
            ["episodeId"] = episode.EpisodeId,
            ["x"] = episode.X,
            ["y"] = episode.Y
        };

        Text(json, "title", episode.Title);
        Text(json, "index", episode.Index);
        Text(json, "memo", episode.Memo);
        Text(json, "eventKey", episode.EventKey);
        Text(json, "sceneId", episode.SceneId);

        if (episode.NoteCard)
        {
            json["noteCard"] = true;
        }

        return json;
    }

    private static ChapterEpisode ReadEpisode(JsonObject json) => new(
        JsonSupport.RequiredText(json["episodeId"], "에피소드의 episodeId"),
        Text(json["title"], "에피소드의 title"),
        Text(json["index"], "에피소드의 index"),
        JsonSupport.RequiredNumber(json["x"], "에피소드의 x"),
        JsonSupport.RequiredNumber(json["y"], "에피소드의 y"),
        Omittable(json["memo"], "에피소드의 memo"))
    {
        EventKey = Omittable(json["eventKey"], "에피소드의 eventKey"),
        SceneId = JsonSupport.RequiredText(json["sceneId"], "에피소드의 sceneId"),
        NoteCard = JsonSupport.Flag(json["noteCard"], "에피소드의 noteCard")
    };

    // ── 간선 ────────────────────────────────────────────────────────────────

    private static JsonObject WriteEdge(ChapterEdge edge)
    {
        var json = new JsonObject
        {
            ["from"] = edge.FromEpisodeId,
            ["to"] = edge.ToEpisodeId
        };

        Text(json, "optionLabel", edge.OptionLabel);
        Text(json, "conditionLabel", edge.ConditionLabel);
        Text(json, "visibleConditionLabel", edge.VisibleConditionLabel);
        Text(json, "lockedMessage", edge.LockedMessage);

        if (edge.Auto)
        {
            json["auto"] = true;
        }

        if (edge.StatChanges.Count > 0)
        {
            var changes = new JsonArray();

            foreach (StatDelta delta in edge.StatChanges)
            {
                var change = new JsonObject
                {
                    ["key"] = delta.Key,
                    ["amount"] = delta.Amount
                };

                // 기본은 Add — 깃발을 켜는 것만 적는다.
                if (delta.Kind != StatChangeKind.Add)
                {
                    change["kind"] = delta.Kind.ToString();
                }

                changes.Add(change);
            }

            json["statChanges"] = changes;
        }

        if (EdgeShapeJson.Write(edge.Shape) is { } shape)
        {
            json["shape"] = shape;
        }

        return json;
    }

    private static ChapterEdge ReadEdge(JsonObject json) => new(
        JsonSupport.RequiredText(json["from"], "간선의 from"),
        JsonSupport.RequiredText(json["to"], "간선의 to"),
        Omittable(json["optionLabel"], "간선의 optionLabel"),
        Omittable(json["conditionLabel"], "간선의 conditionLabel"),
        Omittable(json["lockedMessage"], "간선의 lockedMessage"))
    {
        Auto = JsonSupport.Flag(json["auto"], "간선의 auto"),
        VisibleConditionLabel = Omittable(json["visibleConditionLabel"], "간선의 visibleConditionLabel"),
        StatChanges = Take(json["statChanges"], "statChanges", change => new StatDelta(
            JsonSupport.RequiredText(change["key"], "스탯 변화의 key"),
            JsonSupport.RequiredInt(change["amount"], "스탯 변화의 amount"),
            change["kind"] is null ? StatChangeKind.Add : Kind<StatChangeKind>(change["kind"], "스탯 변화의 kind"))),
        Shape = EdgeShapeJson.Read(json["shape"])
    };

    // ── 조건·스탯·선택지·픽스처 ────────────────────────────────────────────

    private static JsonObject WriteCondition(ChapterCondition condition)
    {
        var json = new JsonObject
        {
            ["label"] = condition.Label,
            ["expression"] = condition.Expression
        };

        Text(json, "description", condition.Description);

        return json;
    }

    /// <summary><c>Parsed</c>·<c>IsValid</c>는 안 읽는다 — 식에서 다시 나온다.</summary>
    private static ChapterCondition ReadCondition(JsonObject json) => new(
        JsonSupport.RequiredString(json["label"], "조건의 label"),
        JsonSupport.RequiredString(json["expression"], "조건의 expression"),
        Omittable(json["description"], "조건의 description"),
        Parsed: [],
        IsValid: false);

    private static JsonObject WriteStat(ChapterStat stat)
    {
        var json = new JsonObject
        {
            ["key"] = stat.Key,
            ["initial"] = stat.Initial,
            ["minimum"] = stat.Minimum,
            ["maximum"] = stat.Maximum
        };

        if (stat.Type != ChapterStatType.Int)
        {
            json["type"] = stat.Type.ToString();
        }

        return json;
    }

    private static ChapterStat ReadStat(JsonObject json) => new(
        JsonSupport.RequiredText(json["key"], "스탯의 key"),
        JsonSupport.RequiredInt(json["initial"], "스탯의 initial"),
        JsonSupport.RequiredInt(json["minimum"], "스탯의 minimum"),
        JsonSupport.RequiredInt(json["maximum"], "스탯의 maximum"),
        json["type"] is null ? ChapterStatType.Int : Kind<ChapterStatType>(json["type"], "스탯의 type"));

    private static JsonObject WriteChoice(ChapterChoiceOption option)
    {
        var json = new JsonObject { ["text"] = option.Text };

        Text(json, "index", option.Index);
        Text(json, "memo", option.Memo);

        return json;
    }

    private static ChapterChoiceOption ReadChoice(JsonObject json) => new(
        Text(json["index"], "선택지의 index"),
        JsonSupport.RequiredString(json["text"], "선택지의 text"),
        Omittable(json["memo"], "선택지의 memo"));

    private static JsonObject WriteFixture(ChapterFixture fixture)
    {
        var json = new JsonObject { ["name"] = fixture.Name };

        if (fixture.IsActive)
        {
            json["active"] = true;
        }

        if (fixture.Stats.Count > 0)
        {
            var stats = new JsonObject();

            foreach ((string key, int value) in fixture.Stats.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                stats[key] = value;
            }

            json["stats"] = stats;
        }

        if (fixture.Choices.Count > 0)
        {
            var choices = new JsonArray();

            foreach (ChapterFixtureChoice choice in fixture.Choices)
            {
                choices.Add(new JsonObject { ["from"] = choice.From, ["to"] = choice.To });
            }

            json["choices"] = choices;
        }

        return json;
    }

    private static ChapterFixture ReadFixture(JsonObject json)
    {
        var stats = new Dictionary<string, int>(StringComparer.Ordinal);

        if (json["stats"] is { } statsJson)
        {
            foreach ((string key, JsonNode? value) in JsonSupport.RequiredObject(statsJson, "픽스처의 stats"))
            {
                stats[key] = JsonSupport.RequiredInt(value, $"픽스처 스탯 '{key}'의 값");
            }
        }

        return new ChapterFixture(
            JsonSupport.RequiredString(json["name"], "픽스처의 name"),
            JsonSupport.Flag(json["active"], "픽스처의 active"),
            stats,
            Take(json["choices"], "픽스처의 choices", choice => new ChapterFixtureChoice(
                JsonSupport.RequiredText(choice["from"], "픽스처 선택의 from"),
                JsonSupport.RequiredText(choice["to"], "픽스처 선택의 to"))));
    }

    // ── 장면 메모 ────────────────────────────────────────────────────────────

    private static JsonObject WriteSceneNote(ChapterSceneNote note)
    {
        var json = new JsonObject { ["sceneId"] = note.SceneId };
        Text(json, "name", string.IsNullOrWhiteSpace(note.Name) ? null : note.Name);
        Text(json, "text", string.IsNullOrWhiteSpace(note.Text) ? null : note.Text);
        return json;
    }

    /// <summary>
    /// 열쇠가 비었거나 같은 장면이 둘이면 읽지 않는다 — 어느 것이 그 장면의 메모인지 도구가 정할 수 없다(§6-8).
    /// 글도 이름도 빈 항목도 받지 않는다 — 쓸 때 빼는 항목이다.
    /// </summary>
    private static List<ChapterSceneNote> ReadSceneNotes(JsonNode? node)
    {
        var notes = new List<ChapterSceneNote>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (ChapterSceneNote note in Take(node, "sceneNotes", json => new ChapterSceneNote(
                     JsonSupport.RequiredText(json["sceneId"], "장면 메모의 sceneId"),
                     Text(json["text"], "장면 메모의 text"),
                     Omittable(json["name"], "장면 메모의 name"))))
        {
            if (!seen.Add(note.SceneId))
            {
                throw new InvalidDataException($"sceneNotes에 장면 '{note.SceneId}'의 메모가 둘 있습니다.");
            }

            if (ChapterSceneNotes.IsBlank(note))
            {
                throw new InvalidDataException($"sceneNotes의 장면 '{note.SceneId}' 항목에 글도 이름도 없습니다.");
            }

            notes.Add(note);
        }

        return notes;
    }

    // ── 잔손 ────────────────────────────────────────────────────────────────

    private static void Put<T>(JsonObject json, string name, List<T> items, Func<T, JsonObject> write)
    {
        if (items.Count == 0)
        {
            return;
        }

        var array = new JsonArray();

        foreach (T item in items)
        {
            array.Add(write(item));
        }

        json[name] = array;
    }

    private static List<T> Take<T>(JsonNode? node, string what, Func<JsonObject, T> read) =>
        JsonSupport.Objects(node, what).Select(read).ToList();

    /// <summary>비어 있으면 안 쓴다 — 기본값을 굳히지 않는다.</summary>
    private static void Text(JsonObject json, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            json[name] = value;
        }
    }

    /// <summary>비면 쓰지 않는 글 칸 — 없으면 빈 글자다.</summary>
    private static string Text(JsonNode? node, string what) => Omittable(node, what) ?? string.Empty;

    /// <summary>비면 쓰지 않는 칸 — 없으면 null, 있으면 빈 글자가 아니어야 한다(툴은 빈 글자를 쓰지 않는다).</summary>
    private static string? Omittable(JsonNode? node, string what) =>
        node is null
            ? null
            : JsonSupport.RequiredString(node, what) is { Length: > 0 } text
                ? text
                : throw new InvalidDataException($"{what}{Josa.이가(what)} 빈 글자로 적혔습니다 — 비면 칸째 쓰지 않습니다.");

    /// <summary>종류 이름 — 쓰는 이름(<c>ToString()</c>) 밖은 받지 않는다.</summary>
    private static T Kind<T>(JsonNode? node, string what) where T : struct, Enum =>
        Enum.TryParse(JsonSupport.RequiredText(node, what), out T kind) && Enum.IsDefined(kind) && kind.ToString() == (string)node!
            ? kind
            : throw new InvalidDataException($"{what} '{(string?)node}'를 모릅니다.");
}
