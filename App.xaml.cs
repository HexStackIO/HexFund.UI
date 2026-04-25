using HexFund.UI.Services;

namespace HexFund.UI;

public partial class App : Application
{
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;

    public App(IAuthService authService, ISettingsService settingsService)
    {
        _authService = authService;
        _settingsService = settingsService;

        InitializeComponent();
        ThemeService.Apply(_settingsService.Theme);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell(_authService, _settingsService));
    }
}