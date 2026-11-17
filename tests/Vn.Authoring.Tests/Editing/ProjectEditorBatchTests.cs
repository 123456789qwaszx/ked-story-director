using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Editing;

/// <summary>
/// <b>여러 명령을 되돌리기 한 칸 · 알림 한 번으로 묶는다</b> (2026-10-29 · D2).
///
/// 엑셀 [대본 가져오기]가 걷히며 챕터 조건의 공급이 <b>조건을 고치는 명령 안</b>으로 옮겨 왔다.
/// 공급은 남의 명령(설정노드·조건 만들기)을 부르므로, 묶음이 없으면 조건 하나를 고쳤는데
/// 되돌리기가 여러 번이어야 한다.
/// </summary>
public sealed class ProjectEditorBatchTests
{
    [Fact]
    public void 묶음은_되돌리기_한_칸이다()
    {
        var editor = new ProjectEditor(new StoryProject());
        string before = ProjectSnapshotCodec.Encode(editor.Project);

        editor.Batch(() =>
        {
            editor.EnsureChapter("하나");
            editor.EnsureChapter("둘");
        });

        Assert.Equal(2, editor.Project.Chapters.Count(chapter => chapter.ChapterId is "하나" or "둘"));

        editor.Undo();

        Assert.Equal(before, ProjectSnapshotCodec.Encode(editor.Project));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void 묶음은_알림이_한_번이다()
    {
        var editor = new ProjectEditor(new StoryProject());
        int raised = 0;
        long revision = editor.Revision;
        editor.Changed += (_, _) => raised++;

        editor.Batch(() =>
        {
            editor.EnsureChapter("하나");
            editor.EnsureChapter("둘");
            editor.EnsureChapter("셋");
        });

        Assert.Equal(1, raised);
        Assert.Equal(revision + 1, editor.Revision);
    }

    [Fact]
    public void 겹친_묶음은_바깥_하나로_센다()
    {
        var editor = new ProjectEditor(new StoryProject());
        int raised = 0;
        editor.Changed += (_, _) => raised++;

        editor.Batch(() =>
        {
            editor.EnsureChapter("하나");
            editor.Batch(() => { editor.EnsureChapter("둘"); });
        });

        Assert.Equal(1, raised);

        editor.Undo();

        Assert.DoesNotContain(editor.Project.Chapters, chapter => chapter.ChapterId is "하나" or "둘");
    }

    [Fact]
    public void 아무것도_안_바뀐_묶음은_칸도_알림도_없다()
    {
        var editor = new ProjectEditor(new StoryProject());
        int raised = 0;
        editor.Changed += (_, _) => raised++;

        editor.Batch(() => { });

        Assert.Equal(0, raised);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void 묶음_안에서_실패하면_묶음_전으로_돌아간다()
    {
        // 반쯤 된 묶음이 남으면 되돌리기 한 칸이 무엇을 되돌리는지 아무도 모른다.
        var editor = new ProjectEditor(new StoryProject());
        string before = ProjectSnapshotCodec.Encode(editor.Project);

        Assert.Throws<InvalidOperationException>(() => editor.Batch(() =>
        {
            editor.EnsureChapter("하나");
            throw new InvalidOperationException("일부러");
        }));

        Assert.Equal(before, ProjectSnapshotCodec.Encode(editor.Project));
        Assert.False(editor.CanUndo);
    }
}
