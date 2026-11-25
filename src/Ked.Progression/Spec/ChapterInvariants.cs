using System;
using System.Collections.Generic;

namespace Ked.Progression
{
    // 전 챕터 공통 규칙:
    // [1]ID는 유일.
    // [2]참조는 반드시 실재.
    // [3]데이터 표현의 자유는 제한.
    // [4]런타임 암묵적 결정 제거.
    
    // ChapterDefinition, ProgressionLoader가 사용.
    // - 챕터 그래프와 스탯 간 모순을 찾아서 차단.
    internal static class ChapterInvariants
    {
        public static void Collect(
            IReadOnlyList<StatDefinition> stats,
            IReadOnlyList<EpisodeNode> nodes,
            string startEpisodeId,
            ICollection<ProgressionDiagnostic> into,
            out Dictionary<string, StatDefinition> statsByKey,
            out Dictionary<string, EpisodeNode> nodesById)
        {
            statsByKey = IndexStats(stats, into);
            nodesById = IndexNodes(nodes, into);

            VerifyStart(startEpisodeId, nodesById, into);
            VerifyEdges(nodes, nodesById, statsByKey, into);
            VerifySceneEntries(nodes, nodesById, startEpisodeId, into);
        }

        private static Dictionary<string, StatDefinition> IndexStats(
            IReadOnlyList<StatDefinition> stats, 
            ICollection<ProgressionDiagnostic> into)
        {
            var byKey = new Dictionary<string, StatDefinition>(StringComparer.Ordinal);

            for (int i = 0; i < stats.Count; i++)
            {
                StatDefinition stat = stats[i];

                if (stat == null)
                {
                    into.Add(ProgressionDiagnostic.Error($"Stats[{i}]", "A stat definition is null."));
                    continue;
                }

                if (byKey.ContainsKey(stat.Key))
                {
                    // 뒤엣것이 이기면 초기값/경계가 조용히 갈린다.
                    into.Add(ProgressionDiagnostic.Error(
                        $"Stats[{i}]", $"Stat key '{stat.Key}' is defined more than once."));
                    continue;
                }

                byKey[stat.Key] = stat;
            }

            return byKey;
        }

        private static Dictionary<string, EpisodeNode> IndexNodes(
            IReadOnlyList<EpisodeNode> nodes, 
            ICollection<ProgressionDiagnostic> into)
        {
            var byId = new Dictionary<string, EpisodeNode>(StringComparer.Ordinal);

            for (int i = 0; i < nodes.Count; i++)
            {
                EpisodeNode node = nodes[i];

                if (node == null)
                {
                    into.Add(ProgressionDiagnostic.Error($"Nodes[{i}]", "An episode is null."));
                    continue;
                }

                if (string.IsNullOrEmpty(node.EpisodeId))
                {
                    into.Add(ProgressionDiagnostic.Error($"Nodes[{i}]", "An episode ID is empty."));
                    continue;
                }

                if (byId.ContainsKey(node.EpisodeId))
                {
                    // 같은 ID가 둘이면 간선이 어느 쪽으로 가는지가 사전 구현에 달린다.
                    into.Add(ProgressionDiagnostic.Error(
                        $"Nodes[{i}]", $"Episode ID '{node.EpisodeId}' is duplicated."));
                    continue;
                }

                byId[node.EpisodeId] = node;
            }

            return byId;
        }

        private static void VerifyStart(
            string startEpisodeId,
            Dictionary<string, EpisodeNode> nodesById,
            ICollection<ProgressionDiagnostic> into)
        {
            if (!string.IsNullOrEmpty(startEpisodeId) && nodesById.ContainsKey(startEpisodeId))
                return;

            into.Add(ProgressionDiagnostic.Error(
                "StartEpisodeId",
                string.IsNullOrEmpty(startEpisodeId)
                    ? "The start episode is empty. The chapter has nowhere to start."
                    : $"Start episode '{startEpisodeId}' is not among the nodes. " +
                      $"Existing: {Join(nodesById.Keys)}"));
        }

        private static void VerifyEdges(
            IReadOnlyList<EpisodeNode> nodes,
            Dictionary<string, EpisodeNode> nodesById,
            Dictionary<string, StatDefinition> statsByKey,
            ICollection<ProgressionDiagnostic> into)
        {
            foreach (EpisodeNode node in nodes)
            {
                if (node == null)
                    continue;
                
                IReadOnlyList<EpisodeOption> options = node.NextOptions;

                for (int i = 0; i < options.Count; i++)
                {
                    EpisodeOption option = options[i];
                    string where = $"Nodes[{node.EpisodeId}].NextOptions[{i}]";

                    if (!nodesById.ContainsKey(option.TargetEpisodeId))
                    {
                        into.Add(ProgressionDiagnostic.Error(
                            where,
                            $"Target '{option.TargetEpisodeId}' is not among the nodes. " +
                            $"Existing: {Join(nodesById.Keys)}"));
                    }

                    if (option.IsAuto)
                        VerifyAuto(options.Count, option, where, into);

                    VerifyConditions(
                        option.VisibleConditions, statsByKey, where + ".VisibleConditions", into);
                    VerifyConditions(
                        option.Conditions, statsByKey, where + ".Conditions", into);
                    VerifyStatChanges(
                        option.StatChanges, statsByKey, where + ".StatChanges", into);
                }
            }
        }

        // 장면에 밖에서 들어오는 자리는 하나다.
        //
        // 그 자리가 장면 루트이고, 롤백이 되돌아가는 곳과 이어하기가 재개하는 곳이
        // 전부 그것이다. 착지점이 여럿이면 "이 장면은 어디서 시작하는가"가 경로마다
        // 달라져 데이터로 정해지지 않는다.
        //
        // 이 규칙 하나가 연결성까지 함께 본다 — 밖에서 오는 길이 루트뿐이므로 도달
        // 가능한 에피소드는 전부 루트에서 장면 안 간선으로 이어진다. 아무 데서도
        // 안 들어오는 고아 노드는 여기서 보지 않는다(도달성의 일이고, 이 클래스는
        // 구조적 무결성만 본다).
        //
        // 장면을 나갔다 되돌아오는 것은 막지 않는다 — 허브 구조(교실 ↔ 복도)가
        // 그것이고, 재진입은 루트에서 다시 여는 새 장면 방문일 뿐이다.
        private static void VerifySceneEntries(
            IReadOnlyList<EpisodeNode> nodes,
            Dictionary<string, EpisodeNode> nodesById,
            string startEpisodeId,
            ICollection<ProgressionDiagnostic> into)
        {
            var landings = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            // 규칙을 깬 자리 = 그 장면에 두 번째 착지점을 만든 간선.
            var offending = new Dictionary<string, string>(StringComparer.Ordinal);

            // 챕터 시작도 밖에서 들어오는 길이다 — 챕터가 그 장면을 여는 자리.
            if (!string.IsNullOrEmpty(startEpisodeId) &&
                nodesById.TryGetValue(startEpisodeId, out EpisodeNode start))
            {
                Land(landings, offending, start.SceneId, start.EpisodeId, "StartEpisodeId");
            }

            foreach (EpisodeNode node in nodes)
            {
                if (node == null)
                    continue;

                IReadOnlyList<EpisodeOption> options = node.NextOptions;

                for (int i = 0; i < options.Count; i++)
                {
                    // 도착이 실재하지 않는 간선은 VerifyEdges가 이미 잡았다.
                    if (!nodesById.TryGetValue(options[i].TargetEpisodeId, out EpisodeNode target))
                        continue;

                    // 장면 안에서 움직이는 간선은 들어오는 길이 아니다.
                    if (string.Equals(node.SceneId, target.SceneId, StringComparison.Ordinal))
                        continue;

                    Land(
                        landings, offending, target.SceneId, target.EpisodeId,
                        $"Nodes[{node.EpisodeId}].NextOptions[{i}]");
                }
            }

            foreach (KeyValuePair<string, HashSet<string>> scene in landings)
            {
                if (scene.Value.Count <= 1)
                    continue;

                into.Add(ProgressionDiagnostic.Error(
                    offending[scene.Key],
                    $"Scene '{scene.Key}' has {scene.Value.Count} entrances from outside: " +
                    $"{Join(scene.Value)}. A scene must start at one place only — " +
                    "it is where rollback returns to and where Continue resumes. " +
                    "Split the other landing points into another scene."));
            }
        }

        private static void Land(
            Dictionary<string, HashSet<string>> landings,
            Dictionary<string, string> offending,
            string sceneId,
            string episodeId,
            string path)
        {
            if (!landings.TryGetValue(sceneId, out HashSet<string> into))
            {
                into = new HashSet<string>(StringComparer.Ordinal);
                landings[sceneId] = into;
            }

            // 같은 자리로 여러 간선이 들어오는 것은 정상이다 — 자리의 수만 센다.
            if (into.Add(episodeId) && into.Count == 2)
                offending[sceneId] = path;
        }

        // 자동 간선 — 묻지 않고 지나가는 길은 셋을 지켜야 한다.
        // 유일한 간선(고를 것이 없어야 묻지 않는 게 맞다) · 조건 없음(판정할 것이 없어야 한다) ·
        // 스탯 변화 없음(스탯 효과는 플레이어에게 보이는 선택지에서만 일어난다 — 도달성 증명과 UX의 근거).
        //
        // 장면은 넘어도 된다. 경계에서 일어나는 일(무대 비우기 · 확정 · 저장)은 묻는 선택지로 넘을 때와
        // 같고 박스만 뜨지 않는다. 자동(Auto)도 장면 소속(SceneId)도 저작자가 적은 것이라 런타임이
        // 몰래 정하는 것이 없다(2026-11-18, docs/PLAN_auto-edge-across-scenes.md).
        private static void VerifyAuto(
            int siblingCount,
            EpisodeOption option,
            string where,
            ICollection<ProgressionDiagnostic> into)
        {
            if (siblingCount != 1)
            {
                into.Add(ProgressionDiagnostic.Error(
                    where,
                    $"An auto edge must be the only edge of its episode. There are {siblingCount} now — " +
                    "if there is something to choose, make it a choice that asks; otherwise remove the others."));
            }

            if (option.VisibleConditions.Count > 0 || option.Conditions.Count > 0)
            {
                into.Add(ProgressionDiagnostic.Error(
                    where, "An auto edge can't have conditions — if there is something to judge, it is a choice that asks."));
            }

            if (option.StatChanges.Count > 0)
            {
                into.Add(ProgressionDiagnostic.Error(
                    where,
                    "An auto edge can't have stat changes — stat effects happen only on choices the player can see. " +
                    "To change a stat, give the edge a label and turn Auto off."));
            }
        }

        private static void VerifyConditions(
            IReadOnlyList<ProgressionCondition> conditions,
            Dictionary<string, StatDefinition> statsByKey,
            string where,
            ICollection<ProgressionDiagnostic> into)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                ProgressionCondition condition = conditions[i];
                string at = $"{where}[{i}]";

                if (condition.Kind != ConditionKind.Stat)
                {
                    into.Add(ProgressionDiagnostic.Error(
                        at, $"Unhandled condition kind '{condition.Kind}'."));
                    continue;
                }

                if (!statsByKey.TryGetValue(condition.Key, out StatDefinition stat))
                {
                    // 없는 키를 0으로 읽으면 오타 낸 조건이
                    // "언제나 통과하는 관문"이 되고, 그 버그는 재생해 봐도 안 보인다.
                    into.Add(ProgressionDiagnostic.Error(
                        at,
                        $"Undefined stat '{condition.Key}'. " +
                        $"Defined: {Join(statsByKey.Keys)}"));
                    continue;
                }

                if (stat.Type != StatType.Bool)
                    continue;
                
                if (condition.Op != ComparisonOp.Equal)
                {
                    into.Add(ProgressionDiagnostic.Error(
                        at,
                        $"'{condition.Key}' is a bool stat, so only Equal can be used. " +
                        $"Got: {condition.Op}."));
                    continue;
                }

                if (condition.Value != 0 && condition.Value != 1)
                {
                    into.Add(ProgressionDiagnostic.Error(
                        at,
                        $"'{condition.Key}' is a bool stat, so the value compared must be 0 or 1. " +
                        $"Got: {condition.Value}."));
                }
            }
        }

        // 선택지 골랐을 때의 스탯 변경 구조 검사
        // [1]동일 키 금지.
        // - 같은 키를 두 번 '정하면' 어느 쪽이 사는지가 배열 순서에 달리고,
        // - 시트에서 행을 옮기는 것만으로 결과가 바뀜.
        
        // [2]숫자에 Set금지.
        // - 스탯 변화의 연속적인 범위 추적이 복잡해짐. 데이터 분석 난이도 보존.
        
        // [3]bool에 Add금지, 한 간선에 Bool 두 번 금지.
        // - add연산을 허용하면 clamp를 거치며 의도가 불분명해짐.
        // - 중복 시 마지막 것이 이기는데, 시트에서 행을 옮기는 것만으로 결과가 바뀜.
        private static void VerifyStatChanges(
            IReadOnlyList<StatChange> changes,
            Dictionary<string, StatDefinition> statsByKey,
            string where,
            ICollection<ProgressionDiagnostic> into)
        {
            var setKeys = new HashSet<string>(StringComparer.Ordinal);
            var reportedDuplicates = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < changes.Count; i++)
            {
                StatChange change = changes[i];
                string at = $"{where}[{i}]";

                if (!statsByKey.TryGetValue(change.Key, out StatDefinition stat))
                {
                    into.Add(ProgressionDiagnostic.Error(
                        at,
                        $"Tries to change undefined stat '{change.Key}'. " +
                        $"Defined: {Join(statsByKey.Keys)}"));
                    continue;
                }

                if (change.Kind == StatChangeKind.Set)
                {
                    VerifySet(change, stat, at, setKeys, reportedDuplicates, into);
                    continue;
                }

                // bool 스탯에 증감은 의미가 없다(0/1 사이를 +1로 오가면 clamp에 걸려
                // 한 방향으로만 간다). 켜고 끄는 것은 Set이 한다.
                if (stat.Type == StatType.Bool)
                {
                    into.Add(ProgressionDiagnostic.Error(
                        at,
                        $"'{change.Key}' is a bool stat, so it can't be increased or decreased. " +
                        "To turn it on or off, set Op to \"Set\"."));
                }
            }
        }

        private static void VerifySet(
            StatChange change,
            StatDefinition stat,
            string at,
            HashSet<string> setKeys,
            HashSet<string> reportedDuplicates,
            ICollection<ProgressionDiagnostic> into)
        {
            // 숫자 스탯을 '정하면' 도달성 증명의 스탯 폭이 뜻을 잃는다 — 이 간선을
            // 지난 순간 앞의 모든 경로가 하나로 접히기 때문이다. 깃발에만 연다.
            if (stat.Type != StatType.Bool)
            {
                into.Add(ProgressionDiagnostic.Error(
                    at,
                    $"'{change.Key}' is a number stat, so Set can't be used. " +
                    "Set is only for bool stats (flags) — for an increase or decrease, leave Op empty or set it to \"Add\"."));

                return;
            }

            if (change.Amount != 0 && change.Amount != 1)
            {
                into.Add(ProgressionDiagnostic.Error(
                    at,
                    $"'{change.Key}' is a bool stat, so the value set must be 0 or 1. " +
                    $"Got: {change.Amount}. " +
                    "Clamping it silently would turn it on with a value other than the one the author wrote."));

                return;
            }

            if (!setKeys.Add(change.Key) && reportedDuplicates.Add(change.Key))
            {
                into.Add(ProgressionDiagnostic.Error(
                    at,
                    $"One edge sets '{change.Key}' twice. Which one wins depends on " +
                    "their order, so merely reordering them changes the result — " +
                    "keep only one."));
            }
        }

        private static string Join(IEnumerable<string> keys) => string.Join(", ", keys);
    }
}