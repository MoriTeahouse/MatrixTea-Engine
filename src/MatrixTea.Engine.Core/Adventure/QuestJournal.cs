// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Adventure;

public sealed record QuestDefinition(string Id, IReadOnlyList<string> Prerequisites);

/// <summary>Validated quest dependency graph. Completion is idempotent; saved IDs survive content expansion.</summary>
public sealed class QuestJournal
{
    private readonly Dictionary<string, QuestDefinition> _definitions;
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    public QuestJournal(IEnumerable<QuestDefinition> definitions, IEnumerable<string>? completed = null)
    {
        ArgumentNullException.ThrowIfNull(definitions); _definitions = new(StringComparer.Ordinal);
        foreach (var item in definitions)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (_definitions.Count >= 256 || string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 256 || item.Prerequisites == null || item.Prerequisites.Count > 256 || !_definitions.TryAdd(item.Id, new(item.Id, item.Prerequisites.ToArray())))
                throw new ArgumentException("Quest IDs must be unique and nonempty.", nameof(definitions));
        }
        var visiting = new HashSet<string>(); var visited = new HashSet<string>();
        foreach (string id in _definitions.Keys) Visit(id, visiting, visited);
        if (completed != null) foreach (string id in completed) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Invalid saved quest ID."); _completed.Add(id); }
    }
    public bool IsCompleted(string id) => _completed.Contains(id);
    public bool IsAvailable(string id) => _definitions.TryGetValue(id, out var item) && !_completed.Contains(id) && item.Prerequisites.All(_completed.Contains);
    public bool Complete(string id)
    {
        if (!_definitions.ContainsKey(id)) throw new KeyNotFoundException(id);
        return IsAvailable(id) && _completed.Add(id);
    }
    public string[] ExportCompleted() => _completed.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    private void Visit(string id, HashSet<string> visiting, HashSet<string> visited)
    {
        if (visited.Contains(id)) return;
        if (!_definitions.TryGetValue(id, out var definition)) throw new ArgumentException($"Missing prerequisite: {id}");
        if (!visiting.Add(id)) throw new ArgumentException($"Quest cycle: {id}");
        foreach (string prerequisite in definition.Prerequisites) Visit(prerequisite, visiting, visited);
        visiting.Remove(id); visited.Add(id);
    }
}
