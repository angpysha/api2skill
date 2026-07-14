using Api2Skill.Examples;

namespace Api2Skill.Tests.Examples;

public class ExampleStoreTests : IDisposable
{
    private readonly string _skillDir = Path.Combine(Path.GetTempPath(), "api2skill-ex-store-" + Guid.NewGuid().ToString("N"));

    public ExampleStoreTests() => Directory.CreateDirectory(_skillDir);

    public void Dispose()
    {
        if (Directory.Exists(_skillDir))
        {
            Directory.Delete(_skillDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Discover_EmptySkill_ReturnsNoItems()
    {
        var result = ExampleStore.Discover(_skillDir);
        Assert.Empty(result.Items);
        Assert.Empty(result.Orphans);
    }

    [Fact]
    public void Write_AndDiscover_RoundTrip()
    {
        ExampleStore.Write(_skillDir, "addPet", "happy", """{"name":"doggie"}""", null, force: false);

        var result = ExampleStore.Discover(_skillDir, knownOperationIds: new HashSet<string> { "addPet" });
        Assert.Single(result.Items);
        Assert.Equal("addPet", result.Items[0].OperationId);
        Assert.Equal("happy", result.Items[0].Name);
        Assert.True(result.Items[0].HasRequest);
        Assert.False(result.Items[0].HasResponse);
        Assert.Empty(result.Orphans);
    }

    [Fact]
    public void Discover_MarksOrphans()
    {
        ExampleStore.Write(_skillDir, "goneOp", "default", """{"a":1}""", null, force: false);
        var result = ExampleStore.Discover(_skillDir, knownOperationIds: new HashSet<string> { "addPet" });
        Assert.Contains("goneOp", result.Orphans);
    }

    [Fact]
    public void Write_WithoutForce_ThrowsWhenExists()
    {
        ExampleStore.Write(_skillDir, "addPet", "happy", "{}", null, force: false);
        Assert.Throws<InvalidOperationException>(() =>
            ExampleStore.Write(_skillDir, "addPet", "happy", """{"x":1}""", null, force: false));
    }

    [Fact]
    public void Remove_DeletesNamedExample()
    {
        ExampleStore.Write(_skillDir, "addPet", "happy", "{}", null, force: false);
        Assert.True(ExampleStore.Remove(_skillDir, "addPet", "happy"));
        Assert.Empty(ExampleStore.Discover(_skillDir).Items);
        Assert.False(Directory.Exists(ExamplePaths.ExamplesRoot(_skillDir)));
    }
}
