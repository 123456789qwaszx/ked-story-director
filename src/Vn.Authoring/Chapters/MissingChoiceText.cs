namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>문구가 빈 선택지가 게임에 나갈 때의 임시 문구</b> (2026-11-26 소유자 — 「선택지부터 먼저 원하는 것에 연결하고, 그 다음에서야 선택지 문구를
/// 쓰고 싶어지는 경우가 많다」 · 「경고 + 이유 + 도착 에피소드 ID」).
///
/// 자동 길이 아닌 길에 문구가 없으면 검증 보고의 <b>경고</b>(<see cref="ChapterDiagnosticCode.OptionLabelBlank"/>)이고, 진행 JSON에는 이 문구가
/// <c>ChoiceLabel</c>로 나간다 — 그 챕터를 게임에서 시험해 볼 수 있고, 덜 쓴 선택지가 화면에서 한눈에 보인다. ⛔ 그 전에는 오류라 그 챕터의
/// 진행 JSON · 대사 Yarn이 통째로 나가지 않았다. 빈 문구 그대로는 못 낸다(런타임 계약 — 자동 길이 아닌 길은 문구가 있어야 한다), 선택지를 빼면
/// 뒤 선택지의 <c>OptionIndex</c>(서버 이력의 열쇠)가 밀린다.
///
/// <b>영어 한 벌 · ASCII만이다.</b> 화면 언어를 따르면 PC마다 내보낸 바이트(버전 해시)가 갈린다. <c>⚠</c> · <c>→</c> 같은 기호는 게임 글꼴에
/// 없으면 네모로 깨진다. 도착은 <b>에피소드 ID</b>다 — 제목은 바뀌고(바꿀 때마다 해시가 갈린다) 길어 단추를 넘친다.
/// </summary>
public static class MissingChoiceText
{
    /// <summary>임시 문구의 머리 — 이 뒤에 도착 에피소드 ID가 붙는다.</summary>
    public const string Prefix = "[TODO] Missing choice text -> ";

    /// <summary>그 에피소드로 가는, 문구가 빈 선택지의 임시 문구.</summary>
    public static string For(string toEpisodeId) => Prefix + toEpisodeId;
}
