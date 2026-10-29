using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>열 때 판을 챕터에 맞춘다</b> (2026-10-29 · D2).
///
/// 엑셀 [대본 가져오기]가 걷히며, 그 단추가 세워 주던 빈 카드를 세울 곳이 없어졌다. 에피소드는
/// 있는데 카드가 없는 옛 프로젝트를 열면 그 에피소드는 <b>글을 쓸 자리가 없다</b> — 그래서 열 때
/// 맞춘다. 맞춘 것만으로는 "고쳤다" 표시가 뜨지 않는다.
/// </summary>
public sealed class OpenAlignmentTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-open-align", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 카드가_없는_에피소드는_열_때_빈_카드를_받고_고친_표시는_안_뜬다()
    {
        string manifest = SaveLegacyProject();

        var session = new AuthoringSession();
        session.Open(manifest);

        Assert.NotNull(EpisodeNaming.CardFor(session.Project, "ch01", "ep01"));
        Assert.NotNull(EpisodeNaming.CardFor(session.Project, "ch01", "ep02"));
        Assert.False(session.IsDirty);
        Assert.Contains("빈 카드", session.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void 맞춘_것은_저장하면_남는다()
    {
        string manifest = SaveLegacyProject();

        var session = new AuthoringSession();
        session.Open(manifest);
        string cardId = EpisodeNaming.CardFor(session.Project, "ch01", "ep01")!.Id;
        session.Save();

        var reopened = new AuthoringSession();
        reopened.Open(manifest);

        Assert.Equal(cardId, EpisodeNaming.CardFor(reopened.Project, "ch01", "ep01")!.Id);
        Assert.DoesNotContain("빈 카드", reopened.StatusMessage, StringComparison.Ordinal);
    }

    /// <summary>엑셀에서 들여온 옛 프로젝트처럼 — 챕터에 에피소드는 있는데 판도 카드도 없다.</summary>
    private string SaveLegacyProject()
    {
        Directory.CreateDirectory(_directory);

        var project = new StoryProject { Title = "옛 프로젝트" };
        var chapter = new ChapterDocument { ChapterId = "ch01" };

        foreach ((string episodeId, double x) in new[] { ("ep01", 0.0), ("ep02", 220.0) })
        {
            chapter.Episodes.Add(new ChapterEpisode(
                episodeId, episodeId, Index: string.Empty, DialogueEntry: episodeId,
                x, 0, Memo: null));
        }

        project.Chapters.Add(chapter);

        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, project);

        return manifest;
    }
}
