using System.Text;
using Vn.Authoring.Definition;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Results;

namespace Vn.Authoring.Chapters;

/// <summary>대사 Yarn을 지으며 만난 문제 하나. 막는 문제를 안은 장면은 파일로 쓰지 않는다.</summary>
public sealed record ChapterYarnProblem(string Message, bool IsBlocking, string? EpisodeId = null, string? LineId = null);

/// <summary>장면 하나의 대사 Yarn — 파일로 쓰기 전의 순수 문자열이다.</summary>
public sealed record ChapterYarnFile(
    string SceneId,
    string FileName,
    string Text,
    IReadOnlyList<ChapterYarnProblem> Problems)
{
    public bool HasBlockingProblems => Problems.Any(problem => problem.IsBlocking);
}

/// <summary>
/// <b>대사만 있는 Yarn</b> — <c>exported/yarn/{챕터}_{장면}.yarn</c> (2026-11-17 · <c>docs/work-orders/yarn-output-orders.md</c>).
///
/// 작가가 「대사만으로 굴러가는지」 확인하고, 연출을 붙이는 쪽(AI)이 장면 하나를 파일 하나로 읽는다. <b>장면 = 파일, 에피소드 = 노드.</b>
/// 분기 카드는 그 집(부르는 에피소드 — <see cref="ChapterBranchCards.HomesOf"/>)의 노드 바로 뒤에 노드로 선다.
/// 장면 메모는 파일 머리에, 에피소드 지시는 그 노드 앞에 <b>주석</b>으로 선다(소유자 2026-11-17 — 메모의 집은 진행 JSON이 아니라 여기다).
///
/// ⛔ 2026-10-29(R2 · R3)에 「게임용 Yarn은 compositor만 낸다」로 이미터를 걷었던 것을 <b>대사만 있는 Yarn</b>의 범위에서 뒤집었다.
/// 연출 명령은 여기서 내지 않고, <b>게임 폴더에는 쓰지 않는다</b> — 연출 든 Yarn이 거기 있으면 덮여서 연출이 사라진다.
///
/// <b>지키는 것</b>
/// <list type="bullet">
/// <item>노드 타이틀은 진행 JSON의 <c>DialogueEntryId</c>와 <b>같은 함수</b>
/// (<see cref="ChapterProgressionExporter.DialogueEntryIdOf"/>)를 지난다 — 갈리면 진행 JSON이 없는 노드를 부른다.</item>
/// <item>본문은 카드 글 칸과 같은 합성기(<see cref="ResultDocumentComposer"/>)의 조각에서 짓는다 — 화면과 파일이 갈리지 않는다.</item>
/// <item><c>&lt;&lt;set&gt;&gt;</c> · <c>&lt;&lt;declare&gt;&gt;</c> · 선택지는 내지 않는다(런타임에 Yarn 변수 층이 없고, 선택지는 챕터 간선의 것이다).</item>
/// <item>카드나 줄이 없는 에피소드도 노드는 선다 — 진행 JSON이 그 타이틀을 부른다.</item>
/// <item>결정적이다 — 같은 입력이면 같은 바이트(UTF-8 BOM 없음 · LF). 장면은 하이어아키 차례, 에피소드는 장면 안 차례.</item>
/// </list>
/// </summary>
public static class ChapterYarnExporter
{
    /// <summary><c>exported/</c> 아래 대사 Yarn이 놓이는 폴더.</summary>
    public const string FolderName = "yarn";

    public const string FileExtension = ".yarn";

    /// <summary>
    /// 본문이 빈 대사 줄의 자리를 채우는 <b>보이지 않는 한 글자</b>(U+00A0). 빈 본문을 그대로 내면 <c>#line:</c> 태그만 남은 줄이 되어
    /// Yarn이 파싱하지 못한다. 줄을 빼지 않는 까닭은 LineId다 — 연출은 그 열쇠에 걸린다.
    /// </summary>
    public const string EmptyLineBody = " ";

    /// <summary>그 프로젝트의 대사 Yarn 폴더 — <c>exported/yarn/</c>.</summary>
    public static string FolderFor(string projectPath) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(projectPath))!,
        ChapterExportService.ExportFolderName,
        FolderName);

    /// <summary>
    /// 장면 하나의 파일 이름 — <c>{챕터}_{장면}.yarn</c>. 폴더가 챕터를 섞어 담으므로 챕터를 앞에 붙인다.
    /// 이름은 노드 타이틀과 같은 정규화를 지난다(문자 · 숫자 · 밑줄).
    /// </summary>
    public static string FileNameOf(string chapterId, string sceneId) =>
        $"{YarnSyntax.SanitizeNodeName(chapterId)}_{YarnSyntax.SanitizeNodeName(sceneId)}{FileExtension}";

    /// <summary>이 챕터의 파일이 쓰는 이름의 머리 — 낡은 파일을 걷을 때 「이 챕터의 것」을 가린다.</summary>
    public static string FilePrefixOf(string chapterId) => YarnSyntax.SanitizeNodeName(chapterId) + "_";

    /// <summary>챕터의 장면마다 파일 하나.</summary>
    /// <param name="project">대본(카드)을 읽을 프로젝트. 없으면 노드마다 본문이 비어 선다.</param>
    public static IReadOnlyList<ChapterYarnFile> Export(
        ChapterGraphModel chapter,
        StoryProject? project,
        GameDefinition? definition = null)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var via = ChapterBoard.For(project, chapter.ChapterId);
        IReadOnlyList<BranchCardHome> homes = project is null ? [] : ChapterBranchCards.HomesOf(project, chapter.ChapterId);
        var files = new List<ChapterYarnFile>();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (ChapterScene scene in ChapterSceneGrouping.Of(chapter))
        {
            ChapterYarnFile file = Scene(chapter, scene, via, homes, project, definition);

            // 정규화가 두 장면을 한 이름으로 접으면(`장면 1` · `장면.1`) 뒤의 것이 앞의 파일을 덮는다 — 쓰기 전에 막는다.
            if (names.TryGetValue(file.FileName, out string? taken))
            {
                file = file with
                {
                    Problems =
                    [
                        .. file.Problems,
                        new ChapterYarnProblem(
                            $"장면 '{scene.SceneId}'와 '{taken}'의 파일 이름이 같습니다({file.FileName}) — 장면 ID를 고쳐 주세요.",
                            IsBlocking: true)
                    ]
                };
            }
            else
            {
                names[file.FileName] = scene.SceneId;
            }

            files.Add(file);
        }

        return files;
    }

    private static ChapterYarnFile Scene(
        ChapterGraphModel chapter,
        ChapterScene scene,
        ChapterBoard via,
        IReadOnlyList<BranchCardHome> homes,
        StoryProject? project,
        GameDefinition? definition)
    {
        var problems = new List<ChapterYarnProblem>();
        var text = new StringBuilder();

        // ── 파일 머리: 장면 이름과 장면 메모 ────────────────────────────────────
        text.Append("// 장면: ").Append(OneLine(scene.DisplayName));

        if (!string.Equals(scene.DisplayName, scene.SceneId, StringComparison.Ordinal))
        {
            text.Append(" (").Append(OneLine(scene.SceneId)).Append(')');
        }

        text.Append('\n');
        AppendComment(text, ChapterSceneNotes.TextOf(chapter.SceneNotes, scene.SceneId));

        foreach (ChapterEpisode episode in scene.Episodes)
        {
            text.Append('\n');
            text.Append("// 에피소드: ").Append(OneLine(episode.ShownTitle)).Append('\n');
            AppendComment(text, episode.Memo);

            string title = ChapterProgressionExporter.DialogueEntryIdOf(episode, via);

            text.Append("title: ").Append(title).Append("\n---\n");
            AppendBody(text, episode.EpisodeId, via.EpisodeNodeFor(episode), project, definition, problems);
            text.Append("===\n");

            // 이 에피소드가 부르는 분기 카드는 바로 뒤에 노드로 선다 — 분기 카드는 에피소드가 아니라 진행 JSON에 없고, 이 노드를
            // `<<detour>>`가 부른다(2026-11-17). 타이틀은 분기점이 부르는 이름과 같은 함수를 지난다.
            foreach (BranchCardHome home in homes.Where(home => string.Equals(home.EpisodeId, episode.EpisodeId, StringComparison.Ordinal)))
            {
                text.Append('\n');
                text.Append("// 분기 카드: ").Append(OneLine(home.Card.Name))
                    .Append(" — 「").Append(OneLine(episode.ShownTitle)).Append("」에서 들른다\n");
                text.Append("title: ").Append(YarnNaming.StoryNodeTitleOf(home.Card.Name, home.Card.Id)).Append("\n---\n");
                AppendBody(text, episode.EpisodeId, home.Card, project, definition, problems);
                text.Append("===\n");
            }
        }

        return new ChapterYarnFile(
            scene.SceneId,
            FileNameOf(chapter.ChapterId, scene.SceneId),
            ChapterExportBytes.Normalize(text.ToString()),
            problems);
    }

    /// <param name="episodeId">문제를 짚을 에피소드 — 분기 카드면 그 집(부르는 에피소드).</param>
    private static void AppendBody(
        StringBuilder text,
        string episodeId,
        DialogueNode? card,
        StoryProject? project,
        GameDefinition? definition,
        List<ChapterYarnProblem> problems)
    {
        if (project is null || card is null)
        {
            return; // 카드가 없다 — 노드만 선다. 없는 카드는 챕터 검증이 짚는다.
        }

        DialogueDraft draft = DialoguePublisher.Draft(project, card.Id, definition);

        foreach (PublishProblem problem in draft.Problems)
        {
            // 줄 없는 카드는 막지 않는다 — 빈 노드로 선다(작가가 아직 안 쓴 에피소드다).
            bool blocking = problem.IsBlocking && problem.Kind != PublishProblemKind.MissingScript;
            problems.Add(new ChapterYarnProblem(problem.Message, blocking, episodeId, problem.LineId));
        }

        RenderedDocument document = ResultDocumentComposer.Compose(
            DialoguePublisher.AsResult(draft),
            project,
            OutputPresetCatalog.RuntimeFull.Options);

        foreach (RenderedSegment segment in document.Segments)
        {
            string indent = YarnSyntax.IndentOf(segment.IndentLevel);

            switch (segment.Kind)
            {
                case RenderedSegmentKind.ConditionBegin:
                    text.Append(indent);
                    YarnSyntax.AppendCondition(text, "if", segment.Expression);
                    text.Append('\n');
                    break;

                case RenderedSegmentKind.ConditionElseIf:
                    text.Append(indent);
                    YarnSyntax.AppendCondition(text, "elseif", segment.Expression);
                    text.Append('\n');
                    break;

                case RenderedSegmentKind.ConditionEnd:
                    text.Append(indent).Append("<<endif>>\n");
                    break;

                case RenderedSegmentKind.DialogueLine:
                    AppendLine(text, segment, indent, episodeId, problems);
                    break;

                case RenderedSegmentKind.BranchDetour:
                    text.Append(indent);
                    YarnSyntax.AppendDetour(text, TargetOf(segment));
                    text.Append('\n');
                    break;

                case RenderedSegmentKind.DefaultJump:
                    text.Append(indent);
                    YarnSyntax.AppendJump(text, TargetOf(segment));
                    text.Append('\n');
                    break;

                case RenderedSegmentKind.Warning:
                    problems.Add(new ChapterYarnProblem(
                        segment.Text ?? "알 수 없는 경고", IsBlocking: false, episodeId, segment.Source.LineId));
                    break;
            }
        }
    }

    private static void AppendLine(
        StringBuilder text,
        RenderedSegment segment,
        string indent,
        string episodeId,
        List<ChapterYarnProblem> problems)
    {
        string speaker = segment.Speaker?.Trim() ?? string.Empty;
        string body = YarnSyntax.EscapeLineText(OneLine(segment.Text));
        string? lineId = segment.Source.LineId;

        if (string.IsNullOrEmpty(lineId))
        {
            problems.Add(new ChapterYarnProblem(
                $"대사 '{segment.Text}'에 LineId가 없어 #line: 태그를 만들 수 없습니다.", IsBlocking: true, episodeId));
            return;
        }

        // 화자 없이 `->`나 `===`로 시작하는 줄은 Yarn이 선택지 · 노드 끝으로 읽는다 — 막을 이스케이프가 없어 사람에게 돌려보낸다.
        if (speaker.Length == 0 &&
            (body.StartsWith("->", StringComparison.Ordinal) || body.StartsWith("===", StringComparison.Ordinal)))
        {
            problems.Add(new ChapterYarnProblem(
                $"대사 '{segment.Text}'가 `->`나 `===`로 시작합니다 — Yarn이 대사로 읽지 못합니다. 첫 글자를 고쳐 주세요.",
                IsBlocking: true,
                episodeId,
                lineId));
        }

        // `{ }`는 스탯 읽기(`{stat("키")}`)라 그대로 싣는다. 짝이 안 맞으면 그 줄부터 파일이 깨진다.
        if (body.Count(c => c == '{') != body.Count(c => c == '}'))
        {
            problems.Add(new ChapterYarnProblem(
                $"대사 '{segment.Text}'의 중괄호 짝이 맞지 않습니다 — `{{ }}`는 스탯 읽기에만 씁니다.",
                IsBlocking: true,
                episodeId,
                lineId));
        }

        text.Append(indent);

        if (speaker.Length > 0)
        {
            text.Append(speaker).Append(": ");
        }

        text.Append(body.Length == 0 && speaker.Length == 0 ? EmptyLineBody : body);
        text.Append(" #line:").Append(lineId).Append('\n');
    }

    /// <summary>출구가 가리키는 노드의 타이틀 — 그 노드가 제 파일에서 갖는 타이틀과 같은 규칙이다.</summary>
    private static string TargetOf(RenderedSegment segment) =>
        YarnNaming.StoryNodeTitleOf(segment.TargetNodeName, segment.TargetNodeId);

    /// <summary>메모를 주석으로 — Yarn 주석은 한 줄짜리라 줄마다 <c>// </c>를 붙인다. 빈 메모는 아무것도 안 쓴다.</summary>
    private static void AppendComment(StringBuilder text, string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo))
        {
            return;
        }

        foreach (string line in memo.ReplaceLineEndings("\n").Trim('\n').Split('\n'))
        {
            text.Append("//");

            if (line.Length > 0)
            {
                text.Append(' ').Append(line.TrimEnd());
            }

            text.Append('\n');
        }
    }

    /// <summary>한 줄에 설 글 — 줄바꿈은 빈칸으로 접는다(줄이 갈리면 뒤쪽이 주석 · 대사 밖으로 샌다).</summary>
    private static string OneLine(string? value) =>
        (value ?? string.Empty).ReplaceLineEndings(" ").Trim();
}
