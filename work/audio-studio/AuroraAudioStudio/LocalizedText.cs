using AuroraAudioStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace AuroraAudioStudio;

// Only authored elements are localized. Never write into framework template children:
// setting their Text replaces TemplateBinding and freezes NavigationView labels.
public static class LocalizedText
{
    public static string TranslateValue(string value) => service?.Translate(value) ?? value;
    private static readonly List<WeakReference<DependencyObject>> targets = [];
    private static readonly DependencyProperty RegisteredProperty = DependencyProperty.RegisterAttached("Registered", typeof(bool), typeof(LocalizedText), new PropertyMetadata(false));
    private static LocalizationService? service;
    public static readonly DependencyProperty KeyProperty = Register("Key");
    public static readonly DependencyProperty HeaderKeyProperty = Register("HeaderKey");
    public static readonly DependencyProperty PlaceholderKeyProperty = Register("PlaceholderKey");
    public static readonly DependencyProperty NameKeyProperty = Register("NameKey");
    public static readonly DependencyProperty OnKeyProperty = Register("OnKey");
    public static readonly DependencyProperty OffKeyProperty = Register("OffKey");
    private static DependencyProperty Register(string name) => DependencyProperty.RegisterAttached(name, typeof(string), typeof(LocalizedText), new PropertyMetadata(null, Changed));
    public static string GetKey(DependencyObject d) => (string)d.GetValue(KeyProperty);
    public static void SetKey(DependencyObject d, string value) => d.SetValue(KeyProperty, value);
    public static string GetHeaderKey(DependencyObject d) => (string)d.GetValue(HeaderKeyProperty);
    public static void SetHeaderKey(DependencyObject d, string value) => d.SetValue(HeaderKeyProperty, value);
    public static string GetPlaceholderKey(DependencyObject d) => (string)d.GetValue(PlaceholderKeyProperty);
    public static void SetPlaceholderKey(DependencyObject d, string value) => d.SetValue(PlaceholderKeyProperty, value);
    public static string GetNameKey(DependencyObject d) => (string)d.GetValue(NameKeyProperty);
    public static void SetNameKey(DependencyObject d, string value) => d.SetValue(NameKeyProperty, value);
    public static string GetOnKey(DependencyObject d) => (string)d.GetValue(OnKeyProperty);
    public static void SetOnKey(DependencyObject d, string value) => d.SetValue(OnKeyProperty, value);
    public static string GetOffKey(DependencyObject d) => (string)d.GetValue(OffKeyProperty);
    public static void SetOffKey(DependencyObject d, string value) => d.SetValue(OffKeyProperty, value);
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)d.GetValue(RegisteredProperty)) { d.SetValue(RegisteredProperty, true); targets.Add(new(d)); }
        Apply(d);
    }
    public static void Refresh(LocalizationService localization)
    {
        service = localization;
        targets.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in targets) if (reference.TryGetTarget(out var target)) Apply(target);
    }
    private static void Apply(DependencyObject target)
    {
        var localization = service;
        if (localization is null) return;
        string Text(string key) => localization.Get(key) is var value && value != key ? value : localization.Translate(key);
        if (target.GetValue(KeyProperty) is string key)
        {
            var text = Text(key);
            if (target is TextBlock block) block.Text = text;
            else if (target is Run run) run.Text = text;
            else if (target is ContentControl content) content.Content = text;
        }
        if (target.GetValue(HeaderKeyProperty) is string header)
        {
            if (target is ComboBox combo) combo.Header = Text(header);
            else if (target is TextBox box) box.Header = Text(header);
            else if (target is ToggleSwitch toggle) toggle.Header = Text(header);
            else if (target is Expander expander) expander.Header = Text(header);
        }
        if (target is TextBox input && target.GetValue(PlaceholderKeyProperty) is string placeholder) input.PlaceholderText = Text(placeholder);
        if (target.GetValue(NameKeyProperty) is string name) AutomationProperties.SetName(target, Text(name));
        if (target is ToggleSwitch toggleSwitch)
        {
            if (target.GetValue(OnKeyProperty) is string on) toggleSwitch.OnContent = Text(on);
            if (target.GetValue(OffKeyProperty) is string off) toggleSwitch.OffContent = Text(off);
        }
    }
}

public sealed class LocalizedValueConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => LocalizedText.TranslateValue(value?.ToString() ?? "");
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
