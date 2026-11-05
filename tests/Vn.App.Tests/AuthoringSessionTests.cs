using System.Text;
using Vn.App.Services;
using Vn.Authoring.Definition;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

public class AuthoringSessionTests
{
    [Fact]
    public void 새_프로젝트는_기본_StoryFile을_현재_파일로_가진다()
    {
        var session = new AuthoringSession();

        StoryFile file = Assert.Single(session.Project.Files);
        Assert.Equal(file.Id, session.ActiveFileId);
        Assert.Same(file, session.ActiveFile);
        Assert.EndsWith(StoryFileJson.FileExtension, file.RelativePath);
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
        var session = new AuthoringSession();
        StoryFile file = session.ActiveFile!;

        Assert.False(session.IsDirty);
        DialogueNode node = session.Editor.AddDialogueNode(file.Id, name: "새 장면");
        Assert.True(session.IsDirty);

        session.Editor.Undo();
        Assert.False(session.IsDirty);

        session.Editor.Redo();
        Assert.True(session.IsDirty);
        Assert.NotNull(session.Project.FindNode(node.Id));
    }

    /// <summary>
    /// 형식 1 · 2는 자동 마이그레이션하지 않고 거부한다(형식 3은 옮긴다 — 기계적이라서). 이 테스트는 그 마이그레이션을
    /// 검증하던 테스트를 대체한다. 열지 못한 뒤에도 세션은 원래 상태로 계속 쓸 수 있어야 한다.
    /// </summary>
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

            Assert.Contains("더 이상 열 수 없습니다", error.Message, StringComparison.Ordinal);
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
    public void 새_프로젝트의_모든_파일은_기본적으로_그래프에_펼쳐진다()
    {
        var session = new AuthoringSession();
        StoryFile first = Assert.Single(session.Project.Files);

        StoryFile second = session.Editor.AddStoryFile("두 번째");

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
        var session = new AuthoringSession();
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Editor.AddStoryFile("두 번째");
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
        var session = new AuthoringSession();
        StoryFile file = session.ActiveFile!;

        session.SetFileExpanded(file.Id, expanded: false);

        Assert.False(session.IsDirty);
        Assert.False(session.Editor.CanUndo);
        Assert.False(session.ExpandedFileIds.Contains(file.Id));
    }

    [Fact]
    public void 새_노드는_ActiveFile의_마지막에_추가된다()
    {
        var session = new AuthoringSession();
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Editor.AddStoryFile("두 번째");
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
        var session = new AuthoringSession();
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Editor.AddStoryFile("두 번째");
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

        StoryFile added = session.Editor.AddStoryFile("추가");
        session.Editor.AddDialogueNode(added.Id, name: "장면");

        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.True(change.FileListChanged));
        Assert.True(changes[0].ExpandedFilesChanged);
        Assert.False(changes[1].ExpandedFilesChanged);
    }


    [Fact]
    public void 접어_둔_파일은_다른_프로젝트_편집_뒤에도_접힌_상태를_유지한다()
    {
        var session = new AuthoringSession();
        StoryFile first = session.ActiveFile!;
        StoryFile second = session.Editor.AddStoryFile("두 번째");
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
        StoryFile added = session.Editor.AddStoryFile("추가");

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
    /// 작가는 화자를 더하지 않는다 — 캐릭터는 컨셉·배경이 꼼꼼히 정해져야 하는 것이라
    /// 작가가 임의로 더할 자리가 아니다. 더하는 화면도 고르는 목록도 없지만, <b>구판 프로젝트의 값은 조용히 날리지 않는다</b> — 이 저장소의
    /// "구판 데이터는 지우지 않고 무시한다" 규칙이다.
    /// </summary>
    [Fact]
    public void 폐지된_작가_화자_데이터는_열고_저장해도_살아남는다()
    {
        string directory = TempDirectory();

        try
        {
            string path = Path.Combine(directory, "project.vnproject.json");
            var session = new AuthoringSession();
            session.Save(path);

            // 구판 프로젝트가 들고 있던 값 — 이제 아무 화면도 이걸 읽지 않는다.
            session.Project.WriterSpeakers.Add(new WriterSpeaker { Name = "구판화자" });
            session.Save(path);

            var reopened = new AuthoringSession();
            reopened.Open(path);
            Assert.Equal("구판화자", Assert.Single(reopened.Project.WriterSpeakers).Name);

            // 정의 파일은 손대지 않는다 (기획자 전용).
            Assert.DoesNotContain(reopened.Definition.Speakers, speaker =>
                string.Equals(speaker.Name, "구판화자", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 화자를 지우면 세션의 정의가 <b>곧바로</b> 바뀐다 — 챕터 그래프에서 화자를 지우면
    /// 연출 그래프도 옛 목록을 보이면 안 된다. 화면은 그릴 때마다 정의를 읽는다.
    /// </summary>
    [Fact]
    public void 화자를_지우면_정의가_곧바로_바뀐다()
    {
        string directory = TempDirectory();

        try
        {
            var session = new AuthoringSession();
            session.Save(Path.Combine(directory, "project.vnproject.json"));

            Assert.True(session.SaveSpeakers([
                new SpeakerSpec { Name = "라루" },
                new SpeakerSpec { Name = "윌로우" }
            ]));

            // 지우는 방식 — 남길 것만 넘긴다(통째로 쓰는 것이 규격이다).
            Assert.True(session.SaveSpeakers([new SpeakerSpec { Name = "라루" }]));

            Assert.Equal("라루", Assert.Single(session.Definition.Speakers).Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ── 새 프로젝트 자동 준비 + PNG 가져오기 ────────────────────────

    [Fact]
    public void 게임_폴더를_정하면_저장이_진행_JSON을_거기로도_낸다()
    {
        // 프로젝트는 폴더다 — 복제는 탐색기, 버전은 git이 맡는다.
        // 폴더의 runtime.target.json이 게임 폴더를 가리키고, 두 툴이 각자 산출물을 거기로 쓴다.
        string project = TempDirectory();
        string game = TempDirectory();

        try
        {
            var session = new AuthoringSession();
            session.Save(Path.Combine(project, "project.vnproject.json"));
            Assert.Null(session.GameFolder);
            Assert.Contains("게임 폴더가 없습니다", session.StatusMessage);

            RuntimeTarget.SaveBeside(session.ProjectPath!, RuntimeTarget.FromAbsolute(session.ProjectPath!, game));
            session.ReloadRuntimeTarget();

            Assert.Equal(Path.GetFullPath(game), session.GameFolder);

            // 다시 열어도 설정이 따라온다 — 파일이 프로젝트 폴더에 산다.
            var reopened = new AuthoringSession();
            reopened.Open(session.ProjectPath!);
            Assert.Equal(Path.GetFullPath(game), reopened.GameFolder);
            Assert.Contains("게임 폴더", reopened.StatusMessage);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
            Directory.Delete(game, recursive: true);
        }
    }

    [Fact]
    public void 저장은_정의_파일이_없으면_뼈대를_깔고_있는_파일은_건드리지_않는다()
    {
        string directory = TempDirectory();

        try
        {
            var session = new AuthoringSession();
            session.Save(Path.Combine(directory, "project.vnproject.json"));

            string definition = Path.Combine(directory, "game.definition.json");
            Assert.True(File.Exists(definition));

            File.WriteAllText(definition, """{"variables":[],"speakers":[]}""");
            session.Save();

            Assert.Equal("""{"variables":[],"speakers":[]}""", File.ReadAllText(definition));
            Assert.False(Directory.Exists(Path.Combine(directory, "assets"))); // 에셋 폴더는 compositor의 일이다
        }
        finally
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
