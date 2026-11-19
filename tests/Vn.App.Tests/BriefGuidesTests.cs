using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>설명 간소화</b> — 설정 창 「기타」의 체크를 켜면 쓰는 법 안내글이 숨고, 「없습니다 — …」 줄은 상태만 남는다(2026-11-18 소유자 ·
/// <c>docs/work-orders/brief-guides-orders.md</c>). 이 PC의 앱 설정이고 처음에는 꺼져 있다. 누르는 즉시 반영된다.
/// </summary>
public sealed class BriefGuidesTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-brief-guides", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 켜면_구성의_안내는_숨고_없습니다_줄은_상태만_남으며_끄면_돌아온다() => HeadlessUi.Run(() =>
    {
        GuideMode.Set(false);

        try
        {
            (MainWindow window, _) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
            ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
            view.Inspect(new HierarchyPick(HierarchyKind.Chapter, "ch01"));   // 「조건 및 스탯」 탭
            Dispatcher.UIThread.RunJobs();

            GuideLine conditionHint = Guide(view.FindControl<StackPanel>("ConditionSection")!, "조건마다 식을 정합니다");
            GuideLine emptyConditions = Guide(view.FindControl<StackPanel>("ConditionListPanel")!, "조건이 없습니다");
            GuideLine subtitle = Guide(view.FindControl<TabItem>("ConditionTabItem")!, "이 챕터에서만 씁니다");
            Assert.True(conditionHint.IsVisible);
            Assert.Equal("조건이 없습니다 — 아래 칸에 이름을 적고 [＋ 추가]로 만듭니다.", emptyConditions.Text);

            // 설정 창 「기타」 — 처음에는 꺼져 있다. 누르는 즉시 반영된다.
            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.False(settings.BriefGuidesCheck.IsChecked);
            settings.BriefGuidesCheck.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(GuideMode.Brief);
            Assert.True(AppSettingsService.LoadBriefGuides());

            Assert.False(conditionHint.IsVisible);
            Assert.False(subtitle.IsVisible);
            Assert.Equal("조건이 없습니다.", emptyConditions.Text);   // 상태만
            Assert.True(emptyConditions.IsVisible);
            Assert.Contains(view.FindControl<TabItem>("ConditionTabItem")!.GetLogicalDescendants().OfType<TextBlock>(),
                block => block.Text == "챕터 단위 조건 · 스탯" && block.IsEffectivelyVisible);   // 머리는 그대로

            // 다시 지은 줄도 스위치를 따른다.
            view.Refresh();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("조건이 없습니다.", Guide(view.FindControl<StackPanel>("ConditionListPanel")!, "조건이 없습니다").Text);

            // 화자 탭의 풀이도 숨는다.
            view.ShowSideTab(SideTab.Speakers);
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.FindControl<TextBlock>("SpeakerHintText")!.IsVisible);

            // 끄면 돌아온다.
            settings.BriefGuidesCheck.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.FindControl<TextBlock>("SpeakerHintText")!.IsVisible);
            view.ShowSideTab(SideTab.Conditions);
            Dispatcher.UIThread.RunJobs();
            Assert.True(conditionHint.IsVisible);
            Assert.StartsWith("조건이 없습니다 — ", Guide(view.FindControl<StackPanel>("ConditionListPanel")!, "조건이 없습니다").Text, StringComparison.Ordinal);

            settings.Close();
            window.Close();
        }
        finally
        {
            GuideMode.Set(false);   // 설정은 이 시험 프로세스가 함께 쓴다
        }
    });

    [Fact]
    public void 대본의_화자_조건_스탯_탭도_안내는_숨고_상태만_남는다() => HeadlessUi.Run(() =>
    {
        GuideMode.Set(false);

        try
        {
            (MainWindow window, _) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
            GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
            ContentControl speakers = graph.FindControl<ContentControl>("SpeakerHost")!;
            ContentControl conditions = graph.FindControl<ContentControl>("ConditionHost")!;

            GuideLine speakerHint = Guide(speakers, "대본의 화자 칸에서 고를 이름입니다");
            GuideLine noSpeakers = Guide(speakers, "아직 화자가 없습니다");
            GuideLine exprHint = Guide(conditions, "스탯과 조건의 식은 [구성] 탭에서 정합니다");
            GuideLine conditionHint = Guide(conditions, "간선의 표시조건 · 해금조건에 고를 이름입니다");
            GuideLine noStats = Guide(conditions, "이 챕터에는 아직 스탯이 없습니다");
            GuideLine subtitle = Guide(graph.FindControl<TabItem>("ConditionTabItem")!, "이 챕터에서만 씁니다");
            Assert.All(new[] { speakerHint, noSpeakers, exprHint, conditionHint, noStats, subtitle }, line => Assert.True(line.IsVisible));

            // 안내글은 화면에 서 있을 때 스위치를 듣고, 가려진 탭의 글은 그 탭이 열릴 때 맞춘다 — 사람이 탭을 여는 것처럼 연다.
            GuideMode.Set(true);
            graph.ShowSideTab(SideTab.Speakers);
            Dispatcher.UIThread.RunJobs();
            Assert.False(speakerHint.IsVisible);
            Assert.Equal("아직 화자가 없습니다.", noSpeakers.Text);

            graph.ShowSideTab(SideTab.Conditions);
            Dispatcher.UIThread.RunJobs();
            Assert.All(new[] { exprHint, conditionHint, subtitle }, line => Assert.False(line.IsVisible));
            Assert.Equal("이 챕터에는 아직 스탯이 없습니다.", noStats.Text);

            // 다시 지은 탭(화자를 더했다)도 스위치를 따른다. 상태만 말하는 줄(「이 챕터에는 아직 조건이 없습니다.」)은 그대로다.
            window.SessionProbe.AddSpeakerName("하린");
            graph.ShowSideTab(SideTab.Speakers);
            Dispatcher.UIThread.RunJobs();
            Assert.False(Guide(speakers, "대본의 화자 칸에서 고를 이름입니다").IsVisible);
            graph.ShowSideTab(SideTab.Conditions);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(conditions.GetLogicalDescendants().OfType<TextBlock>(),
                block => block.Text == "이 챕터에는 아직 조건이 없습니다." && block is not GuideLine);

            window.Close();
        }
        finally
        {
            GuideMode.Set(false);
        }
    });

    /// <summary>그 자리 안에서 그 글로 시작하는 안내글 — 긴 꼴로 찾는다.</summary>
    internal static GuideLine Guide(ILogical host, string start) =>
        host.GetLogicalDescendants().OfType<GuideLine>().Single(line => line.Full.StartsWith(start, StringComparison.Ordinal));
}
