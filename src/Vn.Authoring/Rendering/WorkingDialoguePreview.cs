using Vn.Authoring.Definition;
using Vn.Authoring.Model;
using Vn.Authoring.Results;

namespace Vn.Authoring.Rendering;

/// <summary>
/// DialogueNode의 작업 상태를 그대로 펼쳐 보여 준다 — 카드의 글 칸이 여기를 지난다.
/// (⛔ 곁기둥의 Script Preview는 2026-10-29에 걷혔다.)
///
/// 초안(<see cref="DialoguePublisher.Draft"/>)을 결과 모양으로 감싸 합성기에 넘기고, 초안의 문제는
/// 진단 조각으로 끼운다.
/// </summary>
public static class WorkingDialoguePreview
{
    public static RenderedDocument Compose(
        StoryProject project,
        string dialogueNodeId,
        GameDefinition? definition = null,
        DocumentOutputOptions? options = null,
        OutputPresetId? presetId = null,
        ILocalizedLineProvider? localization = null,
        string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        DialogueDraft draft = DialoguePublisher.Draft(project, dialogueNodeId, definition, locale);
        RenderedDocument document = ResultDocumentComposer.Compose(
            DialoguePublisher.AsResult(draft),
            project,
            options,
            presetId,
            localization);

        options ??= OutputPresetCatalog.RuntimeFull.Options;

        if (!options.IncludeDiagnostics || draft.Problems.Count == 0)
        {
            return document;
        }

        return new RenderedDocument(
            document.SourceNodeId,
            InsertProblems(document, draft),
            document.Options,
            document.PresetId);
    }

    /// <summary>
    /// <b>대사 노드 카드의 글 칸에 보이는 글</b> (D4 · 2026-10-29) — 보는 면이 아니라 <b>쓰는 면</b>이다.
    /// 넣을 때 <see cref="Editing.ProjectEditor.ApplyScenarioText"/>가 같은 문법으로 되읽으므로(ScenarioOnly 왕복, X12a)
    /// 그 프리셋의 표기를 그대로 쓰되 <b>진단은 뺀다</b> — <c>[주의]</c> 줄은 파서가 해석 못 한 줄로 올려,
    /// 넣을 때마다 제가 낸 글이 문제로 되돌아온다(머리글을 뺀 2026-09-18과 같은 이유).
    /// 끝의 줄바꿈은 걷는다 — 빈 줄 하나뿐인 새 카드는 빈 칸으로 보인다.
    ///
    /// ⭐ <b>「조건 분기」 자리는 빈 줄이다</b>(<see cref="ScenarioWriting"/>) — 되읽을 때 빈 줄은 건너뛰므로 대본은 그대로다.
    /// </summary>
    public static string ScenarioText(StoryProject project, string dialogueNodeId, GameDefinition? definition = null) =>
        ScenarioWriting(project, dialogueNodeId, definition).Text;

    /// <summary>
    /// <see cref="ScenarioText"/>과 그 줄 짝 — 연출 그래프 카드가 분기 점을 글 칸에 세운다.
    ///
    /// <b>분기 자리는 빈 줄이다</b> (2026-10-30 소유자: *"분기가 생성 되는 점 위치를, 그 생성한 텍스트 라인 위쪽에
    /// 위치시키고, 조건 분기가 있다면, 그 분기의 공간만큼 텍스트 줄 간격을 늘려"* · *"같은 자리에 여러 분기가 있을 때, 점이
    /// 겹쳐지지 않게"*). 분기가 걸린 대사 줄 바로 위에 분기 수만큼 빈 줄이 끼고, 분기 점은 저마다 제 빈 줄에 선다 — 다녀오는
    /// 차례로 위에서 아래로. 되읽을 때(<see cref="Editing.ProjectEditor.ApplyScenarioText"/>) 빈 줄은 건너뛰므로 이 줄들은
    /// 대본을 바꾸지 않는다.
    /// </summary>
    public static ScenarioWritingLayout ScenarioWriting(
        StoryProject project, string dialogueNodeId, GameDefinition? definition = null)
    {
        DocumentOutputOptions scenario = OutputPresetCatalog.ScenarioOnly.Options;
        var writing = new DocumentOutputOptions(
            scenario.Format,
            scenario.IncludeStructure,
            scenario.IncludeConditions,
            scenario.IncludeDialogueText,
            scenario.IncludeLocalizedDialogue,
            scenario.IncludeSpeaker,
            scenario.IncludeLineId,
            scenario.IncludeExecutionJumps,
            includeDiagnostics: false);

        RenderedDocument document = Compose(
            project, dialogueNodeId, definition, writing, OutputPresetId.ScenarioOnly);

        (string text, IReadOnlyDictionary<string, int> rowOfLine) = DocumentPreviewFormatter.FormatWithRows(document);
        IReadOnlyList<DialogueLineExtension> extensions =
            project.FindDialogue(dialogueNodeId)?.LineExtensions ?? (IReadOnlyList<DialogueLineExtension>)[];

        return WithBranchRows(text, rowOfLine, extensions);
    }

    /// <summary>분기가 걸린 대사 줄 위에 분기 수만큼 빈 줄을 끼우고, 줄 짝을 그만큼 민다.</summary>
    private static ScenarioWritingLayout WithBranchRows(
        string text, IReadOnlyDictionary<string, int> rowOfLine, IReadOnlyList<DialogueLineExtension> extensions)
    {
        // 그 글 줄 앞에 낄 분기들 — 글 줄 번호로.
        var branchesBefore = new Dictionary<int, (string LineId, IReadOnlyList<string> Targets)>();

        foreach (DialogueLineExtension extension in extensions)
        {
            if (extension.DetourTargetNodeIds.Count > 0 && rowOfLine.TryGetValue(extension.LineId, out int at))
            {
                branchesBefore[at] = (extension.LineId, extension.DetourTargetNodeIds);
            }
        }

        string[] rows = text.Split('\n');
        var shown = new List<string>(rows.Length);
        var shiftedRowOf = new Dictionary<int, int>();
        var rowOfBranch = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int row = 0; row < rows.Length; row++)
        {
            if (branchesBefore.TryGetValue(row, out (string LineId, IReadOnlyList<string> Targets) branch))
            {
                foreach (string target in branch.Targets)
                {
                    rowOfBranch[Flow.ExitPort.DetourKey(branch.LineId, target)] = shown.Count;
                    shown.Add(string.Empty);
                }
            }

            shiftedRowOf[row] = shown.Count;
            shown.Add(rows[row]);
        }

        var lines = rowOfLine.ToDictionary(pair => pair.Key, pair => shiftedRowOf[pair.Value], StringComparer.Ordinal);

        return new ScenarioWritingLayout(string.Join('\n', shown).TrimEnd('\n'), lines, rowOfBranch);
    }

    private static List<RenderedSegment> InsertProblems(RenderedDocument document, DialogueDraft draft)
    {
        var segments = new List<RenderedSegment>(document.Segments);
        var warnings = new List<RenderedSegment>();

        for (int index = 0; index < draft.Problems.Count; index++)
        {
            PublishProblem problem = draft.Problems[index];

            warnings.Add(new RenderedSegment(
                Id: $"warning:{draft.SourceNodeId}:{index}",
                Kind: RenderedSegmentKind.Warning,
                Layer: DocumentLayer.Diagnostics,
                Source: new RenderSourceReference(
                    NodeId: draft.SourceNodeId,
                    LineId: problem.LineId),
                Text: problem.Message));
        }

        // 헤더가 있으면 그 바로 뒤에 둔다. 문서를 위에서 읽을 때 먼저 눈에 띄어야 한다.
        int header = segments.FindIndex(
            segment => segment.Kind == RenderedSegmentKind.NodeHeader);
        segments.InsertRange(header + 1, warnings);
        return segments;
    }
}

/// <summary>카드 글 칸의 글과 그 줄 짝 (<see cref="WorkingDialoguePreview.ScenarioWriting"/>).</summary>
/// <param name="Text">글 칸에 보이는 글 — 분기 자리는 빈 줄이다.</param>
/// <param name="RowOfLine">대본의 줄(LineId)마다 그 줄이 글의 몇 번째 줄인가(0부터). 대사 줄과 선택지 줄만 든다.</param>
/// <param name="RowOfBranch">분기마다(<see cref="Flow.ExitPort.DetourKey"/>) 그 분기 자리 빈 줄이 몇 번째 줄인가.</param>
public sealed record ScenarioWritingLayout(
    string Text,
    IReadOnlyDictionary<string, int> RowOfLine,
    IReadOnlyDictionary<string, int> RowOfBranch);
