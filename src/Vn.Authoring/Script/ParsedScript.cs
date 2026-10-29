namespace Vn.Authoring.Script;

/// <param name="SourceLineNumber">입력 글에서의 1부터 시작하는 줄 번호.</param>
/// <param name="LineId">
/// 입력이 이미 알고 있는 줄의 신원(<c>#line:</c> 태그에서 온다).
/// <b>있으면 동기화가 위치·내용이 아니라 이것으로 짝을 찾는다</b> — 신원은 ID이지 위치가 아니다.
/// 신원을 모르는 입력은 null로 두고, 그때는 내용 기반 매칭이 그대로 돈다.
/// </param>
public sealed record ParsedScriptLine(
    int SourceLineNumber,
    string Speaker,
    string Text,
    string? LineId = null);

/// <summary>
/// 동기화(<see cref="ScriptSynchronizer"/>)에 넣을 줄 목록 — 카드 글 칸의 글을 읽은 것이다
/// (<c>ProjectEditor.ApplyScenarioText</c>).
///
/// ⛔ 작가의 평평한 대본 파일을 읽던 <c>ScriptParser</c>(<c>캐릭터이름: 대사</c>)와 그 문제 목록은
/// 대본 가져오기와 함께 걷혔다 (2026-10-29 · L5). 글은 이제 카드에서만 들어온다.
/// </summary>
public sealed class ParsedScript
{
    public ParsedScript(IReadOnlyList<ParsedScriptLine> lines, string contentHash)
    {
        Lines = lines;
        ContentHash = contentHash;
    }

    public IReadOnlyList<ParsedScriptLine> Lines { get; }

    /// <summary>원본 글의 해시. 같은 글을 다시 넣었는지 판별한다.</summary>
    public string ContentHash { get; }
}
