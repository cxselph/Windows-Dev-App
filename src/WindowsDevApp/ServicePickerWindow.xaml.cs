using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsDevApp.Services;

namespace WindowsDevApp;

public partial class ServicePickerWindow : Window
{
    private readonly List<ServiceInfo> _allServices;
    public string? SelectedServiceName { get; private set; }

    public ServicePickerWindow(List<ServiceInfo> services)
    {
        InitializeComponent();
        _allServices = services;
        ServicesListBox.ItemsSource = _allServices;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = FilterBox.Text.Trim();
        ServicesListBox.ItemsSource = string.IsNullOrEmpty(text)
            ? _allServices
            : _allServices.Where(s =>
                s.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesListBox.SelectedItem is ServiceInfo info)
        {
            SelectedServiceName = info.Name;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ServicesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OkButton_Click(sender, e);
    }
}
