using System.Text.Json.Nodes;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;

namespace Vn.Authoring.Serialization;

internal static class StoryNodeJson
{
    /// <summary>
    /// <see cref="DialogueNode.MarkedEpisodeId"/>가 앉는 칸. 2026-09-17에 <c>excelEpisode</c>에서
    /// 바뀌었다 — 엑셀은 이 값의 주인이 아니게 된 지 오래다(R-F).
    ///
    /// ⛔ 옛 이름 <c>excelEpisode</c>도 한동안 계속 읽었다(저장된 신원이라 못 읽으면 되살릴 길이 없다).
    /// 2026-10-29(L6)에 그 갈래를 걷었다 — 2026-09-17 소유자: *"이전의 프로젝트들은 모두 버리고"*.
    /// 그 뒤로 한 번이라도 저장한 프로젝트는 이미 새 이름으로 옮겨 앉아 있다.
    /// </summary>
    private const string EpisodeMarkKey = "episodeMark";

    /// <summary>
    /// 연출 층의 노드 종류 — 연출 노드 · 명령 공급 노드. 2026-10-29에 <c>ked-story-compositor</c>로 갔다.
    /// 옛 파일에 있으면 읽지 않는다(<see cref="Read"/>가 null) — 원본은 <see cref="ProjectStore"/>가 남긴다.
    /// </summary>
    public static IReadOnlyList<string> PresentationKinds { get; } = ["presentation", "commandSupply"];

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

        if (node.DefaultExitTargetNodeId is not null)
        {
            json["defaultExit"] = node.DefaultExitTargetNodeId;
        }

        // 선 모양(G3) — ⚠ "lines"는 대사 노드의 LineId별 논리가 이미 쓰는 칸이라 이름이 다르다.
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

    /// <summary>노드 하나를 읽는다. 연출 층의 노드(<see cref="PresentationKinds"/>)면 null이다.</summary>
    public static StoryNode? Read(JsonObject json, ScriptTable? scripts = null)
    {
        string id = (string?)json["id"] ?? Identifier.Node();
        string name = (string?)json["name"] ?? "이름 없음";
        string kind = (string?)json["kind"] ?? "dialogue";

        if (PresentationKinds.Contains(kind))
        {
            return null;
        }

        StoryNode node = kind switch
        {
            "set" => ReadSetNode(json, id, name),
            "dialogue" => ReadDialogueNode(json, id, name, scripts),
            _ => throw new InvalidDataException($"지원하지 않는 노드 종류 '{kind}'입니다.")
        };

        node.DefaultExitTargetNodeId = (string?)json["defaultExit"];

        if (json["layout"] is JsonObject layout)
        {
            node.Layout = new NodeLayout
            {
                X = (double?)layout["x"] ?? 0,
                Y = (double?)layout["y"] ?? 0
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
    /// 2026-10-29). ⛔ 형식 3까지는 대본을 따로 선 파일에 두고 여기에는 그 Id만 적었다.
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
            // 나간다. 읽을 때는 둘 다 받으므로 옛 판이 그대로 열린다.
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

            // 「조건 분기」 표식 (R7 P-5) — 이 줄에서 다녀올 노드. 조건이 아니다.
            if (extension.DetourTargetNodeId is { Length: > 0 } detour)
            {
                lineJson["detour"] = detour;
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

                if (transition.OptionId is not null)
                {
                    transitionJson["option"] = transition.OptionId;
                }

                // ⚠ 앵커를 <b>글자로 적어 둔다</b> — 규칙(몇 번째인가)이 나중에 바뀌어도
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
        if (node.ChoiceExits.Count > 0)
        {
            var choiceExits = new JsonObject();

            foreach ((string choice, string target) in node.ChoiceExits.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                choiceExits[choice] = target;
            }

            json["choiceExits"] = choiceExits;
        }

        // ⛔ `excelLines`(인덱스 → LineId)는 2026-09-16에 더 내보내지 않는다 (R-D).
        //    구판 파일에 남아 있어도 읽지 않고 지나간다 — 뜻이 없는 값이다.
    }

    private static SetNode ReadSetNode(JsonObject json, string id, string name)
    {
        var node = new SetNode(id, name);

        // ⛔ `assignments`는 2026-09-17에 폐지됐다 (작가 변수 폐지) — 옛 프로젝트 파일에
        //    남아 있어도 <b>안 읽는다</b>. 소유자가 옛 프로젝트를 버리기로 했으므로
        //    되살릴 자리가 없다.

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

    /// <summary>
    /// 카드의 대본 — 형식 4면 그 자리에 든 내용, 형식 3이면 가리킨 Id로 표(<see cref="ScriptTable"/>)에서 받는다.
    /// 대본 없이 저장된 옛 카드는 빈 대본을 받는다.
    /// </summary>
    private static Script.ScriptDocument ReadScript(JsonNode? script, string nodeId, ScriptTable? scripts)
    {
        switch (script)
        {
            case JsonObject body:
                return ScriptDocumentJson.ReadBody(body, nodeId);

            case JsonValue reference when reference.TryGetValue(out string? scriptId):
                return scripts?.For(nodeId, scriptId)
                    ?? throw new InvalidDataException(
                        $"DialogueNode '{nodeId}'가 따로 선 대본 '{scriptId}'를 가리킵니다 — 형식 3 판은 프로젝트와 함께 읽어야 합니다.");

            default:
                var empty = new Script.ScriptDocument();
                empty.RequireLocale(empty.PrimaryLocale);
                return empty;
        }
    }

    private static DialogueNode ReadDialogueNode(JsonObject json, string id, string name, ScriptTable? scripts)
    {
        var node = new DialogueNode(id, name)
        {
            Script = ReadScript(json["script"], id, scripts),
            MarkedEpisodeId = (string?)json[EpisodeMarkKey]
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
                var transition = new LineConditionTransition(
                    ParseKind((string?)transitionJson["kind"]),
                    (string?)transitionJson["condition"],
                    (string?)transitionJson["option"]);

                extension.Transitions.Add(transition);

                if ((string?)transitionJson["exit"] is { } exit && transition.OpensBranch)
                {
                    node.BranchExits[lineId] = exit;
                }
            }

            // ⛔ 줄의 `set`도 2026-09-17에 폐지됐다 — 옛 파일에 남아 있어도 안 읽는다.

            if ((string?)lineJson["detour"] is { Length: > 0 } detour)
            {
                extension.DetourTargetNodeId = detour;
            }

            node.LineExtensions.Add(extension);
        }

        foreach (JsonNode? item in json["trailing"]?.AsArray() ?? new JsonArray())
        {
            if (item is not JsonObject transitionJson)
            {
                continue;
            }

            var transition = new LineConditionTransition(
                ParseKind((string?)transitionJson["kind"]),
                (string?)transitionJson["condition"],
                (string?)transitionJson["option"]);

            node.TrailingTransitions.Add(transition);

            // 끝의 빈 갈래도 출구(detour)를 가질 수 있다 — 열쇠는 그 갈래의 앵커다.
            if ((string?)transitionJson["exit"] is { } exit &&
                (string?)transitionJson["anchor"] is { } anchor &&
                transition.OpensBranch)
            {
                node.BranchExits[anchor] = exit;
            }
        }

        if (json["choiceExits"] is JsonObject choiceExits)
        {
            foreach ((string choice, JsonNode? value) in choiceExits)
            {
                node.ChoiceExits[choice] = (string?)value
                    ?? throw new InvalidDataException(
                        $"DialogueNode '{id}'의 choiceExits['{choice}']에 대상 노드가 없습니다.");
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

        if (transition.OptionId is not null)
        {
            json["option"] = transition.OptionId;
        }

        if (hasExit && transition.OpensBranch)
        {
            json["exit"] = node.BranchExits[extension.LineId];
        }

        return json;
    }

    /// <summary>
    /// 줄의 전환 목록 — 새 <c>conditions</c> 배열이 있으면 그것, 없으면 옛 <c>condition</c>
    /// 한 칸. 전환이 하나뿐이던 시절의 파일이 그대로 열린다.
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
        ConditionTransitionKind.BeginChoice => "beginChoice",
        ConditionTransitionKind.BeginNextOption => "beginNextOption",
        ConditionTransitionKind.EndChoice => "endChoice",
        _ => "endIf"
    };

    private static ConditionTransitionKind ParseKind(string? kind) => kind switch
    {
        "beginIf" => ConditionTransitionKind.BeginIf,
        "beginElseIf" => ConditionTransitionKind.BeginElseIf,
        "beginChoice" => ConditionTransitionKind.BeginChoice,
        "beginNextOption" => ConditionTransitionKind.BeginNextOption,
        "endChoice" => ConditionTransitionKind.EndChoice,
        _ => ConditionTransitionKind.EndIf
    };
}
