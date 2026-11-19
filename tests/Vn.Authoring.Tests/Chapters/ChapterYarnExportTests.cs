using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;
using Yarn;
using Yarn.Compiler;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 대사만 있는 Yarn <c>exported/yarn/{챕터}_{장면}.yarn</c> (2026-11-17 · <c>docs/design/two-graphs.md</c> 7절 V).
/// 지키는 것: <b>실제로 컴파일된다</b>(YarnSpinner.Compiler 3.2.1 — 앱은 컴파일러를 싣지 않고 여기서만 본다) · 노드 타이틀 = 진행 JSON의
/// <c>DialogueEntryId</c> · 장면 메모와 에피소드 지시는 주석 · 작가의 글자가 그대로 문자열 표에 실린다 · 결정성.
/// </summary>
public sealed class ChapterYarnExportTests
{
    private const string Ch = "festival_eve";

    /// <summary>시험 재료(축제 전날 — <c>tests/fixtures/festival-eve</c>)와 런타임 예제(연출 데모)의 원고.</summary>
    private static readonly string[] SampleManifests =
    [
        "tests/fixtures/festival-eve/festival_eve.vnproject.json",
        "samples/RuntimeSamples/PresentationDemo/presentation_demo.vnproject.json"
    ];

    private static string SamplePath(string relative) =>
        Path.GetFullPath("../../../../../" + relative);

    private static ProjectEditor Sample() => new(ProjectStore.Load(SamplePath(SampleManifests[0])).Project);

    private static ChapterGraphModel Model(ProjectEditor editor, string chapterId = Ch) =>
        editor.FindChapter(chapterId)!.ToGraphModel(chapterId);

    private static IReadOnlyList<ChapterYarnFile> Export(ProjectEditor editor) =>
        ChapterYarnExporter.Export(Model(editor), editor.Project);

    /// <summary>
    /// 파일들을 <b>한 Yarn 프로젝트로</b> 컴파일한다 — 장면 사이의 <c>&lt;&lt;detour&gt;&gt;</c>가 서려면 함께 걸어야 한다.
    /// <c>stat</c>은 런타임이 등록하는 함수라(계약서 §D1-1) 여기서도 같은 모양(글자 → 수)으로 알려 준다.
    /// </summary>
    internal static CompilationResult Compile(IEnumerable<ChapterYarnFile> files)
    {
        var library = new Library();
        library.RegisterFunction("stat", (string _) => 0f);

        ISourceInput[] inputs = files
            .Select(file => (ISourceInput)new CompilationJob.File { FileName = file.FileName, Source = file.Text })
            .ToArray();

        return Compiler.Compile(CompilationJob.CreateFromInputs(inputs, library));
    }

    private static string[] Errors(CompilationResult result) => result.Diagnostics
        .Where(diagnostic => diagnostic.Severity == Diagnostic.DiagnosticSeverity.Error)
        .Select(diagnostic => $"{diagnostic.FileName}:{diagnostic.Range.Start.Line + 1}: {diagnostic.Message}")
        .ToArray();

    private static void SetLine(DialogueNode card, string lineId, string speaker, string text) =>
        card.Script.RequireLocale(card.Script.PrimaryLocale).Entries[lineId] = new LocalizedLine(speaker, text);

    [Fact]
    public void 견본_프로젝트의_모든_장면이_막힘_없이_컴파일된다()
    {
        foreach (string manifest in SampleManifests)
        {
            StoryProject project = ProjectStore.Load(SamplePath(manifest)).Project;

            List<ChapterYarnFile> files = project.Chapters
                .SelectMany(chapter => ChapterYarnExporter.Export(chapter.ToGraphModel(chapter.ChapterId), project))
                .ToList();

            Assert.NotEmpty(files);
            Assert.All(files, file => Assert.False(file.HasBlockingProblems, $"{file.FileName}: {string.Join(" / ", file.Problems)}"));

            CompilationResult result = Compile(files);
            Assert.True(Errors(result).Length == 0, string.Join("\n", Errors(result)));
        }
    }

    [Fact]
    public void 노드_타이틀은_진행_JSON의_DialogueEntryId와_같다()
    {
        ProjectEditor editor = Sample();
        ChapterGraphModel model = Model(editor);

        ChapterExportResult progression = ChapterProgressionExporter.Export(model, editor.Project);
        string[] entries = JsonDocument.Parse(progression.Json!).RootElement.GetProperty("Nodes").EnumerateArray()
            .Select(node => node.GetProperty("DialogueEntryId").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CompilationResult result = Compile(Export(editor));

        Assert.Empty(Errors(result));

        // 진행 JSON이 부르는 노드 + 분기 카드 노드(진행 JSON에 없고 `<<detour>>`가 부른다 — 2026-11-17).
        string[] branches = ChapterBranchCards.HomesOf(editor.Project, Ch)
            .Select(home => YarnNaming.StoryNodeTitleOf(home.Card.Name, home.Card.Id))
            .ToArray();
        Assert.Equal(5, branches.Length);
        Assert.Equal(
            entries.Concat(branches).Order(StringComparer.Ordinal).ToArray(),
            result.Program!.Nodes.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void 분기_카드는_집의_장면_파일에서_그_에피소드_바로_뒤에_서고_분기점이_부르는_노드가_다_있다()
    {
        ProjectEditor editor = Sample();
        IReadOnlyList<ChapterYarnFile> files = Export(editor);
        string classroom = files.Single(file => file.SceneId == "교실").Text;

        // 교실 - 간판 완성(Festival_Wrapup)이 부르는 둘 — 그 노드가 끝난 바로 뒤에 대본 차례로.
        int wrapup = classroom.IndexOf("title: Festival_Wrapup\n", StringComparison.Ordinal);
        int remade = classroom.IndexOf("// 분기 카드: Festival_Wrapup_Remade — 「교실 - 간판 완성」에서 들른다\ntitle: Festival_Wrapup_Remade\n", StringComparison.Ordinal);
        int sticker = classroom.IndexOf("title: Festival_Wrapup_Sticker\n", StringComparison.Ordinal);
        Assert.True(wrapup >= 0 && remade > wrapup && sticker > remade, classroom);
        Assert.DoesNotContain("title: Festival_Staffroom_Kasmi", classroom, StringComparison.Ordinal);

        // 분기점이 부르는 노드는 모두 선다 — 없으면 게임에서 `<<detour>>`가 없는 노드를 부른다.
        CompilationResult result = Compile(files);
        string[] detours = files.SelectMany(file => file.Text.Split('\n'))
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("<<detour ", StringComparison.Ordinal))
            .Select(line => line["<<detour ".Length..^2])
            .Distinct()
            .ToArray();
        Assert.Equal(5, detours.Length);
        Assert.All(detours, title => Assert.Contains(title, result.Program!.Nodes.Keys));
    }

    [Fact]
    public void 장면이_파일이고_파일_이름은_챕터_장면이다()
    {
        ProjectEditor editor = Sample();
        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(Model(editor));
        IReadOnlyList<ChapterYarnFile> files = Export(editor);

        Assert.Equal(scenes.Select(scene => scene.SceneId), files.Select(file => file.SceneId));
        Assert.Equal("festival_eve_교무실_앞.yarn", files.Single(file => file.SceneId == "교무실 앞").FileName);

        // 장면 안 에피소드 차례 그대로 노드가 서고, 그 장면의 에피소드가 부르는 분기 카드가 함께 선다.
        IReadOnlyList<BranchCardHome> homes = ChapterBranchCards.HomesOf(editor.Project, Ch);

        foreach ((ChapterScene scene, ChapterYarnFile file) in scenes.Zip(files))
        {
            string[] titles = file.Text.Split('\n')
                .Where(line => line.StartsWith("title: ", StringComparison.Ordinal))
                .Select(line => line["title: ".Length..])
                .ToArray();

            int branches = homes.Count(home => scene.Episodes.Any(episode => episode.EpisodeId == home.EpisodeId));
            Assert.Equal(scene.Episodes.Count + branches, titles.Length);
        }
    }

    [Fact]
    public void 장면_메모는_파일_머리에_에피소드_지시는_그_노드_앞에_주석으로_선다()
    {
        ProjectEditor editor = Sample();
        ChapterScene first = ChapterSceneGrouping.Of(Model(editor))[0];
        ChapterEpisode second = first.Episodes[1];

        editor.SetSceneName(Ch, first.SceneId, "마감 전날의 교실");
        editor.SetSceneNote(Ch, first.SceneId, "마감 전날.\n\n하린: 지침 → 결심");
        editor.UpdateEpisodeMemo(Ch, second.EpisodeId, "짧게\r\n해 질 무렵");

        ChapterYarnFile file = Export(editor)[0];
        string title = ChapterProgressionExporter.DialogueEntryIdOf(second);

        Assert.StartsWith(
            $"// 장면: 마감 전날의 교실 ({first.SceneId})\n// 마감 전날.\n//\n// 하린: 지침 → 결심\n\n// 에피소드: ",
            file.Text);
        Assert.Contains($"// 에피소드: {second.ShownTitle}\n// 짧게\n// 해 질 무렵\ntitle: {title}\n---\n", file.Text);
        Assert.Empty(Errors(Compile(Export(editor))));
    }

    [Fact]
    public void 이름이_없는_장면은_장면ID만_적는다()
    {
        ProjectEditor editor = Sample();
        ChapterScene first = ChapterSceneGrouping.Of(Model(editor))[0];

        Assert.StartsWith($"// 장면: {first.SceneId}\n\n// 에피소드: ", Export(editor)[0].Text);
    }

    [Fact]
    public void 작가가_적은_Yarn_글자는_그대로_문자열_표에_실린다()
    {
        // #은 태그, //는 주석(조용히 잘린다), <<는 명령, \는 이스케이프 — 막지 않으면 원고가 깨지거나 줄어든다.
        // {stat("키")}는 일부러 적는 스탯 읽기라 그대로 둔다.
        ProjectEditor editor = Sample();
        ChapterEpisode episode = Model(editor).Episodes[0];
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Ch, episode.EpisodeId)!;
        ScriptLine[] lines = card.Script.ActiveLines.Take(3).ToArray();

        SetLine(card, lines[0].Id, "하린", "1등 #1 이다 // 진짜로 <<농담>> C:\\temp");
        SetLine(card, lines[1].Id, "", "http://x.y — 3 < 5 > 2 [b]굵게[/b]");
        SetLine(card, lines[2].Id, "시구레", "성실은 {stat(\"성실\")}점");

        CompilationResult result = Compile(Export(editor));

        Assert.Empty(Errors(result));
        Assert.Equal("하린: 1등 #1 이다 // 진짜로 <<농담>> C:\\temp", result.StringTable!["line:" + lines[0].Id].text);
        Assert.Equal("http://x.y — 3 < 5 > 2 [b]굵게[/b]", result.StringTable!["line:" + lines[1].Id].text);
        Assert.Equal("시구레: 성실은 {0}점", result.StringTable!["line:" + lines[2].Id].text);
    }

    [Fact]
    public void 빈_줄은_보이지_않는_한_글자로_서고_줄바꿈은_빈칸으로_접힌다()
    {
        ProjectEditor editor = Sample();
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Ch, Model(editor).Episodes[0].EpisodeId)!;
        ScriptLine[] lines = card.Script.ActiveLines.Take(2).ToArray();

        SetLine(card, lines[0].Id, "", "");
        SetLine(card, lines[1].Id, "하린", "첫 줄\n둘째 줄");

        string text = Export(editor)[0].Text;

        Assert.Contains($"\n{ChapterYarnExporter.EmptyLineBody} #line:{lines[0].Id}\n", text);
        Assert.Contains($"\n하린: 첫 줄 둘째 줄 #line:{lines[1].Id}\n", text);
        Assert.Empty(Errors(Compile(Export(editor))));
    }

    [Fact]
    public void 분기에_건_식_미정_조건은_항상_통과로_나간다()
    {
        // 간선 · 분기와 같은 규칙(writer-fields W1) — 식이 빈 조건은 오류가 아니라 「항상 통과」다.
        ProjectEditor editor = Sample();
        DialogueNode branch = EpisodeNaming.BoardOf(editor.Project, Ch)!.Nodes.OfType<DialogueNode>().Single(node => node.Name == "Festival_Wrapup_Remade");
        editor.AddChapterCondition(Ch, "다시 그렸을 때", string.Empty);

        Assert.Null(BranchEntryCondition.Set(editor, branch.Id, "다시 그렸을 때"));

        string text = Export(editor).Single(file => file.Text.Contains("title: Festival_Wrapup_Remade\n", StringComparison.Ordinal)).Text;

        Assert.Contains("title: Festival_Wrapup_Remade\n---\n<<if true>>\n    하린:", text);
        Assert.Empty(Errors(Compile(Export(editor))));
    }

    [Fact]
    public void 카드가_없는_에피소드도_노드는_선다()
    {
        // 진행 JSON이 그 타이틀을 부른다 — 노드가 없으면 런타임이 없는 노드를 찾는다.
        ProjectEditor editor = Sample();
        ChapterGraphModel model = Model(editor);
        ChapterEpisode episode = model.Episodes[0];

        IReadOnlyList<ChapterYarnFile> files = ChapterYarnExporter.Export(model, project: null);
        string title = ChapterProgressionExporter.DialogueEntryIdOf(episode);

        Assert.Contains($"title: {title}\n---\n===\n", files[0].Text);
        Assert.All(files, file => Assert.False(file.HasBlockingProblems));
    }

    [Fact]
    public void Yarn이_읽지_못하는_줄은_막는다()
    {
        ProjectEditor editor = Sample();
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Ch, Model(editor).Episodes[0].EpisodeId)!;
        ScriptLine[] lines = card.Script.ActiveLines.Take(2).ToArray();

        SetLine(card, lines[0].Id, "", "-> 가자");
        SetLine(card, lines[1].Id, "하린", "값 { 끝");

        ChapterYarnFile file = Export(editor)[0];

        Assert.True(file.HasBlockingProblems);
        Assert.Contains(file.Problems, problem => problem.IsBlocking && problem.LineId == lines[0].Id);
        Assert.Contains(file.Problems, problem => problem.IsBlocking && problem.LineId == lines[1].Id);
    }

    [Fact]
    public void 정규화가_두_장면을_한_이름으로_접으면_뒤의_것을_막는다()
    {
        ProjectEditor editor = Sample();
        ChapterGraphModel model = Model(editor);
        ChapterEpisode[] episodes = model.Episodes.ToArray();

        foreach (ChapterEpisode episode in episodes)
        {
            editor.UpdateEpisode(Ch, episode.EpisodeId, sceneId: Array.IndexOf(episodes, episode) < episodes.Length / 2 ? "장면 1" : "장면.1");
        }

        IReadOnlyList<ChapterYarnFile> files = Export(editor);

        Assert.Equal(["festival_eve_장면_1.yarn", "festival_eve_장면_1.yarn"], files.Select(file => file.FileName).ToArray());
        Assert.False(files[0].HasBlockingProblems);
        Assert.True(files[1].HasBlockingProblems);
    }

    [Fact]
    public void 같은_입력이면_같은_바이트다()
    {
        ProjectEditor editor = Sample();
        editor.SetSceneNote(Ch, Model(editor).Episodes[0].SceneId, "메모\r\n둘째");

        string[] first = Export(editor).Select(file => file.Text).ToArray();
        string[] again = Export(editor).Select(file => file.Text).ToArray();

        Assert.Equal(first, again);
        Assert.All(first, text => Assert.DoesNotContain("\r", text));
    }
}
