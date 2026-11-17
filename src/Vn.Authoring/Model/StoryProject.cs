using Vn.Authoring.Chapters;
using Vn.Authoring.Script;

namespace Vn.Authoring.Model;

/// <summary>
/// 저작 도구가 다루는 공식 원본.
///
/// 세 종류의 데이터가 한 지붕 아래 있고, 서로 겹치지 않는다.
/// <code>
/// Files/Nodes   대사 카드 — 제 대본(줄 정체성과 locale별 화자·대사)과 그 줄들에 얹는 대사 논리 · 실행 흐름
/// Chapters      기획자의 챕터 구조
/// </code>
///
/// 대본은 2026-10-29까지 카드와 따로 선 목록(<c>Scripts</c>)이었고 카드가 Id로 가리켰다. 카드와 늘 1:1이 된 뒤로
/// 카드가 제 대본을 직접 든다(<see cref="DialogueNode.Script"/> · <c>docs/plans/script-merge.md</c> S2).
///
/// <b>연출은 여기 없다</b> (2026-10-29). 연출 노드 · 명령 공급 · 발행 결과 · 곡선 · 빠른 명령 · 에셋
/// 설정은 <c>ked-story-compositor</c>의 것이다 — 그 툴은 이 파일을 읽기만 하고 연출은 제 파일에 둔다.
///
/// 프로젝트는 여러 <see cref="StoryFile"/>을 가지고, 각 파일이 노드를 소유한다.
/// 프로젝트 전체 노드 순서가 필요할 때는 <see cref="EnumerateNodes"/>를 사용한다.
/// 그 순서는 파일 순서 뒤에 각 파일 안의 노드 순서를 이어 붙인 것이다.
/// </summary>
public sealed class StoryProject
{
    /// <summary>
    /// 지금 쓰는 형식 — <b>4</b>: 카드의 대본이 판 파일 안에 든다(2026-10-29 · <c>docs/plans/script-merge.md</c>).
    /// <b>이 형식만 연다</b> — 그 앞의 형식은 옮기지 않고 거절한다(2026-11-17 · <c>docs/work-orders/legacy-read-orders.md</c>).
    /// </summary>
    public const int CurrentFormatVersion = 4;

    public string Title { get; set; } = "새 프로젝트";

    public List<StoryFile> Files { get; init; } = new();

    /// <summary>
    /// <b>기획자의 챕터 — 에피소드 구조·간선·선택지·조건·스탯</b> (R-F · 2026-09-16,
    /// 지시서 §1.1).
    ///
    /// 이 목록이 서기 전까지 그 값들의 주인은 <c>chapters/{Id}.xlsx</c>였다 — 툴은
    /// 셀을 고치고 파일을 다시 읽었다. 이제 주인이 여기이고 워크북은 <b>산출물</b>이다.
    ///
    /// <b>대본과 짝을 이룬다.</b> 카드의 대본(<see cref="DialogueNode.Script"/>)이 대사 본문의 원본이듯 이쪽이
    /// 구조의 원본이고, 둘을 잇는 것은 <c>DialogueNode.MarkedEpisodeId</c>다.
    /// </summary>
    public List<ChapterDocument> Chapters { get; init; } = new();

    // ⛔ `Links`(조건 공급 연결 — 범위 결정에서는 2026-08-17에 빠졌다)는 2026-11-17에 옛 데이터 읽기와 함께 걷혔다. 조건 범위는
    //    판이다(<see cref="Flow.ConnectedSetNodeResolver"/>) · docs/work-orders/legacy-read-orders.md L5.

    /// <summary>
    /// <b>유물이다</b> (2026-08-23 소유자: "작가가 더한 화자라는 개념도 없애는 게 맞을 것
    /// 같아 — 이런 캐릭터는 컨셉과 배경이 꼼꼼히 정해져야 하는데 작가가 임의로 추가한다는
    /// 게 좋아보이진 않아서"). 화자의 주인은 기획자 하나다: 챕터 `화자` 시트와
    /// `game.definition.json`.
    ///
    /// <b>아무도 읽지 않는다</b> — 더하는 화면도, 고르는 목록도 사라졌다. 그래도 지우지
    /// 않는 이유는 구판 프로젝트 파일의 값을 조용히 날리지 않기 위해서다(이 저장소의
    /// "구판 데이터는 지우지 않고 무시한다" 규칙). 읽고 다시 쓰기만 한다.
    /// </summary>
    public List<WriterSpeaker> WriterSpeakers { get; init; } = new();

    /// <summary>이야기가 시작되는 노드. null이면 아직 정하지 않은 것이다.</summary>
    public string? StartNodeId { get; set; }

    /// <summary>[대본] 판의 크기(2026-11-17) — [⚙ 설정]에서 고친다. 바꾸는 길은 <see cref="Editing.ProjectEditor.SetBoardSize"/> 하나다.</summary>
    public BoardSize Board { get; set; } = BoardSize.Default;

    /// <summary>
    /// 프로젝트 전체 노드를 결정적인 순서로 펼친다.
    /// 파일 순서 → 각 파일의 Nodes 순서다.
    /// </summary>
    public IEnumerable<StoryNode> EnumerateNodes()
    {
        return Files.SelectMany(file => file.Nodes);
    }

    public StoryFile? FindFile(string? fileId)
    {
        return fileId is null
            ? null
            : Files.FirstOrDefault(file => string.Equals(file.Id, fileId, StringComparison.Ordinal));
    }

    public StoryFile? FindFileContainingNode(string? nodeId)
    {
        return nodeId is null
            ? null
            : Files.FirstOrDefault(file => file.Nodes.Any(
                node => string.Equals(node.Id, nodeId, StringComparison.Ordinal)));
    }

    public StoryNode? FindNode(string? nodeId)
    {
        return nodeId is null
            ? null
            : EnumerateNodes().FirstOrDefault(
                node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));
    }

    public DialogueNode? FindDialogue(string? nodeId) => FindNode(nodeId) as DialogueNode;

    /// <summary>모든 대사 카드의 대본 — 카드 순서대로. 화자 개명처럼 대본 전체를 훑는 일이 쓴다.</summary>
    public IEnumerable<ScriptDocument> EnumerateScripts() =>
        EnumerateNodes().OfType<DialogueNode>().Select(node => node.Script);

    /// <summary>
    /// 프로젝트 안의 모든 SetNode 조건. 파일 순서, SetNode 순서, 조건 순서를 지킨다.
    /// 편집·진단용 전체 조회이며 DialogueNode의 드롭다운 범위에는 사용하지 않는다.
    /// Dialogue별 범위는 AvailableConditionResolver가 판을 기준으로 계산한다.
    /// </summary>
    public IEnumerable<ConditionDefinition> EnumerateConditions()
    {
        return EnumerateNodes().OfType<SetNode>().SelectMany(node => node.Conditions);
    }

    public ConditionDefinition? FindCondition(string? conditionId)
    {
        return conditionId is null
            ? null
            : EnumerateConditions()
                .FirstOrDefault(item => string.Equals(item.Id, conditionId, StringComparison.Ordinal));
    }
}
