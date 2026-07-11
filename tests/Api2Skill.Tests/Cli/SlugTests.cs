using Api2Skill.Cli;

namespace Api2Skill.Tests.Cli;

/// <summary>
/// Adversarial coverage for <see cref="Slug.Create"/> (FR-008 default naming from
/// <c>info.title</c> / <c>--name</c>). No prior test file existed for this unit even though it
/// determines the on-disk output directory name — worth locking down separately from the
/// end-to-end CLI tests in ExitCodeTests.
/// </summary>
public class SlugTests
{
    [Theory]
    [InlineData("Swagger Petstore", "swagger-petstore")]
    [InlineData("  Leading And Trailing Spaces  ", "leading-and-trailing-spaces")]
    [InlineData("Already-Slugged-Name", "already-slugged-name")]
    [InlineData("Multiple   Internal    Spaces", "multiple-internal-spaces")]
    [InlineData("Punctuation!?!@#$%^&*()", "punctuation")]
    public void Create_SlugifiesCommonTitles(string input, string expected) =>
        Assert.Equal(expected, Slug.Create(input));

    [Fact]
    public void Create_PureUnicodeTitle_WithNoAsciiAlphanumerics_FallsBackToSkill()
    {
        // Slug only keeps [a-z0-9] after lowering; a title with no ASCII alphanumeric content
        // at all (e.g. pure CJK, pure emoji) strips to nothing and must fall back to "skill"
        // rather than producing an empty directory name — Path.Combine(".", "") would resolve
        // to the current directory itself, which would be a serious footgun (writing/deleting
        // files at "." under --force).
        Assert.Equal("skill", Slug.Create("你好世界"));
        Assert.Equal("skill", Slug.Create("😀🎉🚀"));
        Assert.Equal("skill", Slug.Create("*** ??? !!!"));
    }

    [Fact]
    public void Create_MixedUnicodeAndAscii_StripsTheNonAsciiCodePoint()
    {
        // A precomposed non-ASCII letter (ü = "ü") is outside [a-z0-9] and is replaced
        // like any other non-alphanumeric run, distinct from its ASCII transliteration — this
        // is a real (if inherent-to-the-design) collision risk worth documenting with a test:
        // "Zürich API" and "Z rich API" both slugify to "z-rich-api", so --force or a distinct
        // --name is the escape hatch, not something Slug itself resolves.
        var withDiaeresis = "Zürich API"; // "Zürich API"
        Assert.Equal("z-rich-api", Slug.Create(withDiaeresis));
    }

    [Fact]
    public void Create_EmptyOrWhitespaceOnlyInput_FallsBackToSkill()
    {
        Assert.Equal("skill", Slug.Create(""));
        Assert.Equal("skill", Slug.Create("   "));
    }

    [Fact]
    public void Create_IsIdempotent_ReSlugifyingAnAlreadySlugifiedNameIsANoOp()
    {
        var once = Slug.Create("My Cool API v2");
        var twice = Slug.Create(once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Create_VeryLongTitle_DoesNotThrow_AndProducesANonEmptySlug()
    {
        var longTitle = string.Concat(Enumerable.Repeat("Very Long API Title Segment ", 200));
        var slug = Slug.Create(longTitle);
        Assert.NotEmpty(slug);
        Assert.DoesNotContain("--", slug, StringComparison.Ordinal);
    }
}
