using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;
using HexFund.UI.Services;
using HexFund.UI.Validation;
using System.Collections.ObjectModel;

namespace HexFund.UI.ViewModels;

/// <summary>A selectable report type shown as a chip.</summary>
public partial class FeedbackTypeOption : ObservableObject
{
    public FeedbackTypeOption(FeedbackType type, string label)
    {
        Type = type;
        Label = label;
    }

    public FeedbackType Type { get; }
    public string Label { get; }

    [ObservableProperty] private bool isSelected;
}

/// <summary>
/// Drives the send-feedback page. The unsent text is kept in preferences, so a failed or
/// abandoned attempt never loses what the user typed.
/// </summary>
public partial class FeedbackViewModel : BaseViewModel
{
    private const int DraftSaveDelayMs = 600;

    private readonly IApiService _apiService;
    private readonly IDiagnosticsService _diagnosticsService;

    private CancellationTokenSource? _saveCts;
    private bool _draftLoaded;

    public FeedbackViewModel(
        IApiService apiService,
        IDiagnosticsService diagnosticsService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService = apiService;
        _diagnosticsService = diagnosticsService;

        TypeOptions = new ObservableCollection<FeedbackTypeOption>
        {
            new(FeedbackType.Bug, "Bug"),
            new(FeedbackType.Idea, "Idea"),
            new(FeedbackType.Question, "Question"),
        };
        TypeOptions[0].IsSelected = true;
    }

    public ObservableCollection<FeedbackTypeOption> TypeOptions { get; }

    [ObservableProperty] private FeedbackType selectedType = FeedbackType.Bug;
    [ObservableProperty] private string body = string.Empty;
    [ObservableProperty] private string bodyError = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;

    // Off by default: the user opts in to sending diagnostics for each report.
    [ObservableProperty] private bool includeDiagnostics;
    [ObservableProperty] private bool showDiagnosticsPreview;
    [ObservableProperty] private string diagnosticsPreview = string.Empty;
    [ObservableProperty] private bool isSending;

    public string CharacterCount => $"{Body.Length} / {UIValidator.FeedbackBodyMaxLength}";
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);
    public bool CanSubmit => !IsSending;
    public string SubmitLabel => IsSending ? "Sending..." : "Send report";

    public string BodyPlaceholder => SelectedType switch
    {
        FeedbackType.Bug => "What happened, and what did you expect to happen?",
        FeedbackType.Idea => "What would make HexFund better for you?",
        _ => "What would you like to know?",
    };

    /// <summary>Restores the saved draft the first time the page appears.</summary>
    public void Initialize()
    {
        if (_draftLoaded) return;
        _draftLoaded = true;

        var draft = FeedbackDraft.Load();
        if (!string.IsNullOrEmpty(draft)) Body = draft;
    }

    /// <summary>Saves the draft immediately, for when the page is leaving.</summary>
    public void FlushDraft()
    {
        _saveCts?.Cancel();
        FeedbackDraft.Save(Body);
    }

    [RelayCommand]
    private void SelectType(FeedbackTypeOption option)
    {
        foreach (var item in TypeOptions) item.IsSelected = ReferenceEquals(item, option);
        SelectedType = option.Type;
    }

    [RelayCommand]
    private void ToggleDiagnosticsPreview()
    {
        ShowDiagnosticsPreview = !ShowDiagnosticsPreview;
        if (ShowDiagnosticsPreview) RefreshPreview();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        FlushDraft();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsSending) return;

        BodyError = UIValidator.ValidateFeedbackBody(Body) ?? string.Empty;
        StatusMessage = string.Empty;
        if (!string.IsNullOrEmpty(BodyError)) return;

        IsSending = true;
        try
        {
            var request = new SubmitFeedbackRequest
            {
                Type = SelectedType,
                Body = Body.Trim(),
                Diagnostics = IncludeDiagnostics ? _diagnosticsService.Capture() : null,
            };

            var result = await _apiService.SubmitFeedbackAsync(request);
            var outcome = FeedbackOutcomes.From(result);

            if (outcome.IsSuccess)
            {
                _saveCts?.Cancel();
                FeedbackDraft.Clear();
                Body = string.Empty;
                await Shell.Current.DisplayAlert("Thank you", outcome.Message, "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // The draft is already stored by the debounced save; save it now in case the user leaves.
            FlushDraft();
            if (outcome.IsFieldError) BodyError = outcome.Message;
            else StatusMessage = outcome.Message;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Feedback submit error: {ex.GetType().Name}");
            FlushDraft();
            StatusMessage = FeedbackOutcomes.ServerMessage;
        }
        finally
        {
            IsSending = false;
        }
    }

    private void RefreshPreview() =>
        DiagnosticsPreview = _diagnosticsService.Capture().ToDisplayText();

    partial void OnBodyChanged(string value)
    {
        OnPropertyChanged(nameof(CharacterCount));
        if (!string.IsNullOrEmpty(BodyError)) BodyError = string.Empty;
        ScheduleDraftSave();
    }

    partial void OnSelectedTypeChanged(FeedbackType value) =>
        OnPropertyChanged(nameof(BodyPlaceholder));

    partial void OnStatusMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsSendingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SubmitLabel));
    }

    partial void OnIncludeDiagnosticsChanged(bool value)
    {
        if (!value) ShowDiagnosticsPreview = false;
    }

    // The draft is written after a short pause so typing does not hit storage on every keystroke.
    private void ScheduleDraftSave()
    {
        if (!_draftLoaded) return;

        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;
        var text = Body;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DraftSaveDelayMs, token);
                FeedbackDraft.Save(text);
            }
            catch (OperationCanceledException) { }
        });
    }
}

/// <summary>
/// The unsent feedback text, kept in device preferences. Preferences are not encrypted, so the
/// draft is cleared on sign-out and account deletion.
/// </summary>
public static class FeedbackDraft
{
    private const string Key = "feedback_draft";

    public static string Load() => Preferences.Default.Get(Key, string.Empty);

    public static void Save(string text)
    {
        if (string.IsNullOrEmpty(text)) Preferences.Default.Remove(Key);
        else Preferences.Default.Set(Key, text);
    }

    public static void Clear() => Preferences.Default.Remove(Key);
}
