using EffectsSpace.Animation;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Rendering;

var passed = 0; var failed = 0;
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL {name}: {e}"); } }
void Equal(double expected, double actual, double tolerance = 1e-6) { if (Math.Abs(expected - actual) > tolerance || !double.IsFinite(actual)) throw new Exception($"Expected {expected}, got {actual}."); }
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected an exception."); }
EditorSession Session() => new(MotionProject.Empty());
Channel Curve(Interpolation mode = Interpolation.Linear) => new() { Keys = [new() { Time = 0, Value = 10, Interpolation = mode }, new() { Time = 2, Value = 30 }] };

Test("rational frame time", () => Equal(1001d / 30000, new FrameRate(30000, 1001).Seconds(1)));
Test("frame round trip", () => { var r = new FrameRate(30000, 1001); for (var i = 0; i < 100000; i += 137) Equal(i, r.Frame(r.Seconds(i))); });
Test("timecode", () => Check(new FrameRate(30, 1).Timecode(62.5) == "00:01:02:15"));
Test("negative timecode", () => Check(new FrameRate(24, 1).Timecode(-2) == "00:00:00:00"));
Test("axis roundtrip", () => { var a = new TimelineAxis(330, 81, 2.3); Equal(7.1, a.ToTime(a.ToX(7.1))); });
Test("axis snap", () => Equal(1d / 30, new TimelineAxis(0, 100, 0).SnapX(4, new(30, 1))));
Test("constant channel", () => Equal(42, CurveEvaluator.Evaluate(new(42), 7)));
Test("linear curve", () => Equal(20, CurveEvaluator.Evaluate(Curve(), 1)));
Test("curve before first", () => Equal(10, CurveEvaluator.Evaluate(Curve(), -5)));
Test("curve after last", () => Equal(30, CurveEvaluator.Evaluate(Curve(), 10)));
Test("hold curve", () => Equal(10, CurveEvaluator.Evaluate(Curve(Interpolation.Hold), 1.99)));
Test("hold at next key", () => Equal(30, CurveEvaluator.Evaluate(Curve(Interpolation.Hold), 2)));
Test("bezier midpoint", () => Equal(20, CurveEvaluator.Evaluate(Curve(Interpolation.Bezier), 1)));
Test("bezier slow start", () => Check(CurveEvaluator.Evaluate(Curve(Interpolation.Bezier), .2) < CurveEvaluator.Evaluate(Curve(), .2)));
Test("bezier solves horizontal coordinate", () => Equal(.5, CurveEvaluator.Bezier(.5, .1, 0, .9, 1)));
Test("key insertion sorted", () => { var c = Curve(); c.SetKey(1, 99); Equal(1, c.Keys[1].Time); Equal(99, CurveEvaluator.Evaluate(c, 1)); });
Test("key replacement no duplicate", () => { var c = Curve(); c.SetKey(2, 55); Equal(2, c.Keys.Count); Equal(55, CurveEvaluator.Evaluate(c, 2)); });
Test("reject NaN key", () => Throws(() => Curve().SetKey(double.NaN, 1)));
Test("loopOut wraps", () => { var c = Curve(); c.Expression = "loopOut()"; Equal(20, CurveEvaluator.Evaluate(c, 3)); });
Test("loopOut preserves final key", () => { var c = Curve(); c.Expression = "loopOut()"; Equal(30, CurveEvaluator.Evaluate(c, 2)); });
foreach (var (expression, expected) in new (string, double)[] { ("1 + 2 * 3", 7), ("(1 + 2) * 3", 9), ("-2 * -3", 6), ("sin(pi / 2)", 1), ("cos(0)", 1), ("time * 10 + value", 25), ("index + 1", 4), ("clamp(20, 0, 10)", 10), ("linear(5, 0, 10, 0, 100)", 50), ("ease(5, 0, 10, 0, 100)", 50), ("min(3, 9) + max(2, 5)", 8), ("sqrt(9) + abs(-2)", 5) })
    Test("expression " + expression, () => { Check(ScalarExpression.TryEvaluate(expression, 2, 5, 3, out var value, out _)); Equal(expected, value); });
foreach (var expression in new[] { "1 / 0", "sqrt(-1)", "fetch('x')", "while(true){}", "time.foo", "sin(1,2)", "linear(1,0,0,1,2)", new string('(', 80) + "1" + new string(')', 80), "unknown", "1e9999", "" })
    Test("reject expression " + expression[..Math.Min(30, expression.Length)], () => Check(!ScalarExpression.TryEvaluate(expression, 0, 0, 1, out _, out _)));
Test("deterministic wiggle", () => { Check(ScalarExpression.TryEvaluate("wiggle(2, 20)", 1.4, 100, 1, out var a, out _)); Check(ScalarExpression.TryEvaluate("wiggle(2, 20)", 1.4, 100, 1, out var b, out _)); Equal(a, b); Check(a >= 80 && a <= 120); });
Test("invalid expression falls back", () => { var c = Curve(); c.Expression = "bad()"; Equal(20, CurveEvaluator.Evaluate(c, 1)); });
Test("sample validates", () => ProjectValidator.Validate(SampleProject.Create()));
Test("sample roundtrip", () => { var p = SampleProject.Create(); Check(ProjectJson.Save(p) == ProjectJson.Save(ProjectJson.Load(ProjectJson.Save(p)))); });
Test("empty project valid", () => ProjectValidator.Validate(MotionProject.Empty()));
Test("reject wrong schema", () => { var p = MotionProject.Empty(); p.SchemaVersion = 99; Throws(() => ProjectValidator.Validate(p)); });
Test("reject invalid dimensions", () => { var p = MotionProject.Empty(); p.Compositions[0].Width = 0; Throws(() => ProjectValidator.Validate(p)); });
Test("reject missing active comp", () => { var p = MotionProject.Empty(); p.ActiveCompositionId = "missing"; Throws(() => ProjectValidator.Validate(p)); });
Test("reject corrupt JSON", () => Throws(() => ProjectJson.Load("{broken")));
Test("reject zero fps", () => { var p = MotionProject.Empty(); p.Compositions[0].FrameRate = new(0, 1); Throws(() => ProjectValidator.Validate(p)); });
Test("reject invalid work area", () => { var p = MotionProject.Empty(); p.Compositions[0].WorkEnd = 99; Throws(() => ProjectValidator.Validate(p)); });
Test("reject missing media", () => { var p = MotionProject.Empty(); p.Compositions[0].Layers.Add(new() { Kind = LayerKind.Image }); Throws(() => ProjectValidator.Validate(p)); });
Test("reject duplicate identifiers", () => { var p = MotionProject.Empty(); var l = new Layer(); p.Compositions[0].Layers.Add(l); p.Compositions[0].Layers.Add(l); Throws(() => ProjectValidator.Validate(p)); });
Test("reject parent cycle", () => { var p = MotionProject.Empty(); var a = new Layer(); var b = new Layer(); a.ParentId = b.Id; b.ParentId = a.Id; p.Compositions[0].Layers = [a, b]; Throws(() => ProjectValidator.Validate(p)); });
Test("reject composition cycle", () => { var p = MotionProject.Empty(); p.Compositions[0].Layers.Add(new() { Kind = LayerKind.Composition, SourceId = p.Compositions[0].Id }); Throws(() => ProjectValidator.Validate(p)); });
Test("reject zero stretch", () => { var p = MotionProject.Empty(); p.Compositions[0].Layers.Add(new() { Stretch = 0 }); Throws(() => ProjectValidator.Validate(p)); });
Test("reject duplicate key times", () => { var p = MotionProject.Empty(); var l = new Layer(); l.Transform.X.Keys = [new() { Time = 0 }, new() { Time = 0 }]; p.Compositions[0].Layers.Add(l); Throws(() => ProjectValidator.Validate(p)); });
Test("local transform anchor", () => { var c = new Composition(); var l = new Layer(); c.Layers.Add(l); l.Transform.X.Value = 100; l.Transform.AnchorX.Value = 20; Equal(80, TransformEvaluator.ToWorld(c, l, 0, new(0, 0)).X); });
Test("parent transform", () => { var c = new Composition(); var a = new Layer(); var b = new Layer { ParentId = a.Id }; a.Transform.X.Value = 100; b.Transform.X.Value = 20; c.Layers = [a, b]; Equal(120, TransformEvaluator.ToWorld(c, b, 0, new(0, 0)).X); });
Test("inverse world transform", () => { var c = new Composition(); var l = new Layer(); c.Layers.Add(l); l.Transform.X.Value = 500; l.Transform.Rotation.Value = 37; var p = new Vec2(20, 30); var q = TransformEvaluator.ToLocal(c, l, 0, TransformEvaluator.ToWorld(c, l, 0, p))!.Value; Equal(p.X, q.X, .001); Equal(p.Y, q.Y, .001); });
Test("singular transform cannot hit", () => { var c = new Composition(); var l = new Layer(); c.Layers.Add(l); l.Transform.ScaleX.Value = 0; Check(TransformEvaluator.ToLocal(c, l, 0, new(0, 0)) is null); });
Test("add and undo", () => { var s = Session(); s.AddLayer(LayerKind.Text); Equal(1, s.Composition.Layers.Count); s.Undo(); Equal(0, s.Composition.Layers.Count); s.Redo(); Equal(1, s.Composition.Layers.Count); });
Test("edit rollback on invalid mutation", () => { var s = Session(); Throws(() => s.Edit("bad", () => s.Composition.Width = -1)); Equal(1920, s.Composition.Width); Check(!s.IsEditing && !s.CanUndo); });
Test("edit rollback on exception", () => { var s = Session(); Throws(() => s.Edit("bad", () => { s.Composition.Width = 5; throw new Exception(); })); Equal(1920, s.Composition.Width); });
Test("cancel drag restores state", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle); s.BeginEdit("drag"); s.Primary!.Transform.X.Value = 9; s.CancelEdit(); Equal(960, s.Primary!.Transform.X.Value); });
Test("no-op has no history", () => { var s = Session(); s.Edit("noop", () => { }); Check(!s.CanUndo); });
Test("redo invalidated by new edit", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle); s.Undo(); s.AddLayer(LayerKind.Text); Check(!s.CanRedo); });
Test("time is not an edit", () => { var s = Session(); s.SetTime(2.4); Check(!s.CanUndo); Equal(2.4, s.Time); });
Test("time clamps at final frame", () => { var s = Session(); s.SetTime(900); Equal(239d / 30, s.Time); });
Test("duplicate creates fresh key ids", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle); s.AddKey("X"); s.DuplicateSelection(); Equal(2, s.Composition.Layers.Count); ProjectValidator.Validate(s.Project); });
Test("split preserves source offset", () => { var s = Session(); var l = s.AddLayer(LayerKind.Rectangle); l.StartTime = .25; s.SetTime(3); s.SplitSelection(); Equal(2, s.Composition.Layers.Count); Check(s.Composition.Layers.All(x => x.StartTime == .25)); Equal(3, s.Composition.Layers[0].InPoint); Equal(3, s.Composition.Layers[1].OutPoint); });
Test("locked layer cannot delete", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle).Locked = true; s.DeleteSelection(); Equal(1, s.Composition.Layers.Count); });
Test("delete removes parent references", () => { var s = Session(); var a = s.AddLayer(LayerKind.Null); var b = s.AddLayer(LayerKind.Rectangle); b.ParentId = a.Id; s.Select(a.Id); s.DeleteSelection(); Check(s.Composition.Layers[0].ParentId is null); });
Test("precompose and undo", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle); s.AddLayer(LayerKind.Text); s.SelectAll(); s.Precompose(); Equal(2, s.Project.Compositions.Count); Equal(1, s.Composition.Layers.Count); s.Undo(); Equal(1, s.Project.Compositions.Count); Equal(2, s.Composition.Layers.Count); });
Test("precompose rejects external parent", () => { var s = Session(); var a = s.AddLayer(LayerKind.Null); var b = s.AddLayer(LayerKind.Rectangle); b.ParentId = a.Id; Throws(() => s.Precompose()); });
Test("animated property inserts at playhead", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle); s.ToggleAnimation("X"); s.SetTime(1); s.SetProperty("X", 150); Equal(2, s.Primary!.Transform.X.Keys.Count); Equal(150, CurveEvaluator.Evaluate(s.Primary.Transform.X, 1)); });
Test("auto key creates first key", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle); s.AutoKey = true; s.SetProperty("Y", 100); Equal(1, s.Primary!.Transform.Y.Keys.Count); });
Test("disable animation preserves evaluated value", () => { var s = Session(); var l = s.AddLayer(LayerKind.Rectangle); l.Transform.X = Curve(); s.SetTime(1); s.ToggleAnimation("X"); Equal(0, s.Primary!.Transform.X.Keys.Count); Equal(20, s.Primary.Transform.X.Value); });
Test("effect defaults and undo", () => { var s = Session(); s.AddLayer(LayerKind.Ellipse); s.AddEffect(EffectKind.Glow); Equal(32, s.Primary!.Effects[0].Parameters["Radius"].Value); s.Undo(); Equal(0, s.Primary!.Effects.Count); });
Test("mask is editable geometry", () => { var s = Session(); s.AddLayer(LayerKind.Rectangle); s.AddMask(); Equal(4, s.Primary!.Masks[0].Path.Nodes.Count); });
Test("parent cycle edit rolls back", () => { var s = Session(); var l = s.AddLayer(LayerKind.Null); Throws(() => s.SetParent(l.Id)); Check(s.Primary!.ParentId is null); });
Test("work area frame boundary", () => { var s = Session(); s.SetTime(3); s.SetWorkArea(false); Equal(3 + 1d / 30, s.Composition.WorkEnd); });
Test("markers undo", () => { var s = Session(); s.AddMarker("beat"); Equal(1, s.Composition.Markers.Count); s.Undo(); Equal(0, s.Composition.Markers.Count); });
Test("render order back to front", () => { var c = new Composition(); var a = new Layer(); var b = new Layer(); c.Layers = [a, b]; Check(RenderPlanner.Build(c, 1).Layers[0].Layer.Id == b.Id); });
Test("solo isolation", () => { var c = new Composition { Layers = [new() { Solo = true }, new()] }; Equal(1, RenderPlanner.Build(c, 1).Layers.Count); });
Test("out point is exclusive", () => { var c = new Composition { Layers = [new() { OutPoint = 2 }] }; Equal(0, RenderPlanner.Build(c, 2).Layers.Count); });
Test("null does not render", () => Equal(0, RenderPlanner.Build(new() { Layers = [new() { Kind = LayerKind.Null }] }, 1).Layers.Count));
Test("hit test skips locked background", () => { var c = new Composition { Layers = [new() { Locked = true }, new()] }; Check(RenderPlanner.HitTest(c, 1, new(20, 20))?.Id == c.Layers[1].Id); });
Test("ellipse corner is outside", () => { var c = new Composition { Layers = [new() { Kind = LayerKind.Ellipse, Width = 100, Height = 100 }] }; Check(RenderPlanner.HitTest(c, 1, new(1, 1)) is null); });
Test("export includes start excludes end", () => { var c = new Composition { WorkStart = 1, WorkEnd = 2 }; var t = RenderPlanner.ExportTimes(c).ToArray(); Equal(30, t.Length); Equal(1, t[0]); Equal(59d / 30, t[^1]); });
Test("export frame budget", () => Throws(() => RenderPlanner.ExportTimes(new Composition(), maximumFrames: 3).ToArray()));
Test("pixel budget", () => Throws(() => RenderBudget.Default.Check(8192, 8192)));
Test("valid pixel budget", () => RenderBudget.Default.Check(1920, 1080));
Test("media history shares payload", () => { var s = Session(); var a = new MediaAsset { Data = [1, 2, 3] }; s.Edit("asset", () => s.Project.Assets.Add(a)); s.AddLayer(LayerKind.Text); s.Undo(); Check(ReferenceEquals(a.Data, s.Project.Assets[0].Data)); });

Console.WriteLine($"\n{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
