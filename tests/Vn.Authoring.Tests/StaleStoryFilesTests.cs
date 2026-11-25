using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>지운 챕터의 판 파일은 저장할 때 걷힌다</b>(2026-11-18 소유자 — 「확인하다 찾은 문제만 고쳐줘」 ·
/// stale-story-files). ⛔ 그 전에는 챕터를 지운 뒤 저장해도 그 판의 <c>story/sf_….vnstory.json</c>이 남아 쌓였다.
///
/// 못 박는 것: ① 목차에서 빠진 판 파일은 지우고 남은 판은 그대로 ② 툴이 지은 이름만 — 복사본 · 다른 이름 · 하위 폴더 · 다른 파일은 그대로
/// ③ 폴더에 다른 목차가 있으면 아무것도 지우지 않는다 ④ 되돌려서 다시 저장하면 판 파일이 다시 선다.
/// </summary>
public sealed class StaleStoryFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-stale-story-" + Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, ProjectFolder.DefaultManifestName);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 지운_챕터의_판_파일은_저장할_때_걷히고_남은_판과_사람의_파일은_그대로다()
    {
        (ProjectEditor editor, string kept, string removed) = SaveTwoChapters();

        // 사람이 둔 것들 — 지우면 안 된다.
        string story = Path.GetDirectoryName(removed)!;
        string copy = Path.Combine(story, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(removed)) + " - 복사본.vnstory.json");
        string otherName = Path.Combine(story, "notes.vnstory.json");
        string nested = Path.Combine(story, "old", Path.GetFileName(removed));
        string text = Path.Combine(story, "readme.txt");
        File.Copy(removed, copy);
        File.WriteAllText(otherName, "{}");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.Copy(removed, nested);
        File.WriteAllText(text, "메모");

        editor.RemoveChapterWithBoard("ch02");
        ProjectStore.Save(ManifestPath, editor.Project);

        Assert.False(File.Exists(removed));
        Assert.True(File.Exists(kept));
        Assert.All(new[] { copy, otherName, nested, text }, path => Assert.True(File.Exists(path), path));

        // 다시 열어도 멀쩡하다 — 남은 판은 목차가 가리키는 그대로다.
        StoryProject reopened = ProjectStore.Load(ManifestPath).Project;
        Assert.Equal(["ch01"], reopened.Chapters.Select(chapter => chapter.ChapterId));
    }

    [Fact]
    public void 되돌려서_다시_저장하면_판_파일이_다시_선다()
    {
        (ProjectEditor editor, _, string removed) = SaveTwoChapters();

        editor.RemoveChapterWithBoard("ch02");
        ProjectStore.Save(ManifestPath, editor.Project);
        Assert.False(File.Exists(removed));

        editor.Undo();
        ProjectStore.Save(ManifestPath, editor.Project);

        Assert.True(File.Exists(removed));
        Assert.Contains(ProjectStore.Load(ManifestPath).Project.Chapters, chapter => chapter.ChapterId == "ch02");
    }

    [Fact]
    public void 폴더에_다른_목차가_있으면_아무것도_지우지_않는다()
    {
        (ProjectEditor editor, _, string removed) = SaveTwoChapters();
        File.WriteAllText(Path.Combine(_directory, "other" + ProjectManifestJson.FileExtension), "{}");

        editor.RemoveChapterWithBoard("ch02");
        ProjectStore.Save(ManifestPath, editor.Project);

        Assert.True(File.Exists(removed));   // 다른 목차가 이 판을 가리킬 수 있다
    }

    [Theory]
    [InlineData("sf_c5irt5ve.vnstory.json", true)]
    [InlineData("sf_00000000.vnstory.json", true)]
    [InlineData("sf_c5irt5ve - 복사본.vnstory.json", false)]
    [InlineData("sf_C5IRT5VE.vnstory.json", false)]   // 툴은 소문자만 짓는다
    [InlineData("sf_c5irt5v.vnstory.json", false)]
    [InlineData("nd_c5irt5ve.vnstory.json", false)]
    [InlineData("notes.vnstory.json", false)]
    [InlineData("sf_c5irt5ve.json", false)]
    public void 툴이_지은_판_파일_이름만_알아본다(string name, bool toolMade) =>
        Assert.Equal(toolMade, ProjectStore.IsToolMadeStoryFileName(name));

    [Fact]
    public void 툴이_짓는_판_파일_이름은_알아보는_모양이다()
    {
        for (int index = 0; index < 50; index++)
        {
            Assert.True(ProjectStore.IsToolMadeStoryFileName(new StoryFile().RelativePath.Split('/')[^1]));
        }
    }

    /// <summary>챕터 둘(판 둘)을 저장한다 — ch01의 판 파일 · ch02의 판 파일 경로를 돌려준다.</summary>
    private (ProjectEditor Editor, string Kept, string Removed) SaveTwoChapters()
    {
        Directory.CreateDirectory(_directory);
        var editor = new ProjectEditor(new StoryProject { Title = "판 파일 정리" });

        foreach (string chapterId in new[] { "ch01", "ch02" })
        {
            editor.EnsureChapter(chapterId);
            editor.EnsureChapterBoard(chapterId);
            editor.AddEpisode(chapterId, chapterId + "_a", title: "a", 0, 0, sceneId: "s1");
        }

        ProjectStore.Save(ManifestPath, editor.Project);

        string PathOf(string chapterId) => Path.GetFullPath(Path.Combine(
            _directory, editor.Project.Files.Single(file => file.Name == chapterId).RelativePath));

        string kept = PathOf("ch01");
        string removed = PathOf("ch02");
        Assert.True(File.Exists(kept));
        Assert.True(File.Exists(removed));
        return (editor, kept, removed);
    }
}
