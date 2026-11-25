using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>문구가 제 칸에 드는가 — 손으로 돌리는 점검</b>(2026-11-25 영어 화면 E7). 시험이 아니다: 환경 변수 <c>VN_UI_PROBE</c>가 없으면 아무것도
/// 하지 않고 지나간다. 문구 표(<c>Ui.resx</c> · <c>Ui.en.resx</c>)를 고친 뒤 글이 잘리지 않는지 볼 때 쓴다.
///
/// 켜면 그 언어로 두 창 폭(1400 · 최소 폭 1000)에서 주요 화면 — 시작 · [대본](곁기둥 셋 · 분기 카드) · [구성](챕터 · 에피소드 · 선택지 · 분기 카드 ·
/// 곁기둥 셋 · 검증 보고 펼침) · 설정 창(묶음 모두 펼침) — 을 그려 임시 폴더의 <c>vn-ui-probe/언어폭/</c>에 그림(PNG)과 <c>report.tsv</c>를 남긴다.
/// 보고는 보이는 글 가운데 <b>칸보다 긴 것</b>이다: <c>W</c> 한 줄 글이 칸보다 넓다(줄임표 「…」로 잘리거나 넘친다) · <c>H</c> 줄을 넘기는 글이
/// 칸 높이를 넘는다 · <c>CLIP</c> 조상이 글의 일부를 가린다(가로 스크롤이 있는 자리 — 검증 보고 — 는 괜찮다) · <c>HIDDEN</c> 창 밖으로 밀렸다.
/// 판(카드 · 선택지 칸) 위의 글은 판이 움직여 보는 자리라 가림은 세지 않는다(제 칸보다 넓은 것만 센다).
///
/// <code>
/// $env:VN_UI_PROBE="en"; dotnet test tests/Vn.App.Tests --filter "FullyQualifiedName~UiTextFitProbe"; Remove-Item Env:VN_UI_PROBE
/// </code>
/// 값은 <c>en</c> · <c>ko</c>(한 언어씩 따로 돌린다 — 화면 언어는 프로세스 전체이고, 한 번 지어 두는 표(단축키 표)는 처음 언어로 남는다).
/// ⚠ 다른 시험과 함께 돌리지 않는다 — 화면 언어를 바꾸는 동안 겹쳐 도는 시험이 흔들린다.
/// </summary>
public sealed class UiTextFitProbe
{
    [Theory]
    [InlineData(1400, 900)]
    [InlineData(1000, 640)]
    public void 그린다(int width, int height)
    {
        string? language = Environment.GetEnvironmentVariable("VN_UI_PROBE")?.Trim().ToLowerInvariant();

        if (language is not (UiLanguage.Korean or UiLanguage.English))
        {
            return;
        }

        HeadlessUi.Run(() =>
        {
            string output = Path.Combine(Path.GetTempPath(), "vn-ui-probe", $"{language}{width}");
            Directory.CreateDirectory(output);
            var report = new List<string>();
            UiLanguage.Apply(language);

            try
            {
                Walk(output, report, width, height);
            }
            finally
            {
                UiLanguage.Apply(UiLanguage.Korean);
                File.WriteAllLines(Path.Combine(output, "report.tsv"), report);
            }
        });
    }

    private static void Walk(string output, List<string> report, int width, int height)
    {
        string manifest = SaveProject(Path.Combine(output, "project"), out string branchId);

        var window = new MainWindow { Width = width, Height = height };
        window.Show();
        Look(window, window, "00-startup", output, report);

        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        var tabs = window.FindControl<TabControl>("MainTabs")!;
        tabs.SelectedIndex = MainTab.Script;
        Dispatcher.UIThread.RunJobs();

        DialogueNode root = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        session.Select(root.Id);
        var graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.FocusSelection();
        Look(window, window, "10-script-inspector", output, report);
        graph.ShowSideTab(SideTab.Speakers);
        Look(window, window, "11-script-speakers", output, report);
        graph.ShowSideTab(SideTab.Conditions);
        Look(window, window, "12-script-conditions", output, report);
        graph.ShowSideTab(SideTab.Inspector);
        session.Select(branchId);
        Look(window, window, "13-script-branch-card", output, report);

        tabs.SelectedIndex = MainTab.Structure;
        Dispatcher.UIThread.RunJobs();
        var chapters = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        chapters.SelectChapter("ch01");
        Look(window, window, "20-structure-chapter", output, report);
        chapters.SelectEpisode("b");
        Look(window, window, "21-structure-episode", output, report);
        chapters.SelectEdgeKey("root", "b", "Stay");
        Look(window, window, "22-structure-choice", output, report);
        chapters.SelectBranchCard(branchId);
        Look(window, window, "23-structure-branch-card", output, report);
        chapters.ShowSideTab(SideTab.Speakers);
        Look(window, window, "24-structure-speakers", output, report);
        chapters.ShowSideTab(SideTab.Conditions);
        Look(window, window, "25-structure-conditions", output, report);
        chapters.ShowSideTab(SideTab.Inspector);

        foreach (Expander expander in window.GetVisualDescendants().OfType<Expander>())
        {
            expander.IsExpanded = true;
        }

        Look(window, window, "26-structure-report-open", output, report);

        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        foreach (Expander expander in settings.GetVisualDescendants().OfType<Expander>())
        {
            expander.IsExpanded = true;
        }

        Look(settings, settings, "30-settings", output, report);
        settings.Close();
        window.Close();
    }

    /// <summary>챕터 하나(장면 둘 · 에피소드 셋) · 스탯 하나 · 조건 하나 · 선택지 둘(하나는 조건 · 스탯변화) · 분기 카드 하나.</summary>
    private static string SaveProject(string directory, out string branchId)
    {
        Directory.CreateDirectory(directory);

        var editor = new ProjectEditor(new StoryProject { Title = "Probe" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "a", title: "a", 260, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "b", title: "b", 520, 0, sceneId: "hall");
        editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.AddChapterCondition("ch01", "close_to_harin", "trust >= 3");
        editor.AddEdge("ch01", "root", "a", optionLabel: "Follow Laru down the long hallway", statChanges: "trust +1");
        editor.AddEdge("ch01", "root", "b", conditionLabel: "close_to_harin", optionLabel: "Stay");

        DialogueNode root = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        editor.ApplyScenarioText(root.Id, "Harin: It was a really long day.\nThe door opens and a strange wind blows in.");

        DialogueNode caller = EpisodeNaming.CardFor(editor.Project, "ch01", "a")!;
        DialogueNode branch = editor.AddBranchMarker(caller.Id, caller.Script.ActiveLines.First().Id);
        editor.SetBranchCardTitle(branch.Id, "Side path");
        Assert.Null(BranchEntryCondition.Set(editor, branch.Id, "close_to_harin"));
        branchId = branch.Id;

        string manifest = Path.Combine(directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);
        return manifest;
    }

    /// <summary>그 창을 그려 두고, 보이는 글 가운데 칸보다 긴 것을 적는다.</summary>
    private static void Look(TopLevel shot, Visual root, string name, string output, List<string> report)
    {
        Dispatcher.UIThread.RunJobs();
#pragma warning disable CS0618
        shot.CaptureRenderedFrame()!.Save(Path.Combine(output, name + ".png"));
#pragma warning restore CS0618

        foreach (TextBlock block in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!block.IsEffectivelyVisible || string.IsNullOrEmpty(block.Text) || block.Bounds.Width <= 0 ||
                Problem(block, shot) is not { } problem)
            {
                continue;
            }

            Point at = block.TranslatePoint(default, shot) ?? default;
            string text = block.Text.Replace("\n", " ⏎ ", StringComparison.Ordinal);
            report.Add($"{name}\t{problem}\t@{at.X:0},{at.Y:0}\t{text}");
        }
    }

    private static string? Problem(TextBlock block, TopLevel shot)
    {
        var face = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch);
        double width = block.Bounds.Width - block.Padding.Left - block.Padding.Right;

        if (block.TextWrapping == TextWrapping.NoWrap)
        {
            double natural = new TextLayout(block.Text, face, block.FontSize, null).WidthIncludingTrailingWhitespace;

            if (natural > width + 0.75)
            {
                return $"W {natural:0}>{width:0}";
            }
        }
        else
        {
            var wrapped = new TextLayout(block.Text, face, block.FontSize, null, textWrapping: TextWrapping.Wrap, maxWidth: Math.Max(1, width));
            double height = block.Bounds.Height - block.Padding.Top - block.Padding.Bottom;

            if (wrapped.Height > height + 0.75)
            {
                return $"H {wrapped.Height:0}>{height:0}";
            }
        }

        if (block.GetVisualAncestors().Any(ancestor => ancestor is Control { Name: "GraphCanvas" or "ZoomHost" }))
        {
            return null;
        }

        var own = new Rect(block.TranslatePoint(default, shot) ?? default, block.Bounds.Size);
        Rect seen = own;

        foreach (Visual ancestor in block.GetVisualAncestors().Where(ancestor => ancestor.ClipToBounds))
        {
            seen = seen.Intersect(new Rect(ancestor.TranslatePoint(default, shot) ?? default, ancestor.Bounds.Size));
        }

        if (seen.Width > 0 && seen.Width < own.Width - 1)
        {
            return $"CLIP {seen.Width:0}/{own.Width:0}";
        }

        return seen.Width <= 0 && !block.GetVisualAncestors().Any(ancestor => ancestor is ScrollContentPresenter)
            ? "HIDDEN"
            : null;
    }
}
