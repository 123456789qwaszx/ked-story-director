using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Serialization;

/// <summary>
/// <b>따로 저장된 대본을 카드에 꽂아 주는 다리</b> (2026-10-29 · <c>docs/plans/script-merge.md</c> S2).
///
/// 카드가 제 대본을 직접 들지만(<see cref="DialogueNode.Script"/>) 형식 3 파일은 대본을 <c>script/*.vnscript.json</c>에 따로 두고
/// 카드는 Id로 가리킨다. 판을 읽을 때 카드마다 이 표에서 제 대본을 받는다.
///
/// ⚠ <b>대본 하나를 두 카드가 가리키면</b>(대본을 나눠 쓰던 옛 프로젝트) 둘째부터는 같은 내용의 사본을 새 Id로 받는다 —
/// 줄 신원(LineId)은 그대로라 각 카드의 조건 · 출구가 그대로 붙는다. 어느 카드도 가리키지 않는 대본은 받아 가는 카드가
/// 없으니 버려진다(S1).
/// </summary>
internal sealed class ScriptTable
{
    private readonly Dictionary<string, ScriptDocument> _byId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    public ScriptTable(IEnumerable<ScriptDocument> scripts)
    {
        foreach (ScriptDocument script in scripts)
        {
            if (!_byId.TryAdd(script.Id, script))
            {
                throw new InvalidDataException($"대본 Id '{script.Id}'가 중복됩니다.");
            }
        }
    }

    /// <summary>
    /// 그 카드가 들 대본. 카드가 대본을 가리키지 않았으면(대본 없이 저장된 옛 카드) 빈 대본을 새로 세운다.
    /// </summary>
    public ScriptDocument For(string nodeId, string nodeName, string? scriptId)
    {
        if (scriptId is null)
        {
            var fresh = new ScriptDocument(name: $"{nodeName} 대본");
            fresh.RequireLocale(fresh.PrimaryLocale);
            return fresh;
        }

        if (!_byId.TryGetValue(scriptId, out ScriptDocument? script))
        {
            throw new InvalidDataException($"DialogueNode '{nodeId}'가 없는 대본 '{scriptId}'를 가리킵니다.");
        }

        return _taken.Add(scriptId) ? script : CopyWithNewId(script);
    }

    private static ScriptDocument CopyWithNewId(ScriptDocument script)
    {
        JsonObject copy = ScriptDocumentJson.WriteObject(script);
        copy["scriptId"] = Identifier.Script();
        return ScriptDocumentJson.ReadObject(copy);
    }
}
