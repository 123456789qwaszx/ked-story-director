using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Text;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>화자를 더하면 임시 캐릭터 키가 선다</b> (2026-11-19 소유자 · usability UX29 — 「한글 이름을 로마자로 하되 앞에 temp_를 붙여 임시라는
/// 걸로 · 이름을 고쳐도 따라가는 것은 직관을 깨뜨릴 수 있으니 뺀다」). ⛔ 전에는 키가 빈 채로 섰다.
/// </summary>
public sealed class CharacterKeyTests
{
    [Theory]
    [InlineData("하린", "harin")]
    [InlineData("시구레", "sigure")]
    [InlineData("카스미", "kaseumi")]     // 표준 표기다 — 실제 키(kasmi)와 다를 수 있어 임시다
    [InlineData("샤통", "syatong")]
    [InlineData("윌로", "willo")]         // ㄹㄹ은 ll
    [InlineData("석우", "seogu")]         // 받침이 다음 ㅇ 자리로 넘어간다
    [InlineData("은영", "eunyeong")]      // ㅇ 받침은 넘어가지 않는다
    [InlineData("Mika Lee", "mika_lee")]
    [InlineData("선생님 A", "seonsaengnim_a")]
    [InlineData("??? ", "")]
    [InlineData("花子", "")]
    public void 한글_이름을_로마자로(string name, string expected) =>
        Assert.Equal(expected, HangulRomanizer.Romanize(name));

    [Fact]
    public void 화자를_더하면_temp_와_로마자의_키가_서고_화자와_한_칸이다()
    {
        var editor = new ProjectEditor(new StoryProject());
        long start = editor.Revision;

        Speaker speaker = editor.AddSpeaker("하린");

        Assert.Equal(new Speaker("하린", "temp_harin"), speaker);
        Assert.Equal(start + 1, editor.Revision);

        editor.Undo();
        Assert.Empty(editor.Project.Speakers);
    }

    [Fact]
    public void 겹치면_수를_붙이고_로마자로_바꿀_것이_없으면_char_번호다()
    {
        var editor = new ProjectEditor(new StoryProject());

        editor.AddSpeaker("하린");
        editor.AddSpeaker("Harin");
        editor.AddSpeaker("HARIN!");
        editor.AddSpeaker("???");
        editor.AddSpeaker("!!!");

        Assert.Equal(
            ["temp_harin", "temp_harin2", "temp_harin3", "temp_char01", "temp_char02"],
            editor.Project.Speakers.Select(speaker => speaker.CharacterId));
    }

    [Fact]
    public void 이름을_고쳐도_키는_따라가지_않는다()
    {
        // 소유자 — 따라가면 직관을 깨뜨릴 수 있다. 키는 더할 때 한 번 짓는다.
        var editor = new ProjectEditor(new StoryProject());
        editor.AddSpeaker("하린");

        editor.RenameSpeaker("하린", "하린이");

        Assert.Equal(new Speaker("하린이", "temp_harin"), Assert.Single(editor.Project.Speakers));
    }

    [Fact]
    public void 임시_키는_앞머리로_가린다()
    {
        // [구성] 「화자」 탭이 이것으로 ⚠를 붙인다 — 검증 보고는 짚지 않는다(usability UX30).
        Assert.True(CharacterKey.IsTemp("temp_harin"));
        Assert.False(CharacterKey.IsTemp("harin"));
        Assert.False(CharacterKey.IsTemp(string.Empty));
        Assert.False(CharacterKey.IsTemp(null));
    }
}
