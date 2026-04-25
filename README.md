# HexFund UI

A .NET MAUI mobile app for iOS and Android that connects to the FinancePlanner API. Features a monthly calendar view, transaction management, recurring transaction support, cash flow projections, and per-account theming.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Building & Running](#building--running)
- [Project Structure](#project-structure)
- [Features](#features)
- [Key Design Decisions](#key-design-decisions)

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 9.0+ |
| .NET MAUI Workload | `dotnet workload install maui` |
| Android SDK | API 21+ (Android 5.0) |
| Xcode | 13+ (iOS 14.2+ deployment target) |
| Visual Studio / VS Code | With MAUI extension |

```bash
# Install the MAUI workload if not already present
dotnet workload install maui
```

---

## Getting Started

```bash
# 1. Clone the repository
git clone <repo-url>
cd HexFund_UI

# 2. Restore dependencies
dotnet restore

# 3. Set the API base URL (see Configuration)

# 4. Run on Android emulator
dotnet run -f net9.0-android

# 5. Run on iOS simulator
dotnet run -f net9.0-ios
```

---

## Configuration

The API base URL is defined in `Config/AppConstants.cs`:

```csharp
public static class AppConstants
{
    public const string ApiBaseUrl = "https://your-api-url/api/";
    // ...
}
```

Update `ApiBaseUrl` to point to your running instance of the FinancePlanner API. For local development, use your machine's LAN IP (not `localhost`) when testing on a physical device or Android emulator:

| Target | URL format |
|--------|-----------|
| iOS Simulator | `http://localhost:5001/api/` |
| Android Emulator | `http://10.0.2.2:5001/api/` |
| Physical Device | `http://<your-machine-ip>:5001/api/` |

---

## Building & Running

```bash
# Android
dotnet build -f net9.0-android
dotnet run -f net9.0-android

# iOS (requires macOS + Xcode)
dotnet build -f net9.0-ios
dotnet run -f net9.0-ios

# Release build (trimming + AOT enabled)
dotnet publish -f net9.0-android -c Release
dotnet publish -f net9.0-ios -c Release
```

Release builds enable partial trimming and profiled AOT compilation for faster startup and smaller binary size.

---

## Project Structure

```
Config/
└── AppConstants.cs         # Base URL, cache TTLs, fallback colors

Converters/
└── ValueConverters.cs      # XAML value converters

Diagnostics/
└── GCMonitor.cs            # Optional GC pressure monitor

Models/                     # DTOs matching the API response shapes
├── Account.cs
├── AuthModels.cs
├── CalendarModels.cs       # CalendarGridDay, CalendarEventChip, DailyBalanceSnapshot
├── Requests.cs             # Create/Update/Amend request models
├── Transaction.cs
├── TransactionOccurrence.cs
└── User.cs

Platforms/
├── Android/                # Android-specific entry point and manifest
└── iOS/                    # iOS-specific entry point and Info.plist

Services/
├── AccountStateService.cs  # Shared selected-account + cross-VM event bus
├── ApiService.cs           # All HTTP calls to the API (with client-side caching)
├── AuthService.cs          # Login, register, logout, token persistence
├── CacheService.cs         # IMemoryCache wrapper with typed key helpers
├── SettingsService.cs      # Persistent user preferences (view mode, theme)
├── ThemeOption.cs          # Record type for theme picker entries
└── ThemeService.cs         # Runtime ResourceDictionary theme switching

ViewModels/
├── BaseViewModel.cs        # ThemePrimaryColor / ThemeMutedColor bindings
├── AccountsViewModel.cs
├── CalendarViewModel.cs
├── LoginViewModel.cs
├── RegisterViewModel.cs
├── SettingsViewModel.cs
└── TransactionsViewModel.cs

Views/                      # XAML pages + code-behind
├── AccountsPage
├── CalendarPage
├── LoginPage
├── RegisterPage
├── SettingsPage
└── TransactionsPage
```

---

## Features

### Calendar
A 42-cell fixed grid showing the current month. Each cell displays up to two event chips — one for income, one for an expense — with optional per-transaction custom colors. Tapping a cell opens a day detail panel showing individual transaction occurrences and daily totals.

Navigation arrows and a month picker let users jump to any month. Adjacent months are prefetched in the background after a short delay so scrolling feels instant.

### Transactions
Full CRUD for recurring and one-time transactions. Supports the following recurrence frequencies:

- Once
- Daily
- Weekly
- Bi-weekly
- Monthly
- Bi-monthly
- 1st & 3rd Friday (paycheck schedule)

**Amendment** — Rather than editing a recurring transaction directly, you can "amend" it from an effective date forward. The original transaction is preserved up to that date; a successor takes over from it. A "Show History" toggle reveals predecessor rows in the list.

Each transaction supports an optional hex color for visual grouping on the calendar.

### Accounts
Multiple accounts per user, each with its own initial balance and currency. Balances are computed in real time from transaction history — no stored running balance that can drift. Selecting an account switches the Calendar and Transactions views to that account.

### Settings
- **Calendar view** — toggle between month grid and daily list
- **Color theme** — eight built-in themes (Default, Ocean, Forest, Sunset, Monochrome, Rose, Midnight, Blush) applied at runtime without restarting the app
- **Clear cache** — forces a full data reload on next navigation
- **Logout**

---

## Key Design Decisions

**Unified month cache** — `CalendarViewModel` caches each month as a single `MonthData` record (overview + all transaction occurrences together). This prevents the split-cache problem where an overview and its transactions could come from different fetch generations after a mutation.

**Batch month fetch** — A single `GET /transactions-for-month` call replaces the 20–30 per-date requests the calendar previously made. The API returns a date-keyed dictionary; the UI converts the string keys to `DateTime` locally.

**In-place list reconciliation** — `TransactionsViewModel.ReconcileList` updates the `ObservableCollection<Transaction>` with targeted Add/Remove/Move operations instead of replacing the collection. This avoids the `CollectionChanged(Reset)` event that causes every visible cell to be destroyed and recreated.

**Cross-ViewModel event bus** — `AccountStateService` owns the selected account and exposes two events: `SelectedAccountChanged` and `TransactionsChanged`. ViewModels subscribe to these rather than polling, so the Calendar automatically refreshes when a transaction is created, updated, or deleted in the Transactions view.

**Runtime theming** — `ThemeService` merges a code-built `ResourceDictionary` into `Application.Current.Resources` at runtime. `BaseViewModel` exposes `ThemePrimaryColor` and `ThemeMutedColor` as computed properties that resolve from app resources on every access, so XAML bindings update immediately on a theme change without requiring `{DynamicResource}` throughout the markup.

**Token expiry check on startup** — `AuthService.LoadAuthState` reads the stored token expiry from `Preferences` and clears the auth state immediately if the token has already expired, preventing the app from launching into an authenticated shell that immediately fails API calls.