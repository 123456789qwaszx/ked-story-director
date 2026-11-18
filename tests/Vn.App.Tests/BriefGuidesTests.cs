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
            (MainWindow window, _) = HierarchyFixture.Open(_directory, tab: 0);
            ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
            view.Inspect(new HierarchyPick(HierarchyKind.Chapter, "ch01"));   // 「조건 및 스탯」 탭
            Dispatcher.UIThread.RunJobs();

            GuideLine conditionHint = Guide(view.FindControl<StackPanel>("ConditionSection")!, "조건마다 식을 정합니다");
            GuideLine emptyConditions = Guide(view.FindControl<StackPanel>("ConditionListPanel")!, "조건이 없습니다");
            GuideLine subtitle = Guide(view.FindControl<TabItem>("ConditionTabItem")!, "이 챕터에서만 씁니다");
            Assert.True(conditionHint.IsVisible);
            Assert.Equal("조건이 없습니다 — 아래 [＋ 조건]으로 만듭니다.", emptyConditions.Text);

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

    /// <summary>그 자리 안에서 그 글로 시작하는 안내글 — 긴 꼴로 찾는다.</summary>
    internal static GuideLine Guide(ILogical host, string start) =>
        host.GetLogicalDescendants().OfType<GuideLine>().Single(line => line.Full.StartsWith(start, StringComparison.Ordinal));
}
