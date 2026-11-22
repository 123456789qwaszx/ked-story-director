namespace Vn.Authoring.Model;

/// <summary>
/// 화자 하나 — 대본에 적히는 이름과 초상화 · 표정이 붙는 캐릭터 키. 모든 챕터가 같은 목록을 쓴다(<see cref="StoryProject.Speakers"/>).
/// 이름은 [대본] 탭이, 캐릭터 키는 [구성] 탭이 고친다 — 바꾸는 길은 <see cref="Editing.ProjectEditor"/>의 화자 명령뿐이다.
/// </summary>
/// <param name="Name">대본에 적히는 화자명 그대로. Ordinal 정확 일치로 찾는다.</param>
/// <param name="CharacterId">초상화 매니페스트의 캐릭터 키. 비어 있으면 초상화가 붙지 않는다(오류가 아니다).</param>
/// <param name="Color">
/// 이름 색(작가용 — 2026-11-23 소유자 · writer-formatting WF-D15) — [대본] 카드 글 칸에서 이 화자의 이름이 이 색으로 보인다. 없으면 보통 글자색이다.
/// 작가가 보기 위한 것이라 내보내기(대사 Yarn · 진행 JSON)는 읽지 않는다.
/// </param>
public sealed record Speaker(string Name, string CharacterId, WriterRgb? Color = null);
