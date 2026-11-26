namespace Vn.App.Theming;

/// <summary>테마 값의 꼴 — 이름마다 어떤 값이 드는지(색 · 그림 · 수 …). 테마를 고치는 기능을 더할 때 값을 고르고 적는 법을 이것으로 가른다.</summary>
internal enum ThemeValueKind
{
    /// <summary>색 — 리소스는 <c>Color</c>.</summary>
    Color,

    /// <summary>색 — 리소스는 단색 붓(<c>SolidColorBrush</c>).</summary>
    Brush,

    /// <summary>바탕 — 색이나 그림(붓).</summary>
    Paint,

    /// <summary>그림(붓) — 없을 수도 있다.</summary>
    Picture,

    /// <summary>수.</summary>
    Number,

    /// <summary>네 방향의 거리(<c>Thickness</c>) — 왼쪽,위,오른쪽,아래.</summary>
    Thickness,

    /// <summary>모서리 둥글기(<c>CornerRadius</c>).</summary>
    Corner,

    /// <summary>그림자 — 테마 파일에는 <c>"x y 번짐 퍼짐 색"</c>(예: <c>"0 3 0 0 #C9DCE8"</c>)으로 적고, <c>none</c>이면 없다.</summary>
    Shadow,

    /// <summary>글꼴(<c>FontFamily</c>).</summary>
    Font
}

/// <summary>사람에게 보일 묶음 — 테마를 고치는 화면이 생기면 이 차례로 놓는다.</summary>
internal enum ThemeKeyGroup
{
    /// <summary>자주 바꾸는 것.</summary>
    Favorites,

    /// <summary>창 · 위 띠 · 글꼴.</summary>
    Window,

    /// <summary>단추 · 탭 · 글 칸.</summary>
    Controls,

    /// <summary>[대본] 판 — 바탕 · 테두리 · 이름표.</summary>
    Board,

    /// <summary>[대본] 카드.</summary>
    Cards,

    /// <summary>선택지 · 길.</summary>
    Choices,

    /// <summary>[구성] 판.</summary>
    Structure,

    /// <summary>상태 색 — 고름 · 오류 · 경고.</summary>
    Status,

    /// <summary>그 밖의 색.</summary>
    Other,

    /// <summary>모양 · 크기.</summary>
    Shapes

}

/// <summary>작가가 바꿀 수 있는 이름 하나 — 이름(리소스 키) · 꼴 · 묶음 · 두 언어의 설명.</summary>
internal sealed record ThemeKey(string Name, ThemeValueKind Kind, ThemeKeyGroup Group, string Korean, string English)
{
    /// <summary>화면 언어의 설명.</summary>
    public string Description => Services.UiLanguage.Current == Services.UiLanguage.English ? English : Korean;
}

/// <summary>
/// <b>테마가 정하는 이름의 목록</b>(2026-11-26 · themes) — 이름(리소스 키) · 값의 꼴 · 묶음 · 무엇인지(두 언어)를 한 곳에 모은다. 나중에 테마를 고치는 기능을
/// 더할 때 이 목록을 돈다(2026-11-26 소유자 — 「차후에 원한다면 테마 편집 관련 기능을 추가할 수 있도록 이름과 목록을 한 군데에 모아두는 정리만」).
/// <see cref="Skin"/>(코드가 쓰는 이름)은 모두 여기 있어야 하고, 그 밖은 XAML만 쓰는 이름(<c>Themes/PastelSky/Tokens.axaml</c> · Fluent 테마의 이름)이다.
/// <c>ThemeTests</c>가 이 목록과 테마가 맞는지 본다.
///
/// 이름을 더하려면: 테마의 Tokens.axaml에 값 · 여기 한 줄(꼴 · 묶음 · 설명 두 언어). 설명은 화면 어디에 보이는지를 말한다 — 코드의 낱말이 아니라.
/// </summary>
internal static class ThemeKeys
{
    /// <summary>묶음의 이름 — 화면 언어로.</summary>
    public static string GroupName(ThemeKeyGroup group) => (group, Services.UiLanguage.Current == Services.UiLanguage.English) switch
    {
        (ThemeKeyGroup.Favorites, false) => "자주 바꾸는 것",
        (ThemeKeyGroup.Favorites, true) => "Most often changed",
        (ThemeKeyGroup.Window, false) => "창 · 위 띠 · 글꼴",
        (ThemeKeyGroup.Window, true) => "Window, top bar and fonts",
        (ThemeKeyGroup.Controls, false) => "단추 · 탭 · 글 칸",
        (ThemeKeyGroup.Controls, true) => "Buttons, tabs and text boxes",
        (ThemeKeyGroup.Board, false) => "[대본] 판 — 바탕 · 테두리 · 이름표",
        (ThemeKeyGroup.Board, true) => "[Script] board — background, frames and labels",
        (ThemeKeyGroup.Cards, false) => "[대본] 카드",
        (ThemeKeyGroup.Cards, true) => "[Script] cards",
        (ThemeKeyGroup.Choices, false) => "선택지 · 길",
        (ThemeKeyGroup.Choices, true) => "Choices and paths",
        (ThemeKeyGroup.Structure, false) => "[구성] 판",
        (ThemeKeyGroup.Structure, true) => "[Structure] board",
        (ThemeKeyGroup.Status, false) => "상태 색 — 고름 · 오류 · 경고",
        (ThemeKeyGroup.Status, true) => "Status colours — selection, errors, warnings",
        (ThemeKeyGroup.Other, false) => "그 밖의 색",
        (ThemeKeyGroup.Other, true) => "Other colours",
        (ThemeKeyGroup.Shapes, false) => "모양 · 크기",
        (ThemeKeyGroup.Shapes, true) => "Shapes and sizes",
        _ => group.ToString()
    };

    /// <summary>모든 이름 — 묶음 차례, 묶음 안에서는 적은 차례.</summary>
    public static IReadOnlyList<ThemeKey> All { get; } =
    [
        new("WindowBackground", ThemeValueKind.Brush, ThemeKeyGroup.Favorites, "창 전체의 바탕", "Background of the whole window"),
        new("TopBarBackground", ThemeValueKind.Paint, ThemeKeyGroup.Favorites, "맨 위 띠의 바탕 — 색이나 그림", "Background of the top bar — a colour or a picture"),
        new("BoardBackground", ThemeValueKind.Paint, ThemeKeyGroup.Favorites, "[대본] 판의 바탕(챕터 바깥의 책상)", "Background of the [Script] board (the desk around chapters)"),
        new("TopBarEdgeBrush", ThemeValueKind.Picture, ThemeKeyGroup.Favorites, "맨 위 띠 바로 아래 줄에 얹는 그림(가로로 되풀이된다)", "Picture laid on the strip just below the top bar (repeats sideways)"),
        new("EpisodeBand", ThemeValueKind.Color, ThemeKeyGroup.Favorites, "에피소드 카드의 머리 띠", "Header band of episode cards"),
        new("SelectedBand", ThemeValueKind.Color, ThemeKeyGroup.Favorites, "고른 카드의 머리 띠", "Header band of the selected card"),
        new("Selection", ThemeValueKind.Color, ThemeKeyGroup.Favorites, "고른 것 — 카드 테두리 · 범위 사각형 · 고른 선", "Selection — card border, selection rectangle, selected line"),
        new("TabSelectedBackground", ThemeValueKind.Brush, ThemeKeyGroup.Favorites, "고른 탭의 바탕", "Background of the selected tab"),
        new("ChunkyPrimaryBackground", ThemeValueKind.Brush, ThemeKeyGroup.Favorites, "「+ 에피소드」 같은 만들기 단추의 바탕", "Background of create buttons such as “+ Episode”"),
        new("SystemControlForegroundBaseHighBrush", ThemeValueKind.Brush, ThemeKeyGroup.Favorites, "본문 글자의 색", "Colour of body text"),
        new("UiFont", ThemeValueKind.Font, ThemeKeyGroup.Favorites, "본문 글꼴", "Body font"),
        new("DisplayFont", ThemeValueKind.Font, ThemeKeyGroup.Favorites, "제목 글꼴(탭 · 단추 · 카드 제목)", "Title font (tabs, buttons, card titles)"),
        new("FrameFont", ThemeValueKind.Font, ThemeKeyGroup.Favorites, "챕터 · 장면 이름표의 글꼴", "Font of chapter and scene labels"),
        new("FontScale", ThemeValueKind.Number, ThemeKeyGroup.Favorites, "틀의 글자 크기 배수 — 1은 기본 크기, 1.15는 15% 크게(판 위의 글은 따르지 않는다)", "Text size multiplier for panels — 1 is the default size, 1.15 is 15% larger (text on the board doesn't follow)"),
        new("TopBarEdgeHeight", ThemeValueKind.Number, ThemeKeyGroup.Window, "맨 위 띠 아래 줄의 높이(0이면 없다)", "Height of the strip below the top bar (0 hides it)"),
        new("TopBarEdgeMask", ThemeValueKind.Picture, ThemeKeyGroup.Window, "맨 위 띠 아래 줄의 모양 — 그림의 불투명한 자리에 창의 바탕색이 선다(지금은 구름 물결)", "Shape of the strip below the top bar — the window colour shows where the picture is opaque (now: cloud waves)"),
        new("MainTabTrack", ThemeValueKind.Brush, ThemeKeyGroup.Window, "주 탭([대본] · [구성]) 전환 단추의 바닥", "Track behind the main tab switch ([Script] · [Structure])"),
        new("TopBarButtonBorder", ThemeValueKind.Brush, ThemeKeyGroup.Window, "맨 위 띠 위 단추의 테두리", "Border of buttons on the top bar"),
        new("HintSurface", ThemeValueKind.Brush, ThemeKeyGroup.Window, "판 아래 안내 줄의 바탕", "Background of the hint line below the board"),
        new("SystemAccentColor", ThemeValueKind.Color, ThemeKeyGroup.Window, "강조색 — 글 칸의 초점 테두리 · 체크 표시 · 고른 글", "Accent — focused text box border, check marks, selected text"),
        new("ScrollBarThumbBackgroundColor", ThemeValueKind.Color, ThemeKeyGroup.Window, "스크롤 막대의 색", "Colour of scroll bars"),
        new("ToolTipBackground", ThemeValueKind.Brush, ThemeKeyGroup.Window, "풍선 도움말의 바탕", "Background of tooltips"),
        new("ToolTipBorderBrush", ThemeValueKind.Brush, ThemeKeyGroup.Window, "풍선 도움말의 테두리", "Border of tooltips"),
        new("ButtonBackground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "작은 단추의 바탕", "Background of small buttons"),
        new("ButtonBackgroundPointerOver", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "작은 단추에 마우스를 올렸을 때", "Small buttons under the mouse"),
        new("ButtonBorderBrush", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "작은 단추의 테두리", "Border of small buttons"),
        new("ButtonForeground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "작은 단추의 글", "Text of small buttons"),
        new("ToggleButtonBackgroundChecked", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "켜진 단추(굵게 · 정렬 등)의 바탕", "Background of switched-on buttons (bold, alignment …)"),
        new("ChunkyBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "큰 단추의 테두리(아래쪽이 굵다 — 단추의 두께)", "Border of large buttons (thicker at the bottom — the button’s depth)"),
        new("ChunkyBorderHover", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "큰 단추에 마우스를 올렸을 때의 테두리", "Border of large buttons under the mouse"),
        new("ChunkyPrimaryBackgroundHover", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "만들기 단추에 마우스를 올렸을 때", "Create buttons under the mouse"),
        new("ChunkyPrimaryBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "만들기 단추의 테두리", "Border of create buttons"),
        new("ChunkyPrimaryForeground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "만들기 단추의 글", "Text of create buttons"),
        new("ChunkyDangerBackground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "지우기 단추의 바탕", "Background of delete buttons"),
        new("ChunkyDangerBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "지우기 단추의 테두리", "Border of delete buttons"),
        new("ChunkyDangerBorderHover", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "지우기 단추에 마우스를 올렸을 때의 테두리", "Border of delete buttons under the mouse"),
        new("ChunkyDangerForeground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "지우기 단추의 글", "Text of delete buttons"),
        new("ChunkyDisabledBackground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "못 누르는 큰 단추의 바탕", "Background of disabled large buttons"),
        new("ChunkyDisabledBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "못 누르는 큰 단추의 테두리", "Border of disabled large buttons"),
        new("ChunkyDisabledForeground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "못 누르는 큰 단추의 글", "Text of disabled large buttons"),
        new("TabHoverBackground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "탭에 마우스를 올렸을 때", "Tabs under the mouse"),
        new("MainTabBackground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "[대본] · [구성] 탭의 바탕", "Background of the [Script] and [Structure] tabs"),
        new("MainTabBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "[대본] · [구성] 탭의 테두리", "Border of the [Script] and [Structure] tabs"),
        new("MainTabSelectedBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "고른 [대본] · [구성] 탭의 테두리", "Border of the selected [Script]/[Structure] tab"),
        new("TextControlBackground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "글 칸의 바탕", "Background of text boxes"),
        new("TextControlBorderBrush", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "글 칸의 테두리", "Border of text boxes"),
        new("TextControlForeground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "글 칸의 글", "Text in text boxes"),
        new("TextControlPlaceholderForeground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "빈 글 칸의 안내 글", "Placeholder text in empty text boxes"),
        new("CardWritingHoverBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "카드 글 칸에 마우스를 올렸을 때의 테두리", "Card writing box border under the mouse"),
        new("CardWritingFocusBackground", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "카드 글 칸에 쓰는 동안의 바탕", "Card writing box background while writing"),
        new("CardWritingFocusBorder", ThemeValueKind.Brush, ThemeKeyGroup.Controls, "카드 글 칸에 쓰는 동안의 테두리", "Card writing box border while writing"),
        new("RowSelected", ThemeValueKind.Color, ThemeKeyGroup.Controls, "하이어아키에서 고른 줄의 바탕", "Background of the selected hierarchy row"),
        new("BoardDot", ThemeValueKind.Brush, ThemeKeyGroup.Board, "챕터 바깥 책상에 박힌 점의 색(반투명)", "Colour of the dots on the desk around chapters (translucent)"),
        new("BoardDotSpacing", ThemeValueKind.Number, ThemeKeyGroup.Board, "점 사이의 거리 — 배율 100%에서(0이면 점이 없다)", "Distance between dots at 100% zoom (0 hides the dots)"),
        new("BoardDotSize", ThemeValueKind.Number, ThemeKeyGroup.Board, "점의 지름", "Diameter of the dots"),
        new("ChapterFrameBorder", ThemeValueKind.Color, ThemeKeyGroup.Board, "지금 챕터의 테두리", "Border of the current chapter"),
        new("ChapterFrameBorderIdle", ThemeValueKind.Color, ThemeKeyGroup.Board, "다른 챕터의 테두리", "Border of other chapters"),
        new("ChapterFrameShadow", ThemeValueKind.Shadow, ThemeKeyGroup.Board, "챕터 테두리의 그림자", "Shadow of chapter frames"),
        new("ChapterFrameFill", ThemeValueKind.Color, ThemeKeyGroup.Board, "지금 챕터의 바탕", "Fill of the current chapter"),
        new("ChapterFrameFillIdle", ThemeValueKind.Color, ThemeKeyGroup.Board, "다른 챕터의 바탕", "Fill of other chapters"),
        new("ChapterLabel", ThemeValueKind.Color, ThemeKeyGroup.Board, "지금 챕터 이름표의 바탕", "Background of the current chapter’s label"),
        new("ChapterLabelIdle", ThemeValueKind.Color, ThemeKeyGroup.Board, "다른 챕터 이름표의 바탕", "Background of other chapters’ labels"),
        new("ChapterLabelInk", ThemeValueKind.Color, ThemeKeyGroup.Board, "챕터 이름표의 글", "Text of chapter labels"),
        new("SceneFrameBorder", ThemeValueKind.Color, ThemeKeyGroup.Board, "장면 테두리", "Border of scenes"),
        new("SceneFrameFill", ThemeValueKind.Color, ThemeKeyGroup.Board, "장면 바탕", "Fill of scenes"),
        new("SceneLabel", ThemeValueKind.Color, ThemeKeyGroup.Board, "장면 이름표의 바탕", "Background of scene labels"),
        new("SceneLabelBorder", ThemeValueKind.Color, ThemeKeyGroup.Board, "장면 이름표의 테두리", "Border of scene labels"),
        new("SceneLabelInk", ThemeValueKind.Color, ThemeKeyGroup.Board, "장면 이름표의 글", "Text of scene labels"),
        new("Scene", ThemeValueKind.Color, ThemeKeyGroup.Board, "장면 — 접은 장면 상자의 테두리", "Scene — border of collapsed scene boxes"),
        new("SceneProxySurface", ThemeValueKind.Color, ThemeKeyGroup.Board, "접은 장면 상자의 바탕", "Background of collapsed scene boxes"),
        new("ProxySurface", ThemeValueKind.Color, ThemeKeyGroup.Board, "접은 판 상자의 바탕", "Background of collapsed board boxes"),
        new("FloatingSurface", ThemeValueKind.Color, ThemeKeyGroup.Board, "판 위에 뜬 것(미니맵 · 선 모양 고르기)의 바탕", "Background of things floating on the board (minimap, line shape picker)"),
        new("FloatingBorder", ThemeValueKind.Color, ThemeKeyGroup.Board, "판 위에 뜬 것의 테두리", "Border of things floating on the board"),
        new("FloatingDivider", ThemeValueKind.Color, ThemeKeyGroup.Board, "미니맵과 배율 줄 사이의 선", "Line between the minimap and the zoom bar"),
        new("FindBarSurface", ThemeValueKind.Color, ThemeKeyGroup.Board, "찾기 띠의 바탕", "Background of the find bar"),
        new("MinimapZoomHidden", ThemeValueKind.Color, ThemeKeyGroup.Board, "미니맵을 감췄을 때 남는 배율 글", "Zoom text left when the minimap is hidden"),
        new("EpisodeSurface", ThemeValueKind.Color, ThemeKeyGroup.Cards, "에피소드 카드의 바탕", "Background of episode cards"),
        new("EpisodeBorder", ThemeValueKind.Color, ThemeKeyGroup.Cards, "에피소드 카드의 테두리", "Border of episode cards"),
        new("EpisodeAccent", ThemeValueKind.Color, ThemeKeyGroup.Cards, "에피소드 카드의 아이콘 · 왼쪽 막대", "Icon and left bar of episode cards"),
        new("SceneRootBand", ThemeValueKind.Color, ThemeKeyGroup.Cards, "장면 시작 카드의 머리 띠", "Header band of scene-start cards"),
        new("SceneRootSurface", ThemeValueKind.Color, ThemeKeyGroup.Cards, "장면 시작 카드의 바탕", "Background of scene-start cards"),
        new("SceneRootBorder", ThemeValueKind.Color, ThemeKeyGroup.Cards, "장면 시작 카드의 테두리", "Border of scene-start cards"),
        new("SceneRootAccent", ThemeValueKind.Color, ThemeKeyGroup.Cards, "장면 시작 카드의 아이콘 · 왼쪽 막대", "Icon and left bar of scene-start cards"),
        new("BranchBand", ThemeValueKind.Color, ThemeKeyGroup.Cards, "분기 카드의 머리 띠", "Header band of branch cards"),
        new("BranchSurface", ThemeValueKind.Color, ThemeKeyGroup.Cards, "분기 카드의 바탕", "Background of branch cards"),
        new("BranchBorder", ThemeValueKind.Color, ThemeKeyGroup.Cards, "분기 카드의 테두리", "Border of branch cards"),
        new("BranchAccent", ThemeValueKind.Color, ThemeKeyGroup.Cards, "분기 카드의 아이콘 · 왼쪽 막대", "Icon and left bar of branch cards"),
        new("OtherCardBand", ThemeValueKind.Color, ThemeKeyGroup.Cards, "그 밖의 카드의 머리 띠", "Header band of other cards"),
        new("BlockEpisode", ThemeValueKind.Color, ThemeKeyGroup.Cards, "줄인 판의 에피소드 카드 덩어리", "Zoomed-out block of episode cards"),
        new("BlockSceneRoot", ThemeValueKind.Color, ThemeKeyGroup.Cards, "줄인 판의 장면 시작 카드 덩어리", "Zoomed-out block of scene-start cards"),
        new("BlockBranch", ThemeValueKind.Color, ThemeKeyGroup.Cards, "줄인 판의 분기 카드 덩어리", "Zoomed-out block of branch cards"),
        new("BlockOther", ThemeValueKind.Color, ThemeKeyGroup.Cards, "줄인 판의 그 밖의 카드 덩어리", "Zoomed-out block of other cards"),
        new("OtherCardSurface", ThemeValueKind.Color, ThemeKeyGroup.Cards, "그 밖의 카드의 바탕", "Background of other cards"),
        new("OtherCardBorder", ThemeValueKind.Color, ThemeKeyGroup.Cards, "그 밖의 카드의 테두리", "Border of other cards"),
        new("OtherCardAccent", ThemeValueKind.Color, ThemeKeyGroup.Cards, "그 밖의 카드의 아이콘 · 왼쪽 막대", "Icon and left bar of other cards"),
        new("SelectedRibbon", ThemeValueKind.Color, ThemeKeyGroup.Cards, "고른 카드 위의 리본(투명하면 없다)", "Ribbon on the selected card (transparent hides it)"),
        new("CardShadow", ThemeValueKind.Shadow, ThemeKeyGroup.Cards, "카드의 그림자", "Shadow of cards"),
        new("CardSelectedShadow", ThemeValueKind.Shadow, ThemeKeyGroup.Cards, "고른 카드의 그림자", "Shadow of the selected card"),
        new("FrameLabelShadow", ThemeValueKind.Shadow, ThemeKeyGroup.Cards, "챕터 · 장면 이름표의 그림자", "Shadow of chapter and scene labels"),
        new("SlotSurface", ThemeValueKind.Color, ThemeKeyGroup.Choices, "선택지 칸의 바탕", "Background of choice slots"),
        new("SlotInk", ThemeValueKind.Color, ThemeKeyGroup.Choices, "선택지 문구의 글(투명하면 점의 색을 따른다)", "Text of choice labels (transparent follows the dot colour)"),
        new("SlotPlaceholderBorder", ThemeValueKind.Color, ThemeKeyGroup.Choices, "선택지 칸의 테두리", "Border of choice slots"),
        new("SlotAlertSurface", ThemeValueKind.Color, ThemeKeyGroup.Choices, "문구가 없는 선택지 칸(「문구를 적어 주세요」)의 바탕", "Background of a choice slot that has no text yet (“Write the choice text”)"),
        new("SlotLive", ThemeValueKind.Color, ThemeKeyGroup.Choices, "아직 안 이은 선택지 칸의 점", "Dot of a choice slot not yet connected"),
        new("SlotAsleep", ThemeValueKind.Color, ThemeKeyGroup.Choices, "아직 못 쓰는 칸의 테두리", "Border of slots not yet usable"),
        new("Rail", ThemeValueKind.Color, ThemeKeyGroup.Choices, "선택지 줄기 · 가로 줄 · 도착 화살표", "Choice trunk, bus and arrival arrows"),
        new("ChoiceLine1", ThemeValueKind.Color, ThemeKeyGroup.Choices, "첫째 선택지의 선과 문구", "Line and text of the first choice"),
        new("ChoiceLine2", ThemeValueKind.Color, ThemeKeyGroup.Choices, "둘째 선택지의 선과 문구", "Line and text of the second choice"),
        new("ChoiceLine3", ThemeValueKind.Color, ThemeKeyGroup.Choices, "셋째 선택지의 선과 문구", "Line and text of the third choice"),
        new("ChoiceInk", ThemeValueKind.Color, ThemeKeyGroup.Choices, "이어진 선택지의 표", "Marks of connected choices"),
        new("Connection", ThemeValueKind.Color, ThemeKeyGroup.Choices, "자동 길의 선", "Lines of automatic paths"),
        new("Join", ThemeValueKind.Color, ThemeKeyGroup.Choices, "선택지를 끌 때 「잇는다」는 표", "“Connect” badge while dragging a choice"),
        new("BranchHub", ThemeValueKind.Color, ThemeKeyGroup.Choices, "분기 묶음 점", "Branch bundle dot"),
        new("BranchHubOpen", ThemeValueKind.Color, ThemeKeyGroup.Choices, "펼친 분기 묶음 점", "Expanded branch bundle dot"),
        new("BranchLine1", ThemeValueKind.Color, ThemeKeyGroup.Choices, "분기 선 색 1", "Branch line colour 1"),
        new("BranchLine2", ThemeValueKind.Color, ThemeKeyGroup.Choices, "분기 선 색 2", "Branch line colour 2"),
        new("BranchLine3", ThemeValueKind.Color, ThemeKeyGroup.Choices, "분기 선 색 3", "Branch line colour 3"),
        new("BranchLine4", ThemeValueKind.Color, ThemeKeyGroup.Choices, "분기 선 색 4", "Branch line colour 4"),
        new("BranchLine5", ThemeValueKind.Color, ThemeKeyGroup.Choices, "분기 선 색 5", "Branch line colour 5"),
        new("BranchLine6", ThemeValueKind.Color, ThemeKeyGroup.Choices, "분기 선 색 6", "Branch line colour 6"),
        new("StructureDesk", ThemeValueKind.Color, ThemeKeyGroup.Structure, "[구성] 판 바깥", "Area around the [Structure] board"),
        new("StructureBoard", ThemeValueKind.Color, ThemeKeyGroup.Structure, "[구성] 판", "The [Structure] board"),
        new("StructureSelection", ThemeValueKind.Color, ThemeKeyGroup.Structure, "[구성]에서 고른 것", "Selection on the [Structure] board"),
        new("StructureCardSurface", ThemeValueKind.Color, ThemeKeyGroup.Structure, "[구성] 카드의 바탕", "Background of [Structure] cards"),
        new("StructureSlot", ThemeValueKind.Color, ThemeKeyGroup.Structure, "[구성] 카드의 테두리 · 빈 칸 점", "Border and empty-slot dots of [Structure] cards"),
        new("StructureEdge", ThemeValueKind.Color, ThemeKeyGroup.Structure, "조건 없는 길", "Paths without a condition"),
        new("StructureEdgeConditioned", ThemeValueKind.Color, ThemeKeyGroup.Structure, "조건 걸린 길", "Paths with a condition"),
        new("StructureEdgeInk", ThemeValueKind.Color, ThemeKeyGroup.Structure, "길 이름표의 글", "Text of path labels"),
        new("StructureLabelSurface", ThemeValueKind.Color, ThemeKeyGroup.Structure, "길 이름표의 바탕", "Background of path labels"),
        new("StructureBranchChipSurface", ThemeValueKind.Color, ThemeKeyGroup.Structure, "분기 카드 표의 바탕", "Background of branch-card tags"),
        new("StructureInk", ThemeValueKind.Color, ThemeKeyGroup.Structure, "분기 카드 표의 글", "Text of branch-card tags"),
        new("StructureInkSoft", ThemeValueKind.Color, ThemeKeyGroup.Structure, "분기 카드 표의 조건 글", "Condition text of branch-card tags"),
        new("ConditionVisible", ThemeValueKind.Color, ThemeKeyGroup.Structure, "표시조건", "Show conditions"),
        new("ConditionUnlock", ThemeValueKind.Color, ThemeKeyGroup.Structure, "해금조건", "Unlock conditions"),
        new("CardName", ThemeValueKind.Color, ThemeKeyGroup.Structure, "카드 이름", "Card names"),
        new("StatKey", ThemeValueKind.Color, ThemeKeyGroup.Structure, "스탯 키", "Stat keys"),
        new("StatValue", ThemeValueKind.Color, ThemeKeyGroup.Structure, "스탯 값", "Stat values"),
        new("SceneChip", ThemeValueKind.Color, ThemeKeyGroup.Structure, "장면 이름 표", "Scene name tags"),
        new("NoteBorder", ThemeValueKind.Color, ThemeKeyGroup.Structure, "안내 쪽지의 왼쪽 줄", "Left line of guide notes"),
        new("Accent", ThemeValueKind.Color, ThemeKeyGroup.Status, "강조 — 지금 챕터 · 자동 길", "Highlight — current chapter, automatic paths"),
        new("Danger", ThemeValueKind.Color, ThemeKeyGroup.Status, "막힘 · 끊기 · 문구 없음", "Blocked, disconnect, missing text"),
        new("DangerDeep", ThemeValueKind.Color, ThemeKeyGroup.Status, "[구성]의 오류 색", "Error colour on [Structure]"),
        new("DangerText", ThemeValueKind.Color, ThemeKeyGroup.Status, "검증 보고의 오류 글", "Error text in the validation report"),
        new("DangerButton", ThemeValueKind.Color, ThemeKeyGroup.Status, "지우기 확인 단추의 글", "Text of the delete confirmation button"),
        new("Success", ThemeValueKind.Color, ThemeKeyGroup.Status, "이어짐 · 늘어나는 스탯", "Connected, increasing stat"),
        new("Warning", ThemeValueKind.Color, ThemeKeyGroup.Status, "설정 창의 주의 글", "Caution text in Settings"),
        new("WarningText", ThemeValueKind.Color, ThemeKeyGroup.Status, "검증 보고의 경고 글", "Warning text in the validation report"),
        new("WarningMark", ThemeValueKind.Color, ThemeKeyGroup.Status, "하이어아키의 ⚠", "The ⚠ mark in the hierarchy"),
        new("ProblemInk", ThemeValueKind.Color, ThemeKeyGroup.Status, "대사 편집기의 문제 글", "Problem text in the dialogue editor"),
        new("NoticeSurface", ThemeValueKind.Color, ThemeKeyGroup.Status, "「바깥에서 바뀐 프로젝트」 띠의 바탕", "Background of the “changed outside” bar"),
        new("NoticeBorder", ThemeValueKind.Color, ThemeKeyGroup.Status, "그 띠의 테두리", "Border of that bar"),
        new("NoticeInk", ThemeValueKind.Color, ThemeKeyGroup.Status, "그 띠의 글", "Text of that bar"),
        new("OnAccent", ThemeValueKind.Color, ThemeKeyGroup.Other, "색 바탕(표 · 꼬리표) 위의 글", "Text on coloured tags"),
        new("Surface", ThemeValueKind.Color, ThemeKeyGroup.Other, "작은 흰 면 — 점의 안쪽 등", "Small light surfaces — inside of dots etc."),
        new("IconInk", ThemeValueKind.Color, ThemeKeyGroup.Other, "도구 막대의 선 그림", "Line icons in the toolbar"),
        new("Hairline", ThemeValueKind.Color, ThemeKeyGroup.Other, "옅은 테두리 · 구분선", "Faint borders and dividers"),
        new("Divider", ThemeValueKind.Color, ThemeKeyGroup.Other, "틀의 구분선(위 띠 · 도구 막대 · 기둥 사이)", "Frame dividers (top bar, toolbar, between panels)"),
        new("SwatchBorder", ThemeValueKind.Color, ThemeKeyGroup.Other, "색 견본의 테두리", "Border of colour swatches"),
        new("Neutral", ThemeValueKind.Color, ThemeKeyGroup.Other, "곁글 · 중립 표시", "Secondary text and neutral marks"),
        new("NeutralLight", ThemeValueKind.Color, ThemeKeyGroup.Other, "더 옅은 곁글", "Lighter secondary text"),
        new("NeutralDark", ThemeValueKind.Color, ThemeKeyGroup.Other, "더 짙은 곁글", "Darker secondary text"),
        new("CardRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "[대본] 카드의 모서리 둥글기", "Corner roundness of [Script] cards"),
        new("OtherCardRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "그 밖의 카드의 모서리", "Corner roundness of other cards"),
        new("StructureCardRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "[구성] 카드의 모서리", "Corner roundness of [Structure] cards"),
        new("ChapterFrameRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "챕터 테두리의 모서리", "Corner roundness of chapter frames"),
        new("SceneFrameRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "장면 테두리의 모서리", "Corner roundness of scene frames"),
        new("LabelRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "챕터 이름표의 모서리", "Corner roundness of chapter labels"),
        new("ChipRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "작은 표의 모서리", "Corner roundness of small tags"),
        new("SlotRadius", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "선택지 칸의 모서리", "Corner roundness of choice slots"),
        new("FloatingCorner", ThemeValueKind.Corner, ThemeKeyGroup.Shapes, "미니맵 · 찾기 띠의 모서리", "Corner roundness of the minimap and find bar"),
        new("LineWeight", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "판의 선 굵기 배수", "Line thickness multiplier on the board"),
        new("CardSpineOpacity", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "카드 왼쪽 막대의 진하기(0이면 안 보인다)", "Opacity of the card’s left bar (0 hides it)"),
        new("SlotPill", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "1이면 선택지 문구를 둥근 칸에 담는다, 0이면 글만", "1 puts choice text in rounded slots, 0 shows text only"),
        new("LineRowBoxed", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "1이면 인스펙터의 대사 줄마다 상자, 0이면 구분선만", "1 boxes each line in the inspector, 0 uses dividers only"),
        new("ChapterLabelFont", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "챕터 이름표의 글자 크기", "Text size of chapter labels"),
        new("SceneLabelFont", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "장면 이름표의 글자 크기", "Text size of scene labels"),
        new("FrameWeight", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "챕터 · 장면 테두리의 굵기 배수", "Thickness multiplier of chapter and scene frames"),
        new("FrameLabelGrow", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "1이면 이름표를 크고 넉넉하게, 0이면 작게", "1 makes labels large and roomy, 0 small"),
        new("TypeMeta", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "틀의 글자 크기 단계 — 꼬리표 · 줄 번호 · 열 머리(다섯 단계 가운데 하나라도 0이면 단계를 두지 않는다)", "Panel text size step — tags, row numbers, column headers (if any of the five steps is 0, there are no steps)"),
        new("TypeUi", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "틀의 글자 크기 단계 — 단추 · 입력 칸 · 탭", "Panel text size step — buttons, input boxes, tabs"),
        new("TypeBody", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "틀의 글자 크기 단계 — 읽는 글(인스펙터의 대사)", "Panel text size step — text to read (the Inspector's lines)"),
        new("TypeHeading", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "틀의 글자 크기 단계 — 제목", "Panel text size step — headings"),
        new("TypeTitle", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "틀의 글자 크기 단계 — 큰 제목", "Panel text size step — large titles"),
        new("DimLift", ThemeValueKind.Number, ThemeKeyGroup.Shapes, "옅은 곁글을 얼마나 진하게 — 0은 그대로, 1은 본문과 같다", "How much to darken faint secondary text — 0 as is, 1 same as body text")
    ];

    private static readonly Dictionary<string, ThemeKey> ByName =
        All.ToDictionary(key => key.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>이름으로(대소문자 무관) — 모르는 이름이면 null.</summary>
    public static ThemeKey? Find(string name) => ByName.GetValueOrDefault(name.Trim());
}
