using Vn.App.Services;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// 시험의 판 — 지금 판이 챕터 판이면 그것, 아니면 챕터 하나(<c>ch01</c>)와 그 판을 세워 고른다. 카드는 챕터 판에만 선다
/// (2026-11-17 — 낙서판을 걷었다, <c>docs/work-orders/scene-required-orders.md</c>).
/// </summary>
internal static class TestChapter
{
    internal static StoryFile Board(AuthoringSession session, string chapterId = "ch01")
    {
        if (session.ActiveFile is { } active &&
            Vn.Authoring.Chapters.EpisodeNaming.ChapterOfBoard(session.Project, active) is not null)
        {
            return active;
        }

        session.Editor.EnsureChapter(chapterId);
        StoryFile board = session.Project.FindFile(session.Editor.EnsureChapterBoard(chapterId))!;
        session.SelectFile(board.Id);
        return board;
    }
}
