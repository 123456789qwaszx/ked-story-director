namespace Vn.App.Views;

/// <summary>
/// 오른쪽 기둥의 탭 셋 — 두 탭([구성] · [대본])이 같은 차례 · 같은 이름이다(side-tabs).
/// 고르면 그것을 보이는 탭으로 간다. 콤보로 챕터를 바꾸는 것은 탭을 바꾸지 않는다.
/// </summary>
internal enum SideTab
{
    /// <summary>고른 하나 — 장면 · 에피소드 · 선택지 · 분기 · 빈 선택지 칸, 그리고 챕터 · 프로젝트의 안내.</summary>
    Inspector,

    /// <summary>프로젝트 전체의 화자 — 모든 챕터가 같은 목록을 쓴다.</summary>
    Speakers,

    /// <summary>챕터 단위의 조건 · 스탯 — 지금 챕터를 보인다.</summary>
    Conditions
}
