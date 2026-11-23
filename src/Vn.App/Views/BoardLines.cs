using Vn.App.Services;
using Vn.Authoring.Model;

namespace Vn.App.Views;

/// <summary>
/// <b>[대본] 판에 새로 잇는 선의 꼴</b> — 직각 · 곡선 · 직선(2026-11-23 소유자 — 「대본탭의 간선의 형태도, 설정에서 직선, 곡선, 직각 중에 기본 생성을 선택할 수 있게
/// 하여 주십시오」 · 설정 창 「기타」의 「새로 잇는 선」). 이 PC의 앱 설정이다(<see cref="AppSettingsService.LoadNewLineShape"/>). 선택지 길 · 분기 선이 <b>생길 때</b> 그
/// 꼴이 선에 적히고(프로젝트 파일 · 같이 쓰는 사람에게도 같다), 뒤에 바꿔도 이미 있는 선은 그대로다(<see cref="Vn.Authoring.Editing.ProjectEditor.NewLineShape"/>).
/// 선의 「↺ 자동」도 이 꼴의 처음 자리로 되돌린다. 처음 값은 직각(그 전의 모습)이다.
/// </summary>
internal static class BoardLines
{
    private static EdgeShapeKind? _kind;

    /// <summary>새로 잇는 선의 꼴.</summary>
    internal static EdgeShapeKind Kind => _kind ??= Parse(AppSettingsService.LoadNewLineShape());

    /// <summary>그 꼴의 처음 모양(굽이 없음) — 「↺ 자동」이 되돌리는 모양.</summary>
    internal static EdgeShape Default => new(Kind);

    /// <summary>바꾸고 기억한다. 같으면 아무것도 하지 않는다. 다음에 잇는 선부터 따른다.</summary>
    internal static void Set(EdgeShapeKind kind)
    {
        if (kind == Kind)
        {
            return;
        }

        _kind = kind;
        AppSettingsService.SaveNewLineShape(kind == EdgeShapeKind.Elbow ? null : kind.ToString().ToLowerInvariant());
    }

    /// <summary>앱 설정의 글 — 모르는 글 · 없음은 직각.</summary>
    private static EdgeShapeKind Parse(string? text) =>
        Enum.TryParse(text, ignoreCase: true, out EdgeShapeKind kind) && Enum.IsDefined(kind) ? kind : EdgeShapeKind.Elbow;
}
