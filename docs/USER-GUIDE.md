# User guide

## First launch

EffectsSpace opens the original ORBITAL motion study at its hero frame. Every visible element is editable. The left Project panel lists the main composition and the nested orbital system. Click a composition to open it. Double-click a composition layer in the viewer or timeline to enter its source.

The default workspace has Project/Effect Controls on the left, Composition in the center, Properties/Effects & Presets on the right, and Timeline/Render Queue below. Drag the narrow separators to resize panels. The Window menu offers Default, Animation and Composition workspace presets. At narrower widths the project panel is hidden to preserve editing space; choose a workspace to restore it.

## Create and arrange layers

Use Layer → New or the right-side creation buttons when nothing is selected. The Rectangle, Ellipse and Star tools draw a layer by dragging on the composition. Hold Shift for equal dimensions. Click once for a default-size shape. The text tool creates a text layer at the clicked position; double-click an existing text layer to edit it in place. Click outside the editor to commit, or Escape to cancel.

Use V to select. Drag inside an unlocked layer to move it. Drag one of the eight boundary handles to scale while keeping the opposite point fixed. Hold Shift to constrain the scale. W selects the rotation tool; drag around the anchor and hold Shift to snap to 15 degrees. Y selects the anchor tool, compensating position so moving the anchor does not move the rendered content. The inspector exposes exact values for all transform channels.

The Pen tool adds points with clicks. Drag after placing a point to create symmetric Bezier handles. Click the first point to close a path, or Enter to finish an open path. Escape cancels the unfinished path. With a path selected, drag its nodes to reshape it.

## Build a timeline

Click the ruler to position the playhead. Space plays or pauses the work area using an elapsed-time clock. Page Up/Down moves one frame; Shift moves ten frames. Home/End reaches the composition endpoints. B sets the work-area start at the current frame; N sets its exclusive end after the current frame. Work-area handles can be dragged directly.

Click a layer name to select it. Shift-click toggles membership in the selection. The eye, solo dot and lock controls are live switches. Drag a clip to move it; drag its edges to trim; Alt-drag slips the source offset. Full-duration layers cannot move beyond the composition boundary, so trim first when an offset is needed. Ctrl+Shift+D splits at the playhead. Delete removes an unlocked selection. Ctrl+D duplicates layers with fresh identifiers and remapped selected parent/matte links.

The timeline scrolls vertically with the wheel. Ctrl+wheel zooms around the pointer; Shift+wheel scrolls time. The toolbar also offers fit and zoom controls. Click the disclosure arrow to expand properties. P/S/R/T/A reveals position/scale/rotation/opacity/anchor; U shows animated or expression-driven channels.

## Animate

Enable a property's stopwatch at the desired starting frame. Move to another frame, then edit the property. Animated channels insert a new key at the playhead rather than replacing their base value. Auto-key creates a first key when needed. Disabling a stopwatch removes its keys and preserves the evaluated value at the current frame.

Drag a timeline diamond to retime it. The Graph Editor displays the selected property's value curve; drag keys vertically to change values. Ctrl-click in the graph adds a key. Easy Ease/F9 uses temporal Bezier interpolation. Linear and Hold are available from Animation. When a key is selected, interpolation/deletion targets it; otherwise interpolation targets the selected layer's current property keys.

Expressions are scalar and side-effect-free. Examples:

```text
value + sin(time * 2) * 30
500 + cos(time * 0.75) * 320
wiggle(2, 20)
linear(time, 0, 1, 0, 100)
loopOut()
```

Use the Expression Property selector to choose the channel. Invalid new expressions are rejected with a status message. This grammar is not JavaScript and cannot access browser, files or network services.

## Effects, masks and parent relationships

Select a layer and choose an entry in Effects & Presets. Its controls appear in the left Effect Controls panel. Parameters support precise text entry and scrubbing from the small horizontal grip. Effects can be enabled, removed and reordered. Auto-key applies to effect parameters too.

Add Mask creates an inset rectangle in local layer coordinates. Choose Add, Subtract or Intersect, invert it, or adjust feather. The renderer combines mask geometry first and uses the largest feather among enabled masks. Multiple independent feather fields are not modeled as Adobe-equivalent raster operations.

The Properties panel has parent and track-matte selectors. Cyclic relationships are rejected atomically. A hidden matte can still supply alpha. Pre-compose moves selected layers into a full-size nested composition; include linked parent/matte layers together to avoid crossing dependencies. Changing a parent preserves local values, not the prior world transform.

## Import, save and recover

Import PNG, JPEG or WebP footage. Images are validated before creation and embedded in the project, so `.effects` files remain self-contained. Each image is limited to 32 MiB, total embedded media to 64 MiB, and image/render dimensions to the documented budgets. Unsupported footage is rejected instead of represented by a decorative placeholder.

Save exports a versioned JSON `.effects` file. Open validates the entire project before replacing the current session. Keep project exports as durable backups. Browser IndexedDB and desktop local-application-data recovery are conveniences, not a substitute for backups. A failed recovery load is reported without deleting the original recovery file.

## Export

Snapshot PNG exports the current frame with alpha. Render PNG Sequence exports every frame in the work area to a ZIP with `sequence.json`, using the exact rational frame rate and an exclusive end time. Exports are limited to 3600 frames and 256 MiB per archive. Use a shorter work area or smaller composition when an export exceeds the budget. Cancellation does not change the project.

This release exports 8-bit sRGB PNG, not professional HDR or color-managed delivery. Enabled video/audio layers are rejected by export because no media decoder/encoder is implemented. See the feature ledger for the complete scope boundary.
