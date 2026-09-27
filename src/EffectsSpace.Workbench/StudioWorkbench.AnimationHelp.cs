using EffectsSpace.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private async Task ShowAnimationHelpAsync()
    {
        const string guide = "KEYFRAME EDITING\n\nShift-click a diamond to add or remove it from the selection. Drag an empty property row or graph area to select a rectangular region; Shift adds to the existing selection. Ctrl+Shift+A selects every key in the current property. Click a selected diamond and drag to move the entire group. Left/Right nudges one frame, Shift nudges ten. Escape or lost capture cancels the drag.\n\nCopy / Cut / Paste\nCtrl+C copies the selected keys to a session-local clipboard; Ctrl+X cuts unlocked keys. Ctrl+V pastes relative to the playhead, preserving spacing and interpolation. Ctrl+Shift+V pastes one copied channel into the current property. Multiple source layers map to an equal number of selected destination layers in stacking order. Matching effect kinds and occurrence indices are used, not effect IDs from another layer. Missing destinations and frame-rate collisions are rejected without partial edits. Existing keys at pasted times are replaced.\n\nGraph Editor\nShift+F3 toggles the editor. Shift+F4 switches between value and signed velocity. Value graph keys can be dragged vertically and horizontally. The velocity graph permits horizontal retiming only; its vertical axis is property units per second. Expression velocity is a finite-difference estimate. Hold jumps and infinite Bezier endpoint slopes have undefined velocity.\n\nTemporal handles\nSelect keys on a non-flat Bezier segment in the value graph to reveal circular handles. Drag them to change timing influence and value easing. The graph scale stays fixed during a drag. F9 eases both sides, Shift+F9 eases the incoming side, and Ctrl+F9 eases the outgoing side. Linear and Hold are available from Animation. Expression-driven and equal-value segments do not expose handles.\n\nEffect tracks\nPress E to show effect parameters. Their stopwatches, keys, expressions and graph editing use the same animation engine as transforms. Click a parameter label in Effect Controls to open its graph. Ctrl+Alt+B adds Gaussian Blur and reveals its track. J/K moves to the previous/next key. Shift+F2 expands the Animation workspace.\n\nEditing guarantees\nRetiming validates every selected channel before changing any key. A collision leaves the last valid preview intact. Clipboard and drag operations are single undo steps. Undo/redo restores multi-key selection. Imported projects remain unchanged when validation fails. The clipboard is local to this editor session, not the operating-system clipboard.\n\nScope\nThis is a scalar value/velocity editor, not Adobe spatial-motion-path, roving-key or complete speed-graph parity. Video/audio processing, 3D, tracking and Adobe formats are separate unimplemented areas.";
        await new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "Animation Editing", CloseButtonText = "Close", RequestedTheme = ElementTheme.Dark,
            Content = new ScrollViewer { MaxHeight = 540, Content = new TextBlock { Text = guide, TextWrapping = TextWrapping.Wrap, FontFamily = Studio.Font, FontSize = 12, MaxWidth = 650, Foreground = Studio.Brush(Studio.TextColor) } }
        }.ShowAsync();
    }
}
