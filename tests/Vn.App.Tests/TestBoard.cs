using Vn.App.Services;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// 편집 기계(글 칸 · 분기점 · 끌기 · 선)를 시험하는 <b>그릇 판</b> — 챕터 판이 아니라 카드가 에피소드가 되지 않는다. 화면에는 이런 판이 없다
/// (2026-11-17 — 새 프로젝트의 「기본 파일」 낙서판을 걷었다, <c>docs/work-orders/scene-required-orders.md</c> E4). 지금 판이 있으면 그것을 쓴다.
/// ⚠ 저수준 정리(편집 명령이 챕터 판만 받게)에서 이 시험들을 챕터 판으로 옮기고 이 손잡이를 걷는다.
/// </summary>
internal static class TestBoard
{
    internal static StoryFile Of(AuthoringSession session)
    {
        if (session.ActiveFile is { } active)
        {
            return active;
        }

        StoryFile board = session.Editor.AddStoryFile("판");
        session.SelectFile(board.Id);
        return board;
    }
}
