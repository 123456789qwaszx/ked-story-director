using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>화자는 프로젝트의 것이다</b> (2026-11-17 소유자 · <c>docs/work-orders/definition-split-orders.md</c> S3) — 「speaker는 전체 프로젝트 단위」.
/// 화자 명령은 조작마다 되돌리기 한 칸이고, 매니페스트와 되돌리기 스냅숏을 왕복한다.
/// </summary>
public class SpeakerCommandTests
{
    [Fact]
    public void 화자_명령은_조작마다_되돌리기_한_칸이고_같은_값은_기록하지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        long start = editor.Revision;

        editor.AddSpeaker("  라루 ");
        Assert.True(editor.SetSpeakerCharacterId("라루", " laru "));
        Assert.Equal(start + 2, editor.Revision);
        Assert.Equal(new Speaker("라루", "laru"), Assert.Single(editor.Project.Speakers));

        // 같은 값 · 없는 화자는 기록하지 않는다.
        Assert.False(editor.SetSpeakerCharacterId("라루", "laru"));
        Assert.False(editor.SetSpeakerCharacterId("없음", "x"));
        Assert.False(editor.RemoveSpeaker("없음"));
        Assert.Equal(start + 2, editor.Revision);

        Assert.True(editor.RemoveSpeaker("라루"));
        Assert.Empty(editor.Project.Speakers);

        editor.Undo();
        Assert.Equal(new Speaker("라루", "laru"), Assert.Single(editor.Project.Speakers));
        editor.Undo();
        Assert.Equal(new Speaker("라루", "temp_raru"), Assert.Single(editor.Project.Speakers));   // 더할 때 선 임시 키(UX29)
        editor.Undo();
        Assert.Empty(editor.Project.Speakers);   // 화자와 임시 키가 한 칸이다
    }

    [Fact]
    public void 빈_이름과_같은_이름은_서지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.AddSpeaker("라루");
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddSpeaker("  "));
        Assert.Throws<InvalidOperationException>(() => editor.AddSpeaker("라루"));
        Assert.Equal(revision, editor.Revision);
    }

    [Fact]
    public void 이름을_바꾸면_목록과_이미_쓴_대사가_한_칸에_따라가고_키는_그대로다()
    {
        var sample = new Sample();
        ProjectEditor editor = sample.Editor;
        editor.AddSpeaker("라루");
        editor.SetSpeakerCharacterId("라루", "laru");
        editor.AddSpeaker("윌로");
        Assert.True(editor.ApplyScenarioText(sample.Dialogue.Id, "라루: 안녕\n윌로: 응", confirmDeletes: true).Applied);
        long revision = editor.Revision;

        Assert.Equal(1, editor.RenameSpeaker("라루", "라루엘"));

        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal([new Speaker("라루엘", "laru"), new Speaker("윌로", "temp_willo")], editor.Project.Speakers);
        Assert.Equal(["라루엘", "윌로"], Speakers(sample));

        // 이미 있는 이름으로는 바꾸지 않는다 — 두 화자의 줄이 합쳐진다.
        Assert.Throws<InvalidOperationException>(() => editor.RenameSpeaker("라루엘", "윌로"));
        Assert.Equal(revision + 1, editor.Revision);

        editor.Undo();
        Assert.Equal("라루", editor.Project.Speakers[0].Name);
        Assert.Equal(["라루", "윌로"], Speakers(sample));
    }

    [Fact]
    public void 화자는_매니페스트와_되돌리기_스냅숏을_왕복한다()
    {
        var project = new StoryProject { Speakers = [new("라루", "laru"), new("늙은 상인", string.Empty)] };

        string manifest = ProjectManifestJson.Write(project);
        Assert.Equal(project.Speakers, ProjectManifestJson.Read(manifest).Speakers);
        Assert.Equal(project.Speakers, ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(project)).Speakers);

        // 화자가 없으면 칸을 쓰지 않는다.
        Assert.DoesNotContain("\"speakers\"", ProjectManifestJson.Write(new StoryProject()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "name": "윌로" }""")]                                                        // characterId가 빠졌다
    [InlineData("""{ "name": "", "characterId": "" }""")]                                         // 빈 이름
    [InlineData("""{ "name": "라루", "characterId": "laru" }, { "name": "라루", "characterId": "x" }""")]   // 겹친 이름
    [InlineData("\"라루\"")]                                                                    // 객체가 아니다
    public void 손으로_고친_매니페스트의_빠진_칸_빈_이름_겹친_이름은_받지_않는다(string items)
    {
        // 2026-11-18 · old-shapes O8 — ⛔ 그 전에는 빈 이름을 건너뛰고 같은 이름은 처음 것만 받았다(앞뒤 공백도 걷었다).
        string manifest = ProjectManifestJson.Write(new StoryProject()).Replace(
            "\"files\"",
            $$"""
            "speakers": [ {{items}} ],
            "files"
            """,
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => JsonSupport.ValidateProject(new StoryProject { Speakers = ProjectManifestJson.Read(manifest).Speakers.ToList() }));
    }

    private static List<string> Speakers(Sample sample) =>
        sample.Script.ActiveLines
            .Select(line => sample.Script.RequireLocale(sample.Script.PrimaryLocale).Entries[line.Id].Speaker)
            .ToList();
}
