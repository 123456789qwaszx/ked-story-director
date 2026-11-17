using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App;

/// <summary>
/// [대본] 탭의 <b>화자 목록</b> — 하이어아키의 프로젝트 줄을 고르면 인스펙터에 선다(<c>docs/work-orders/writer-fields-orders.md</c> W3).
///
/// 작가가 화자 이름을 더하고 고치고 지운다. 이름을 고치면 이미 쓰인 대사 줄의 화자가 따라간다. 캐릭터 키(초상화 · 표정)는 여기서
/// 정하지 않는다 — [구성] 탭의 [화자] 탭이 잇는다. 규칙(빈 이름 · 같은 이름)은 <see cref="AuthoringSession"/>의 화자 창구 한 벌이다.
///
/// 칸은 초점이 떠날 때와 Enter에 넣고, Esc는 넣지 않은 글을 걷는다. 화자 목록은 프로젝트의 것이라(되돌리기 한 칸) 넣고 나면 —
/// 되돌려도 — <see cref="AuthoringSession.SpeakersChanged"/>로 요약이 다시 선다.
/// </summary>
public partial class MainWindow
{
    /// <summary>다시 세운 요약에서 초점을 돌려 줄 칸의 Tag — [＋ 추가]로 여럿을 잇달아 더할 때 손이 마우스로 가지 않게.</summary>
    private string? _speakerFocusTag;

    /// <summary>프로젝트 요약 칸의 화자 목록 — 이름 칸 · ✕ 줄들과 맨 아래 더하기 줄.</summary>
    private void SpeakerList()
    {
        IReadOnlyList<Speaker> speakers = _session.Project.Speakers;

        Note("대본의 화자 칸에서 고를 이름입니다. 모든 챕터가 같은 목록을 씁니다. 이름을 고치면 이미 쓴 대사 줄도 따라갑니다. " +
             "캐릭터 키(초상화 · 표정)는 [구성] 탭에서 잇습니다.");

        if (speakers.Count == 0)
        {
            Note("아직 화자가 없습니다 — 아래에 이름을 적고 [＋ 추가]를 누르세요.");
        }

        var rows = new StackPanel { Spacing = 3, Margin = new Thickness(0, 4, 0, 0) };

        foreach (Speaker speaker in speakers)
        {
            rows.Children.Add(SpeakerRow(speaker));
        }

        DirectorSummary.Children.Add(rows);

        var addBox = new TextBox { FontSize = 12, PlaceholderText = "새 화자 이름", Tag = "speaker-add-name" };
        var addButton = new Button
        {
            Content = "＋ 추가",
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = "speaker-add"
        };
        ToolTip.SetTip(addButton, "Enter로도 더합니다. 같은 이름은 한 번만 섭니다.");

        void Add() => UiGuard.Run(_session, "화자 추가", () =>
        {
            string typed = (addBox.Text ?? string.Empty).Trim();

            if (_session.AddSpeakerName(typed) is { } refused)
            {
                _session.SetStatus(refused);
                return;
            }

            _speakerFocusTag = "speaker-add-name";
            _session.SetStatus($"화자 '{typed}'을 더했습니다 — 모든 챕터의 대본에서 고를 수 있습니다. 캐릭터 키는 [구성] 탭에서 잇습니다.");
        });

        addButton.Click += (_, _) => Add();
        addBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Add();
            }
        };

        var addRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetColumn(addButton, 1);
        addRow.Children.Add(addBox);
        addRow.Children.Add(addButton);
        DirectorSummary.Children.Add(addRow);
    }

    /// <summary>화자 한 줄 — `[이름][✕]`. 이름을 비우는 것은 지우기가 아니다 — 지우기는 ✕ 하나다.</summary>
    private Control SpeakerRow(Speaker speaker)
    {
        string speakerName = speaker.Name;
        var name = new TextBox { Text = speakerName, FontSize = 12, Tag = "speaker-name:" + speakerName };

        void Commit() => UiGuard.Run(_session, "화자 이름", () =>
        {
            string typed = (name.Text ?? string.Empty).Trim();

            if (string.Equals(typed, speakerName, StringComparison.Ordinal))
            {
                name.Text = speakerName;   // 앞뒤 공백만 걷었다
                return;
            }

            string? report = _session.RenameSpeakerName(speakerName, typed);
            bool renamed = _session.Project.Speakers.Any(item => string.Equals(item.Name, typed, StringComparison.Ordinal)) &&
                           !_session.Project.Speakers.Any(item => string.Equals(item.Name, speakerName, StringComparison.Ordinal));

            if (!renamed)
            {
                name.Text = speakerName;
            }

            if (report is not null)
            {
                _session.SetStatus(report);
            }
        });

        name.LostFocus += (_, _) => Commit();
        name.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Commit();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                name.Text = speakerName;
            }
        }, RoutingStrategies.Tunnel);

        var remove = new Button
        {
            Content = "✕",
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = "speaker-remove:" + speakerName
        };
        ToolTip.SetTip(remove, $"'{speakerName}'를 목록에서 지웁니다. 이미 대본에 적힌 이름은 그대로 남습니다(화자 칸은 자유 입력).");
        remove.Click += (_, _) => UiGuard.Run(_session, "화자 삭제", () =>
        {
            if (_session.RemoveSpeakerName(speakerName))
            {
                _session.SetStatus($"화자 '{speakerName}'를 목록에서 지웠습니다 — 이미 대본에 적힌 이름은 그대로입니다.");
            }
        });

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(remove, 1);
        row.Children.Add(name);
        row.Children.Add(remove);

        return row;
    }

    /// <summary>화자 목록이 바뀌었다 — 프로젝트 요약이 서 있으면 다시 세운다(넣기가 끝난 뒤에 — 넣는 칸을 그 자리에서 걷지 않게).</summary>
    private void OnSpeakersChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => UiGuard.Run(_session, "화자", () =>
    {
        if (!DirectorSummary.IsVisible || _directorSummary is not { Kind: HierarchyKind.Project } pick)
        {
            _speakerFocusTag = null;
            return;
        }

        FillDirectorSummary(pick);

        if (_speakerFocusTag is { } tag)
        {
            _speakerFocusTag = null;
            DirectorSummary.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(box => Equals(box.Tag, tag))?.Focus();
        }
    }));
}
