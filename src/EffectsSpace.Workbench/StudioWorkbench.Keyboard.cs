using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Viewer;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private void Shortcut(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;
        var focused = FocusManager.GetFocusedElement(XamlRoot);
        if (focused is TextBox or PasswordBox) return;
        bool Down(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
        bool ctrl = Down(VirtualKey.Control), shift = Down(VirtualKey.Shift);
        var handled = true;
        Run(() =>
        {
            if (ctrl)
            {
                switch (e.Key)
                {
                    case VirtualKey.Z: if (shift) Session.Redo(); else Session.Undo(); break;
                    case VirtualKey.Y: Session.Redo(); break;
                    case VirtualKey.S: _ = RunAsync(SaveAsync); break;
                    case VirtualKey.O: _ = RunAsync(OpenAsync); break;
                    case VirtualKey.I: _ = RunAsync(ImportAsync); break;
                    case VirtualKey.N: _ = RunAsync(NewCompositionAsync); break;
                    case VirtualKey.K: _ = RunAsync(CompositionSettingsAsync); break;
                    case VirtualKey.D: if (shift) Session.SplitSelection(); else Session.DuplicateSelection(); break;
                    case VirtualKey.C: if (shift) Session.Precompose(); else handled = false; break;
                    case VirtualKey.A: Session.SelectAll(); break;
                    case VirtualKey.M: _ = RunAsync(ExportSequenceAsync); break;
                    default: handled = false; break;
                }
            }
            else switch (e.Key)
            {
                case VirtualKey.Space: TogglePlayback(); break;
                case VirtualKey.Home: Pause(); Session.SetTime(0); break;
                case VirtualKey.End: Pause(); Session.SetTime(Session.Composition.LastFrameTime); break;
                case VirtualKey.PageUp: Step(shift ? -10 : -1); break;
                case VirtualKey.PageDown: Step(shift ? 10 : 1); break;
                case VirtualKey.Delete: case VirtualKey.Back: Delete(); break;
                case VirtualKey.F9: Session.SetInterpolation(Interpolation.Bezier); break;
                case VirtualKey.F3: if (shift) Timeline.ToggleGraph(); else handled = false; break;
                case VirtualKey.B: Session.SetWorkArea(true); break;
                case VirtualKey.N: Session.SetWorkArea(false); break;
                case VirtualKey.V: SelectTool(ViewerTool.Select); break;
                case VirtualKey.H: SelectTool(ViewerTool.Hand); break;
                case VirtualKey.Z: SelectTool(ViewerTool.Zoom); break;
                case VirtualKey.W: SelectTool(ViewerTool.Rotate); break;
                case VirtualKey.Y: SelectTool(ViewerTool.Anchor); break;
                case VirtualKey.Q: SelectTool(ViewerTool.Rectangle); break;
                case VirtualKey.G: SelectTool(ViewerTool.Pen); break;
                case VirtualKey.T: if (shift || Session.Primary is null) SelectTool(ViewerTool.Text); else { Session.Property = "Opacity"; Timeline.ShowProperties("T"); } break;
                case VirtualKey.P: Session.Property = "X"; Timeline.ShowProperties("P"); break;
                case VirtualKey.S: Session.Property = "ScaleX"; Timeline.ShowProperties("S"); break;
                case VirtualKey.R: Session.Property = "Rotation"; Timeline.ShowProperties("R"); break;
                case VirtualKey.A: Session.Property = "AnchorX"; Timeline.ShowProperties("A"); break;
                case VirtualKey.U: Timeline.ShowProperties("U"); break;
                case VirtualKey.Left: Nudge(shift ? -10 : -1, 0); break;
                case VirtualKey.Right: Nudge(shift ? 10 : 1, 0); break;
                case VirtualKey.Up: Nudge(0, shift ? -10 : -1); break;
                case VirtualKey.Down: Nudge(0, shift ? 10 : 1); break;
                case VirtualKey.Escape: Pause(); Viewer.CancelDrag(); Viewer.EndText(false); Session.CancelEdit(); break;
                default: handled = false; break;
            }
        });
        e.Handled = handled;
    }
}
