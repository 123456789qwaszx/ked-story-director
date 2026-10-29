using Vn.App.Services;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// 챕터 판에 딸린 설정 노드 (2026-08-17 소유자 — 계층 분리).
///
/// ⛔ 세션이 챕터 스탯 키를 「A계층 어휘」로 들던 길(<c>SupplyChapterVocabulary</c>)은 2026-10-29에 걷혔다 — 작가 변수
/// 후보에서 스탯을 빼려던 것인데 작가 변수가 2026-09-17에 폐지돼 아무도 읽지 않았다(<c>legacy-cleanup.md</c> L2).
/// </summary>
public sealed class ChapterVocabularyTests
{
    [Fact]
    public void 챕터_판을_열면_설정_노드가_함께_선다()
    {
        // 2026-08-17 소유자 — 설정노드는 만들고 지우는 것이 아니라 챕터에 딸린 자리다.
        var session = new AuthoringSession();
        string fileId = session.EnsureChapterBoard("ch01");

        SetNode settings = Assert.Single(
            session.Project.FindFile(fileId)!.Nodes.OfType<SetNode>());
        Assert.Equal("ch01 설정", settings.Name);

        // 같은 챕터를 다시 열어도 하나뿐이다.
        session.EnsureChapterBoard("ch01");
        Assert.Single(session.Project.FindFile(fileId)!.Nodes.OfType<SetNode>());
    }
}
