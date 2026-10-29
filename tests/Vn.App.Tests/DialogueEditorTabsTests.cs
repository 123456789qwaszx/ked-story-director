using Avalonia.Controls;
using Avalonia.LogicalTree;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>대사 편집기에는 탭이 없다</b> (2026-10-29 소유자: *"Script Preview 탭은 더 이상 필요없으니 완전히
/// 제거하십시오."*). [발행] 탭(2026-09-15)에 이어 Script Preview도 걷혀 남는 것이 대사 편집 하나라, 탭 머리도
/// 함께 걷혔다. 긴 대본은 카드 안 글 칸(D4)에 붙여 넣는다.
/// </summary>
public sealed class DialogueEditorTabsTests
{
    [Fact]
    public void Script_Preview도_탭도_없고_줄_목록이_바로_선다() => HeadlessUi.Run(() =>
    {
        var editor = new DialogueNodeEditor();
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(editor.GetLogicalDescendants().OfType<TabControl>());
        Assert.Null(editor.FindControl<TextBox>("PreviewBox"));
        Assert.Null(editor.FindControl<Button>("ApplyScenarioButton"));
        Assert.Null(editor.FindControl<ComboBox>("PreviewPresetCombo"));
        Assert.NotNull(editor.FindControl<StackPanel>("LineHost"));

        window.Close();
    });
}
