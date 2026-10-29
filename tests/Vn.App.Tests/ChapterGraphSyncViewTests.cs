using Avalonia.Controls;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.Authoring.Editing;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Tests.Fixtures;

namespace Vn.App.Tests;

/// <summary>
/// 챕터 그래프가 읽어 보여 주는 것 — 고른 에피소드의 대사 미리보기, [챕터] 탭의 스탯 표, 그리고
/// 검증 보고와 진행 JSON 자동 내보내기(G8).
///
/// ⛔ 이 파일의 앞 절(노드 클릭이 대본 워크북을 만들어 열고, [대본 가져오기]가 대사 노드·보고 패널·배지로
/// 이어지던 것)은 2026-10-29에 엑셀 연동과 함께 걷혔다(docs/design/two-graphs.md §7 D2). 이름의
/// "동기화"는 그 흔적이다.
/// </summary>
public sealed class ChapterGraphSyncViewTests
{
    [Fact]
    public void 에피소드를_고르면_대사_탭에서_대사가_읽힌다() => HeadlessUi.Run(() =>
    {
        // 소유자 요청 — 시나리오 그래프까지 안 가도, 챕터 그래프의 [대사] 탭에서
        // 고른 에피소드의 대사가 (읽기 전용으로) 보여야 한다. 원천은 그 에피소드 카드의 대본이다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        WriteLines(
            session,
            "main05.02",
            ("윌로", "복도는 조용했다."),
            ("라루", "여기서 기다릴까?"),
            ("윌로", "너를 믿어."),
            ("라루", "다리가 무거워."),
            ("라루", "아직도 화가 나."),
            ("윌로", "문이 열렸다."));

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var preview = view.FindControl<SelectableTextBlock>("DialoguePreviewText")!;

        // 깊이·들여쓰기 없이 「화자: 대사」 줄이 차례로 선다.
        Assert.Equal(
            """
            윌로: 복도는 조용했다.
            라루: 여기서 기다릴까?
            윌로: 너를 믿어.
            라루: 다리가 무거워.
            라루: 아직도 화가 나.
            윌로: 문이 열렸다.
            """.ReplaceLineEndings("\n"),
            preview.Text!.ReplaceLineEndings("\n"));

        // 아직 대사를 안 적은 에피소드는 빈 화면 대신 그 사실을 말한다.
        view.SelectEpisode("main05.01");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(string.Empty, preview.Text);
        Assert.Contains("아직 적힌 대사가 없습니다",
            view.FindControl<TextBlock>("DialoguePreviewHeader")!.Text);
    });

    [Fact]
    public void 챕터_탭이_스탯을_고칠_수_있게_세운다() => HeadlessUi.Run(() =>
    {
        // 소유자 점검 — 스탯은 어디에서도 값이 안 보이던 표다. [챕터] 탭이 조건 아래에 세운다.
        // 에피소드·간선은 그래프가 이미 그린다.
        //
        // ⚠ <b>2026-09-17에 읽기 전용이 아니게 됐다</b> (소유자 — 계층을 하나로 합치면서):
        //    *"이제 엑셀이 아닌 챕터그래프에서 직접 조건과 스탯을 정의하려고 합니다"*.
        //    R-F가 워크북을 산출물로 만든 뒤로 *"편집은 엑셀에서"*는 이미 낡은 안내였다.
        //
        // 2026-08-24 소유자 — 나란히 서 있던 픽스처 표는 걷었다("한 번도 안 썼어").
        // 챕터의 픽스처는 그대로다: 화면만 없앴다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        StackPanel row = view.FindControl<StackPanel>("StatListPanel")!
            .Children.OfType<StackPanel>()
            .Single(panel => Equals(panel.Tag, "stat:trust"));

        // 값 칸 넷(표시이름·초기·최소·최대) + 개명 · 삭제.
        Assert.Equal(4, row.Children.OfType<TextBox>().Count());
        Assert.Equal(2, row.Children.OfType<Button>().Count());

        Assert.Null(view.FindControl<StackPanel>("FixtureListPanel"));
    });

    // ── G8 내보내기 — 사람 손을 기다리지 않는다 (2026-08-17) ────────────────

    [Fact]
    public void 검증_오류가_있으면_JSON이_안_나가고_사유를_말한다() => HeadlessUi.Run(() =>
    {
        // 견본 챕터의 branch05.02A는 도달 불가다 — 신뢰를 올리는 길이 없어 `신뢰높음`이 안 선다 → 거부.
        // 쓰레기가 런타임으로 넘어가는 것보다 파일이 안 나가는 편이 낫다(G8).
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.False(Directory.Exists(project.ExportFolder));

        // 사유는 상태줄이 아니라 검증 보고에 선다 — 상태줄은 다음 동작의 보고가 곧 덮는다.
        // ⚠ 접힌 채로도 <b>머리글이 말한다</b> — 런타임으로 나갈 것이 안 나간 상태다.
        var expander = view.FindControl<Expander>("DiagnosticsExpander")!;
        Assert.Contains("진행 JSON 미출력", (string)expander.Header!);
        Assert.Contains("🔴", (string)expander.Header!);   // 안 나간 것은 오류 급 = 빨강
        Assert.False(expander.IsExpanded);

        Assert.Contains(
            view.FindControl<StackPanel>("DiagnosticsPanel")!.Children.OfType<TextBlock>(),
            line => line.Text!.Contains("진행 JSON이 나가지 않았습니다", StringComparison.Ordinal)
                && line.Text.Contains("ch05", StringComparison.Ordinal));
    });

    [Fact]
    public void 검증을_통과하면_규약_폴더에_JSON이_저절로_나간다() => HeadlessUi.Run(() =>
    {
        // 도달 불가를 `도달불가 허용`(D3)으로 명시 예외 처리하면 검증을 통과한다.
        // 단추를 누르지 않는다 — 프로젝트를 여는 그 순간 나간다.
        using var project = new TempProject(allowUnreachable: true);

        (ChapterGraphView view, _) = Show(project);

        string path = Path.Combine(project.ExportFolder, "ch05.progression.json");
        Assert.True(File.Exists(path));

        // 잘 나갔을 때는 아무 말도 없다 — 파일이 있다는 것이 곧 증거다.
        Assert.DoesNotContain(
            "진행 JSON", (string)view.FindControl<Expander>("DiagnosticsExpander")!.Header!);

        string json = File.ReadAllText(path);
        Assert.Contains("\"StartEpisodeId\": \"main05.01\"", json);
        Assert.DoesNotContain("기본 루트", json);   // 픽스처는 섞이지 않는다
    });

    [Fact]
    public void 툴에서_고치면_JSON도_따라_갱신된다() => HeadlessUi.Run(() =>
    {
        // 툴에서 고친 것이 JSON까지 간다 — 자동 내보내기(2026-08-17)는 사람 손을 기다리지 않는다.
        // (2026-09-16 R-F 전에는 `엑셀을_고치면_JSON도_따라_갱신된다`였다 — 워크북을 고쳐 다시 읽혔다.)
        using var project = new TempProject(allowUnreachable: true);

        (ChapterGraphView view, AuthoringSession session) = Show(project);

        string path = Path.Combine(project.ExportFolder, "ch05.progression.json");
        Assert.DoesNotContain("새로판길", File.ReadAllText(path));

        // ⚠ 문구를 준다 (2026-08-23). 문구 없는 간선은 에피소드당 하나뿐인데 main05.01에
        //    이미 있다. 둘이 되면 진행 코어가 그 챕터를 못 싣고 내보내기가 옳게 거부한다 —
        //    이 테스트가 재려는 것은 "고치면 JSON이 따라오는가"이지 그쪽이 아니다.
        //
        // 새 에피소드는 카드와 첫 줄이 함께 선다 — 빈 노드 오류에 걸리지 않는다.
        session.Editor.AddEpisode("ch05", "새로판길", title: string.Empty, 3, 1);
        session.Editor.AddEdge("ch05", "main05.01", "새로판길", optionLabel: "새로 판 길로");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains("새로판길", File.ReadAllText(path));
    });

    [Fact]
    public void 고른_챕터만이_아니라_모든_챕터가_나간다() => HeadlessUi.Run(() =>
    {
        // 고른 챕터만 내보내면 나머지는 누른 순간의 낡은 판으로 굳는다 — 사람 손을
        // 없앤 이유가 그것이다.
        //
        // 둘째 챕터도 판과 카드가 서 있어야 검증이 그 챕터의 카드를 찾는다 — 열 때 맞추기
        // (<c>AlignBoardsWithChapters</c>)가 두 챕터 모두에 세운다. 판 없는 챕터는 거부가 맞다
        // (전에는 검증이 ch05 판의 같은 EpisodeId 노드를 집어 통과시켰다 — excel-boundary C1).
        using var project = new TempProject(allowUnreachable: true, "ch05", "ch99");

        (ChapterGraphView view, AuthoringSession session) = Show(project);

        Assert.True(File.Exists(Path.Combine(project.ExportFolder, "ch05.progression.json")));
        Assert.True(
            File.Exists(Path.Combine(project.ExportFolder, "ch99.progression.json")),
            Reasons(project, "ch99", session.Project));
    });

    /// <summary>안 나간 사유를 실패 메시지에 담는다 — "왜"가 없으면 이 테스트는 못 고친다.</summary>
    private static string Reasons(TempProject project, string chapterId, StoryProject board)
    {
        ChapterGraphModel model = board.Chapters
            .Single(chapter => chapter.ChapterId == chapterId)
            .ToGraphModel(chapterId);

        // ⚠ 내보내기까지 돌린다 — 검증만 보면 코어 관문(CoreRefusals)이 낸 거부가 안 보인다.
        ChapterExportResult result = ChapterProgressionExporter.Export(model, board);

        string errors = string.Join(" || ", result.Validation.All
            .Where(item => item.Severity == ChapterDiagnosticSeverity.Error)
            .Select(item => item.Message));

        string files = Directory.Exists(project.ExportFolder)
            ? string.Join(", ", Directory.GetFiles(project.ExportFolder).Select(Path.GetFileName))
            : "(폴더 없음)";

        return $"거부={result.Refused} / 오류={(errors.Length == 0 ? "(없음)" : errors)} / 나간 것={files}";
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 그 에피소드 카드에 대사를 줄마다 적는다 — 작가가 카드에서 하는 일. 첫 줄은 카드가 설 때
    /// 함께 선 빈 줄이다.
    ///
    /// ⚠ 붙여넣기(<c>ApplyScenarioText</c>)로 한 번에 넣지 않는다 — 그 빈 줄이 고쳐진 것인지
    /// 사라진 것인지 판정이 서지 않아 붙여넣기가 통째로 멈춘다.
    /// </summary>
    private static void WriteLines(
        AuthoringSession session, string episodeId, params (string Speaker, string Text)[] lines)
    {
        string scriptId = EpisodeNaming.CardFor(session.Project, "ch05", episodeId)!.ScriptId!;

        for (int index = 0; index < lines.Length; index++)
        {
            string lineId = index == 0
                ? session.Project.FindScript(scriptId)!.ActiveLines.First().Id
                : session.Editor.InsertScriptLine(scriptId).Id;

            session.Editor.SetScriptLineText(scriptId, lineId, lines[index].Speaker, lines[index].Text);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static (ChapterGraphView View, AuthoringSession Session) Show(TempProject project)
    {
        var session = new AuthoringSession();
        session.Open(project.ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        view.Attach(session);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (view, session);
    }

    /// <summary>견본 챕터(ch05 복도)를 든 프로젝트. 판과 카드는 열 때 선다.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "vn-chapter-sync-view", Guid.NewGuid().ToString("N"));

        /// <param name="allowUnreachable">
        /// 견본에서 도달 불가인 <c>branch05.02A</c>를 `도달불가 허용`(D3)으로 명시 예외 처리한다 —
        /// 검증을 통과해 진행 JSON이 나가는 모양.
        /// </param>
        /// <param name="chapterIds">견본을 세울 챕터 Id들. 비우면 <c>ch05</c> 하나다.</param>
        public TempProject(bool allowUnreachable = false, params string[] chapterIds)
        {
            string[] ids = chapterIds.Length > 0 ? chapterIds : [SampleChapters.Corridor];

            ManifestPath = SampleProject.Create(
                _directory,
                "검증 보고",
                ids.Select(id => (SampleChapters.Corridor, id)),
                project =>
                {
                    // 실제 프로젝트의 모양 — 시나리오 파일이 하나는 있다.
                    project.Files.Add(new StoryFile("sf_main", "본편", "story/main.vnstory.json"));

                    if (allowUnreachable)
                    {
                        var editor = new ProjectEditor(project);

                        foreach (string id in ids)
                        {
                            editor.UpdateEpisode(id, "branch05.02A", allowUnreachable: true);
                        }
                    }
                });
        }

        public string ManifestPath { get; }

        public string ExportFolder => Path.Combine(_directory, ChapterExportService.ExportFolderName);

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
