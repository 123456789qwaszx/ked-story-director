using System.Text.Json.Nodes;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Text;

namespace Vn.Authoring.Serialization;

internal static class StoryNodeJson
{
    /// <summary>
    /// <see cref="DialogueNode.MarkedEpisodeId"/>가 앉는 칸.
    /// </summary>
    private const string EpisodeMarkKey = "episodeMark";

    /// <summary><see cref="DialogueNode.IsBranchCard"/>가 앉는 칸 — 분기 카드일 때만 쓴다.</summary>
    private const string BranchCardKey = "branchCard";

    /// <summary><see cref="DialogueNode.Title"/>(분기 카드의 보이는 제목)이 앉는 칸 — 있을 때만 쓴다.</summary>
    private const string TitleKey = "title";

    /// <summary><see cref="DialogueNode.WriterMarks"/>(작가용 글 서식)가 앉는 칸 — 서식이 있을 때만 쓴다.</summary>
    private const string WriterMarksKey = "writerMarks";

    /// <summary><see cref="DialogueNode.WriterAlign"/>이 앉는 칸 — 왼쪽(기본)이 아닐 때만 쓴다.</summary>
    private const string WriterAlignKey = "writerAlign";

    /// <summary><see cref="DialogueNode.WriterBase"/>(카드의 처음 글꼴 · 크기)가 앉는 칸 — 적혀 있을 때만 쓴다.</summary>
    private const string WriterBaseKey = "writerBase";

    /// <summary><see cref="DialogueNode.WriterGaps"/>(작가가 띄운 줄 사이)가 앉는 칸 — 띄운 줄이 있을 때만 쓴다.</summary>
    private const string WriterGapsKey = "writerGaps";

    public static JsonObject Write(StoryNode node)
    {
        string kind = node switch
        {
            SetNode => "set",
            DialogueNode => "dialogue",
            _ => throw new InvalidDataException($"지원하지 않는 노드 종류 '{node.GetType().Name}'입니다.")
        };

        var json = new JsonObject
        {
            ["id"] = node.Id,
            ["kind"] = kind,
            ["name"] = node.Name,
            ["layout"] = new JsonObject
            {
                ["x"] = Math.Round(node.Layout.X, 2),
                ["y"] = Math.Round(node.Layout.Y, 2)
            }
        };

        // 폭 · 글 칸 높이는 사람이 바꾼 카드에만 적는다 — 기본 크기 카드의 파일은 그대로다.
        if (node.Layout.Width is { } width)
        {
            ((JsonObject)json["layout"]!)["width"] = Math.Round(width, 2);
        }

        if (node.Layout.WritingHeight is { } writingHeight)
        {
            ((JsonObject)json["layout"]!)["writingHeight"] = Math.Round(writingHeight, 2);
        }

        // 선 모양(G3) — "lines"는 대사 노드의 LineId별 논리가 이미 쓰는 칸이라 이름이 다르다.
        var shapes = new JsonObject();

        foreach ((string key, EdgeShape shape) in node.EdgeShapes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (EdgeShapeJson.Write(shape) is { } written)
            {
                shapes[key] = written;
            }
        }

        if (shapes.Count > 0)
        {
            json["edgeShapes"] = shapes;
        }

        switch (node)
        {
            case SetNode setNode:
                WriteSetNode(setNode, json);
                break;
            case DialogueNode dialogue:
                WriteDialogueNode(dialogue, json);
                break;
        }

        return json;
    }

    /// <summary>노드 하나를 읽는다.</summary>
    public static StoryNode Read(JsonObject json)
    {
        // 쓰는 모양만 받는다(2026-11-18 · old-shapes O8) — ⛔ 그 전에는 빠진 Id에 새 Id를, 이름에 「이름 없음」을, 종류에 대사를,
        //    자리에 0을 넣고 모르는 선 모양은 건너뛰었다.
        string id = JsonSupport.RequiredText(json["id"], "노드의 id");
        string name = JsonSupport.RequiredText(json["name"], $"노드 '{id}'의 name");
        string kind = JsonSupport.RequiredText(json["kind"], $"노드 '{id}'의 kind");

        StoryNode node = kind switch
        {
            "set" => ReadSetNode(json, id, name),
            "dialogue" => ReadDialogueNode(json, id, name),
            _ => throw new InvalidDataException($"지원하지 않는 노드 종류 '{kind}'입니다.")
        };

        JsonObject layout = JsonSupport.RequiredObject(json["layout"], $"노드 '{id}'의 layout");
        node.Layout = new NodeLayout
        {
            X = JsonSupport.RequiredNumber(layout["x"], $"노드 '{id}'의 layout.x"),
            Y = JsonSupport.RequiredNumber(layout["y"], $"노드 '{id}'의 layout.y"),
            Width = JsonSupport.OptionalNumber(layout["width"], $"노드 '{id}'의 layout.width"),
            WritingHeight = JsonSupport.OptionalNumber(layout["writingHeight"], $"노드 '{id}'의 layout.writingHeight")
        };

        if (json["edgeShapes"] is { } shapes)
        {
            foreach ((string key, JsonNode? value) in JsonSupport.RequiredObject(shapes, $"노드 '{id}'의 edgeShapes"))
            {
                node.EdgeShapes[key] = EdgeShapeJson.Read(value)
                    ?? throw new InvalidDataException($"노드 '{id}'의 선 모양 '{key}'가 비었습니다.");
            }
        }

        return node;
    }

    private static void WriteSetNode(SetNode node, JsonObject json)
    {
        if (node.Conditions.Count > 0)
        {
            var conditions = new JsonArray();
            foreach (ConditionDefinition condition in node.Conditions)
            {
                conditions.Add(new JsonObject
                {
                    ["id"] = condition.Id,
                    ["name"] = condition.Name,
                    ["expression"] = condition.Expression
                });
            }
            json["conditions"] = conditions;
        }
    }

    /// <summary>
    /// 분기점 전부를 차례대로 편다 — 이어진 분기(<c>detour</c>)에 이어지지 않은 분기점의 자리(<c>openDetours</c>)를 끼운다. 자리는 쓰는
    /// 모양(오름차순 · 겹침 없음 · 분기점 수 안)만 받는다(2026-11-18 · old-shapes O8 — ⛔ 그 전에는 어긋난 자리를 버리거나 뒤로 물렸다).
    /// </summary>
    private static List<string?> ReadDetourSlots(IReadOnlyList<string> targets, JsonNode? openJson, string owner)
    {
        List<int> positions = openJson switch
        {
            null => [],
            JsonArray { Count: > 0 } array => array.Select(item => JsonSupport.RequiredInt(item, $"{owner}의 openDetours 자리")).ToList(),
            _ => throw new InvalidDataException($"{owner}의 openDetours는 자리 번호의 배열입니다.")
        };

        int total = targets.Count + positions.Count;

        for (int index = 0; index < positions.Count; index++)
        {
            if (positions[index] < 0 || positions[index] >= total || (index > 0 && positions[index] <= positions[index - 1]))
            {
                throw new InvalidDataException($"{owner}의 openDetours 자리가 어긋났습니다(분기점 {total}개 안에서 오름차순이어야 합니다).");
            }
        }

        var slots = new List<string?>(total);
        int target = 0;

        for (int index = 0; index < total; index++)
        {
            slots.Add(positions.Contains(index) ? null : targets[target++]);
        }

        return slots;
    }

    /// <summary>
    /// 대사 노드는 <b>제 대본</b>(<c>script</c> — 줄 신원 · locale별 화자 · 대사)과 <b>LineId별 논리</b>를 쓴다(형식 4 ·
    /// 2026-10-29).
    /// 조건 갈래 출구는 갈래를 여는 줄의 항목 안에 함께 적어 둔다. 별도 배열로 나누면
    /// 줄이 사라졌을 때 짝 없는 출구가 파일에 남는다.
    /// </summary>
    private static void WriteDialogueNode(DialogueNode node, JsonObject json)
    {
        json["script"] = ScriptDocumentJson.WriteBody(node.Script);

        if (node.MarkedEpisodeId is not null)
        {
            json[EpisodeMarkKey] = node.MarkedEpisodeId;
        }

        // 분기 카드만 적는다(2026-11-17) — 에피소드 카드에는 칸이 없다. 노드 종류(`kind`)는 그대로 `dialogue`다(다른 저장소가 이 파일을 읽는다).
        if (node.IsBranchCard)
        {
            json[BranchCardKey] = true;
        }

        if (node.Title is { Length: > 0 } title)
        {
            json[TitleKey] = title;
        }

        var lines = new JsonArray();

        foreach (DialogueLineExtension extension in node.LineExtensions)
        {
            // 아무것도 담지 않은 확장은 저장하지 않는다. 파일이 조용해야 diff가 읽힌다.
            if (extension.IsEmpty)
            {
                continue;
            }

            var lineJson = new JsonObject { ["lineId"] = extension.LineId };

            // 흔한 경우(전환 하나)는 `condition` 한 칸으로 그대로 쓴다 — 파일이 조용해야
            // diff가 읽힌다. 둘 이상이 몰린 줄(겹쳐 닫기·연달아 열기)만 `conditions` 배열로
            // 나간다. 읽을 때는 둘 다 받는다(<see cref="Transitions"/>).
            if (extension.Transitions.Count == 1)
            {
                lineJson["condition"] = TransitionJson(extension.Transitions[0]);
            }
            else if (extension.Transitions.Count > 1)
            {
                var array = new JsonArray();

                foreach (LineConditionTransition transition in extension.Transitions)
                {
                    array.Add(TransitionJson(transition));
                }

                lineJson["conditions"] = array;
            }

            // 「조건 분기」 표식 (R7 P-5) — 이 줄 앞에서 차례로 다녀올 노드들. 조건이 아니다.
            // 하나면 문자열(그전과 같은 바이트), 여럿이면 배열이다(DetourJson).
            if (DetourJson.Write(extension.DetourTargetNodeIds) is { } detours)
            {
                lineJson["detour"] = detours;
            }

            // 연결만 끊은 분기점의 자리(그 줄 분기점 전체에서 몇째) — 있을 때만 적는다. 다녀갈 곳이 없으니 `detour`와 따로
            // 둔다(읽는 쪽이 모르면 그냥 건너뛴다).
            if (extension.OpenDetourPositions.Count > 0)
            {
                lineJson["openDetours"] = new JsonArray(
                    extension.OpenDetourPositions.Select(position => (JsonNode?)JsonValue.Create(position)).ToArray());
            }

            lines.Add(lineJson);
        }

        json["lines"] = lines;

        // 마지막 줄 뒤의 전환 (2026-08-24) — 대사 없는 조건 블록이 대본의 끝에 있을 때.
        // 줄이 아니라 노드에 붙으므로 `lines` 밖에 선다. 없으면 칸도 안 만든다.
        if (node.TrailingTransitions.Count > 0)
        {
            // ⛔ 여는 전환에 적던 `anchor`(갈래 신원)와 `exit`(갈래 출구)는 2026-11-19에 걷혔다(usability UX28) — 그 전의 파일에 있으면
            //    모르는 칸으로 지나가고 다음 저장에서 빠진다(기본 출구 `defaultExit`와 같은 처지).
            var trailing = new JsonArray();

            foreach (LineConditionTransition transition in node.TrailingTransitions)
            {
                trailing.Add(TransitionJson(transition));
            }

            json["trailing"] = trailing;
        }

        // 작가용 글 서식(2026-11-22) — 있을 때만 적는다. 서식 없는 프로젝트의 파일은 그대로다.
        if (WriterMarksJson.Write(node) is { } marks)
        {
            json[WriterMarksKey] = marks;
        }

        if (node.WriterAlign != WriterAlign.Left)
        {
            json[WriterAlignKey] = WriterMarksJson.AlignName(node.WriterAlign);
        }

        if (node.WriterBase is { } writerBase)
        {
            json[WriterBaseKey] = WriterMarksJson.WriteBase(writerBase);
        }

        // 작가가 띄운 줄 사이(2026-11-25) — 있을 때만 적는다. 띄운 줄이 없는 카드의 파일은 그대로다.
        if (WriterMarksJson.WriteGaps(node) is { } gaps)
        {
            json[WriterGapsKey] = gaps;
        }
    }

    private static SetNode ReadSetNode(JsonObject json, string id, string name)
    {
        var node = new SetNode(id, name);

        foreach (JsonObject condition in JsonSupport.Objects(json["conditions"], $"설정 노드 '{id}'의 conditions"))
        {
            node.Conditions.Add(new ConditionDefinition(JsonSupport.RequiredText(condition["id"], $"설정 노드 '{id}'의 조건 id"))
            {
                Name = JsonSupport.RequiredString(condition["name"], $"설정 노드 '{id}'의 조건 name"),
                Expression = JsonSupport.RequiredString(condition["expression"], $"설정 노드 '{id}'의 조건 expression")
            });
        }

        return node;
    }

    /// <summary>카드의 대본 — 카드 JSON의 <c>script</c> 칸. 늘 쓰이므로 없으면 깨진 파일이다.</summary>
    private static Script.ScriptDocument ReadScript(JsonNode? script, string nodeId) =>
        script is JsonObject body
            ? ScriptDocumentJson.ReadBody(body, nodeId)
            : throw new InvalidDataException($"DialogueNode '{nodeId}'에 대본(script)이 없습니다.");

    private static DialogueNode ReadDialogueNode(JsonObject json, string id, string name)
    {
        var node = new DialogueNode(id, name)
        {
            Script = ReadScript(json["script"], id),
            MarkedEpisodeId = JsonSupport.OptionalString(json[EpisodeMarkKey], $"DialogueNode '{id}'의 {EpisodeMarkKey}"),
            IsBranchCard = JsonSupport.Flag(json[BranchCardKey], $"DialogueNode '{id}'의 {BranchCardKey}"),
            Title = JsonSupport.OptionalString(json[TitleKey], $"DialogueNode '{id}'의 {TitleKey}")
        };

        HashSet<string> lineIds = new(StringComparer.Ordinal);

        foreach (JsonObject lineJson in JsonSupport.RequiredObjects(json["lines"], $"DialogueNode '{id}'의 lines"))
        {
            string lineId = JsonSupport.RequiredText(lineJson["lineId"], $"DialogueNode '{id}'의 줄 항목의 lineId");

            if (!lineIds.Add(lineId))
            {
                throw new InvalidDataException(
                    $"DialogueNode '{id}'에서 LineId '{lineId}' 항목이 중복됩니다.");
            }

            var extension = new DialogueLineExtension(lineId);

            foreach (JsonObject transitionJson in Transitions(lineJson, id))
            {
                extension.Transitions.Add(ReadTransition(transitionJson, id));
            }

            extension.DetourTargetNodeIds.AddRange(DetourJson.Read(lineJson["detour"]));
            extension.SetDetourSlots(ReadDetourSlots(extension.DetourTargetNodeIds, lineJson["openDetours"], $"DialogueNode '{id}'의 줄 '{lineId}'"));

            node.LineExtensions.Add(extension);
        }

        foreach (JsonObject transitionJson in JsonSupport.Objects(json["trailing"], $"DialogueNode '{id}'의 trailing"))
        {
            node.TrailingTransitions.Add(ReadTransition(transitionJson, id));
        }

        // 작가용 글 서식은 너그럽게 읽는다 — 서식 때문에 프로젝트가 안 열리는 일은 없다(writer-formatting 5절 ③).
        WriterMarksJson.Read(node, json[WriterMarksKey]);
        node.WriterAlign = WriterMarksJson.ReadAlign(json[WriterAlignKey]);
        node.WriterBase = WriterMarksJson.ReadBase(json[WriterBaseKey]);
        WriterMarksJson.ReadGaps(node, json[WriterGapsKey]);

        return node;
    }

    /// <summary>전환 하나 — 종류(<see cref="ParseKind"/>)와 조건 Id(없으면 닫기 · 그 밖).</summary>
    private static LineConditionTransition ReadTransition(JsonObject json, string nodeId) => new(
        ParseKind(JsonSupport.RequiredText(json["kind"], $"DialogueNode '{nodeId}'의 전환 kind"), nodeId),
        JsonSupport.OptionalString(json["condition"], $"DialogueNode '{nodeId}'의 전환 condition"));

    /// <summary>전환 하나를 JSON으로 — 종류와 조건 Id.</summary>
    private static JsonObject TransitionJson(LineConditionTransition transition)
    {
        var json = new JsonObject
        {
            ["kind"] = KindName(transition.Kind)
        };

        if (transition.ConditionId is not null)
        {
            json["condition"] = transition.ConditionId;
        }

        return json;
    }

    /// <summary>
    /// 줄의 전환 목록 — <c>conditions</c> 배열(둘 이상)이 있으면 그것, 없으면 <c>condition</c> 한 칸(하나) — 쓰는 쪽의 두 모양이다. 둘 다
    /// 있거나 하나짜리 배열 · 객체 아닌 칸은 받지 않는다.
    /// </summary>
    private static IEnumerable<JsonObject> Transitions(JsonObject lineJson, string nodeId)
    {
        string what = $"DialogueNode '{nodeId}'의 줄 '{(string?)lineJson["lineId"]}'의 전환";

        return (lineJson["conditions"], lineJson["condition"]) switch
        {
            (null, null) => [],
            (null, { } single) => [JsonSupport.RequiredObject(single, what)],
            (JsonArray { Count: > 1 } array, null) => JsonSupport.Objects(array, what),
            _ => throw new InvalidDataException($"{what}{Josa.은는(what)} condition 하나이거나 conditions 둘 이상입니다.")
        };
    }

    private static string KindName(ConditionTransitionKind kind) => kind switch
    {
        ConditionTransitionKind.BeginIf => "beginIf",
        ConditionTransitionKind.BeginElseIf => "beginElseIf",
        _ => "endIf"
    };

    /// <summary>
    /// 종류 이름 → 종류. <see cref="KindName"/>이 쓰는 셋 밖은 <b>깨진 파일</b>이다(2026-11-17) — ⛔ 전에는 모르는 이름을 <c>endIf</c>로
    /// 돌리고 걷힌 대사 안 선택지(<c>beginChoice</c> · <c>beginNextOption</c> · <c>endChoice</c>)는 알아보고 버렸다.
    /// </summary>
    private static ConditionTransitionKind ParseKind(string kind, string nodeId) => kind switch
    {
        "beginIf" => ConditionTransitionKind.BeginIf,
        "beginElseIf" => ConditionTransitionKind.BeginElseIf,
        "endIf" => ConditionTransitionKind.EndIf,
        _ => throw new InvalidDataException($"DialogueNode '{nodeId}'의 조건 전환 종류 '{kind}'를 모릅니다.")
    };
}
