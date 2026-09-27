namespace EffectsSpace.Core;

public sealed class Channel
{
    public double Value { get; set; }
    public string Expression { get; set; } = "";
    public List<Keyframe> Keys { get; set; } = [];
    public Channel() { }
    public Channel(double value) => Value = value;
    public void SetKey(double time, double value, Interpolation interpolation = Interpolation.Bezier)
    {
        if (!double.IsFinite(time) || !double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(time));
        var existing = Keys.Find(k => Math.Abs(k.Time - time) < 1e-8);
        if (existing is not null) { existing.Value = value; return; }
        Keys.Add(new Keyframe { Time = time, Value = value, Interpolation = interpolation });
        Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
    }
}
