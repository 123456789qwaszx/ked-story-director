using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Text;

namespace Vn.Authoring.Serialization;

internal static class JsonSupport
{
    internal static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonObject ParseObject(string json, string kind)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidDataException($"{kind} 파일의 루트가 JSON 객체가 아닙니다.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{kind} 파일을 읽을 수 없습니다. {exception.Message}", exception);
        }
    }

    // ── 엄격한 읽기 (2026-11-18 · old-shapes O8) ─────────────────────────────────
    // 툴이 늘 쓰는 칸이 빠졌거나 모양이 다르면 받지 않는다 — 대신 값을 채우지도, 모르는 항목을 건너뛰지도 않는다. 기본값이라 쓰지 않는
    // 칸만 없어도 된다(Optional…). ⛔ 그 전에는 빠진 이름 · 자리 · 종류에 대신 값을 넣고 객체 아닌 항목은 건너뛰었다.

    /// <summary>문자열 칸 — 빈 글자도 받는다(툴이 빈 채로 쓰는 칸).</summary>
    internal static string RequiredString(JsonNode? node, string what) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? (string)value!
            : throw new InvalidDataException($"{what}{Josa.이가(what)} 없거나 문자열이 아닙니다.");

    /// <summary>비지 않은 문자열 칸 — Id · 이름 · 열쇠.</summary>
    internal static string RequiredText(JsonNode? node, string what) =>
        RequiredString(node, what) is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidDataException($"{what}{Josa.이가(what)} 비었습니다.");

    /// <summary>없어도 되는 문자열 칸 — 있으면 문자열이어야 한다.</summary>
    internal static string? OptionalString(JsonNode? node, string what) => node is null ? null : RequiredString(node, what);

    internal static double RequiredNumber(JsonNode? node, string what) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && double.IsFinite((double)value)
            ? (double)value
            : throw new InvalidDataException($"{what}{Josa.이가(what)} 없거나 수가 아닙니다.");

    internal static double? OptionalNumber(JsonNode? node, string what) => node is null ? null : RequiredNumber(node, what);

    internal static int RequiredInt(JsonNode? node, string what) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue(out int number)
            ? number
            : throw new InvalidDataException($"{what}{Josa.이가(what)} 없거나 정수가 아닙니다.");

    /// <summary>켤 때만 쓰는 깃발 — 없으면 꺼짐, 있으면 <c>true</c>여야 한다(툴은 <c>false</c>를 쓰지 않는다).</summary>
    internal static bool Flag(JsonNode? node, string what) =>
        node is null
            ? false
            : node is JsonValue value && value.GetValueKind() == JsonValueKind.True
                ? true
                : throw new InvalidDataException($"{what}{Josa.은는(what)} 켤 때만 true로 적습니다.");

    internal static JsonObject RequiredObject(JsonNode? node, string what) =>
        node as JsonObject ?? throw new InvalidDataException($"{what}{Josa.이가(what)} 없거나 객체가 아닙니다.");

    /// <summary>객체 배열 — 없으면 빈 것(비면 쓰지 않는 칸), 배열이 아니거나 객체 아닌 항목이 있으면 받지 않는다.</summary>
    internal static IEnumerable<JsonObject> Objects(JsonNode? node, string what)
    {
        if (node is null)
        {
            return [];
        }

        if (node is not JsonArray array)
        {
            throw new InvalidDataException($"{what}{Josa.이가(what)} 배열이 아닙니다.");
        }

        return array.Select(item => item as JsonObject ?? throw new InvalidDataException($"{what}에 객체가 아닌 항목이 있습니다.")).ToList();
    }

    /// <summary>늘 쓰는 객체 배열 — 없으면 받지 않는다.</summary>
    internal static IEnumerable<JsonObject> RequiredObjects(JsonNode? node, string what) =>
        node is null ? throw new InvalidDataException($"{what}{Josa.이가(what)} 없습니다.") : Objects(node, what);

    public static string ToDeterministicText(JsonObject root)
    {
        return root.ToJsonString(WriteOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static void WriteAtomic(string path, string text)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = fullPath + ".tmp";

        try
        {
            File.WriteAllText(temporary, EnsureTrailingLf(text), new UTF8Encoding(false));
            File.Move(temporary, fullPath, overwrite: true);
        }
        catch
        {
            // 교체에 실패한 임시 파일이 다음 저장을 방해하지 않게 정리한다.
            try
            {
                File.Delete(temporary);
            }
            catch
            {
                // 원래 저장 예외를 보존한다.
            }

            throw;
        }
    }

    public static string EnsureTrailingLf(string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }

    public static void ValidateProject(StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        HashSet<string> speakerNames = new(StringComparer.Ordinal);

        foreach (Speaker speaker in project.Speakers)
        {
            if (string.IsNullOrWhiteSpace(speaker.Name) || !speakerNames.Add(speaker.Name))
            {
                throw new InvalidDataException($"화자 이름 '{speaker.Name}'이 비었거나 겹칩니다.");
            }
        }

        HashSet<string> fileIds = new(StringComparer.Ordinal);
        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> chapterIds = project.Chapters.Select(chapter => chapter.ChapterId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> boardNames = new(StringComparer.Ordinal);

        foreach (StoryFile file in project.Files)
        {
            if (!fileIds.Add(file.Id))
            {
                throw new InvalidDataException($"StoryFile Id '{file.Id}'가 중복됩니다.");
            }

            // 판은 챕터의 판이다 — 판은 챕터와 함께 서고 바뀌고 지워진다(2026-11-18 · old-shapes O3). 챕터가 아닌 판은 옛 데이터라 받지 않는다.
            if (!chapterIds.Contains(file.Name) || !boardNames.Add(file.Name))
            {
                throw new InvalidDataException($"판 '{file.Name}'{Josa.은는(file.Name)} 챕터의 판이 아니거나 같은 챕터의 판이 둘입니다.");
            }

            string relativePath = ProjectStore.NormalizeRelativeStoryPath(file.RelativePath);

            if (!paths.Add(relativePath))
            {
                throw new InvalidDataException($"StoryFile 경로 '{relativePath}'가 중복됩니다.");
            }

            // 설정 노드는 그 판의 챕터 조건 공급 노드 하나뿐이다 — 공급만 세운다(2026-11-18 · old-shapes O6). 옛 「X 설정」 노드는 받지 않는다.
            if (file.Nodes.OfType<SetNode>().Count(node =>
                    string.Equals(node.Name, Chapters.ChapterBoardSupply.ConditionSupplyNodeName(file.Name), StringComparison.Ordinal)) !=
                file.Nodes.OfType<SetNode>().Count() || file.Nodes.OfType<SetNode>().Count() > 1)
            {
                throw new InvalidDataException($"판 '{file.Name}'에 챕터 조건 공급 노드가 아닌 설정 노드가 있거나 공급 노드가 둘입니다.");
            }

            foreach (StoryNode node in file.Nodes)
            {
                if (!nodeIds.Add(node.Id))
                {
                    throw new InvalidDataException($"노드 Id '{node.Id}'가 프로젝트 전체에서 중복됩니다.");
                }

                if (node is DialogueNode dialogue)
                {
                    ValidateDialogueNode(project, dialogue);
                }
            }
        }

        ValidateEpisodeCards(project);

        if (project.StartNodeId is not null && project.FindNode(project.StartNodeId) is null)
        {
            throw new InvalidDataException(
                $"시작 노드 '{project.StartNodeId}'를 프로젝트에서 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 에피소드와 카드는 한 칸에 서고 함께 지워진다(2026-11-18 · old-shapes O4) — 그래서 에피소드마다 그 챕터의 판에 카드가 <b>하나</b>,
    /// 에피소드 카드는 그 챕터의 에피소드를 표식(<see cref="DialogueNode.MarkedEpisodeId"/>)으로 가리키고 이름도 그 EpisodeId다(O5 — Yarn 노드
    /// 이름은 EpisodeId에서 짓는다), 분기 카드는 표식이 없다. 에피소드는 늘 장면에 속한다(O7 — 장면ID가 비지 않는다).
    /// 그렇지 않은 프로젝트(옛 데이터 · 손으로 고친 파일)는 받지 않는다.
    /// </summary>
    private static void ValidateEpisodeCards(StoryProject project)
    {
        foreach (Chapters.ChapterDocument chapter in project.Chapters)
        {
            if (chapter.Episodes.FirstOrDefault(episode => string.IsNullOrWhiteSpace(episode.SceneId)) is { } sceneless)
            {
                throw new InvalidDataException($"챕터 '{chapter.ChapterId}'의 에피소드 '{sceneless.EpisodeId}'에 장면ID가 없습니다.");
            }

            HashSet<string> episodes = chapter.Episodes.Select(episode => episode.EpisodeId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> marked = new(StringComparer.Ordinal);
            StoryFile? board = project.Files.FirstOrDefault(file => string.Equals(file.Name, chapter.ChapterId, StringComparison.Ordinal));

            foreach (DialogueNode card in board?.Nodes.OfType<DialogueNode>() ?? [])
            {
                bool fits = card.IsBranchCard
                    ? card.MarkedEpisodeId is null
                    : card.MarkedEpisodeId is { } mark && string.Equals(card.Name, mark, StringComparison.Ordinal) &&
                      episodes.Contains(mark) && marked.Add(mark);

                if (!fits)
                {
                    throw new InvalidDataException(
                        $"판 '{chapter.ChapterId}'의 카드 '{card.Name}'{Josa.이가(card.Name)} 그 챕터의 에피소드 하나를 가리키지 않습니다(표식이 없거나 · 없는 에피소드거나 · 겹치거나 · 이름이 EpisodeId와 다릅니다).");
                }
            }

            if (episodes.FirstOrDefault(episode => !marked.Contains(episode)) is { } lonely)
            {
                throw new InvalidDataException($"챕터 '{chapter.ChapterId}'의 에피소드 '{lonely}'에 카드가 없습니다.");
            }
        }
    }

    private static void ValidateScript(DialogueNode node)
    {
        ScriptDocument script = node.Script;
        HashSet<string> lineIds = new(StringComparer.Ordinal);

        foreach (ScriptLine line in script.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Id))
            {
                throw new InvalidDataException($"'{node.Id}'의 대본에 빈 LineId가 있습니다.");
            }

            if (!lineIds.Add(line.Id))
            {
                throw new InvalidDataException($"'{node.Id}'의 대본에서 LineId '{line.Id}'가 중복됩니다.");
            }
        }

        HashSet<string> locales = new(StringComparer.Ordinal);

        foreach (ScriptLocale locale in script.Locales)
        {
            if (!locales.Add(locale.Locale))
            {
                throw new InvalidDataException(
                    $"'{node.Id}'의 대본에서 locale '{locale.Locale}'이 중복됩니다.");
            }

            foreach (string lineId in locale.Entries.Keys)
            {
                if (!lineIds.Contains(lineId))
                {
                    throw new InvalidDataException(
                        $"'{node.Id}'의 대본의 locale '{locale.Locale}'이 없는 LineId '{lineId}'를 가리킵니다.");
                }
            }
        }
    }

    /// <summary>
    /// 카드의 대본이 성립하는지, 같은 LineId에 확장이 두 벌 붙지 않았는지 본다. (카드는 제 대본을 든다 — 가리키는 대본이 있는지 보던 검사는
    /// S2에 걷혔다.)
    /// </summary>
    private static void ValidateDialogueNode(
        StoryProject project,
        DialogueNode node)
    {
        ValidateScript(node);

        HashSet<string> lineIds = new(StringComparer.Ordinal);

        foreach (DialogueLineExtension extension in node.LineExtensions)
        {
            if (!lineIds.Add(extension.LineId))
            {
                throw new InvalidDataException(
                    $"DialogueNode '{node.Id}'에서 LineId '{extension.LineId}' 항목이 중복됩니다.");
            }
        }

        foreach (string openLineId in node.BranchExits.Keys)
        {
            // 줄 없는 갈래(대본 끝의 빈 블록)는 줄 항목이 있을 수 없다 — 그 자리에 사는
            // 것은 `TrailingTransitions`다 (2026-08-24, `Flow.BranchAnchor`).
            if (Flow.BranchAnchor.IsTrailing(openLineId))
            {
                if (!node.TrailingTransitions.Any(transition => transition.OpensBranch))
                {
                    throw new InvalidDataException(
                        $"DialogueNode '{node.Id}'의 조건 출구가 없는 꼬리 갈래 '{openLineId}'에 매달려 있습니다.");
                }

                continue;
            }

            // 한 줄이 갈래를 여럿 열면 신원에 `#n`이 붙는다 — 뿌리로 되돌려 본다.
            string root = openLineId.Split(Flow.BranchAnchor.Separator)[0];

            if (!lineIds.Contains(root))
            {
                throw new InvalidDataException(
                    $"DialogueNode '{node.Id}'의 조건 출구가 항목 없는 LineId '{openLineId}'에 매달려 있습니다.");
            }
        }

        foreach (string target in node.BranchExits.Values)
        {
            if (project.FindNode(target) is null)
            {
                throw new InvalidDataException(
                    $"DialogueNode '{node.Id}'의 조건 출구가 없는 노드 '{target}'를 가리킵니다.");
            }
        }

        // 분기는 같은 판의 분기 카드에만 잇는다 — 잇는 길이 모두 그것만 받는다(2026-11-18 · old-shapes O2 · 판은 O8). 다른 것을 가리키면 받지 않는다.
        StoryFile? home = project.FindFileContainingNode(node.Id);

        foreach (string target in node.LineExtensions.SelectMany(extension => extension.DetourTargetNodeIds))
        {
            if (project.FindNode(target) is not DialogueNode { IsBranchCard: true } ||
                !ReferenceEquals(project.FindFileContainingNode(target), home))
            {
                throw new InvalidDataException(
                    $"DialogueNode '{node.Id}'의 분기가 같은 판의 분기 카드가 아닌 '{target}'를 가리킵니다 — 분기는 같은 판의 분기 카드에만 잇습니다.");
            }
        }
    }
}
