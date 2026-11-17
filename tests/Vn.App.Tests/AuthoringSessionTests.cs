using System.Text;
using Vn.App.Services;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

public class AuthoringSessionTests : IDisposable
{
    [Fact]
    public void 새_프로젝트는_판_없이_시작한다()
    {
        // 2026-11-17 소유자 — 낙서판(「기본 파일」)을 걷었다. 판은 챕터가 세운다 — 첫 [＋ 대사 노드]가 챕터 · 장면과 함께 세운다.
        var session = new AuthoringSession();

        Assert.Empty(session.Project.Files);
        Assert.Null(session.ActiveFile);
        Assert.Contains("[＋ 대사 노드]", session.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void 프로젝트를_열면_시작_노드가_속한_파일이_현재_파일이_된다()
    {
        var project = new StoryProject { Title = "테스트" };
        var first = new StoryFile("sf_a", "A", "story/a.vnstory.json");
        var second = new StoryFile("sf_b", "B", "story/b.vnstory.json");
        project.Files.Add(first);
        project.Files.Add(second);
        first.Nodes.Add(new DialogueNode("nd_a", "A"));
        second.Nodes.Add(new DialogueNode("nd_b", "B"));
        project.StartNodeId = "nd_b";

        string directory = TempDirectory();
        string path = Path.Combine(directory, "project" + ProjectManifestJson.FileExtension);

        try
        {
            ProjectStore.Save(path, project);
            var session = new AuthoringSession();

            session.Open(path);

            Assert.Equal(second.Id, session.ActiveFileId);
            Assert.Equal("nd_b", session.SelectedNodeId);
            Assert.All(session.Project.Files, file => Assert.True(session.ExpandedFileIds.Contains(file.Id)));
            Assert.Equal(Path.GetFullPath(path), session.ProjectPath);
            Assert.False(session.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void dirty_비교는_디스크가_아니라_ProjectSnapshotCodec을_사용한다()
    {
        AuthoringSession session = OpenWithChapters("ch01");
        StoryFile file = session.ActiveFile!;

        Assert.False(session.IsDirty);

        DialogueNode node = session.Editor.AddDialogueNode(file.Id, name: "새 장면", sceneId: "scene01");
        Assert.True(session.IsDirty);

        session.Editor.Undo();
        Assert.False(session.IsDirty);

        session.Editor.Redo();
        Assert.True(session.IsDirty);
        Assert.NotNull(session.Project.FindNode(node.Id));
    }

    [Fact]
    public void 이전_형식을_열면_거부하고_세션은_그대로_남는다()
    {
        string directory = TempDirectory();
        string legacyPath = Path.Combine(directory, "legacy.vnproject.json");
        File.WriteAllText(legacyPath, """
            {
              "formatVersion": 2,
              "title": "이전",
              "files": []
            }
            """, new UTF8Encoding(false));

        try
        {
            var session = new AuthoringSession();
            string? pathBefore = session.ProjectPath;
            int nodesBefore = session.Project.EnumerateNodes().Count();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => session.Open(legacyPath));

            Assert.Contains("열지 않습니다", error.Message, StringComparison.Ordinal);
            Assert.Equal(pathBefore, session.ProjectPath);
            Assert.Equal(nodesBefore, session.Project.EnumerateNodes().Count());
            Assert.False(session.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }


    [Fact]
    public void 연_프로젝트의_모든_판은_기본적으로_그래프에_펼쳐지고_새_판도_펼쳐진다()
    {
        AuthoringSession session = OpenWithChapters("ch01");
        StoryFile first = Assert.Single(session.Project.Files);
        session.Editor.EnsureChapter("ch02");
        StoryFile second = session.Project.FindFile(session.Editor.EnsureChapterBoard("ch02"))!;

        Assert.True(session.ExpandedFileIds.Contains(first.Id));
        Assert.True(session.ExpandedFileIds.Contains(second.Id));
        Assert.Equal(
            new[] { first.Id, second.Id },
            session.ExpandedFileIds.OrderBy(id => session.Project.Files.FindIndex(file => file.Id == id)));
    }

    [Fact]
    public void 파일_선택은_펼침을_접지_않는다_여러_파일을_함께_본다()
    {
        // 선택과 펼침은 독립이다 — "선택=그 파일만 펼침"이면 여러 파일을 오가며 함께
        // 볼 수 없다. 접힌 파일을 고르면 펴 주기만 한다.
        AuthoringSession session = OpenWithChapters("ch01", "ch02");
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Project.Files.Single(file => file.Id != first.Id);
        session.Editor.AddDialogueNode(first.Id, name: "첫 파일");
        session.Editor.AddDialogueNode(second.Id, name: "둘째 파일");

        session.SelectFile(second.Id);

        Assert.Equal(second.Id, session.ActiveFileId);
        Assert.True(session.ExpandedFileIds.Contains(first.Id));  // 다른 파일은 접히지 않는다
        Assert.True(session.ExpandedFileIds.Contains(second.Id));

        // 접어 둔 파일을 고르면 펴 주기만 한다 — 고른 파일이 안 보이는 일은 없다.
        session.SetFileExpanded(first.Id, expanded: false);
        session.SelectFile(first.Id);
        Assert.True(session.ExpandedFileIds.Contains(first.Id));
        Assert.True(session.ExpandedFileIds.Contains(second.Id));
    }

    [Fact]
    public void 펼침_체크는_프로젝트_dirty와_Undo에_영향을_주지_않는다()
    {
        AuthoringSession session = OpenWithChapters("ch01");
        StoryFile file = session.ActiveFile!;

        session.SetFileExpanded(file.Id, expanded: false);

        Assert.False(session.IsDirty);
        Assert.False(session.Editor.CanUndo);
        Assert.False(session.ExpandedFileIds.Contains(file.Id));
    }

    [Fact]
    public void 새_노드는_ActiveFile의_마지막에_추가된다()
    {
        AuthoringSession session = OpenWithChapters("ch01", "ch02");
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Project.Files.Single(file => file.Id != first.Id);
        SetNode existing = session.Editor.AddSetNode(second.Id, name: "기존");

        session.SelectFile(second.Id);
        DialogueNode added = session.Editor.AddDialogueNode(session.ActiveFileId!, name: "추가");

        Assert.Empty(first.Nodes);
        Assert.Same(added, second.Nodes[^1]);
        Assert.Equal(new[] { existing.Id, added.Id }, second.Nodes.Select(node => node.Id));
    }

    [Fact]
    public void 파일_상태_변경_이벤트는_현재_선택과_펼침_변경을_구분한다()
    {
        AuthoringSession session = OpenWithChapters("ch01", "ch02");
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Project.Files.Single(file => file.Id != first.Id);
        var changes = new List<FileGraphStateChangedEventArgs>();
        session.FileGraphStateChanged += (_, e) => changes.Add(e);

        session.SelectFile(second.Id);
        session.SetFileExpanded(first.Id, expanded: false);

        Assert.Collection(
            changes,
            active =>
            {
                // 이미 펼쳐진 파일의 선택은 펼침을 건드리지 않는다 (선택과 펼침은 독립이다).
                Assert.True(active.ActiveFileChanged);
                Assert.False(active.ExpandedFilesChanged);
                Assert.False(active.FileListChanged);
            },
            expanded =>
            {
                Assert.False(expanded.ActiveFileChanged);
                Assert.True(expanded.ExpandedFilesChanged);
                Assert.False(expanded.FileListChanged);
            });
    }

    [Fact]
    public void 파일_추가와_노드_개수_변경은_파일_목록_갱신으로_알린다()
    {
        var session = new AuthoringSession();
        var changes = new List<FileGraphStateChangedEventArgs>();
        session.FileGraphStateChanged += (_, e) => changes.Add(e);

        StoryFile added = NewBoard(session, "ch09");
        session.Editor.AddDialogueNode(added.Id, name: "장면");

        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.True(change.FileListChanged));
        Assert.True(changes[0].ExpandedFilesChanged);
        Assert.False(changes[1].ExpandedFilesChanged);
    }


    [Fact]
    public void 접어_둔_파일은_다른_프로젝트_편집_뒤에도_접힌_상태를_유지한다()
    {
        AuthoringSession session = OpenWithChapters("ch01", "ch02");
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Project.Files.Single(file => file.Id != first.Id);
        DialogueNode node = session.Editor.AddDialogueNode(first.Id, name: "장면");

        session.SetFileExpanded(second.Id, expanded: false);
        session.Editor.RenameNode(node.Id, "수정된 장면");

        Assert.False(session.ExpandedFileIds.Contains(second.Id));
        Assert.True(session.ExpandedFileIds.Contains(first.Id));
    }

    [Fact]
    public void Undo로_파일이_사라지면_workspace_상태에서_정리되고_Redo시_다시_펼쳐진다()
    {
        var session = new AuthoringSession();
        StoryFile added = NewBoard(session, "ch09");

        Assert.True(session.ExpandedFileIds.Contains(added.Id));

        session.Editor.Undo();

        Assert.Null(session.Project.FindFile(added.Id));
        Assert.DoesNotContain(added.Id, session.ExpandedFileIds);

        session.Editor.Redo();

        Assert.NotNull(session.Project.FindFile(added.Id));
        Assert.Contains(added.Id, session.ExpandedFileIds);
    }

    // ── 튜닝 관리 ───────────────────────────────────────────────────

    /// <summary>
    /// 화자는 프로젝트의 것이다(2026-11-17 · definition-split S3) — 고칠 때마다 되돌리기 한 칸이고, 저장하면 매니페스트에 남는다.
    /// 정의 파일은 만들지 않는다. ⛔ 그 전에는 고칠 때 바로 game.definition.json에 썼다(되돌리기 밖).
    /// </summary>
    [Fact]
    public void 화자는_프로젝트의_것이라_되돌리기_한_칸이고_저장하면_매니페스트에_남는다()
    {
        string directory = TempDirectory();

        try
        {
            string manifest = Path.Combine(directory, "project.vnproject.json");
            var session = new AuthoringSession();
            long before = session.Editor.Revision;

            // 저장 전 프로젝트에서도 고친다.
            Assert.Null(session.AddSpeakerName("라루"));
            Assert.Null(session.AddSpeakerName("윌로우"));
            Assert.True(session.RemoveSpeakerName("윌로우"));
            Assert.False(session.SetSpeakerCharacterId("라루", string.Empty));   // 같은 값은 기록하지 않는다
            Assert.Equal(before + 3, session.Editor.Revision);
            Assert.Equal("라루", Assert.Single(session.Project.Speakers).Name);

            session.Editor.Undo();
            Assert.Equal(["라루", "윌로우"], session.Project.Speakers.Select(speaker => speaker.Name));

            session.Save(manifest);
            Assert.Contains("\"speakers\"", File.ReadAllText(manifest));
            Assert.False(File.Exists(Path.Combine(directory, "game.definition.json")));

            var reopened = new AuthoringSession();
            reopened.Open(manifest);
            Assert.Equal(["라루", "윌로우"], reopened.Project.Speakers.Select(speaker => speaker.Name));
            Assert.False(reopened.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// <b>옮김</b>(definition-split S3) — 프로젝트에 화자가 없고 옆 정의 파일에 speakers가 있으면 열 때 들인다(「고쳤다」가 선다). 저장하면
    /// 프로젝트에 남고 정의 파일에서 걷힌다 — 남의 칸은 둔다. 프로젝트에 화자가 있으면 정의 파일의 것은 보지 않는다.
    /// </summary>
    [Fact]
    public void 열_때_정의_파일의_화자를_프로젝트로_옮기고_저장하면_정의_파일에서_걷는다()
    {
        string directory = TempDirectory();

        try
        {
            string manifest = Path.Combine(directory, "project.vnproject.json");
            string definition = Path.Combine(directory, "game.definition.json");
            new AuthoringSession().Save(manifest);
            File.WriteAllText(definition, """
                { "speakers": [ { "name": "라루", "characterId": "laru" }, { "name": "윌로", "characterId": "" } ],
                  "presentationCommands": [ { "id": "shake" } ] }
                """);

            var session = new AuthoringSession();
            session.Open(manifest);

            Assert.Equal([("라루", "laru"), ("윌로", "")], session.Project.Speakers.Select(speaker => (speaker.Name, speaker.CharacterId)));
            Assert.True(session.IsDirty);
            Assert.Contains("화자 2명을", session.StatusMessage, StringComparison.Ordinal);

            session.Save();
            string left = File.ReadAllText(definition);
            Assert.DoesNotContain("speakers", left, StringComparison.Ordinal);
            Assert.Contains("presentationCommands", left, StringComparison.Ordinal);

            // 이미 옮겼으면 정의 파일의 화자는 보지 않는다(다음 저장에서 걷힌다).
            File.WriteAllText(definition, """{ "speakers": [ { "name": "유령", "characterId": "ghost" } ] }""");
            var again = new AuthoringSession();
            again.Open(manifest);
            Assert.Equal(["라루", "윌로"], again.Project.Speakers.Select(speaker => speaker.Name));
            Assert.False(again.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ── 새 프로젝트 자동 준비 + PNG 가져오기 ────────────────────────

    [Fact]
    public void 보낼_곳은_프로젝트_폴더에_살고_저장만이_보내기를_부탁한다()
    {
        // 프로젝트는 폴더다 — 폴더의 runtime.target.json이 형식마다 보낼 곳을 든다(2026-11-17 · export-destinations G1). 보내기는 저장할
        // 때만이다 — 세션은 부탁만 하고 [구성]의 다시 읽기가 exported/를 새로 쓴 뒤에 보낸다.
        string project = TempDirectory();
        string game = TempDirectory();

        try
        {
            var session = new AuthoringSession();
            session.Save(Path.Combine(project, "project.vnproject.json"));
            Assert.True(session.Destinations.IsEmpty);
            Assert.False(session.TakeDeliveryRequest());   // 보낼 곳이 없으면 부탁하지 않는다
            Assert.DoesNotContain("게임 폴더", session.StatusMessage);

            ExportDestinations.SaveBeside(
                session.ProjectPath!,
                ExportDestinations.None.With(ExportFormat.Progression, ExportDestinations.Relativize(session.ProjectPath!, game)));
            session.ReloadDestinations();
            Assert.Equal(Path.GetFullPath(game), session.Destinations.ResolveExisting(session.ProjectPath!, ExportFormat.Progression));

            // 고치기만으로는 부탁하지 않는다 — 저장이 부탁하고, 한 번 물으면 내린다.
            session.Editor.EnsureChapter("ch01");
            Assert.False(session.TakeDeliveryRequest());
            session.Save();
            Assert.Contains("보낼 곳에도 보냅니다", session.StatusMessage);
            Assert.True(session.TakeDeliveryRequest());
            Assert.False(session.TakeDeliveryRequest());

            // 다시 열어도 설정이 따라온다 — 파일이 프로젝트 폴더에 산다. 열기는 부탁하지 않는다.
            var reopened = new AuthoringSession();
            reopened.Open(session.ProjectPath!);
            Assert.Equal(Path.GetFullPath(game), reopened.Destinations.ResolveExisting(reopened.ProjectPath!, ExportFormat.Progression));
            Assert.Contains("저장하면 보냅니다: 진행 JSON", reopened.StatusMessage);
            Assert.False(reopened.TakeDeliveryRequest());
        }
        finally
        {
            Directory.Delete(project, recursive: true);
            Directory.Delete(game, recursive: true);
        }
    }

    /// <summary>
    /// 저장은 정의 파일을 만들지 않고, 있으면 이 툴이 쓰던 칸(variables · conditions · speakers · 이 툴의 안내)만 걷는다 — 남의 칸은 둔다.
    /// 남는 것이 없으면 파일을 지운다(definition-split S3). ⛔ 그 전에는 없으면 뼈대를 깔았다.
    /// </summary>
    [Fact]
    public void 저장은_정의_파일을_만들지_않고_이_툴이_쓰던_칸만_걷는다()
    {
        string directory = TempDirectory();

        try
        {
            var session = new AuthoringSession();
            session.Save(Path.Combine(directory, "project.vnproject.json"));

            string definition = Path.Combine(directory, "game.definition.json");
            Assert.False(File.Exists(definition));

            File.WriteAllText(definition, """
                { "variables": [ { "name": "trust" } ], "conditions": [], "speakers": [],
                  "presentationCommandCategories": [ { "id": "camera" } ] }
                """);
            session.Save();
            Assert.Equal(
                ["presentationCommandCategories"],
                ((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(definition))!).Select(item => item.Key));
            Assert.Contains("걷었습니다", session.StatusMessage, StringComparison.Ordinal);

            // 걷을 것이 없으면 건드리지 않는다.
            DateTime written = File.GetLastWriteTimeUtc(definition);
            session.Save();
            Assert.Equal(written, File.GetLastWriteTimeUtc(definition));

            // 이 툴의 칸과 안내뿐이면 파일째 지운다.
            File.WriteAllText(definition, """{ "_안내": "게임이 아는 어휘의 원천입니다.", "variables": [], "speakers": [] }""");
            session.Save();
            Assert.False(File.Exists(definition));

            // 깨진 파일은 건드리지 않는다.
            File.WriteAllText(definition, "{ 이건 JSON이 아님");
            session.Save();
            Assert.Equal("{ 이건 JSON이 아님", File.ReadAllText(definition));
            Assert.False(Directory.Exists(Path.Combine(directory, "assets"))); // 에셋 폴더는 compositor의 일이다
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>챕터 판만 든 프로젝트를 저장하고 연다 — 깨끗한 세션(고친 것 없음), 지금 판은 첫 챕터의 판이다.</summary>
    private AuthoringSession OpenWithChapters(params string[] chapterIds)
    {
        var editor = new Vn.Authoring.Editing.ProjectEditor(new StoryProject { Title = "판" });

        foreach (string chapterId in chapterIds)
        {
            editor.EnsureChapter(chapterId);
            editor.EnsureChapterBoard(chapterId);
        }

        string directory = TempDirectory();
        _directories.Add(directory);
        string path = Path.Combine(directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(path, editor.Project);

        var session = new AuthoringSession();
        session.Open(path);
        session.SelectFile(EpisodeNamingBoard(session, chapterIds[0]));
        return session;
    }

    /// <summary>챕터와 그 판을 한 되돌리기 칸에 세운다 — 판이 생기는 일 하나.</summary>
    private static StoryFile NewBoard(AuthoringSession session, string chapterId)
    {
        string boardId = string.Empty;
        session.Editor.Batch(() =>
        {
            session.Editor.EnsureChapter(chapterId);
            boardId = session.Editor.EnsureChapterBoard(chapterId);
        });

        return session.Project.FindFile(boardId)!;
    }

    private static string EpisodeNamingBoard(AuthoringSession session, string chapterId) =>
        Vn.Authoring.Chapters.EpisodeNaming.BoardOf(session.Project, chapterId)!.Id;

    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (string directory in _directories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string TempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"VnTool.Session.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
