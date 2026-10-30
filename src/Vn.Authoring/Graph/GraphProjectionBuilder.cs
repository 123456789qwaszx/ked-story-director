using Vn.Authoring.Chapters;
using Vn.Authoring.Definition;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;

namespace Vn.Authoring.Graph;

/// <summary>
/// StoryProject와 workspace의 펼침 상태에서 GraphView용 읽기 모델을 만든다.
///
/// 이 객체는 공식 데이터를 수정하지 않는다. 파일을 접어도 연결의 실제 source/target NodeId는
/// 그대로 유지하고, endpoint만 FileProxy의 해당 노드 행으로 바꾼다.
/// </summary>
public static class GraphProjectionBuilder
{
    private const double EmptyProxyStartX = 80;
    private const double EmptyProxyStartY = 80;
    private const double EmptyProxyGapX = 280;

    /// <summary>
    /// 카드를 그리지 않는 노드인가 — <b>설정노드 전부</b>다.
    ///
    /// 챕터 조건 공급 설정노드는 배관이라 원래 숨었다(까닭은 <see cref="Build"/> 안의 주석).
    /// 남은 설정노드는 옛 판의 「X 설정」 카드뿐인데 — 작가 조건 · 변수를 담던 자리다 — 그 안을
    /// 채우던 편집기가 걷혀 눌러도 빈 칸이었다. 2026-10-29(L6)부터 새 판에는 세우지 않고, 있는
    /// 것은 여기서 숨긴다. 데이터는 그대로라 그 안의 조건은 대사의 조건 목록에서 계속 고른다.
    /// </summary>
    public static bool IsHiddenPlumbing(StoryNode node) => node is SetNode;

    /// <param name="collapsedScenes">
    /// 펼친 판 안에서 접어 둔 장면들 (2026-10-30 — 하이어아키의 눈). 그 장면의 노드는 카드 대신
    /// <see cref="CollapsedSceneProjection"/>의 줄이 된다. 접힌 판 안의 장면 · 장면ID를 하나도 안 적은 챕터의 장면은 묻지 않는다
    /// (앞은 판째 접혀 있고, 뒤는 하이어아키에 장면 단이 없다).
    /// </param>
    public static GraphProjection Build(
        StoryProject project,
        IReadOnlySet<string> expandedFileIds,
        GameDefinition? definition = null,
        IReadOnlySet<GraphSceneKey>? collapsedScenes = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(expandedFileIds);

        var fileByNodeId = new Dictionary<string, StoryFile>(StringComparer.Ordinal);
        var rowIndexByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var portsByNodeId = new Dictionary<string, IReadOnlyList<GraphOutputPortProjection>>(StringComparer.Ordinal);

        // 배관 노드는 카드를 그리지 않는다 — A 계층 격리 (2026-08-15 소유자): 챕터 조건 공급
        // 설정노드는 동기화의 배관이지 작가의 데이터가 아니다. 식(스탯 변수)이 연출 그래프에
        // 노출되면 안 된다. 공급 자체는 데이터에 살아 있어 조건 라벨과 <<if>> 역조회는 변함없다.
        // (연출 노드 · 연출 공급 노드도 여기 숨던 배관이었는데 2026-10-29에 compositor로 갔다.)
        var hiddenPlumbingNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (StoryFile file in project.Files)
        {
            for (int index = 0; index < file.Nodes.Count; index++)
            {
                StoryNode node = file.Nodes[index];
                fileByNodeId[node.Id] = file;
                rowIndexByNodeId[node.Id] = index;
                portsByNodeId[node.Id] = BuildPorts(node, file, project, definition);

                if (IsHiddenPlumbing(node))
                {
                    hiddenPlumbingNodeIds.Add(node.Id);
                }
            }
        }

        IReadOnlyList<RawConnection> rawConnections = BuildRawConnections(project, portsByNodeId);
        Dictionary<string, int> incoming = CountByNode(rawConnections.Select(item => item.TargetNodeId));
        Dictionary<string, int> outgoing = CountByNode(rawConnections.Select(item => item.SourceNodeId));

        var items = new List<GraphItemProjection>();

        // 접은 장면에 든 노드 → 그 장면. 간선 끝을 정할 때 쓴다(상자 안의 줄 번호는 rowIndexByNodeId가 든다).
        var foldedSceneByNodeId = new Dictionary<string, GraphSceneKey>(StringComparer.Ordinal);

        for (int fileIndex = 0; fileIndex < project.Files.Count; fileIndex++)
        {
            StoryFile file = project.Files[fileIndex];

            if (expandedFileIds.Contains(file.Id))
            {
                SceneMap? folds = collapsedScenes is { Count: > 0 } && collapsedScenes.Any(key =>
                    string.Equals(key.FileId, file.Id, StringComparison.Ordinal))
                    ? ScenesOf(project, file)
                    : null;

                foreach (StoryNode node in file.Nodes)
                {
                    if (hiddenPlumbingNodeIds.Contains(node.Id))
                    {
                        continue;
                    }

                    if (folds?.SceneOf(project, node.Id, node.Name) is { } sceneId &&
                        collapsedScenes!.Contains(new GraphSceneKey(file.Id, sceneId)))
                    {
                        foldedSceneByNodeId[node.Id] = new GraphSceneKey(file.Id, sceneId);
                        continue;
                    }

                    items.Add(new ExpandedNodeProjection(
                        file.Id,
                        node.Id,
                        node.Name,
                        KindOf(node),
                        new GraphPosition(node.Layout.X, node.Layout.Y),
                        portsByNodeId[node.Id]));
                }

                if (folds is not null)
                {
                    items.AddRange(FoldedScenes(file, folds, foldedSceneByNodeId, rawConnections, rowIndexByNodeId));
                }

                continue;
            }

            IReadOnlyList<CollapsedNodeEntry> entries = file.Nodes
                .Where(node => !hiddenPlumbingNodeIds.Contains(node.Id))
                .Select(node => new CollapsedNodeEntry(
                    node.Id,
                    node.Name,
                    KindOf(node),
                    incoming.GetValueOrDefault(node.Id),
                    outgoing.GetValueOrDefault(node.Id)))
                .ToList();

            IReadOnlyList<CollapsedSceneGroup> scenes = SceneGroups(project, file, entries);

            items.Add(new CollapsedFileProjection(
                file.Id,
                file.Name,
                file.RelativePath,
                ProxyPosition(file, fileIndex),
                // ⚠ 묶음을 평평하게 편 것이 곧 행 순서다 — 묶으면서 순서가 바뀌므로
                //   `entries`를 그대로 두면 행 번호와 화면이 어긋난다.
                scenes.SelectMany(scene => scene.Entries).ToList(),
                scenes));
        }

        // 접힌 파일의 행 번호는 배관을 빼고 남은 항목 기준이다. 원본 인덱스를 쓰면 간선 끝이
        // 숨은 행을 가리켜 허공에 붙는다.
        //
        // ⚠ <b>장면 머리글도 한 행을 차지한다</b> (R6 S-3). 머리글을 안 세면 그 아래 노드의
        //    간선이 한 칸씩 위로 붙는다 — 화면과 기하가 같은 셈법을 써야 한다.
        foreach (GraphItemProjection item in items)
        {
            if (item is not CollapsedFileProjection proxy)
            {
                continue;
            }

            int row = 0;

            foreach (CollapsedSceneGroup scene in proxy.Scenes)
            {
                if (scene.HasHeader)
                {
                    row++;
                }

                foreach (CollapsedNodeEntry entry in scene.Entries)
                {
                    rowIndexByNodeId[entry.NodeId] = row++;
                }
            }
        }

        var connections = new List<GraphConnectionProjection>();

        foreach (RawConnection raw in rawConnections)
        {
            if (!fileByNodeId.TryGetValue(raw.SourceNodeId, out StoryFile? sourceFile) ||
                !fileByNodeId.TryGetValue(raw.TargetNodeId, out StoryFile? targetFile))
            {
                continue;
            }

            // 간선 정합: 한쪽 끝 노드가 배관 숨김으로 안 보이면 간선도 숨는다.
            if (hiddenPlumbingNodeIds.Contains(raw.SourceNodeId) ||
                hiddenPlumbingNodeIds.Contains(raw.TargetNodeId))
            {
                continue;
            }

            // 한 접은 장면 안끼리의 간선은 상자 안에 그리지 않는다 — 상자 머리가 센다(InnerCount).
            if (foldedSceneByNodeId.TryGetValue(raw.SourceNodeId, out GraphSceneKey sourceScene) &&
                foldedSceneByNodeId.TryGetValue(raw.TargetNodeId, out GraphSceneKey targetScene) &&
                sourceScene == targetScene)
            {
                continue;
            }

            GraphEndpointProjection Endpoint(StoryFile file, string nodeId, bool output)
            {
                if (!expandedFileIds.Contains(file.Id))
                {
                    return new GraphEndpointProjection(
                        file.Id,
                        nodeId,
                        output ? GraphEndpointKind.CollapsedFileNodeOutput : GraphEndpointKind.CollapsedFileNodeInput,
                        null,
                        rowIndexByNodeId[nodeId]);
                }

                if (foldedSceneByNodeId.TryGetValue(nodeId, out GraphSceneKey scene))
                {
                    return new GraphEndpointProjection(
                        file.Id,
                        nodeId,
                        output ? GraphEndpointKind.CollapsedSceneNodeOutput : GraphEndpointKind.CollapsedSceneNodeInput,
                        null,
                        rowIndexByNodeId[nodeId],
                        scene.SceneId);
                }

                return new GraphEndpointProjection(
                    file.Id,
                    nodeId,
                    output ? GraphEndpointKind.ExpandedNodeOutput : GraphEndpointKind.ExpandedNodeInput,
                    output ? raw.PortKey : null,
                    null);
            }

            GraphEndpointProjection source = Endpoint(sourceFile, raw.SourceNodeId, output: true);
            GraphEndpointProjection target = Endpoint(targetFile, raw.TargetNodeId, output: false);

            connections.Add(new GraphConnectionProjection(
                raw.Key,
                raw.Kind,
                raw.SourceNodeId,
                raw.TargetNodeId,
                raw.Label,
                raw.PaletteIndex,
                raw.ExecutionPort,
                source,
                target));
        }

        return new GraphProjection(items, connections);
    }

    private static IReadOnlyList<GraphOutputPortProjection> BuildPorts(
        StoryNode node,
        StoryFile file,
        StoryProject project,
        GameDefinition? definition)
    {
        IEnumerable<Vn.Authoring.Flow.ExitPort> exits = NodeConnections.PortsOf(node, project, definition);

        // T2 (철도 배선) — 에피소드 카드의 선택지 옵션 포트와 기본 출구는 카드가 아니라
        // 짝 간선의 칩에 산다. IF 갈래 출구는 카드에 남는다(내부 곁가지는 간선과 무관).
        //
        // ⚠ <b>「조건 분기」 표식도 카드에 남는다</b> (R7 P-5 · 2026-09-17). 이것을 안 넣어
        //    두면 「분기 추가」가 <b>안 보이는 포트를 뚫는다</b> — 소유자가 그린 그림의
        //    핵심("연출그래프의 카드에 포트만 뚫리는데")이 통째로 빠진다. P-6 뒤로는 판의
        //    카드가 전부 에피소드라 이 갈래에 <b>모두</b> 걸리므로 더 그렇다.
        if (node is DialogueNode { MarkedEpisodeId: not null })
        {
            exits = exits.Where(exit =>
                exit.Kind == Vn.Authoring.Flow.ExitPortKind.Detour ||
                (exit.Kind == Vn.Authoring.Flow.ExitPortKind.Branch && !exit.IsChoice));
        }

        var ports = exits
            .Select(exit => new GraphOutputPortProjection(
                PortKey(exit),
                exit.Kind == ExitPortKind.Default
                    ? GraphOutputPortKind.ExecutionDefault
                    : GraphOutputPortKind.ExecutionBranch,
                exit.NodeId,
                exit.Label,
                exit.PaletteIndex,
                exit.IsConnected,
                exit,
                OnLeft: exit.TargetNodeId is { } targetId &&
                        file.Nodes.FirstOrDefault(other => string.Equals(other.Id, targetId, StringComparison.Ordinal))
                            is { } target &&
                        EdgeSide.GoesLeft(node.Layout.X, target.Layout.X)))
            .ToList();

        // 설정노드의 "이 챕터에 공급 중" 포트는 2026-08-22에 사라졌다 (소유자) —
        // 공급 범위가 판 전체가 된 2026-08-17 이후로 이을 곳이 없어 <b>사실 표시</b>로만
        // 남아 있었다. 카드가 늘 켜진 포트를 달고 있으면 "여기서 무언가를 이을 수 있다"고
        // 말하는 셈이라 오히려 거짓말이었다. 공급은 같은 판에 선 것으로 이미 성립한다.

        ports.AddRange(ChoiceSlots(node, file, project));

        return ports;
    }

    /// <summary>화면이 <b>새로</b> 낼 수 있는 선택지 칸 수 (R7 §2 ③ — 소유자: "딱 3개").</summary>
    private const int ChoiceSlots3 = 3;

    /// <summary>
    /// <b>챕터 간선 슬롯</b> — 이 에피소드에서 나가는 선택지들 (R7 P-1 · 2026-09-16).
    ///
    /// 앞칸부터 <b>이미 있는 간선</b>, 남은 칸은 빈 슬롯이다. 라벨을 적어야 살아나고
    /// (§2 ③), 이어 붙는 순간 고쳐지는 것은 <c>ChapterDocument.Edges</c>다(§2 ①).
    ///
    /// ⚠ <b>간선이 셋을 넘으면 넘는 대로 전부 낸다.</b> 3은 <b>새로 만드는 칸</b>의 수이지
    /// 상한이 아니다 — 선택지를 넷 만든 챕터의 넷째를 화면이 숨기면 사람은 <b>사라진 줄</b>
    /// 안다. 상한을 모델에 박는 것은 v9가 없앤 `선택지수` 칸을 되살리는 일이다.
    ///
    /// ⚠ <b>챕터 판의 대사 노드는 전부 낸다</b> (R7 P-6 · 결정 ⑤ · 2026-09-17). 전에는
    /// 에피소드 노드만 냈지만 자유 씬이라는 종류가 없어졌다 — 카드를 세우는 것이 곧
    /// 에피소드를 만드는 일이라 칸을 못 낼 노드가 없다. 챕터가 아닌 판(작가의 낙서판)에는
    /// 여전히 칸이 없다.
    /// </summary>
    private static IEnumerable<GraphOutputPortProjection> ChoiceSlots(
        StoryNode node, StoryFile file, StoryProject project)
    {
        if (node is not DialogueNode dialogue ||
            project.Chapters.FirstOrDefault(item =>
                string.Equals(item.ChapterId, file.Name, StringComparison.Ordinal)) is not { } chapter)
        {
            yield break;
        }

        // 노드 → 에피소드는 표식이 먼저고 없으면 이름이다 — 장면 묶기와 같은 규칙(EpisodeNaming 한 벌)이다.
        string episodeId = EpisodeNaming.EpisodeIdOf(dialogue);

        if (!chapter.Episodes.Any(episode =>
                string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal)))
        {
            yield break;
        }

        int slot = 0;

        foreach (ChapterEdge edge in chapter.Edges.Where(item =>
                     string.Equals(item.FromEpisodeId, episodeId, StringComparison.Ordinal)))
        {
            yield return Choice(new GraphChoicePort(
                chapter.ChapterId,
                episodeId,
                slot++,
                edge.OptionLabel ?? string.Empty,
                edge.ToEpisodeId,
                NodeOf(file, edge.ToEpisodeId),
                edge.Auto), dialogue.Id);
        }

        for (; slot < ChoiceSlots3; slot++)
        {
            yield return Choice(new GraphChoicePort(
                chapter.ChapterId, episodeId, slot,
                Label: string.Empty, ToEpisodeId: null, ToNodeId: null, IsAuto: false), dialogue.Id);
        }
    }

    private static GraphOutputPortProjection Choice(GraphChoicePort choice, string nodeId) =>
        new($"choice:{choice.Slot}",
            GraphOutputPortKind.Choice,
            nodeId,
            choice.Label,
            choice.Slot,
            !choice.IsEmpty,
            ExecutionPort: null,
            choice);

    /// <summary>
    /// 그 에피소드의 대사 노드 — <b>같은 판에서만</b> 찾는다.
    ///
    /// ⚠ 프로젝트 전체를 이름으로 훑으면 다른 챕터의 같은 Id가 걸린다(개명이 그 함정을
    /// 이미 한 번 밟았다 — 2026-08-25). 못 찾아도 간선은 있다: <b>대본이 없는 것과 길이
    /// 없는 것은 다르다.</b>
    /// </summary>
    private static string? NodeOf(StoryFile file, string episodeId) =>
        file.Nodes.OfType<DialogueNode>().FirstOrDefault(node =>
            string.Equals(
                EpisodeNaming.EpisodeIdOf(node),
                episodeId,
                StringComparison.Ordinal))?.Id;

    private static IReadOnlyList<RawConnection> BuildRawConnections(
        StoryProject project,
        IReadOnlyDictionary<string, IReadOnlyList<GraphOutputPortProjection>> portsByNodeId)
    {
        var result = new List<RawConnection>();

        foreach (StoryNode node in project.EnumerateNodes())
        {
            IReadOnlyList<GraphOutputPortProjection> ports = portsByNodeId[node.Id];

            foreach (GraphOutputPortProjection port in ports)
            {
                if (port.ExecutionPort is not ExitPort exit ||
                    exit.TargetNodeId is not string targetNodeId)
                {
                    continue;
                }

                result.Add(new RawConnection(
                    // ⚠ 분기는 줄 하나에 여럿일 수 있다(2026-10-29) — 열쇠에 다녀갈 카드까지 든다(ExitPort.DetourKey).
                    //   줄만 쓰면 같은 줄의 분기 선들이 한 열쇠를 나눠 가져, 끄는 동안 선을 열쇠로 다시 찾는 화면이
                    //   모두 첫 선의 경로를 받는다 — 나머지 분기가 끊긴 것처럼 보였다.
                    $"execution:{exit.NodeId}:{exit.Kind}:{(exit.Kind == ExitPortKind.Detour ? exit.ExitKey : exit.BranchOpenLineId) ?? "default"}",
                    exit.Kind == ExitPortKind.Default
                        ? GraphConnectionKind.ExecutionDefault
                        : GraphConnectionKind.ExecutionBranch,
                    node.Id,
                    targetNodeId,
                    port.Key,
                    port.Label,
                    port.PaletteIndex,
                    exit));
            }
        }

        // 조건 공급선은 그리지 않는다 (2026-08-17 소유자 — "챕터 단위로 전역에 쓰이는거야").
        // 범위가 판 전체이므로 선을 그리면 대사노드 수만큼 거미줄이 되고, 그 선이 무엇을
        // 정하지도 않는다. 구판 프로젝트에 남은 Settings 링크 데이터는 조용히 무시된다.

        return result;
    }

    private static Dictionary<string, int> CountByNode(IEnumerable<string> nodeIds)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string nodeId in nodeIds)
        {
            counts[nodeId] = counts.GetValueOrDefault(nodeId) + 1;
        }

        return counts;
    }

    private static GraphNodeKind KindOf(StoryNode node)
    {
        return node switch
        {
            DialogueNode => GraphNodeKind.Dialogue,
            SetNode => GraphNodeKind.Set,
            _ => throw new NotSupportedException($"지원하지 않는 노드 타입입니다: {node.GetType().Name}")
        };
    }

    /// <summary>
    /// 접힌 판의 행들을 <b>장면으로 묶는다</b> (R6 S-3 · 2026-09-16).
    ///
    /// ⛔ <b>장면은 여기서도 엔티티가 아니다</b> — 노드에서 에피소드로, 에피소드에서
    /// <c>SceneId</c>로 가는 투영이다. 판 이름이 챕터 Id와 같은 것이 그 다리이고, 그것이
    /// <c>EnsureChapterBoard</c>가 세우는 규약이다.
    ///
    /// ⚠ <b>묶지 않는 경우 둘</b>: ① 챕터를 못 찾는 판(작가의 자유 판) ② 장면ID를 하나도
    /// 안 적은 챕터. ②를 그대로 묶으면 에피소드 수만큼
    /// 장면 머리글이 생겨 프록시가 통째로 노이즈가 된다(<c>docs/plans/R6-explorer.md</c> §2).
    ///
    /// ⚠ <b>자유 씬은 장면 밖이다.</b> 에피소드가 아닌 대사노드·설정 노드는 챕터의
    /// 진행에 안 실리므로 장면 경계도 없다 — 맨 뒤 묶음으로 모은다.
    /// </summary>
    private static IReadOnlyList<CollapsedSceneGroup> SceneGroups(
        StoryProject project, StoryFile file, IReadOnlyList<CollapsedNodeEntry> entries)
    {
        if (ScenesOf(project, file) is not { } map)
        {
            return [Single(entries)];
        }

        string? SceneFor(CollapsedNodeEntry entry) => map.SceneOf(project, entry.NodeId, entry.NodeName);

        var groups = new List<CollapsedSceneGroup>();

        foreach (ChapterScene scene in map.Scenes)
        {
            List<CollapsedNodeEntry> inScene = entries
                .Where(entry => string.Equals(SceneFor(entry), scene.SceneId, StringComparison.Ordinal))
                .ToList();

            if (inScene.Count > 0)
            {
                groups.Add(new CollapsedSceneGroup(scene.SceneId, scene.DisplayName, inScene));
            }
        }

        List<CollapsedNodeEntry> outside = entries.Where(entry => SceneFor(entry) is null).ToList();

        if (outside.Count > 0)
        {
            groups.Add(new CollapsedSceneGroup(string.Empty, "장면 밖", outside));
        }

        return groups;
    }

    private static CollapsedSceneGroup Single(IReadOnlyList<CollapsedNodeEntry> entries) =>
        new(string.Empty, string.Empty, entries);

    /// <summary>
    /// 판의 노드를 장면으로 가르는 표 — 판이 챕터의 것이고 그 챕터가 장면ID를 하나라도 적었을 때만 있다(R6-explorer §2:
    /// 장면ID를 하나도 안 적은 챕터는 장면 단이 없다).
    /// </summary>
    private static SceneMap? ScenesOf(StoryProject project, StoryFile file)
    {
        ChapterDocument? chapter = project.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, file.Name, StringComparison.Ordinal));

        if (chapter is null)
        {
            return null;
        }

        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(
            chapter.ToGraphModel(chapter.ChapterId));

        if (scenes.Count == 0 || scenes.All(scene => scene.IsDefault))
        {
            return null;
        }

        var sceneOf = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ChapterScene scene in scenes)
        {
            foreach (ChapterEpisode episode in scene.Episodes)
            {
                sceneOf[episode.EpisodeId] = scene.SceneId;
            }
        }

        return new SceneMap(chapter, scenes, sceneOf);
    }

    /// <summary>
    /// 펼친 판의 접은 장면들을 상자로 (2026-10-30). 장면 순서는 챕터의 장면 순서, 줄 순서는 판의 노드 순서다.
    ///
    /// ⚠ <b>들고 나는 수는 그려지는 것만 센다</b> — 밖과 잇는 실행 간선과 선택지 길. 장면 안끼리의 길은 줄에 선이 안 붙으므로
    ///   점을 채우면 거짓말이고, 대신 상자 머리의 <see cref="CollapsedSceneProjection.InnerCount"/>가 된다.
    /// </summary>
    private static IEnumerable<CollapsedSceneProjection> FoldedScenes(
        StoryFile file,
        SceneMap map,
        IReadOnlyDictionary<string, GraphSceneKey> foldedSceneByNodeId,
        IReadOnlyList<RawConnection> rawConnections,
        Dictionary<string, int> rowIndexByNodeId)
    {
        foreach (ChapterScene scene in map.Scenes)
        {
            var key = new GraphSceneKey(file.Id, scene.SceneId);
            List<StoryNode> nodes = file.Nodes
                .Where(node => foldedSceneByNodeId.TryGetValue(node.Id, out GraphSceneKey owner) && owner == key)
                .ToList();

            if (nodes.Count == 0)
            {
                continue;
            }

            var inside = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
            bool Inside(string? episodeId) =>
                episodeId is not null && map.SceneOfEpisode.TryGetValue(episodeId, out string? sceneId) &&
                string.Equals(sceneId, scene.SceneId, StringComparison.Ordinal);

            int inner =
                rawConnections.Count(raw => inside.Contains(raw.SourceNodeId) && inside.Contains(raw.TargetNodeId)) +
                map.Chapter.Edges.Count(edge => Inside(edge.FromEpisodeId) && Inside(edge.ToEpisodeId));

            var entries = new List<CollapsedNodeEntry>();

            foreach (StoryNode node in nodes)
            {
                string? episodeId = map.EpisodeOf(node);

                int incoming =
                    rawConnections.Count(raw => raw.TargetNodeId == node.Id && !inside.Contains(raw.SourceNodeId)) +
                    (episodeId is null ? 0 : map.Chapter.Edges.Count(edge =>
                        string.Equals(edge.ToEpisodeId, episodeId, StringComparison.Ordinal) && !Inside(edge.FromEpisodeId)));
                int outgoing =
                    rawConnections.Count(raw => raw.SourceNodeId == node.Id && !inside.Contains(raw.TargetNodeId)) +
                    (episodeId is null ? 0 : map.Chapter.Edges.Count(edge =>
                        string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal) && !Inside(edge.ToEpisodeId)));

                rowIndexByNodeId[node.Id] = entries.Count;
                entries.Add(new CollapsedNodeEntry(node.Id, node.Name, KindOf(node), incoming, outgoing));
            }

            yield return new CollapsedSceneProjection(
                file.Id,
                scene.SceneId,
                scene.DisplayName,
                new GraphPosition(nodes.Min(node => node.Layout.X), nodes.Min(node => node.Layout.Y)),
                entries,
                inner);
        }
    }

    /// <summary>
    /// 한 챕터 판의 장면 표 — 에피소드 → 장면ID.
    ///
    /// ⚠ 노드 → 에피소드는 <b>표식이 먼저고 없으면 이름이다</b> — EpisodeNaming 한 벌과 같아야 한다(아니면 같은 에피소드를
    ///   화면마다 다르게 짚는다).
    /// </summary>
    private sealed record SceneMap(
        ChapterDocument Chapter,
        IReadOnlyList<ChapterScene> Scenes,
        IReadOnlyDictionary<string, string> SceneOfEpisode)
    {
        public string? EpisodeOf(StoryNode node) => EpisodeOf(node as DialogueNode, node.Name);

        public string? SceneOf(StoryProject project, string nodeId, string nodeName) =>
            EpisodeOf(project.FindNode(nodeId) as DialogueNode, nodeName) is { } episodeId
                ? SceneOfEpisode[episodeId]
                : null;

        private string? EpisodeOf(DialogueNode? dialogue, string nodeName) =>
            dialogue is { MarkedEpisodeId: { } marked } && SceneOfEpisode.ContainsKey(marked)
                ? marked
                : SceneOfEpisode.ContainsKey(nodeName) ? nodeName : null;
    }

    private static GraphPosition ProxyPosition(StoryFile file, int fileIndex)
    {
        if (file.Nodes.Count == 0)
        {
            return new GraphPosition(
                EmptyProxyStartX + (fileIndex * EmptyProxyGapX),
                EmptyProxyStartY);
        }

        // 접기 전 노드들이 있던 영역의 중심에 프록시를 둔다. 실제 노드 Layout은 바꾸지 않는다.
        return new GraphPosition(
            file.Nodes.Average(node => node.Layout.X),
            file.Nodes.Average(node => node.Layout.Y));
    }

    private static string PortKey(ExitPort exit)
    {
        // ⚠ <b>「조건 분기」는 제 접두를 진다</b> (R7 P-5). 갈래와 표식은 열쇠가 둘 다 LineId라,
        //    한 줄이 갈래도 열고 표식도 지면 열쇠가 겹쳐 <b>두 포트가 한 자리를 다툰다</b>.
        return exit.Kind switch
        {
            ExitPortKind.Default => $"execution:{exit.NodeId}:default",
            // 분기는 줄 하나에 여럿일 수 있다(2026-10-29) — 열쇠에 다녀갈 카드까지 든다(ExitPort.DetourKey).
            ExitPortKind.Detour => $"execution:{exit.NodeId}:detour:{exit.ExitKey}",
            _ => $"execution:{exit.NodeId}:branch:{exit.BranchOpenLineId}"
        };
    }


    private sealed record RawConnection(
        string Key,
        GraphConnectionKind Kind,
        string SourceNodeId,
        string TargetNodeId,
        string PortKey,
        string Label,
        int PaletteIndex,
        ExitPort? ExecutionPort);
}
