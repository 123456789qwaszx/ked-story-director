using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Flow;

/// <summary>
/// 대본의 줄 하나와 그 줄에 얹힌 대사 논리를 합쳐 놓은 <b>읽기 전용</b> 투영.
///
/// 저장되지 않는다. 화면·흐름 계산·미리보기가 모두 이 하나를 본다. 각자 대본과 확장 데이터를
/// 따로 합치면 어느 쪽이 맞는지 알 수 없는 순간이 생긴다.
/// </summary>
public sealed record DialogueLine(
    int Index,
    string LineId,
    int Revision,
    string Speaker,
    string Text,
    LineConditionTransition? Transition,
    IReadOnlyList<LineConditionTransition>? ExtraTransitions = null,

    /// <summary>「조건 분기」 표식이 가리키는 노드들 (R7 P-5) — 이 줄 앞에서 차례로 다녀온다.</summary>
    IReadOnlyList<string>? Detours = null)
{
    /// <summary>이 줄 앞에서 차례로 다녀올 노드들. 없으면 빈 목록이다.</summary>
    public IReadOnlyList<string> DetourTargetNodeIds => Detours ?? Array.Empty<string>();

    /// <summary>이 줄 앞의 전환들 — 순서가 곧 일어나는 순서다 (2026-08-17).</summary>
    public IReadOnlyList<LineConditionTransition> Transitions =>
        Transition is null
            ? Array.Empty<LineConditionTransition>()
            : [Transition, .. ExtraTransitions ?? Array.Empty<LineConditionTransition>()];
}

/// <summary>대본에 더 이상 없는 LineId에 남아 있는 대사 논리.</summary>
/// <param name="IsRetired">대본에서 사라진 줄인지. false면 대본 자체에 없는 Id다.</param>
public sealed record OrphanLineExtension(
    DialogueLineExtension Extension,
    bool IsRetired,
    LocalizedLine? LastKnownText);

/// <summary>
/// DialogueNode를 열었을 때 실제로 보이는 것.
/// </summary>
public sealed class DialogueScript
{
    public DialogueScript(
        string locale,
        IReadOnlyList<DialogueLine> lines,
        IReadOnlyList<OrphanLineExtension> orphans)
    {
        Locale = locale;
        Lines = lines;
        Orphans = orphans;
    }

    // ⛔ 읽겠다고 적어 둔 대본 Id(`ScriptId`)와 그것을 찾았는지(`IsResolved`)는 대본이 카드 안으로 들어오며
    //    걷혔다(S2) — 카드는 언제나 제 대본을 든다.

    public string Locale { get; }

    /// <summary>대본 순서 그대로. 은퇴한 줄은 들어 있지 않다.</summary>
    public IReadOnlyList<DialogueLine> Lines { get; }

    /// <summary>대본에서 사라진 줄에 남은 대사 논리. 자동으로 지우지 않는다.</summary>
    public IReadOnlyList<OrphanLineExtension> Orphans { get; }

    public DialogueLine? Find(string? lineId)
    {
        return lineId is null
            ? null
            : Lines.FirstOrDefault(line => string.Equals(line.LineId, lineId, StringComparison.Ordinal));
    }
}

/// <summary>
/// DialogueNode가 읽는 대본과 그 노드의 확장 데이터를 합친다.
///
/// <b>합성 방향은 언제나 대본 → 화면이다.</b> 화면이 본 것을 대본으로 되돌려 쓰지 않는다.
/// 대본에 없는 LineId의 확장 데이터는 버리지 않고 고아로 알린다.
/// </summary>
public static class DialogueScriptResolver
{
    public static DialogueScript Resolve(
        StoryProject project,
        DialogueNode node,
        string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(node);

        ScriptDocument document = node.Script;
        string targetLocale = locale ?? document.PrimaryLocale;
        var extensions = new Dictionary<string, DialogueLineExtension>(StringComparer.Ordinal);

        foreach (DialogueLineExtension extension in node.LineExtensions)
        {
            extensions.TryAdd(extension.LineId, extension);
        }

        var lines = new List<DialogueLine>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;

        foreach (ScriptLine scriptLine in document.ActiveLines)
        {
            LocalizedLine text = document.Text(scriptLine.Id, targetLocale);
            extensions.TryGetValue(scriptLine.Id, out DialogueLineExtension? extension);
            used.Add(scriptLine.Id);

            lines.Add(new DialogueLine(
                index++,
                scriptLine.Id,
                scriptLine.Revision,
                text.Speaker,
                text.Text,
                extension?.Transition,
                extension is { Transitions.Count: > 1 } ? extension.Transitions.Skip(1).ToList() : null,
                extension is { DetourTargetNodeIds.Count: > 0 } ? [.. extension.DetourTargetNodeIds] : null));
        }

        var orphans = new List<OrphanLineExtension>();

        foreach (DialogueLineExtension extension in node.LineExtensions)
        {
            if (used.Contains(extension.LineId))
            {
                continue;
            }

            ScriptLine? retired = document.FindLine(extension.LineId);
            orphans.Add(new OrphanLineExtension(
                extension,
                retired is not null,
                retired is null ? null : document.Text(extension.LineId, targetLocale)));
        }

        return new DialogueScript(targetLocale, lines, orphans);
    }
}
