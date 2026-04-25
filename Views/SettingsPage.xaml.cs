using HexFund.UI.ViewModels;
using Microsoft.Maui.Controls;
using System;

namespace HexFund.UI.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
            await vm.InitializeAsync();
    }
}
