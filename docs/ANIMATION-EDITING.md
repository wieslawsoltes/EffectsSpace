# Animation editing

## Select and retime

Shift-click a diamond to add/remove it from the selection. Drag an empty property row or graph area to draw a marquee; Shift adds the enclosed keys to the original selection. Ctrl+Shift+A selects all keys in the current property. Selection can span layers and channels; it does not itself create a document revision.

Drag any already-selected key to move the entire group. Left/Right nudges a frame; Shift nudges ten. The value graph also changes the selected keys' values by one shared delta. The velocity graph permits horizontal retiming only. The graph's vertical range is held fixed while dragging, avoiding a moving scale under the pointer.

Retiming starts a single `EditorSession` transaction. Original times/values are captured once. Each preview computes a shared frame-snapped delta, clamps the group to the applicable time/value ranges, and checks every participating channel against unselected keys. Any occupied destination rejects the entire preview, leaving the previous valid preview unchanged. Commit creates one undo entry; Escape, capture loss or disposing an unfinished edit restores the original document and selection.

## Clipboard semantics

Ctrl+C captures an immutable, session-local keyframe clipboard. Ctrl+X copies then removes unlocked selected keys. Ctrl+V pastes relative to the current playhead. Ctrl+Shift+V explicitly maps a single copied channel to the current property instead of its original property role.

Relative timing, values, interpolation modes and temporal handles are preserved. Each pasted key receives a new identifier. The target frame rate snaps incoming times; two incoming keys that collapse onto the same destination time are rejected, not silently merged. Existing destination keys at pasted times are replaced in the same transaction. A paste that would extend beyond the last composition frame fails before modifying any channel.

One copied source layer can be pasted into multiple selected destination layers. Multiple source layers require an equal number of unlocked selected destination layers and map in front-to-back stacking order. Transform properties map by stable names such as `X` or `Opacity`. Effect properties map by effect kind, same-kind occurrence and parameter key. Missing effect instances must be added first; they are not fabricated during paste.

The clipboard is not the browser/system clipboard. It survives opening another project within the same editor session but does not survive a page reload. Text editors retain ordinary operating-system text-copy behavior.

## Value and velocity graphs

Shift+F3 toggles the graph. Shift+F4 switches between value and signed velocity, or use the corresponding toolbar icons. Velocity is expressed in property units per second, not normalized segment progress. Outside a keyframe animation it is zero. Hold jumps and infinite Bezier endpoint slopes have undefined derivatives.

For a Bezier segment, the evaluator solves the horizontal cubic coordinate, then computes:

```text
velocity = (rightValue - leftValue) / (rightTime - leftTime)
           * dyBezier(u) / dxBezier(u)
```

When both endpoint derivatives vanish, the implementation tests the second and then third derivatives to obtain a finite limit where one exists. A nonzero vertical derivative over a zero horizontal derivative is not labeled as zero velocity. Expressions are not symbolically differentiated; their graph is explicitly labeled as a finite-difference velocity estimate.

This is a scalar velocity graph. It is not a complete Adobe spatial speed editor and does not implement roving keys, spatial tangents, or coupled multidimensional speed editing.

## Temporal Bezier handles

Select either endpoint of a non-flat Bezier segment in the value graph. Circular handles expose the selected key's incoming and/or outgoing temporal control point. Drag horizontally to change time influence and vertically to change normalized value influence, including overshoot. Horizontal coordinates remain within `[0,1]`; vertical coordinates are bounded to `[-100,100]`. Those bounds match document validation.

The outgoing key stores both normalized controls for the segment ending at the next key. Easy Ease on a selected key adjusts that key's outgoing control and its predecessor's incoming control. Ease In changes only the incoming side; Ease Out only the outgoing side. Converting a linear segment initializes the untouched controls to a linear curve, avoiding accidental easing of the other endpoint.

Equal-value segments and expression-driven channels do not expose handle dragging. A flat segment cannot represent arbitrary intermediate value excursions using normalized endpoint-relative controls; it needs additional keys. Linear and Hold remain available from Animation.

## Effect tracks

Press E to reveal effect parameters. Each effect parameter uses a stable address:

```text
fx/<effect-id>/<parameter-key>
```

Reordering the stack cannot invalidate its address. Effect Controls exposes a stopwatch beside each parameter; clicking its label opens the property graph. Auto-key, interpolation, expressions, group selection and clipboard operations use the same channel implementation as transforms. Effect numeric fields update their evaluated values during playback without rebuilding the inspector.

Ctrl+Alt+B adds Gaussian Blur and reveals its tracks. J/K navigates to the previous/next key of the current property. Shift+F2 opens the taller Animation workspace.

## Reuse the editing library

```csharp
using EffectsSpace.Core;
using EffectsSpace.Editing;

var session = new EditorSession(MotionProject.Empty());
var layer = session.AddLayer(LayerKind.Rectangle);
layer.Transform.X.SetKey(1, 100, Interpolation.Linear);
layer.Transform.X.SetKey(2, 300, Interpolation.Linear);
session.SelectKeys(layer.Transform.X.Keys.Select(key => key.Id));

// A host-owned drag transaction; disposal before Commit rolls it back.
using (var drag = new KeyframeMove(session))
{
    if (drag.TryPreview(seconds: 0.5, valueDelta: 20))
        drag.Commit();
}

session.CopyKeys();
session.SetTime(4);
session.PasteKeys();
session.EaseKeys(EaseDirection.Both);
session.Undo();
```

`LayerChannels.Enumerate(layer)` supplies property metadata, stable paths and semantic ranges. `CurveVelocity.TryEvaluate` returns a validity flag rather than inventing a derivative at discontinuities. The engine libraries contain no Uno dependency.

For a document change outside a gesture, use `session.Edit(...)` instead of mutating a session-owned model directly. The setup above shows channel construction; production workflows should include their initial key creation in an edit when undo is required.

## Validation

The animation executable tests selection, history, immutable clipboard content, paste mapping, atomic rejection, cross-channel collisions, range clamping, locked layers, easing semantics and derivatives. Browser acceptance tests perform actual pointer drags, keyboard shortcuts, text entry and selection. Read-only geometry diagnostics expose where controls and keys are rendered; they do not offer a model mutation API.
