using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Text;
using Vn.Authoring.Strings;

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
                ChapterId = JsonSupport.RequiredText(json["chapterId"], Msg.File_ChapterIdOfChapter),
                DisplayName = Omittable(json["displayName"], Msg.File_DisplayNameOfChapter),
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
        JsonSupport.RequiredText(json["episodeId"], Msg.File_EpisodeIdOfEpisode),
        Text(json["title"], Msg.File_TitleOfEpisode),
        Text(json["index"], Msg.File_IndexOfEpisode),
        JsonSupport.RequiredNumber(json["x"], Msg.File_XOfEpisode),
        JsonSupport.RequiredNumber(json["y"], Msg.File_YOfEpisode),
        Omittable(json["memo"], Msg.File_MemoOfEpisode))
    {
        EventKey = Omittable(json["eventKey"], Msg.File_EventKeyOfEpisode),
        SceneId = JsonSupport.RequiredText(json["sceneId"], Msg.File_SceneIdOfEpisode),
        NoteCard = JsonSupport.Flag(json["noteCard"], Msg.File_NoteCardOfEpisode)
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
        JsonSupport.RequiredText(json["from"], Msg.File_FromOfPath),
        JsonSupport.RequiredText(json["to"], Msg.File_ToOfPath),
        Omittable(json["optionLabel"], Msg.File_OptionLabelOfPath),
        Omittable(json["conditionLabel"], Msg.File_ConditionLabelOfPath),
        Omittable(json["lockedMessage"], Msg.File_LockedMessageOfPath))
    {
        Auto = JsonSupport.Flag(json["auto"], Msg.File_AutoOfPath),
        VisibleConditionLabel = Omittable(json["visibleConditionLabel"], Msg.File_VisibleConditionLabelOfPath),
        StatChanges = Take(json["statChanges"], "statChanges", change => new StatDelta(
            JsonSupport.RequiredText(change["key"], Msg.File_KeyOfStatChange),
            JsonSupport.RequiredInt(change["amount"], Msg.File_AmountOfStatChange),
            change["kind"] is null ? StatChangeKind.Add : Kind<StatChangeKind>(change["kind"], Msg.File_KindOfStatChange))),
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
        JsonSupport.RequiredString(json["label"], Msg.File_LabelOfCondition),
        JsonSupport.RequiredString(json["expression"], Msg.File_ExpressionOfCondition),
        Omittable(json["description"], Msg.File_DescriptionOfCondition),
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
        JsonSupport.RequiredText(json["key"], Msg.File_KeyOfStat),
        JsonSupport.RequiredInt(json["initial"], Msg.File_InitialOfStat),
        JsonSupport.RequiredInt(json["minimum"], Msg.File_MinimumOfStat),
        JsonSupport.RequiredInt(json["maximum"], Msg.File_MaximumOfStat),
        json["type"] is null ? ChapterStatType.Int : Kind<ChapterStatType>(json["type"], Msg.File_TypeOfStat));

    private static JsonObject WriteChoice(ChapterChoiceOption option)
    {
        var json = new JsonObject { ["text"] = option.Text };

        Text(json, "index", option.Index);
        Text(json, "memo", option.Memo);

        return json;
    }

    private static ChapterChoiceOption ReadChoice(JsonObject json) => new(
        Text(json["index"], Msg.File_IndexOfChoice),
        JsonSupport.RequiredString(json["text"], Msg.File_TextOfChoice),
        Omittable(json["memo"], Msg.File_MemoOfChoice));

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
            foreach ((string key, JsonNode? value) in JsonSupport.RequiredObject(statsJson, Msg.File_StatsOfTestStart))
            {
                stats[key] = JsonSupport.RequiredInt(value, string.Format(Msg.File_ValueOfTestStartStat, key));
            }
        }

        return new ChapterFixture(
            JsonSupport.RequiredString(json["name"], Msg.File_NameOfTestStart),
            JsonSupport.Flag(json["active"], Msg.File_ActiveOfTestStart),
            stats,
            Take(json["choices"], Msg.File_ChoicesOfTestStart, choice => new ChapterFixtureChoice(
                JsonSupport.RequiredText(choice["from"], Msg.File_FromOfTestStartChoice),
                JsonSupport.RequiredText(choice["to"], Msg.File_ToOfTestStartChoice))));
    }

    // ── 장면 메모 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 장면 곁 자료의 색 — 팔레트 칸은 <c>color</c>(1 ~ 5), RGB로 고른 색은 <c>rgb</c>(<c>"#RRGGBB"</c> · 2026-11-27). 둘이 함께면 어느 것인지 도구가 정할 수
    /// 없어 읽지 않는다. 없으면 자동(null).
    /// </summary>
    private static int? ReadSceneColor(JsonObject json)
    {
        string sceneId = JsonSupport.RequiredText(json["sceneId"], Msg.File_SceneIdOfSceneNote);
        int? slot = json["color"] is { } color ? JsonSupport.RequiredInt(color, Msg.File_ColorOfSceneNote) : null;
        string? rgb = json["rgb"] is { } text ? JsonSupport.RequiredText(text, Msg.File_RgbOfSceneNote) : null;

        if (slot is not null && rgb is not null)
        {
            throw new InvalidDataException(string.Format(Msg.File_SceneColorAndRgb, sceneId));
        }

        if (slot is int picked && !SceneColors.IsSlot(picked))
        {
            throw new InvalidDataException(string.Format(Msg.File_SceneColorOutOfRange, sceneId, picked, SceneColors.Count));
        }

        if (rgb is not null)
        {
            return SceneColors.TryParseHex(rgb, out int custom) && rgb.StartsWith('#')
                ? custom
                : throw new InvalidDataException(string.Format(Msg.File_SceneRgbInvalid, sceneId, rgb));
        }

        return slot;
    }

    private static JsonObject WriteSceneNote(ChapterSceneNote note)
    {
        var json = new JsonObject { ["sceneId"] = note.SceneId };
        Text(json, "name", string.IsNullOrWhiteSpace(note.Name) ? null : note.Name);
        Text(json, "text", string.IsNullOrWhiteSpace(note.Text) ? null : note.Text);

        // 고른 장면 색 — 팔레트 칸이면 color(1 ~ 5), RGB로 고른 색이면 rgb("#RRGGBB" · 2026-11-27). 자동이면 적지 않는다(design/scene-colors.md 5.1).
        // rgb는 따로 적는다 — 옛 리더는 모르는 칸을 읽지 않으니 그 장면을 자동 색으로 연다(color에 넣으면 1 ~ 5 밖이라 열지 못한다).
        if (note.Color is int color)
        {
            if (SceneColors.IsCustom(color))
            {
                json["rgb"] = SceneColors.HexOf(color);
            }
            else
            {
                json["color"] = color;
            }
        }

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
                     JsonSupport.RequiredText(json["sceneId"], Msg.File_SceneIdOfSceneNote),
                     Text(json["text"], Msg.File_TextOfSceneNote),
                     Omittable(json["name"], Msg.File_NameOfSceneNote),
                     ReadSceneColor(json))))
        {
            if (!seen.Add(note.SceneId))
            {
                throw new InvalidDataException(string.Format(Msg.File_SceneNotesHasTwoNotesFor, note.SceneId));
            }

            if (ChapterSceneNotes.IsBlank(note))
            {
                throw new InvalidDataException(string.Format(Msg.File_SceneNotesEntryForSceneHas, note.SceneId));
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
                : throw new InvalidDataException(string.Format(Msg.File_IsWrittenAsEmptyString, what, Josa.이가(what)));

    /// <summary>종류 이름 — 쓰는 이름(<c>ToString()</c>) 밖은 받지 않는다.</summary>
    private static T Kind<T>(JsonNode? node, string what) where T : struct, Enum =>
        Enum.TryParse(JsonSupport.RequiredText(node, what), out T kind) && Enum.IsDefined(kind) && kind.ToString() == (string)node!
            ? kind
            : throw new InvalidDataException(string.Format(Msg.File_Unknown, what, (string?)node));
}
