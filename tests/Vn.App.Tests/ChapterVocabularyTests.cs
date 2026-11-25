using Vn.App.Services;
using Vn.Authoring.Flow;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// 챕터 판의 설정 노드 (계층 분리).
///
/// 판마다 「X 설정」 노드를 세우지 않는다 — 그 안을 채울 편집기가 없어 눌러도 빈 칸이 된다. 챕터 조건 공급 노드도
/// 2026-11-25에 걷혔다(condition-supply C1) — 판에는 카드만 선다.
/// </summary>
public sealed class ChapterVocabularyTests
{
    [Fact]
    public void 챕터_판을_열어도_설정_노드는_서지_않는다()
    {
        var session = new AuthoringSession();
        session.Editor.EnsureChapter("ch01");   // 판은 챕터가 세운다
        string fileId = session.EnsureChapterBoard("ch01");
        session.EnsureChapterBoard("ch01");

        Assert.Empty(session.Project.FindFile(fileId)!.Nodes);
    }

}
