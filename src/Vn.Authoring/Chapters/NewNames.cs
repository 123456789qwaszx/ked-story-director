using Vn.Authoring.Model;
using Vn.Authoring.Rendering;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 새로 만드는 챕터 · 장면 · 에피소드의 <b>자동 ID와 처음 이름</b> (2026-11-16 · <c>docs/work-orders/display-names-orders.md</c> N2).
///
/// [대본] 탭(작가 자리)은 이름만 다룬다 — 작가는 ID를 보지도 정하지도 않는다. 그래서 만들 때 ID는 겹치지 않는 자리표시가 붙고
/// (챕터 <c>ch01</c> · 장면 <c>scene01</c> · 에피소드 <c>ch01_scene01_new01</c> · 분기 카드 <c>ch01_scene01_branch01</c>), 이름은 처음
/// 이름이 채워진다. 같은 이름이 여럿이어도 ID가 달라 문제없다. ID는 개발자가 [구성] 탭에서 바꾼다.
///
/// <b>에피소드 · 분기 카드의 ID에는 챕터와 장면이 든다</b> (2026-11-17 · <c>docs/work-orders/auto-id-orders.md</c>) — 그 ID가 곧 Yarn 노드
/// 이름이고, 게임은 모든 챕터의 Yarn을 한 프로젝트로 컴파일하므로 프로젝트에서 하나여야 한다. ⛔ 전에는 챕터마다 <c>new01</c>부터 셌다.
/// </summary>
public static class NewNames
{
    /// <summary>새 에피소드의 처음 제목.</summary>
    public const string Episode = "새 에피소드";

    /// <summary>새 장면의 처음 이름.</summary>
    public const string Scene = "새 장면";

    /// <summary>새 챕터의 처음 이름 — 작가가 이름을 비워 만들었을 때.</summary>
    public const string Chapter = "새 챕터";

    /// <summary>새 분기 카드의 처음 제목(<see cref="DialogueNode.Title"/>).</summary>
    public const string Branch = "새 분기";

    /// <summary>
    /// 에피소드ID 자동 앞머리 — <c>{챕터}_{장면}_new</c>(장면이 없으면 <c>{챕터}_new</c>). Yarn 노드 이름 정규화를 지난 글자다 — 장면ID에 공백이
    /// 있어도 ID에는 들지 않는다.
    /// </summary>
    public static string EpisodePrefix(string chapterId, string? sceneId) => Prefix(chapterId, sceneId, "new");

    /// <summary>분기 카드 이름의 자동 앞머리 — <c>{챕터}_{장면}_branch</c>. 장면은 그 카드의 집(처음 부르는 에피소드)의 장면이다.</summary>
    public static string BranchPrefix(string chapterId, string? sceneId) => Prefix(chapterId, sceneId, "branch");

    /// <summary>그 ID가 그 앞머리의 자동 모양 그대로인가 — 앞머리 + 숫자 둘 이상.</summary>
    public static bool IsAuto(string id, string prefix) =>
        id.Length >= prefix.Length + 2 &&
        id.StartsWith(prefix, StringComparison.Ordinal) &&
        id[prefix.Length..].All(char.IsAsciiDigit);

    /// <summary>그 챕터 · 장면의 다음 에피소드ID — 이미 쓰인 이름(<see cref="TakenNames"/>)을 피한다.</summary>
    public static string NextEpisodeId(StoryProject project, ChapterDocument chapter, string? sceneId)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        return NextFree(EpisodePrefix(chapter.ChapterId, sceneId), TakenNames(project, chapter));
    }

    /// <summary>그 챕터 · 장면의 다음 분기 카드 이름 — 이미 쓰인 이름(<see cref="TakenNames"/>)을 피한다.</summary>
    public static string NextBranchName(StoryProject project, ChapterDocument chapter, string? sceneId)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        return NextFree(BranchPrefix(chapter.ChapterId, sceneId), TakenNames(project, chapter));
    }

    /// <summary>
    /// 이미 쓰인 이름 — 그 챕터의 에피소드ID와 <b>프로젝트의 모든 카드 이름</b>(그대로와 Yarn 노드 이름 둘 다). 카드 이름이 곧 Yarn 노드
    /// 이름이라 다른 챕터의 카드와도 겹치면 안 된다.
    /// </summary>
    public static HashSet<string> TakenNames(StoryProject project, ChapterDocument chapter)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(chapter);

        var taken = new HashSet<string>(chapter.Episodes.Select(episode => episode.EpisodeId), StringComparer.Ordinal);

        foreach (StoryNode node in project.EnumerateNodes())
        {
            taken.Add(node.Name);
            taken.Add(YarnNaming.StoryNodeTitleOf(node.Name, node.Id));
        }

        return taken;
    }

    /// <summary>앞머리 뒤에 <paramref name="from"/>부터 세어 처음 빈 번호(두 자리 이상)를 붙인다.</summary>
    public static string NextFree(string prefix, IReadOnlySet<string> taken, int from = 1)
    {
        ArgumentNullException.ThrowIfNull(taken);

        for (int number = Math.Max(from, 1); ; number++)
        {
            string candidate = $"{prefix}{number:D2}";

            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Prefix(string chapterId, string? sceneId, string kind) =>
        YarnSyntax.SanitizeNodeName(string.IsNullOrWhiteSpace(sceneId)
            ? $"{chapterId}_{kind}"
            : $"{chapterId}_{sceneId.Trim()}_{kind}");

    /// <summary>
    /// 그 프로젝트에서 아직 안 쓰인 챕터 ID — <c>ch01</c>, <c>ch02</c>… 진행 JSON 파일 이름이 되므로 대소문자를 가리지 않고 피한다
    /// (대소문자를 가리지 않는 파일 시스템에서 두 챕터가 한 파일을 쓰지 않게). 판 이름도 피한다(챕터 = 판 1:1).
    /// </summary>
    public static string NextChapterId(StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var taken = new HashSet<string>(
            project.Chapters.Select(chapter => chapter.ChapterId).Concat(project.Files.Select(file => file.Name)),
            StringComparer.OrdinalIgnoreCase);
        int number = 1;

        while (taken.Contains($"ch{number:D2}"))
        {
            number++;
        }

        return $"ch{number:D2}";
    }

    /// <summary>
    /// 그 챕터에서 아직 안 쓰인 장면 ID — <c>scene01</c>, <c>scene02</c>… 에피소드가 쓰는 장면ID뿐 아니라 <b>장면 항목(이름 · 메모)의
    /// 열쇠도 피한다</b> — 주인 없는 항목의 열쇠를 새 장면이 받으면 옛 이름 · 메모를 물려받는다.
    /// </summary>
    public static string NextSceneId(ChapterDocument chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var taken = new HashSet<string>(
            chapter.Episodes.Select(episode => episode.SceneId).Concat(chapter.SceneNotes.Select(note => note.SceneId)),
            StringComparer.Ordinal);
        int number = 1;

        while (taken.Contains($"scene{number:D2}"))
        {
            number++;
        }

        return $"scene{number:D2}";
    }
}
