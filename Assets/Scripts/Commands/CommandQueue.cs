// ============================================================================
// CommandQueue.cs — Local command list management
// This is a pure C# class (not MonoBehaviour). Each player has one locally.
// The queue is serialized to a string array for network transmission.
// ============================================================================

using System;
using System.Collections.Generic;

public class CommandQueue
{
    public const int MaxCommands = 10;

    private readonly List<CommandType> commands = new List<CommandType>();

    /// <summary>Fired whenever the queue contents change.</summary>
    public event Action OnQueueChanged;

    // ── Properties ──────────────────────────────────────────────────────
    public int Count => commands.Count;
    public bool IsFull => commands.Count >= MaxCommands;
    public bool IsEmpty => commands.Count == 0;
    public IReadOnlyList<CommandType> Commands => commands.AsReadOnly();

    // ── Mutation ─────────────────────────────────────────────────────────

    /// <summary>Append a command. Returns false if the queue is full.</summary>
    public bool Add(CommandType command)
    {
        if (IsFull) return false;
        commands.Add(command);
        OnQueueChanged?.Invoke();
        return true;
    }

    /// <summary>Remove the command at the given index.</summary>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= commands.Count) return false;
        commands.RemoveAt(index);
        OnQueueChanged?.Invoke();
        return true;
    }

    /// <summary>Clear all commands from the queue.</summary>
    public void Clear()
    {
        commands.Clear();
        OnQueueChanged?.Invoke();
    }

    /// <summary>Swap two commands for reordering.</summary>
    public void Swap(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= commands.Count) return;
        if (indexB < 0 || indexB >= commands.Count) return;

        CommandType temp = commands[indexA];
        commands[indexA] = commands[indexB];
        commands[indexB] = temp;
        OnQueueChanged?.Invoke();
    }

    /// <summary>Get a copy of the command list.</summary>
    public List<CommandType> ToList()
    {
        return new List<CommandType>(commands);
    }

    // ── Serialization (for network transmission) ─────────────────────────

    /// <summary>Convert to string array for ServerRpc transmission.</summary>
    public string[] ToStringArray()
    {
        string[] result = new string[commands.Count];
        for (int i = 0; i < commands.Count; i++)
        {
            result[i] = commands[i].ToString();
        }
        return result;
    }

    /// <summary>Parse a string array back into a command list.</summary>
    public static List<CommandType> FromStringArray(string[] data)
    {
        var result = new List<CommandType>();
        if (data == null) return result;

        foreach (string s in data)
        {
            if (Enum.TryParse(s, out CommandType cmd))
                result.Add(cmd);
        }
        return result;
    }

    /// <summary>Compact comma-separated format: "MoveForward,TurnLeft,MoveForward"</summary>
    public string ToCommaSeparatedString()
    {
        return string.Join(",", ToStringArray());
    }

    /// <summary>Parse from comma-separated format.</summary>
    public static List<CommandType> FromCommaSeparatedString(string data)
    {
        if (string.IsNullOrEmpty(data)) return new List<CommandType>();
        return FromStringArray(data.Split(','));
    }
}
