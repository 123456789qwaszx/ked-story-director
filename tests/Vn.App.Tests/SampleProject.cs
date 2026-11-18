using Vn.Authoring.Model;
using Vn.Authoring.Serialization;
using Vn.Authoring.Tests.Fixtures;

namespace Vn.App.Tests;

/// <summary>
/// 견본 챕터를 든 프로젝트를 폴더에 세운다 (2026-10-29 · D2).
///
/// 엑셀 연동이 있던 시절 화면 테스트는 엑셀 견본을 <c>chapters/</c>에 복사하고 세션이 그것을 자동으로
/// 들여오기를 기다렸다. 이제 챕터는 프로젝트에 산다 — 견본(<see cref="SampleChapters"/>)을 매니페스트에
/// 싣고 판과 카드를 세워(<see cref="SampleChapters.StandCards"/>) 저장한다.
/// </summary>
internal static class SampleProject
{
    /// <summary>매니페스트 파일 이름. 테스트끼리 같은 이름을 쓴다.</summary>
    public const string ManifestName = "project";

    /// <param name="chapters">견본 이름과 그 견본을 세울 챕터 Id.</param>
    /// <param name="configure">저장 전에 프로젝트를 더 다듬을 자리(판·화자 등).</param>
    /// <returns>저장된 매니페스트 경로.</returns>
    public static string Create(
        string directory,
        string title,
        IEnumerable<(string Sample, string ChapterId)> chapters,
        Action<StoryProject>? configure = null)
    {
        Directory.CreateDirectory(directory);

        var project = new StoryProject { Title = title };

        foreach ((string sample, string chapterId) in chapters)
        {
            project.Chapters.Add(SampleChapters.Load(sample, chapterId));
        }

        SampleChapters.StandCards(project);
        configure?.Invoke(project);

        string manifest = Path.Combine(directory, ManifestName + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, project);

        return manifest;
    }

    /// <summary>견본 하나(<c>ch05</c> 복도)를 그 이름 그대로 든 프로젝트.</summary>
    public static string Corridor(string directory, string title = "견본", Action<StoryProject>? configure = null) =>
        Create(directory, title, [(SampleChapters.Corridor, SampleChapters.Corridor)], configure);
}
