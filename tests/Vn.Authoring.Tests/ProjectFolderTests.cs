using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>프로젝트는 폴더다</b> (2026-11-04 소유자) — 새로/열기는 폴더, manifest 이름은 툴이 짓고, 게임 폴더는 폴더 안
/// <c>runtime.target.json</c> 하나로 두 툴이 나눈다. "다른 이름으로 저장"은 걷혔다(복제는 탐색기 · 버전은 git).
/// </summary>
public sealed class ProjectFolderTests
{
    private static string Temp()
    {
        string path = Path.Combine(Path.GetTempPath(), "vn-folder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void 폴더_안의_유일한_manifest를_찾는다()
    {
        string folder = Temp();

        try
        {
            Assert.False(ProjectFolder.Find(folder).IsFound);
            Assert.Contains("없습니다", ProjectFolder.Find(folder).Problem);

            string manifest = ProjectFolder.NewManifestPathIn(folder);
            Assert.EndsWith("project.vnproject.json", manifest);
            ProjectStore.Save(manifest, new StoryProject { Title = "t" });

            Assert.Equal(manifest, ProjectFolder.Find(folder).ManifestPath);
            Assert.True(ProjectFolder.IsProjectFolder(folder));

            // 기존 예제처럼 다른 이름이어도 하나면 찾는다 — 이름은 규칙이 아니라 기본값이다.
            File.Move(manifest, Path.Combine(folder, "festival_eve.vnproject.json"));
            Assert.EndsWith("festival_eve.vnproject.json", ProjectFolder.Find(folder).ManifestPath);

            // 둘이면 고르지 않는다 — 사람이 고른 적 없는 프로젝트를 열면 안 된다.
            File.WriteAllText(manifest, "{}");
            ProjectFolder.Found two = ProjectFolder.Find(folder);
            Assert.False(two.IsFound);
            Assert.Contains("2개", two.Problem);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void 이미_프로젝트가_있는_폴더에는_새로_깔지_않는다()
    {
        string folder = Temp();

        try
        {
            Assert.Null(ProjectFolder.WhyCannotCreateIn(folder));
            Assert.Null(ProjectFolder.WhyCannotCreateIn(Path.Combine(folder, "없는-하위")));

            ProjectStore.Save(ProjectFolder.NewManifestPathIn(folder), new StoryProject());

            Assert.Contains("이미 프로젝트가 있습니다", ProjectFolder.WhyCannotCreateIn(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void 보낼_곳은_상대_경로로_남고_없으면_풀리지_않고_모르는_칸은_지킨다()
    {
        string root = Temp();

        try
        {
            string project = Path.Combine(root, "proj");
            string game = Path.Combine(root, "game", "ChapterProgression");
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(game);
            string manifest = Path.Combine(project, "project.vnproject.json");

            Assert.True(ExportDestinations.LoadBeside(manifest).IsEmpty);

            string relative = ExportDestinations.Relativize(manifest, game);
            Assert.Equal("../game/ChapterProgression", relative);

            ExportDestinations.SaveBeside(manifest, ExportDestinations.None
                .With(ExportFormat.Progression, relative)
                .With(ExportFormat.DialogueYarn, "../yarn"));
            ExportDestinations loaded = ExportDestinations.LoadBeside(manifest);
            Assert.Equal((relative, null, "../yarn"), (loaded.Progression, loaded.Scenes, loaded.DialogueYarn));
            Assert.Equal(Path.GetFullPath(game), loaded.ResolveExisting(manifest, ExportFormat.Progression));
            Assert.Contains("\"gameFolder\"", File.ReadAllText(ExportDestinations.PathFor(manifest)), StringComparison.Ordinal);   // compositor가 읽는 칸

            // 가리키는 폴더가 없으면 만들지 않는다 — 다른 PC에서 엉뚱한 자리에 쌓이지 않게.
            Assert.Null(loaded.ResolveExisting(manifest, ExportFormat.DialogueYarn));
            Directory.Delete(game, recursive: true);
            Assert.Null(loaded.ResolveExisting(manifest, ExportFormat.Progression));
            Assert.False(Directory.Exists(Path.GetFullPath(game)));

            // 모르는 칸(다른 툴의 것)은 지킨다 — 이 툴의 칸을 다 비워도 파일이 남는다.
            File.WriteAllText(ExportDestinations.PathFor(manifest), "{ \"gameFolder\": \"../g\", \"compositorOnly\": 3 }");
            ExportDestinations.SaveBeside(manifest, ExportDestinations.None);
            string kept = File.ReadAllText(ExportDestinations.PathFor(manifest));
            Assert.Contains("compositorOnly", kept, StringComparison.Ordinal);
            Assert.DoesNotContain("\"gameFolder\":", kept, StringComparison.Ordinal);

            // 남는 칸이 없으면 파일을 걷는다.
            File.WriteAllText(ExportDestinations.PathFor(manifest), "{ \"gameFolder\": \"../g\" }");
            ExportDestinations.SaveBeside(manifest, ExportDestinations.None);
            Assert.False(File.Exists(ExportDestinations.PathFor(manifest)));

            // 깨진 파일은 "없음"이다 — 설정 하나 때문에 프로젝트를 못 열면 안 된다.
            File.WriteAllText(ExportDestinations.PathFor(manifest), "{ not json");
            Assert.True(ExportDestinations.LoadBeside(manifest).IsEmpty);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void 내보내기는_exported에만_쓰고_나간_자리를_말한다()
    {
        // 보낼 곳(게임 폴더 등)은 저장할 때 ExportDelivery가 비춘다 — 내보내기 자체는 exported/만 쓴다(2026-11-17).
        string root = Temp();

        try
        {
            string manifest = Path.Combine(root, "project.vnproject.json");

            var editor = new ProjectEditor(new StoryProject());
            editor.EnsureChapter("ch01");
            editor.AddEpisode("ch01", "ep01", "첫 에피소드", 0, 0);
            ProjectStore.Save(manifest, editor.Project);

            var entries = new[] { new ChapterEntry("ch01", editor.FindChapter("ch01")!.ToGraphModel("ch01")) };
            ChapterExportRun run = new ChapterExportService().ExportAll(entries, manifest, editor.Project);

            Assert.True(run.AllExported);
            Assert.Equal([ChapterExportService.ExportPathFor(manifest, "ch01")], run.Written);
            Assert.Contains("진행 JSON → exported/ (ch01.progression.json)", run.WhereNotice);
            Assert.DoesNotContain("보낸 곳", run.WhereNotice);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
