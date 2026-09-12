using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AuroraAudioStudio;

public sealed partial class MainPage
{
    private UtilityDraftStore utilityDrafts = null!;
    private string? utilityFeature;
    private bool restoringUtilityDraft;
    private readonly DispatcherTimer draftTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    private void InitializeUtilityDrafts()
    {
        utilityDrafts = new(settings);
        draftTimer.Tick += (_, _) => SaveUtilityDraft();
        utilitySources.CollectionChanged += (_, _) => ScheduleUtilityDraft();
        InputPathBox.TextChanged += (_, _) => ScheduleUtilityDraft();
        foreach (var picker in new[] { UtilityModelPicker, UtilityPresetPicker, UtilityTrackModePicker, SourceLanguagePicker })
            picker.SelectionChanged += (_, _) => ScheduleUtilityDraft();
    }

    private void ScheduleUtilityDraft()
    {
        if (restoringUtilityDraft || utilityFeature is null) return;
        draftTimer.Stop();
        draftTimer.Start();
    }

    private void SaveUtilityDraft()
    {
        draftTimer.Stop();
        if (restoringUtilityDraft || utilityFeature is null) return;
        static string Tag(ComboBox picker) => (picker.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var selected = (UtilitySourcesList.SelectedItem as MediaSourceItem)?.Path ?? InputPathBox.Text;
        var draft = new UtilityDraft(utilitySources.Select(x => x.Path).ToList(), selected,
            Tag(UtilityModelPicker), Tag(UtilityPresetPicker), Tag(UtilityTrackModePicker), Tag(SourceLanguagePicker));
        if (!utilityDrafts.TrySave(utilityFeature, draft, out var error)) SetStatus(error);
    }

    private void RestoreUtilityDraft(string tag)
    {
        utilityFeature = tag;
        if (utilityDrafts.Get(tag) is not { } draft) return;
        SelectTag(UtilityPresetPicker, draft.Preset);
        SelectTag(UtilityTrackModePicker, draft.TrackMode);
        SelectTag(UtilityModelPicker, draft.ModelId);
        SelectTag(SourceLanguagePicker, draft.SourceLanguage);
        // Keep missing paths visible: temporarily disconnected media must not disappear.
        foreach (var path in draft.Sources) utilitySources.Add(new MediaSourceItem { Path = path });
        InputPathBox.Text = draft.SelectedPath;
        UtilitySourcesList.SelectedIndex = draft.Sources.IndexOf(draft.SelectedPath);
        UtilityStatusText.Text = utilitySources.Count == 0 ? localization.Translate("等待添加素材") : localization.Format("sourcesAdded", utilitySources.Count);
        RunUtilityButton.Content = utilitySources.Count > 1 ? localization.Format("processSources", utilitySources.Count) : localization.Translate("开始处理");
    }
}
