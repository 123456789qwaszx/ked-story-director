using Vn.App.Services;
using Vn.Authoring.Model;

namespace Vn.App.Views;

/// <summary>
/// <b>[대본] 판 카드의 기본 크기</b>(2026-11-25 소유자 — 「새 에피소드를 처음 만들었을 때, 줄일 수있는 최소 한도크기와는 별개로, 생성 시에 세팅해주는
/// 기본크기는 별개여야 한다 … 가로 세로 크기 맞춤은 이 기본 크기는 지켜주도록 세팅이 가능해야」 · 2026-11-26 소유자 — 「새 카드를 만들 때라기 보다는, 카드를
/// 최소화 했을 때 어느 크기를 가질 것인지를 결정하는 개념에 가깝습니다」 — 설정 창의 이름을 「[대본] 새 카드」에서 「[대본] 카드 기본 크기」로).
///
/// 이 PC의 앱 설정이다(<see cref="AppSettingsService.LoadNewCardSize"/> · 설정 창 「[대본] 카드 기본 크기」). <see cref="ScriptFont"/>와 같은 꼴: 처음 읽을 때 앱
/// 설정에서 가져오고, 바꾸면 기억하고 알린다.
/// <list type="bullet">
/// <item><b>기본 크기</b>(<see cref="Width"/> · <see cref="WritingHeight"/>) — 카드를 만들 때 카드에 적힌다(뒤에 바꿔도 서 있는 카드는 그대로다). 기본 크기로 고정한 카드가
/// 서는 크기 · 크기 손잡이를 두 번 눌러 돌아가는 크기도 이것이다.</item>
/// <item><b>가장 작은 크기</b>(<see cref="MinWidth"/> · <see cref="MinWritingHeight"/>) — 손잡이 · 글 맞춤으로 줄일 수 있는 끝. 설정이 아니다.</item>
/// <item><b>글 맞춤이 기본 크기를 지킨다</b>(<see cref="FitKeepsSize"/>) — 켜면 가로 · 세로 글 맞춤이 기본 크기보다 작게 줄이지 않는다(처음에는 켜져 있다).</item>
/// </list>
/// </summary>
internal static class CardDefaults
{
    internal const double DefaultWidth = 320;
    internal const double DefaultWritingHeight = 160;

    /// <summary>카드 폭의 가장 작은 값 — 판이 정한다(설정이 아니다).</summary>
    internal const double MinWidth = BoardSize.CardWidth;

    /// <summary>글 칸 높이의 가장 작은 값 — 판이 정한다(설정이 아니다).</summary>
    internal const double MinWritingHeight = BoardSize.CardWritingHeight;

    /// <summary>설정 창에서 고를 수 있는 기본 크기의 끝.</summary>
    internal const double MaxWidth = 1600;

    internal const double MaxWritingHeight = 1600;

    private static (double Width, double WritingHeight, bool FitKeeps)? _now;

    private static (double Width, double WritingHeight, bool FitKeeps) Now => _now ??= Clamp(AppSettingsService.LoadNewCardSize());

    /// <summary>기본 크기의 폭 — 새 카드 · 기본 크기로 고정한 카드가 이 폭으로 선다.</summary>
    internal static double Width => Now.Width;

    /// <summary>기본 크기의 글 칸 높이.</summary>
    internal static double WritingHeight => Now.WritingHeight;

    /// <summary>글 맞춤이 기본 크기보다 작게 줄이지 않는가.</summary>
    internal static bool FitKeepsSize => Now.FitKeeps;

    /// <summary>가로 글 맞춤이 줄이는 끝 — 기본 크기를 지키면 기본 폭, 아니면 가장 작은 폭.</summary>
    internal static double FitFloorWidth => FitKeepsSize ? Math.Max(MinWidth, Width) : MinWidth;

    /// <summary>세로 글 맞춤이 줄이는 끝 — 기본 크기를 지키면 기본 높이, 아니면 가장 작은 높이.</summary>
    internal static double FitFloorWritingHeight => FitKeepsSize ? Math.Max(MinWritingHeight, WritingHeight) : MinWritingHeight;

    /// <summary>바뀌었다 — 판이 켜진 글 맞춤을 다시 맞춘다.</summary>
    internal static event Action? Changed;

    private static bool? _pinsWidthOnly;

    /// <summary>
    /// <b>기본 크기로 고정할 때 가로만 고정한다</b>(2026-11-26 소유자 — 「가로만 활성으로 할 수 있도록 … 가로영역은 기본크기로 두되, 세로는 세로글 맞춤을 둘 수
    /// 있도록」). 다음에 고정하는 카드부터 따른다 — 이미 고정한 카드는 고정할 때의 방식 그대로다(카드에 적힌다 · <see cref="Vn.Authoring.Model.NodeLayout.PinnedWidthOnly"/>).
    /// 처음에는 꺼져 있다(가로 · 세로 모두).
    /// </summary>
    internal static bool PinsWidthOnly => _pinsWidthOnly ??= AppSettingsService.LoadPinWidthOnly();

    /// <summary>고정 방식이 바뀌었다 — 판이 고정 단추의 풍선을 다시 단다.</summary>
    internal static event Action? PinModeChanged;

    /// <summary>바꾸고 기억한다. 같으면 아무것도 하지 않는다.</summary>
    internal static void SetPinsWidthOnly(bool widthOnly)
    {
        if (widthOnly == PinsWidthOnly)
        {
            return;
        }

        _pinsWidthOnly = widthOnly;
        AppSettingsService.SavePinWidthOnly(widthOnly);
        PinModeChanged?.Invoke();
    }

    /// <summary>바꾸고 기억한다 — 범위 밖은 끝으로 당긴다(정수로). 같으면 아무것도 하지 않는다.</summary>
    internal static void Set(double width, double writingHeight, bool fitKeepsSize)
    {
        (double Width, double WritingHeight, bool FitKeeps) next = Clamp((width, writingHeight, fitKeepsSize));

        if (next == Now)
        {
            return;
        }

        _now = next;
        AppSettingsService.SaveNewCardSize(next.Width, next.WritingHeight, next.FitKeeps);
        Changed?.Invoke();
    }

    /// <summary>
    /// 시험의 손잡이 — 앱 설정에 적지 않고 그 동안만 값을 바꾼다(놓으면 앞의 값으로). 새 카드 크기와 상관없는 시험이 옛 판(가장 작은 크기 = 새 카드
    /// 크기)의 모양 그대로 돌게 한다.
    /// </summary>
    internal static IDisposable Pin(double width, double writingHeight, bool fitKeepsSize)
    {
        (double Width, double WritingHeight, bool FitKeeps) before = Now;
        _now = Clamp((width, writingHeight, fitKeepsSize));
        return new Unpin(() => _now = before);
    }

    private sealed class Unpin(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }

    /// <summary>처음 값으로 — 320 × 160 · 글 맞춤이 기본 크기를 지킨다 · 가로 · 세로 모두 고정한다.</summary>
    internal static void Reset()
    {
        Set(DefaultWidth, DefaultWritingHeight, fitKeepsSize: true);
        SetPinsWidthOnly(false);
    }

    /// <summary>시험의 손잡이 — 앱 설정에 적지 않고 그 동안만 고정 방식을 바꾼다.</summary>
    internal static IDisposable PinWidthOnlyForTest(bool widthOnly)
    {
        bool before = PinsWidthOnly;
        _pinsWidthOnly = widthOnly;
        return new Unpin(() => _pinsWidthOnly = before);
    }

    private static (double Width, double WritingHeight, bool FitKeeps) Clamp((double Width, double WritingHeight, bool FitKeeps) value) => (
        Math.Round(Math.Clamp(double.IsFinite(value.Width) ? value.Width : DefaultWidth, MinWidth, MaxWidth)),
        Math.Round(Math.Clamp(double.IsFinite(value.WritingHeight) ? value.WritingHeight : DefaultWritingHeight, MinWritingHeight, MaxWritingHeight)),
        value.FitKeeps);
}
