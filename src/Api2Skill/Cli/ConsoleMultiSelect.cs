namespace Api2Skill.Cli;

/// <summary>
/// Pure multi-select state for the interactive host picker (↑↓ move, Space toggle, Enter confirm).
/// UI I/O lives in <see cref="ConsoleMultiSelect"/>; unit tests drive this type directly.
/// </summary>
public sealed class MultiSelectState
{
    private readonly bool[] _selected;

    public MultiSelectState(int itemCount, int initialCursor = 0)
    {
        if (itemCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount));
        }

        _selected = new bool[itemCount];
        Cursor = Math.Clamp(initialCursor, 0, itemCount - 1);
    }

    public int Cursor { get; private set; }

    public int Count => _selected.Length;

    public bool IsSelected(int index) => _selected[index];

    public void MoveUp() => Cursor = Cursor == 0 ? Count - 1 : Cursor - 1;

    public void MoveDown() => Cursor = Cursor == Count - 1 ? 0 : Cursor + 1;

    public void Toggle() => _selected[Cursor] = !_selected[Cursor];

    /// <summary>Indices currently marked selected (stable ascending order).</summary>
    public IReadOnlyList<int> SelectedIndices()
    {
        var list = new List<int>();
        for (var i = 0; i < _selected.Length; i++)
        {
            if (_selected[i])
            {
                list.Add(i);
            }
        }

        return list;
    }
}

/// <summary>
/// Raw-console multi-select (no Spectre.Console — keeps Native AOT / trim surface small).
/// </summary>
public static class ConsoleMultiSelect
{
    /// <summary>
    /// Runs an interactive multi-select. Returns selected indices, or an empty list if the
    /// user confirms with nothing selected (caller decides how to treat that).
    /// </summary>
    public static IReadOnlyList<int> Run(IReadOnlyList<string> labels, TextWriter output, Func<ConsoleKeyInfo> readKey)
    {
        var state = new MultiSelectState(labels.Count);
        Render(labels, state, output, clear: false);

        while (true)
        {
            var key = readKey();
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    state.MoveUp();
                    break;
                case ConsoleKey.DownArrow:
                    state.MoveDown();
                    break;
                case ConsoleKey.Spacebar:
                    state.Toggle();
                    break;
                case ConsoleKey.Enter:
                    return state.SelectedIndices();
                case ConsoleKey.Escape:
                    return [];
                default:
                    continue;
            }

            Render(labels, state, output, clear: true);
        }
    }

    private static void Render(IReadOnlyList<string> labels, MultiSelectState state, TextWriter output, bool clear)
    {
        if (clear)
        {
            // Move cursor up to redraw in place (one line per item + header + footer).
            var lines = labels.Count + 2;
            output.Write($"\x1b[{lines}A");
        }

        output.WriteLine("Select skill roots (↑↓ move, Space toggle, Enter confirm):");
        for (var i = 0; i < labels.Count; i++)
        {
            var marker = state.IsSelected(i) ? "[x]" : "[ ]";
            var cursor = i == state.Cursor ? ">" : " ";
            output.WriteLine($"{cursor} {marker} {labels[i]}");
        }

        output.WriteLine();
    }
}
