using Vn.Authoring.Model;
using Vn.Authoring.Chapters;

namespace Vn.Authoring.Editing;

/// <summary>
/// <b>챕터를 바꾸는 코드는 이 파일 안에만 있다</b>.
///
/// <c>ProjectEditor.Scripts.cs</c>가 대사에 대해 하는 일을 챕터 구조에 대해 한다 — 그래야
/// <i>"한 값을 고쳤을 때 권위 있는 데이터가 하나만 바뀐다"</i>가 코드 구조로 보장된다.
///
/// <b>여기는 파일을 모른다.</b> 고치는 것은 <b>모델</b>뿐이라 열 번호·시트 이름·행 삭제 같은
/// 파일 사정이 부르는 쪽까지 스미지 않는다.
///
/// 간선의 신원은 (출발, 도착, 문구)이고,
/// 개명은 간선·픽스처를 함께 끌고 가며, 삭제는 끝점 간선을 함께 걷고, <b>선택지 사전은
/// 건드리지 않는다</b>(어휘집이지 배선이 아니다).
///
/// 실패는 <see cref="InvalidOperationException"/>이다. 부르는 쪽이
/// <c>UiGuard</c>로 받아 상태줄에 말한다.
/// </summary>
public sealed partial class ProjectEditor
{
    /// <summary>그 Id의 챕터. 없으면 null — 부르는 쪽이 "아직 안 들여왔다"를 구분한다.</summary>
    public ChapterDocument? FindChapter(string chapterId) =>
        Project.Chapters.FirstOrDefault(chapter =>
            string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal));

    private ChapterDocument RequireChapter(string chapterId) =>
        FindChapter(chapterId)
        ?? throw new InvalidOperationException($"챕터 '{chapterId}'가 프로젝트에 없습니다.");

    /// <summary>빈 챕터 하나를 세운다. 같은 Id가 이미 있으면 그것을 돌려준다(멱등).</summary>
    public ChapterDocument EnsureChapter(string chapterId)
    {
        if (FindChapter(chapterId) is { } existing)
        {
            return existing;
        }

        var chapter = new ChapterDocument { ChapterId = chapterId };
        Mutate(() => Project.Chapters.Add(chapter));

        return chapter;
    }

    /// <summary>
    /// 챕터와 <b>그 판</b>을 한 번의 변경으로 걷는다 — 되돌리기 한 번에 둘 다 돌아온다.
    /// </summary>
    /// <returns>판과 함께 걷힌 노드 수. 지울 것이 없었으면 0.</returns>
    /// <remarks>
    /// 챕터는 <c>Project.Chapters</c>에 산다 — 판만 걷으면
    /// <b>목록·트리·검증에 그대로 남는다</b>.
    /// </remarks>
    public int RemoveChapterWithBoard(string chapterId)
    {
        if (FindChapter(chapterId) is not { } chapter)
        {
            return 0;
        }

        StoryFile? board = BoardOf(chapterId);   // 아직 판을 안 세운 챕터도 있다

        // ⛔ 「마지막 판은 지우지 않는다(새 노드가 갈 곳이 없어진다)」는 2026-11-17에 걷었다 — 판이 없으면 [＋ 에피소드]가 챕터와
        //    장면을 세운다(낙서판을 걷으며 새 프로젝트도 판 없이 시작한다).

        int nodes = board?.Nodes.Count ?? 0;

        Mutate(() =>
        {
            Project.Chapters.Remove(chapter);

            if (board is not null)
            {
                RemoveFileCore(board);
            }
        });

        return nodes;
    }

    /// <summary>
    /// 챕터 Id를 바꾸고, 그 이름을 지고 있는 모델 둘 — 판(<c>StoryFile.Name</c>, 챕터 = 판 1:1)과
    /// 조건 공급 설정노드(<c>챕터 {Id} 조건</c>) — 를 <b>한 번의 변경</b>으로 함께 바꾼다.
    /// 되돌리기 한 번에 셋이 돌아온다.
    ///
    /// <b>개명의 주인은 모델이다</b>. <c>Project.Chapters</c>의 Id와 판·배관이 함께 바뀌어야
    /// 저장하고 다시 열었을 때 챕터와 판의 이름이 갈리지 않는다. 파일 이름 바꾸기는 후속 출력의 일이다.
    ///
    /// </summary>
    /// <returns>판·배관의 이름이 실제로 따라갔는가(없었으면 거짓).</returns>
    public ChapterRenameEffect RenameChapter(string oldId, string newId)
    {
        ChapterDocument chapter = RequireChapter(oldId);

        if (FindChapter(newId) is not null)
        {
            throw new InvalidOperationException($"챕터 '{newId}'가 이미 있습니다.");
        }

        StoryFile? board = EpisodeNaming.BoardOf(Project, oldId);

        string newSupplyName = ChapterBoardSupply.ConditionSupplyNodeName(newId);

        // 그 판의 공급 노드(설정 노드는 그것 하나뿐이다 — 저장 검증이 지킨다).
        SetNode? supply = board?.Nodes.OfType<SetNode>().SingleOrDefault();

        Mutate(() =>
        {
            chapter.ChapterId = newId;

            if (board is not null)
            {
                board.Name = newId;
            }

            if (supply is not null)
            {
                supply.Name = newSupplyName;
            }
        });

        return new ChapterRenameEffect(board is not null, supply is not null);
    }

    // ── 에피소드 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 에피소드 하나를 세운다 — 카드(이름 = EpisodeId)와 한 칸에.
    /// </summary>
    /// <param name="sceneId">
    /// 어느 장면에 놓을지 — 에피소드는 늘 장면에 속한다(비면 거절 · old-shapes O7). <b>만들면서 함께 정한다</b>.
    /// </param>
    public ChapterEpisode AddEpisode(
        string chapterId, string episodeId, string title, double x, double y, string sceneId)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        if (FindEpisode(chapter, episodeId) is not null)
        {
            throw new InvalidOperationException($"EpisodeId '{episodeId}'가 이미 있습니다.");
        }

        (_, ChapterEpisode? episode, Action attach) = NewEpisodeCore(
            chapterId, episodeId, title, x, y, sceneId);

        Mutate(attach);
        return episode!;
    }

    /// <summary>
    /// <b>에피소드 하나를 짓기만 한다</b> — 카드·대본까지 함께, <see cref="Mutate"/> 밖에서.
    ///
    /// 「에피소드에는 대사노드가 있다」는 <b>화면이 아니라 편집기 명령</b>이 지킨다. 화면마다
    /// 노드 만들기를 한 줄 더 부르게 하면 그 한 줄을 잊은 화면에서 만든 에피소드만 쓸 자리가 없다.
    ///
    /// <b>창구를 늘릴 때 잊을 수 있는 자리를 없앤 것</b>이 요점이다. 에피소드를 만드는
    /// 길은 전부 여기를 지나므로, 어디서 만들었느냐가 결과를 바꾸지 않는다.
    ///
    /// <see cref="EnsureChapterBoard"/>가 판을 <b>먼저</b> 세운다(없을 때만). 판이 없는
    /// 챕터에 에피소드를 넣는 것은 카드를 놓을 자리가 없다는 뜻이다.
    /// </summary>
    private (DialogueNode Node, ChapterEpisode? Episode, Action Attach) NewEpisodeCore(
        string chapterId, string episodeId, string title, double x, double y, string sceneId)
    {
        string fileId = EnsureChapterBoard(chapterId);

        // <b>자리가 둘이다.</b> 주어진 (x, y)는 <b>챕터 그래프</b>의 자리이고, 연출 판의
        //    카드는 제 판의 규칙(<see cref="Graph.NodePlacement"/>)으로 선다.
        //    한쪽 좌표를 다른 판에 그대로 넣으면 사람이 맞춰 둔 배치가 흐트러진다.
        (double cardX, double cardY) = Graph.NodePlacement.For(Project, chapterId, episodeId);

        return NewDialogueNodeCore(
            fileId,
            cardX,
            cardY,
            name: episodeId,
            sceneId: sceneId,
            title: title,
            episodePosition: (x, y));
    }

    /// <summary>속성 패널의 저장 — null이 아닌 것만 바꾼다. 장면ID는 비울 수 없다(에피소드는 늘 장면에 속한다).</summary>
    public void UpdateEpisode(
        string chapterId,
        string episodeId,
        string? title = null,
        string? memo = null,
        string? sceneId = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEpisodeIndex(chapter, episodeId);

        if (sceneId is not null && string.IsNullOrWhiteSpace(sceneId))
        {
            throw new InvalidOperationException("장면ID는 비울 수 없습니다 — 에피소드는 늘 장면에 속합니다.");
        }

        // 제목만 바꾸면 그 카드의 제목 변경이다 — 판은 그 카드의 제목 글만 고친다(usability UX15 · ⛔ 구조 — 판을 통째로).
        bool titleOnly = memo is null && sceneId is null && EpisodeNaming.CardFor(Project, chapterId, episodeId) is not null;

        Mutate(titleOnly ? ProjectChangeKind.CardTitle : ProjectChangeKind.Structure, () => chapter.Episodes[index] = chapter.Episodes[index] with
        {
            Title = title ?? chapter.Episodes[index].Title,
            Memo = memo ?? chapter.Episodes[index].Memo,
            SceneId = sceneId?.Trim() ?? chapter.Episodes[index].SceneId
        }, titleOnly ? EpisodeNaming.CardFor(Project, chapterId, episodeId)!.Id : null);
    }

    /// <summary>그 챕터의 판 — 규칙은 <see cref="EpisodeNaming.BoardOf"/> 하나다.</summary>
    private StoryFile? BoardOf(string chapterId) => EpisodeNaming.BoardOf(Project, chapterId);

    /// <summary>
    /// 장면ID 바꾸기 — 그 장면의 에피소드 모두에 새 장면ID를 적는다. <b>비울 수 없다</b> — 에피소드는 늘 장면에 속한다(2026-11-17 ·
    /// ⛔ 전에는 비우면 장면을 풀어 저마다 제 장면이 됐다). 되돌리기 한 칸이고, <b>장면 메모를 데려간다</b>(같은 칸 안에서 열쇠를 옮긴다):
    /// <list type="bullet">
    /// <item>새 장면ID에 메모가 없으면 옛 메모가 새 열쇠로 간다(바꾸기 · 메모 없는 장면으로 합치기).</item>
    /// <item>받는 장면에 이미 메모가 있으면 그것을 두고, 보낸 장면의 메모는 옛 열쇠에 <b>주인 없이</b> 남는다.</item>
    /// </list>
    /// </summary>
    /// <returns>장면ID가 바뀐 에피소드 수 — 그 장면의 에피소드가 없으면 0이고 아무것도 하지 않는다.</returns>
    public int RenameScene(string chapterId, string sceneId, string name)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string wanted = name.Trim();

        if (wanted.Length == 0)
        {
            throw new InvalidOperationException("장면ID는 비울 수 없습니다 — 에피소드는 늘 장면에 속합니다.");
        }

        List<int> members = Enumerable.Range(0, chapter.Episodes.Count)
            .Where(index => string.Equals(chapter.Episodes[index].SceneId, sceneId, StringComparison.Ordinal))
            .ToList();

        if (members.Count == 0)
        {
            return 0;
        }

        Mutate(() =>
        {
            foreach (int index in members)
            {
                chapter.Episodes[index] = chapter.Episodes[index] with { SceneId = wanted };
            }

            CarrySceneNote(chapter, sceneId, wanted);
        });

        return members.Count;
    }

    // ── 장면 메모 ──────────────────────────────────────────────────────────
    // 장면이 아니라 장면에 붙는 곁 자료다(ChapterSceneNote). 열쇠는 SceneId.

    /// <summary>
    /// 장면 메모를 적는다 — 비우면 글을 지운다(이름은 그대로). 되돌리기 한 칸, 같은 값이면 칸을 쓰지 않는다.
    /// 앞뒤 빈칸은 걷는다(글 안의 줄바꿈은 그대로).
    /// </summary>
    public void SetSceneNote(string chapterId, string sceneId, string? text) =>
        UpdateSceneEntry(chapterId, sceneId, string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim(), name: null, setName: false);

    /// <summary>
    /// 장면 이름 — 작가가 [대본] 탭에서 붙이는 이름(SceneId는 그대로다 — 같은 이름의 장면이 여럿이어도 합쳐지지 않는다).
    /// 비우면 이름을 지운다(SceneId가 보인다). 되돌리기 한 칸, 같은 값이면 칸을 쓰지 않는다. 앞뒤 빈칸은 걷는다.
    /// </summary>
    public void SetSceneName(string chapterId, string sceneId, string? name) =>
        UpdateSceneEntry(chapterId, sceneId, text: null, string.IsNullOrWhiteSpace(name) ? null : name.Trim(), setName: true);

    /// <summary>장면 항목(이름 · 메모)을 통째로 지운다 — 주인 없는 항목의 [지우기]. 되돌리기 한 칸, 없으면 칸을 쓰지 않는다.</summary>
    public void RemoveSceneNote(string chapterId, string sceneId)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        if (!chapter.SceneNotes.Any(note => string.Equals(note.SceneId, sceneId, StringComparison.Ordinal)))
        {
            return;
        }

        Mutate(ProjectChangeKind.NodeMetadata, () =>
            chapter.SceneNotes.RemoveAll(note => string.Equals(note.SceneId, sceneId, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 장면 항목 하나를 고친다 — <paramref name="text"/>가 null이면 글을, <paramref name="setName"/>이 거짓이면 이름을 그대로 둔다.
    /// 둘 다 비면 항목을 지운다. 글자라 <c>NodeMetadata</c>로 알린다 — 인스펙터를 다시 세우면 쓰던 칸이 사라진다.
    /// </summary>
    private void UpdateSceneEntry(string chapterId, string sceneId, string? text, string? name, bool setName)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        if (string.IsNullOrWhiteSpace(sceneId))
        {
            throw new ArgumentException("장면 항목의 장면ID가 비었습니다.", nameof(sceneId));
        }

        int index = chapter.SceneNotes.FindIndex(note => string.Equals(note.SceneId, sceneId, StringComparison.Ordinal));
        ChapterSceneNote current = index >= 0 ? chapter.SceneNotes[index] : new ChapterSceneNote(sceneId, string.Empty);
        ChapterSceneNote wanted = current with
        {
            Text = text ?? (string.IsNullOrWhiteSpace(current.Text) ? string.Empty : current.Text),
            Name = setName ? name : (string.IsNullOrWhiteSpace(current.Name) ? null : current.Name)
        };

        bool same = string.Equals(Normal(current.Text), Normal(wanted.Text), StringComparison.Ordinal) &&
                    string.Equals(Normal(current.Name), Normal(wanted.Name), StringComparison.Ordinal);

        if (same)
        {
            return;
        }

        Mutate(ProjectChangeKind.NodeMetadata, () =>
        {
            if (ChapterSceneNotes.IsBlank(wanted))
            {
                chapter.SceneNotes.RemoveAt(index);
            }
            else if (index >= 0)
            {
                chapter.SceneNotes[index] = wanted;
            }
            else
            {
                chapter.SceneNotes.Add(wanted);
            }
        });

        static string? Normal(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// 에피소드 지시 — 그 에피소드의 <see cref="ChapterEpisode.Memo"/>. 장면 메모 공간의 에피소드 카드가 이것을 쓴다.
    /// 비우면 지운다(null). 카드는 그대로다 — 카드를 떼는 것은 <see cref="RemoveEpisodeNoteCard"/>다. 되돌리기 한 칸, 같은 값이면 칸을 쓰지 않는다. 앞뒤 빈칸은 걷는다.
    /// </summary>
    public void UpdateEpisodeMemo(string chapterId, string episodeId, string? memo)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEpisodeIndex(chapter, episodeId);
        string? wanted = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
        string? current = string.IsNullOrWhiteSpace(chapter.Episodes[index].Memo) ? null : chapter.Episodes[index].Memo;

        if (string.Equals(current, wanted, StringComparison.Ordinal))
        {
            return;
        }

        // 지시를 적으면 카드도 선다 — 그 뒤로 지시를 비워도 카드는 [빼기]로만 내린다.
        Mutate(ProjectChangeKind.EpisodeNote, () => chapter.Episodes[index] = chapter.Episodes[index] with
        {
            Memo = wanted,
            NoteCard = chapter.Episodes[index].NoteCard || wanted is not null
        });
    }

    /// <summary>
    /// 장면 메모 공간에 그 에피소드의 지시 카드를 붙인다 — 지시가 비어도 카드가 남는다(<see cref="ChapterEpisode.NoteCard"/>).
    /// 되돌리기 한 칸, 이미 붙었으면 칸을 쓰지 않는다.
    /// </summary>
    public void AddEpisodeNoteCard(string chapterId, string episodeId)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEpisodeIndex(chapter, episodeId);

        if (chapter.Episodes[index].NoteCard)
        {
            return;
        }

        Mutate(ProjectChangeKind.EpisodeNote, () => chapter.Episodes[index] = chapter.Episodes[index] with { NoteCard = true });
    }

    /// <summary>
    /// 지시 카드를 뗀다 — [빼기]. 카드 표시와 지시를 함께 지운다, 되돌리기 한 칸. 이미 없으면 칸을 쓰지 않는다.
    /// </summary>
    public void RemoveEpisodeNoteCard(string chapterId, string episodeId)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEpisodeIndex(chapter, episodeId);

        if (!chapter.Episodes[index].HasNoteCard)
        {
            return;
        }

        Mutate(ProjectChangeKind.EpisodeNote, () =>
            chapter.Episodes[index] = chapter.Episodes[index] with { NoteCard = false, Memo = null });
    }

    /// <summary>
    /// 장면의 열쇠가 바뀔 때 메모를 데려간다 — 받는 열쇠에 이미 메모가 있으면 그것을 두고 옛 것은 옛 열쇠에 남는다
    /// (주인 없는 메모가 된다). <c>Mutate</c> 안에서만 부른다.
    /// </summary>
    private static void CarrySceneNote(ChapterDocument chapter, string fromSceneId, string toSceneId)
    {
        if (string.Equals(fromSceneId, toSceneId, StringComparison.Ordinal))
        {
            return;
        }

        int from = chapter.SceneNotes.FindIndex(note => string.Equals(note.SceneId, fromSceneId, StringComparison.Ordinal));

        if (from < 0 || ChapterSceneNotes.Of(chapter.SceneNotes, toSceneId) is not null)
        {
            return;
        }

        chapter.SceneNotes.RemoveAll(note => string.Equals(note.SceneId, toSceneId, StringComparison.Ordinal));
        from = chapter.SceneNotes.FindIndex(note => string.Equals(note.SceneId, fromSceneId, StringComparison.Ordinal));
        chapter.SceneNotes[from] = chapter.SceneNotes[from] with { SceneId = toSceneId };
    }

    /// <summary>
    /// 에피소드의 자리 — 그래프에서 끌어 옮긴 결과.
    ///
    /// 자리는 <b>구조가 아니다</b> — 파생 그래프도 편집 행도 안 달라지므로
    /// <c>NodeMetadata</c>로 알린다. 화면이 통째로 다시 서지 않는다.
    ///
    /// <b>되돌리기 목록은 종류를 안 본다</b>. <c>Mutate</c>는 종류와
    /// 무관하게 스냅샷을 하나씩 쌓으므로, <b>포인터가 움직일 때마다 이것을 부르면</b>
    /// 되돌리기 목록이 픽셀 단위로 차 사람이 진짜 되돌리고 싶은 편집을 밀어낸다.
    /// (연출 그래프가 지금 그렇게 부르고 있다 — 그쪽은 따로 볼 일이다.)
    /// <b>끄는 동안은 화면만 움직이고, 놓을 때 한 번 부른다.</b>
    /// </summary>
    public void MoveEpisode(string chapterId, string episodeId, double x, double y)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEpisodeIndex(chapter, episodeId);

        Mutate(ProjectChangeKind.ChapterLayout, () =>
            chapter.Episodes[index] = chapter.Episodes[index] with
            {
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2)
            });
    }

    /// <summary>
    /// 여러 에피소드의 자리를 <b>한 번의 변경으로</b> 옮긴다 — 첫 자리 심기와 [자동 정렬]이
    /// 쓴다.
    ///
    /// <b>한 번인 것이 요점이다.</b> <see cref="MoveEpisode"/>를 스무 번 부르면 되돌리기도
    /// 스무 번이라, 정렬 한 번을 무르려고 Ctrl+Z를 스무 번 눌러야 한다. 사람이 한 동작으로
    /// 한 일은 한 번에 돌아와야 한다.
    ///
    /// 모르는 EpisodeId가 있으면 <b>던진다</b>. 조용히 건너뛰면 "정렬했는데 하나가 제자리"를
    /// 사람이 버그로 읽고, 그 자리를 찾을 단서가 없다.
    /// </summary>
    public void MoveEpisodes(
        string chapterId, IReadOnlyDictionary<string, (double X, double Y)> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        ChapterDocument chapter = RequireChapter(chapterId);

        // 자리(index)를 먼저 전부 확인한다 — 반쯤 옮기고 던지면 판이 어중간해진다.
        List<(int Index, double X, double Y)> moves = positions
            .Select(pair => (
                Index: RequireEpisodeIndex(chapter, pair.Key),
                X: Math.Round(pair.Value.X, 2),
                Y: Math.Round(pair.Value.Y, 2)))
            .ToList();

        if (moves.Count == 0)
        {
            return;
        }

        Mutate(ProjectChangeKind.ChapterLayout, () =>
        {
            foreach ((int index, double x, double y) in moves)
            {
                chapter.Episodes[index] = chapter.Episodes[index] with { X = x, Y = y };
            }
        });
    }

    /// <summary>
    /// EpisodeId 개명. <b>간선의 출발·도착과 픽스처 고정 선택, 카드의 표식과 이름이 함께 따라간다</b> — 신원이
    /// 바뀌었는데 참조가 남으면 유령 간선이 되고, 표식이 남으면 에피소드를 잃은 카드가 된다. 에피소드 카드의 이름은 늘 EpisodeId다
    /// (저장 검증이 지킨다 · old-shapes O4 · O5).
    /// </summary>
    public void RenameEpisode(string chapterId, string oldId, string newId)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEpisodeIndex(chapter, oldId);

        if (FindEpisode(chapter, newId) is not null)
        {
            throw new InvalidOperationException($"EpisodeId '{newId}'가 이미 있습니다.");
        }

        DialogueNode? card = EpisodeNaming.CardFor(Project, chapterId, oldId);

        Mutate(() =>
        {
            if (card is not null)
            {
                card.MarkedEpisodeId = newId;
                card.Name = newId;
            }

            chapter.Episodes[index] = chapter.Episodes[index] with { EpisodeId = newId };

            Retarget(chapter, oldId, newId);
        });
    }

    /// <summary>
    /// 에피소드 삭제 — 그 에피소드를 끝점으로 하는 간선과 픽스처 고정 선택, 그리고 <b>그 카드</b>(제 대본째)를 한 칸에 함께 걷는다. 에피소드와
    /// 카드는 한 칸에 서고 함께 지워진다(2026-11-18 · old-shapes O4 — ⛔ 그 전에는 행만 걷고 카드는 부르는 쪽이 따로 걷었다).
    ///
    /// <b>선택지 사전은 건드리지 않는다</b>(v9). 챕터 전체의 어휘라, 에피소드 하나가
    /// 사라졌다고 낱말을 지우면 다른 에피소드의 드롭다운에서도 사라진다.
    /// </summary>
    public void RemoveEpisode(string chapterId, string episodeId)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEpisodeIndex(chapter, episodeId);
        DialogueNode? card = EpisodeNaming.CardFor(Project, chapterId, episodeId);

        Mutate(() =>
        {
            chapter.Episodes.RemoveAt(index);

            chapter.Edges.RemoveAll(edge =>
                string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal) ||
                string.Equals(edge.ToEpisodeId, episodeId, StringComparison.Ordinal));

            Retarget(chapter, episodeId, null);

            if (card is not null && Project.FindFileContainingNode(card.Id) is { } board)
            {
                RemoveNodeCore(board, card);
            }
        });
    }

    /// <summary>
    /// 장면 지우기 — 그 장면의 에피소드를 <see cref="RemoveEpisode"/>와 같이 걷고(카드 · 간선 · 픽스처 참조) 장면 항목(이름 · 메모)도 걷는다.
    /// 그 에피소드들이 부르던 분기 카드도 걷는다 — 장면을 걷은 뒤 집을 잃는 것만(<see cref="ChapterBranchCards.GoingWithScene"/> · 2026-11-18
    /// 소유자 — 「장면 지우기시에 분기카드까지 함께」). 한 되돌리기 칸이다. 장면째 지우는 것이라 메모를 주인 없이 남기지 않는다(에피소드를 지우거나
    /// 옮겨 장면이 비는 것과 다르다). ⛔ 그 전에는 분기 카드가 판에 남았다(부르는 곳이 없어 검증이 경고했다).
    /// </summary>
    /// <returns>걷은 에피소드 · 분기 카드 수 — 그 장면이 없으면 둘 다 0이고 아무것도 하지 않는다.</returns>
    /// <exception cref="InvalidOperationException">챕터의 마지막 장면이다(<see cref="ChapterMinimum"/> — 화면은 그때 지우기를 끈다).</exception>
    public RemovedScene RemoveScene(string chapterId, string sceneId)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        if (ChapterMinimum.IsLastScene(chapter.Episodes, sceneId))
        {
            throw new InvalidOperationException(ChapterMinimum.LastSceneReason);
        }
        List<string> episodes = chapter.Episodes
            .Where(episode => string.Equals(episode.SceneId, sceneId, StringComparison.Ordinal))
            .Select(episode => episode.EpisodeId)
            .ToList();

        if (episodes.Count == 0)
        {
            return new RemovedScene(0, 0);
        }

        // 걷기 전에 센다 — 에피소드를 걷고 나면 집을 잃은 카드는 HomesOf에서 사라져 찾을 수 없다.
        List<string> branchCards = ChapterBranchCards.GoingWithScene(Project, chapterId, sceneId)
            .Select(card => card.Id)
            .ToList();

        Batch(() =>
        {
            foreach (string episodeId in episodes)
            {
                RemoveEpisode(chapterId, episodeId);
            }

            foreach (string cardId in branchCards)
            {
                RemoveNode(cardId);
            }

            RemoveSceneNote(chapterId, sceneId);
        });

        return new RemovedScene(episodes.Count, branchCards.Count);
    }

    /// <summary><see cref="RemoveScene"/>이 걷은 것 — 에피소드 수와 분기 카드 수.</summary>
    public readonly record struct RemovedScene(int Episodes, int BranchCards);

    // ── 간선 ────────────────────────────────────────────────────────────────
    // 길 하나가 곧 선택지 하나이고, 신원은 (출발, 도착, 문구)다.

    /// <param name="before">
    /// 이 간선 <b>앞에</b> 끼워 넣는다 — 없으면 끝에 붙는다.
    /// 한 에피소드에서 나가는 간선의 순서가 곧 화면의 선택지 순서이자 런타임 <c>OptionIndex</c>라(§5-3), 연출 그래프는
    /// 사람이 적어 둔 줄 자리에 맞춰 넣는다.
    /// </param>
    public ChapterEdge AddEdge(
        string chapterId,
        string fromEpisodeId,
        string toEpisodeId,
        string? conditionLabel = null,
        string? optionLabel = null,
        string? statChanges = null,
        bool auto = false,
        ChapterEdge? before = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        // 새로 잇는 선의 꼴(설정 · NewLineShape) — 직각이면 적지 않는다.
        ChapterEdge edge = NewEdge(chapter, fromEpisodeId, toEpisodeId, optionLabel, conditionLabel, statChanges, auto) with { Shape = NewLineShapeOrNull() };
        int at = before is null
            ? chapter.Edges.Count
            : RequireEdgeIndex(chapter, before.FromEpisodeId, before.ToEpisodeId, before.OptionLabel);

        // 출발은 그대로다 — 판은 그 출발의 철도만 다시 긋는다(usability UX19).
        Mutate(ProjectChangeKind.ChoiceRoute, () =>
        {
            chapter.Edges.Insert(at, edge);
            LearnChoiceLabel(chapter, optionLabel);
        }, EpisodeNaming.CardFor(Project, chapterId, fromEpisodeId)?.Id);

        return edge;
    }

    /// <summary>
    /// 선택지 한 줄의 배선을 고친다 — 문구와 도착을 한 변경으로.
    /// 찾을 때는 고치기 <b>전</b>의 신원을 쓴다.
    ///
    /// <b>목록의 자리를 지킨다</b> — 간선 순서가 곧 화면 순서이고 런타임 <c>OptionIndex</c>다(two-graphs.md §5-3).
    /// 관문 · 잠금 안내 · (주지 않으면) 스탯 변화도 그대로 남는다. 걷고 새로 그으면 셋 다 잃는다.
    ///
    /// 고친 신원이 다른 간선과 겹치면 던진다(<see cref="AddEdge"/>와 같은 규칙). 바뀌는 것이 없으면 아무것도
    /// 적지 않는다(§5-8).
    /// </summary>
    public void SetEdgeRoute(
        string chapterId,
        string fromEpisodeId,
        string currentToEpisodeId,
        string? currentOptionLabel,
        string newToEpisodeId,
        string? newOptionLabel,
        string? statChanges = null,
        bool? auto = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEdgeIndex(chapter, fromEpisodeId, currentToEpisodeId, currentOptionLabel);
        ChapterEdge current = chapter.Edges[index];

        ChapterEdge routed = current with
        {
            ToEpisodeId = newToEpisodeId,
            OptionLabel = newOptionLabel,
            StatChanges = statChanges is null ? current.StatChanges : ParseDeltas(chapter, statChanges),
            Auto = auto ?? current.Auto
        };

        if (routed.Equals(current))
        {
            return;
        }

        if (HasEdge(chapter, fromEpisodeId, newToEpisodeId, newOptionLabel, exceptIndex: index))
        {
            throw new InvalidOperationException(
                $"간선 {fromEpisodeId}→{newToEpisodeId}이 같은 선택지 문구로 이미 있습니다.");
        }

        Mutate(ProjectChangeKind.ChoiceRoute, () =>
        {
            chapter.Edges[index] = routed;
            LearnChoiceLabel(chapter, newOptionLabel);
        }, EpisodeNaming.CardFor(Project, chapterId, fromEpisodeId)?.Id);
    }

    /// <summary>간선 한 줄의 속성 편집 — null이 아닌 것만 바꾼다 (관문 둘 포함, v8).</summary>
    public void UpdateEdge(
        string chapterId,
        string fromEpisodeId,
        string toEpisodeId,
        string? conditionLabel = null,
        string? lockedMessage = null,
        string? statChanges = null,
        string? matchOptionLabel = null,
        string? visibleConditionLabel = null,
        string? optionLabel = null,
        bool? auto = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEdgeIndex(chapter, fromEpisodeId, toEpisodeId, matchOptionLabel);

        // 이음(출발 · 도착)은 그대로다 — 선택지의 내용만(usability UX16). 판은 출발 카드에서 나가는 철도만 다시 긋는다.
        Mutate(ProjectChangeKind.ChoiceDetail, () =>
        {
            ChapterEdge edge = chapter.Edges[index];

            chapter.Edges[index] = edge with
            {
                ConditionLabel = NoneIfBlank(conditionLabel ?? edge.ConditionLabel),
                VisibleConditionLabel = NoneIfBlank(visibleConditionLabel ?? edge.VisibleConditionLabel),
                LockedMessage = NoneIfBlank(lockedMessage ?? edge.LockedMessage),
                StatChanges = statChanges is null ? edge.StatChanges : ParseDeltas(chapter, statChanges),
                OptionLabel = optionLabel ?? edge.OptionLabel,
                Auto = auto ?? edge.Auto
            };

            LearnChoiceLabel(chapter, optionLabel);
        }, EpisodeNaming.CardFor(Project, chapterId, fromEpisodeId)?.Id);
    }

    /// <summary>
    /// <b>선택지 레인의 모양을 정한다</b> — 간선이 모양을 진다
    /// (<see cref="ChapterEdge.Shape"/>). 되돌리기 한 칸이고, 자리만 바뀐 변경(<see cref="ProjectChangeKind.Layout"/>)이라
    /// 판은 카드를 다시 짓지 않고 선만 다시 긋는다.
    /// null이나 기본 모양이면 자동으로 돌아간다. 바뀌는 것이 없으면 아무것도 적지 않는다.
    /// </summary>
    public void SetEdgeShape(
        string chapterId, string fromEpisodeId, string toEpisodeId, string? optionLabel, EdgeShape? shape)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEdgeIndex(chapter, fromEpisodeId, toEpisodeId, optionLabel);
        EdgeShape? wanted = shape is null || shape.IsDefault ? null : shape;

        if (Equals(chapter.Edges[index].Shape, wanted))
        {
            return;
        }

        Mutate(ProjectChangeKind.Layout, () => chapter.Edges[index] = chapter.Edges[index] with { Shape = wanted });
    }

    /// <summary>간선 삭제 — 사전의 문구는 남는다(다른 길에서 또 쓴다).</summary>
    public void RemoveEdge(
        string chapterId, string fromEpisodeId, string toEpisodeId, string? optionLabel = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        int index = RequireEdgeIndex(chapter, fromEpisodeId, toEpisodeId, optionLabel);

        Mutate(ProjectChangeKind.ChoiceRoute, () => chapter.Edges.RemoveAt(index), EpisodeNaming.CardFor(Project, chapterId, fromEpisodeId)?.Id);
    }

    // ── 선택지 사전 · 조건 ──────────────────────────────────────────────────

    /// <summary>선택지 사전에 문구 한 줄 — 어느 에피소드의 것도 아니다 (v9).</summary>
    public void AddChoiceLabel(string chapterId, string text)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("선택지 문구가 비어 있습니다.");
        }

        if (FindChoice(chapter, text) is not null)
        {
            throw new InvalidOperationException($"'{text}'는 이미 선택지 사전에 있습니다.");
        }

        // 사전은 [대본] 판에 서지 않는다 — 판 · 인스펙터가 다시 세울 것이 없다([구성]은 어느 변경에든 다시 읽는다).
        Mutate(ProjectChangeKind.Content, () => LearnChoiceLabel(chapter, text));
    }

    /// <summary>
    /// 챕터의 표시 이름(<see cref="ChapterDocument.DisplayName"/>)을 고친다. 비우면 Id로 물러선다.
    /// 되돌리기 한 칸 — 구조가 아니라 글자라 그래프는 다시 서지 않는다.
    /// </summary>
    public void UpdateChapterDisplayName(string chapterId, string? displayName)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string? wanted = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

        if (string.Equals(chapter.DisplayName, wanted, StringComparison.Ordinal))
        {
            return;
        }

        Mutate(ProjectChangeKind.NodeMetadata, () => chapter.DisplayName = wanted);
    }

    /// <summary>
    /// 챕터 조건 하나 — 간선의 표시조건·해금조건이 라벨로 가리킨다.
    ///
    /// <b>이름에 "Chapter"가 붙은 이유</b>: <c>AddCondition</c>은 이미 <b>설정노드</b>의
    /// 창구다. 둘은 다른 것이다 — 저쪽은 대사 안의 [3] 조건이 태어나는 자리이고, 이쪽은
    /// 챕터 간선이 보는 [2] 진행 스탯이다(지시서 §3.2). 같은 이름으로 두면 인자 개수만
    /// 다른 호출이 <b>조용히 저쪽으로 간다</b> — 실제로 이 테스트를 쓰다 한 번 물렸다.
    /// </summary>
    public void AddChapterCondition(string chapterId, string label, string expression, string? description = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        if (FindCondition(chapter, label) is not null)
        {
            throw new InvalidOperationException($"조건 라벨 '{label}'이 이미 있습니다.");
        }

        // 조건과 그 조건의 공급이 <b>한 변경</b>이다 — 되돌리기 한 번에 함께 간다. 공급 노드는 판에 서지 않는 배관이라 판에는 조건만 바뀐 것이다(UX16).
        Batch(ProjectChangeKind.ChapterCondition, () =>
        {
            Mutate(() => chapter.Conditions.Add(
                new ChapterCondition(label, expression, description, Parsed: [], IsValid: false)));

            SupplyChapterConditions(chapterId);
        });
    }

    /// <summary>챕터 조건의 식·설명을 고친다 — 설정노드의 <c>UpdateCondition</c>과 다른 것이다.</summary>
    public void UpdateChapterCondition(string chapterId, string label, string expression, string? description = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        ChapterCondition condition = FindCondition(chapter, label)
            ?? throw new InvalidOperationException($"조건 라벨 '{label}'이 없습니다.");

        int index = chapter.Conditions.IndexOf(condition);

        Batch(ProjectChangeKind.ChapterCondition, () =>
        {
            Mutate(() => chapter.Conditions[index] = condition with
            {
                Expression = expression,
                Description = description ?? condition.Description,

                // 해석은 담지 않는다 — ToGraphModel이 식에서 다시 푼다. 여기서 옛 해석을
                //    들고 있으면 검증이 고치기 전 값으로 참을 말한다.
                Parsed = [],
                IsValid = false
            });

            // 사본은 이름으로 짝짓는다 — 같은 이름의 사본이 Id를 지킨 채 새 식을 받는다(식 미정 ↔ 식 채움도 같다).
            SupplyChapterConditions(chapterId);
        });
    }

    // ── 판에 공급하는 챕터 조건 ─────────────────────────────────────────────
    //
    // 챕터 조건은 판의 공급 노드(`챕터 {Id} 조건`)로 건너간다. 따로 누르는 단추가 있으면
    //    그것을 안 누른 동안 대사 노드의 조건 목록이 옛것이 되므로,
    //    <b>조건이 바뀌는 그 명령</b>(<see cref="AddChapterCondition"/>·<see cref="UpdateChapterCondition"/>)과
    //    <b>판이 서는 순간</b>(<see cref="EnsureChapterBoard"/>)이 공급을 부른다.

    /// <summary>판이 있으면 챕터 조건을 그 판의 공급 노드로 보낸다. 멱등이다 — 바뀔 것이 없으면 안 건드린다.</summary>
    private void SupplyChapterConditions(string chapterId)
    {
        if (FindChapter(chapterId) is { } chapter &&
            EpisodeNaming.BoardOf(Project, chapterId) is { } board)
        {
            ChapterBoardSupply.SupplyChapterConditionsToBoard(
                this, board.Id, chapter.ToGraphModel(string.Empty));
        }
    }

    /// <summary>
    /// <b>스탯 개명이 참조를 끌고 간다</b>.
    ///
    /// <b>개명이 쉬운 것이 곧 위험이다.</b> 툴이 정의를 쥐면 개명은 한 번에 쉬워지는데 —
    /// <b>쉬운데 안전하지 않은 것이 어려운데 안전하지 않은 것보다 나쁘다.</b>
    ///
    /// 키가 사는 자리는 넷이고 <b>한 번의 변경</b>으로 함께 간다:
    ///
    /// <list type="number">
    /// <item><c>스탯</c> 정의 행 — 표시이름이 키와 같았으면 그것도 따라간다(안 정한 이름이다).</item>
    /// <item>간선의 <c>스탯변화</c> — 해석된 채로 살아서 키만 갈면 된다.</item>
    /// <item>챕터 <c>조건</c> 식 — 원문이라 문법을 아는 <see cref="ConditionExpressionParser.ReplaceStatKey"/>가 간다.</item>
    /// <item><b>판에 공급된 조건</b> — 아래.</item>
    /// <item><c>픽스처</c>의 시작값 — 키로 든 사전이라 안 갈면 그 값이 <b>조용히 사라지고</b>
    /// 워커가 초기값으로 되돌아간다.</item>
    /// </list>
    ///
    /// <b>넷째를 빠뜨리면 대사 갈래가 통째로 고아가 된다.</b> 판의 공급 노드는 챕터 조건을
    /// 번역해 들고 있고(<c>stat("trust") &gt;= 3</c>), 줄에 매달린 전환은 그 조건의 <b>Id</b>로
    /// 잇는다. 그런데 <c>ChapterBoardSupply.RenameSuppliedCondition</c>은 <b>식으로 짝을
    /// 찾는다</b> — 스탯을 갈면 식이 달라져 짝을 못 찾고, 다음 동기화가 <b>새 Id로 다시
    /// 만든다</b>. 그 자리 주석이 경고하는 바로 그 고아다. 그래서 여기서 <b>Id를 지킨 채</b>
    /// 식만 갈아 끼운다.
    /// </summary>
    /// <returns>바꾼 자리의 수. 거절되면 <c>Applied</c>가 false이고 아무것도 안 건드렸다.</returns>
    public StatRenameOutcome RenameChapterStat(string chapterId, string oldKey, string newKey)
    {
        ChapterDocument chapter = RequireChapter(chapterId);

        string from = (oldKey ?? string.Empty).Trim();
        string to = (newKey ?? string.Empty).Trim();

        if (from.Length == 0 || to.Length == 0)
        {
            return StatRenameOutcome.Refuse("스탯 키가 비어 있습니다.");
        }

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return StatRenameOutcome.Refuse("같은 이름입니다 — 바꿀 것이 없습니다.");
        }

        int index = chapter.Stats.FindIndex(stat =>
            string.Equals(stat.Key, from, StringComparison.Ordinal));

        if (index < 0)
        {
            return StatRenameOutcome.Refuse($"스탯 '{from}'이 이 챕터에 없습니다.");
        }

        if (chapter.Stats.Any(stat => string.Equals(stat.Key, to, StringComparison.Ordinal)))
        {
            return StatRenameOutcome.Refuse($"스탯 '{to}'가 이미 있습니다 — 둘을 합치려면 먼저 하나를 지우세요.");
        }

        // 정규화 뒤에 겹치는 것은 막지 않는다 — `stat("키")`의 인자는
        //    식별자가 아니라 <b>문자열 리터럴</b>이라 정규화가 없다. `호감 도`와
        //    `호감_도`는 게임에서도 서로 다른 스탯이다.

        ChapterStat renamed = chapter.Stats[index] with { Key = to };

        var edges = new List<(int Index, ChapterEdge Edge)>();

        for (int i = 0; i < chapter.Edges.Count; i++)
        {
            if (!chapter.Edges[i].StatChanges.Any(delta =>
                    string.Equals(delta.Key, from, StringComparison.Ordinal)))
            {
                continue;
            }

            edges.Add((i, chapter.Edges[i] with
            {
                StatChanges = chapter.Edges[i].StatChanges
                    .Select(delta => string.Equals(delta.Key, from, StringComparison.Ordinal)
                        ? delta with { Key = to }
                        : delta)
                    .ToList()
            }));
        }

        var conditions = new List<(int Index, ChapterCondition Condition)>();

        for (int i = 0; i < chapter.Conditions.Count; i++)
        {
            string rewritten = ConditionExpressionParser.ReplaceStatKey(
                chapter.Conditions[i].Expression, from, to);

            if (string.Equals(rewritten, chapter.Conditions[i].Expression, StringComparison.Ordinal))
            {
                continue;
            }

            conditions.Add((i, chapter.Conditions[i] with
            {
                Expression = rewritten,

                // 옛 해석을 들고 있으면 검증이 고치기 전 값으로 참을 말한다 —
                //    `UpdateChapterCondition`과 같은 규율이다. ToGraphModel이 다시 푼다.
                Parsed = [],
                IsValid = false
            }));
        }

        var fixtures = new List<(int Index, ChapterFixture Fixture)>();

        for (int i = 0; i < chapter.Fixtures.Count; i++)
        {
            if (!chapter.Fixtures[i].Stats.ContainsKey(from))
            {
                continue;
            }

            var stats = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach ((string key, int value) in chapter.Fixtures[i].Stats)
            {
                stats[string.Equals(key, from, StringComparison.Ordinal) ? to : key] = value;
            }

            fixtures.Add((i, chapter.Fixtures[i] with { Stats = stats }));
        }

        List<ConditionDefinition> supplied = SuppliedConditionsReading(chapterId, from);

        Mutate(() =>
        {
            chapter.Stats[index] = renamed;

            foreach ((int at, ChapterFixture fixture) in fixtures)
            {
                chapter.Fixtures[at] = fixture;
            }

            foreach ((int at, ChapterEdge edge) in edges)
            {
                chapter.Edges[at] = edge;
            }

            foreach ((int at, ChapterCondition condition) in conditions)
            {
                chapter.Conditions[at] = condition;
            }

            foreach (ConditionDefinition condition in supplied)
            {
                // Id를 지킨 채 식만 간다 — 줄에 매달린 전환이 이 Id로 잇는다.
                condition.Expression = condition.Expression.Replace(
                    Rendering.YarnSyntax.StatRead(from),
                    Rendering.YarnSyntax.StatRead(to),
                    StringComparison.Ordinal);
            }
        });

        return new StatRenameOutcome(
            true, edges.Count, conditions.Count, supplied.Count, fixtures.Count, null);
    }

    /// <summary>
    /// 그 챕터 판의 공급 노드가 들고 있는 조건 중 <b>이 스탯을 읽는</b> 것들.
    /// 판이 없거나 공급 노드가 아직 없으면 빈 목록이다.
    /// </summary>
    private List<ConditionDefinition> SuppliedConditionsReading(string chapterId, string statKey)
    {
        string read = Rendering.YarnSyntax.StatRead(statKey);

        return SupplyNodesOf(chapterId)
            .SelectMany(node => node.Conditions)
            .Where(condition => condition.Expression.Contains(read, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// <b>스탯 하나를 지운다 — 쓰는 곳이 있으면 거절하고 어디인지 말한다</b>.
    ///
    /// <b>개명과 달리 답이 없다.</b> 개명은 모든 참조가 새 이름을 따라가면 끝이지만, 삭제는
    /// <c>trust +1</c>이 적힌 간선을 만났을 때 툴이 정할 수 없다 — 그 항만 뺄지(작가가 적은
    /// 의도가 사라진다), 간선을 걷을지(이야기 구조가 바뀐다). 조건식 <c>trust &gt;= 3</c>은
    /// 아예 <b>뺄 수가 없다</b>(식이 통째로 깨진다).
    ///
    /// <b>나중에 진단이 잡아 준다는 것으로는 부족하다.</b> 파서가 <c>UnknownStatKey</c>로
    /// 잡기는 하는데 문구가 *"`스탯` 시트에 없는 스탯키입니다"* — <b>오타를 가정한 안내</b>다.
    /// 방금 일부러 지운 사람에게는 틀린 말이고, 진단은 검증할 때나 보인다.
    /// <b>의도가 머릿속에 있는 순간은 지우는 그 순간</b>이라 그 자리에서 말한다.
    ///
    /// <b>픽스처의 시작값은 막지 않고 함께 지운다.</b> 키로 든 사전이고 없으면 초기값으로
    /// 읽히며(<c>ChapterFixtureWalker</c>) 내보내기에도 안 섞인다 — 잃을 저작이 없다.
    ///
    /// <b><paramref name="clearUses"/> — 「쓰는 곳을 전부 비우고 지우기」</b>.
    /// 화면이 <see cref="FindStatUses"/>로 목록을 먼저 보여 주고, 사람이 한 번 더 누르면
    /// 그 목록을 비운 뒤 지운다. <b>한 번의 변경</b>이라 되돌리기 한 번에 전부 돌아온다.
    ///
    /// <list type="bullet">
    /// <item>간선 — <c>스탯변화</c>에서 <b>그 항만</b> 뺀다. 길 자체를 걷으면 이야기 구조가 바뀐다.</item>
    /// <item>조건 — <b>식만 비우고 조건 자체는 남긴다.</b> 검증이 *"조건식이 비어 있습니다"*로
    /// 울어서 사람이 채운다. 조건을 통째로 지우면 그것을 쓰는 간선의 표시/해금이 함께
    /// 풀려 <b>연쇄가 한 단계 더</b> 간다 — 지우는 사람이 안 본 자리까지 바뀐다.</item>
    /// <item>판에 공급된 사본도 <b>식만 비운다</b>(Id는 지킨다). 이미터가 빈 식을
    /// <c>&lt;&lt;if false&gt;&gt;</c>로 내므로 매달린 갈래는 <b>서 있되 안 탄다</b> —
    /// 조용히 꺼져 있는 것이 사라지는 것보다 낫다.</item>
    /// </list>
    /// </summary>
    public StatRemoveOutcome RemoveChapterStat(string chapterId, string key, bool clearUses = false)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string target = (key ?? string.Empty).Trim();

        int index = chapter.Stats.FindIndex(stat =>
            string.Equals(stat.Key, target, StringComparison.Ordinal));

        if (index < 0)
        {
            return StatRemoveOutcome.Refuse($"스탯 '{target}'이 이 챕터에 없습니다.", []);
        }

        IReadOnlyList<ChapterUse> uses = FindStatUses(chapterId, target);

        if (uses.Count > 0 && !clearUses)
        {
            return StatRemoveOutcome.Refuse(
                $"스탯 '{target}'을 지울 수 없습니다 — {uses.Count}곳에서 쓰고 있습니다.",
                uses);
        }

        var edges = new List<(int Index, ChapterEdge Edge)>();
        var conditions = new List<(int Index, ChapterCondition Condition)>();
        var emptied = new List<ConditionDefinition>();

        if (clearUses)
        {
            for (int i = 0; i < chapter.Edges.Count; i++)
            {
                if (!chapter.Edges[i].StatChanges.Any(delta =>
                        string.Equals(delta.Key, target, StringComparison.Ordinal)))
                {
                    continue;
                }

                // 간선은 <b>그 항만</b> 뺀다 — 길 자체를 걷으면 이야기 구조가 바뀐다.
                edges.Add((i, chapter.Edges[i] with
                {
                    StatChanges = chapter.Edges[i].StatChanges
                        .Where(delta => !string.Equals(delta.Key, target, StringComparison.Ordinal))
                        .ToList()
                }));
            }

            for (int i = 0; i < chapter.Conditions.Count; i++)
            {
                if (string.Equals(
                        ConditionExpressionParser.ReplaceStatKey(chapter.Conditions[i].Expression, target, "\0"),
                        chapter.Conditions[i].Expression,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                // <b>식만 비우고 조건은 남긴다</b>. 조건을 통째로 지우면
                //    그것을 쓰는 간선의 표시/해금이 함께 풀려 <b>연쇄가 한 단계 더</b> 간다 —
                //    지우는 사람이 안 본 자리까지 바뀐다. 빈 식은 검증이 짚어 사람이 채운다.
                conditions.Add((i, chapter.Conditions[i] with
                {
                    Expression = string.Empty,
                    Parsed = [],
                    IsValid = false
                }));

                emptied.AddRange(SuppliedNamed(chapterId, chapter.Conditions[i].Label)
                    .Select(pair => pair.Condition));
            }
        }

        var fixtures = new List<(int Index, ChapterFixture Fixture)>();

        for (int i = 0; i < chapter.Fixtures.Count; i++)
        {
            if (!chapter.Fixtures[i].Stats.ContainsKey(target))
            {
                continue;
            }

            fixtures.Add((i, chapter.Fixtures[i] with
            {
                Stats = chapter.Fixtures[i].Stats
                    .Where(pair => !string.Equals(pair.Key, target, StringComparison.Ordinal))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            }));
        }

        Mutate(() =>
        {
            chapter.Stats.RemoveAt(index);

            foreach ((int at, ChapterEdge edge) in edges)
            {
                chapter.Edges[at] = edge;
            }

            foreach ((int at, ChapterCondition condition) in conditions)
            {
                chapter.Conditions[at] = condition;
            }

            foreach (ConditionDefinition supplied in emptied)
            {
                // <b>Id는 지킨다</b> — 줄에 매달린 갈래가 이 Id로 잇는다. 챕터 조건은 식 미정이 됐으므로 사본도
                //    식 미정의 식(항상 통과)이 된다 — 간선과 같은 규칙이다(writer-fields W1 · 2026-11-17).
                //    전에는 비워서 `<<if false>>`로 꺼 두었다.
                supplied.Expression = ChapterBoardSupply.UndecidedYarn;
            }

            foreach ((int at, ChapterFixture fixture) in fixtures)
            {
                chapter.Fixtures[at] = fixture;
            }
        });

        return new StatRemoveOutcome(
            true, fixtures.Count, edges.Count, conditions.Count, [], null);
    }

    /// <summary>
    /// 이 스탯을 <b>쓰는 곳</b>을 전부 찾는다 — 간선의 <c>스탯변화</c>와 챕터 <c>조건</c>의 식.
    ///
    /// <b>사람이 찾아갈 수 있게</b> 적는다. *"쓰이고 있습니다"*로 끝내면 챕터를 뒤져야 한다.
    ///
    /// 픽스처는 세지 않는다 — 막을 이유가 없어 삭제가 함께 치운다(위).
    /// 판에 공급된 조건도 세지 않는다 — 챕터 조건에서 <b>파생</b>된 것이라, 원본 조건이
    /// 막고 있으면 여기까지 올 일이 없다.
    /// </summary>
    public IReadOnlyList<ChapterUse> FindStatUses(string chapterId, string key)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string target = (key ?? string.Empty).Trim();

        if (target.Length == 0)
        {
            return [];
        }

        var uses = new List<ChapterUse>();

        foreach (ChapterEdge edge in chapter.Edges)
        {
            if (!edge.StatChanges.Any(delta =>
                    string.Equals(delta.Key, target, StringComparison.Ordinal)))
            {
                continue;
            }

            string label = edge.OptionLabel is { Length: > 0 } text ? $" \"{text}\"" : string.Empty;

            uses.Add(new ChapterUse(
                ChapterUseKind.Edge,
                $"간선 {edge.FromEpisodeId} → {edge.ToEpisodeId}{label}",
                $"스탯변화: {StatDeltaParser.Format(edge.StatChanges)}"));
        }

        foreach (ChapterCondition condition in chapter.Conditions)
        {
            // 원문을 본다 — 해석은 스탯이 사라지는 중이라 이미 못 믿는다.
            if (!string.Equals(
                    ConditionExpressionParser.ReplaceStatKey(condition.Expression, target, "\u0000"),
                    condition.Expression,
                    StringComparison.Ordinal))
            {
                uses.Add(new ChapterUse(
                    ChapterUseKind.Condition,
                    $"조건 '{condition.Label}'",
                    condition.Expression));
            }
        }

        return uses;
    }

    // ── 스탯 정의 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 스탯 하나를 세운다.
    ///
    /// <b>경계(최소·최대·초기)는 여기서 안 막는다.</b> <see cref="ChapterDocument"/>가 이미
    /// 진단으로 짚고(<c>최소 &gt; 최대</c>, 초기값이 범위 밖), <b>읽기는 오류가 있어도 모델을
    /// 만든다</b>(규칙 14) — 여기서만 막으면 툴로 만든 것과 파일에서 읽은 것(옛 프로젝트 · 손으로
    /// 고친 파일)이 서로 다른
    /// 규칙을 살게 된다. 값을 채워 가는 중에 막히지도 않는다.
    ///
    /// 막는 것은 <b>키 중복</b>뿐이다. 같은 키가 둘이면 어느 행이 이기는지 말할 수 없어
    /// 진단으로 넘길 수 있는 상태가 아니다 — 정규화 뒤에 겹치는 것도 같다(화면에서는 달라
    /// 보이는데 <b>게임에서 하나가 된다</b>).
    /// </summary>
    public ChapterStat AddChapterStat(
        string chapterId,
        string key,
        int initial = 0,
        int minimum = 0,
        int maximum = 100,
        ChapterStatType type = ChapterStatType.Int)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string target = (key ?? string.Empty).Trim();

        if (target.Length == 0)
        {
            throw new InvalidOperationException("스탯 키가 비어 있습니다.");
        }

        RequireStatKeyFree(chapter, target, replacing: null);

        var stat = BoolBounded(new ChapterStat(
            target,
            initial,
            minimum,
            maximum,
            type));

        Mutate(() => chapter.Stats.Add(stat));

        return stat;
    }

    /// <summary>
    /// 스탯의 값·타입을 고친다 — null이 아닌 것만 바꾼다
    /// (<see cref="UpdateEpisode"/>와 같은 손버릇).
    ///
    /// <b>키는 여기서 못 바꾼다.</b> 키를 바꾸는 것은 참조를 끌고 가는 일이라
    /// <see cref="RenameChapterStat"/>의 몫이다 — 여기서 슬쩍 바꾸면 간선·조건·픽스처가
    /// 옛 이름을 가리킨 채 남는다.
    /// </summary>
    public void UpdateChapterStat(
        string chapterId,
        string key,
        int? initial = null,
        int? minimum = null,
        int? maximum = null,
        ChapterStatType? type = null)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string target = (key ?? string.Empty).Trim();

        int index = chapter.Stats.FindIndex(stat =>
            string.Equals(stat.Key, target, StringComparison.Ordinal));

        if (index < 0)
        {
            throw new InvalidOperationException($"스탯 '{target}'이 이 챕터에 없습니다.");
        }

        Mutate(() => chapter.Stats[index] = BoolBounded(chapter.Stats[index] with
        {
            Initial = initial ?? chapter.Stats[index].Initial,
            Minimum = minimum ?? chapter.Stats[index].Minimum,
            Maximum = maximum ?? chapter.Stats[index].Maximum,
            Type = type ?? chapter.Stats[index].Type
        }));
    }

    /// <summary>
    /// bool 스탯은 값 공간이 0/1 하나다 — 범위를 0~1로 고정하고 처음값은 0이 아니면 1이다.
    ///
    /// 옛 워크북 리더가 하던 일이다(최소·최대 칸을 읽지 않고 고정했다). 리더가 걷힌 뒤 편집기가
    /// 경계를 받는 그대로 두면 깃발에 0~100 같은 범위가 붙고, 도달성 증명이 없는 값까지 훑는다.
    /// </summary>
    private static ChapterStat BoolBounded(ChapterStat stat) =>
        stat.Type == ChapterStatType.Bool
            ? stat with { Minimum = 0, Maximum = 1, Initial = stat.Initial == 0 ? 0 : 1 }
            : stat;

    /// <summary>
    /// 이 키를 써도 되는가 — 겹치면 던진다.
    /// <paramref name="replacing"/>은 자기 자신이라 안 센다.
    /// </summary>
    private static void RequireStatKeyFree(ChapterDocument chapter, string key, string? replacing)
    {
        bool Other(ChapterStat stat) =>
            replacing is null || !string.Equals(stat.Key, replacing, StringComparison.Ordinal);

        if (chapter.Stats.Any(stat => Other(stat) &&
                string.Equals(stat.Key, key, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"스탯 '{key}'가 이미 있습니다.");
        }

        // 정규화 뒤 겹침은 막지 않는다 — `stat("키")`의 인자가 문자열 리터럴이라 정규화가 없다.
    }

    /// <summary>
    /// <b>챕터 조건 하나를 지운다 — 쓰는 곳이 있으면 거절하고 어디인지 말한다</b>
    /// (스탯 삭제와 같은 규율).
    ///
    /// 조건을 부르는 곳은 둘이다:
    ///
    /// <list type="bullet">
    /// <item><b>간선</b> — 표시조건·해금조건에 라벨로 적는다. 지우면
    /// <c>ConditionLabelUndefined</c>로 울지만 그 문구는 <b>오타를 가정</b>한다.</item>
    /// <item><b>대사 줄의 갈래</b> — 판에 공급된 조건을 <b>Id</b>로 붙든다. 챕터 조건이
    /// 사라지면 동기화가 그 공급 조건을 <b>지우지도 갱신하지도 않아</b> 옛 식을 든 채 남는다.
    /// 갈래는 계속 살아 있고, 무엇을 묻는지는 아무 데도 없다.</item>
    /// </list>
    /// </summary>
    public ChapterConditionRemoveOutcome RemoveChapterCondition(string chapterId, string label)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string target = (label ?? string.Empty).Trim();

        ChapterCondition? condition = chapter.Conditions.FirstOrDefault(item =>
            string.Equals(item.Label, target, StringComparison.Ordinal));

        if (condition is null)
        {
            return ChapterConditionRemoveOutcome.Refuse($"조건 '{target}'이 이 챕터에 없습니다.", []);
        }

        IReadOnlyList<ChapterUse> uses = FindConditionUses(chapterId, target);

        if (uses.Count > 0)
        {
            return ChapterConditionRemoveOutcome.Refuse(
                $"조건 '{target}'을 지울 수 없습니다 — {uses.Count}곳에서 쓰고 있습니다.", uses);
        }

        // 아무도 안 부르면 판에 공급된 사본도 함께 걷는다 — 남겨 두면 정의 없는 조건이
        // 드롭다운에 계속 보인다.
        List<(SetNode Node, ConditionDefinition Condition)> supplied = SuppliedNamed(chapterId, target);

        Mutate(() =>
        {
            chapter.Conditions.Remove(condition);

            foreach ((SetNode node, ConditionDefinition item) in supplied)
            {
                node.Conditions.Remove(item);
            }
        });

        return new ChapterConditionRemoveOutcome(true, supplied.Count, [], null);
    }

    /// <summary>
    /// 챕터 조건의 <b>라벨</b>을 바꾼다 — [대본] 탭(작가가 「하린과 친해졌을 때」처럼 뜻으로 부르는 이름, writer-fields W4). 식은 그대로다.
    ///
    /// 라벨은 이름이 아니라 <b>참조 열쇠</b>다 — 간선의 표시조건 · 해금조건이 이 글자로 가리킨다. 그래서 그 자리들이 함께 간다.
    /// 판에 공급된 사본(대사 줄의 갈래가 <b>Id</b>로 붙든다)은 공급이 식으로 짝을 찾아 이름만 따라간다 — Id가 그대로라 갈래도 그대로다.
    /// 비울 수 없고, 이미 있는 라벨로는 바꾸지 않는다(두 조건이 한 라벨로 합쳐진다). 되돌리기 한 칸이다.
    /// </summary>
    public ChapterConditionRenameOutcome RenameChapterCondition(string chapterId, string oldLabel, string newLabel)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string from = (oldLabel ?? string.Empty).Trim();
        string to = (newLabel ?? string.Empty).Trim();

        if (to.Length == 0)
        {
            return ChapterConditionRenameOutcome.Refuse("조건 이름은 비울 수 없습니다 — 지우려면 ✕를 누르세요.");
        }

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return ChapterConditionRenameOutcome.Refuse("같은 이름입니다 — 바꿀 것이 없습니다.");
        }

        if (FindCondition(chapter, from) is not { } condition)
        {
            return ChapterConditionRenameOutcome.Refuse($"조건 '{from}'이 이 챕터에 없습니다.");
        }

        if (FindCondition(chapter, to) is not null)
        {
            return ChapterConditionRenameOutcome.Refuse($"조건 '{to}'이 이미 있습니다.");
        }

        int index = chapter.Conditions.IndexOf(condition);
        var edges = new List<(int Index, ChapterEdge Edge)>();

        for (int i = 0; i < chapter.Edges.Count; i++)
        {
            ChapterEdge edge = chapter.Edges[i];
            bool unlock = string.Equals(edge.ConditionLabel, from, StringComparison.Ordinal);
            bool visible = string.Equals(edge.VisibleConditionLabel, from, StringComparison.Ordinal);

            if (unlock || visible)
            {
                edges.Add((i, edge with
                {
                    ConditionLabel = unlock ? to : edge.ConditionLabel,
                    VisibleConditionLabel = visible ? to : edge.VisibleConditionLabel
                }));
            }
        }

        Batch(() =>
        {
            Mutate(() =>
            {
                chapter.Conditions[index] = condition with { Label = to };

                foreach ((int at, ChapterEdge edge) in edges)
                {
                    chapter.Edges[at] = edge;
                }
            });

            // 공급 사본의 이름을 먼저 옮긴다(Id 보존 = 매달린 분기 보존) — 공급은 이름으로 짝을 찾는다.
            foreach ((SetNode _, ConditionDefinition copy) in SuppliedNamed(chapterId, from))
            {
                UpdateCondition(copy.Id, to, copy.Expression);
            }

            SupplyChapterConditions(chapterId);
        });

        return new ChapterConditionRenameOutcome(true, edges.Count, null);
    }

    /// <summary>이 조건을 <b>쓰는 곳</b> — 간선의 표시/해금, 그리고 대사 줄의 갈래.</summary>
    public IReadOnlyList<ChapterUse> FindConditionUses(string chapterId, string label)
    {
        ChapterDocument chapter = RequireChapter(chapterId);
        string target = (label ?? string.Empty).Trim();

        if (target.Length == 0)
        {
            return [];
        }

        var uses = new List<ChapterUse>();

        foreach (ChapterEdge edge in chapter.Edges)
        {
            bool unlock = string.Equals(edge.ConditionLabel, target, StringComparison.Ordinal);
            bool visible = string.Equals(edge.VisibleConditionLabel, target, StringComparison.Ordinal);

            if (!unlock && !visible)
            {
                continue;
            }

            uses.Add(new ChapterUse(
                ChapterUseKind.Edge,
                edge.Where,
                unlock && visible ? "표시조건 · 해금조건" : unlock ? "해금조건" : "표시조건"));
        }

        var ids = new HashSet<string>(
            SuppliedNamed(chapterId, target).Select(pair => pair.Condition.Id), StringComparer.Ordinal);

        if (ids.Count == 0)
        {
            return uses;
        }

        foreach (DialogueNode node in Project.EnumerateNodes().OfType<DialogueNode>())
        {
            foreach (DialogueLineExtension extension in node.LineExtensions)
            {
                if (!extension.Transitions.Any(transition =>
                        transition.ConditionId is { } id && ids.Contains(id)))
                {
                    continue;
                }

                // 줄은 차례로 가리킨다 — 화면에 LineId를 보이지 않는다.
                int order = node.Script.ActiveLines
                    .Select(line => line.Id)
                    .ToList()
                    .IndexOf(extension.LineId) + 1;

                uses.Add(new ChapterUse(
                    ChapterUseKind.DialogueBranch,
                    $"대사 '{node.Name}'",
                    order > 0 ? $"{order}번째 줄의 갈래" : "대본에서 사라진 줄의 갈래"));
            }
        }

        return uses;
    }

    /// <summary>그 챕터 판의 공급 노드가 이 이름으로 들고 있는 조건들.</summary>
    private List<(SetNode Node, ConditionDefinition Condition)> SuppliedNamed(string chapterId, string label) =>
        SupplyNodesOf(chapterId)
            .SelectMany(node => node.Conditions.Select(condition => (Node: node, Condition: condition)))
            .Where(pair => string.Equals(pair.Condition.Name, label, StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// <b>그 챕터의</b> 공급 노드 — 공급(<see cref="ChapterBoardSupply.SupplyChapterConditionsToBoard"/>)이
    /// 찾는 것과 같은 이름 규약이다.
    ///
    /// 이름 모양(<c>챕터 … 조건</c>)만 보고 <b>모든 챕터의</b> 공급 노드를 훑으면 안 된다.
    /// 스탯·조건은 챕터마다 따로라서, ch01의 <c>trust</c>를 고치거나 조건을 지우면
    /// 같은 이름을 쓰는 ch02의 공급 사본까지 바뀌거나 걷힌다.
    /// </summary>
    private IEnumerable<SetNode> SupplyNodesOf(string chapterId)
    {
        string name = ChapterBoardSupply.ConditionSupplyNodeName(chapterId);

        return Project.EnumerateNodes().OfType<SetNode>()
            .Where(node => string.Equals(node.Name, name, StringComparison.Ordinal));
    }

    // ── 잔손 ────────────────────────────────────────────────────────────────

    private static ChapterEpisode? FindEpisode(ChapterDocument chapter, string episodeId) =>
        chapter.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal));

    private static int RequireEpisodeIndex(ChapterDocument chapter, string episodeId)
    {
        int index = chapter.Episodes.FindIndex(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal));

        return index >= 0
            ? index
            : throw new InvalidOperationException($"에피소드 '{episodeId}'가 없습니다.");
    }

    /// <summary>
    /// 간선 찾기 — (출발, 도착)으로 좁히고, 문구가 주어지면 거기까지 맞춘다(같은 도착으로
    /// 문구 여럿일 때의 정확한 신원 — v9).
    /// </summary>
    private static int RequireEdgeIndex(
        ChapterDocument chapter, string fromEpisodeId, string toEpisodeId, string? optionLabel)
    {
        int index = chapter.Edges.FindIndex(edge =>
            string.Equals(edge.FromEpisodeId, fromEpisodeId, StringComparison.Ordinal) &&
            string.Equals(edge.ToEpisodeId, toEpisodeId, StringComparison.Ordinal) &&
            (optionLabel is null ||
             string.Equals(edge.OptionLabel?.Trim() ?? string.Empty, optionLabel.Trim(), StringComparison.Ordinal)));

        return index >= 0
            ? index
            : throw new InvalidOperationException($"간선 {fromEpisodeId}→{toEpisodeId}이 없습니다.");
    }

    private ChapterEdge NewEdge(
        ChapterDocument chapter,
        string fromEpisodeId,
        string toEpisodeId,
        string? optionLabel,
        string? conditionLabel = null,
        string? statChanges = null,
        bool auto = false)
    {
        if (HasEdge(chapter, fromEpisodeId, toEpisodeId, optionLabel))
        {
            throw new InvalidOperationException(
                $"간선 {fromEpisodeId}→{toEpisodeId}이 같은 선택지 문구로 이미 있습니다.");
        }

        return new ChapterEdge(fromEpisodeId, toEpisodeId, optionLabel, NoneIfBlank(conditionLabel), LockedMessage: null)
        {
            Auto = auto,
            StatChanges = statChanges is null ? [] : ParseDeltas(chapter, statChanges)
        };
    }

    /// <summary>
    /// 빈 조건 · 잠금 안내문은 「없음」(null)이다 — 패널은 「(없음)」을 빈 글자로 보내는데, 빈 글자로 남기면 화면이 조건이 있는
    /// 것으로 읽어 「해금: 」이 선다.
    /// </summary>
    private static string? NoneIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>간선의 신원 (출발, 도착, 문구)가 이미 있는가(v9). <paramref name="exceptIndex"/>의 간선은 빼고 본다.</summary>
    private static bool HasEdge(
        ChapterDocument chapter, string fromEpisodeId, string toEpisodeId, string? optionLabel, int exceptIndex = -1) =>
        chapter.Edges
            .Where((_, index) => index != exceptIndex)
            .Any(edge =>
                string.Equals(edge.FromEpisodeId, fromEpisodeId, StringComparison.Ordinal) &&
                string.Equals(edge.ToEpisodeId, toEpisodeId, StringComparison.Ordinal) &&
                string.Equals(
                    edge.OptionLabel?.Trim() ?? string.Empty,
                    optionLabel?.Trim() ?? string.Empty,
                    StringComparison.Ordinal));

    /// <summary>
    /// 사전에 없는 낱말이면 올려 둔다 — 다음부터 드롭다운에서 고른다(옛 <c>EnsureChoiceLabel</c>).
    ///
    /// <c>Mutate</c> 안에서만 부른다 — 이것만으로 되돌리기 항목을 따로 만들지 않는다.
    /// </summary>
    private static void LearnChoiceLabel(ChapterDocument chapter, string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || FindChoice(chapter, text) is not null)
        {
            return;
        }

        // 인덱스는 사전 안의 순서일 뿐이다 — 10 단위로 세어 사람이 사이에 끼울 자리를 남긴다.
        string index = ((chapter.ChoiceOptions.Count + 1) * 10).ToString(
            System.Globalization.CultureInfo.InvariantCulture);

        chapter.ChoiceOptions.Add(new ChapterChoiceOption(index, text.Trim(), Memo: null));
    }

    private static ChapterChoiceOption? FindChoice(ChapterDocument chapter, string text) =>
        chapter.ChoiceOptions.FirstOrDefault(option =>
            string.Equals(option.Text.Trim(), text.Trim(), StringComparison.Ordinal));

    private static ChapterCondition? FindCondition(ChapterDocument chapter, string label) =>
        chapter.Conditions.FirstOrDefault(condition =>
            string.Equals(condition.Label, label, StringComparison.Ordinal));

    /// <summary>
    /// 픽스처의 고정 선택이 가리키는 에피소드를 따라가게 한다 — 개명이면 새 Id로,
    /// 삭제(<paramref name="newId"/>가 null)면 그 항목을 걷는다.
    /// </summary>
    private static void Retarget(ChapterDocument chapter, string oldId, string? newId)
    {
        for (int index = 0; index < chapter.Fixtures.Count; index++)
        {
            ChapterFixture fixture = chapter.Fixtures[index];

            List<ChapterFixtureChoice> choices = fixture.Choices
                .Select(choice => new ChapterFixtureChoice(
                    string.Equals(choice.From, oldId, StringComparison.Ordinal) ? newId ?? oldId : choice.From,
                    string.Equals(choice.To, oldId, StringComparison.Ordinal) ? newId ?? oldId : choice.To))
                .Where(choice => newId is not null ||
                    (!string.Equals(choice.From, oldId, StringComparison.Ordinal) &&
                     !string.Equals(choice.To, oldId, StringComparison.Ordinal)))
                .ToList();

            chapter.Fixtures[index] = fixture with { Choices = choices };
        }

        if (newId is null)
        {
            return;
        }

        for (int index = 0; index < chapter.Edges.Count; index++)
        {
            ChapterEdge edge = chapter.Edges[index];

            chapter.Edges[index] = edge with
            {
                FromEpisodeId = string.Equals(edge.FromEpisodeId, oldId, StringComparison.Ordinal)
                    ? newId
                    : edge.FromEpisodeId,
                ToEpisodeId = string.Equals(edge.ToEpisodeId, oldId, StringComparison.Ordinal)
                    ? newId
                    : edge.ToEpisodeId
            };
        }
    }

    /// <summary>
    /// `스탯변화` 문법(`trust +1; anger -1`)을 푼다 — 문법을 읽는 자리는
    /// <see cref="StatDeltaParser"/> 하나다.
    ///
    /// <b>못 읽은 것은 조용히 버리지 않는다.</b> 옛 경로에서는 셀에 원문이 남아 리더가
    /// 다음 읽기에 짚어 줬는데, 이제 다시 읽는 일이 없어 여기서 안 막으면 영영 사라진다.
    /// </summary>
    private static IReadOnlyList<StatDelta> ParseDeltas(ChapterDocument chapter, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        StatDeltaParseResult parsed = StatDeltaParser.Parse(
            text, chapter.Stats.Select(stat => stat.Key).ToList());

        return parsed.IsValid
            ? parsed.Deltas
            : throw new InvalidOperationException(
                $"스탯변화 '{text}': {parsed.Problems[0].Message}");
    }
}


/// <summary>
/// 스탯 개명 한 판의 결과.
/// </summary>
/// <param name="Applied">개명했는가. false면 <b>아무것도 안 건드렸다</b>.</param>
/// <param name="Edges">`스탯변화`가 바뀐 간선 수.</param>
/// <param name="Conditions">식이 바뀐 챕터 조건 수.</param>
/// <param name="SuppliedConditions">
/// 판에 공급된 조건 중 식이 바뀐 수 — <b>Id를 지킨 채</b> 갈았다. 이걸 빠뜨리면 다음
/// 동기화가 새 Id로 다시 만들어 줄에 매달린 갈래가 전부 고아가 된다.
/// </param>
/// <param name="Fixtures">시작값을 갈아 끼운 픽스처 수 — 안 갈면 그 값이 조용히 사라진다.</param>
/// <param name="Refusal">거절 사유. <paramref name="Applied"/>가 false일 때만 있다.</param>
public sealed record StatRenameOutcome(
    bool Applied,
    int Edges,
    int Conditions,
    int SuppliedConditions,
    int Fixtures,
    string? Refusal)
{
    public static StatRenameOutcome Refuse(string reason) => new(false, 0, 0, 0, 0, reason);
}

/// <summary>
/// <b>무언가를 쓰고 있는 자리 하나</b> — 사람이 <b>찾아갈 수 있게</b> 적는다.
///
/// *"쓰이고 있습니다"*로 끝내면 챕터를 뒤져야 한다. 지우기를 거절하는 쪽이 이 목록을
/// 들고 와야 <b>거절이 곧 정리 안내</b>가 된다.
/// </summary>
/// <param name="Where">어디인가 — `간선 root → a "믿는다"` · `조건 '신뢰높음'`.</param>
/// <param name="Detail">무엇이 적혀 있는가 — 그 칸의 원문.</param>
public sealed record ChapterUse(ChapterUseKind Kind, string Where, string Detail);

public enum ChapterUseKind
{
    /// <summary>간선이 쓴다 — `스탯변화`이거나 표시/해금 조건 라벨.</summary>
    Edge,

    /// <summary>챕터 `조건`의 식이 쓴다.</summary>
    Condition,

    /// <summary>대사 줄의 갈래가 쓴다 — 판에 공급된 조건을 Id로 붙들고 있다.</summary>
    DialogueBranch
}

/// <summary>
/// 스탯 삭제 한 판의 결과.
/// </summary>
/// <param name="Applied">지웠는가. false면 <b>아무것도 안 건드렸다</b>.</param>
/// <param name="Fixtures">시작값을 함께 치운 픽스처 수 — 막을 이유가 없는 자리다.</param>
/// <param name="EdgesCleared">`clearUses`로 그 항을 뺀 간선 수.</param>
/// <param name="ConditionsCleared">`clearUses`로 <b>식을 비운</b> 조건 수 — 조건 자체는 남는다.</param>
/// <param name="Uses">
/// 거절의 <b>근거</b>. 여기가 비어 있지 않으면 그 자리를 먼저 비워야 지울 수 있다.
/// </param>
/// <param name="Refusal">거절 사유. <paramref name="Applied"/>가 false일 때만 있다.</param>
public sealed record StatRemoveOutcome(
    bool Applied,
    int Fixtures,
    int EdgesCleared,
    int ConditionsCleared,
    IReadOnlyList<ChapterUse> Uses,
    string? Refusal)
{
    public static StatRemoveOutcome Refuse(string reason, IReadOnlyList<ChapterUse> uses) =>
        new(false, 0, 0, 0, uses, reason);
}

/// <summary>챕터 조건 삭제 한 판의 결과.</summary>
/// <param name="Applied">지웠는가. false면 <b>아무것도 안 건드렸다</b>.</param>
/// <param name="SuppliedRemoved">판에서 함께 걷은 공급 조건 수.</param>
/// <param name="Uses">거절의 <b>근거</b> — 먼저 비워야 할 자리들.</param>
/// <param name="Refusal">거절 사유. <paramref name="Applied"/>가 false일 때만 있다.</param>
public sealed record ChapterConditionRemoveOutcome(
    bool Applied,
    int SuppliedRemoved,
    IReadOnlyList<ChapterUse> Uses,
    string? Refusal)
{
    public static ChapterConditionRemoveOutcome Refuse(string reason, IReadOnlyList<ChapterUse> uses) =>
        new(false, 0, uses, reason);
}

/// <summary>챕터 조건 라벨 바꾸기 한 판의 결과 — <see cref="ProjectEditor.RenameChapterCondition"/>.</summary>
/// <param name="Applied">바꿨는가. false면 <b>아무것도 안 건드렸다</b>.</param>
/// <param name="Edges">라벨이 따라간 간선 수(표시조건 · 해금조건).</param>
/// <param name="Refusal">거절 사유. <paramref name="Applied"/>가 false일 때만 있다.</param>
public sealed record ChapterConditionRenameOutcome(bool Applied, int Edges, string? Refusal)
{
    public static ChapterConditionRenameOutcome Refuse(string reason) => new(false, 0, reason);
}

/// <summary>챕터 개명이 모델에서 함께 바꾼 것 — <see cref="ProjectEditor.RenameChapter"/>의 결과.</summary>
/// <param name="BoardRenamed">판 이름이 따라갔는가(판이 아직 없었으면 거짓).</param>
/// <param name="SupplyRenamed">조건 공급 노드의 이름이 따라갔는가.</param>
public sealed record ChapterRenameEffect(bool BoardRenamed, bool SupplyRenamed);

