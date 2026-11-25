using Vn.Authoring.Model;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 판의 <b>대사 노드</b>와 챕터의 <b>에피소드</b>를 잇는 이름 규약 — <b>한 벌이다</b>.
///
/// 그래프 투영·자리잡기·대본 탭·연출 그래프·챕터 편집·내보내기가 모두 이 규칙을 쓴다.
/// 한 규칙이 여러 곳에 살면 갈리므로 여기 한 곳에 둔다.
///
/// <b>표식이 신원이고 이름은 글자다.</b> <see cref="StoryNode.Name"/>은 사람이 판에서
/// 고치는 글자라 신원으로 삼으면 개명 때마다 끊긴다. 그래서 <see cref="DialogueNode.MarkedEpisodeId"/>가 신원이다 — 에피소드 카드는
/// 늘 표식을 든다(그렇지 않은 판은 저장 검증이 받지 않는다 · 2026-11-18 old-shapes O4 — ⛔ 그 전에는 표식 없는 구판 카드를 이름으로 찾았다).
///
/// <b>챕터 판의 대사 노드는 전부 에피소드</b>라서 이 규약이 늘 성립한다.
/// </summary>
public static class EpisodeNaming
{
    /// <summary>
    /// 이 판의 <b>챕터</b> — 늘 있다. 판은 챕터와 함께 서고 바뀌고 지워지며, 챕터가 아닌 판은 저장 검증이 받지 않는다(2026-11-18 · old-shapes O3 —
    /// ⛔ 그 전에는 옛 프로젝트에 남은 판에서 null이었다).
    ///
    /// <b>챕터 = 판 1:1이고, 잇는 것은 이름이다</b> (G-1 v2). 그래서 <b>여기 한 곳에만</b> 둔다.
    /// </summary>
    public static ChapterDocument ChapterOfBoard(StoryProject project, StoryFile board)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(board);

        return project.Chapters.FirstOrDefault(chapter =>
                   string.Equals(chapter.ChapterId, board.Name, StringComparison.Ordinal))
               ?? throw new InvalidOperationException(string.Format(Msg.Edit_BoardHasNoChapter, board.Name));
    }

    /// <summary>
    /// 그 챕터의 <b>판</b>. 없으면 <c>null</c>이다 — 만들지 않는다
    /// (만드는 것은 <c>ProjectEditor.EnsureChapterBoard</c>다).
    ///
    /// <see cref="ChapterOfBoard"/>의 역이고 <b>같은 한 줄</b>이다. 한 줄짜리라 복사가 쉽고,
    /// 쉬운 만큼 어디서 어떻게 찾는지가 갈리기 쉬워 여기 한 곳에 둔다.
    /// </summary>
    public static StoryFile? BoardOf(StoryProject project, string chapterId)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.Files.FirstOrDefault(file =>
            string.Equals(file.Name, chapterId, StringComparison.Ordinal));
    }

    /// <summary>
    /// 그 에피소드를 <b>지고 있는 카드</b>. 없으면 <c>null</c>이다.
    ///
    /// <b>그 챕터의 판에서만</b> 찾는다. EpisodeId는 챕터 안에서만 유일하므로, 프로젝트
    /// 전체를 훑으면 다른 챕터의 같은 Id를 집는다.
    ///
    /// 찾는 법이 여러 벌이면 서로 답이 갈린다. 그래서 카드 찾기는 이것 하나다(<see cref="ChapterBoard"/>도 같은 표식으로 찾는다).
    /// </summary>
    public static DialogueNode? CardFor(StoryProject project, string chapterId, string episodeId)
    {
        ArgumentNullException.ThrowIfNull(project);

        return BoardOf(project, chapterId)?.Nodes.OfType<DialogueNode>()
            .FirstOrDefault(node =>
                !node.IsBranchCard &&
                string.Equals(EpisodeIdOf(node), episodeId, StringComparison.Ordinal));
    }

    /// <summary>이 카드(프로젝트의 노드)가 선 <b>챕터</b> — 늘 있다(<see cref="ChapterOfBoard"/>).</summary>
    public static ChapterDocument ChapterOf(StoryProject project, StoryNode node)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(node);

        return ChapterOfBoard(project, project.FindFileContainingNode(node.Id)
            ?? throw new InvalidOperationException(string.Format(Msg.Edit_NodeIsnTInProject, node.Id)));
    }

    /// <summary>
    /// 그 카드가 화면에 보이는 이름 — 챕터의 에피소드면 그 제목(<see cref="ChapterEpisode.ShownTitle"/>), 분기 카드면 그 제목
    /// (<see cref="BranchTitleOf"/>), 아니면 노드 이름이다. [대본] 탭은 이 이름을 보이고 고친다. ID는 [구성] 탭에서 정한다.
    /// </summary>
    public static string ShownNameOf(StoryProject project, StoryNode node)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(node);

        if (node is DialogueNode { IsBranchCard: true } branch)
        {
            return BranchTitleOf(branch);
        }

        return node is DialogueNode dialogue &&
               ChapterOf(project, dialogue).Episodes.FirstOrDefault(episode =>
                   string.Equals(episode.EpisodeId, EpisodeIdOf(dialogue), StringComparison.Ordinal)) is { } found
            ? found.ShownTitle
            : node.Name;
    }

    /// <summary>분기 카드의 보이는 제목 — <see cref="DialogueNode.Title"/>, 비면 이름(ID)이다(2026-11-17).</summary>
    public static string BranchTitleOf(DialogueNode card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Title is { Length: > 0 } title ? title : card.Name;
    }

    /// <summary>
    /// 글(검증 · 상태줄)에서 분기 카드를 가리키는 말 — 「제목」(ID). 새 분기 카드는 모두 「새 분기」로 서므로 제목만으로는 어느 카드인지
    /// 모른다. 제목이 ID와 같으면(비었으면) 「ID」만.
    /// </summary>
    public static string BranchLabelOf(DialogueNode card)
    {
        ArgumentNullException.ThrowIfNull(card);

        string title = BranchTitleOf(card);
        return string.Equals(title, card.Name, StringComparison.Ordinal) ? $"「{title}」" : $"「{title}」({card.Name})";
    }

    /// <summary>그 카드가 대신하는 에피소드 Id — 에피소드 카드면 표식, 분기 카드면 그 이름(분기 카드 ID)이다.</summary>
    public static string EpisodeIdOf(DialogueNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.MarkedEpisodeId ?? node.Name;
    }

    /// <summary>이 카드가 대신하는 <b>에피소드</b> — 표식으로 찾는다. 분기 카드면 <c>null</c>이다.</summary>
    public static ChapterEpisode? EpisodeFor(ChapterDocument chapter, DialogueNode node)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(node);

        // 분기 카드는 에피소드가 아니다(2026-11-17) — 이름이 우연히 에피소드와 같아도 그 에피소드를 대신하지 않는다.
        if (node.IsBranchCard)
        {
            return null;
        }

        return chapter.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, node.MarkedEpisodeId, StringComparison.Ordinal));
    }
}
