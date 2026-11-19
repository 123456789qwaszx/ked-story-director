using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

/// <summary>
/// 시험의 판 — 챕터 하나와 그 판. 카드는 챕터 판에만 선다(2026-11-17 — 낙서판을 걷었다, <c>docs/work-orders/scene-required-orders.md</c>).
/// </summary>
internal static class TestChapter
{
    internal static StoryFile Board(ProjectEditor editor, string chapterId = "ch01")
    {
        editor.EnsureChapter(chapterId);
        return editor.Project.FindFile(editor.EnsureChapterBoard(chapterId))!;
    }
}
