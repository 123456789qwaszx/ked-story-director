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
    public void 진행_JSON의_스탯_DisplayName은_키다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "", 0, 0, sceneId: "ep01");
        editor.AddChapterStat("ch01", "mina_trust", maximum: 10);

        ChapterExportResult export = ChapterProgressionExporter.Export(editor.FindChapter("ch01")!.ToGraphModel("ch01"));
        JsonElement stat = JsonDocument.Parse(export.Json!).RootElement.GetProperty("Stats")[0];

        Assert.Equal(("mina_trust", "mina_trust"),
            (stat.GetProperty("Key").GetString(), stat.GetProperty("DisplayName").GetString()));
    }
}
