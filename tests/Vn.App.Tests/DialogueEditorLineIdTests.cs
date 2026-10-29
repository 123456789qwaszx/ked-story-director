using Avalonia.Controls;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>대사 편집기는 LineId를 보이지 않는다</b> (2026-10-29 소유자: *"LineId는 필요없으니 화면에 노출되지 않도록
/// 해주십시오."*). 줄 신원은 데이터에 그대로 산다 — 쓰는 사람에게 보일 이유가 없을 뿐이다.
/// </summary>
public sealed class DialogueEditorLineIdTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-line-id", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 줄_표에는_LineId_열도_LineId_글자도_없다() => HeadlessUi.Run(() =>
    {
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "LineId" });

        var session = new AuthoringSession();
        session.Open(manifest);
        session.Editor.EnsureChapter("ch01");
        DialogueNode node = session.Editor.AddDialogueNode(session.EnsureChapterBoard("ch01"), 0, 0, "root");
        string lineId = node.Script.ActiveLines.First().Id;
        session.Editor.SetScriptLineText(node.Id, lineId, "라루", "첫 줄");

        var editor = new DialogueNodeEditor();
        var window = new Window { Width = 1200, Height = 800, Content = editor };
        window.Show();
        editor.Attach(session);
        editor.Show(node.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Control[] shown = editor.GetVisualDescendants().OfType<Control>().ToArray();

        // 머리글은 # · 화자 · 대사뿐이다.
        Assert.DoesNotContain(shown.OfType<TextBlock>(), block => block.Text == "LineId");

        // 줄의 신원은 글자로도 툴팁으로도 안 보인다.
        Assert.DoesNotContain(
            shown.OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains(lineId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            shown,
            control => (ToolTip.GetTip(control) as string ?? string.Empty).Contains(lineId, StringComparison.Ordinal));

        // 대사는 그대로 보인다 — 열을 걷다 칸이 밀리지 않았다.
        Assert.Contains(shown.OfType<TextBox>(), box => box.Text == "첫 줄");

        window.Close();
    });
}
