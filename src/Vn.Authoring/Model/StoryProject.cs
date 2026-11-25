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
    /// 지금 쓰는 형식 — <b>5</b>: 조건 공급 노드가 없고 대본의 줄 전환이 챕터 조건을 이름으로 가리킨다(2026-11-25 · condition-supply C1).
    /// 4는 카드의 대본이 판 파일 안에 든 형식이었다(2026-10-29 · <c>docs/plans/script-merge.md</c>).
    /// <b>이 형식만 연다</b> — 그 앞의 형식은 옮기지 않고 거절한다(2026-11-17 · <c>docs/work-orders/legacy-read-orders.md</c> — 2026-11-25 소유자
    /// 「아직 본 작업에 들어가진 않아서 굳이 불필요한 형식을 신경쓰고 싶지 않아」).
    /// </summary>
    public const int CurrentFormatVersion = 5;

    public string Title { get; set; } = "새 프로젝트";

    public List<StoryFile> Files { get; init; } = new();

    /// <summary>
    /// <b>기획자의 챕터 — 에피소드 구조·간선·선택지·조건·스탯</b> (R-F · 2026-09-16,
    /// 지시서 §1.1).
    ///
    /// 이 목록이 서기 전까지 그 값들의 주인은 <c>chapters/{Id}.xlsx</c>였다 — 툴은
    /// 셀을 고치고 파일을 다시 읽었다. 이제 주인이 여기다(⛔ 워크북 산출은 2026-10-29에 엑셀 연동과 함께 걷혔다).
    ///
    /// <b>대본과 짝을 이룬다.</b> 카드의 대본(<see cref="DialogueNode.Script"/>)이 대사 본문의 원본이듯 이쪽이
    /// 구조의 원본이고, 둘을 잇는 것은 <c>DialogueNode.MarkedEpisodeId</c>다.
    /// </summary>
    public List<ChapterDocument> Chapters { get; init; } = new();

    // ⛔ `Links`(조건 공급 연결 — 범위 결정에서는 2026-08-17에 빠졌다)는 2026-11-17에 옛 데이터 읽기와 함께 걷혔다. 조건 범위는
    //    그 카드의 챕터다(<see cref="Flow.AvailableConditionResolver"/>) · docs/work-orders/legacy-read-orders.md L5. 유물이던 작가 화자
    //    `WriterSpeakers`도 함께 걷혔다.

    /// <summary>
    /// <b>화자 목록</b> — 대본의 화자 이름 ↔ 초상화 · 표정의 캐릭터 키. 챕터가 아니라 <b>프로젝트</b>의 것이다 — 모든 챕터가 같은 목록을 쓴다
    /// (2026-11-17 소유자 · <c>docs/work-orders/definition-split-orders.md</c> S3). 이름은 겹치지 않고 비지 않는다.
    ///
    /// ⛔ 그 전에는 프로젝트 옆 <c>game.definition.json</c>의 <c>speakers</c>에 살아 되돌리기 밖이었고, 고칠 때 바로 디스크에 썼다.
    /// </summary>
    public List<Speaker> Speakers { get; init; } = new();

    /// <summary>화자명에 잇는 캐릭터 키. 목록에 없거나 키가 비었으면 null.</summary>
    public string? FindSpeakerCharacterId(string? speakerName) =>
        FindSpeaker(speakerName) is { CharacterId: { Length: > 0 } characterId } ? characterId : null;

    /// <summary>그 이름의 화자 — 앞뒤 공백을 걷고 Ordinal로 찾는다. 없으면 null.</summary>
    public Speaker? FindSpeaker(string? speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return null;
        }

        string name = speakerName.Trim();
        return Speakers.FirstOrDefault(speaker => string.Equals(speaker.Name, name, StringComparison.Ordinal));
    }

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

    // ⛔ `EnumerateConditions` · `FindCondition`(공급 노드의 조건 사본 — 프로젝트 전체)은 2026-11-25에 공급 노드와 함께 걷혔다(condition-supply C1).
}
