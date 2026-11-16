using Vn.Authoring.Model;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 새로 만드는 챕터 · 장면 · 에피소드의 <b>자동 ID와 처음 이름</b> (2026-11-16 · <c>docs/work-orders/display-names-orders.md</c> N2).
///
/// [대본] 탭(작가 자리)은 이름만 다룬다 — 작가는 ID를 보지도 정하지도 않는다. 그래서 만들 때 ID는 겹치지 않는 자리표시가 붙고
/// (챕터 <c>ch01</c> · 장면 <c>scene01</c> · 에피소드 <c>new01</c>), 이름은 처음 이름이 채워진다. 같은 이름이 여럿이어도 ID가 달라
/// 문제없다. ID는 개발자가 [구성] 탭에서 바꾼다.
/// </summary>
public static class NewNames
{
    /// <summary>새 에피소드의 처음 제목.</summary>
    public const string Episode = "새 에피소드";

    /// <summary>새 장면의 처음 이름.</summary>
    public const string Scene = "새 장면";

    /// <summary>새 챕터의 처음 이름 — 작가가 이름을 비워 만들었을 때.</summary>
    public const string Chapter = "새 챕터";

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
            chapter.Episodes.Select(episode => episode.EffectiveSceneId).Concat(chapter.SceneNotes.Select(note => note.SceneId)),
            StringComparer.Ordinal);
        int number = 1;

        while (taken.Contains($"scene{number:D2}"))
        {
            number++;
        }

        return $"scene{number:D2}";
    }
}
