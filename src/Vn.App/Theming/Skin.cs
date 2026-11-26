using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace Vn.App.Theming;

/// <summary>
/// <b>테마가 정하는 색 · 모양의 이름표</b>(2026-11-26 · themes) — 화면 코드는 색을 숫자로 적지 않고 여기서 이름으로 꺼낸다. 값은 켜진 테마의
/// <c>Themes/{테마}/Tokens.axaml</c>에 같은 이름(속성 이름 = 리소스 키)으로 있다. 테마가 적지 않은 이름은 기본 테마(<c>Themes/Classic</c>)의 값이다.
///
/// 이름을 하나 더하려면: 여기 속성 한 줄 + <c>Themes/Classic/Tokens.axaml</c>에 같은 키 한 줄. 다른 테마는 바꾸고 싶을 때만 적는다.
/// 시험(<c>ThemeTokenTests</c>)이 여기의 이름이 모두 기본 테마에 있는지 · 테마마다 값의 꼴이 맞는지를 본다.
///
/// 색은 <see cref="Color"/>로 주고 붓은 쓰는 곳이 만든다 — <c>Skin.Selection.Brush()</c> · 투명도를 줄 때 <c>Skin.Selection.Brush(42)</c>
/// (<see cref="SkinColorExtensions"/>). ⚠ 값을 정적 필드에 담아 두지 않는다 — 테마가 서기 전에 꺼낸 값이 굳는다(속성으로 그때그때 묻는다).
/// </summary>
internal static class Skin
{
    // ── 공통 ─────────────────────────────────────────────

    /// <summary>색 바탕(표 · 꼬리표) 위의 글.</summary>
    public static Color OnAccent => C();

    /// <summary>흰 바탕 — 선택지 칸 · 점 안쪽 · [구성] 판.</summary>
    public static Color Surface => C();

    /// <summary>도구 막대의 선 그림.</summary>
    public static Color IconInk => C();

    /// <summary>옅은 테두리 · 구분선의 바탕색(쓰는 곳이 투명도를 준다).</summary>
    public static Color Hairline => C();

    /// <summary>틀의 구분선(위 띠 · 툴바 · 기둥 사이).</summary>
    public static Color Divider => C();

    /// <summary>색 견본의 테두리(쓰는 곳이 투명도를 준다).</summary>
    public static Color SwatchBorder => C();

    /// <summary>곁글 · 중립 표시.</summary>
    public static Color Neutral => C();

    /// <summary>더 옅은 중립 — 빈 칸의 안내 글 · 잔상.</summary>
    public static Color NeutralLight => C();

    /// <summary>짙은 중립 — 눈 그림.</summary>
    public static Color NeutralDark => C();

    // ── 상태 ─────────────────────────────────────────────

    /// <summary>고른 것 — 카드 테두리 · 범위 사각형 · 고른 선 · 미니맵 창.</summary>
    public static Color Selection => C();

    /// <summary>선택지를 끌어 「잇는다」는 표(흰 글이 올라간다).</summary>
    public static Color Join => C();

    /// <summary>강조 — 지금 챕터 · 하이어아키에서 고른 줄 · 자동 길 · [구성]에서 고른 것.</summary>
    public static Color Accent => C();

    /// <summary>막힘 · 끊기 · 문구 없음.</summary>
    public static Color Danger => C();

    /// <summary>[구성]의 오류 색 — 문구 없는 길 · 줄어드는 스탯.</summary>
    public static Color DangerDeep => C();

    /// <summary>검증 보고의 오류 글 · 오류 카드 테두리.</summary>
    public static Color DangerText => C();

    /// <summary>지우기 확인 단추의 글.</summary>
    public static Color DangerButton => C();

    /// <summary>이어짐 · 늘어나는 스탯.</summary>
    public static Color Success => C();

    /// <summary>설정 창의 주의 글.</summary>
    public static Color Warning => C();

    /// <summary>검증 보고의 경고 글.</summary>
    public static Color WarningText => C();

    /// <summary>하이어아키의 ⚠.</summary>
    public static Color WarningMark => C();

    /// <summary>대사 편집기의 문제 글.</summary>
    public static Color ProblemInk => C();

    // ── 틀 ─────────────────────────────────────────────

    /// <summary>판 위에 뜬 것 — 미니맵 · 선 모양 고르기.</summary>
    public static Color FloatingSurface => C();

    /// <summary>판 위에 뜬 것의 테두리.</summary>
    public static Color FloatingBorder => C();

    /// <summary>판 위에 뜬 것 안의 구분선(미니맵과 배율 줄 사이).</summary>
    public static Color FloatingDivider => C();

    /// <summary>찾기 띠.</summary>
    public static Color FindBarSurface => C();

    /// <summary>「바깥에서 바뀐 프로젝트」 띠의 바탕.</summary>
    public static Color NoticeSurface => C();

    /// <summary>그 띠의 테두리.</summary>
    public static Color NoticeBorder => C();

    /// <summary>그 띠의 글.</summary>
    public static Color NoticeInk => C();

    /// <summary>[구성] 안내 쪽지의 왼쪽 줄.</summary>
    public static Color NoteBorder => C();

    // ── [대본] 판 ─────────────────────────────────────────────

    // ⛔ 장면 한 색(Scene) · 접은 장면 상자의 바탕(SceneProxySurface)은 2026-11-26에 걷었다 — 장면마다 색이 갈린다(SceneTint1 ~ · SceneMark1 ~).

    /// <summary>접은 판 상자의 바탕.</summary>
    public static Color ProxySurface => C();

    /// <summary>에피소드 카드 — 등뼈 · 아이콘.</summary>
    public static Color EpisodeAccent => C();

    /// <summary>에피소드 카드 — 바탕.</summary>
    public static Color EpisodeSurface => C();

    /// <summary>에피소드 카드 — 테두리.</summary>
    public static Color EpisodeBorder => C();

    /// <summary>장면 시작 카드 — 등뼈 · 아이콘.</summary>
    public static Color SceneRootAccent => C();

    /// <summary>장면 시작 카드 — 바탕.</summary>
    public static Color SceneRootSurface => C();

    /// <summary>장면 시작 카드 — 테두리.</summary>
    public static Color SceneRootBorder => C();

    /// <summary>분기 카드 — 등뼈 · 아이콘.</summary>
    public static Color BranchAccent => C();

    /// <summary>분기 카드 — 바탕.</summary>
    public static Color BranchSurface => C();

    /// <summary>분기 카드 — 테두리.</summary>
    public static Color BranchBorder => C();

    /// <summary>그 밖의 카드 — 등뼈 · 아이콘.</summary>
    public static Color OtherCardAccent => C();

    /// <summary>그 밖의 카드 — 바탕.</summary>
    public static Color OtherCardSurface => C();

    /// <summary>그 밖의 카드 — 테두리.</summary>
    public static Color OtherCardBorder => C();

    /// <summary>안 이은 선택지 칸의 점.</summary>
    public static Color SlotLive => C();

    /// <summary>아직 못 쓰는 칸의 테두리.</summary>
    public static Color SlotAsleep => C();

    /// <summary>빈 선택지 칸의 테두리.</summary>
    public static Color SlotPlaceholderBorder => C();

    /// <summary>챕터 간선 철도의 선.</summary>
    public static Color Rail => C();

    /// <summary>이어진 선택지 — 철도 표 · [구성]의 점.</summary>
    public static Color ChoiceInk => C();

    /// <summary>보통 길(자동 길)의 선.</summary>
    public static Color Connection => C();

    /// <summary>분기 묶음 점.</summary>
    public static Color BranchHub => C();

    /// <summary>펼친 분기 묶음 점.</summary>
    public static Color BranchHubOpen => C();

    /// <summary>미니맵을 감췄을 때 남는 배율 글.</summary>
    public static Color MinimapZoomHidden => C();

    /// <summary>분기 선 색 1 — 파랑.</summary>
    public static Color BranchLine1 => C();

    /// <summary>분기 선 색 2 — 보라.</summary>
    public static Color BranchLine2 => C();

    /// <summary>분기 선 색 3 — 초록.</summary>
    public static Color BranchLine3 => C();

    /// <summary>분기 선 색 4 — 주황.</summary>
    public static Color BranchLine4 => C();

    /// <summary>분기 선 색 5 — 자홍.</summary>
    public static Color BranchLine5 => C();

    /// <summary>분기 선 색 6 — 청록.</summary>
    public static Color BranchLine6 => C();

    // ── [대본] 판 — 테두리 · 머리 띠 · 선택지 칸 ─────────────────────────────────────────────

    /// <summary>지금 챕터의 테두리.</summary>
    public static Color ChapterFrameBorder => C();

    /// <summary>다른 챕터의 테두리.</summary>
    public static Color ChapterFrameBorderIdle => C();

    /// <summary>지금 챕터의 바탕.</summary>
    public static Color ChapterFrameFill => C();

    /// <summary>다른 챕터의 바탕.</summary>
    public static Color ChapterFrameFillIdle => C();

    /// <summary>지금 챕터 이름표의 바탕.</summary>
    public static Color ChapterLabel => C();

    /// <summary>다른 챕터 이름표의 바탕.</summary>
    public static Color ChapterLabelIdle => C();

    /// <summary>챕터 이름표의 글.</summary>
    public static Color ChapterLabelInk => C();

    /// <summary>장면 테두리.</summary>
    public static Color SceneFrameBorder => C();

    /// <summary>
    /// 장면 바탕 — 팔레트 칸 1 ~ 5(세이지 · 장미 · 민트 · 연두 · 라일락 · design/scene-colors.md). <b>비치는 색</b>이다 — 챕터 바탕 위에 한 겹이면 견본의 옅은 색이고,
    /// 장면이 겹친 자리는 한 겹 더 진해진다(같은 색이면 진한 그 색, 다른 색이면 섞인 색 · 2026-11-26 소유자 — 「같은 색의 장면끼리 겹칠 때, 이게 아예 구분이
    /// 안 가는데 … 색을 겹치게 해서」). 칸은 <see cref="SceneTintOf"/>로 고른다.
    /// </summary>
    public static Color SceneTint1 => C();

    /// <summary>장면 바탕 — 칸 2(장미). <see cref="SceneTint1"/> 참조.</summary>
    public static Color SceneTint2 => C();

    /// <summary>장면 바탕 — 칸 3(민트).</summary>
    public static Color SceneTint3 => C();

    /// <summary>장면 바탕 — 칸 4(연두).</summary>
    public static Color SceneTint4 => C();

    /// <summary>장면 바탕 — 칸 5(라일락).</summary>
    public static Color SceneTint5 => C();

    /// <summary>장면의 점 — 칸 1 ~ 5의 진한 색(장면 이름표 · 접은 장면 상자 · 하이어아키 · [구성] 꼬리표). 옅은 바탕은 작은 점으로 보이지 않는다.</summary>
    public static Color SceneMark1 => C();

    /// <summary>장면의 점 — 칸 2.</summary>
    public static Color SceneMark2 => C();

    /// <summary>장면의 점 — 칸 3.</summary>
    public static Color SceneMark3 => C();

    /// <summary>장면의 점 — 칸 4.</summary>
    public static Color SceneMark4 => C();

    /// <summary>장면의 점 — 칸 5.</summary>
    public static Color SceneMark5 => C();

    /// <summary>그 칸의 장면 바탕(1 ~ 5 · 밖이면 1).</summary>
    public static Color SceneTintOf(int color) => color switch
    {
        2 => SceneTint2,
        3 => SceneTint3,
        4 => SceneTint4,
        5 => SceneTint5,
        _ => SceneTint1
    };

    /// <summary>그 칸의 장면 점(1 ~ 5 · 밖이면 1).</summary>
    public static Color SceneMarkOf(int color) => color switch
    {
        2 => SceneMark2,
        3 => SceneMark3,
        4 => SceneMark4,
        5 => SceneMark5,
        _ => SceneMark1
    };

    /// <summary>장면 이름표의 바탕.</summary>
    public static Color SceneLabel => C();

    /// <summary>장면 이름표의 테두리(<see cref="FrameLabelGrow"/>일 때만 선다).</summary>
    public static Color SceneLabelBorder => C();

    /// <summary>장면 이름표의 글.</summary>
    public static Color SceneLabelInk => C();

    /// <summary>에피소드 카드 — 머리 띠(투명이면 띠를 짓지 않는다).</summary>
    public static Color EpisodeBand => C();

    /// <summary>장면 시작 카드 — 머리 띠.</summary>
    public static Color SceneRootBand => C();

    /// <summary>분기 카드 — 머리 띠.</summary>
    public static Color BranchBand => C();

    /// <summary>그 밖의 카드 — 머리 띠.</summary>
    public static Color OtherCardBand => C();

    /// <summary>고른 카드 — 머리 띠.</summary>
    public static Color SelectedBand => C();

    /// <summary>줄인 판의 덩어리(design/board-zoom.md 3절) — 에피소드 카드. 장면 바탕과 대비 3:1 이상.</summary>
    public static Color BlockEpisode => C();

    /// <summary>줄인 판의 덩어리 — 장면 시작 카드.</summary>
    public static Color BlockSceneRoot => C();

    /// <summary>줄인 판의 덩어리 — 분기 카드.</summary>
    public static Color BlockBranch => C();

    /// <summary>줄인 판의 덩어리 — 그 밖의 카드.</summary>
    public static Color BlockOther => C();

    /// <summary>선택지 칸의 바탕.</summary>
    public static Color SlotSurface => C();

    /// <summary>선택지 문구의 글(투명이면 점의 색을 따른다).</summary>
    public static Color SlotInk => C();

    /// <summary>하이어아키에서 고른 줄의 바탕.</summary>
    public static Color RowSelected => C();

    /// <summary>이어졌는데 문구가 없는 선택지 칸(「문구를 적어 주세요」)의 바탕 — 테두리 · 글은 <see cref="Danger"/>다.</summary>
    public static Color SlotAlertSurface => C();

    /// <summary>선택지의 길 색 — 카드 아래 칸의 차례대로 첫째. 이은 칸의 문구와 그 선이 같은 색이다(문구가 어느 선의 것인지 · 선이 어디로 가는지).</summary>
    public static Color ChoiceLine1 => C();

    /// <summary>선택지의 길 색 — 둘째 칸.</summary>
    public static Color ChoiceLine2 => C();

    /// <summary>선택지의 길 색 — 셋째 칸(넷째부터는 첫째로 돌아간다).</summary>
    public static Color ChoiceLine3 => C();

    // ── [구성] 판 ─────────────────────────────────────────────

    /// <summary>[구성] 판(카드가 놓이는 면).</summary>
    public static Color StructureBoard => C();

    /// <summary>[구성]에서 고른 것 — 카드 · 분기 표 · 빈 칸 점 · 길.</summary>
    public static Color StructureSelection => C();

    /// <summary>고른 [대본] 카드 위의 리본(투명이면 짓지 않는다).</summary>
    public static Color SelectedRibbon => C();

    /// <summary>판 바깥 책상.</summary>
    public static Color StructureDesk => C();

    /// <summary>에피소드 카드의 바탕.</summary>
    public static Color StructureCardSurface => C();

    /// <summary>카드 테두리 · 빈 칸 점 · 들어오는 점.</summary>
    public static Color StructureSlot => C();

    /// <summary>조건 없는 길.</summary>
    public static Color StructureEdge => C();

    /// <summary>조건 걸린 길 · 그 길이 닿는 카드 테두리.</summary>
    public static Color StructureEdgeConditioned => C();

    /// <summary>길 이름표의 글.</summary>
    public static Color StructureEdgeInk => C();

    /// <summary>길 이름표의 바탕.</summary>
    public static Color StructureLabelSurface => C();

    /// <summary>분기 카드 표의 바탕.</summary>
    public static Color StructureBranchChipSurface => C();

    /// <summary>분기 카드 표의 글.</summary>
    public static Color StructureInk => C();

    /// <summary>분기 카드 표의 조건 글.</summary>
    public static Color StructureInkSoft => C();

    /// <summary>표시조건.</summary>
    public static Color ConditionVisible => C();

    /// <summary>해금조건.</summary>
    public static Color ConditionUnlock => C();

    /// <summary>카드 이름.</summary>
    public static Color CardName => C();

    /// <summary>스탯 키.</summary>
    public static Color StatKey => C();

    /// <summary>스탯 값.</summary>
    public static Color StatValue => C();

    /// <summary>
    /// 분기 카드로 가는 글(「⑂ 이름」). ⛔ [구성] 카드의 장면 꼬리표는 2026-11-27부터 그 장면의 색이다(<see cref="SceneMarkOf"/> · design/scene-colors.md ⑥) —
    /// 그 전에는 이 색(회색)이었다.
    /// </summary>
    public static Color SceneChip => C();

    // ── 모양 ─────────────────────────────────────────────

    /// <summary>[대본] 카드의 모서리.</summary>
    public static double CardRadius => N();

    /// <summary>그 밖의 카드의 모서리.</summary>
    public static double OtherCardRadius => N();

    /// <summary>챕터 테두리의 모서리.</summary>
    public static double ChapterFrameRadius => N();

    /// <summary>장면 테두리의 모서리.</summary>
    public static double SceneFrameRadius => N();

    /// <summary>챕터 이름표의 모서리.</summary>
    public static double LabelRadius => N();

    /// <summary>작은 표(장면 이름표 · 분기 조건 표)의 모서리.</summary>
    public static double ChipRadius => N();

    /// <summary>[대본] 카드 왼쪽 등뼈 막대의 진하기(0이면 안 보인다 — 자리는 그대로).</summary>
    public static double CardSpineOpacity => N();

    /// <summary>[대본] 판의 길(선택지 선 · 분기 선)의 굵기 배수.</summary>
    public static double LineWeight => N();

    /// <summary>선택지 칸의 모서리.</summary>
    public static double SlotRadius => N();

    /// <summary>1이면 적힌 선택지 문구도 알약 칸에 담는다.</summary>
    public static double SlotPill => N();

    /// <summary>고른 [대본] 카드의 그림자(테두리 밖의 고리까지) — 테마가 적지 않으면 <see cref="CardShadow"/>와 같다.</summary>
    public static BoxShadows CardSelectedShadow =>
        ThemeService.ShadowsOf(nameof(CardSelectedShadow)) is { Count: > 0 } ring ? ring : CardShadow;

    /// <summary>챕터 이름표의 글자 크기(판 위의 글 — 배율을 따른다).</summary>
    public static double ChapterLabelFont => N();

    /// <summary>장면 이름표의 글자 크기.</summary>
    public static double SceneLabelFont => N();

    /// <summary>1이면 챕터 · 장면 이름표의 여백을 넉넉히 하고 장면 이름표에 테두리를 두른다(선 위에 걸터앉는 이름표), 0이면 기본.</summary>
    public static double FrameLabelGrow => N();

    /// <summary>챕터 · 장면 테두리의 굵기 배수 — 기본 테마의 굵기(지금 챕터 1.6 · 다른 챕터 1 · 장면 1)에 곱한다.</summary>
    public static double FrameWeight => N();

    /// <summary>[구성] 카드의 모서리.</summary>
    public static double StructureCardRadius => N();

    /// <summary>1이면 인스펙터의 대사 줄마다 테두리 상자를 두르고, 0이면 줄 아래 구분선만 둔다.</summary>
    public static double LineRowBoxed => N();

    /// <summary>판 위에 뜬 것(미니맵 · 찾기 띠)의 모서리.</summary>
    public static CornerRadius FloatingCorner => ThemeService.CornerOf(nameof(FloatingCorner));

    // ── 곁글의 진하기 ─────────────────────────────────────────────

    /// <summary>
    /// 곁글(투명도로 낮춘 글)을 얼마나 되올리는가 — 0이면 그대로, 1이면 본문과 같다. 코드는 <see cref="Dim"/>으로 쓰고, XAML은 이 값에서 셈한
    /// <c>Dim45</c> ~ <c>Dim70</c>(기본 0.45 ~ 0.7)을 <c>Opacity="{DynamicResource Dim60}"</c>로 쓴다(<see cref="ThemeService"/>가 테마를 입힐 때 셈한다).
    /// </summary>
    public static double DimLift => N();

    /// <summary>
    /// 틀의 곁글의 진하기 — 기본 테마의 투명도를 주면 테마가 정한 만큼 되올린다(기본 테마는 준 값 그대로). 옅은 바탕색의 테마에서 곁글이
    /// 읽히지 않을 만큼 흐려지지 않게 한다.
    /// </summary>
    public static double Dim(double opacity) => opacity + ((1 - opacity) * DimLift);

    // ── 글자 크기(틀) ─────────────────────────────────────────────

    /// <summary>
    /// 틀(하이어아키 · 인스펙터 · 설정 창 · 도구 막대)의 글자 크기 배수 — 코드는 <see cref="Font"/>로 쓰고, XAML은 이 값에서 셈한 단계
    /// <c>TextXxs</c> 9 · <c>TextXs</c> 10 · <c>TextSm</c> 11 · <c>TextMd</c> 12 · <c>TextLg</c> 14(기본 테마의 크기)를 <c>{DynamicResource TextSm}</c>으로 쓴다
    /// (<see cref="ThemeService"/>가 테마를 입힐 때 셈한다). 판 위의 글은 따르지 않는다.
    /// </summary>
    public static double FontScale => N();

    /// <summary>
    /// 틀의 글자 크기 — 기본 테마의 크기를 주면 테마의 배수를 곱해 0.5 단위로 맞추고, 테마가 크기 단계를 두면(<see cref="TypeMeta"/> ~ <see cref="TypeTitle"/>)
    /// 그 값 이상인 가장 작은 단계로 올린다(가장 큰 단계를 넘으면 가장 큰 단계). 기본 테마는 단계가 없어 준 값 그대로다.
    /// 판 위의 글(카드 · 선택지 칸 · 이름표)에는 쓰지 않는다 — 그쪽은 치수가 정해져 있고 배율로 본다.
    /// </summary>
    public static double Font(double size)
    {
        double scaled = Math.Round(size * FontScale * 2, MidpointRounding.AwayFromZero) / 2;
        double[] steps = [TypeMeta, TypeUi, TypeBody, TypeHeading, TypeTitle];

        if (steps.Any(step => step <= 0))
        {
            return scaled;
        }

        Array.Sort(steps);

        foreach (double step in steps)
        {
            if (step >= scaled)
            {
                return step;
            }
        }

        return steps[^1];
    }

    /// <summary>
    /// 틀의 글자 크기 단계(2026-11-26 — 디자인 점검 1번 「크기가 제각각」): 꼬리표 · 줄 번호 · 열 머리(<see cref="TypeMeta"/>) · 단추 · 입력 · 탭(<see cref="TypeUi"/>) ·
    /// 읽는 글(<see cref="TypeBody"/>) · 제목(<see cref="TypeHeading"/>) · 큰 제목(<see cref="TypeTitle"/>). 틀의 글자는 이 다섯 크기 가운데 하나로 선다(<see cref="Font"/>).
    /// 하나라도 0이면 단계를 두지 않는다(기본 테마).
    /// </summary>
    public static double TypeMeta => N();

    /// <inheritdoc cref="TypeMeta"/>
    public static double TypeUi => N();

    /// <inheritdoc cref="TypeMeta"/>
    public static double TypeBody => N();

    /// <inheritdoc cref="TypeMeta"/>
    public static double TypeHeading => N();

    /// <inheritdoc cref="TypeMeta"/>
    public static double TypeTitle => N();

    /// <summary>챕터 · 장면 이름표의 그림자 — 기본 테마는 없다.</summary>
    public static BoxShadows FrameLabelShadow => ThemeService.ShadowsOf(nameof(FrameLabelShadow));

    /// <summary>[대본] 카드의 그림자 — 기본 테마는 없다.</summary>
    public static BoxShadows CardShadow => ThemeService.ShadowsOf(nameof(CardShadow));

    /// <summary>챕터 테두리의 그림자 — 챕터의 면이 판의 바탕에서 떠 보이게(없으면 none).</summary>
    public static BoxShadows ChapterFrameShadow => ThemeService.ShadowsOf(nameof(ChapterFrameShadow));

    /// <summary>분기 선의 색 여섯 — <see cref="Views.BranchPalette"/>가 차례로 쓴다.</summary>
    public static IReadOnlyList<Color> BranchLines =>
        [BranchLine1, BranchLine2, BranchLine3, BranchLine4, BranchLine5, BranchLine6];

    private static Color C([CallerMemberName] string key = "") => ThemeService.ColorOf(key);

    private static double N([CallerMemberName] string key = "") => ThemeService.NumberOf(key);
}

/// <summary>색에서 붓을 만든다 — 같은 색은 같은 붓 하나를 돌려 쓴다(테마가 바뀌면 비운다 · <see cref="ThemeService.BrushOf"/>).</summary>
internal static class SkinColorExtensions
{
    /// <summary>이 색의 붓.</summary>
    public static IBrush Brush(this Color color) => ThemeService.BrushOf(color);

    /// <summary>이 색에 투명도(0 ~ 255)를 준 붓.</summary>
    public static IBrush Brush(this Color color, byte alpha) => ThemeService.BrushOf(color.Alpha(alpha));

    /// <summary>이 색에 투명도(0 ~ 255)를 준 색.</summary>
    public static Color Alpha(this Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
