using Avalonia;
using Avalonia.Controls;
using Vn.App.Services;

namespace Vn.App.Views;

/// <summary>
/// <b>설명 간소화의 스위치</b> — 앱 하나에 하나다(설정 창 「기타」의 「설명 간소화」 · 이 PC의 앱 설정 <c>BriefGuides</c>, 처음 꺼짐 · 2026-11-18 소유자 ·
/// brief-guides). 켜면 <see cref="GuideLine"/>이 짧은 꼴(없으면 숨김)로 선다.
/// </summary>
internal static class GuideMode
{
    private static bool? _brief;

    /// <summary>설명을 간소화하는가 — 처음 읽을 때 앱 설정에서 가져온다.</summary>
    internal static bool Brief => _brief ??= AppSettingsService.LoadBriefGuides();

    /// <summary>스위치가 바뀌었다 — 서 있는 <see cref="GuideLine"/>이 제 꼴을 바꾼다.</summary>
    internal static event Action? Changed;

    /// <summary>스위치를 바꾸고 기억한다. 같으면 아무것도 하지 않는다.</summary>
    internal static void Set(bool brief)
    {
        if (Brief == brief)
        {
            return;
        }

        _brief = brief;
        AppSettingsService.SaveBriefGuides(brief);
        Changed?.Invoke();
    }
}

/// <summary>
/// <b>안내글 한 줄</b> — 긴 꼴(<see cref="Full"/>)과 짧은 꼴(<see cref="Brief"/>)을 함께 들고 <see cref="GuideMode"/>를 따른다. 짧은 꼴이 없으면
/// 간소화 때 숨는다(쓰는 법을 풀어 주는 글). 상태를 말하고 쓰는 법을 덧붙인 줄은 짧은 꼴에 상태만 둔다(「스탯이 없습니다.」).
///
/// 글은 <see cref="Full"/>로 준다 — <c>Text</c>를 직접 쓰면 스위치가 덮는다. 보이기도 이 줄이 정한다(밖에서 <c>IsVisible</c>을 만지지 않는다).
/// </summary>
internal sealed class GuideLine : TextBlock
{
    private string _full = string.Empty;
    private string? _brief;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>긴 꼴 — 간소화를 끄면 이 글이 선다.</summary>
    public string Full
    {
        get => _full;
        set
        {
            _full = value ?? string.Empty;
            Apply();
        }
    }

    /// <summary>짧은 꼴 — 간소화를 켜면 이 글이 선다. null이나 빈 글이면 숨는다.</summary>
    public string? Brief
    {
        get => _brief;
        set
        {
            _brief = value;
            Apply();
        }
    }

    /// <summary>두 꼴을 한 번에 준다.</summary>
    internal void Set(string full, string? brief)
    {
        _full = full;
        _brief = brief;
        Apply();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        GuideMode.Changed += Apply;
        Apply();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        GuideMode.Changed -= Apply;
        base.OnDetachedFromVisualTree(e);
    }

    private void Apply()
    {
        bool brief = GuideMode.Brief;
        Text = brief ? _brief ?? string.Empty : _full;
        IsVisible = !brief || !string.IsNullOrEmpty(_brief);
    }
}
