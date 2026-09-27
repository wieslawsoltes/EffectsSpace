using EffectsSpace.Controls;
using EffectsSpace.Editing;
using EffectsSpace.Timeline;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private StudioButton? _valueGraphButton, _velocityGraphButton, _copyKeysButton, _pasteKeysButton;
    private TextBlock? _keySelectionLabel;
    private void AppendAnimationToolbar(StackPanel row)
    {
        row.Children.Add(Studio.Separator());
        _valueGraphButton = Button("Value Graph", () => { Timeline.GraphMode = true; Timeline.GraphKind = GraphKind.Value; }, IconKind.Graph, false);
        _velocityGraphButton = Button("Velocity Graph", () => { Timeline.GraphMode = true; Timeline.GraphKind = GraphKind.Velocity; }, IconKind.Graph, false);
        row.Children.Add(_valueGraphButton); row.Children.Add(_velocityGraphButton);
        row.Children.Add(Button("Select Property Keys", () => Session.SelectPropertyKeys(), IconKind.Diamond, false));
        _copyKeysButton = Button("Copy Keys", CopyKeyframes, IconKind.Composition, false);
        _pasteKeysButton = Button("Paste Keys", () => Session.PasteKeys(), IconKind.Import, false);
        row.Children.Add(_copyKeysButton); row.Children.Add(_pasteKeysButton);
        row.Children.Add(Button("Previous Key", () => Session.NavigateKey(-1), IconKind.Previous, false));
        row.Children.Add(Button("Next Key", () => Session.NavigateKey(1), IconKind.Next, false));
        row.Children.Add(Button("Effect Tracks", ShowEffectTracks, IconKind.Settings, false));
        _keySelectionLabel = Studio.Text("0 keys", 10, Studio.Muted); row.Children.Add(_keySelectionLabel);
    }
    private void UpdateAnimationToolbar()
    {
        if (_valueGraphButton is null) return;
        _valueGraphButton.Active = Timeline.GraphMode && Timeline.GraphKind == GraphKind.Value;
        _velocityGraphButton!.Active = Timeline.GraphMode && Timeline.GraphKind == GraphKind.Velocity;
        _copyKeysButton!.IsEnabled = Session.SelectedKeyIds.Count > 0;
        _pasteKeysButton!.IsEnabled = Session.KeyClipboard is not null && Session.Primary?.Locked == false;
        _keySelectionLabel!.Text = $"{Session.SelectedKeyIds.Count} keys";
    }
}
