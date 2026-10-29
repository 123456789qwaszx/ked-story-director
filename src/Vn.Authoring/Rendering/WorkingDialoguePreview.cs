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
    /// </summary>
    public static string ScenarioText(StoryProject project, string dialogueNodeId, GameDefinition? definition = null)
    {
        DocumentOutputOptions scenario = OutputPresetCatalog.ScenarioOnly.Options;
        var writing = new DocumentOutputOptions(
            scenario.Format,
            scenario.IncludeStructure,
            scenario.IncludeSetAssignments,
            scenario.IncludeConditions,
            scenario.IncludeDialogueText,
            scenario.IncludeLocalizedDialogue,
            scenario.IncludeSpeaker,
            scenario.IncludeLineId,
            scenario.IncludeExecutionJumps,
            includeDiagnostics: false);

        RenderedDocument document = Compose(
            project, dialogueNodeId, definition, writing, OutputPresetId.ScenarioOnly);

        return DocumentPreviewFormatter.Format(document).TrimEnd('\n');
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
