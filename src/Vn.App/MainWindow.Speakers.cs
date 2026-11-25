using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;
using Vn.Authoring.Text;
using Vn.App.Strings;
using Vn.Authoring.Strings;

namespace Vn.App;

/// <summary>
/// [대본] 탭의 <b>화자 목록</b> — 오른쪽 기둥의 「화자」 탭에 늘 선다(writer-fields W3 · side-tabs V3 — ⛔ 그 전에는
/// 하이어아키의 프로젝트 줄을 골라야 인스펙터에 섰다).
///
/// 작가가 화자 이름을 더하고 고치고 지운다. 이름을 고치면 이미 쓰인 대사 줄의 화자가 따라간다. 캐릭터 키(초상화 · 표정)는 여기서
/// 정하지 않는다 — [구성] 탭의 [화자] 탭이 잇는다. 규칙(빈 이름 · 같은 이름)은 <see cref="AuthoringSession"/>의 화자 창구 한 벌이다.
///
/// 목록 아래에는 <b>대본에만 있는 이름</b>(화자 칸에 바로 적었지만 목록에 없는 이름)이 모인다 — 거기서 목록에 올리거나 이름을 고친다(usability UX34).
///
/// 칸은 초점이 떠날 때와 Enter에 넣고, Esc는 넣지 않은 글을 걷는다. 화자 목록은 프로젝트의 것이라(되돌리기 한 칸) 넣고 나면 —
/// 되돌려도 — <see cref="AuthoringSession.SpeakersChanged"/>로 탭이 다시 선다.
/// </summary>
public partial class MainWindow
{
    /// <summary>다시 세운 「화자」 탭에서 초점을 돌려 줄 칸의 Tag — [＋ 추가]로 여럿을 잇달아 더할 때 손이 마우스로 가지 않게.</summary>
    private string? _speakerFocusTag;

    /// <summary>「화자」 탭의 화자 목록 — 이름 칸 · ✕ 줄들과 맨 아래 더하기 줄.</summary>
    private void SpeakerList()
    {
        IReadOnlyList<Speaker> speakers = _session.Project.Speakers;

        Guide(Ui.Speakers_NamesToPickInScript);

        if (speakers.Count == 0)
        {
            Guide(Ui.Speakers_NoSpeakersYetTypeName, Ui.Speakers_NoSpeakersYet);
        }

        var rows = new StackPanel { Spacing = 3, Margin = new Thickness(0, 4, 0, 0) };

        foreach (Speaker speaker in speakers)
        {
            rows.Children.Add(SpeakerRow(speaker));
        }

        _sheet.Children.Add(rows);

        var addBox = new TextBox { FontSize = 12, PlaceholderText = Ui.Speakers_NewSpeakerName, Tag = "speaker-add-name" };
        var addButton = new Button
        {
            Content = Ui.Speakers_Add,
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = "speaker-add"
        };
        ToolTip.SetTip(addButton, Ui.Speakers_EnterAlsoAddsSameName);

        void Add() => UiGuard.Run(_session, Ui.Speakers_AddSpeaker, () =>
        {
            string typed = (addBox.Text ?? string.Empty).Trim();

            if (_session.AddSpeakerName(typed) is { } refused)
            {
                _session.SetStatus(refused);
                return;
            }

            _speakerFocusTag = "speaker-add-name";
            string key = _session.Project.FindSpeaker(typed)?.CharacterId ?? string.Empty;
            _session.SetStatus(string.Format(Ui.Speakers_AddedSpeakerItCanBe, typed, key));
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
        _sheet.Children.Add(addRow);

        UnlistedSpeakerList();
    }

    /// <summary>
    /// <b>대본에만 있는 이름</b>(2026-11-19 소유자 · usability UX34 — 「화자를 추가하지 않고도 그대로 대사에 speaker로 바로 쓰기도 합니다. 이런 등록되지
    /// 않은 speaker들 역시 화자 탭에서 모아 둔 뒤에 관리하는 기능」). 대사의 화자 칸에 적혔는데 목록에 없는 이름(<see cref="UnlistedSpeakers"/>)마다
    /// `[이름][N줄][등록]` 한 줄, 둘 이상이면 [모두 등록]. 이름을 고치면 그 줄들이 따라가고, 목록의 이름으로 고치면 그 화자로 합쳐진다(오타).
    /// 없으면 칸이 서지 않는다. 대본이 바뀌어 이름 · 줄 수가 달라지면 <see cref="AuthoringSession.SpeakersChanged"/>로 다시 선다.
    /// </summary>
    private void UnlistedSpeakerList()
    {
        IReadOnlyList<UnlistedSpeakers.Entry> unlisted = UnlistedSpeakers.Of(_session.Project);

        if (unlisted.Count == 0)
        {
            return;
        }

        _sheet.Children.Add(new TextBlock
        {
            Text = Plural.Format(Ui.Speakers_NamesOnlyInScript, unlisted.Count),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 16, 0, 0),
            Tag = "speaker-unlisted-header"
        });
        Guide(Ui.Speakers_NamesTypedDirectlyInDialogue,
              Ui.Speakers_RegisterRenamingUpdatesThoseLines);

        var rows = new StackPanel { Spacing = 3, Margin = new Thickness(0, 4, 0, 0) };

        foreach (UnlistedSpeakers.Entry entry in unlisted)
        {
            rows.Children.Add(UnlistedSpeakerRow(entry));
        }

        _sheet.Children.Add(rows);

        if (unlisted.Count < 2)
        {
            return;
        }

        var all = new Button
        {
            Content = Ui.Speakers_RegisterAll,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
            Tag = "speaker-register-all"
        };
        ToolTip.SetTip(all, Plural.Format(Ui.Speakers_PutsNamesAboveIntoSpeaker, unlisted.Count));
        all.Click += (_, _) => UiGuard.Run(_session, Ui.Speakers_RegisterAllSpeakers, () =>
        {
            // 누른 때의 대본으로 다시 본다 — 이름 칸을 고치던 중이면 초점이 떠나며 먼저 고쳐졌다.
            if (_session.RegisterSpeakerNames(UnlistedSpeakers.Of(_session.Project).Select(entry => entry.Name)) is { } report)
            {
                _session.SetStatus(report);
            }
        });
        _sheet.Children.Add(all);
    }

    /// <summary>대본에만 있는 이름 한 줄 — `[이름][N줄][등록]`. 이름 칸은 화자 줄의 칸처럼 초점이 떠날 때 · Enter에 넣고 Esc는 걷는다.</summary>
    private Control UnlistedSpeakerRow(UnlistedSpeakers.Entry entry)
    {
        string speakerName = entry.Name;
        var name = new TextBox { Text = speakerName, FontSize = 12, Tag = "speaker-unlisted:" + speakerName };
        ToolTip.SetTip(name, Plural.Format(Ui.Speakers_RenamingUpdatesAllDialogueLines, speakerName, Josa.이가(speakerName), entry.Lines));

        void Commit() => UiGuard.Run(_session, Ui.Speakers_SpeakerNameInScript, () =>
        {
            string typed = (name.Text ?? string.Empty).Trim();

            if (string.Equals(typed, speakerName, StringComparison.Ordinal))
            {
                name.Text = speakerName;   // 앞뒤 공백만 걷었다
                return;
            }

            long before = _session.Editor.Revision;
            string? report = _session.FixUnlistedSpeakerName(speakerName, typed);

            if (_session.Editor.Revision == before)
            {
                name.Text = speakerName;   // 못 고쳤다 — 칸이 돌아온다
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

        var lines = new TextBlock
        {
            Text = Plural.Format(Ui.Speakers_Lines, entry.Lines),
            FontSize = 11,
            Opacity = 0.65,
            MinWidth = 34,
            TextAlignment = TextAlignment.Right,   // 줄 수의 자릿수가 달라도 이름 칸 끝이 나란하다
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = "speaker-unlisted-lines:" + speakerName
        };

        var register = new Button
        {
            Content = Ui.Speakers_Register,
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            Tag = "speaker-register:" + speakerName
        };
        ToolTip.SetTip(register, string.Format(Ui.Speakers_PutsInSpeakerListWith, speakerName, Josa.을를(speakerName)));
        register.Click += (_, _) => UiGuard.Run(_session, Ui.Speakers_RegisterSpeaker, () =>
        {
            // 그 칸을 고치던 중이면 초점이 떠나며 이름이 먼저 고쳐졌다 — 이제 아무 줄도 쓰지 않는 옛 이름은 올리지 않는다.
            if (!UnlistedSpeakers.Of(_session.Project).Any(item => string.Equals(item.Name, speakerName, StringComparison.Ordinal)))
            {
                return;
            }

            if (_session.RegisterSpeakerNames([speakerName]) is { } report)
            {
                _session.SetStatus(report);
            }
        });

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        Grid.SetColumn(lines, 1);
        Grid.SetColumn(register, 2);
        row.Children.Add(name);
        row.Children.Add(lines);
        row.Children.Add(register);

        return row;
    }

    /// <summary>
    /// 화자의 이름 색 단추(2026-11-23 소유자 — 「화자탭에서 그 화자별로 원하는 색상을 지정 한 뒤에, 에피소드 탭 내부에서 그 화자를 사용할 때, 자동으로 그 색상으로
    /// 이름이 바뀌도록」 · writer-formatting WF-D15). 정한 색이면 그 색 네모, 없으면 빈 네모. 누르면 색 차림(<see cref="WriterColorChooser"/> — 정해 둔 열 가지 ·
    /// 없음 · 이 프로젝트에 쓴 색 · 직접 고르기)이 열린다. 고르면 되돌리기 한 칸이고, [대본] 카드 글 칸의 그 이름이 그 색이 된다.
    ///
    /// <paramref name="fill"/>이면 <b>이름 바탕색</b> 단추다(2026-11-23 소유자 — 「여기다가 캐릭터의 글자 바탕색도 조정할 수 있게 해줄래?」 · WF-D22) — 「가」가
    /// 그 화자의 이름 색으로 그 바탕 위에 선다(이름이 칸에서 보일 모습). 바탕색 차림이다(정해 둔 연한 열 가지).
    /// </summary>
    private Button SpeakerColorButton(Speaker speaker, bool fill = false)
    {
        string speakerName = speaker.Name;
        WriterRgb? now = fill ? speaker.Fill : speaker.Color;
        string what = fill ? Ui.Speakers_NameBackground : Ui.Speakers_NameColor;
        var chooser = new WriterColorChooser(fill, fill ? Ui.Speakers_ClearNameBackgroundShownWithout : Ui.Speakers_ClearNameColorShownIn);
        var flyout = new Flyout { Content = chooser.Content, Placement = Avalonia.Controls.PlacementMode.BottomEdgeAlignedLeft };
        var swatch = new Border
        {
            Width = fill ? 18 : 14,
            Height = fill ? 16 : 14,
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            Background = now is { } color ? WriterPalette.Brush(color) : Brushes.Transparent
        };

        // 바탕색 단추는 이름이 보일 모습 — 이름 색의 「가」가 그 바탕 위에.
        if (fill)
        {
            var glyph = new TextBlock
            {
                Text = Ui.Format_Text,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (speaker.Color is { } nameColor)
            {
                glyph.Foreground = WriterPalette.Brush(nameColor);
            }

            swatch.Child = glyph;
        }

        var button = new Button
        {
            Content = swatch,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, fill ? 6 : 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Flyout = flyout,
            Tag = (fill ? "speaker-fill:" : "speaker-color:") + speakerName
        };
        ToolTip.SetTip(button, now is { } shown
            ? string.Format(Ui.Speakers_InScriptBoxClickTo, speakerName, what, shown.Hex)
            : string.Format(Ui.Speakers_InScriptBoxNotSet, speakerName, what));

        flyout.Opening += (_, _) => chooser.Prepare(GraphEditorView.ProjectColors(_session.Project, fill), now);
        chooser.Picked += picked =>
        {
            flyout.Hide();
            UiGuard.Run(_session, Ui.Speakers_Speaker + what, () =>
            {
                if (fill ? _session.Editor.SetSpeakerFill(speakerName, picked) : _session.Editor.SetSpeakerColor(speakerName, picked))
                {
                    _session.SetStatus(picked is { } rgb
                        ? string.Format(Ui.Speakers_SetOfSpeakerToName, speakerName, what, rgb.Hex)
                        : string.Format(Ui.Speakers_ClearedOfSpeaker, speakerName, what));
                }
            });
        };

        return button;
    }

    /// <summary>화자 한 줄 — `[이름 색][이름 바탕색][이름][✕]`. 이름을 비우는 것은 지우기가 아니다 — 지우기는 ✕ 하나다.</summary>
    private Control SpeakerRow(Speaker speaker)
    {
        string speakerName = speaker.Name;
        var name = new TextBox { Text = speakerName, FontSize = 12, Tag = "speaker-name:" + speakerName };

        void Commit() => UiGuard.Run(_session, Ui.Speakers_SpeakerName, () =>
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
        ToolTip.SetTip(remove, string.Format(Ui.Speakers_RemovesFromListNamesAlready, speakerName));
        remove.Click += (_, _) => UiGuard.Run(_session, Ui.Speakers_DeleteSpeaker, () =>
        {
            if (_session.RemoveSpeakerName(speakerName))
            {
                _session.SetStatus(string.Format(Ui.Speakers_RemovedSpeakerFromListNames, speakerName));
            }
        });

        // [이름 색][이름 바탕색][이름][✕] — 바탕색은 2026-11-23(WF-D22).
        Button color = SpeakerColorButton(speaker);
        Button fill = SpeakerColorButton(speaker, fill: true);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        Grid.SetColumn(fill, 1);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(remove, 3);
        row.Children.Add(color);
        row.Children.Add(fill);
        row.Children.Add(name);
        row.Children.Add(remove);

        return row;
    }

    /// <summary>화자 목록이 바뀌었다 — 「화자」 탭을 다시 세운다(넣기가 끝난 뒤에 — 넣는 칸을 그 자리에서 걷지 않게).</summary>
    private void OnSpeakersChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => UiGuard.Run(_session, Ui.Board_Speakers, () =>
    {
        FillSpeakerTab();

        if (_speakerFocusTag is { } tag)
        {
            _speakerFocusTag = null;
            DirectorSpeakers.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(box => Equals(box.Tag, tag))?.Focus();
        }
    }));
}
