using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Config;
using HexFund.UI.Models;
using HexFund.UI.Services;
using HexFund.UI.Validation;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace HexFund.UI.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ICacheService _cacheService;
    private readonly IApiService _apiService;

    // ── App info ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string appVersion = AppConstants.AppVersion;
    [ObservableProperty] private string buildNumber = AppConstants.BuildNumber.ToString();

    // ── Auth state ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool canLogout;

    // ── Profile ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string? currentUserEmail;
    [ObservableProperty] private bool isEditingProfile;
    [ObservableProperty] private string editFirstName = string.Empty;
    [ObservableProperty] private string editLastName = string.Empty;
    [ObservableProperty] private bool isSavingProfile;
    [ObservableProperty] private string profileError = string.Empty;
    [ObservableProperty] private string profileSuccess = string.Empty;

    public string CurrentUserName =>
        _authService.CurrentUser is { } u
            ? $"{u.FirstName} {u.LastName}".Trim()
            : string.Empty;

    // ── Calendar view ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool isCalendarGridView;

    public string CalendarViewLabel => IsCalendarGridView ? "Grid View" : "List View";
    public string CalendarViewDescription => IsCalendarGridView
        ? "Showing month as a grid (tap to switch to list)"
        : "Showing daily cards (tap to switch to grid)";

    // ── Theme ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private ColorTheme selectedTheme;
    [ObservableProperty] private bool showThemeModal;

    /// <summary>
    /// Display name shown in the Settings row and theme modal header.
    /// Matches the gem/metal names from the HexFund design system.
    /// </summary>
    public string SelectedThemeLabel => SelectedTheme switch
    {
        ColorTheme.Gold      => "Gold",
        ColorTheme.Sapphire  => "Sapphire",
        ColorTheme.Emerald   => "Emerald",
        ColorTheme.Bronze    => "Bronze",
        ColorTheme.Obsidian  => "Obsidian",
        ColorTheme.Ruby      => "Ruby",
        ColorTheme.Amethyst  => "Amethyst",
        ColorTheme.Platinum  => "Platinum",
        _                    => "Gold"
    };

    /// <summary>
    /// The raw accent Color for the active theme — used to tint the preview
    /// swatch in the Settings row. Matches ThemeService accent values exactly.
    /// </summary>
    public Color SelectedThemeColor => SelectedTheme switch
    {
        ColorTheme.Gold      => Color.FromArgb("#D4AF37"),
        ColorTheme.Sapphire  => Color.FromArgb("#4A7FD6"),
        ColorTheme.Emerald   => Color.FromArgb("#35B88C"),
        ColorTheme.Bronze    => Color.FromArgb("#B87333"),
        ColorTheme.Obsidian  => Color.FromArgb("#8E94A0"),
        ColorTheme.Ruby      => Color.FromArgb("#C73A57"),
        ColorTheme.Amethyst  => Color.FromArgb("#9C6ED1"),
        ColorTheme.Platinum  => Color.FromArgb("#D9DDE2"),
        _                    => Color.FromArgb("#D4AF37"),
    };

    partial void OnDeleteConfirmationTextChanged(string value) =>
        OnPropertyChanged(nameof(CanConfirmDelete));

    partial void OnSelectedThemeChanged(ColorTheme value)
    {
        ThemeService.Apply(value);
        SettingsService.Theme = value;
        OnPropertyChanged(nameof(SelectedThemeLabel));
        OnPropertyChanged(nameof(SelectedThemeColor));
    }

    partial void OnIsCalendarGridViewChanged(bool value)
    {
        OnPropertyChanged(nameof(CalendarViewLabel));
        OnPropertyChanged(nameof(CalendarViewDescription));
    }

    // ── Changelog ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool showChangelogModal;
    [ObservableProperty] private ObservableCollection<PatchNote> patchNotes = new();

    // ── Categories ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool showCategoriesModal;
    [ObservableProperty] private ObservableCollection<UserCategory> categories = new();
    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private string categoryError = string.Empty;
    [ObservableProperty] private bool isSavingCategory;
    [ObservableProperty] private bool isLoadingCategories;

    public bool HasCategories => Categories.Count > 0;

    // ── Delete account ────────────────────────────────────────────────────────
    [ObservableProperty] private bool showDeleteAccountModal;
    [ObservableProperty] private string deleteConfirmationText = string.Empty;
    [ObservableProperty] private bool isDeletingAccount;
    [ObservableProperty] private string deleteAccountError = string.Empty;

    /// <summary>
    /// The user must type this exact phrase to unlock the final delete button,
    /// preventing accidental taps on a destructive irreversible action.
    /// </summary>
    public const string DeleteConfirmationPhrase = "DELETE MY ACCOUNT";

    public bool CanConfirmDelete =>
        DeleteConfirmationText.Trim().Equals(
            DeleteConfirmationPhrase, StringComparison.Ordinal);

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsViewModel(
        IAuthService authService,
        ICacheService cacheService,
        IApiService apiService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _authService = authService;
        _cacheService = cacheService;
        _apiService = apiService;

        IsCalendarGridView = SettingsService.CalendarView == CalendarViewMode.Grid;
        SelectedTheme = SettingsService.Theme;

        UpdateUserInfo();
        LoadChangelog();

        _authService.AuthStateChanged += OnAuthStateChanged;
        SettingsService.SettingsChanged += OnSettingsServiceChanged;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>Called from SettingsPage.OnAppearing to load categories once.</summary>
    public async Task InitializeAsync()
    {
        if (Categories.Count == 0)
            await LoadCategoriesAsync();
    }

    // ── Auth state change handlers ────────────────────────────────────────────

    private void OnSettingsServiceChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsCalendarGridView = SettingsService.CalendarView == CalendarViewMode.Grid;
            SelectedTheme = SettingsService.Theme;
        });

    private void OnAuthStateChanged() =>
        MainThread.BeginInvokeOnMainThread(UpdateUserInfo);

    private void UpdateUserInfo()
    {
        CanLogout = _authService.IsAuthenticated;

        if (_authService.IsAuthenticated && _authService.CurrentUser != null)
            CurrentUserEmail = _authService.CurrentUser.Email;
        else
            CurrentUserEmail = null;

        OnPropertyChanged(nameof(CurrentUserName));
    }

    // ── Profile editing ───────────────────────────────────────────────────────

    [RelayCommand]
    private void StartEditProfile()
    {
        if (_authService.CurrentUser == null) return;
        EditFirstName = _authService.CurrentUser.FirstName;
        EditLastName = _authService.CurrentUser.LastName;
        ProfileError = string.Empty;
        ProfileSuccess = string.Empty;
        IsEditingProfile = true;
    }

    [RelayCommand]
    private void CancelEditProfile()
    {
        IsEditingProfile = false;
        ProfileError = string.Empty;
        ProfileSuccess = string.Empty;
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        var firstNameError = UIValidator.ValidateProfileName(EditFirstName, "First name");
        var lastNameError  = UIValidator.ValidateProfileName(EditLastName, "Last name");

        if (firstNameError != null || lastNameError != null)
        {
            ProfileError = firstNameError ?? lastNameError ?? string.Empty;
            return;
        }

        IsSavingProfile = true;
        ProfileError = string.Empty;
        ProfileSuccess = string.Empty;

        try
        {
            var request = new UpdateProfileRequest
            {
                FirstName = EditFirstName.Trim(),
                LastName  = EditLastName.Trim(),
            };

            var updated = await _apiService.UpdateProfileAsync(request);

            if (updated != null)
            {
                if (_authService.CurrentUser != null)
                {
                    _authService.CurrentUser.FirstName = updated.FirstName;
                    _authService.CurrentUser.LastName  = updated.LastName;
                }

                OnPropertyChanged(nameof(CurrentUserName));
                IsEditingProfile = false;
                ProfileSuccess = "Profile updated successfully.";
            }
            else
            {
                ProfileError = "Failed to update profile. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ProfileError = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsSavingProfile = false;
        }
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenThemeModal() => ShowThemeModal = true;

    [RelayCommand]
    private void CloseThemeModal() => ShowThemeModal = false;

    [RelayCommand]
    private void SelectTheme(string parameter)
    {
        if (int.TryParse(parameter, out var index))
            SelectedTheme = (ColorTheme)index;

        ShowThemeModal = false;
    }

    // ── Calendar view ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleCalendarView()
    {
        IsCalendarGridView = !IsCalendarGridView;
        SettingsService.CalendarView = IsCalendarGridView ? CalendarViewMode.Grid : CalendarViewMode.List;
    }

    // ── Changelog ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenChangelogModal() => ShowChangelogModal = true;

    [RelayCommand]
    private void CloseChangelogModal() => ShowChangelogModal = false;

    private void LoadChangelog()
    {
        // Fire-and-forget — constructor cannot be async. Any failure is
        // silent so a missing changelog never crashes the settings page.
        _ = LoadChangelogAsync();
    }

    private async Task LoadChangelogAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(
                AppConstants.ChangelogResourcePath);

            using var reader = new System.IO.StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var notes = JsonSerializer.Deserialize<List<PatchNote>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (notes == null || notes.Count == 0) return;

            MainThread.BeginInvokeOnMainThread(() =>
                PatchNotes = new ObservableCollection<PatchNote>(notes));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Changelog load error: {ex.Message}");
        }
    }

    // ── Categories ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenCategoriesModalAsync()
    {
        await LoadCategoriesAsync();
        NewCategoryName = string.Empty;
        CategoryError = string.Empty;
        ShowCategoriesModal = true;
    }

    [RelayCommand]
    private void CloseCategoriesModal()
    {
        ShowCategoriesModal = false;
        CategoryError = string.Empty;
    }

    private async Task LoadCategoriesAsync()
    {
        IsLoadingCategories = true;
        try
        {
            var list = await _apiService.GetCategoriesAsync(forceRefresh: true);
            Categories = new ObservableCollection<UserCategory>(list);
            OnPropertyChanged(nameof(HasCategories));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load categories error: {ex.Message}");
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        var error = UIValidator.ValidateCategoryName(NewCategoryName);
        if (error != null)
        {
            CategoryError = error;
            return;
        }

        IsSavingCategory = true;
        CategoryError = string.Empty;

        try
        {
            var created = await _apiService.CreateCategoryAsync(
                new CreateCategoryRequest { Name = NewCategoryName.Trim() });

            if (created != null)
            {
                var insertAt = Categories
                    .TakeWhile(c => string.Compare(c.Name, created.Name,
                        StringComparison.OrdinalIgnoreCase) < 0)
                    .Count();
                Categories.Insert(insertAt, created);
                OnPropertyChanged(nameof(HasCategories));
                NewCategoryName = string.Empty;
            }
            else
            {
                CategoryError = $"A category named '{NewCategoryName.Trim()}' already exists.";
            }
        }
        catch (Exception ex)
        {
            CategoryError = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsSavingCategory = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(UserCategory category)
    {
        var mainPage = Application.Current?.MainPage;
        if (mainPage == null) return;

        var confirm = await mainPage.DisplayAlert(
            "Delete Category",
            $"Delete '{category.Name}'? Existing transactions will keep this category label.",
            "Delete", "Cancel");

        if (!confirm) return;

        try
        {
            var success = await _apiService.DeleteCategoryAsync(category.CategoryId);
            if (success)
            {
                Categories.Remove(category);
                OnPropertyChanged(nameof(HasCategories));
            }
            else
            {
                await mainPage.DisplayAlert("Error", "Failed to delete category.", "OK");
            }
        }
        catch (Exception ex)
        {
            await mainPage.DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private void OpenDeleteAccountModal()
    {
        DeleteConfirmationText = string.Empty;
        DeleteAccountError = string.Empty;
        ShowDeleteAccountModal = true;
    }

    [RelayCommand]
    private void CloseDeleteAccountModal()
    {
        ShowDeleteAccountModal = false;
        DeleteConfirmationText = string.Empty;
        DeleteAccountError = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAccountAsync()
    {
        if (!CanConfirmDelete) return;

        var mainPage = Application.Current?.MainPage;
        if (mainPage == null) return;

        // Final explicit confirmation — belt-and-suspenders after the typed phrase.
        var confirmed = await mainPage.DisplayAlert(
            "This cannot be undone",
            "Your account, all financial data, and your sign-in identity will be permanently deleted. There is no recovery.\n\nProceed?",
            "Yes, permanently delete everything",
            "Cancel");

        if (!confirmed) return;

        IsDeletingAccount = true;
        DeleteAccountError = string.Empty;

        try
        {
            var success = await _authService.DeleteAccountAndDataAsync();

            if (!success)
            {
                DeleteAccountError =
                    "Deletion failed. Please check your connection and try again. " +
                    "If the problem persists, contact support.";
                return;
            }

            // Auth state change fires automatically from DeleteAccountAndDataAsync,
            // which will route the app back to the login screen.
            ShowDeleteAccountModal = false;
        }
        catch (Exception ex)
        {
            DeleteAccountError = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsDeletingAccount = false;
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!_authService.IsAuthenticated) return;

        var mainPage = Application.Current?.MainPage;
        if (mainPage == null) return;

        bool confirm = await mainPage.DisplayAlert(
            "Logout", "Are you sure you want to logout?", "Yes", "No");
        if (!confirm) return;

        try
        {
            _cacheService.Clear();
            await _authService.LogoutAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
            await mainPage.DisplayAlert(
                "Error", "An error occurred while logging out. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        var mainPage = Application.Current?.MainPage;
        if (mainPage == null) return;

        bool confirm = await mainPage.DisplayAlert(
            "Clear Cache",
            "This will clear all locally cached data. The app will re-fetch fresh data from the server on next use.",
            "Clear", "Cancel");

        if (!confirm) return;

        try
        {
            _cacheService.Clear();
            await mainPage.DisplayAlert(
                "Cache Cleared",
                "All cached data has been cleared.", "OK");
        }
        catch (Exception ex)
        {
            await mainPage.DisplayAlert("Error", "Failed to clear cache. Please try again.", "OK");
        }
    }
}
