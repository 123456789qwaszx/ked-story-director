using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>작가용 글 서식</b> (2026-11-22 소유자 · <c>docs/plans/writer-formatting.md</c>) — 작가가 보기 위한 굵게 · 기울임 · 취소선 · 색 · 글꼴 · 크기 · 정렬.
/// 대사 글 밖(<see cref="DialogueNode.WriterMarks"/>)에 서고, 글이 바뀌면 같은 글자를 따라가며, <b>내보내기에 나가지 않는다</b>.
/// </summary>
public sealed class WriterMarksTests
{
    private static readonly WriterLook Bold = new(Bold: true);

    // ── 구간 셈 ──────────────────────────────────────────

    [Fact]
    public void 겹쳐_입히면_쪼개지고_이웃한_같은_모양은_합쳐진다()
    {
        List<WriterSpan> bold = WriterMarkMath.Apply([], 10, 2, 6, look => look with { Bold = true });
        Assert.Equal([new WriterSpan(2, 4, Bold)], bold);

        // 가운데에 색을 더하면 셋으로 쪼개진다.
        List<WriterSpan> colored = WriterMarkMath.Apply(bold, 10, 3, 5, look => look with { Color = WriterColor.Red });
        Assert.Equal(
            [new WriterSpan(2, 1, Bold), new WriterSpan(3, 2, Bold with { Color = WriterColor.Red }), new WriterSpan(5, 1, Bold)],
            colored);

        // 색을 걷으면 다시 하나다.
        Assert.Equal([new WriterSpan(2, 4, Bold)], WriterMarkMath.Apply(colored, 10, 0, 10, look => look with { Color = null }));

        // 글 밖 · 겹침 · 빈 구간은 정규형에서 사라진다.
        Assert.Equal(
            [new WriterSpan(0, 2, Bold)],
            WriterMarkMath.Normalize([new WriterSpan(-2, 3, Bold), new WriterSpan(1, 0, Bold), new WriterSpan(1, 2, Bold), new WriterSpan(2, 9, WriterLook.Plain)], 5));
    }

    [Theory]
    // 앞에 넣기 — 구간이 민다.
    [InlineData("0123456789", "xx0123456789", 2, 3, 4, 3)]
    // 구간 끝에 바로 이어 치기 — 구간이 늘어난다(앞 글자의 모양을 잇는다).
    [InlineData("0123456789", "01234xx56789", 2, 3, 2, 5)]
    // 구간 앞에 바로 치기 — 늘지 않고 민다.
    [InlineData("0123456789", "01xx23456789", 2, 3, 4, 3)]
    // 구간 안에 쓰기 — 늘어난다.
    [InlineData("0123456789", "012xx3456789", 2, 3, 2, 5)]
    public void 글이_바뀌면_구간이_같은_글자를_따라간다(string before, string after, int start, int length, int expectedStart, int expectedLength) =>
        Assert.Equal([new WriterSpan(expectedStart, expectedLength, Bold)], WriterMarkMath.Rebase([new WriterSpan(start, length, Bold)], before, after));

    [Fact]
    public void 걸쳐_지우면_바깥_조각만_남는다()
    {
        // 「234」가 굵다 — 「34567」을 지우면 「2」만 남는다.
        Assert.Equal([new WriterSpan(2, 1, Bold)], WriterMarkMath.Rebase([new WriterSpan(2, 3, Bold)], "0123456789", "01289"));

        // 「234」가 굵다 — 「012」를 「ab」로 바꾸면 「34」만 남고 앞으로 당겨진다.
        Assert.Equal([new WriterSpan(2, 2, Bold)], WriterMarkMath.Rebase([new WriterSpan(2, 3, Bold)], "0123456789", "ab3456789"));

        // 「234」를 통째로 지우면 사라진다.
        Assert.Empty(WriterMarkMath.Rebase([new WriterSpan(2, 3, Bold)], "0123456789", "0156789"));

        // 굵은 「234」를 통째로 「XY」로 고쳐 쓰면 그 모양을 잇는다(구간 안에서 고쳐 썼다).
        Assert.Equal([new WriterSpan(2, 2, Bold)], WriterMarkMath.Rebase([new WriterSpan(2, 3, Bold)], "0123456789", "01XY56789"));
    }

    [Fact]
    public void 넣기_전에_두_곳을_고쳐도_사이의_서식은_그대로다()
    {
        // 「긴 하루」가 굵다 — 앞에 「아, 」를 넣고 「하루」 끝에 「들」을 이어 친 것이 한 번에 들어왔다.
        Assert.Equal(
            [new WriterSpan(7, 5, Bold)],
            WriterMarkMath.Rebase([new WriterSpan(4, 4, Bold)], "오늘은 긴 하루였어", "아, 오늘은 긴 하루들였어"));

        // 같은 글자가 이어지는 곳 — 「ㅋㅋ」 끝에 「ㅋ」를 더하면 굵은 「ㅋㅋ」가 이어 굵다.
        Assert.Equal([new WriterSpan(2, 3, Bold)], WriterMarkMath.Rebase([new WriterSpan(2, 2, Bold)], "아아ㅋㅋ", "아아ㅋㅋㅋ"));
    }

    // ── 편집기 ──────────────────────────────────────────

    [Fact]
    public void 굵게는_모두_굵으면_끄고_섞였으면_모두_켜며_여러_줄도_되돌리기_한_칸이다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "지문 한 줄");
        int notified = 0;
        editor.Changed += (_, args) =>
        {
            notified++;
            Assert.Equal(ProjectChangeKind.WriterMarks, args.Kind);
            Assert.Equal(card.Id, args.NodeId);
        };

        // 「긴 하루」만 굵게 — 화자는 글 밖이라 자리는 대사 글 기준이다(「오늘은 긴 하루였어」의 4..8).
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 4, 4)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        Assert.Equal([new WriterSpan(4, 4, Bold)], Marks(editor, card)[ids[0]]);

        // 섞인 범위(굵은 「긴 하루」 + 굵지 않은 둘째 줄) — 모두 켠다. 두 줄이 한 칸이다.
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 8), new WriterRange(ids[1], 0, 2)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        Assert.Equal([new WriterSpan(0, 8, Bold)], Marks(editor, card)[ids[0]]);
        Assert.Equal([new WriterSpan(0, 2, Bold)], Marks(editor, card)[ids[1]]);
        Assert.Equal(2, notified);

        // 모두 굵다 — 끈다. 줄의 서식이 비면 칸도 사라진다.
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[1], 0, 2)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        Assert.False(Marks(editor, card).ContainsKey(ids[1]));

        // 되돌리기 한 번이면 두 줄이 함께 돌아온다.
        editor.Undo();
        editor.Undo();
        Assert.Equal([new WriterSpan(4, 4, Bold)], Marks(editor, card)[ids[0]]);
        Assert.False(Marks(editor, card).ContainsKey(ids[1]));
    }

    [Fact]
    public void 색_글꼴_크기_정렬을_입히고_바뀐_것이_없으면_적지_않는다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어");
        int notified = 0;
        editor.Changed += (_, _) => notified++;

        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Color, Color: WriterColor.Red));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Fill, Color: WriterColor.Yellow));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Font, Font: "Noto Serif"));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Grow, Step: 2));
        editor.SetWriterAlign(card.Id, WriterAlign.Center);

        Assert.Equal(
            [new WriterSpan(0, 3, new WriterLook(Color: WriterColor.Red, Fill: WriterColor.Yellow, Font: "Noto Serif", Grow: 2))],
            Marks(editor, card)[ids[0]]);
        Assert.Equal(WriterAlign.Center, editor.Project.FindDialogue(card.Id)!.WriterAlign);
        Assert.Equal(5, notified);

        // 같은 색 · 같은 정렬 · 빈 범위 · 은퇴한 줄 — 적지 않는다.
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Color, Color: WriterColor.Red));
        editor.SetWriterAlign(card.Id, WriterAlign.Center);
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 2, 0)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        editor.ApplyWriterStyle(card.Id, [new WriterRange("없는 줄", 0, 3)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        Assert.Equal(5, notified);

        // 크기는 끝에서 멈춘다.
        for (int step = 0; step < 50; step++)
        {
            editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Grow, Step: 1));
        }

        Assert.Equal(WriterMarkMath.MaxGrow, Marks(editor, card)[ids[0]][0].Look.Grow);
    }

    [Fact]
    public void 글_칸에서_고쳐도_곁기둥에서_고쳐도_서식이_같은_글자를_따라간다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "둘째 줄");
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 4, 4)], WriterStyleChange.Toggle(WriterStyleKind.Bold));

        // 글 칸 — 앞에 「아,」를 더하고 줄도 하나 끼운다. 「긴 하루」가 그대로 굵다.
        string shown = WorkingDialoguePreviewText(editor, card);
        string edited = shown.Replace("오늘은", "아, 오늘은", StringComparison.Ordinal) + "\n새 줄";
        Assert.True(editor.ApplyScenarioText(card.Id, CarryIds(editor, card, edited), strict: true).Applied);
        Assert.Equal("아, 오늘은 긴 하루였어", editor.Project.FindDialogue(card.Id)!.Script.Text(ids[0]).Text);
        Assert.Equal([new WriterSpan(7, 4, Bold)], Marks(editor, card)[ids[0]]);

        // 곁기둥 — 줄 표에서 고친다. 「긴」 앞에 바로 친 「아주 」는 구간 앞이라 모양을 잇지 않고 민다, 「하루」 안에 친 「의」는 잇는다.
        editor.SetScriptLineText(card.Id, ids[0], "하린", "아, 오늘은 아주 긴 하루였어");
        Assert.Equal([new WriterSpan(10, 4, Bold)], Marks(editor, card)[ids[0]]);
        editor.SetScriptLineText(card.Id, ids[0], "하린", "아, 오늘은 아주 긴 하의루였어");
        Assert.Equal([new WriterSpan(10, 5, Bold)], Marks(editor, card)[ids[0]]);

        // 다른 언어의 글을 고쳐도 서식은 그대로다.
        editor.SetScriptLineText(card.Id, ids[0], "Harin", "What a long day", locale: "en-US");
        Assert.Equal([new WriterSpan(10, 5, Bold)], Marks(editor, card)[ids[0]]);

        // 화자 이름을 바꿔도(화자는 글 밖) 그대로다.
        editor.RenameSpeaker("하린", "하린2");
        Assert.Equal([new WriterSpan(10, 5, Bold)], Marks(editor, card)[ids[0]]);

        // 되돌리기는 글과 서식을 함께 돌린다.
        editor.Undo();
        editor.Undo();
        editor.Undo();
        editor.Undo();
        Assert.Equal("아, 오늘은 긴 하루였어", editor.Project.FindDialogue(card.Id)!.Script.Text(ids[0]).Text);
        Assert.Equal([new WriterSpan(7, 4, Bold)], Marks(editor, card)[ids[0]]);
    }

    // ── 저장 ──────────────────────────────────────────

    [Fact]
    public void 서식은_있을_때만_저장되고_다시_읽으면_그대로다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어");
        string plain = ProjectSnapshotCodec.Encode(editor.Project);
        Assert.DoesNotContain("writerMarks", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("writerAlign", plain, StringComparison.Ordinal);

        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 4, 4)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Color, Color: WriterColor.Blue));
        editor.SetWriterAlign(card.Id, WriterAlign.Right);

        StoryProject reloaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project));
        DialogueNode again = reloaded.FindDialogue(card.Id)!;
        Assert.Equal(Marks(editor, card)[ids[0]], again.WriterMarks[ids[0]]);
        Assert.Equal(WriterAlign.Right, again.WriterAlign);
        Assert.Equal(ProjectSnapshotCodec.Encode(editor.Project), ProjectSnapshotCodec.Encode(reloaded));
    }

    [Fact]
    public void 이상한_서식이_든_파일도_열리고_정규형으로_읽힌다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어");
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 2)], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        string good = ProjectSnapshotCodec.Encode(editor.Project);

        // 모르는 색 · 글 밖 · 겹침 · 숫자가 아닌 자리 · 배열이 아닌 줄 · 모르는 정렬.
        string broken = ReplaceObject(
            good, "\"writerMarks\"",
            $"{{\"{ids[0]}\": [ {{\"start\": 0, \"length\": 3, \"bold\": true, \"color\": \"mauve\"}}, {{\"start\": 2, \"length\": 99, \"italic\": true}}, " +
            "{\"start\": \"x\", \"length\": 1}, {\"start\": 1, \"length\": -4} ], \"없는줄\": 3 }, \"writerAlign\": \"diagonal\"");

        StoryProject opened = ProjectSnapshotCodec.Decode(broken);
        DialogueNode node = opened.FindDialogue(card.Id)!;
        int length = node.Script.Text(ids[0]).Text.Length;

        Assert.Equal(
            [new WriterSpan(0, 2, Bold), new WriterSpan(2, length - 2, new WriterLook(Italic: true))],
            node.WriterMarks[ids[0]]);
        Assert.Single(node.WriterMarks);
        Assert.Equal(WriterAlign.Left, node.WriterAlign);
    }

    [Fact]
    public void 서식을_잔뜩_입혀도_내보낸_대사_Yarn과_진행_JSON은_바이트까지_같다()
    {
        string root = Path.Combine(Path.GetTempPath(), "vn-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            string manifest = Path.Combine(root, "project.vnproject.json");
            var editor = new ProjectEditor(new StoryProject());
            editor.EnsureChapter("ch01");
            editor.AddSpeaker("하린");
            editor.AddEpisode("ch01", "ep01", "첫 에피소드", 0, 0, sceneId: "ep01");
            DialogueNode card = EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!;
            Assert.True(editor.ApplyScenarioText(card.Id, "하린: 오늘은 정말 긴 하루였어\n문이 열린다.\n하린: 들어가자").Applied);

            Dictionary<string, byte[]> plain = Export(editor, manifest);
            Assert.Contains(plain.Keys, path => path.EndsWith(".yarn", StringComparison.Ordinal));
            Assert.Contains(plain.Keys, path => path.EndsWith(".progression.json", StringComparison.Ordinal));

            string[] ids = editor.Project.FindDialogue(card.Id)!.Script.ActiveLines.Select(line => line.Id).ToArray();

            foreach (WriterStyleChange change in new[]
                     {
                         WriterStyleChange.Toggle(WriterStyleKind.Bold), WriterStyleChange.Toggle(WriterStyleKind.Italic),
                         WriterStyleChange.Toggle(WriterStyleKind.Strike), new WriterStyleChange(WriterStyleKind.Color, Color: WriterColor.Red),
                         new WriterStyleChange(WriterStyleKind.Fill, Color: WriterColor.Yellow), new WriterStyleChange(WriterStyleKind.Font, Font: "Noto Serif"),
                         new WriterStyleChange(WriterStyleKind.Grow, Step: 4)
                     })
            {
                editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 2, 5), new WriterRange(ids[1], 0, 3), new WriterRange(ids[2], 0, 4)], change);
            }

            editor.SetWriterAlign(card.Id, WriterAlign.Center);
            Assert.NotEmpty(editor.Project.FindDialogue(card.Id)!.WriterMarks);

            Dictionary<string, byte[]> marked = Export(editor, manifest);
            Assert.Equal(plain.Keys.Order(StringComparer.Ordinal), marked.Keys.Order(StringComparer.Ordinal));

            foreach ((string path, byte[] bytes) in plain)
            {
                Assert.Equal(bytes, marked[path]);
            }

            // 프로젝트 파일에는 서식이 실렸다(내보낸 것에만 없다).
            Assert.Contains("writerMarks", string.Concat(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
                .Where(path => !path.Contains("exported", StringComparison.Ordinal)).Select(File.ReadAllText)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ── 글 칸 한 줄의 대사 자리 ─────────────────────────

    [Theory]
    [InlineData("하린: 오늘은", 4, 3)]
    [InlineData("  하린:   오늘은  ", 8, 3)]
    [InlineData("문이 열린다.", 0, 7)]
    [InlineData("윌로 : 지문이다", 0, 9)]
    [InlineData("늙은 상인: 어서 오게", 7, 5)]
    [InlineData("하린: 시간은 3:00이야", 4, 10)]
    [InlineData("하린: 고친 줄 #line:ln_0001", 4, 4)]
    public void 글_칸_한_줄에서_대사_글의_자리를_파서와_같은_규칙으로_잰다(string row, int start, int length)
    {
        Speaker[] speakers = [new("하린", "harin"), new("늙은 상인", "merchant")];
        Assert.Equal((start, length), ScenarioTextParser.DialogueBodyRange(row, speakers));

        // 파서가 읽는 대사 글과 같은 글자다.
        ScenarioLine parsed = Assert.Single(ScenarioTextParser.Parse(row, speakers).Lines);
        Assert.Equal(parsed.Text, row.Substring(start, length));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<<if $favor >= 5>>")]
    [InlineData("<<endif>>")]
    [InlineData("-> 선택지")]
    [InlineData("[장식]")]
    public void 대사_줄이_아니면_자리가_없다(string row) =>
        Assert.Null(ScenarioTextParser.DialogueBodyRange(row, []));

    [Fact]
    public void 고친_글의_줄마다_잇는_대본_줄을_안다()
    {
        IReadOnlyList<string?> ids = ScenarioLineIdentity.RowLineIds(
            "하린: 첫 줄\n둘째 줄",
            new Dictionary<string, int> { ["a"] = 0, ["b"] = 1 },
            "하린: 첫 줄 고침\n새로 끼운 줄\n\n<<endif>>\n둘째 줄");

        // 고친 구간은 앞에서부터 하나씩 잇는다(글 칸에 넣을 때와 같은 짝) — 빈 줄 · 조건 줄은 대본 줄이 아니다.
        Assert.Equal(["a", null, null, null, "b"], ids);
    }

    // ── 도움 ──────────────────────────────────────────

    /// <summary>그 칸의 값(중괄호로 짝이 맞는 객체)을 통째로 바꾼다.</summary>
    private static string ReplaceObject(string json, string key, string replacement)
    {
        int at = json.IndexOf(key, StringComparison.Ordinal);
        Assert.True(at > 0);
        int open = json.IndexOf('{', at);
        int depth = 0;
        int close = open;

        for (; close < json.Length; close++)
        {
            depth += json[close] switch { '{' => 1, '}' => -1, _ => 0 };

            if (depth == 0)
            {
                break;
            }
        }

        return json[..at] + key + ": " + replacement + json[(close + 1)..];
    }

    /// <summary>저장하고 내보낸다 — exported 아래 파일마다 그 바이트.</summary>
    private static Dictionary<string, byte[]> Export(ProjectEditor editor, string manifest)
    {
        ProjectStore.Save(manifest, editor.Project);
        var entries = new[] { new ChapterEntry("ch01", editor.FindChapter("ch01")!.ToGraphModel("ch01")) };
        Assert.True(new ChapterExportService().ExportAll(entries, manifest, editor.Project).AllExported);
        string exported = Path.Combine(Path.GetDirectoryName(manifest)!, "exported");

        return Directory.GetFiles(exported, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(exported, path), File.ReadAllBytes, StringComparer.Ordinal);
    }

    private static (ProjectEditor Editor, DialogueNode Card, string[] Ids) Stage(params string[] lines)
    {
        var sample = new Sample();
        sample.Editor.AddSpeaker("하린");
        Assert.True(sample.Editor.ApplyScenarioText(sample.Dialogue.Id, string.Join('\n', lines)).Applied);
        string[] ids = sample.Script.ActiveLines.Select(line => line.Id).ToArray();
        return (sample.Editor, sample.Dialogue, ids);
    }

    private static Dictionary<string, IReadOnlyList<WriterSpan>> Marks(ProjectEditor editor, DialogueNode card) =>
        editor.Project.FindDialogue(card.Id)!.WriterMarks;

    private static string WorkingDialoguePreviewText(ProjectEditor editor, DialogueNode card) =>
        Vn.Authoring.Rendering.WorkingDialoguePreview.ScenarioText(editor.Project, card.Id);

    private static string CarryIds(ProjectEditor editor, DialogueNode card, string edited)
    {
        ScenarioWritingLayout layout = Vn.Authoring.Rendering.WorkingDialoguePreview.ScenarioWriting(editor.Project, card.Id);
        return ScenarioLineIdentity.CarryLineIds(layout.Text, layout.RowOfLine, edited);
    }
}
