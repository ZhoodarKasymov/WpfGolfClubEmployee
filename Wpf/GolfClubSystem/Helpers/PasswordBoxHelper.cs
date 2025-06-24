using System.Windows;
using System.Windows.Controls;

namespace GolfClubSystem.Helpers;

public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxHelper),
            new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword", typeof(bool), typeof(PasswordBoxHelper),
            new PropertyMetadata(false, OnBindPasswordChanged));

    public static string GetBoundPassword(DependencyObject obj)
    {
        return (string)obj.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject obj, string value)
    {
        obj.SetValue(BoundPasswordProperty, value);
    }

    public static bool GetBindPassword(DependencyObject obj)
    {
        return (bool)obj.GetValue(BindPasswordProperty);
    }

    public static void SetBindPassword(DependencyObject obj, bool value)
    {
        obj.SetValue(BindPasswordProperty, value);
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox passwordBox && !GetUpdatingPassword(passwordBox))
        {
            passwordBox.Password = e.NewValue as string;
        }
    }

    private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
    {
        if (dp is PasswordBox passwordBox)
        {
            if ((bool)e.OldValue)
            {
                passwordBox.PasswordChanged -= HandlePasswordChanged;
            }

            if ((bool)e.NewValue)
            {
                passwordBox.PasswordChanged += HandlePasswordChanged;
            }
        }
    }

    private static readonly DependencyProperty UpdatingPasswordProperty =
        DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    private static bool GetUpdatingPassword(DependencyObject obj)
    {
        return (bool)obj.GetValue(UpdatingPasswordProperty);
    }

    private static void SetUpdatingPassword(DependencyObject obj, bool value)
    {
        obj.SetValue(UpdatingPasswordProperty, value);
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            SetUpdatingPassword(passwordBox, true);
            SetBoundPassword(passwordBox, passwordBox.Password);
            SetUpdatingPassword(passwordBox, false);
        }
    }
}