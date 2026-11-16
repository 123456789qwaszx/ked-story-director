using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 스탯은 키만 있다 (2026-11-16 · <c>docs/work-orders/writer-fields-orders.md</c> W2). 표시 이름은 걷혔다 — 게임 화면의 글자는 게임 쪽이 키로
/// 로컬라이즈한다. 옛 파일의 스탯 <c>displayName</c>은 읽지 않고 지나가고, 진행 JSON의 <c>DisplayName</c>에는 키가 나간다(계약 모양 그대로).
/// </summary>
public sealed class StatKeyOnlyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-stat-key-only", Guid.NewGuid().ToString("N"));

    public StatKeyOnlyTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 옛_파일의_스탯_표시_이름은_읽고_지나가고_다시_쓰지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddChapterStat("ch01", "mainatrust", maximum: 10);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        // 옛 툴이 쓴 모양 — 스탯마다 displayName이 있었다.
        string text = File.ReadAllText(manifest);
        File.WriteAllText(manifest, text.Replace("\"key\": \"mainatrust\",", "\"key\": \"mainatrust\",\n          \"displayName\": \"마이나의 신뢰도\",", StringComparison.Ordinal));
        Assert.Contains("마이나의 신뢰도", File.ReadAllText(manifest), StringComparison.Ordinal);

        StoryProject read = ProjectStore.Load(manifest).Project;
        Assert.Equal("mainatrust", Assert.Single(Assert.Single(read.Chapters).Stats).Key);

        ProjectStore.Save(manifest, read);
        Assert.DoesNotContain("displayName", File.ReadAllText(manifest), StringComparison.Ordinal);
    }

    [Fact]
    public void 진행_JSON의_스탯_DisplayName은_키다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "", 0, 0);
        editor.AddChapterStat("ch01", "마이나의 신뢰도", maximum: 10);

        ChapterExportResult export = ChapterProgressionExporter.Export(editor.FindChapter("ch01")!.ToGraphModel("ch01"));
        JsonElement stat = JsonDocument.Parse(export.Json!).RootElement.GetProperty("Stats")[0];

        Assert.Equal(("마이나의 신뢰도", "마이나의 신뢰도"),
            (stat.GetProperty("Key").GetString(), stat.GetProperty("DisplayName").GetString()));
    }
}
