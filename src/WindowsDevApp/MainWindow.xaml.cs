using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WindowsDevApp.Models;
using WindowsDevApp.Services;

namespace WindowsDevApp;

public partial class MainWindow : Window
{
    private readonly GitHubService _github = new();
    private readonly GitService _git;

    private ProfileData _data = new();
    private ObservableCollection<Profile> _profiles = new();
    private Profile? _selectedProfile;
    private List<EnvField> _envFields = new();

    public MainWindow()
    {
        InitializeComponent();
        _git = new GitService(() => _github.Token ?? SecretStore.LoadToken());
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _data = ProfileStore.Load();
        _profiles = new ObservableCollection<Profile>(_data.Profiles);
        ProfilesList.ItemsSource = _profiles;

        if (_data.SelectedProfileId != null)
        {
            var p = _profiles.FirstOrDefault(x => x.Id == _data.SelectedProfileId);
            if (p != null) ProfilesList.SelectedItem = p;
        }

        var storedToken = SecretStore.LoadToken();
        if (!string.IsNullOrEmpty(storedToken))
        {
            GitHubStatusText.Text = "Signing in...";
            var (success, login, error) = await _github.LoginAsync(storedToken);
            if (success)
            {
                ShowLoggedIn(login!);
                GitHubStatusText.Text = "";
            }
            else
            {
                GitHubStatusText.Text = $"Saved token invalid: {error}";
            }
        }
    }

    // ---------- GitHub login ----------

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var token = TokenBox.Password;
        if (string.IsNullOrWhiteSpace(token))
        {
            GitHubStatusText.Text = "Enter a token first.";
            return;
        }

        LoginButton.IsEnabled = false;
        GitHubStatusText.Text = "Signing in...";
        var (success, login, error) = await _github.LoginAsync(token);
        LoginButton.IsEnabled = true;

        if (success)
        {
            SecretStore.SaveToken(token);
            TokenBox.Clear();
            ShowLoggedIn(login!);
            GitHubStatusText.Text = "";
        }
        else
        {
            GitHubStatusText.Text = $"Login failed: {error}";
        }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _github.LogOut();
        SecretStore.ClearToken();
        LoggedOutPanel.Visibility = Visibility.Visible;
        LoggedInPanel.Visibility = Visibility.Collapsed;
        GitHubStatusText.Text = "";
    }

    private void ShowLoggedIn(string login)
    {
        LoggedInLabel.Text = $"Logged in as {login}";
        LoggedOutPanel.Visibility = Visibility.Collapsed;
        LoggedInPanel.Visibility = Visibility.Visible;
    }

    // ---------- Profiles ----------

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedProfile = ProfilesList.SelectedItem as Profile;
        _data.SelectedProfileId = _selectedProfile?.Id;
        PersistProfiles();

        if (_selectedProfile != null)
        {
            ProfileEditorPanel.IsEnabled = true;
            MainTabs.IsEnabled = true;
            ProfileNameBox.Text = _selectedProfile.Name;
            RepoPathBox.Text = _selectedProfile.RepoPath;
            EnvPathBox.Text = _selectedProfile.EnvFilePath;
            ServiceNameBox.Text = _selectedProfile.ServiceName;

            RefreshBranchesFromLocalRepo();
            LoadEnvFields();
            RefreshServiceStatus();
        }
        else
        {
            ProfileEditorPanel.IsEnabled = false;
            MainTabs.IsEnabled = false;
        }
    }

    private void AddProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = new Profile();
        _profiles.Add(profile);
        _data.Profiles = _profiles.ToList();
        PersistProfiles();
        ProfilesList.SelectedItem = profile;
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null) return;

        var result = MessageBox.Show($"Delete profile '{_selectedProfile.Name}'? This does not delete any files.",
            "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _profiles.Remove(_selectedProfile);
        _data.Profiles = _profiles.ToList();
        _data.SelectedProfileId = null;
        PersistProfiles();
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null) return;

        _selectedProfile.Name = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? "Untitled profile" : ProfileNameBox.Text.Trim();
        _selectedProfile.RepoPath = RepoPathBox.Text.Trim();
        _selectedProfile.EnvFilePath = EnvPathBox.Text.Trim();
        _selectedProfile.ServiceName = ServiceNameBox.Text.Trim();

        _data.Profiles = _profiles.ToList();
        PersistProfiles();
        ProfilesList.Items.Refresh();

        RefreshBranchesFromLocalRepo();
        LoadEnvFields();
        RefreshServiceStatus();
    }

    private void PersistProfiles() => ProfileStore.Save(_data);

    private void BrowseRepoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select the local repo folder" };
        if (dialog.ShowDialog() == true)
        {
            RepoPathBox.Text = dialog.FolderName;
        }
    }

    private void BrowseEnvButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select .env file",
            Filter = "Env files (*.env;*.env.*)|*.env;*.env.*|All files (*.*)|*.*",
            CheckFileExists = false
        };
        if (dialog.ShowDialog() == true)
        {
            EnvPathBox.Text = dialog.FileName;
        }
    }

    private void PickServiceButton_Click(object sender, RoutedEventArgs e)
    {
        var services = ServiceControlService.ListServices();
        var picker = new ServicePickerWindow(services) { Owner = this };
        if (picker.ShowDialog() == true && picker.SelectedServiceName != null)
        {
            ServiceNameBox.Text = picker.SelectedServiceName;
        }
    }

    // ---------- Branches ----------

    private void RefreshBranchesFromLocalRepo()
    {
        BranchesList.ItemsSource = null;
        CurrentBranchText.Text = "";

        if (_selectedProfile == null || string.IsNullOrWhiteSpace(_selectedProfile.RepoPath))
            return;

        if (!_git.IsValidRepo(_selectedProfile.RepoPath))
        {
            BranchStatusText.Text = "This folder is not a git repository.";
            return;
        }

        try
        {
            var branches = _git.ListBranches(_selectedProfile.RepoPath);
            BranchesList.ItemsSource = branches;
            CurrentBranchText.Text = $"Current branch: {_git.GetCurrentBranch(_selectedProfile.RepoPath)}";
        }
        catch (Exception ex)
        {
            BranchStatusText.Text = $"Could not read branches: {ex.Message}";
        }
    }

    private async void RefreshBranchesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null) return;
        var repoPath = _selectedProfile.RepoPath;

        await RunBranchOperation("Fetching...", () => _git.Fetch(repoPath));
    }

    private async void CheckoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null) return;
        if (BranchesList.SelectedItem is not BranchItem branch)
        {
            BranchStatusText.Text = "Select a branch first.";
            return;
        }

        var repoPath = _selectedProfile.RepoPath;
        await RunBranchOperation("Checking out...", () => _git.Checkout(repoPath, branch));
    }

    private async void PullButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null) return;
        var repoPath = _selectedProfile.RepoPath;

        await RunBranchOperation("Pulling...", () => _git.Pull(repoPath));
    }

    private async void ForceSyncButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null) return;

        var confirm = MessageBox.Show(
            "This will discard ALL local changes and untracked files in this repo folder, resetting it to exactly match the remote branch. Continue?",
            "Confirm force sync", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var repoPath = _selectedProfile.RepoPath;
        await RunBranchOperation("Force syncing...", () => _git.ForceSyncToRemote(repoPath));
    }

    private void CleanupBranchesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null) return;
        var repoPath = _selectedProfile.RepoPath;

        List<BranchItem> merged;
        try
        {
            merged = _git.GetMergedLocalBranches(repoPath);
        }
        catch (Exception ex)
        {
            BranchStatusText.Text = $"Could not check merged branches: {ex.Message}";
            return;
        }

        if (merged.Count == 0)
        {
            BranchStatusText.Text = "No local branches are fully merged into the current branch.";
            return;
        }

        var picker = new CleanupBranchesWindow(merged) { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedBranchNames.Count == 0)
            return;

        var confirm = MessageBox.Show(
            $"Delete {picker.SelectedBranchNames.Count} local branch(es)? This cannot be undone. Remote branches are not affected.",
            "Confirm delete branches", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var result = _git.DeleteLocalBranches(repoPath, picker.SelectedBranchNames);
        BranchStatusText.Text = result.Message;
        RefreshBranchesFromLocalRepo();
    }

    private async Task RunBranchOperation(string busyMessage, Func<GitOperationResult> operation)
    {
        BranchStatusText.Text = busyMessage;
        SetBranchButtonsEnabled(false);

        try
        {
            var result = await Task.Run(operation);
            BranchStatusText.Text = result.Message;
        }
        finally
        {
            SetBranchButtonsEnabled(true);
            RefreshBranchesFromLocalRepo();
            LoadEnvFields();
        }
    }

    private void SetBranchButtonsEnabled(bool enabled)
    {
        RefreshBranchesButton.IsEnabled = enabled;
        CheckoutButton.IsEnabled = enabled;
        PullButton.IsEnabled = enabled;
        ForceSyncButton.IsEnabled = enabled;
        CleanupBranchesButton.IsEnabled = enabled;
    }

    // ---------- Env file ----------

    private void LoadEnvFields()
    {
        EnvFieldsList.ItemsSource = null;
        EnvValueBox.Text = "";
        EnvSelectedKeyText.Text = "";
        EnvStatusText.Text = "";

        if (_selectedProfile == null || string.IsNullOrWhiteSpace(_selectedProfile.EnvFilePath))
            return;

        if (!File.Exists(_selectedProfile.EnvFilePath))
        {
            EnvStatusText.Text = "Env file not found at the configured path.";
            return;
        }

        try
        {
            _envFields = EnvFileService.Load(_selectedProfile.EnvFilePath);
            EnvFieldsList.ItemsSource = _envFields;
        }
        catch (Exception ex)
        {
            EnvStatusText.Text = $"Could not read env file: {ex.Message}";
        }
    }

    private void ReloadEnvButton_Click(object sender, RoutedEventArgs e) => LoadEnvFields();

    private void EnvFieldsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EnvFieldsList.SelectedItem is EnvField field)
        {
            EnvSelectedKeyText.Text = field.Key;
            EnvValueBox.Text = field.Value;
        }
        else
        {
            EnvSelectedKeyText.Text = "";
            EnvValueBox.Text = "";
        }
    }

    private void SaveEnvButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null || EnvFieldsList.SelectedItem is not EnvField field)
        {
            EnvStatusText.Text = "Select a field first.";
            return;
        }

        try
        {
            EnvFileService.SetField(_selectedProfile.EnvFilePath, field.Key, EnvValueBox.Text);
            field.Value = EnvValueBox.Text;
            EnvStatusText.Text = $"Saved '{field.Key}'.";
        }
        catch (Exception ex)
        {
            EnvStatusText.Text = $"Save failed: {ex.Message}";
        }
    }

    // ---------- Windows service ----------

    private void RefreshServiceStatus()
    {
        ServiceOpStatusText.Text = "";

        if (_selectedProfile == null || string.IsNullOrWhiteSpace(_selectedProfile.ServiceName))
        {
            ServiceDisplayText.Text = "No service configured.";
            ServiceStatusLabel.Text = "";
            return;
        }

        ServiceDisplayText.Text = _selectedProfile.ServiceName;
        try
        {
            ServiceStatusLabel.Text = $"Status: {ServiceControlService.GetStatus(_selectedProfile.ServiceName)}";
        }
        catch (Exception ex)
        {
            ServiceStatusLabel.Text = $"Could not read status: {ex.Message}";
        }
    }

    private void RefreshServiceButton_Click(object sender, RoutedEventArgs e) => RefreshServiceStatus();

    private async void RestartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile == null || string.IsNullOrWhiteSpace(_selectedProfile.ServiceName))
        {
            ServiceOpStatusText.Text = "Configure a service name first.";
            return;
        }

        var serviceName = _selectedProfile.ServiceName;
        RestartServiceButton.IsEnabled = false;
        ServiceOpStatusText.Text = "Restarting...";

        try
        {
            var (_, message) = await Task.Run(() => ServiceControlService.Restart(serviceName, TimeSpan.FromSeconds(30)));
            ServiceOpStatusText.Text = message;
        }
        finally
        {
            RestartServiceButton.IsEnabled = true;
            RefreshServiceStatus();
        }
    }
}
