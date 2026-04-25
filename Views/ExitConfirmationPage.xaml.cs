namespace HexFund.UI.Views;

/// <summary>
/// Transparent modal page shown when the user presses the system back button
/// from the root tab shell. Replaces the native Android AlertDialog so the
/// dialog inherits all DynamicResource theme tokens and matches the in-app
/// modal style used by the Edit / Amend dialogs.
/// </summary>
public partial class ExitConfirmationPage : ContentPage
{
    public ExitConfirmationPage()
    {
        InitializeComponent();
    }

    private async void OnCancelClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync(animated: false);

    private void OnExitClicked(object sender, EventArgs e) =>
        Application.Current?.Quit();

    private async void OnBackdropTapped(object sender, TappedEventArgs e) =>
        await Navigation.PopModalAsync(animated: false);
}
