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
        if (e.Handled || XamlRoot is null) return;
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or PasswordBox) return;
        bool Down(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
        bool ctrl = Down(VirtualKey.Control), shift = Down(VirtualKey.Shift), alt = Down(VirtualKey.Menu);
        // A command must never replace the document underneath a captured drag.
        if (Session.IsEditing && e.Key != VirtualKey.Escape) { e.Handled = true; return; }
        var handled = true;
        Run(() =>
        {
            if (e.Key == VirtualKey.F9)
            { Session.EaseKeys(shift ? EaseDirection.In : ctrl ? EaseDirection.Out : EaseDirection.Both); return; }
            if (ctrl)
            {
                switch (e.Key)
                {
                    case VirtualKey.Z: if (shift) Session.Redo(); else Session.Undo(); break;
                    case VirtualKey.Y: if (alt) NewAdjustment(); else Session.Redo(); break;
                    case VirtualKey.S: _ = RunAsync(SaveAsync); break;
                    case VirtualKey.O: _ = RunAsync(OpenAsync); break;
                    case VirtualKey.I: _ = RunAsync(ImportAsync); break;
                    case VirtualKey.N: _ = RunAsync(NewCompositionAsync); break;
                    case VirtualKey.K: _ = RunAsync(CompositionSettingsAsync); break;
                    case VirtualKey.D: if (shift) Session.SplitSelection(); else Session.DuplicateSelection(); break;
                    case VirtualKey.C: if (shift) Session.Precompose(); else CopyKeyframes(); break;
                    case VirtualKey.X: Session.CutKeys(); ShowStatus("Cut keyframes to the session clipboard"); break;
                    case VirtualKey.V: Session.PasteKeys(shift); Timeline.ShowProperties("U"); break;
                    case VirtualKey.A: if (shift || Session.SelectedKeyIds.Count > 0 || Timeline.GraphMode) Session.SelectPropertyKeys(); else Session.SelectAll(); break;
                    case VirtualKey.M: _ = RunAsync(alt ? ExportAviAsync : ExportSequenceAsync); break;
                    case VirtualKey.L: if (alt) LoadMediaStudy(); else handled = false; break;
                    case VirtualKey.W: if (alt) _ = RunAsync(ExportAudioAsync); else handled = false; break;
                    case VirtualKey.T: if (alt) EditTime("enable"); else handled = false; break;
                    case VirtualKey.F: if (alt) EditTime("freeze"); else handled = false; break;
                    case VirtualKey.B: if (alt) { ApplyEffect(EffectKind.GaussianBlur); ShowEffectTracks(); } else handled = false; break;
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
                case VirtualKey.F2: if (shift) SetWorkspace("Animation"); else handled = false; break;
                case VirtualKey.F3: if (shift) Timeline.ToggleGraph(); else handled = false; break;
                case VirtualKey.F4: if (shift) Timeline.ToggleGraphKind(); else handled = false; break;
                case VirtualKey.F5: if (shift) _rightPanel.Select("Composite"); else handled = false; break;
                case VirtualKey.F7: if (shift) OpenMedia(); else handled = false; break;
                case VirtualKey.F6: if (shift) ShowRenderStats(); else handled = false; break;
                case VirtualKey.J: Session.NavigateKey(-1); break;
                case VirtualKey.K: Session.NavigateKey(1); break;
                case VirtualKey.B: Session.SetWorkArea(true); break;
                case VirtualKey.N: Session.SetWorkArea(false); break;
                case VirtualKey.E: ShowEffectTracks(); break;
                case VirtualKey.V: SelectTool(ViewerTool.Select); break;
                case VirtualKey.H: SelectTool(ViewerTool.Hand); break;
                case VirtualKey.Z: SelectTool(ViewerTool.Zoom); break;
                case VirtualKey.W: SelectTool(ViewerTool.Rotate); break;
                case VirtualKey.Y: SelectTool(ViewerTool.Anchor); break;
                case VirtualKey.Q: SelectTool(ViewerTool.Rectangle); break;
                case VirtualKey.G: SelectTool(ViewerTool.Pen); break;
                case VirtualKey.T: if (shift || Session.Primary is null) SelectTool(ViewerTool.Text); else ShowTrack("Opacity", "T"); break;
                case VirtualKey.P: ShowTrack("X", "P"); break;
                case VirtualKey.S: ShowTrack("ScaleX", "S"); break;
                case VirtualKey.R: ShowTrack("Rotation", "R"); break;
                case VirtualKey.A: ShowTrack("AnchorX", "A"); break;
                case VirtualKey.U: Timeline.ShowProperties("U"); break;
                case VirtualKey.Left: if (Session.SelectedKeyIds.Count > 0) Session.NudgeKeys(shift ? -10 : -1); else Nudge(shift ? -10 : -1, 0); break;
                case VirtualKey.Right: if (Session.SelectedKeyIds.Count > 0) Session.NudgeKeys(shift ? 10 : 1); else Nudge(shift ? 10 : 1, 0); break;
                case VirtualKey.Up: Nudge(0, shift ? -10 : -1); break;
                case VirtualKey.Down: Nudge(0, shift ? 10 : 1); break;
                case VirtualKey.Escape: Pause(); Timeline.CancelInteraction(); Viewer.CancelDrag(); Viewer.EndText(false); Session.CancelEdit(); break;
                default: handled = false; break;
            }
        });
        e.Handled = handled;
    }
    private void CopyKeyframes() { Session.CopyKeys(); ShowStatus($"Copied {Session.KeyClipboard!.KeyCount} keyframes · session clipboard"); UpdateAnimationToolbar(); }
    private void ShowTrack(string property, string filter)
    { Session.Property = property; Session.ClearKeySelection(); Timeline.ShowProperties(filter); }
    private void ShowEffectTracks()
    {
        if (Session.Primary is { } layer)
        {
            var properties = LayerChannels.Enumerate(layer).Where(p => p.EffectKind is not null).ToArray();
            if (!properties.Any(p => p.Path == Session.Property) && properties.Length > 0) Session.Property = properties[0].Path;
        }
        Session.ClearKeySelection(); Timeline.ShowProperties("E");
    }
}
