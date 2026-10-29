using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Results;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// 출력 프리셋은 같은 StoryProject를 서로 다른 읽기 문서로 투영할 뿐,
/// 공식 원본과 Snapshot을 수정하지 않아야 한다.
/// </summary>
public class OutputPresetTests
{
    [Fact]
    public void 프리셋_합성은_StoryProject와_Snapshot을_수정하지_않는다()
    {
        PresetSample sample = BuildPresetSample();
        string before = ProjectSnapshotCodec.Encode(sample.Sample.Project);

        foreach (OutputPreset preset in new[] { OutputPresetCatalog.RuntimeFull, OutputPresetCatalog.ScenarioOnly })
        {
            _ = Compose(sample, preset);
        }

        string after = ProjectSnapshotCodec.Encode(sample.Sample.Project);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Runtime_Full은_모든_저작_레이어를_포함한다()
    {
        PresetSample sample = BuildPresetSample();

        RenderedDocument document = Compose(sample, OutputPresetCatalog.RuntimeFull);

        Assert.Contains(document.Segments, segment => segment.Kind == RenderedSegmentKind.NodeHeader);
        // ⛔ SetAssignment 층은 2026-09-17에 비었고(작가 변수 폐지) 2026-10-29(L5)에 종류째 걷혔다.
        Assert.Contains(document.Segments, segment => segment.Kind == RenderedSegmentKind.ConditionBegin);
        Assert.Contains(document.Segments, segment => segment.Kind == RenderedSegmentKind.DialogueLine);
        Assert.Contains(document.Segments, segment => segment.Kind == RenderedSegmentKind.BranchDetour);
        Assert.Contains(document.Segments, segment => segment.Kind == RenderedSegmentKind.DefaultJump);
        Assert.Equal(OutputPresetId.RuntimeFull, document.PresetId);
    }

    [Fact]
    public void Scenario_Only는_조건과_대사만_남긴다()
    {
        PresetSample sample = BuildPresetSample();

        RenderedDocument document = Compose(sample, OutputPresetCatalog.ScenarioOnly);

        // ⛔ <b>머리글은 안 낸다</b> (2026-09-18 소유자). 이 프리셋은 <b>쓰는 면</b>이고
        //    그 글은 그대로 파서로 되돌아가는데, 파서는 <c>[</c>로 시작하는 줄을 「해석 못 한
        //    줄」로 올린다 — 저장할 때마다 자기가 낸 `[장면] …`이 붉은 글씨로 돌아왔다.
        Assert.DoesNotContain(document.Segments, segment => segment.Kind == RenderedSegmentKind.NodeHeader);
        Assert.Contains(document.Segments, segment => segment.Kind == RenderedSegmentKind.ConditionBegin);
        Assert.Contains(document.Segments, segment => segment.Kind == RenderedSegmentKind.DialogueLine);
        Assert.DoesNotContain(document.Segments, segment =>
            segment.Kind is RenderedSegmentKind.BranchJump
                or RenderedSegmentKind.BranchDetour
                or RenderedSegmentKind.DefaultJump);

        string text = DocumentPreviewFormatter.Format(document);
        Assert.DoesNotContain("[장면]", text, StringComparison.Ordinal);

        // X11 — 조건은 대괄호 표기가 아니라 실제 Yarn 문법이다(표기=파싱 문법, X12 왕복 전제).
        Assert.Contains("<<if ", text, StringComparison.Ordinal);
        Assert.Contains("<<endif>>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[조건", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#line:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<<set", text, StringComparison.Ordinal);
    }

    [Fact]
    public void 시나리오_프리셋은_남아_있는_Segment의_source_mapping을_그대로_유지한다()
    {
        PresetSample sample = BuildPresetSample();
        RenderedDocument full = Compose(sample, OutputPresetCatalog.RuntimeFull);
        Dictionary<string, RenderSourceReference> fullSources = full.Segments.ToDictionary(
            segment => segment.Id,
            segment => segment.Source,
            StringComparer.Ordinal);

        RenderedDocument document = Compose(sample, OutputPresetCatalog.ScenarioOnly);

        foreach (RenderedSegment segment in document.Segments)
        {
            Assert.True(fullSources.TryGetValue(segment.Id, out RenderSourceReference? source));
            Assert.Equal(source, segment.Source);
            Assert.False(string.IsNullOrWhiteSpace(segment.Source.NodeId));
        }
    }

    private static RenderedDocument Compose(PresetSample sample, OutputPreset preset)
    {
        return ResultDocumentComposer.Compose(
            sample.Dialogue,
            sample.Sample.Project,
            preset.Options,
            preset.Id);
    }

    private static PresetSample BuildPresetSample()
    {
        var sample = new Sample();

        string opening = sample.Line(
            "첫 대사",
            LineConditionTransition.BeginIf(sample.ConditionA.Id));
        sample.Editor.SetScriptLineText(sample.Dialogue.Id, opening, "라루", "첫 대사");
        string ending = sample.Line("조건 뒤", LineConditionTransition.EndIf());
        sample.Editor.SetScriptLineText(sample.Dialogue.Id, ending, "윌로", "조건 뒤");

        // ⚠ 작가가 <b>줄에</b> 단 set — 2026-08-24부터 문서에 남는 set은 이것뿐이다.
        // 설정노드의 초기값은 선언으로만 나간다(작업지시 §4).

        sample.Editor.SetExitTarget(
            sample.Dialogue.Id,
            ExitPortKind.Branch,
            opening,
            sample.TargetA.Id);
        sample.Editor.SetExitTarget(
            sample.Dialogue.Id,
            ExitPortKind.Default,
            null,
            sample.TargetDefault.Id);

        return new PresetSample(sample, opening, sample.Unfold());
    }

    private sealed record PresetSample(
        Sample Sample,
        string Opening,
        DialogueResult Dialogue);

    private sealed class DictionaryLocalizedLineProvider : ILocalizedLineProvider
    {
        private readonly IReadOnlyDictionary<string, string> _lines;

        public DictionaryLocalizedLineProvider(IReadOnlyDictionary<string, string> lines)
        {
            _lines = lines;
        }

        public string? GetLocalizedText(string lineId)
        {
            return _lines.TryGetValue(lineId, out string? text) ? text : null;
        }
    }
}
