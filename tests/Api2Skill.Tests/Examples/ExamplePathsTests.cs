using Api2Skill.Examples;

namespace Api2Skill.Tests.Examples;

public class ExamplePathsTests
{
    [Theory]
    [InlineData("addPet", true)]
    [InlineData("find-by-status", true)]
    [InlineData("a", true)]
    [InlineData("..", false)]
    [InlineData("foo/bar", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSafePathSegment_Validates(string? value, bool expected) =>
        Assert.Equal(expected, ExamplePaths.IsSafePathSegment(value));

    [Theory]
    [InlineData("default", true)]
    [InlineData("happy", true)]
    [InlineData("a", true)]
    [InlineData("happy-path", true)]
    [InlineData("Happy", false)]
    [InlineData("-bad", false)]
    [InlineData("bad-", false)]
    [InlineData("", false)]
    public void IsValidName_RequiresSlug(string? value, bool expected) =>
        Assert.Equal(expected, ExamplePaths.IsValidName(value));

    [Fact]
    public void NormalizeName_DefaultsWhenMissing()
    {
        Assert.Equal("default", ExamplePaths.NormalizeName(null));
        Assert.Equal("default", ExamplePaths.NormalizeName("  "));
        Assert.Equal("happy", ExamplePaths.NormalizeName("happy"));
    }

    [Fact]
    public void RelativeLinkFromReference_UsesExamplesPrefix()
    {
        Assert.Equal(
            "../examples/addPet/happy/request.json",
            ExamplePaths.RelativeLinkFromReference("addPet", "happy", ExamplePaths.RequestFileName));
    }
}
