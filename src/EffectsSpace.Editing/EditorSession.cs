using EffectsSpace.Core;
using EffectsSpace.Documents;

namespace EffectsSpace.Editing;

public enum ChangeKind { Document, Selection, Time, Preview, KeySelection }

public sealed class EditorSession
{
    private sealed record SelectionState(string[] Layers, string[] Keys, string Property, string? ActiveKey);
    private sealed record HistoryEntry(string Name, ProjectSnapshot Before, ProjectSnapshot After, SelectionState BeforeSelection, SelectionState AfterSelection);
    private readonly List<HistoryEntry> _undo = [], _redo = [];
    private readonly HashSet<string> _selectedKeys = new(StringComparer.Ordinal);
    private ProjectSnapshot? _before;
    private SelectionState? _beforeSelection;
    private string _transactionName = "";
    private string? _activeKey;
    private long _revision;
    public MotionProject Project { get; private set; }
    public Composition Composition => Project.Compositions.First(c => c.Id == Project.ActiveCompositionId);
    public HashSet<string> SelectedIds { get; } = new(StringComparer.Ordinal);
    public IReadOnlySet<string> SelectedKeyIds => _selectedKeys;
    /// <summary>Compatibility single-key accessor. Assigning it replaces the key selection.</summary>
    public string? SelectedKeyId
    {
        get => _activeKey is not null && _selectedKeys.Contains(_activeKey) ? _activeKey : _selectedKeys.FirstOrDefault();
        set { _selectedKeys.Clear(); if (value is not null) _selectedKeys.Add(value); _activeKey = value; }
    }
    public string Property { get; set; } = "X";
    public KeyframeClipboard? KeyClipboard { get; internal set; }
    public double Time { get; private set; }
    public long Revision => _revision;
    public bool AutoKey { get; set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string UndoName => _undo.LastOrDefault()?.Name ?? "";
    public string RedoName => _redo.LastOrDefault()?.Name ?? "";
    public bool IsEditing => _before is not null;
    public event Action<ChangeKind>? Changed;
    public IEnumerable<Layer> Selection => Composition.Layers.Where(l => SelectedIds.Contains(l.Id));
    public Layer? Primary => Selection.FirstOrDefault();

    public EditorSession(MotionProject project) { ProjectValidator.Validate(project); Project = project; }
    public void Select(string? id, bool additive = false)
    {
        if (!additive) SelectedIds.Clear();
        if (id is not null && Composition.Layers.Any(l => l.Id == id))
        { if (additive && SelectedIds.Contains(id)) SelectedIds.Remove(id); else SelectedIds.Add(id); }
        SelectedKeyId = null; NormalizeSelection(); Changed?.Invoke(ChangeKind.Selection);
    }
    public void SelectAll()
    {
        SelectedIds.Clear(); SelectedKeyId = null;
        foreach (var layer in Composition.Layers.Where(l => !l.Locked)) SelectedIds.Add(layer.Id);
        NormalizeSelection(); Changed?.Invoke(ChangeKind.Selection);
    }
    public void SelectKeys(IEnumerable<string> ids, bool additive = false)
    {
        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        var entries = Composition.Layers.SelectMany(l => LayerChannels.Enumerate(l)
            .SelectMany(p => p.Channel.Keys.Where(k => wanted.Contains(k.Id)).Select(k => (Layer: l, Property: p, Key: k)))).ToArray();
        var oldLayers = SelectedIds.ToHashSet(StringComparer.Ordinal);
        if (!additive) { _selectedKeys.Clear(); if (entries.Length > 0) SelectedIds.Clear(); }
        foreach (var entry in entries)
        { SelectedIds.Add(entry.Layer.Id); _selectedKeys.Add(entry.Key.Id); _activeKey = entry.Key.Id; }
        if (entries.Length > 0) Property = entries[0].Property.Path;
        NormalizeSelection();
        Changed?.Invoke(oldLayers.SetEquals(SelectedIds) ? ChangeKind.KeySelection : ChangeKind.Selection);
    }
    public void ToggleKey(string id)
    {
        if (!_selectedKeys.Remove(id)) { SelectKeys([id], true); return; }
        if (_activeKey == id) _activeKey = _selectedKeys.FirstOrDefault();
        Changed?.Invoke(ChangeKind.KeySelection);
    }
    public void ClearKeySelection() { _selectedKeys.Clear(); _activeKey = null; Changed?.Invoke(ChangeKind.KeySelection); }
    public void SetTime(double time)
    {
        if (!double.IsFinite(time)) return;
        var next = Math.Clamp(Composition.FrameRate.Snap(time), 0, Composition.LastFrameTime);
        if (Math.Abs(next - Time) < 1e-9) return;
        Time = next; Changed?.Invoke(ChangeKind.Time);
    }
    public void Activate(string compositionId)
    {
        if (IsEditing) CancelEdit();
        if (!Project.Compositions.Any(c => c.Id == compositionId)) throw new ArgumentException("Unknown composition.");
        Project.ActiveCompositionId = compositionId; SelectedIds.Clear(); SelectedKeyId = null; Property = "X";
        Time = Math.Min(Time, Composition.LastFrameTime); Changed?.Invoke(ChangeKind.Document);
    }
    public void BeginEdit(string name)
    {
        if (_before is not null) throw new InvalidOperationException("An edit transaction is already active.");
        _before = ProjectSnapshot.Capture(Project); _beforeSelection = CaptureSelection(); _transactionName = name;
    }
    public void PreviewChanged() => Changed?.Invoke(ChangeKind.Preview);
    public void CommitEdit()
    {
        if (_before is null) return;
        try
        {
            ProjectValidator.Validate(Project); NormalizeSelection();
            var after = ProjectSnapshot.Capture(Project);
            if (after.Json != _before.Json || !after.Assets.Keys.Order().SequenceEqual(_before.Assets.Keys.Order()))
            {
                _undo.Add(new(_transactionName, _before, after, _beforeSelection!, CaptureSelection())); _redo.Clear();
                while (_undo.Count > 100 || _undo.Sum(e => (long)e.Before.Json.Length + e.After.Json.Length) > 32 * 1024 * 1024) _undo.RemoveAt(0);
                _revision++;
            }
            _before = null; _beforeSelection = null;
        }
        catch { CancelEdit(); throw; }
        Changed?.Invoke(ChangeKind.Document);
    }
    public void CancelEdit()
    {
        if (_before is null) return;
        Project = _before.Restore(); _before = null;
        RestoreSelection(_beforeSelection!); _beforeSelection = null; Changed?.Invoke(ChangeKind.Document);
    }
    public void Edit(string name, Action change)
    {
        BeginEdit(name);
        try { change(); CommitEdit(); } catch { CancelEdit(); throw; }
    }
    public void Undo()
    {
        if (IsEditing) CancelEdit(); if (_undo.Count == 0) return;
        var item = _undo[^1] with { AfterSelection = CaptureSelection() };
        _undo.RemoveAt(_undo.Count - 1); _redo.Add(item);
        Project = item.Before.Restore(); _revision++; RestoreSelection(item.BeforeSelection); Changed?.Invoke(ChangeKind.Document);
    }
    public void Redo()
    {
        if (IsEditing) CancelEdit(); if (_redo.Count == 0) return;
        var item = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); _undo.Add(item);
        Project = item.After.Restore(); _revision++; RestoreSelection(item.AfterSelection); Changed?.Invoke(ChangeKind.Document);
    }
    public void Replace(MotionProject project)
    {
        ProjectValidator.Validate(project); _before = null; _beforeSelection = null; _undo.Clear(); _redo.Clear();
        Project = project; SelectedIds.Clear(); SelectedKeyId = null; Property = "X"; Time = 0; _revision++; Changed?.Invoke(ChangeKind.Document);
    }
    private SelectionState CaptureSelection() => new(SelectedIds.ToArray(), _selectedKeys.ToArray(), Property, SelectedKeyId);
    private void RestoreSelection(SelectionState state)
    {
        SelectedIds.Clear(); SelectedIds.UnionWith(state.Layers); _selectedKeys.Clear(); _selectedKeys.UnionWith(state.Keys);
        _activeKey = state.ActiveKey; Property = state.Property; NormalizeSelection();
    }
    private void NormalizeSelection()
    {
        SelectedIds.IntersectWith(Composition.Layers.Select(l => l.Id));
        _selectedKeys.IntersectWith(Selection.SelectMany(LayerChannels.Enumerate).SelectMany(p => p.Channel.Keys).Select(k => k.Id));
        if (_activeKey is not null && !_selectedKeys.Contains(_activeKey)) _activeKey = _selectedKeys.FirstOrDefault();
        if (Primary is { } primary && LayerChannels.Find(primary, Property) is null) Property = "X";
        Time = Math.Clamp(Time, 0, Composition.LastFrameTime);
    }
}
