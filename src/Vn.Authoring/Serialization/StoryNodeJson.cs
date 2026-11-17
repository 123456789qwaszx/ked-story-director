using System.Text.Json.Nodes;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;

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
        string id = (string?)json["id"] ?? Identifier.Node();
        string name = (string?)json["name"] ?? "이름 없음";
        string kind = (string?)json["kind"] ?? "dialogue";

        StoryNode node = kind switch
        {
            "set" => ReadSetNode(json, id, name),
            "dialogue" => ReadDialogueNode(json, id, name),
            _ => throw new InvalidDataException($"지원하지 않는 노드 종류 '{kind}'입니다.")
        };

        if (json["layout"] is JsonObject layout)
        {
            node.Layout = new NodeLayout
            {
                X = (double?)layout["x"] ?? 0,
                Y = (double?)layout["y"] ?? 0,
                Width = (double?)layout["width"],
                WritingHeight = (double?)layout["writingHeight"]
            };
        }

        if (json["edgeShapes"] is JsonObject shapes)
        {
            foreach ((string key, JsonNode? value) in shapes)
            {
                if (EdgeShapeJson.Read(value) is { } shape)
                {
                    node.EdgeShapes[key] = shape;
                }
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
    /// 대사 노드는 <b>제 대본</b>(<c>script</c> — 줄 신원 · locale별 화자 · 대사)과 <b>LineId별 논리</b>를 쓴다(형식 4 ·
    /// 2026-10-29).
    /// 조건 갈래 출구는 갈래를 여는 줄의 항목 안에 함께 적어 둔다. 별도 배열로 나누면
    /// 줄이 사라졌을 때 짝 없는 출구가 파일에 남는다.
    /// </summary>
    /// <summary>
    /// 분기점 전부를 차례대로 편다 — 이어진 분기(<c>detour</c>)에 이어지지 않은 분기점의 자리(<c>openDetours</c>)를 끼운다.
    /// 범위를 벗어난 자리는 뒤로 물린다.
    /// </summary>
    private static List<string?> ReadDetourSlots(IReadOnlyList<string> targets, JsonNode? openJson)
    {
        List<int> positions = openJson switch
        {
            JsonArray array => array.Select(item => (int?)item ?? -1).Where(position => position >= 0).Distinct().Order().ToList(),
            _ => []
        };

        int total = targets.Count + positions.Count;
        var slots = new List<string?>(total);
        int target = 0;

        for (int index = 0; index < total; index++)
        {
            bool open = positions.Contains(index) || target >= targets.Count;
            slots.Add(open ? null : targets[target++]);
        }

        return slots;
    }

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
            bool hasExit = extension.Transitions.Any(transition => transition.OpensBranch) &&
                node.BranchExits.ContainsKey(extension.LineId);

            // 아무것도 담지 않은 확장은 저장하지 않는다. 파일이 조용해야 diff가 읽힌다.
            if (extension.IsEmpty && !hasExit)
            {
                continue;
            }

            var lineJson = new JsonObject { ["lineId"] = extension.LineId };

            // 흔한 경우(전환 하나)는 `condition` 한 칸으로 그대로 쓴다 — 파일이 조용해야
            // diff가 읽힌다. 둘 이상이 몰린 줄(겹쳐 닫기·연달아 열기)만 `conditions` 배열로
            // 나간다. 읽을 때는 둘 다 받는다(<see cref="Transitions"/>).
            if (extension.Transitions.Count == 1)
            {
                lineJson["condition"] = TransitionJson(extension.Transitions[0], node, extension, hasExit);
            }
            else if (extension.Transitions.Count > 1)
            {
                var array = new JsonArray();

                foreach (LineConditionTransition transition in extension.Transitions)
                {
                    array.Add(TransitionJson(transition, node, extension, hasExit));
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
            var trailing = new JsonArray();

            int opened = 0;

            foreach (LineConditionTransition transition in node.TrailingTransitions)
            {
                var transitionJson = new JsonObject
                {
                    ["kind"] = KindName(transition.Kind)
                };

                if (transition.ConditionId is not null)
                {
                    transitionJson["condition"] = transition.ConditionId;
                }

                // 앵커를 <b>글자로 적어 둔다</b> — 규칙(몇 번째인가)이 나중에 바뀌어도
                // 이미 매달린 출구가 제 갈래를 계속 찾는다. 읽는 쪽도 이 글자를 쓴다.
                if (transition.OpensBranch)
                {
                    string anchor = BranchAnchor.ForTrailing(opened++);
                    transitionJson["anchor"] = anchor;

                    if (node.BranchExits.TryGetValue(anchor, out string? exit))
                    {
                        transitionJson["exit"] = exit;
                    }
                }

                trailing.Add(transitionJson);
            }

            json["trailing"] = trailing;
        }

        // 선택지별 자유 씬 배선 (v9) — 열쇠가 대본의 줄이 아니라 <b>문구</b>다. 그래서
        // 줄 항목 안이 아니라 노드에 따로 선다(대본이 바뀌어도 이 배선은 그대로다).
    }

    private static SetNode ReadSetNode(JsonObject json, string id, string name)
    {
        var node = new SetNode(id, name);

        foreach (JsonNode? item in json["conditions"]?.AsArray() ?? new JsonArray())
        {
            if (item is JsonObject condition)
            {
                node.Conditions.Add(new ConditionDefinition((string?)condition["id"])
                {
                    Name = (string?)condition["name"] ?? string.Empty,
                    Expression = (string?)condition["expression"] ?? string.Empty
                });
            }
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
            MarkedEpisodeId = (string?)json[EpisodeMarkKey],
            IsBranchCard = (bool?)json[BranchCardKey] ?? false,
            Title = (string?)json[TitleKey]
        };
        HashSet<string> lineIds = new(StringComparer.Ordinal);

        foreach (JsonNode? item in json["lines"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject lineJson)
            {
                continue;
            }

            string lineId = (string?)lineJson["lineId"]
                ?? throw new InvalidDataException($"DialogueNode '{id}'의 줄 항목에 lineId가 없습니다.");

            if (!lineIds.Add(lineId))
            {
                throw new InvalidDataException(
                    $"DialogueNode '{id}'에서 LineId '{lineId}' 항목이 중복됩니다.");
            }

            var extension = new DialogueLineExtension(lineId);

            foreach (JsonObject transitionJson in Transitions(lineJson))
            {
                ConditionTransitionKind kind = ParseKind((string?)transitionJson["kind"], id);
                var transition = new LineConditionTransition(kind, (string?)transitionJson["condition"]);

                extension.Transitions.Add(transition);

                if ((string?)transitionJson["exit"] is { } exit && transition.OpensBranch)
                {
                    node.BranchExits[lineId] = exit;
                }
            }

            extension.DetourTargetNodeIds.AddRange(DetourJson.Read(lineJson["detour"]));
            extension.SetDetourSlots(ReadDetourSlots(extension.DetourTargetNodeIds, lineJson["openDetours"]));

            node.LineExtensions.Add(extension);
        }

        foreach (JsonNode? item in json["trailing"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject transitionJson)
            {
                continue;
            }

            ConditionTransitionKind kind = ParseKind((string?)transitionJson["kind"], id);
            var transition = new LineConditionTransition(kind, (string?)transitionJson["condition"]);

            node.TrailingTransitions.Add(transition);

            // 끝의 빈 갈래도 출구(detour)를 가질 수 있다 — 열쇠는 그 갈래의 앵커다.
            if ((string?)transitionJson["exit"] is { } exit &&
                (string?)transitionJson["anchor"] is { } anchor &&
                transition.OpensBranch)
            {
                node.BranchExits[anchor] = exit;
            }
        }

        return node;
    }

    /// <summary>전환 하나를 JSON으로. 갈래 출구는 <b>여는 전환</b>에만 붙는다.</summary>
    private static JsonObject TransitionJson(
        LineConditionTransition transition, DialogueNode node, DialogueLineExtension extension, bool hasExit)
    {
        var json = new JsonObject
        {
            ["kind"] = KindName(transition.Kind)
        };

        if (transition.ConditionId is not null)
        {
            json["condition"] = transition.ConditionId;
        }

        if (hasExit && transition.OpensBranch)
        {
            json["exit"] = node.BranchExits[extension.LineId];
        }

        return json;
    }

    /// <summary>
    /// 줄의 전환 목록 — <c>conditions</c> 배열(둘 이상)이 있으면 그것, 없으면 <c>condition</c> 한 칸(하나) — 쓰는 쪽의 두 모양이다.
    /// </summary>
    private static IEnumerable<JsonObject> Transitions(JsonObject lineJson)
    {
        if (lineJson["conditions"] is JsonArray array)
        {
            return array.OfType<JsonObject>();
        }

        return lineJson["condition"] is JsonObject single ? [single] : [];
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
    private static ConditionTransitionKind ParseKind(string? kind, string nodeId) => kind switch
    {
        "beginIf" => ConditionTransitionKind.BeginIf,
        "beginElseIf" => ConditionTransitionKind.BeginElseIf,
        "endIf" => ConditionTransitionKind.EndIf,
        _ => throw new InvalidDataException($"DialogueNode '{nodeId}'의 조건 전환 종류 '{kind}'를 모릅니다.")
    };
}
