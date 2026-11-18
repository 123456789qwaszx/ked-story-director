using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

public class StoryFileTests
{
    [Fact]
    public void 프로젝트_전체_노드_순서는_파일_순서와_파일_내_순서를_따른다()
    {
        var project = new StoryProject();
        var first = new StoryFile("sf_a", "A");
        var second = new StoryFile("sf_b", "B");
        project.Files.Add(first);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "A" });   // 카드는 챕터 판에만 선다
        project.Files.Add(second);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "B" });   // 카드는 챕터 판에만 선다
        var editor = new ProjectEditor(project);

        DialogueNode a1 = editor.AddDialogueNode(first.Id, "A1", name: "A1");
        DialogueNode a2 = editor.AddDialogueNode(first.Id, "A2", name: "A2");
        DialogueNode b1 = editor.AddDialogueNode(second.Id, "B1", name: "B1");

        Assert.Equal(
            new[] { a1.Id, a2.Id, b1.Id },
            project.EnumerateNodes().Select(node => node.Id));
    }

    [Fact]
    public void 새_노드는_지정한_파일의_마지막에만_추가된다()
    {
        var project = new StoryProject();
        var first = new StoryFile("sf_a", "A");
        var second = new StoryFile("sf_b", "B");
        project.Files.Add(first);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "A" });   // 카드는 챕터 판에만 선다
        project.Files.Add(second);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "B" });   // 카드는 챕터 판에만 선다
        var editor = new ProjectEditor(project);

        DialogueNode existing = editor.AddDialogueNode(first.Id, "기존", name: "기존");
        DialogueNode added = editor.AddDialogueNode(second.Id, "새 노드", name: "새 노드");

        Assert.Equal(new[] { existing.Id }, first.Nodes.Select(node => node.Id));
        Assert.Equal(new[] { added.Id }, second.Nodes.Select(node => node.Id));
        Assert.Same(added, second.Nodes[^1]);
    }

    [Fact]
    public void 노드_삭제는_소유_파일에서_제거한다()
    {
        var project = new StoryProject();
        var first = new StoryFile("sf_a", "A");
        var second = new StoryFile("sf_b", "B");
        project.Files.Add(first);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "A" });   // 카드는 챕터 판에만 선다
        project.Files.Add(second);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "B" });   // 카드는 챕터 판에만 선다
        var editor = new ProjectEditor(project);

        DialogueNode source = editor.AddDialogueNode(first.Id, "출발", name: "출발");
        DialogueNode target = editor.AddDialogueNode(second.Id, "도착", name: "도착");

        global::Vn.Authoring.Chapters.EpisodeDeleter.Delete(editor, "B", "도착");   // 에피소드 카드는 에피소드와 함께 지운다

        Assert.Empty(second.Nodes);
        Assert.Single(first.Nodes, node => node.Id == source.Id);
        Assert.Null(project.FindNode(target.Id));
    }

    [Fact]
    public void 프로젝트_전체에서_중복된_노드_Id는_추가할_수_없다()
    {
        var project = new StoryProject();
        var first = new StoryFile("sf_a", "A");
        var second = new StoryFile("sf_b", "B");
        project.Files.Add(first);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "A" });   // 카드는 챕터 판에만 선다
        project.Files.Add(second);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "B" });   // 카드는 챕터 판에만 선다
        var editor = new ProjectEditor(project);

        editor.AddNode(first.Id, new DialogueNode("nd_same", "첫 노드"));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => editor.AddNode(second.Id, new SetNode("nd_same", "중복 노드")));

        Assert.Contains("중복", error.Message, StringComparison.Ordinal);
        Assert.Empty(second.Nodes);
    }
}
