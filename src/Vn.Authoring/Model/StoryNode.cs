using Vn.Authoring.Flow;

namespace Vn.Authoring.Model;

/// <summary>그래프에 배치된 좌표와 폭. 파일 순서와는 아무 관계가 없다.</summary>
public sealed class NodeLayout
{
    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>
    /// 사람이 [대본] 판에서 끌어 넓힌 카드 폭. <c>null</c>이면 판의 기본 폭이다 — 자리처럼 판의 모양이라 프로젝트에 싣는다.
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// 사람이 [대본] 판에서 끌어 늘린 카드 글 칸의 높이. <c>null</c>이면 판의 기본 높이다. 카드 전체가 아니라 글 칸의 높이인
    /// 까닭은 카드의 나머지(머리)를 판이 정하기 때문이다 — 사람이 정하는 것은 글 칸의 높이다. (⛔ 머리 밑에서 갈래 수만큼 늘고 줄던 포트 줄은
    /// 2026-11-19에 걷혔다 · usability UX31.)
    /// </summary>
    public double? WritingHeight { get; set; }
}

/// <summary>
/// 그래프에 놓이는 노드의 공통 부분.
///
/// 파일에서의 순서는 소유한 <see cref="StoryFile.Nodes"/> 안의 순서이고,
/// 화면에서의 위치는 <see cref="Layout"/>이다. 둘은 별개의 개념이며 서로를 따라가지 않는다.
/// </summary>
public abstract class StoryNode
{
    protected StoryNode(string? id, string name)
    {
        Id = id ?? Identifier.Node();
        Name = name;
    }

    public string Id { get; }

    /// <summary>작가가 읽고 바꾸는 이름. 식별자가 아니므로 중복되어도 연결이 끊어지지 않는다.</summary>
    public string Name { get; set; }

    public NodeLayout Layout { get; set; } = new();

    /// <summary>
    /// 이 노드에서 나가는 실행 선의 모양 — 열쇠는 <see cref="EdgeShape.KeyOf(ExitPortKind, string?)"/>이다 (G3 · 2026-10-29).
    /// 사람이 고친 선만 있다. 출구를 끊으면 그 선의 모양도 함께 걷힌다.
    /// </summary>
    public Dictionary<string, EdgeShape> EdgeShapes { get; init; } = new(StringComparer.Ordinal);

    // ⛔ `DefaultExitTargetNodeId`(기본 출구 — 노드가 끝난 뒤 이동할 노드 · 판 파일 `defaultExit`)는 2026-11-17에 걷혔다
    //    (`docs/work-orders/default-exit-orders.md`). 에피소드 사이의 흐름은 챕터 간선이, 분기 카드는 부른 자리로 돌아오는 것이 맡아
    //    쓰는 화면이 남지 않았다.

    // ⛔ `BranchExits`(갈래 출구 — 조건 갈래가 끝나면 들를 카드 · 판 파일 전환의 `exit` · `anchor`)는 2026-11-19에 걷혔다(usability UX28).
    //    대상을 가리지 않아 아무도 부르지 않는 분기 카드에 이으면 내보낸 Yarn이 없는 노드를 불러 게임이 멈췄고, 에피소드에 이으면 그
    //    에피소드가 진행 기록 밖에서 재생됐다. 조건이 맞을 때만 들르는 것은 분기점 + 분기 카드의 첫머리 조건이 한다.
}

/// <summary>
/// <b>챕터 조건 공급 노드</b> — 판마다 하나, 이름은 <c>챕터 {판} 조건</c>(<see cref="Chapters.ChapterBoardSupply"/>). 챕터의 조건을 대본이
/// 쓰는 식(<c>stat("키")</c>)으로 옮긴 사본을 들고, 대본의 줄 전환은 그 사본의 Id를 가리킨다. 공급만 세우고 고치며 판에는 그리지 않는다.
///
/// ⛔ 옛 「X 설정」 노드(작가 조건 · 변수를 담던 자리)는 2026-11-18에 받지 않게 됐다 — 설정 노드는 공급 노드뿐이다(old-shapes O6).
/// </summary>
public sealed class SetNode : StoryNode
{
    public SetNode(string? id = null, string name = "새 설정")
        : base(id, name)
    {
    }

    /// <summary>이 노드가 이후 대사 노드에 공급하는 조건들.</summary>
    public List<ConditionDefinition> Conditions { get; init; } = new();
}

/// <summary>
/// 공급 노드가 든 조건 사본 하나.
///
/// <see cref="Name"/>은 작가가 읽는 이름이고 그래프 간선에도 이것이 표시된다.
/// <see cref="Expression"/>은 게임이 평가할 식이며 VnTool은 내용을 해석하지 않는다.
/// 이 분리가 있어야 게임마다 다른 식을 쓰면서도 저작 화면은 똑같이 동작한다.
/// </summary>
public sealed class ConditionDefinition
{
    public ConditionDefinition(string? id = null)
    {
        Id = id ?? Identifier.Condition();
    }

    public string Id { get; }

    public string Name { get; set; } = string.Empty;

    public string Expression { get; set; } = string.Empty;
}

/// <summary>
/// 대본 하나를 읽어 그 줄들에 <b>대사 논리</b>를 얹는 노드.
///
/// 줄 순서와 화자·대사는 카드가 든 대본(<see cref="Script"/>)이 소유한다. 그 위에 카드가 소유하는 것은
/// LineId에 매달린 조건 전환과 출구다.
///
/// 이 분리 덕분에 대본을 다시 읽어 문구가 바뀌어도 조건 구조가 그대로 남고,
/// 같은 문장을 고칠 수 있는 자리가 도구 안에 하나만 존재한다.
///
/// 출구는 조건 갈래 출구(특정 조건 갈래를 지났을 때만 이동한다)와 분기점(들렀다 돌아온다)이다. 노드가 끝난 뒤의 흐름은 챕터
/// 간선이 맡는다(에피소드) — 분기 카드는 부른 자리로 돌아온다.
///
/// <b>조건 갈래 출구는 갈래를 여는 줄의 Id로 기억한다.</b> 갈래의 마지막 줄에 매달면
/// 줄을 하나 추가하는 순간 출구가 갈래 중간에 파묻히고, 그 아래 대사가 실행되는
/// 모순된 구조가 된다. 여는 줄은 갈래 그 자체이므로 줄을 아무리 넣고 옮겨도
/// 출구는 언제나 그 갈래의 것으로 남고, 화면에는 항상 갈래의 현재 마지막 줄에 표시된다.
/// </summary>
public sealed class DialogueNode : StoryNode
{
    public DialogueNode(string? id = null, string name = "새 대사")
        : base(id, name)
    {
    }

    /// <summary>
    /// 이 카드의 대본 — 줄 신원(LineId) · 줄 개정 번호 · 은퇴한 줄 · locale별 화자 · 대사. 카드마다 하나다.
    ///
    /// 2026-10-29까지는 대본이 프로젝트에 따로 서고(<c>StoryProject.Scripts</c>) 카드는 <c>ScriptId</c>로 가리켰다.
    /// 따로 둔 까닭(작가 대본 가져오기 · 대본 나눠 쓰기 · 엑셀 대본 워크북)이 모두 걷혀 카드가 직접 든다
    /// (<c>docs/plans/script-merge.md</c> S2).
    /// </summary>
    public Script.ScriptDocument Script { get; set; } = new();

    /// <summary>
    /// <b>분기 카드인가</b> (2026-11-17 · <c>docs/work-orders/branch-card-orders.md</c>) — 다른 카드 대사 중의 분기점(<c>&lt;&lt;detour&gt;&gt;</c>)으로
    /// 들렀다 <b>돌아오는</b> 카드. 챕터의 에피소드가 아니다 — 게임의 「지금 에피소드」가 되지 않으므로 선택지 칸이 없고 진행 JSON에 실리지 않는다
    /// (Yarn 노드만). 「분기 추가」와 빈 곳에 놓은 분기점이 세운다. 만들 때 정하는 저장된 신원이다(판 파일의 <c>branchCard</c>).
    /// ⛔ R7 결정 ⑤(2026-09-17 — 판의 카드는 전부 에피소드)를 분기 카드에 한해 뒤집었다: 분기로 들른 카드의 선택지는 게임에 나오지 않는다.
    /// </summary>
    public bool IsBranchCard { get; set; }

    /// <summary>
    /// <b>분기 카드의 보이는 제목</b> (2026-11-17 · <c>docs/work-orders/auto-id-orders.md</c> I4) — [대본] 탭이 보이고 고친다(처음
    /// 「새 분기」, 비면 이름이 보인다). 분기 카드의 <see cref="StoryNode.Name"/>은 ID(= Yarn 노드 이름)이고 [구성] 탭에서 정한다 — 에피소드의
    /// 제목(<c>ChapterEpisode.Title</c>)과 EpisodeId가 갈린 것과 같다. 에피소드 카드에는 쓰지 않는다(제목은 챕터가 든다).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 이 노드가 <b>어느 에피소드인가</b>를 적어 둔 표식.
    ///
    /// <b>2026-09-17까지 이름이 <c>ExcelEpisodeId</c>였고, 그 이름이 두 번 거짓이 됐다.</b>
    /// <list type="number">
    /// <item><b>엑셀은 주인이 아니다</b> (R-D·R-F, 2026-09-16). 옛 머리글은 <i>"본문·화자·줄
    ///   구성은 엑셀이 소유하고 툴에서는 읽기 전용 — 여기서 고쳐도 다음 동기화가 되돌린다"</i>
    ///   였는데, <b>그 동기화가 철거됐다.</b> 대본의 원본은 프로젝트다.</item>
    /// <item><b>null이 「자유 노드」를 뜻하지 않는다</b> (R7 P-6 · 결정 ⑤, 2026-09-17).
    ///   자유 씬이라는 종류가 없어졌다 — 챕터 판의 카드는 에피소드이거나 분기 카드(2026-11-17)다.</item>
    /// </list>
    ///
    /// <b>에피소드 카드에는 늘 있고 분기 카드에는 없다</b> — 이름도 그 EpisodeId다(저장 검증이 지킨다 · 2026-11-18 old-shapes O4 · O5 — ⛔ 그
    /// 전에는 이름과 Id가 다를 때만 적었고, 없으면 이름으로 읽었다). 답을 내는 자리는 <see cref="Chapters.EpisodeNaming"/> 하나다.
    ///
    /// <b>파생값이 아니라 저장된 신원이다.</b> 잃으면 어느 카드가 어느 에피소드였는지 <b>복원할 수 없다</b>.
    /// </summary>
    public string? MarkedEpisodeId { get; set; }

    /// <summary>
    /// LineId별 대사 논리. 목록 순서는 파일에서 읽는 순서일 뿐이고 실행 순서가 아니다.
    /// 실행 순서는 언제나 대본이 정한다.
    /// </summary>
    public List<DialogueLineExtension> LineExtensions { get; init; } = new();

    /// <summary>
    /// <b>마지막 줄 뒤에 남는 전환들</b> (2026-08-24 소유자 — 대사 없는 조건 블록).
    ///
    /// 전환은 늘 <em>다음</em> 대사 줄 앞에 실린다(<see cref="DialogueLineExtension"/>).
    /// 그런데 대본이 조건 블록으로 끝나면 실을 줄이 없다 — 그리고 <b>대사 없는 블록이
    /// 대본의 전부</b>일 수도 있다(조건이 서면 커스텀 씬으로 detour만 하는 에피소드).
    /// 그 블록이 살 자리가 여기다.
    ///
    /// 줄에 실린 전환과 <b>같은 종류의 목록</b>이고 순서도 같은 뜻이다 — 다른 것은
    /// "어느 줄 앞"이 아니라 "모든 줄 뒤"라는 자리뿐이다. 그래서 흐름 해석은 줄을 다 돈
    /// 뒤 이 목록을 같은 기계에 한 번 더 먹인다.
    /// </summary>
    public List<LineConditionTransition> TrailingTransitions { get; init; } = new();

    /// <summary>
    /// <b>작가용 글 서식</b> (2026-11-22 소유자 · <c>docs/plans/writer-formatting.md</c>) — LineId → 그 줄 대사 글(기본 언어 <c>Text</c>)의 서식 구간들.
    /// 작가가 보기 위한 것이라 <b>내보내기(대사 Yarn · 진행 JSON)가 읽지 않는다</b>(WF-D1 · D2). 대사 논리(<see cref="LineExtensions"/>)와 섞지 않는다.
    /// 목록은 늘 정규형이고(<see cref="WriterMarkMath"/>) 빈 목록은 두지 않는다. 은퇴한 줄의 서식도 본문처럼 남긴다 — 되살아나면 돌아온다.
    /// 대사 글이 바뀌면 편집기가 자리를 옮긴다(<see cref="WriterMarkMath.Rebase"/>).
    /// </summary>
    public Dictionary<string, IReadOnlyList<WriterSpan>> WriterMarks { get; init; } = new(StringComparer.Ordinal);

    /// <summary>카드 글 칸의 좌우 정렬(작가용 서식) — 카드 하나에 하나. 기본은 왼쪽이고 그때는 저장하지 않는다.</summary>
    public WriterAlign WriterAlign { get; set; }

    /// <summary>
    /// 카드 글 칸의 처음 글꼴 · 글자 크기(작가용 서식 · <see cref="Model.WriterBase"/>) — 만들 때 그 PC의 설정 값이 적힌다. 서식의 크기는 이것에 더한다.
    /// null이면 아직 안 적힌 카드다(이 값이 생기기 전에 만든 카드) — 앱이 파일을 열 때 그 PC의 설정으로 적는다(<see cref="WriterBaseFill"/>). 내보내기는 읽지 않는다.
    /// </summary>
    public WriterBase? WriterBase { get; set; }

    // ⛔ `ChoiceExits`(선택지 문구 → 거쳐 갈 자유 씬 — 2026-08-17)는 2026-11-17에 옛 데이터 읽기와 함께 걷혔다. 대사 안 선택지가
    //    2026-11-04에 걷힌 뒤로 만드는 곳도 읽는 곳도 없이 파일을 오가기만 했다(docs/work-orders/legacy-read-orders.md L5).
    // `ExcelLineMap`(인덱스 → LineId)은 2026-09-16에 걷혔다 (R-D, 지시서 §2:
    //    "툴이 LineId의 주인"). 워크북을 <b>다시 읽을 때</b> 어느 줄이 어느 줄인지
    //    맞추려고 들고 있던 표다 — 다시 읽지 않으면 맞출 상대가 없고, 신원은 이제
    //    프로젝트가 그냥 갖는다.

    /// <summary>그 줄의 확장 — 없으면 만들어 붙인다. 빈 확장은 저장 때 알아서 빠진다.</summary>
    public DialogueLineExtension RequireExtension(string lineId)
    {
        if (FindExtension(lineId) is { } found)
        {
            return found;
        }

        var made = new DialogueLineExtension(lineId);
        LineExtensions.Add(made);

        return made;
    }

    public DialogueLineExtension? FindExtension(string? lineId)
    {
        return lineId is null
            ? null
            : LineExtensions.FirstOrDefault(
                item => string.Equals(item.LineId, lineId, StringComparison.Ordinal));
    }
}
