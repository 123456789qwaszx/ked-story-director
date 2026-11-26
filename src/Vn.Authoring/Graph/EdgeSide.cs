namespace Vn.Authoring.Graph;

/// <summary>
/// <b>선이 나가는 쪽</b> — 도착 카드가 출발 카드보다 왼쪽이면 왼쪽으로 나간다 (H1 · 2026-10-29 소유자: *"연결되어있는
/// 루트의 현재 연결되어 있는 하나의 루트의 선택지 방향이 바뀌길 원한거야"*).
///
/// 선 하나하나가 제 도착을 본다 — 한 카드의 선택지 셋이 서로 다른 쪽으로 나갈 수 있다. 재는 기준은 <b>선이 실제로 나가는 자리</b>다 —
/// 카드 안 실행 포트(분기 · IF 갈래)는 카드의 가운데(<see cref="GoesLeft"/>), 카드 아래 선택지 줄(점 · 문구 · 레인)은 선택지가 달린
/// 줄기(<see cref="ChoiceGoesLeft"/>).
///
/// 처음(G1 · G2)에는 <b>챕터 시작 에피소드의 카드</b>(「뿌리」)를 기준으로 카드째 뒤집었다 — 소유자가 뜻한 것은 그게
/// 아니었다(*"자기 루트보다 왼쪽에 있냐 오른쪽에 있냐에 따라서 자신의 선택지 방향이 바뀌는데, 내가 원한 건 그게 아니라"*).
/// </summary>
public static class EdgeSide
{
    /// <summary>
    /// 두 카드의 <b>지금 폭으로 잰 가운데</b>끼리 잰다 — 도착의 가운데가 출발의 가운데보다 왼쪽이면 왼쪽. 같으면 오른쪽(늘 쓰던 쪽)이다
    /// (2026-11-19 소유자 — 「에피소드의 현재 가로넓이를 기준으로」 · usability UX6). ⛔ 그 전에는 카드 왼쪽 X끼리 쟀다 — 「카드 폭이 같아
    /// 가운데끼리 잰 것과 같다」던 규칙이라, 카드를 넓힐 수 있게 된 뒤로는 넓힌 카드 아래 한참 오른쪽의 도착도 「오른쪽」이었다.
    /// </summary>
    public static bool GoesLeft(double sourceLeft, double sourceWidth, double targetLeft, double targetWidth) =>
        targetLeft + (targetWidth / 2) < sourceLeft + (sourceWidth / 2);

    /// <summary>
    /// 카드 아래 <b>선택지 줄</b>의 쪽 — 도착의 가운데(선이 닿는 자리)가 <b>선택지가 달린 줄기</b>보다 왼쪽이면 왼쪽. 같으면 오른쪽이다
    /// (2026-11-26 소유자 — 「선택지가 달려있는 에피소드의 좌우 넓이에 영향을 받다보니, 실제 선택지 위치 대비 다음 에피소드 위치상 왼쪽에 있어야
    /// 할 것인데도 오른쪽으로 방향이 생기는 등 이상한 느낌」). 줄기는 카드를 넓혀도 카드 왼쪽에서 같은 자리라, 출발 카드의 폭은 쪽에 끼어들지 않는다.
    /// ⛔ 그 전에는 출발 카드의 지금 폭의 가운데에서 쟀다(<see cref="GoesLeft"/> · 2026-11-19) — 줄기를 카드 왼쪽으로 옮긴 뒤(2026-11-25)로는 넓힌 카드에서
    /// 줄기와 가운데가 멀어져, 선택지 바로 오른쪽 아래의 도착으로도 선이 왼쪽으로 나갔다.
    /// </summary>
    public static bool ChoiceGoesLeft(double trunkX, double targetLeft, double targetWidth) =>
        targetLeft + (targetWidth / 2) < trunkX;
}
