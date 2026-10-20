namespace HexFund.UI.Views;

/// <summary>
/// Transparent modal page shown when the user presses the back button on a root tab. It replaces
/// the native Android dialog so the confirmation uses the theme tokens and matches the in-app
/// dialogs.
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
