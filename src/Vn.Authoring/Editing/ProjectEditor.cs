using Vn.Authoring.Chapters;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Editing;

/// <summary>
/// 프로젝트를 바꾸는 유일한 통로.
///
/// 대사 화면과 그래프 화면은 서로 이야기하지 않는다. 둘 다 이 객체에 의도를 전달하고,
/// <see cref="Changed"/>를 듣고 다시 그린다. 화면끼리 직접 연결하면 화면 수가 늘어날 때마다
/// 연결 수가 제곱으로 늘고, 어느 화면이 최신인지 알 수 없게 된다.
///
/// 되돌리기는 <see cref="ProjectSnapshotCodec"/>의 aggregate 스냅샷 방식이다.
/// 디스크의 manifest/StoryFile 배치와 무관하므로 물리 저장 구조가 바뀌어도 편집 기록은
/// 하나의 문자열로 안정적으로 왕복한다.
/// </summary>
public sealed partial class ProjectEditor
{
    private const int MaxHistory = 100;

    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();
    private readonly Func<string> _newLineId;

    /// <param name="newLineId">
    /// 새 LineId 발급기. 동기화 계획을 테스트에서 읽으려면 Id가 예측 가능해야 한다.
    /// </param>
    public ProjectEditor(
        StoryProject project,
        Func<string>? newLineId = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        _newLineId = newLineId ?? Identifier.Line;
    }

    public StoryProject Project { get; private set; }

    /// <summary>
    /// 무엇이든 바뀌었을 때. 듣는 쪽은 현재 상태를 다시 읽는다.
    ///
    /// 바뀐 것이 구조인지 내용인지 함께 알린다. 화자·대사를 한 글자 칠 때마다 카드 목록을
    /// 통째로 다시 만들면 편집 중인 칸이 사라져 글을 쓸 수 없다. 종류를 알려 주면
    /// 화면이 "다시 그릴 것인가"를 스스로 판단할 수 있고, 그 판단이 화면마다 다를 수 있다.
    /// </summary>
    public event EventHandler<ProjectChangedEventArgs>? Changed;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    // ── 파일과 노드 ────────────────────────────────────────────────────────

    public StoryFile AddStoryFile(string? name = null)
    {
        var file = new StoryFile(name: name ?? NextFileName());

        Mutate(() => Project.Files.Add(file));
        return file;
    }

    /// <summary>
    /// 다른 프로젝트의 <c>.vnstory.json</c> 하나를 이 프로젝트의 파일(판)로 들여온다 (W51).
    ///
    /// 대사 노드가 참조하는 대본은 관례 위치(<c>../script/&lt;scriptId&gt;.vnscript.json</c>)에서
    /// 함께 들여오고, 못 찾으면 경고로 남긴다(조용히 버리지 않는다 — 규칙 14).
    /// Id가 이미 이 프로젝트에 있으면(같은 파일 재수입) 명확히 거부한다 — Id를 다시
    /// 발급하면 노드 간 연결·LineId 계약이 소리 없이 끊어진다.
    /// 다른 파일의 노드를 가리키던 실행 출구는 대상이 없으면 흐름 해석기가 알린다.
    /// </summary>
    public StoryFileImportOutcome ImportStoryFile(string storyJsonPath)
    {
        string fullPath = Path.GetFullPath(storyJsonPath);
        StoryFile imported = StoryFileJson.Read(File.ReadAllText(fullPath));

        if (Project.FindFile(imported.Id) is not null)
        {
            throw new InvalidOperationException(
                $"파일 '{imported.Name}'({imported.Id})은 이미 이 프로젝트에 있습니다 — 같은 파일을 두 번 가져올 수 없습니다.");
        }

        foreach (StoryNode node in imported.Nodes)
        {
            if (Project.FindNode(node.Id) is not null)
            {
                throw new InvalidOperationException(
                    $"노드 '{node.Name}'({node.Id})이 이미 이 프로젝트에 있습니다 — 같은 파일을 두 번 가져올 수 없습니다.");
            }
        }

        // 저장 시 이 프로젝트의 story/ 관례 자리로 다시 쓰인다 — 원본 파일은 건드리지 않는다.
        imported.RelativePath = ProjectStore.DefaultRelativePath(imported.Id);

        // 딸린 대본 — 원본 프로젝트의 관례 자리(story/의 형제 script/)에서 찾는다.
        string? sourceRoot = Path.GetDirectoryName(Path.GetDirectoryName(fullPath));
        var scripts = new List<ScriptDocument>();
        var warnings = new List<string>();

        foreach (DialogueNode dialogue in imported.Nodes.OfType<DialogueNode>())
        {
            if (dialogue.ScriptId is not { } scriptId || Project.FindScript(scriptId) is not null ||
                scripts.Any(script => string.Equals(script.Id, scriptId, StringComparison.Ordinal)))
            {
                continue;
            }

            string? scriptPath = sourceRoot is null
                ? null
                : Path.Combine(sourceRoot, ProjectStore.DefaultScriptPath(scriptId).Replace('/', Path.DirectorySeparatorChar));

            if (scriptPath is not null && File.Exists(scriptPath))
            {
                scripts.Add(ScriptDocumentJson.Read(File.ReadAllText(scriptPath)));
            }
            else
            {
                warnings.Add(
                    $"'{dialogue.Name}'의 대본({scriptId})을 찾지 못했습니다 — 원본 프로젝트의 script 폴더가 옆에 있어야 합니다.");
            }
        }

        Mutate(() =>
        {
            foreach (ScriptDocument script in scripts)
            {
                Project.Scripts.Add(script);
            }

            Project.Files.Add(imported);
        });

        return new StoryFileImportOutcome(imported, scripts.Count, warnings);
    }

    /// <summary>
    /// 시나리오 파일(판)을 통째로 제거한다 (W61). 안의 노드가 맺던 연결·출구는
    /// 노드 삭제와 같은 규칙 하나(<see cref="RemoveReferencesToNode"/>)로 정리된다.
    /// 대본은 지우지 않는다 — 대본은 판이 아니라 프로젝트의 것이다. 한 번의 되돌리기로 통째로 복구된다.
    /// </summary>
    public void RemoveStoryFile(string fileId)
    {
        StoryFile? file = Project.FindFile(fileId);

        if (file is null)
        {
            return;
        }

        if (Project.Files.Count <= 1)
        {
            throw new InvalidOperationException(
                "마지막 시나리오 파일은 제거할 수 없습니다 — 새 노드가 갈 곳이 없어집니다.");
        }

        Mutate(() => RemoveFileCore(file));
    }

    /// <summary>
    /// 판 하나를 걷는 <b>알맹이</b> — <see cref="Mutate"/> 밖이다.
    ///
    /// ⚠ 챕터를 지울 때 판과 챕터가 <b>한 번의 변경</b>이어야 해서 갈라 뒀다
    /// (<see cref="RemoveChapterWithBoard"/>). 되돌리기 한 번에 둘 중 하나만 돌아오면
    /// 챕터는 있는데 판이 없는(또는 그 반대) 상태가 남는다.
    /// </summary>
    private void RemoveFileCore(StoryFile file)
    {
        Project.Files.Remove(file);

        foreach (StoryNode node in file.Nodes)
        {
            RemoveReferencesToNode(node.Id);
        }

        if (file.Nodes.Any(node =>
            string.Equals(Project.StartNodeId, node.Id, StringComparison.Ordinal)))
        {
            Project.StartNodeId = Project.EnumerateNodes().FirstOrDefault()?.Id;
        }
    }

    public void RenameStoryFile(string fileId, string name)
    {
        if (Project.FindFile(fileId) is { } file && !string.Equals(file.Name, name, StringComparison.Ordinal))
        {
            Mutate(ProjectChangeKind.NodeMetadata, () => file.Name = name);
        }
    }

    /// <summary>
    /// 새 노드를 지정한 파일의 가장 아래에 붙인다.
    /// 노드 Id는 프로젝트 전체에서 유일해야 한다.
    /// </summary>
    public TNode AddNode<TNode>(string fileId, TNode node) where TNode : StoryNode
    {
        StoryFile file = RequireFile(fileId);

        if (Project.FindNode(node.Id) is not null)
        {
            throw new InvalidOperationException($"노드 Id '{node.Id}'가 프로젝트 안에서 중복됩니다.");
        }

        Mutate(() =>
        {
            file.Nodes.Add(node);
            Project.StartNodeId ??= node.Id;
        });

        return node;
    }

    /// <summary>
    /// 대사 노드를 만든다. scriptId를 주지 않으면 <b>전용 대본을 함께 만들어 잇는다</b> (X4, D-3)
    /// — 노드는 생성 즉시 편집 대상이고, 첫 빈 줄까지 준비되어 바로 타이핑할 수 있다.
    /// 노드·대본·첫 줄이 한 번의 편집이라 되돌리기 한 번에 함께 사라진다.
    /// </summary>
    public DialogueNode AddDialogueNode(
        string fileId,
        double x = 0,
        double y = 0,
        string? name = null,
        string? scriptId = null,
        string? sceneId = null)
    {
        if (scriptId is null)
        {
            (DialogueNode created, _, Action attach) = NewDialogueNodeCore(fileId, x, y, name, sceneId);
            Mutate(attach);

            return created;
        }

        var node = new DialogueNode(name: name ?? NextName("장면"))
        {
            Layout = new NodeLayout { X = x, Y = y },
            ScriptId = scriptId
        };

        return AddNode(fileId, node);
    }

    /// <summary>
    /// 대사 노드와 그 전용 대본을 <b>짓기만</b> 한다 — <see cref="Mutate"/> 밖이다.
    ///
    /// ⚠ 다른 변경과 <b>한 번에</b> 묶어야 할 때 쓴다(「분기 추가」는 노드·대본·표식이 한
    /// 동작이라 되돌리기 한 번에 셋이 함께 돌아와야 한다). <c>Mutate</c> 안에서 다른 명령을
    /// 부르면 변경이 겹쳐 쌓여 되돌리기가 반쪽이 된다 — 그래서 <b>짓는 것</b>과 <b>붙이는
    /// 것</b>을 갈라 둔다.
    ///
    /// ⭐ <b>챕터 판에 세우면 에피소드도 함께 선다</b> (R7 P-6 · 결정 ⑤ · 2026-09-17).
    /// 자유 씬이라는 종류가 없어졌으므로 <b>판의 대사 노드는 전부 에피소드</b>다 — 카드를
    /// 세우는 것이 곧 에피소드를 만드는 일이고, 그래서 어긋난 이름이 애초에 안 생긴다.
    /// </summary>
    private (DialogueNode Created, ChapterEpisode? Episode, Action Attach) NewDialogueNodeCore(
        string fileId,
        double x,
        double y,
        string? name,
        string? sceneId = null,
        bool allowUnreachable = false,
        string? title = null,
        (double X, double Y)? episodePosition = null)
    {
        StoryFile file = RequireFile(fileId);

        // ⭐ <b>이름을 고르는 규칙도 여기 하나다</b> (2026-09-18). 챕터 판에서는 이 이름이
        //    곧 EpisodeId가 되므로 <c>장면 3</c> 같은 <b>공백 든 Id</b>가 나오면 안 된다 —
        //    그 Id는 진행 JSON과 산출물의 열쇠로 나간다(엑셀 시절에는 대본 워크북의 파일 이름이기도
        //    했다). 다른 창구가 이미 쓰던 `new01`
        //    규약으로 맞춘다.
        var created = new DialogueNode(
            name: name ?? (ChapterOfBoard(file) is { } board
                ? NextEpisodeId(board)
                : NextName("장면")))
        {
            Layout = new NodeLayout { X = x, Y = y }
        };

        if (Project.FindNode(created.Id) is not null)
        {
            throw new InvalidOperationException($"노드 Id '{created.Id}'가 프로젝트 안에서 중복됩니다.");
        }

        var script = new ScriptDocument(name: $"{created.Name} 대본");
        var firstLine = new ScriptLine(_newLineId());
        created.ScriptId = script.Id;

        (ChapterDocument? chapter, ChapterEpisode? episode) =
            NewEpisodeFor(file, created, sceneId, allowUnreachable, title, episodePosition);

        return (created, episode, () =>
        {
            script.Lines.Add(firstLine);
            script.RequireLocale(script.PrimaryLocale).Entries[firstLine.Id] = LocalizedLine.Empty;
            Project.Scripts.Add(script);
            file.Nodes.Add(created);
            Project.StartNodeId ??= created.Id;

            if (chapter is not null && episode is not null)
            {
                chapter.Episodes.Add(episode);
            }
        });
    }

    /// <summary>
    /// 이 노드가 선 <b>장면</b>. 챕터 판이 아니거나 아직 에피소드가 없으면 <c>null</c>이다.
    /// </summary>
    private string? SceneOfNode(StoryFile file, DialogueNode node) =>
        ChapterOfBoard(file) is { } chapter && EpisodeNaming.EpisodeFor(chapter, node) is { } episode
            ? episode.SceneId
            : null;

    /// <summary>이 판이 곧 챕터라면 그 챕터 — 규칙은 <see cref="EpisodeNaming"/>이 갖는다.</summary>
    private ChapterDocument? ChapterOfBoard(StoryFile file) =>
        EpisodeNaming.ChapterOfBoard(Project, file);

    /// <summary>
    /// 이 노드가 질 <b>에피소드</b>를 짓는다 (R7 P-6). 챕터 판이 아니면 둘 다 <c>null</c>이다 —
    /// 작가의 낙서판에는 챕터가 없고, 거기 세운 노드는 진행에 안 실린다.
    ///
    /// ⚠ <b>이미 있는 에피소드면 짓지 않고 표식만 붙인다.</b> 챕터 그래프에서 먼저 만든
    /// 에피소드에 대본을 다는 것이(그리고 열 때 카드 없는 에피소드에 빈 카드를 세우는 것이 —
    /// <see cref="AlignBoardsWithChapters"/>) 정확히 그
    /// 자리다 — 거기서 또 지으면 에피소드 하나에 카드가 둘 선다.
    ///
    /// ⚠ 임포터는 노드를 <b>EpisodeId가 아니라 `대사엔트리`로</b> 짓는다. 그래서 찾는 일은
    /// <see cref="EpisodeNaming.EpisodeFor"/>에 맡긴다 — 여기서 이름만 맞춰 보면 `대사엔트리`가
    /// 따로 적힌 챕터마다 에피소드가 <b>두 배로 불어난다</b>(2026-09-17에 겪었다).
    /// </summary>
    /// <param name="episodePosition">
    /// 챕터 그래프에서의 자리. ⚠ <b>카드의 자리와 다른 판이다</b> — 연출 판의 좌표를 그대로
    /// 챕터 그래프에 넣으면 사람이 맞춰 둔 배치가 흐트러진다. 안 주면 카드 자리를 따른다
    /// (연출 그래프에서 카드부터 세우는 길이 그렇다 — 거기서는 그 자리가 유일한 정보다).
    /// </param>
    private (ChapterDocument? Chapter, ChapterEpisode? Episode) NewEpisodeFor(
        StoryFile file, DialogueNode created, string? sceneId, bool allowUnreachable,
        string? title = null, (double X, double Y)? episodePosition = null)
    {
        if (ChapterOfBoard(file) is not { } chapter)
        {
            return (null, null);
        }

        if (EpisodeNaming.EpisodeFor(chapter, created) is { } standing)
        {
            // 표식이 신원이다 — 이름은 사람이 고치는 글자라 개명 때마다 끊긴다(P-6 결정).
            created.MarkedEpisodeId = standing.EpisodeId;

            return (null, null);
        }

        created.MarkedEpisodeId = created.Name;

        return (chapter, new ChapterEpisode(
            created.Name,
            title ?? created.Name,
            Index: string.Empty,
            DialogueEntry: created.Name,
            Math.Round(episodePosition?.X ?? created.Layout.X, 2),
            Math.Round(episodePosition?.Y ?? created.Layout.Y, 2),
            Memo: null,
            SourceRow: 0,
            AllowUnreachable: allowUnreachable)
        {
            SceneId = sceneId
        });
    }

    /// <summary>
    /// <b>「분기 추가」</b> (R7 P-5 · 2026-09-17 소유자) — 그 줄 <b>앞</b>에 분기 표식을 두고,
    /// 다녀올 <b>자유 씬</b>을 함께 세워 잇는다.
    ///
    /// ⛔ <b>조건을 걸지 않는다.</b> 연출 그래프는 "어디서 갈라지는가"만 짚는다 — 성립하든
    /// 말든 다녀오고, 다녀온 씬이 제 첫머리에서 보고 아니면 곧바로 돌아온다.
    ///
    /// ⭐ <b>조건을 채우는 창구는 챕터 그래프다</b> (2026-09-18에 섰다 — 그전에는 "이후의
    /// 다른 탭"이라고만 적혀 있었다). 판의 점선을 누르면 오른쪽에 조건 한 칸이 열리고,
    /// 그 값은 <b>다녀갈 에피소드의 첫머리</b>에 적힌다
    /// (<see cref="Chapters.BranchEntryCondition"/>). 부르는 쪽 대본은 그대로다.
    ///
    /// ⚠ <b>그냥 에피소드다</b> (R7 P-6 · 결정 ⑤가 2026-09-17에 뒤집었다 — 전에는 자유 씬).
    /// 진행에 실리며, 아래에 선택지 3칸이 뚫린다. <b>출발과 같은
    /// 장면</b>에 둔다 — 곁가지는 제 본줄 옆에 있어야 판에서 읽힌다.
    ///
    /// ⚠ 노드·대본·표식이 <b>한 번의 변경</b>이다. 갈라 두면 되돌리기 한 번에 표식만 사라져
    /// <b>아무도 안 부르는 씬</b>이 판에 남는다.
    /// </summary>
    /// <param name="beforeLineId">이 줄 앞에서 갈라진다. 그 줄에 이미 표식이 있으면 거절한다.</param>
    public DialogueNode AddBranchMarker(string nodeId, string beforeLineId, string? name = null)
    {
        if (Project.FindNode(nodeId) is not DialogueNode source)
        {
            throw new InvalidOperationException("분기는 대사 노드에만 둘 수 있습니다.");
        }

        if (Project.FindFileContainingNode(nodeId) is not { } file)
        {
            throw new InvalidOperationException("판에 서 있지 않은 노드입니다.");
        }

        if (Project.FindScript(source.ScriptId) is not { } script ||
            script.ActiveLines.All(line =>
                !string.Equals(line.Id, beforeLineId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("그 줄이 이 노드의 대본에 없습니다.");
        }

        if (source.FindExtension(beforeLineId)?.DetourTargetNodeId is not null)
        {
            throw new InvalidOperationException(
                "그 자리에는 이미 분기가 있습니다 — 한 자리에 하나만 둘 수 있습니다.");
        }

        // 분기로 다녀갈 카드는 원본의 <b>오른쪽</b>에 선다 (D3 · 2026-10-29 소유자: "분기는 기존처럼
        // 오른쪽으로 빠지도록"). 아래는 선택지로 가는 다음 에피소드의 자리다.
        (double x, double y) = Graph.NodePlacement.BesideOf(file, source);

        (DialogueNode created, _, Action attach) = NewDialogueNodeCore(
            file.Id,
            x,
            y,
            name ?? NextName("분기"),
            SceneOfNode(file, source),
            // ⛔ 들어오는 간선이 없다 — 챕터 진행이 아니라 대본의 `<<detour>>`로 들어간다.
            //    그래서 도달성 증명은 이것을 <b>정확히 도달 불가로</b> 판정하고, 그 판정이
            //    맞다. 증명기를 고치지 않는 이유는 그것이 저쪽 런타임의 <b>오라클</b>이기
            //    때문이다(코퍼스로 고정돼 있어 여기서 답을 바꾸면 둘이 조용히 갈린다).
            //    이미 있는 `도달불가 허용`이 정확히 이 자리를 위한 칸이다 — 오류가 알림으로
            //    낮아지고, 그래도 판에는 표시된다.
            allowUnreachable: true);

        Mutate(() =>
        {
            attach();
            source.RequireExtension(beforeLineId).DetourTargetNodeId = created.Id;
        });

        return created;
    }

    /// <summary>
    /// <b>「분기 지우기」</b> (2026-10-29 소유자: *"조건 분기를 추가는 가능한데, 현재는 지우는게 불가능 합니다.
    /// 지울 수 있도록 해주시고"*) — <see cref="AddBranchMarker"/>의 반대다. 그 줄 앞의 분기 표식을 걷고,
    /// 그 분기가 다녀가던 카드(분기를 추가할 때 함께 선 에피소드)도 <b>다른 곳이 쓰지 않으면</b> 함께 걷는다.
    ///
    /// ⛔ 2026-10-29까지는 표식만 지우고 카드는 판에 남겼다 — "거기 써 둔 대사가 한 번의 실수로 사라진다"가
    /// 근거였는데, 그러면 아무도 안 부르는 카드가 판에 남아 사람은 분기가 지워지지 않았다고 본다. 대본은 원래
    /// 지우지 않고(<see cref="StoryProject.Scripts"/>) 되돌리기 한 번에 셋이 함께 돌아오므로 그 근거가 없어졌다.
    ///
    /// ⚠ <b>남기는 경우</b> — 그 카드를 다른 곳이 쓰고 있으면 표식만 걷는다: 다른 표식 · 출구가 그리로 가거나,
    /// 챕터의 길(간선)이 그리로 들어오거나, 챕터의 시작 에피소드이거나, 다른 판에 서 있을 때. 다른 데서 이어 쓰는
    /// 에피소드를 분기 하나 지우다 함께 걷으면 안 된다.
    /// </summary>
    /// <returns>지운 것. 그 자리에 분기가 없었으면 <c>null</c>이다.</returns>
    public BranchMarkerRemoval? RemoveBranchMarker(string nodeId, string beforeLineId)
    {
        if (Project.FindNode(nodeId) is not DialogueNode source ||
            source.FindExtension(beforeLineId)?.DetourTargetNodeId is not { Length: > 0 } targetId)
        {
            return null;
        }

        StoryNode? target = Project.FindNode(targetId);
        bool take = target is DialogueNode card && MadeForThisBranchOnly(card, source, beforeLineId);

        Batch(() =>
        {
            SetExitTarget(nodeId, ExitPortKind.Detour, beforeLineId, null);

            if (!take)
            {
                return;
            }

            var card = (DialogueNode)target!;

            if (Chapters.EpisodeNaming.ChapterOf(Project, card) is { } chapter &&
                FindEpisode(chapter, Chapters.EpisodeNaming.EpisodeIdOf(card)) is not null)
            {
                RemoveEpisode(chapter.ChapterId, Chapters.EpisodeNaming.EpisodeIdOf(card));
            }

            RemoveNode(card.Id);
        });

        return new BranchMarkerRemoval(
            TakenNodeName: take ? target!.Name : null,
            KeptNodeName: take ? null : target?.Name);
    }

    /// <summary>
    /// 그 카드를 쓰는 것이 <b>이 분기 하나뿐</b>인가 — 같은 판에 서 있고, 다른 표식 · 출구 · 챕터의 길이 그리로
    /// 가지 않고, 시작 노드 · 챕터의 시작 에피소드가 아니다.
    /// </summary>
    private bool MadeForThisBranchOnly(DialogueNode card, DialogueNode source, string beforeLineId)
    {
        if (Project.FindFileContainingNode(card.Id) != Project.FindFileContainingNode(source.Id) ||
            string.Equals(Project.StartNodeId, card.Id, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (StoryNode node in Project.EnumerateNodes())
        {
            if (string.Equals(node.DefaultExitTargetNodeId, card.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (node is not DialogueNode dialogue)
            {
                continue;
            }

            if (dialogue.BranchExits.Values.Concat(dialogue.ChoiceExits.Values)
                    .Any(target => string.Equals(target, card.Id, StringComparison.Ordinal)) ||
                dialogue.LineExtensions.Any(extension =>
                    string.Equals(extension.DetourTargetNodeId, card.Id, StringComparison.Ordinal) &&
                    !(ReferenceEquals(dialogue, source) &&
                      string.Equals(extension.LineId, beforeLineId, StringComparison.Ordinal))))
            {
                return false;
            }
        }

        if (Chapters.EpisodeNaming.ChapterOf(Project, card) is not { } chapter)
        {
            return true;
        }

        string episodeId = Chapters.EpisodeNaming.EpisodeIdOf(card);

        return !string.Equals(
                   chapter.Episodes.FirstOrDefault()?.EpisodeId, episodeId, StringComparison.Ordinal) &&
               !chapter.Edges.Any(edge => string.Equals(edge.ToEpisodeId, episodeId, StringComparison.Ordinal));
    }

    public SetNode AddSetNode(
        string fileId,
        double x = 0,
        double y = 0,
        string? name = null)
    {
        var node = new SetNode(name: name ?? NextName("설정"))
        {
            Layout = new NodeLayout { X = x, Y = y }
        };

        return AddNode(fileId, node);
    }

    public void RemoveNode(string nodeId)
    {
        StoryFile? owner = Project.FindFileContainingNode(nodeId);
        StoryNode? node = Project.FindNode(nodeId);

        if (owner is null || node is null)
        {
            return;
        }

        Mutate(() =>
        {
            owner.Nodes.Remove(node);
            RemoveReferencesToNode(nodeId);

            if (string.Equals(Project.StartNodeId, nodeId, StringComparison.Ordinal))
            {
                Project.StartNodeId = Project.EnumerateNodes().FirstOrDefault()?.Id;
            }
        });
    }

    /// <summary>
    /// 노드를 다른 StoryFile로 옮긴다. 같은 파일을 주면 그 파일 안에서 순서를 바꾼다.
    /// 노드의 Id와 그래프 좌표, 연결은 그대로 유지된다.
    /// </summary>
    public void MoveNodeToFile(string nodeId, string targetFileId, int? targetIndex = null)
    {
        StoryFile? source = Project.FindFileContainingNode(nodeId);
        StoryFile target = RequireFile(targetFileId);
        StoryNode? node = Project.FindNode(nodeId);

        if (source is null || node is null)
        {
            return;
        }

        int sourceIndex = source.Nodes.IndexOf(node);
        int requested = Math.Clamp(targetIndex ?? target.Nodes.Count, 0, target.Nodes.Count);
        int insertionIndex = requested;

        if (ReferenceEquals(source, target))
        {
            // targetIndex는 이동 전 목록을 기준으로 받는다. 앞의 항목을 제거하면 뒤쪽 위치가 하나 줄어든다.
            insertionIndex = requested > sourceIndex ? requested - 1 : requested;
            insertionIndex = Math.Clamp(insertionIndex, 0, source.Nodes.Count - 1);

            if (insertionIndex == sourceIndex)
            {
                return;
            }
        }

        Mutate(() =>
        {
            source.Nodes.Remove(node);
            int at = Math.Clamp(insertionIndex, 0, target.Nodes.Count);
            target.Nodes.Insert(at, node);
        });
    }

    public void RenameNode(string nodeId, string name)
    {
        if (Project.FindNode(nodeId) is { } node && !string.Equals(node.Name, name, StringComparison.Ordinal))
        {
            Mutate(ProjectChangeKind.NodeMetadata, () => node.Name = name);
        }
    }

    /// <summary>노드 하나의 자리를 옮긴다 — <see cref="MoveNodes"/>의 한 칸짜리다.</summary>
    public void MoveNode(string nodeId, double x, double y) =>
        MoveNodes(new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal) { [nodeId] = (x, y) });

    /// <summary>
    /// 여러 노드의 자리를 <b>한 번의 변경으로</b> 옮긴다 — 끌기를 놓을 때 쓴다
    /// (2026-10-29 · D3, <c>docs/design/two-graphs.md</c> §6-2).
    ///
    /// <b>되돌리기 한 칸이다.</b> 예전에는 그래프가 포인터가 움직일 때마다 기록 없이 적었다 —
    /// 끈 자리는 되돌릴 수 없었고, 앞선 편집을 되돌리면 끈 자리까지 조용히 돌아갔다. 이제 그래프는
    /// 끄는 동안 화면만 옮기고 놓을 때 여기를 한 번 부른다(챕터 그래프의 <c>MoveEpisodes</c>와 같다).
    ///
    /// 없는 노드는 건너뛰고, 실제로 달라진 자리가 없으면 아무것도 적지 않는다(§5-8).
    /// </summary>
    public void MoveNodes(IReadOnlyDictionary<string, (double X, double Y)> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        List<(StoryNode Node, double X, double Y)> moves = positions
            .Select(pair => (Node: Project.FindNode(pair.Key), pair.Value.X, pair.Value.Y))
            .Where(move => move.Node is not null && (move.Node.Layout.X != move.X || move.Node.Layout.Y != move.Y))
            .Select(move => (move.Node!, move.X, move.Y))
            .ToList();

        if (moves.Count == 0)
        {
            return;
        }

        Mutate(ProjectChangeKind.Layout, () =>
        {
            foreach ((StoryNode node, double x, double y) in moves)
            {
                node.Layout.X = x;
                node.Layout.Y = y;
            }
        });
    }

    // ── 줄에 얹는 대사 논리 ────────────────────────────────────────────────
    //
    // 줄 자체를 만들고 지우고 옮기는 명령은 여기 없다. 그것은 대본의 일이고
    // ProjectEditor.Scripts.cs에 있다. 여기서는 이미 있는 LineId에 논리를 붙인다.

    /// <summary>
    /// 이 줄에서 조건 흐름을 어떻게 바꿀지 정한다.
    /// 갈래를 더 이상 열지 않게 되면 거기 매달려 있던 조건 출구도 함께 사라진다.
    ///
    /// 대본에 없는 LineId를 주면 아무것도 하지 않는다. 조건은 존재하는 줄에만 붙는다.
    /// </summary>
    public void SetLineTransition(string nodeId, string lineId, LineConditionTransition? transition) =>
        SetLineTransitions(nodeId, lineId, transition is null ? [] : [transition]);

    /// <summary>
    /// 이 줄 앞의 전환들을 통째로 바꾼다 (2026-08-17) — 목록 순서가 곧 일어나는 순서다.
    /// 빈 목록이면 전환이 없는 줄이다. 대본에 없는 LineId를 주면 아무것도 하지 않는다.
    /// </summary>
    public void SetLineTransitions(
        string nodeId, string lineId, IReadOnlyList<LineConditionTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);

        DialogueNode node = RequireDialogue(nodeId);

        if (Project.FindScript(node.ScriptId)?.FindLine(lineId) is not { IsRetired: false })
        {
            return;
        }

        DialogueLineExtension? existing = node.FindExtension(lineId);

        if ((existing?.Transitions.Count ?? 0) == 0 && transitions.Count == 0)
        {
            return;
        }

        // 옵션 라벨 전환은 안정 Id를 가져야 한다 — 아래 루프가 그 자리에서 잇는다.
        var resolved = new List<LineConditionTransition>(transitions.Count);

        foreach (LineConditionTransition item in transitions)
        {
            resolved.Add(item is { OpensOption: true, OptionId: null }
                ? new LineConditionTransition(
                    item.Kind,
                    optionId: existing?.Transitions.FirstOrDefault(candidate => candidate.OpensOption)?.OptionId
                        ?? Identifier.Option())
                : item);
        }

        Mutate(() => WriteLineTransitions(node, lineId, existing, resolved));
    }

    /// <summary>전환을 그 줄에 앉힌다. <b>스냅샷을 쌓지 않는다</b> — 부르는 쪽이 <c>Mutate</c> 안이다.</summary>
    private void WriteLineTransitions(
        DialogueNode node,
        string lineId,
        DialogueLineExtension? existing,
        IReadOnlyList<LineConditionTransition> resolved)
    {
        DialogueLineExtension extension = existing ?? new DialogueLineExtension(lineId);

        if (existing is null)
        {
            node.LineExtensions.Add(extension);
        }

        extension.Transitions.Clear();
        extension.Transitions.AddRange(resolved);

        // 순서가 중요하다. 먼저 주인 없는 출구를 버리고, 그다음에 빈 항목을 버린다.
        // 반대로 하면 아직 출구가 매달려 있어 빈 항목이 남는다.
        PruneBranchExits(node);
        PruneLineExtensions(node);
    }

    /// <summary>
    /// <b>이 노드가 통째로 도는 조건</b>을 건다 — 첫 줄에 <c>BeginIf</c>, 마지막 줄 뒤에
    /// <c>EndIf</c> (2026-09-18). <c>null</c>이면 걷는다.
    ///
    /// <b>분기 에피소드의 첫머리 조건</b>이 사는 자리다 (R7 P-5 · 2026-09-18 소유자:
    /// *"Detour로 조건 분기가 일어나는 마커를 끼워두고 그곳에서 조건과 내용을 추가한다는게
    /// 유지보수 측면에서 얼마나 유리한지"*). 부르는 쪽 대본에는 <c>&lt;&lt;detour&gt;&gt;</c>
    /// 한 줄뿐이고 <b>조건과 내용이 다녀올 곳에 함께</b> 있다 — 짝 맞출 <c>If</c>/<c>EndIf</c>가
    /// 남의 대본에 흩어지지 않는다.
    ///
    /// ⭐ <b>성립하면 돈다</b>(허가). 조건이 거짓이면 본문이 통째로 건너뛰이고, 노드 끝이
    /// 곧 <c>&lt;&lt;return&gt;&gt;</c>이라 곧바로 돌아간다. 모델에 부정(<c>not</c>)이 없으므로
    /// 이 극성이 곧 저장 형태다.
    ///
    /// ⚠ <b>한 번의 변경이다.</b> 줄과 꼬리를 따로 쓰면 되돌리기가 둘로 쪼개져, 한 번 물렀을 때
    /// <c>BeginIf</c>만 남은 <b>짝 없는 상태</b>가 판에 선다.
    ///
    /// ⚠ 첫 줄의 기존 전환은 <b>덮는다</b>. 덮어도 되는 모양인지는 부르는 쪽
    /// (<see cref="Chapters.BranchEntryCondition"/>)이 먼저 재고 온다.
    /// </summary>
    public void SetEntryCondition(string nodeId, string? conditionId)
    {
        DialogueNode node = RequireDialogue(nodeId);

        if (Project.FindScript(node.ScriptId)?.ActiveLines.FirstOrDefault() is not { } first)
        {
            throw new InvalidOperationException(
                "대본이 비어 있어 조건을 걸 자리가 없습니다 — 첫 줄을 먼저 쓰세요.");
        }

        DialogueLineExtension? existing = node.FindExtension(first.Id);

        Mutate(() =>
        {
            WriteLineTransitions(
                node,
                first.Id,
                existing,
                conditionId is null ? [] : [LineConditionTransition.BeginIf(conditionId)]);

            node.TrailingTransitions.Clear();

            if (conditionId is not null)
            {
                node.TrailingTransitions.Add(LineConditionTransition.EndIf());
            }
        });
    }

    private void SetLineTransitionLegacy(string nodeId, string lineId, LineConditionTransition? transition)
    {
        DialogueNode node = RequireDialogue(nodeId);

        if (Project.FindScript(node.ScriptId)?.FindLine(lineId) is not { IsRetired: false })
        {
            return;
        }

        DialogueLineExtension? existing = node.FindExtension(lineId);

        if (existing?.Transition is null && transition is null)
        {
            return;
        }

        // 옵션 라벨 전환은 안정 Id를 가져야 한다. 이미 이 줄이 옵션이었다면 그 Id를 잇는다 —
        // 라벨 전환 종류를 바꿨다고 정체성이 바뀌면 순서 경고(C3)가 성립하지 않는다.
        if (transition is { OpensOption: true, OptionId: null })
        {
            transition = new LineConditionTransition(
                transition.Kind,
                optionId: existing?.Transition?.OptionId ?? Identifier.Option());
        }

        Mutate(() =>
        {
            DialogueLineExtension extension = existing ?? new DialogueLineExtension(lineId);

            if (existing is null)
            {
                node.LineExtensions.Add(extension);
            }

            extension.Transition = transition;

            // 순서가 중요하다. 먼저 주인 없는 출구를 버리고, 그다음에 빈 항목을 버린다.
            // 반대로 하면 아직 출구가 매달려 있어 빈 항목이 남는다.
            PruneBranchExits(node);
            PruneLineExtensions(node);
        });
    }

    // ── 출구 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 포트 하나의 대상 노드를 정한다. null이면 연결을 끊는다.
    /// 그래프에서 간선을 끌든 노드 화면에서 드롭다운을 고르든 결국 이 하나를 부른다.
    /// </summary>
    public void SetExitTarget(ExitPort port, string? targetNodeId)
    {
        ArgumentNullException.ThrowIfNull(port);
        SetExitTarget(port.NodeId, port.Kind, port.ExitKey, targetNodeId);
    }

    public void SetExitTarget(
        string nodeId,
        ExitPortKind kind,
        string? branchOpenLineId,
        string? targetNodeId)
    {
        StoryNode? node = Project.FindNode(nodeId);

        if (node is null)
        {
            return;
        }

        if (node is not DialogueNode)
        {
            // 실행 출구는 DialogueNode만 가진다. SetNode는 공급자라 실행 흐름을 정하지 않는다.
            return;
        }

        if (targetNodeId is not null && Project.FindNode(targetNodeId) is null)
        {
            return;
        }

        if (kind == ExitPortKind.Default)
        {
            // ⛔ ~~커스텀(자유) 노드의 기본 출구 쓰기는 조용히 거절한다~~ (2026-08-21) —
            //    <b>걷혔다</b> (R7 P-6 · 결정 ⑤ · 2026-09-17). 그 거절이 곧 엑셀노드/자유노드의
            //    구분이었고, 같은 규칙이 `NodeConnections`와 `EffectiveDefaultExit`에도
            //    한 벌씩 더 있었다 — 셋을 함께 걷었다.
            if (!string.Equals(node.DefaultExitTargetNodeId, targetNodeId, StringComparison.Ordinal))
            {
                Mutate(() =>
                {
                    node.DefaultExitTargetNodeId = targetNodeId;
                    ForgetShapeIfCut(node, kind, branchOpenLineId, targetNodeId);
                });
            }

            return;
        }

        if (node is not DialogueNode dialogue || branchOpenLineId is null)
        {
            return;
        }

        // 「조건 분기」 표식의 목표는 <b>그 줄</b>이 진다 (R7 P-5) — 갈래 출구 장부에 적으면
        // 여는 전환이 없는 고아가 되고, 저장할 때 그 칸이 전환 안에 있어 통째로 사라진다.
        if (kind == ExitPortKind.Detour)
        {
            DialogueLineExtension extension = dialogue.RequireExtension(branchOpenLineId);

            if (!string.Equals(extension.DetourTargetNodeId, targetNodeId, StringComparison.Ordinal))
            {
                Mutate(() =>
                {
                    extension.DetourTargetNodeId = targetNodeId;
                    ForgetShapeIfCut(dialogue, kind, branchOpenLineId, targetNodeId);
                });
            }

            return;
        }

        // 선택지 출구(v9)는 문구가 열쇠다 — 대본의 줄에 매이지 않는다.
        Dictionary<string, string> exits =
            kind == ExitPortKind.Choice ? dialogue.ChoiceExits : dialogue.BranchExits;

        Mutate(() =>
        {
            if (targetNodeId is null)
            {
                exits.Remove(branchOpenLineId);
            }
            else
            {
                exits[branchOpenLineId] = targetNodeId;
            }

            ForgetShapeIfCut(dialogue, kind, branchOpenLineId, targetNodeId);
        });
    }

    /// <summary>끊긴 출구의 선 모양은 걷는다 — 선이 없는데 모양만 파일에 남으면, 같은 자리를 다시 이을 때 옛 굽이가 되살아난다.</summary>
    private static void ForgetShapeIfCut(StoryNode node, ExitPortKind kind, string? exitKey, string? targetNodeId)
    {
        if (targetNodeId is null)
        {
            node.EdgeShapes.Remove(EdgeShape.KeyOf(kind, exitKey));
        }
    }

    /// <summary>
    /// <b>실행 선의 모양을 정한다</b> (G3 · 2026-10-29 소유자: "곡선이나 그런 걸 조절할 수 있도록 해줘. 피그잼처럼").
    /// 꼴을 바꾸거나 손잡이를 끌어 놓을 때 한 번 — 되돌리기 한 칸이다. null이나 기본 모양이면 자동으로 돌아간다.
    /// 이어지지 않은 출구는 그을 선이 없어 아무것도 적지 않고, 바뀌는 것이 없어도 적지 않는다.
    /// </summary>
    public void SetExitShape(ExitPort exit, EdgeShape? shape)
    {
        ArgumentNullException.ThrowIfNull(exit);

        if (!exit.IsConnected || Project.FindNode(exit.NodeId) is not { } node)
        {
            return;
        }

        string key = EdgeShape.KeyOf(exit);
        EdgeShape? wanted = shape is null || shape.IsDefault ? null : shape;

        if (Equals(node.EdgeShapes.GetValueOrDefault(key), wanted))
        {
            return;
        }

        Mutate(ProjectChangeKind.Layout, () =>
        {
            if (wanted is null)
            {
                node.EdgeShapes.Remove(key);
            }
            else
            {
                node.EdgeShapes[key] = wanted;
            }
        });
    }

    // ── 설정 공급 연결 ──────────────────────────────────────────────────────
    //
    // ⛔ 링크를 만들고 끊고 켜던 편집(AddSettingsLink · RemoveLink · SetLinkEnabled)은 2026-10-29에 걷혔다
    //    (legacy-cleanup.md L-D2). 조건 공급은 2026-08-17부터 판 전체라 링크를 아무도 읽지 않는다. 옛 파일의 링크
    //    데이터는 그대로 읽고 쓰고, 노드를 지우면 그 노드를 가리키던 링크도 함께 걷힌다(RemoveReferencesToNode).

    // ── 조건 ────────────────────────────────────────────────────────────────

    public ConditionDefinition AddCondition(string setNodeId, string name, string expression)
    {
        var setNode = Project.FindNode(setNodeId) as SetNode
            ?? throw new InvalidOperationException($"'{setNodeId}'는 설정 노드가 아닙니다.");

        var condition = new ConditionDefinition { Name = name, Expression = expression };
        Mutate(() => setNode.Conditions.Add(condition));
        return condition;
    }

    public void UpdateCondition(string conditionId, string name, string expression)
    {
        ConditionDefinition? condition = Project.FindCondition(conditionId);

        if (condition is null ||
            (string.Equals(condition.Name, name, StringComparison.Ordinal) &&
             string.Equals(condition.Expression, expression, StringComparison.Ordinal)))
        {
            return;
        }

        // 조건 행의 개수나 순서는 그대로다. 현재 SetNode 입력 컨트롤을 다시 만들면
        // LostFocus로 커밋하는 순간 다음 클릭 대상이 사라진다. 대신 조건 이름을 사용하는
        // 그래프 라벨과 Dialogue 드롭다운만 새 값을 읽도록 별도 변경 종류를 알린다.
        Mutate(ProjectChangeKind.ConditionDefinition, () =>
        {
            condition.Name = name;
            condition.Expression = expression;
        });
    }

    /// <summary>
    /// 조건을 지운다. 그 조건을 쓰던 줄의 전환은 남겨 둔다.
    /// 지우면 작가가 쓴 갈래 구조가 조용히 무너지기 때문이다.
    /// 대신 <see cref="ConditionFlowResolver"/>가 "알 수 없는 조건"으로 알린다.
    /// </summary>
    public void RemoveCondition(string conditionId)
    {
        SetNode? owner = Project.EnumerateNodes().OfType<SetNode>()
            .FirstOrDefault(node => node.Conditions.Any(
                item => string.Equals(item.Id, conditionId, StringComparison.Ordinal)));

        if (owner is null)
        {
            return;
        }

        Mutate(() => owner.Conditions.RemoveAll(
            item => string.Equals(item.Id, conditionId, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 그 판(챕터)의 <b>설정 노드</b>를 보장한다 (2026-08-17 소유자: "조건 노드라기보다는
    /// 챕터별로 자동으로 저장되는 컨트롤러에 가까운 것 같아. 챕터 하나에 저런 설정을 담아두는
    /// 노드가 상시로 뜨는거지").
    ///
    /// 공급 범위가 판 전체가 된 뒤로 설정노드는 <b>여러 개일 이유가 없다</b> — 어차피 다 같은
    /// 곳에 미치므로 나눠 놓으면 "어디에 적었더라"만 생긴다. 그래서 챕터당 하나가 자동으로
    /// 서고, 작가는 그 안을 채울 뿐 노드를 만들거나 지우지 않는다.
    /// </summary>
    /// <returns>그 판의 설정 노드. 이미 있으면 그대로 돌려준다(파일을 건드리지 않는다).</returns>
    public SetNode EnsureChapterSettingsNode(string fileId)
    {
        StoryFile file = RequireFile(fileId);

        // 챕터 조건 공급 노드(A계층 배관)는 세지 않는다 — 그건 기획자 자료를 나르는 자리다.
        SetNode? existing = file.Nodes.OfType<SetNode>()
            .FirstOrDefault(node => !Chapters.ChapterBoardSupply.IsConditionSupplyNode(node, file));

        return existing ?? AddSetNode(fileId, name: ChapterSettingsNodeName(file.Name));
    }

    /// <summary>챕터 설정 노드의 이름 규약. 판 이름 = 챕터 Id다.</summary>
    public static string ChapterSettingsNodeName(string chapterId) => $"{chapterId} 설정";

    /// <summary>
    /// 그 챕터의 <b>판</b>을 보장한다 — 없으면 만들고, 설정 노드까지 세워 돌려준다
    /// (챕터 = 판 1:1, G-1 v2).
    ///
    /// <b>왜 여기 있나 (2026-08-23)</b> — 이 함수는 오래 `AuthoringSession`에 살았는데,
    /// <b>세션 상태를 하나도 안 본다.</b> 열려 있는 파일도, 선택도, 저장 여부도 안 묻고
    /// <see cref="Project"/>와 자기 자신만 쓴다. 세션에 주차돼 있던 순수한 편집 작업이라,
    /// 에피소드 동기화를 화면 밖으로 꺼내려던 시도가 여기서 막혔다 — 도메인이 셸의
    /// 메서드를 불러야 했기 때문이다.
    ///
    /// 왼쪽 챕터 목록 클릭과 에피소드 동기화가 <b>같은 규칙 하나</b>를 쓴다.
    /// </summary>
    /// <returns>그 챕터 판의 파일 Id. 이미 있으면 그대로다.</returns>
    public string EnsureChapterBoard(string chapterId)
    {
        StoryFile? standing = Chapters.EpisodeNaming.BoardOf(Project, chapterId);
        StoryFile board = standing ?? AddStoryFile(chapterId);

        // 챕터마다 설정 노드 하나가 상시로 선다 (2026-08-17 소유자) — 작가가 만들고 지우는
        // 것이 아니라 챕터에 딸린 자리다.
        EnsureChapterSettingsNode(board.Id);

        // 판이 <b>막 섰으면</b> 챕터 조건이 함께 공급된다 (2026-10-29 · D2). 예전에는 이 일을
        // 엑셀 [대본 가져오기]가 했다 — 그 단추가 사라진 지금, 조건을 먼저 적고 에피소드를
        // 나중에 만든 챕터는 여기서 받지 않으면 대사 노드의 조건 목록이 비어 있게 된다.
        if (standing is null)
        {
            SupplyChapterConditions(chapterId);
        }

        return board.Id;
    }

    // ── 되돌리기 ────────────────────────────────────────────────────────────

    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Add(ProjectSnapshotCodec.Encode(Project));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Add(ProjectSnapshotCodec.Encode(Project));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    /// <summary>파일에서 새로 읽었을 때처럼, 기록을 버리고 통째로 갈아 끼운다.</summary>
    public void Replace(StoryProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        _undo.Clear();
        _redo.Clear();
        Raise(ProjectChangeKind.Structure);
    }

    private void Restore(string snapshot)
    {
        Project = ProjectSnapshotCodec.Decode(snapshot);
        Raise(ProjectChangeKind.Structure);
    }

    private void Mutate(Action change) => Mutate(ProjectChangeKind.Structure, change);

    private void Mutate(ProjectChangeKind kind, Action change)
    {
        // 묶음 안이면 스냅샷은 묶음이 처음에 한 번 떠 두었다 — 여기서 또 뜨면 되돌리기가
        // 명령 수만큼 쪼개진다.
        if (_batchDepth == 0)
        {
            PushUndo(ProjectSnapshotCodec.Encode(Project));
        }

        change();
        Raise(kind);
    }

    private void PushUndo(string snapshot)
    {
        _undo.Add(snapshot);

        if (_undo.Count > MaxHistory)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    /// <summary>지금 열려 있는 묶음의 깊이. 0이면 묶음 밖이다.</summary>
    private int _batchDepth;

    /// <summary>묶음 안에서 일어난 변경의 종류. 아무것도 안 바뀌었으면 null이다.</summary>
    private ProjectChangeKind? _batchKind;

    /// <summary>
    /// 여러 명령을 <b>되돌리기 한 칸 · 알림 한 번</b>으로 묶는다 (2026-10-29 · D2).
    ///
    /// <b>왜 필요해졌나</b> — 엑셀 [대본 가져오기]가 걷히며 그 안에 숨어 있던 일(챕터 조건을 판에
    /// 공급하는 일)이 <b>조건을 고치는 그 명령</b>으로 옮겨 왔다. 조건 하나를 고쳤는데 되돌리기가
    /// 두 번이어야 하면 사람은 첫 번째에서 "안 돌아갔다"고 본다. 공급은 남의 명령
    /// (<see cref="AddSetNode"/>·<see cref="AddCondition"/>)을 부르므로, 짓기와 붙이기를 가르는
    /// 예전 방식(<c>NewDialogueNodeCore</c>)으로는 감당이 안 된다.
    ///
    /// ⚠ 안에서 예외가 나면 <b>묶음 전 상태로 되돌리고</b> 다시 던진다 — 반쯤 된 묶음이 남으면
    /// 되돌리기 한 칸이 무엇을 되돌리는지 아무도 모른다.
    ///
    /// ⚠ 묶음 안에서 아무것도 안 바뀌었으면 되돌리기 칸도 알림도 없다.
    /// </summary>
    public void Batch(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (_batchDepth > 0)
        {
            change();
            return;
        }

        string before = ProjectSnapshotCodec.Encode(Project);
        _batchDepth++;
        _batchKind = null;
        bool completed = false;

        try
        {
            change();
            completed = true;
        }
        finally
        {
            _batchDepth--;
            ProjectChangeKind? kind = _batchKind;
            _batchKind = null;

            if (kind is { } changed)
            {
                if (completed)
                {
                    PushUndo(before);
                    Raise(changed);
                }
                else
                {
                    Restore(before);
                }
            }
        }
    }

    /// <summary>
    /// 프로젝트가 <b>실제로 바뀐 횟수</b> (2026-08-24 성능). 두 시점의 값이 같으면 그
    /// 사이에 아무 일도 없었다 — 무슨 일이 있었는지는 몰라도 <em>없었다</em>는 것은 안다.
    ///
    /// <b>왜 필요했나</b> — 에피소드 동기화는 워크북을 반영할 때마다 셸에 "다시 그려라"고
    /// 알렸는데, 그 근거로 쓰던 <c>EpisodeSyncReport.Applied</c>는 <b>"반영을 돌렸다"</b>는
    /// 뜻이지 <b>"뭔가 달라졌다"</b>는 뜻이 아니다(`ChapterBoardSupplyTests`의
    /// `같은_워크북을_두_번_동기화하면_두_번째는_변경이_없다`가 그 사실을 이미 못 박아
    /// 두었다 — 두 번째도 <c>Applied</c>가 참이다). 그래서 아무것도 안 바뀐 동기화가
    /// 매번 전체 다시 그리기를 방송했고, 그 순간 사람이 <b>타이핑하던 칸이 파괴됐다.</b>
    ///
    /// 각 변경을 일일이 세는 대신 여기 하나를 둔다: 새 편집 명령이 생겨도 <c>Mutate</c>를
    /// 지나는 한 저절로 세어진다. 빠뜨릴 수 있는 목록을 만들지 않는다.
    /// </summary>
    public long Revision { get; private set; }

    private void Raise(ProjectChangeKind kind)
    {
        // 묶음 안이면 알림은 묶음이 끝날 때 한 번이다. 종류가 섞이면 구조로 올린다 —
        // 덜 그려서 낡은 화면이 남는 것보다 한 번 더 그리는 편이 낫다.
        if (_batchDepth > 0)
        {
            _batchKind = _batchKind is null || _batchKind == kind ? kind : ProjectChangeKind.Structure;
            return;
        }

        Revision++;
        Changed?.Invoke(this, new ProjectChangedEventArgs(kind));
    }

    /// <summary>
    /// 더 이상 갈래를 열지 않는 줄에 매달린 조건 출구를 버린다.
    /// 남겨 두면 그래프에 주인 없는 간선이 생기고, 그 간선은 어느 화면에서도 지울 수 없다.
    ///
    /// 대본에서 사라진 줄의 출구는 여기서 지우지 않는다. 그것은 <b>고아</b>이지 쓰레기가 아니다.
    /// 대본을 되돌리면 살아나야 하고, 그때까지는 진단으로 보인다.
    /// </summary>
    /// <summary>
    /// 주인 없는 출구를 버린다 — 갈래가 사라졌으면 거기 매달린 배선도 갈 곳이 없다.
    ///
    /// ⚠ <b>신원을 흐름 해석과 같은 규칙으로 짓는다</b>(<see cref="Flow.BranchAnchor"/>).
    /// 예전에는 여기가 <c>extension.Transition</c>(첫 전환) 하나만 보고 <b>맨 줄 Id</b>만
    /// 살렸다. 그래서 (ㄱ) 첫 전환이 여는 것이 아닌 줄의 출구가 통째로 지워졌고,
    /// (ㄴ) 한 줄이 갈래를 여럿 열 때 둘째부터의 출구가 매달자마자 사라졌다.
    /// </summary>
    private static void PruneBranchExits(DialogueNode node)
    {
        var opening = new HashSet<string>(StringComparer.Ordinal);

        foreach (DialogueLineExtension extension in node.LineExtensions)
        {
            int ordinal = 0;

            foreach (LineConditionTransition transition in extension.Transitions)
            {
                if (transition.OpensBranch)
                {
                    opening.Add(Flow.BranchAnchor.ForLine(extension.LineId, ordinal++));
                }
            }
        }

        // 대본 끝의 줄 없는 갈래들 (2026-08-24).
        int trailing = 0;

        foreach (LineConditionTransition transition in node.TrailingTransitions)
        {
            if (transition.OpensBranch)
            {
                opening.Add(Flow.BranchAnchor.ForTrailing(trailing++));
            }
        }

        foreach (string key in node.BranchExits.Keys.Where(key => !opening.Contains(key)).ToList())
        {
            node.BranchExits.Remove(key);
        }
    }

    /// <summary>아무것도 담지 않게 된 확장 항목을 버린다. 빈 항목은 파일만 시끄럽게 한다.</summary>
    private static void PruneLineExtensions(DialogueNode node)
    {
        node.LineExtensions.RemoveAll(extension =>
            extension.IsEmpty && !node.BranchExits.ContainsKey(extension.LineId));
    }

    private void RemoveReferencesToNode(string nodeId)
    {
        // 사라진 노드를 가리키던 출구를 남겨 두면 그래프에 끊어진 간선이 생긴다.
        Project.Links.RemoveAll(link =>
            string.Equals(link.SourceNodeId, nodeId, StringComparison.Ordinal) ||
            string.Equals(link.TargetNodeId, nodeId, StringComparison.Ordinal));

        foreach (StoryNode other in Project.EnumerateNodes())
        {
            if (string.Equals(other.DefaultExitTargetNodeId, nodeId, StringComparison.Ordinal))
            {
                other.DefaultExitTargetNodeId = null;
            }

            if (other is not DialogueNode dialogue)
            {
                continue;
            }

            foreach (string key in dialogue.BranchExits
                         .Where(pair => string.Equals(pair.Value, nodeId, StringComparison.Ordinal))
                         .Select(pair => pair.Key)
                         .ToList())
            {
                dialogue.BranchExits.Remove(key);
            }

            foreach (string key in dialogue.ChoiceExits
                         .Where(pair => string.Equals(pair.Value, nodeId, StringComparison.Ordinal))
                         .Select(pair => pair.Key)
                         .ToList())
            {
                dialogue.ChoiceExits.Remove(key);
            }
        }
    }

    private StoryFile RequireFile(string fileId)
    {
        return Project.FindFile(fileId)
            ?? throw new InvalidOperationException($"StoryFile '{fileId}'를 찾을 수 없습니다.");
    }

    private DialogueNode RequireDialogue(string nodeId)
    {
        return Project.FindDialogue(nodeId)
            ?? throw new InvalidOperationException($"'{nodeId}'는 대사 노드가 아닙니다.");
    }

    private string NextFileName()
    {
        return $"파일 {Project.Files.Count + 1}";
    }

    private string NextName(string prefix)
    {
        int count = Project.EnumerateNodes().Count(node =>
            node.Name.StartsWith(prefix, StringComparison.Ordinal)) + 1;

        return $"{prefix} {count}";
    }

    /// <summary>
    /// 그 챕터에서 <b>아직 안 쓰인 자리표시 EpisodeId</b> — <c>new01</c>, <c>new02</c>…
    ///
    /// ⛔ 이 규약은 2026-09-18까지 <b>화면 둘에 손으로 복사돼</b> 있었고(챕터 그래프·대본 탭),
    /// 연출 그래프는 아예 다른 규약(<c>장면 3</c>)을 썼다 — 같은 행동이 창구마다 다른 Id를
    /// 만들었다. EpisodeId는 진행 JSON과 산출물의 <b>열쇠</b>로 나가므로(엑셀 시절에는 대본 워크북의
    /// 파일 이름이었다) 공백이 든 Id는 특히 나쁘다.
    ///
    /// ⚠ 사람이 고칠 것을 전제한 이름이다 — 이름을 정하는 것은 기획자의 일이고, 여기서는
    /// <b>겹치지 않는 자리</b>만 준다.
    /// </summary>
    /// <summary>
    /// 그 챕터의 다음 자리표시 Id. <b>화면이 손수 세지 않게</b> 열어 둔다 — 세는 규칙이
    /// 화면마다 있으면 하나가 다른 목록을 훑다가 겹치는 Id를 내고, 그러면 <c>AddEpisode</c>가
    /// *"이미 있습니다"*로 던진다.
    /// </summary>
    public string NextEpisodeId(string chapterId) => NextEpisodeId(RequireChapter(chapterId));

    private static string NextEpisodeId(Chapters.ChapterDocument chapter)
    {
        int number = 1;

        while (chapter.Episodes.Any(episode =>
                   string.Equals(episode.EpisodeId, $"new{number:D2}", StringComparison.Ordinal)))
        {
            number++;
        }

        return $"new{number:D2}";
    }
}
