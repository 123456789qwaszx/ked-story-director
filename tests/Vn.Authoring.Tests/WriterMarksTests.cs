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
    private static readonly WriterRgb Red = new(0xD3, 0x2F, 0x2F);
    private static readonly WriterRgb Yellow = new(0xFF, 0xF5, 0x9D);
    private static readonly WriterRgb Blue = new(0x15, 0x65, 0xC0);

    private static readonly WriterLook Bold = new(Bold: true);

    // ── 구간 셈 ──────────────────────────────────────────

    [Fact]
    public void 겹쳐_입히면_쪼개지고_이웃한_같은_모양은_합쳐진다()
    {
        List<WriterSpan> bold = WriterMarkMath.Apply([], 10, 2, 6, look => look with { Bold = true });
        Assert.Equal([new WriterSpan(2, 4, Bold)], bold);

        // 가운데에 색을 더하면 셋으로 쪼개진다.
        List<WriterSpan> colored = WriterMarkMath.Apply(bold, 10, 3, 5, look => look with { Color = Red });
        Assert.Equal(
            [new WriterSpan(2, 1, Bold), new WriterSpan(3, 2, Bold with { Color = Red }), new WriterSpan(5, 1, Bold)],
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

        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Color, Color: Red));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Fill, Color: Yellow));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Font, Font: "Noto Serif"));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Grow, Step: 2));
        editor.SetWriterAlign(card.Id, WriterAlign.Center);

        Assert.Equal(
            [new WriterSpan(0, 3, new WriterLook(Color: Red, Fill: Yellow, Font: "Noto Serif", Grow: 2))],
            Marks(editor, card)[ids[0]]);
        Assert.Equal(WriterAlign.Center, editor.Project.FindDialogue(card.Id)!.WriterAlign);
        Assert.Equal(5, notified);

        // 같은 색 · 같은 정렬 · 빈 범위 · 은퇴한 줄 — 적지 않는다.
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Color, Color: Red));
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

    /// <summary>
    /// 2026-11-23 소유자 — 「정확히 텍스트 입력을 해서 특정값으로 변경하는 것도 필요하겠습니다」(WF-D20). 크기 차가 섞인 글자들도 한 번에 그 값이 된다 — 다른
    /// 모양은 그대로, 끝으로 당기고, 0이면 크기 차가 걷힌다. 되돌리기 한 칸이다.
    /// </summary>
    [Fact]
    public void 크기를_정한_값으로_맞추면_섞인_크기가_그_값이_되고_끝으로_당긴다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어");
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 2)], new WriterStyleChange(WriterStyleKind.Grow, Step: 2));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 2, 2)], WriterStyleChange.Toggle(WriterStyleKind.Bold));

        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 4)], new WriterStyleChange(WriterStyleKind.Size, Step: 5));
        Assert.Equal(
            [new WriterSpan(0, 2, new WriterLook(Grow: 5)), new WriterSpan(2, 2, new WriterLook(Bold: true, Grow: 5))],
            Marks(editor, card)[ids[0]]);

        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 4)], new WriterStyleChange(WriterStyleKind.Size, Step: 500));
        Assert.All(Marks(editor, card)[ids[0]], span => Assert.Equal(WriterMarkMath.MaxGrow, span.Look.Grow));

        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 4)], new WriterStyleChange(WriterStyleKind.Size, Step: 0));
        Assert.Equal([new WriterSpan(2, 2, new WriterLook(Bold: true))], Marks(editor, card)[ids[0]]);

        editor.Undo();
        Assert.All(Marks(editor, card)[ids[0]], span => Assert.Equal(WriterMarkMath.MaxGrow, span.Look.Grow));
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
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Color, Color: Blue));
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
                         WriterStyleChange.Toggle(WriterStyleKind.Strike), new WriterStyleChange(WriterStyleKind.Color, Color: Red),
                         new WriterStyleChange(WriterStyleKind.Fill, Color: Yellow), new WriterStyleChange(WriterStyleKind.Font, Font: "Noto Serif"),
                         new WriterStyleChange(WriterStyleKind.Grow, Step: 4)
                     })
            {
                editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 2, 5), new WriterRange(ids[1], 0, 3), new WriterRange(ids[2], 0, 4)], change);
            }

            editor.SetWriterAlign(card.Id, WriterAlign.Center);
            editor.Project.FindDialogue(card.Id)!.WriterBase = new WriterBase("Noto Serif", 17);   // 카드의 처음 글꼴 · 크기도
            Assert.True(editor.SetSpeakerColor("하린", new WriterRgb(0xAA, 0x33, 0x00)));          // 화자 이름 색도(WF-D15)
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

    // ── 색(WF-D14) ──────────────────────────────────────

    [Fact]
    public void 색은_RGB로_적고_옛_색_이름은_정해_둔_그_색으로_읽는다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 긴 하루였어");
        var custom = new WriterRgb(0x12, 0xAB, 0x34);
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Color, Color: custom));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 3)], new WriterStyleChange(WriterStyleKind.Fill, Color: Yellow));
        string saved = ProjectSnapshotCodec.Encode(editor.Project);
        Assert.Contains("\"#12AB34\"", saved, StringComparison.Ordinal);
        Assert.Equal(new WriterLook(Color: custom, Fill: Yellow), ProjectSnapshotCodec.Decode(saved).FindDialogue(card.Id)!.WriterMarks[ids[0]].Single().Look);

        // 옛 파일(WF-D14 전)의 색 이름 — 글자색은 진한 그 색, 바탕색은 옅은 그 색. 16진수는 대소문자 · # 없이도 읽고, 모르는 것은 버린다.
        foreach ((string color, string fill, WriterLook expected) in new[]
                 {
                     ("red", "yellow", new WriterLook(Color: WriterSwatch.All[0].Text, Fill: WriterSwatch.All[2].Fill)),
                     ("#12ab34", "12AB34", new WriterLook(Color: custom, Fill: custom)),
                     ("#12AB3", "mauve", WriterLook.Plain)
                 })
        {
            string old = ReplaceObject(saved, "\"writerMarks\"",
                $"{{\"{ids[0]}\": [ {{\"start\": 0, \"length\": 3, \"color\": \"{color}\", \"fill\": \"{fill}\"}} ] }}");
            DialogueNode read = ProjectSnapshotCodec.Decode(old).FindDialogue(card.Id)!;
            Assert.Equal(expected, read.WriterMarks.TryGetValue(ids[0], out IReadOnlyList<WriterSpan>? spans) ? spans.Single().Look : WriterLook.Plain);
        }
    }

    [Fact]
    public void 이_프로젝트에_쓴_색은_글자색과_바탕색을_따로_많이_쓴_차례로_센다()
    {
        (ProjectEditor editor, DialogueNode card, string[] ids) = Stage("하린: 오늘은 정말 긴 하루였어");
        var green = new WriterRgb(0, 0x80, 0);
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 2)], new WriterStyleChange(WriterStyleKind.Color, Color: Blue));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 3, 6)], new WriterStyleChange(WriterStyleKind.Color, Color: green));
        editor.ApplyWriterStyle(card.Id, [new WriterRange(ids[0], 0, 4)], new WriterStyleChange(WriterStyleKind.Fill, Color: Yellow));

        IReadOnlyList<DialogueNode> nodes = [editor.Project.FindDialogue(card.Id)!];
        Assert.Equal([green, Blue], WriterMarkMath.UsedColors(nodes, fill: false));
        Assert.Equal([Yellow], WriterMarkMath.UsedColors(nodes, fill: true));
    }

    // ── 화자 이름 색(WF-D15) ────────────────────────────

    [Fact]
    public void 화자_이름_색은_정했을_때만_저장되고_되돌리기_한_칸이다()
    {
        (ProjectEditor editor, _, _) = Stage("하린: 오늘은 긴 하루였어");
        Assert.DoesNotContain("\"color\"", ProjectSnapshotCodec.Encode(editor.Project), StringComparison.Ordinal);

        var color = new WriterRgb(0xAA, 0x33, 0x00);
        Assert.True(editor.SetSpeakerColor("하린", color));
        Assert.False(editor.SetSpeakerColor("하린", color));   // 같은 값은 기록하지 않는다
        Assert.False(editor.SetSpeakerColor("없는 화자", color));
        Assert.Equal(color, ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project)).FindSpeaker("하린")!.Color);

        // 이름을 바꿔도 색은 따라간다. 되돌리면 걷힌다.
        editor.RenameSpeaker("하린", "하린이");
        Assert.Equal(color, editor.Project.FindSpeaker("하린이")!.Color);
        editor.Undo();
        editor.Undo();
        Assert.Null(editor.Project.FindSpeaker("하린")!.Color);

        // 읽지 못하는 색은 색 없이 선다.
        Assert.True(editor.SetSpeakerColor("하린", color));
        string broken = ProjectSnapshotCodec.Encode(editor.Project).Replace("\"#AA3300\"", "\"주황빛\"", StringComparison.Ordinal);
        Assert.Null(ProjectSnapshotCodec.Decode(broken).FindSpeaker("하린")!.Color);
    }

    /// <summary>
    /// 2026-11-23 소유자 — 「화자탭에서 현재는 캐릭터의 글자색만 바꿀 수 있는데, 여기다가 캐릭터의 글자 바탕색도 조정할 수 있게」(WF-D22). 이름 바탕색은 이름
    /// 색과 따로 — 정했을 때만 저장되고, 이름을 바꿔도 따라가고, 되돌리기 한 칸이며, 읽지 못하면 바탕 없이 선다.
    /// </summary>
    [Fact]
    public void 화자_이름_바탕색은_이름_색과_따로_저장되고_되돌리기_한_칸이다()
    {
        (ProjectEditor editor, _, _) = Stage("하린: 오늘은 긴 하루였어");
        var color = new WriterRgb(0xAA, 0x33, 0x00);
        var fill = new WriterRgb(0xFF, 0xF5, 0x9D);
        Assert.True(editor.SetSpeakerColor("하린", color));
        Assert.DoesNotContain("\"fill\"", ProjectSnapshotCodec.Encode(editor.Project), StringComparison.Ordinal);

        Assert.True(editor.SetSpeakerFill("하린", fill));
        Assert.False(editor.SetSpeakerFill("하린", fill));
        Assert.False(editor.SetSpeakerFill("없는 화자", fill));
        Assert.Equal(new Speaker("하린", editor.Project.FindSpeaker("하린")!.CharacterId, color, fill),
            ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project)).FindSpeaker("하린"));

        editor.RenameSpeaker("하린", "하린이");
        Assert.Equal(fill, editor.Project.FindSpeaker("하린이")!.Fill);
        editor.Undo();
        editor.Undo();
        Assert.Equal((color, (WriterRgb?)null), (editor.Project.FindSpeaker("하린")!.Color, editor.Project.FindSpeaker("하린")!.Fill));

        Assert.True(editor.SetSpeakerFill("하린", fill));
        string broken = ProjectSnapshotCodec.Encode(editor.Project).Replace("\"#FFF59D\"", "\"연노랑\"", StringComparison.Ordinal);
        Assert.Null(ProjectSnapshotCodec.Decode(broken).FindSpeaker("하린")!.Fill);
        Assert.Equal(color, ProjectSnapshotCodec.Decode(broken).FindSpeaker("하린")!.Color);
    }

    [Theory]
    [InlineData("하린: 오늘은", 0, 2, "하린")]
    [InlineData("  하린:오늘은", 2, 2, "하린")]
    [InlineData("늙은 상인: 어서 오게", 0, 5, "늙은 상인")]
    public void 글_칸_한_줄에서_화자_이름의_자리를_파서와_같은_규칙으로_잰다(string row, int start, int length, string name) =>
        Assert.Equal((start, length, name), ScenarioTextParser.SpeakerNameRange(row, [new Speaker("하린", "harin"), new Speaker("늙은 상인", "merchant")]));

    [Theory]
    [InlineData("오늘은 맑다")]
    [InlineData("그는 말했다: 가자")]
    [InlineData("<<if $met>>")]
    [InlineData("")]
    public void 화자가_없는_줄은_이름_자리가_없다(string row) =>
        Assert.Null(ScenarioTextParser.SpeakerNameRange(row, [new Speaker("하린", "harin")]));

    // ── 카드의 처음 글꼴 · 크기(WF-D13) ─────────────────

    [Fact]
    public void 처음_글꼴_크기는_적혀_있을_때만_저장되고_다시_읽으면_그대로다()
    {
        (ProjectEditor editor, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어");
        Assert.Null(card.WriterBase);
        Assert.DoesNotContain("writerBase", ProjectSnapshotCodec.Encode(editor.Project), StringComparison.Ordinal);

        card.WriterBase = new WriterBase("맑은 고딕", 14);
        StoryProject reloaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project));
        Assert.Equal(new WriterBase("맑은 고딕", 14), reloaded.FindDialogue(card.Id)!.WriterBase);

        // 기본 글꼴은 빈 글로 적힌다.
        card.WriterBase = new WriterBase(string.Empty, 11);
        Assert.Equal(new WriterBase(string.Empty, 11), ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project)).FindDialogue(card.Id)!.WriterBase);
        Assert.DoesNotContain("\"bold\"", ProjectSnapshotCodec.Encode(editor.Project), StringComparison.Ordinal);

        // 굵게 · 글자색(WF-D16) — 정했을 때만 적는다.
        card.WriterBase = new WriterBase("맑은 고딕", 12, Bold: true, Color: new WriterRgb(0x20, 0x40, 0x80));
        string saved = ProjectSnapshotCodec.Encode(editor.Project);
        Assert.Contains("\"#204080\"", saved, StringComparison.Ordinal);
        Assert.Equal(card.WriterBase, ProjectSnapshotCodec.Decode(saved).FindDialogue(card.Id)!.WriterBase);
    }

    [Theory]
    [InlineData("{\"font\": \"맑은 고딕\", \"size\": \"크다\"}")]
    [InlineData("{\"font\": \"맑은 고딕\", \"size\": 0}")]
    [InlineData("{\"font\": \"맑은 고딕\"}")]
    public void 크기가_이상한_처음_값은_안_적힌_카드로_읽는다(string broken)
    {
        (ProjectEditor editor, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어");
        card.WriterBase = new WriterBase("맑은 고딕", 14);
        StoryProject opened = ProjectSnapshotCodec.Decode(ReplaceObject(ProjectSnapshotCodec.Encode(editor.Project), "\"writerBase\"", broken));
        Assert.Null(opened.FindDialogue(card.Id)!.WriterBase);
    }

    [Fact]
    public void 새_카드는_만들_때_댄_처음_글꼴_크기를_적고_되돌려도_다시_해도_그대로다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        var given = new WriterBase("Noto Serif", 15);
        editor.NewCardWriterBase = () => given;
        editor.AddEpisode("ch01", "ep01", "첫", 0, 0, sceneId: "ep01");
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!;
        Assert.Equal(given, card.WriterBase);

        // 대는 값이 바뀌어도(설정을 바꿨다) 서 있는 카드는 그대로다.
        given = new WriterBase(string.Empty, 20);
        editor.AddEpisode("ch01", "ep02", "둘", 300, 0, sceneId: "ep01");
        Assert.Equal(new WriterBase("Noto Serif", 15), EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!.WriterBase);
        Assert.Equal(new WriterBase(string.Empty, 20), EpisodeNaming.CardFor(editor.Project, "ch01", "ep02")!.WriterBase);

        editor.Undo();
        editor.Redo();
        Assert.Equal(new WriterBase(string.Empty, 20), EpisodeNaming.CardFor(editor.Project, "ch01", "ep02")!.WriterBase);
        Assert.Equal(new WriterBase("Noto Serif", 15), EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!.WriterBase);
    }

    [Fact]
    public void 처음_값이_없는_카드에만_적는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "첫", 0, 0, sceneId: "ep01");
        editor.AddEpisode("ch01", "ep02", "둘", 300, 0, sceneId: "ep01");
        DialogueNode first = EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!;
        DialogueNode second = EpisodeNaming.CardFor(editor.Project, "ch01", "ep02")!;
        first.WriterBase = new WriterBase("Noto Serif", 15);

        Assert.Equal(1, WriterBaseFill.Fill(editor.Project, new WriterBase(string.Empty, 12)));
        Assert.Equal(new WriterBase("Noto Serif", 15), first.WriterBase);
        Assert.Equal(new WriterBase(string.Empty, 12), second.WriterBase);
        Assert.Equal(0, WriterBaseFill.Fill(editor.Project, new WriterBase(string.Empty, 30)));
    }

    // ── 카드 여러 장에 한 번에(WF-D21) ─────────────────────────

    /// <summary>
    /// 2026-11-23 소유자 — 카드만 골랐을 때 서식 도구가 「도구 막대 전부」를 고른 카드들에. 글 전체에 입히는 켜고 끄기는 모든 카드를 하나로 판정하고(처음부터
    /// 굵은 카드는 이미 굵다), 처음 값 · 정렬도 카드 모두에 한 번에 — 되돌리기 한 칸이고 바뀐 것이 없으면 적지 않는다.
    /// </summary>
    [Fact]
    public void 카드_여러_장의_글_전체와_처음_값을_한_번에_바꾸고_되돌리기_한_칸이다()
    {
        (ProjectEditor editor, DialogueNode first, string[] ids) = Stage("하린: 오늘은 긴 하루였어", "문이 열린다.");
        DialogueNode second = editor.AddDialogueNode("sf_test", "둘째", name: "둘째");
        Assert.True(editor.ApplyScenarioText(second.Id, "바람이 분다.").Applied);
        DialogueNode empty = editor.AddDialogueNode("sf_test", "빈", name: "빈");
        first.WriterBase = new WriterBase(string.Empty, 11, Bold: true, LineSpacing: 140);
        second.WriterBase = new WriterBase(string.Empty, 12, LineSpacing: 160);
        editor.ApplyWriterStyle(first.Id, [new WriterRange(ids[0], 0, 2)], WriterStyleChange.Toggle(WriterStyleKind.Italic));
        int changes = 0;
        editor.Changed += (_, change) => changes += change.Kind == ProjectChangeKind.WriterMarks ? 1 : 0;

        // 굵게 — 처음부터 굵은 카드는 이미 굵다고 세므로, 둘째 카드만 굵지 않아 모두 켠다. 빈 카드는 건너뛴다.
        editor.ApplyWriterStyleToCards([first.Id, second.Id, empty.Id], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        Assert.Equal(1, changes);
        Assert.True(WriterMarkMath.IsAll(Marks(editor, second)[second.Script.ActiveLines.Single().Id], 6, 0, 6, look => look.Bold));
        Assert.Equal(new WriterLook(Bold: true, Italic: true), Marks(editor, first)[ids[0]][0].Look);   // 기울임은 그대로

        // 다시 누르면 모두 굵다 — 끈다(처음부터 굵은 카드의 바탕 굵기는 그대로).
        editor.ApplyWriterStyleToCards([first.Id, second.Id], WriterStyleChange.Toggle(WriterStyleKind.Bold));
        Assert.DoesNotContain(Marks(editor, second).Values.SelectMany(spans => spans), span => span.Look.Bold);
        Assert.True(editor.Project.FindDialogue(first.Id)!.WriterBase!.Bold);

        // 처음 값 · 정렬 — 카드 모두에, 한 칸씩.
        editor.SetWriterBase([first.Id, second.Id], basis => basis with { Size = 16 });
        editor.SetWriterAlign([first.Id, second.Id], WriterAlign.Right);
        Assert.Equal((16d, 16d), (first.WriterBase!.Size, editor.Project.FindDialogue(second.Id)!.WriterBase!.Size));
        Assert.Equal(160, editor.Project.FindDialogue(second.Id)!.WriterBase!.LineSpacing);   // 나머지는 그대로
        Assert.All(new[] { first.Id, second.Id }, id => Assert.Equal(WriterAlign.Right, editor.Project.FindDialogue(id)!.WriterAlign));
        Assert.Equal(4, changes);

        editor.SetWriterBase([first.Id, second.Id], basis => basis with { Size = 16 });   // 같다
        editor.SetWriterAlign([first.Id, second.Id], WriterAlign.Right);
        Assert.Equal(4, changes);

        editor.Undo();
        Assert.All(new[] { first.Id, second.Id }, id => Assert.Equal(WriterAlign.Left, editor.Project.FindDialogue(id)!.WriterAlign));
        editor.Undo();
        Assert.Equal((11d, 12d), (editor.Project.FindDialogue(first.Id)!.WriterBase!.Size, editor.Project.FindDialogue(second.Id)!.WriterBase!.Size));
    }

    // ── 카드의 줄 간격(WF-D19) ─────────────────────────

    /// <summary>
    /// 2026-11-23 소유자 — 「줄 간격을 새 카드의 기본값으로 바꿉시다」. 줄 간격도 카드의 처음 값에 적힌다(정했을 때만 · 다시 읽으면 그대로). 줄 간격이 처음 값이
    /// 되기 전에 적힌 카드(글꼴 · 크기만)는 열 때 줄 간격만 그 PC의 설정으로 적힌다 — 글꼴 · 크기는 그대로다. 이상한 값은 안 적힌 것으로 읽는다.
    /// </summary>
    [Fact]
    public void 줄_간격은_처음_값에_적히고_줄_간격만_없는_옛_카드는_줄_간격만_채운다()
    {
        (ProjectEditor editor, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어");
        card.WriterBase = new WriterBase("맑은 고딕", 14, LineSpacing: 160);
        string saved = ProjectSnapshotCodec.Encode(editor.Project);
        Assert.Contains("\"lineSpacing\": 160", saved, StringComparison.Ordinal);
        Assert.Equal(card.WriterBase, ProjectSnapshotCodec.Decode(saved).FindDialogue(card.Id)!.WriterBase);

        // 줄 간격이 없는 옛 처음 값 — 줄 간격만 채운다.
        card.WriterBase = new WriterBase("맑은 고딕", 14);
        Assert.DoesNotContain("lineSpacing", ProjectSnapshotCodec.Encode(editor.Project), StringComparison.Ordinal);
        int empty = editor.Project.EnumerateNodes().OfType<DialogueNode>().Count(node => node.WriterBase is null);   // 처음 값이 아예 없는 다른 카드
        Assert.Equal(empty + 1, WriterBaseFill.Fill(editor.Project, new WriterBase(string.Empty, 11, Bold: true, LineSpacing: 140)));
        Assert.Equal(new WriterBase("맑은 고딕", 14, LineSpacing: 140), card.WriterBase);
        Assert.Equal(0, WriterBaseFill.Fill(editor.Project, new WriterBase(string.Empty, 11, LineSpacing: 200)));

        // 이상한 값 — 안 적힌 것으로 읽는다(처음 값의 나머지는 그대로).
        foreach (string broken in new[] { "\"넓게\"", "0", "-20" })
        {
            string json = ProjectSnapshotCodec.Encode(editor.Project).Replace("\"lineSpacing\": 140", "\"lineSpacing\": " + broken, StringComparison.Ordinal);
            Assert.Equal(new WriterBase("맑은 고딕", 14), ProjectSnapshotCodec.Decode(json).FindDialogue(card.Id)!.WriterBase);
        }
    }

    /// <summary>
    /// 같은 날 — 「서식도구에 줄 간격 조절을 새로 넣는게 맞습니다」. 카드 하나의 줄 간격을 바꾼다 — 처음 값의 나머지(글꼴 · 크기 · 굵게 · 글자색)는 그대로이고,
    /// 같은 값이면 적지 않으며, 되돌리기 한 번에 돌아간다. 처음 값이 아예 없는 카드는 새 카드의 처음 값 위에 적는다.
    /// </summary>
    [Fact]
    public void 카드의_줄_간격을_바꾸면_그_카드만_바뀌고_되돌리기가_돌린다()
    {
        (ProjectEditor editor, DialogueNode card, _) = Stage("하린: 오늘은 긴 하루였어");
        var blue = new WriterRgb(0x20, 0x40, 0x80);
        card.WriterBase = new WriterBase("맑은 고딕", 14, Bold: true, Color: blue, LineSpacing: 140);
        var changes = new List<ProjectChangedEventArgs>();
        editor.Changed += (_, change) => changes.Add(change);

        editor.SetWriterLineSpacing(card.Id, 180);
        Assert.Equal(new WriterBase("맑은 고딕", 14, Bold: true, Color: blue, LineSpacing: 180), editor.Project.FindDialogue(card.Id)!.WriterBase);
        Assert.Equal((ProjectChangeKind.WriterMarks, card.Id), (changes.Single().Kind, changes.Single().NodeId));

        editor.SetWriterLineSpacing(card.Id, 180);   // 같은 값
        Assert.Single(changes);

        editor.Undo();
        Assert.Equal(140, editor.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);
        editor.Redo();
        Assert.Equal(180, editor.Project.FindDialogue(card.Id)!.WriterBase!.LineSpacing);

        // 처음 값이 없는 카드 — 새 카드의 처음 값 위에. 그것도 없으면 적을 수 없다.
        editor.Project.FindDialogue(card.Id)!.WriterBase = null;
        Assert.Throws<InvalidOperationException>(() => editor.SetWriterLineSpacing(card.Id, 150));
        editor.NewCardWriterBase = () => new WriterBase(string.Empty, 11, LineSpacing: 140);
        editor.SetWriterLineSpacing(card.Id, 150);
        Assert.Equal(new WriterBase(string.Empty, 11, LineSpacing: 150), editor.Project.FindDialogue(card.Id)!.WriterBase);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetWriterLineSpacing(card.Id, 0));
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
