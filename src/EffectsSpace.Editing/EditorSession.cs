using EffectsSpace.Core;
using EffectsSpace.Documents;

namespace EffectsSpace.Editing;

public enum ChangeKind { Document, Selection, Time, Preview }

public sealed class EditorSession
{
    private sealed record HistoryEntry(string Name, ProjectSnapshot Before, ProjectSnapshot After);
    private readonly List<HistoryEntry> _undo = [], _redo = [];
    private ProjectSnapshot? _before;
    private string _transactionName = "";
    private long _revision;
    public MotionProject Project { get; private set; }
    public Composition Composition => Project.Compositions.First(c => c.Id == Project.ActiveCompositionId);
    public HashSet<string> SelectedIds { get; } = new(StringComparer.Ordinal);
    public string? SelectedKeyId { get; set; }
    public string Property { get; set; } = "X";
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
        SelectedKeyId = null; Changed?.Invoke(ChangeKind.Selection);
    }
    public void SelectAll() { SelectedIds.Clear(); foreach (var l in Composition.Layers.Where(l => !l.Locked)) SelectedIds.Add(l.Id); Changed?.Invoke(ChangeKind.Selection); }
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
        Project.ActiveCompositionId = compositionId; SelectedIds.Clear(); Time = Math.Min(Time, Composition.LastFrameTime); Changed?.Invoke(ChangeKind.Document);
    }
    public void BeginEdit(string name)
    {
        if (_before is not null) throw new InvalidOperationException("An edit transaction is already active.");
        _before = ProjectSnapshot.Capture(Project); _transactionName = name;
    }
    public void PreviewChanged() => Changed?.Invoke(ChangeKind.Preview);
    public void CommitEdit()
    {
        if (_before is null) return;
        try
        {
            ProjectValidator.Validate(Project);
            var after = ProjectSnapshot.Capture(Project);
            if (after.Json != _before.Json || !after.Assets.Keys.Order().SequenceEqual(_before.Assets.Keys.Order()))
            {
                _undo.Add(new(_transactionName, _before, after)); _redo.Clear();
                while (_undo.Count > 100 || _undo.Sum(e => (long)e.Before.Json.Length + e.After.Json.Length) > 32 * 1024 * 1024) _undo.RemoveAt(0);
                _revision++;
            }
            _before = null; NormalizeSelection();
        }
        catch { CancelEdit(); throw; }
        Changed?.Invoke(ChangeKind.Document);
    }
    public void CancelEdit()
    {
        if (_before is null) return;
        Project = _before.Restore(); _before = null; NormalizeSelection(); Changed?.Invoke(ChangeKind.Document);
    }
    public void Edit(string name, Action change)
    {
        BeginEdit(name);
        try { change(); CommitEdit(); }
        catch { CancelEdit(); throw; }
    }
    public void Undo()
    {
        if (IsEditing) CancelEdit();
        if (_undo.Count == 0) return;
        var item = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); _redo.Add(item);
        Project = item.Before.Restore(); _revision++; NormalizeSelection(); Changed?.Invoke(ChangeKind.Document);
    }
    public void Redo()
    {
        if (IsEditing) CancelEdit();
        if (_redo.Count == 0) return;
        var item = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); _undo.Add(item);
        Project = item.After.Restore(); _revision++; NormalizeSelection(); Changed?.Invoke(ChangeKind.Document);
    }
    public void Replace(MotionProject project)
    {
        ProjectValidator.Validate(project); _before = null; _undo.Clear(); _redo.Clear();
        Project = project; SelectedIds.Clear(); Time = 0; _revision++; Changed?.Invoke(ChangeKind.Document);
    }
    private void NormalizeSelection()
    {
        SelectedIds.IntersectWith(Composition.Layers.Select(l => l.Id));
        Time = Math.Clamp(Time, 0, Composition.LastFrameTime);
    }
}
