using System.Windows;
using WindowsDevApp.Services;

namespace WindowsDevApp;

public class SelectableBranch
{
    public string Name { get; set; } = "";
    public bool IsSelected { get; set; } = true;
}

public partial class CleanupBranchesWindow : Window
{
    private readonly List<SelectableBranch> _items;
    public List<string> SelectedBranchNames { get; private set; } = new();

    public CleanupBranchesWindow(List<BranchItem> mergedBranches)
    {
        InitializeComponent();
        _items = mergedBranches.Select(b => new SelectableBranch { Name = b.Name }).ToList();
        BranchesListBox.ItemsSource = _items;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedBranchNames = _items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
