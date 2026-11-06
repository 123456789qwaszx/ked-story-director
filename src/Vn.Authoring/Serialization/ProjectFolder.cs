namespace Vn.Authoring.Serialization;

/// <summary>
/// <b>프로젝트는 폴더다</b> (2026-11-04 소유자: *"마야든 3dmax든 폴더 단위로 프로젝트를 관리하는데, 그 프로젝트 전체를
/// 다른 이름으로 저장한다는 생각이 얼마나 관행과 다른 행동인지"*).
///
/// 그 전에는 프로젝트가 <b>manifest 파일 하나</b>였고 사용자가 이름을 지었다 — 저장 창이 `.vnproject.json` 대신
/// `.json`만 붙여 compositor의 연출 파일 짝이 어긋났고(실사례 `Festival_Fix.json`), 같은 폴더에 둘을 저장하면
/// `story/` 판 파일을 두 manifest가 공유했다. 이제 사람은 <b>폴더</b>만 고른다: 새 프로젝트는 그 안에 고정 이름으로
/// 깔리고, 열 때는 폴더 안의 유일한 manifest를 찾는다. 복제는 탐색기, 버전은 git이 한다.
///
/// ked-story-director와 ked-story-compositor가 <b>같은 폴더</b>를 연다 — 같은 규칙이 두 저장소에 사본으로 있다.
/// </summary>
public static class ProjectFolder
{
    /// <summary>새 프로젝트의 manifest 이름. 사람이 짓지 않는다 — 확장자 사고가 날 자리가 없다.</summary>
    public const string DefaultManifestName = "project" + ProjectManifestJson.FileExtension;

    /// <summary>폴더 안의 manifest를 찾은 결과. <see cref="ManifestPath"/>가 null이면 <see cref="Problem"/>이 이유다.</summary>
    public sealed record Found(string? ManifestPath, string? Problem)
    {
        public bool IsFound => ManifestPath is not null;
    }

    /// <summary>
    /// 폴더 안의 <b>유일한</b> `*.vnproject.json`. 없으면 "없다", 둘 이상이면 "어느 것인지 말할 수 없다" —
    /// 아무거나 고르면 사람이 고른 적 없는 프로젝트가 열린다.
    /// </summary>
    public static Found Find(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return new Found(null, $"폴더가 없습니다: {folder}");
        }

        string[] manifests = Directory.GetFiles(folder, "*" + ProjectManifestJson.FileExtension, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return manifests.Length switch
        {
            0 => new Found(null,
                $"이 폴더에는 프로젝트({ProjectManifestJson.FileExtension})가 없습니다: {Path.GetFileName(folder)}"),
            1 => new Found(manifests[0], null),
            _ => new Found(null,
                $"이 폴더에 프로젝트 파일이 {manifests.Length}개입니다 — 하나만 남기세요: " +
                string.Join(", ", manifests.Select(Path.GetFileName)))
        };
    }

    /// <summary>폴더가 프로젝트 폴더인가(manifest가 정확히 하나).</summary>
    public static bool IsProjectFolder(string folder) => Find(folder).IsFound;

    /// <summary>새 프로젝트가 설 manifest 자리 — 폴더 안의 고정 이름.</summary>
    public static string NewManifestPathIn(string folder) =>
        Path.Combine(Path.GetFullPath(folder), DefaultManifestName);

    /// <summary>
    /// 새 프로젝트를 깔 수 있는 폴더인가 — 비어 있거나, 프로젝트 파일이 없는 폴더. 이미 프로젝트가 있으면
    /// 그것을 <b>열어야지</b> 덮어쓰면 안 된다.
    /// </summary>
    public static string? WhyCannotCreateIn(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return "폴더를 고르지 않았습니다.";
        }

        if (!Directory.Exists(folder))
        {
            return null;   // 만들면 된다.
        }

        Found found = Find(folder);

        return found.IsFound
            ? $"이 폴더에는 이미 프로젝트가 있습니다({Path.GetFileName(found.ManifestPath)}) — 새로 만들지 않고 엽니다."
            : null;
    }
}
