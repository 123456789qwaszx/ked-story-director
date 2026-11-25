using System.Globalization;
using System.Runtime.CompilerServices;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>시험은 한국어로 돈다</b>(english-ui E5) — 검증 보고 · 거절 사유 문장(<see cref="Msg"/>)을 한국어로 고정한다. 이 PC의 언어가 영어여도 같은 답이
/// 나오게. 어셈블리가 실리는 순간 한 번 돌아 어떤 시험보다 먼저다. 영어 문장은 표에서 언어를 밝혀 꺼내 본다(<c>MessageTableTests</c>).
/// </summary>
internal static class TestLanguage
{
    [ModuleInitializer]
    internal static void Korean() => Msg.Culture = CultureInfo.GetCultureInfo("ko-KR");
}
