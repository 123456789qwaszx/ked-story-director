using Vn.Authoring.Script;

namespace Vn.Authoring.Serialization;

/// <summary>
/// <b>형식 3의 따로 선 대본을 카드에 꽂아 주는 다리</b> (2026-10-29 · <c>docs/plans/script-merge.md</c>).
///
/// 형식 3 파일은 대본을 <c>script/*.vnscript.json</c>에 두고 카드는 Id로 가리킨다. 그런 판을 읽을 때 카드마다 이 표에서
/// 제 대본을 받는다 — 형식 4는 카드 안에 대본이 들어 있어 이 표가 필요 없다.
///
/// ⚠ <b>대본 하나를 두 카드가 가리키면</b>(대본을 나눠 쓰던 옛 프로젝트) 둘째부터는 같은 내용의 사본을 받는다 — 줄 신원
/// (LineId)은 그대로라 각 카드의 조건 · 출구가 그대로 붙는다. 어느 카드도 가리키지 않는 대본은 받아 가는 카드가 없으니
/// 버려진다.
/// </summary>
internal sealed class ScriptTable
{
    private readonly Dictionary<string, ScriptDocument> _byId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    public void Add(string id, ScriptDocument script)
    {
        if (!_byId.TryAdd(id, script))
        {
            throw new InvalidDataException($"대본 Id '{id}'가 중복됩니다.");
        }
    }

    /// <summary>그 카드가 가리키던 대본.</summary>
    public ScriptDocument For(string nodeId, string scriptId)
    {
        if (!_byId.TryGetValue(scriptId, out ScriptDocument? script))
        {
            throw new InvalidDataException($"DialogueNode '{nodeId}'가 없는 대본 '{scriptId}'를 가리킵니다.");
        }

        return _taken.Add(scriptId) ? script : ScriptDocumentJson.Copy(script);
    }
}
