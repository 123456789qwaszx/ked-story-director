using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Text;
using Vn.App.Strings;

namespace Vn.App.Views;

/// <summary>
/// <b>[대본] 카드의 보이는 제목을 고친다</b> — 인스펙터의 이름 칸(<see cref="DialogueNodeEditor"/>)과 카드 머리의 제목 칸(<see cref="GraphEditorView.BeginCardRename"/> ·
/// 2026-11-25 소유자 — 「에피소드의 제목을 바꾸려고 하거나 … 더블클릭을 했다가 원하지 않게 구성탭으로 이동이 되버리는 경우가 많더라고」)이 같은 길을 쓴다.
/// 제목만 고친다 — ID(에피소드ID · 분기 카드 ID)는 그대로이고 [구성] 탭에서 정한다. 비우면 ID가 보인다.
/// </summary>
internal static class CardTitles
{
    /// <summary>그 카드의 제목을 <paramref name="wanted"/>(앞뒤 공백은 걷는다)로 — 지금 보이는 제목과 같으면 아무것도 하지 않고 false.</summary>
    internal static bool Rename(AuthoringSession session, string nodeId, string? wanted)
    {
        if (session.Project.FindDialogue(nodeId) is not { } node)
        {
            return false;
        }

        string title = (wanted ?? string.Empty).Trim();

        if (string.Equals(title, EpisodeNaming.ShownNameOf(session.Project, node), StringComparison.Ordinal))
        {
            return false;
        }

        // 분기 카드 — 보이는 제목이다(2026-11-17). ID(= Yarn 노드 이름)는 [구성] 탭에서 정한다.
        if (node.IsBranchCard)
        {
            session.Editor.SetBranchCardTitle(node.Id, title);
            session.SetStatus(title.Length == 0
                ? string.Format(Ui.Side_ClearedTitleBranchCardID, node.Name, Josa.이가(node.Name))
                : string.Format(Ui.Side_RenamedTitleToBranchCard, title, Josa.으로(title), node.Name, Josa.은는(node.Name)));
            return true;
        }

        // 에피소드 카드 — EpisodeId는 그대로 두고 제목만 고친다(비우면 EpisodeId가 보인다).
        ChapterDocument chapter = EpisodeNaming.ChapterOf(session.Project, node);
        string episodeId = EpisodeNaming.EpisodeIdOf(node);
        session.Editor.UpdateEpisode(chapter.ChapterId, episodeId, title: title);
        session.SetStatus(title.Length == 0
            ? string.Format(Ui.Side_ClearedTitleEpisodeIDIs, episodeId, Josa.이가(episodeId))
            : string.Format(Ui.Side_RenamedTitleToEpisodeID, title, Josa.으로(title), episodeId, Josa.은는(episodeId)));
        return true;
    }
}
