using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>프로젝트 파일의 디스크 지문</b> (2026-11-19 · writer-release WR1) — 목차와 <c>story/</c>의 판 파일을 내용으로 견준다. 작가와 프로젝트를
/// Git으로 주고받으니, 열어 둔 사이 받은(Pull) 파일을 저장이 말없이 덮지 않게 하는 재료다.
/// </summary>
public sealed class ProjectDiskStampTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-disk-stamp", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 다시_찍으면_같고_내용이_바뀌거나_판_파일이_늘거나_줄면_다르다()
    {
        string manifest = SaveProject();
        ProjectDiskStamp opened = ProjectDiskStamp.Read(manifest);

        Assert.True(ProjectDiskStamp.Read(manifest).SameAs(opened));

        // 같은 내용으로 다시 쓰면(날짜만 바뀐다) 같은 것이다.
        string story = Directory.GetFiles(Path.Combine(_directory, "story")).Single();
        File.WriteAllBytes(story, File.ReadAllBytes(story));
        Assert.True(ProjectDiskStamp.Read(manifest).SameAs(opened));

        // 다른 곳이 고쳐 저장했다 — 화자 하나를 더한 프로젝트.
        ProjectEditor other = new(ProjectStore.Load(manifest).Project);
        other.AddSpeaker("하린");
        ProjectStore.Save(manifest, other.Project);
        Assert.False(ProjectDiskStamp.Read(manifest).SameAs(opened));

        // 판 파일이 늘었다(다른 곳이 챕터를 더했다) · 줄었다.
        ProjectDiskStamp now = ProjectDiskStamp.Read(manifest);
        string extra = Path.Combine(_directory, "story", "sf_zzzzzzzz.vnstory.json");
        File.WriteAllText(extra, "{}");
        Assert.False(ProjectDiskStamp.Read(manifest).SameAs(now));
        File.Delete(extra);
        Assert.True(ProjectDiskStamp.Read(manifest).SameAs(now));
        File.Delete(story);
        Assert.False(ProjectDiskStamp.Read(manifest).SameAs(now));
    }

    [Fact]
    public void exported와_보낼_곳_파일은_보지_않는다()
    {
        string manifest = SaveProject();
        ProjectDiskStamp opened = ProjectDiskStamp.Read(manifest);

        Directory.CreateDirectory(Path.Combine(_directory, "exported"));
        File.WriteAllText(Path.Combine(_directory, "exported", "ch01.progression.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, ExportDestinations.FileName), "{}");

        Assert.True(ProjectDiskStamp.Read(manifest).SameAs(opened));
    }

    private string SaveProject()
    {
        var editor = new ProjectEditor(new StoryProject { Title = "지문" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "opening");
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);
        return manifest;
    }
}
