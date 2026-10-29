using System.Text;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Definition;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// 넘겨줄 <b>씨앗 프로젝트</b> (2026-08-17 소유자 — 비개발자 인계).
///
/// 빈 exe만 주면 첫 화면이 막다른 길이다: 새로 만든 `game.definition.json`은 `variables`가
/// 비어 있어 <b>스탯이 하나도 없고</b>, 스탯이 없으면 조건을 못 만들고, 조건이 없으면 간선의
/// 관문을 못 쓴다 — 분기 있는 이야기를 아예 시작할 수 없다.
///
/// 그래서 <b>이미 돌아가는 최소 한 판</b>을 함께 준다. 켜서 열면 분기 하나가 실제로 서 있고,
/// 그것을 고쳐 나가면 된다.
///
/// <b>이 테스트가 곧 씨앗의 정의다.</b> 만드는 법과 "오류 0"이 한자리에 있어서, 규격이 바뀌면
/// 여기가 먼저 빨개진다 — 손으로 만든 폴더를 저장소에 넣어 두면 조용히 낡는다.
/// </summary>
public sealed class StarterProjectTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-starter", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 씨앗_프로젝트는_오류_없이_열리고_분기가_서_있다() => HeadlessUi.Run(() =>
    {
        string manifest = StarterProject.Create(_directory);

        var session = new AuthoringSession();
        session.Open(manifest);

        // 스탯이 있다 — 여기서 막히면 조건도 관문도 못 쓴다(빈 프로젝트의 막다른 길).
        Assert.Contains("trust", session.Definition.Variables.Select(item => item.Name));

        // 받는 사람이 처음 여는 것은 폴더다 — 무엇부터 열지 적힌 한 장이 같이 있다.
        Assert.True(File.Exists(Path.Combine(_directory, "읽어주세요.txt")));

        ChapterGraphModel chapter = session.Editor.FindChapter("ch01")!.ToGraphModel("ch01", session.Definition);

        Assert.DoesNotContain(chapter.Diagnostics, item =>
            item.Severity == ChapterDiagnosticSeverity.Error);

        // 분기 하나가 실제로 서 있다 — 문구 둘이 갈라졌다 다시 만난다.
        Assert.Equal(4, chapter.Episodes.Count);
        Assert.Equal(
            ["라루를 믿는다", "문을 연다", "문을 연다", "혼자 간다"],
            chapter.Edges.Where(edge => !edge.HasNoOptionLabel)
                .Select(edge => edge.OptionLabel!)
                .Order(StringComparer.Ordinal));

        // 도달성 증명이 통과한다 — 닿을 수 없는 에피소드가 없다.
        ChapterValidationResult validation = ChapterValidator.Validate(chapter);

        Assert.False(validation.HasErrors, string.Join("\n", validation.All.Select(item => item.Message)));
    });

    [Fact]
    public void 씨앗의_카드_대본에_대사가_서_있다() => HeadlessUi.Run(() =>
    {
        // 대사는 연출 그래프의 카드 안에 산다 — 에피소드마다 카드가 하나, 열면 쓸 자리가 이미 있다.
        string manifest = StarterProject.Create(_directory);

        var session = new AuthoringSession();
        session.Open(manifest);

        foreach (string episode in (string[])["시작", "믿는길", "혼자길", "끝"])
        {
            Assert.NotEmpty(Lines(session.Project, episode));
        }

        Assert.Equal(
            [
                new LocalizedLine("라루", "고맙다는 말은 안 할래."),
                new LocalizedLine("윌로", "알아. 너답네."),
                new LocalizedLine("라루", "가자.")
            ],
            Lines(session.Project, "믿는길"));

        // 씨앗을 심는 손이 대신 쓴 것이라도 열자마자 "고쳤다" 표시가 뜨면 안 된다 — 카드가 이미
        // 서 있으니 열 때 맞출 것이 없다.
        Assert.False(session.IsDirty);
    });

    [Fact]
    public void 씨앗을_열면_진행_JSON이_저절로_나간다() => HeadlessUi.Run(() =>
    {
        // 검증을 통과하는 판이라는 뜻이다 — 받는 사람의 첫 화면에 빨간 것이 없다.
        string manifest = StarterProject.Create(_directory);

        var session = new AuthoringSession();
        session.Open(manifest);

        ChapterExportResult result = ChapterProgressionExporter.Export(
            session.Editor.FindChapter("ch01")!.ToGraphModel("ch01"));

        Assert.False(result.Refused, string.Join("\n", result.Validation.All.Select(item => item.Message)));
        Assert.Contains("\"StartEpisodeId\": \"시작\"", result.Json!, StringComparison.Ordinal);
    });

    /// <summary>그 에피소드 카드의 대본 — 살아 있는 줄의 화자·대사.</summary>
    private static List<LocalizedLine> Lines(StoryProject project, string episodeId)
    {
        DialogueNode card = EpisodeNaming.CardFor(project, "ch01", episodeId)!;
        ScriptDocument script = card.Script;

        return script.ActiveLines.Select(line => script.Text(line.Id)).ToList();
    }
}

/// <summary>
/// 씨앗 한 판을 만든다 — <b>사람이 툴에서 하는 순서 그대로</b> 진짜 편집 명령을 지난다.
/// 그래서 규격이 바뀌면 여기도 같이 깨지고, 손으로 만든 폴더처럼 조용히 낡지 않는다.
/// </summary>
internal static class StarterProject
{
    /// <returns>만들어진 프로젝트 매니페스트 경로.</returns>
    public static string Create(string folder)
    {
        Directory.CreateDirectory(folder);

        string manifest = Path.Combine(folder, "예제" + ProjectManifestJson.FileExtension);

        // ① 프로젝트 — 첫 저장이 assets 폴더와 기본 튜닝, 빈 정의 파일까지 준비한다.
        var session = new AuthoringSession();
        session.Save(manifest);

        // ② 스탯 — 정의 파일이 어휘의 원천이다(검증 보고의 [등록]과 같은 길).
        //    여기가 비면 조건도 관문도 못 만든다.
        session.RegisterVariables(
        [
            new VariableSpec { Name = "trust", Type = "number", Description = "신뢰" },
            new VariableSpec { Name = "fatigue", Type = "number", Description = "피로" }
        ]);

        // 화자는 챕터가 아니라 프로젝트의 것이다 (2026-08-23) — 챕터 그래프 [화자] 탭이 쓰는
        // 그 배열에 그대로 적는다.
        session.SaveSpeakers(
        [
            new SpeakerSpec { Name = "윌로", CharacterId = "willo" },
            new SpeakerSpec { Name = "라루", CharacterId = "laru" }
        ]);

        // ③ 챕터 — [＋ 챕터]가 스탯을 정의 파일에서 깐다. 표시이름은 [챕터] 탭에서 고친다.
        string? refused = session.CreateChapter("ch01");

        if (refused is not null)
        {
            throw new InvalidOperationException(refused);
        }

        ProjectEditor editor = session.Editor;
        editor.UpdateChapterStat("ch01", "trust", displayName: "신뢰");
        editor.UpdateChapterStat("ch01", "fatigue", displayName: "피로");
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 2", "라루를 믿기로 했다면");

        // ④ 에피소드 넷 — 갈라졌다 다시 만나는 최소 모양. 에피소드마다 연출 그래프에 카드가 함께 선다.
        editor.AddEpisode("ch01", "시작", "복도", 0, 0);
        editor.AddEpisode("ch01", "믿는길", "함께 간다", 1, 0);
        editor.AddEpisode("ch01", "혼자길", "혼자 간다", 1, 1);
        editor.AddEpisode("ch01", "끝", "문 앞", 2, 0);

        // ⑤ 길 — 문구가 곧 선택지 버튼이다.
        editor.AddEdge("ch01", "시작", "믿는길", optionLabel: "라루를 믿는다", statChanges: "trust +2");
        editor.AddEdge("ch01", "시작", "혼자길", optionLabel: "혼자 간다", statChanges: "fatigue +1");
        // v12 — 문구 없는 길은 폐지됐다. 넘어가기만 하는 자리도 버튼 이름을 갖는다.
        editor.AddEdge("ch01", "믿는길", "끝", optionLabel: "문을 연다");
        editor.AddEdge("ch01", "혼자길", "끝", optionLabel: "문을 연다");

        // ⑥ 대사 — 카드의 대본에 쓴다. 대사는 사람이 쓰는 것이라 여기서는 씨앗을 심는 손이 대신 쓴다.
        //    갈래는 대사가 아니라 챕터 간선이 그린다(위 ⑤).
        Write(session, "시작", ("윌로", "복도는 조용했다."), ("라루", "같이 갈까?"));
        Write(session, "믿는길", ("라루", "고맙다는 말은 안 할래."), ("윌로", "알아. 너답네."), ("라루", "가자."));
        Write(session, "혼자길", ("윌로", "혼자 걷는 복도는 길었다."));
        Write(session, "끝", ("윌로", "문이 열렸다."));

        session.Save();

        // ⑦ 받는 사람이 처음 여는 것은 exe가 아니라 폴더다 — 무엇부터 열지 한 장으로 적어 둔다.
        File.WriteAllText(Path.Combine(folder, "읽어주세요.txt"), Guide, Encoding.UTF8);

        return manifest;
    }

    /// <summary>
    /// 그 에피소드 카드의 대본에 줄을 채운다 — [대사 편집]에서 사람이 하는 그대로다: 새 카드에 서
    /// 있는 빈 줄에 먼저 쓰고, 모자라면 줄을 더해 쓴다.
    /// </summary>
    private static void Write(AuthoringSession session, string episodeId, params (string Speaker, string Text)[] lines)
    {
        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", episodeId)
            ?? throw new InvalidOperationException($"에피소드 '{episodeId}'에 카드가 없습니다.");

        ProjectEditor editor = session.Editor;
        ScriptDocument script = card.Script;

        for (int index = 0; index < lines.Length; index++)
        {
            string lineId = script.ActiveLines.ElementAtOrDefault(index)?.Id
                ?? editor.InsertScriptLine(card.Id).Id;

            editor.SetScriptLineText(card.Id, lineId, lines[index].Speaker, lines[index].Text);
        }
    }

    /// <summary>폴더를 처음 연 사람에게 주는 한 장 — 무엇부터 열고, 무엇을 만지는지.</summary>
    private const string Guide = """
        예제 프로젝트 — 돌아가는 최소 한 판

        [처음 할 일]
        1. Vn.App.exe 를 켠다.
        2. 이 폴더의 `예제.vnproject.json` 을 연다.
        3. 챕터 그래프 탭에서 ch01 을 고른다. 네모 넷과 그것을 잇는 길이 보인다.

        [무엇이 들어 있나]
          시작 ─(라루를 믿는다 · 신뢰 +2)→ 믿는길 ─→ 끝
            └─(혼자 간다 · 피로 +1)→ 혼자길 ─┘
          갈라졌다 다시 만나는 최소 모양이다. 이 위에 덧그리면 된다.

        [무엇이 어디 있나]
          예제.vnproject.json  프로젝트 — 이것을 연다. 챕터(스탯 · 조건 · 에피소드 · 길)가 여기 있다
          story/               연출 그래프의 판과 카드(카드의 대사째) — 툴이 쓰고 읽는다
          game.definition.json 스탯 이름(trust · fatigue)과 화자(윌로 · 라루)의 원천
          assets/portraits/    표정 그림을 넣는 곳 (지금은 비어 있다)
          exported/            툴이 내는 진행 JSON — 손으로 고치지 않는다

        [챕터는 챕터 그래프에서 고친다]
        에피소드와 길은 판 위에서 만들고 잇는다. 길을 고르면 오른쪽 [편집] 탭에서
        선택지 문구 · 스탯변화 · 표시조건 · 해금조건을 적는다. 조건과 스탯은
        [챕터] 탭, 화자는 [화자] 탭에서 더하고 고친다.

        [대사는 연출 그래프의 카드에 쓴다]
        에피소드마다 연출 그래프에 카드가 하나 서 있다. 카드를 고르면 아래
        [대사 편집]에서 줄마다 화자와 대사를 쓴다. 갈래는 대사가 아니라 챕터의 길이
        그린다 — 진행 그래프가 볼 수 있는 자리에 갈래를 두어야 "닿을 수 없는
        에피소드"를 툴이 짚어 줄 수 있다.
        """;
}
