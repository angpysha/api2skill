using Api2Skill.Cli;

namespace Api2Skill.Tests.Cli;

public class ConsoleMultiSelectTests
{
    [Fact]
    public void MoveDown_WrapsFromLastToFirst()
    {
        var state = new MultiSelectState(3, initialCursor: 2);
        state.MoveDown();
        Assert.Equal(0, state.Cursor);
    }

    [Fact]
    public void MoveUp_WrapsFromFirstToLast()
    {
        var state = new MultiSelectState(3, initialCursor: 0);
        state.MoveUp();
        Assert.Equal(2, state.Cursor);
    }

    [Fact]
    public void Toggle_SelectsAndDeselectsCurrent()
    {
        var state = new MultiSelectState(4);
        state.MoveDown();
        state.Toggle();
        Assert.Equal([1], state.SelectedIndices());
        state.Toggle();
        Assert.Empty(state.SelectedIndices());
    }

    [Fact]
    public void MultipleToggles_CollectStableIndices()
    {
        var state = new MultiSelectState(4);
        state.Toggle(); // 0
        state.MoveDown();
        state.MoveDown();
        state.Toggle(); // 2
        Assert.Equal([0, 2], state.SelectedIndices());
    }

    [Fact]
    public void Run_EnterReturnsSelected_EscapeReturnsEmpty()
    {
        using var sw = new StringWriter();
        var keys = new Queue<ConsoleKeyInfo>(
        [
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false),
            new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false),
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false),
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false),
        ]);

        var selected = ConsoleMultiSelect.Run(
            ["A", "B", "C"],
            sw,
            () => keys.Dequeue());

        Assert.Equal([0, 1], selected);

        keys = new Queue<ConsoleKeyInfo>(
        [
            new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false),
        ]);
        Assert.Empty(ConsoleMultiSelect.Run(["A"], TextWriter.Null, () => keys.Dequeue()));
    }
}
