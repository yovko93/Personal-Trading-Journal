using System.Windows;

namespace PersonalTradingJournal.Desktop.Validation;

public static class FormValidation
{
    public static readonly DependencyProperty IsInvalidProperty =
        DependencyProperty.RegisterAttached(
            "IsInvalid",
            typeof(bool),
            typeof(FormValidation),
            new PropertyMetadata(false));

    public static bool GetIsInvalid(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsInvalidProperty);
    }

    public static void SetIsInvalid(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsInvalidProperty, value);
    }
}
