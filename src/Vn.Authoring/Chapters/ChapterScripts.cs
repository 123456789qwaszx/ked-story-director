using System.Globalization;
using Vn.Authoring.Model;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>그 챕터의 대본</b> — 에피소드의 대사 노드(에피소드 차례)와 판의 분기 카드. 캐릭터 키 검사(<see cref="ChapterSpeakerKeys"/> · writer-comfort
/// WC6)와 쓴 글의 양(<see cref="Count"/> · WC7)이 같은 차례로 훑는다.
/// </summary>
public static class ChapterScripts
{
    /// <summary>대본이 사는 곳 — (노드, 사람이 읽는 이름). 에피소드 차례, 그다음 분기 카드.</summary>
    public static IEnumerable<(DialogueNode Node, string Place)> PlacesOf(ChapterGraphModel chapter, StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);

        ChapterBoard board = ChapterBoard.For(project, chapter.ChapterId);

        foreach (ChapterEpisode episode in chapter.Episodes)
        {
            if (board.EpisodeNodeFor(episode) is { } node)
            {
                yield return (node, string.Format(Msg.Diag_Episode, episode.ShownTitle));
            }
        }

        foreach (DialogueNode card in EpisodeNaming.BoardOf(project, chapter.ChapterId)?.Nodes.OfType<DialogueNode>()
                     .Where(node => node.IsBranchCard) ?? [])
        {
            yield return (card, string.Format(Msg.Diag_BranchCard, EpisodeNaming.BranchLabelOf(card)));
        }
    }

    /// <summary>
    /// <b>쓴 글의 양</b>(2026-11-18 소유자 — 「챕터에 작성해둔 글자 수를 표기해주면 작가들이 좋아할 것입니다. 자기의 작업량을 알 수 있으니까요」 ·
    /// writer-comfort WC7). 대사 · 지문의 글(기본 언어)만 센다 — 화자 이름은 고르는 것이라 세지 않는다. 글자는 사람이 보는 글자 하나씩이다
    /// (한글 음절 하나 · 이모지 하나가 1). 줄은 글이 있는 줄만.
    /// </summary>
    public static ChapterTextCount Count(ChapterGraphModel chapter, StoryProject project)
    {
        int characters = 0;
        int withoutSpaces = 0;
        int lines = 0;

        foreach ((DialogueNode node, _) in PlacesOf(chapter, project))
        {
            foreach (Script.ScriptLine line in node.Script.ActiveLines)
            {
                string text = node.Script.Text(line.Id).Text ?? string.Empty;

                if (text.Trim().Length == 0)
                {
                    continue;
                }

                lines++;
                characters += new StringInfo(text).LengthInTextElements;
                withoutSpaces += new StringInfo(string.Concat(text.Where(letter => !char.IsWhiteSpace(letter)))).LengthInTextElements;
            }
        }

        return new ChapterTextCount(characters, withoutSpaces, lines);
    }
}

/// <summary>쓴 글의 양 — 글자(공백 포함) · 글자(공백 제외) · 글이 있는 줄.</summary>
public readonly record struct ChapterTextCount(int Characters, int CharactersWithoutSpaces, int Lines);
