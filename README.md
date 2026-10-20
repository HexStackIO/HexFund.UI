# HexFund UI

HexFund is a personal cash-flow planning app for Android and iOS, built with .NET MAUI. This repository
is the mobile client. The backend is [HexStackIO/HexFund.API](https://github.com/HexStackIO/HexFund.API).

This document is both a developer guide and a product and engineering reference. Everything in it was
derived from the code in this repository. Items that could not be confirmed from the code are marked
**(unverified)**, and anything that is a suggestion rather than a fact is in
[section 11](#11-roadmap-and-enhancement-ideas).

**Contents**

1. [Product overview](#1-product-overview)
2. [Screens and navigation](#2-screens-and-navigation)
3. [Architecture](#3-architecture)
4. [Tech stack](#4-tech-stack)
5. [Authentication](#5-authentication)
6. [Backend contract](#6-backend-contract)
7. [Getting started](#7-getting-started)
8. [Release process](#8-release-process)
9. [Privacy and data handling](#9-privacy-and-data-handling)
10. [Known limitations and tech debt](#10-known-limitations-and-tech-debt)
11. [Roadmap and enhancement ideas](#11-roadmap-and-enhancement-ideas)
12. [Glossary](#12-glossary)

---

## 1. Product overview

### What it is

HexFund lets a person record their accounts and their income and expenses, one-time or recurring, and then
see what their balance will look like day by day and month by month. The balance is driven by the
transactions the user enters. The app does not connect to banks (the account deletion page states this
explicitly).

Tagline in the app: "Plan. Track. Grow." / "Take control of your finances."

### Who it is for and the problem it solves

The audience and positioning below are inferred from the app's features and copy. No market research or
user data is in this repository.

| Question | Answer from the code |
|----------|----------------------|
| Who is it for? | Individuals who want to plan their own cash flow by hand, without bank linking. Accounts are described in the app as "a real wallet, bank account, or savings pot". |
| What problem does it solve? | It answers "what will my balance be on a given day?" by expanding recurring income and bills (paychecks, rent, subscriptions) into dated occurrences on a calendar, with monthly totals and spending breakdowns. |
| What makes it different? | Recurring transactions can be amended from a date forward while keeping the earlier version as history. Schedules include a "1st and 3rd Friday" pattern. Eight accent themes on a dark "vault" design. |

### Core user flows

1. **Sign in or register** through a hosted Microsoft Entra External ID page (one button: "Sign In / Register").
2. **Onboarding** on the Home page walks a new user through creating an account and adding a first
   transaction. It can be skipped and reset (Settings, "Reset Tutorial").
3. **Create an account** with a name and a starting balance (USD).
4. **Add a transaction** (income or expense, amount, description, category, frequency, start date and optional
   end date) from the floating hexagon button, the Home page or the calendar.
5. **View the calendar** for a month: starting and ending balance, income, expenses and net change, a day
   grid with indicators, and the occurrences of a selected day.
6. **Manage transactions** on the Ledger tab: sort, filter, edit, amend from a date, deactivate, delete, and
   browse the amendment history.
7. **Review insights**: spending by category, a six-month net-change trend and spend-spike alerts.
8. **Manage the profile**: edit the name, pick a theme, manage categories, sign out or delete the account.

### Features implemented

| Area | What the code does |
|------|--------------------|
| Authentication | Microsoft Entra External ID sign-in and registration through MSAL (silent token first, then interactive). Sign out. In-app permanent account deletion with a typed confirmation phrase. |
| Accounts | Create (name up to 25 characters, starting balance within +/- 1,000,000,000), rename, activate or deactivate, delete (with its transactions), select the active account. Names must be unique, case-insensitive. Currency is always "USD" in the client. |
| One-time transactions | Frequency "Once": a single dated occurrence. The end date equals the start date. |
| Recurring transactions | Seven frequency types, see the table below. Optional end date (up to 100 years after the start). |
| Income and expense | Amount is entered as a positive number and the sign is applied from the income or expense toggle. Amounts must be above zero and at most 1,000,000,000. |
| Amend from a date | "Amend" changes the amount, description, category or colour from an effective date forward. The server creates a successor transaction and the old one becomes history. The client snaps the effective date to the next date on the schedule to avoid a spurious one-off occurrence. |
| History | Superseded versions are hidden from the main list and shown in an expandable "History" section of the current version, with Edit and Restore actions. |
| Activate and deactivate | A transaction can be switched off ("Hide") and on again without deleting it. |
| Categories | User-defined categories (up to 25 characters, unique), created inline while adding a transaction or in Settings. "Uncategorized" is the default. |
| Colours | Transactions can carry a custom colour (hex string) that tints cards and calendar chips. The colour picker UI is not present in the current pages, see [section 10](#10-known-limitations-and-tech-debt). |
| Sorting and filtering | Ledger sort by start date, name, amount or recurrence, ascending or descending. Filters: recurrence, date range, amount range, category text. |
| Calendar | Month grid (42 cells), month navigation, jump to today, month and year picker, swipe left and right to change month, day panel with summary cards, end-of-day balance and the day's transactions, and an add button for the selected day. Negative-balance days are outlined in red. |
| Home dashboard | Balance of the selected account, net change this month, top three active accounts by balance, five most recent and five upcoming occurrences across all active accounts, onboarding prompts. |
| Insights | Expense breakdown by category for a month (needs at least five transactions), six-month net-change bars, and alerts when a category rose more than 20% compared with the previous month. |
| Profile | View and edit first and last name. The email is read-only. |
| Theming | Eight accent themes (Gold, Sapphire, Emerald, Bronze, Obsidian, Ruby, Amethyst, Platinum) on a fixed dark surface, with per-theme logo, backdrop and button images. Gold is the default. |
| Changelog | A "What's New" dialog reads `Resources/changelog.json`. |
| Feedback | Signed-in users can send a bug report, idea or question from Settings, with optional diagnostics (off by default) and a draft that survives failures. Signed-out users can open the support page. |
| Utilities | Clear in-memory cache, reset onboarding tutorial. |

### Frequency types

These are the values of `FrequencyType`. The schedule itself is computed by the API; the client's description
below comes from its schedule-snapping logic and display strings.

| Value | Name | Display text | Meaning |
|-------|------|--------------|---------|
| 0 | `Once` | Once | A single occurrence on the start date. |
| 1 | `Daily` | Daily | Every day. |
| 2 | `Weekly` | Weekly | Every week on the weekday of the start date. |
| 3 | `BiWeekly` | Bi-Weekly | Every second week on the weekday of the start date. |
| 4 | `FirstThirdFriday` | 1st & 3rd Friday | The first and third Friday of each month. |
| 5 | `Monthly` | Monthly | Every month on the day of the month of the start date (clamped to the last day of short months). |
| 6 | `BiMonthly` | Bi-Monthly | Every second month on the day of the month of the start date. The client treats it as every two months. |

### Platform and store status

| Item | Status |
|------|--------|
| Application ID | `com.hexstackio.hexfund` |
| Android | Targets Android 16 (API level 36), minimum Android 7.0 (API 24, required by Play automatic protection). Built as `net9.0-android36.0`. |
| iOS | `net9.0-ios` is in the project (minimum iOS 14.2) with iOS-specific code, but there is no iOS build or release pipeline in this repository. The iOS build was not verified in this repository's current state **(unverified)**. |
| Google Play | Closed testing. The release workflow uploads to the `alpha` track. The app is in a closed beta. |
| Latest changelog entry | 1.9 (2026-04-30). Version numbers are computed by CI, see [section 8](#8-release-process). |
| Windows, macOS | Not targeted. |

---

## 2. Screens and navigation

Navigation uses .NET MAUI Shell (`AppShell`). There are two tab bars. Only one is visible at a time,
depending on whether the user is signed in.

**Signed out (`AuthTabBar`)**

| Tab | Page | Purpose |
|-----|------|---------|
| Login | `LoginPage` | "Sign In / Register" button, themed backdrop and logo, error message. |
| Settings | `SettingsPage` | Guest view: theme and app information. A notice says the user is not logged in. |

**Signed in (`MainTabBar`)**

| Tab | Page | Purpose |
|-----|------|---------|
| Home | `HomePage` | Dashboard: balance hero, accounts preview, recent and upcoming activity, onboarding. A gear button opens Settings. |
| Calendar | `CalendarPage` | Month summary, day grid, selected-day panel, month picker. |
| Ledger | `TransactionsPage` | Transaction list with sort, filter, history, and the edit, amend, filter and delete dialogs. |
| Insights | `InsightsPage` | Category breakdown, six-month trend, spend alerts. |

**Pushed routes** (registered in `AppShell`)

| Route | Page | Purpose |
|-------|------|---------|
| `accounts` | `AccountsPage` | List, select, create, edit and delete accounts. |
| `settings` | `SettingsPage` | Profile, theme, categories, app information, changelog, cache and tutorial reset, logout, account deletion. |
| `feedback` | `FeedbackPage` | Send feedback: type chips, message with counter, optional diagnostics with a "What is sent" preview, send button. Opened from Settings. |
| `add` | `AddEntryPage` | Create a transaction: income or expense toggle, amount, description, category chips with inline new category, frequency, start date, optional end date, account picker. |

**Other UI elements**

| Element | Behaviour |
|---------|-----------|
| Floating hexagon button | Injected into the native window after sign-in (`App.xaml.cs`), pulsing, themed per theme. Opens `add` when an account is selected, otherwise `accounts`. Nudges the Calendar and Ledger tab labels apart. |
| `ExitConfirmationPage` | Transparent modal shown when the Android back button is pressed on a root tab, instead of closing the app. |
| Back button on the Ledger | Closes an open dialog before anything else. |

---

## 3. Architecture

### Pattern

MVVM with CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Pages are XAML with
minimal code-behind. Each page gets its view model through constructor injection from the MAUI service
container (`MauiProgram`). Services are singletons and view models and pages are transient.

### Folder layout

```
Config/        AppConstants (URLs, TTLs, titles), EntraAuthConfig, AuthSecrets (generated, not committed)
Controls/      SwipeableCollectionView (CollectionView with swipe events)
Converters/    XAML value converters
Diagnostics/   GCMonitor (debug builds only)
Models/        DTOs, requests and calendar display models
Platforms/     Android (activities, manifest, handlers, tab bar effect) and iOS equivalents
Resources/     Fonts, images, styles, changelog.json
Services/      ApiService, AuthService, CacheService, AccountStateService, SettingsService, ThemeService
Validation/    UIValidator (client-side input rules)
ViewModels/    One view model per page, BaseViewModel, TransactionCardViewModel
Views/         XAML pages
whatsnew/      Google Play release notes (whatsnew-en-US)
.github/       Android build and distribution workflow
```

### Key services

| Service | Responsibility |
|---------|----------------|
| `IApiService` / `ApiService` | Typed HTTP client for the API. Reads are cached, writes invalidate the affected cache entries. Failures are logged to the debug output and returned as null, false or an empty list. |
| `IAuthService` / `AuthService` | MSAL sign-in (silent then interactive, 5 minute interactive timeout), sign-out, account deletion, `AuthStateChanged` event. Holds the access token and the current user. |
| `ICacheService` / `CacheService` | In-memory cache (`IMemoryCache`) with typed key helpers and calendar invalidation by account. |
| `IAccountStateService` | The selected account plus the `SelectedAccountChanged` and `TransactionsChanged` events used to coordinate pages. |
| `ISettingsService` | Device-local preferences: theme, calendar view mode, onboarding flag. |
| `IDiagnosticsService` | Builds the optional diagnostics sent with feedback: app and device info, the current tab, the theme and the recent failed requests. |
| `ThemeService` (static) | Builds and applies the theme resource dictionary. |

### State management

* The selected account lives in `AccountStateService`. Changing it reloads Home, Calendar and Insights.
* After any transaction is created, updated, deleted or amended, the originating view model calls
  `NotifyTransactionsChanged()`. Home, Calendar and Insights subscribe and reload.
* Theme changes raise `SettingsChanged`. `BaseViewModel` re-raises the theme bindings, `AppShell` recolours the
  tab bar, `App` swaps the button image and the calendar repaints today's cell.
* Preferences stored on the device: `color_theme`, `calendar_view_mode`, `onboarding_complete`, `token_expiry`.
  The MSAL token cache is managed by MSAL.

### Caching and offline behaviour

The cache is memory only and is lost when the app restarts. There is no offline mode: if a request fails the
app shows an empty state or an error message.

| Data | Cache lifetime |
|------|----------------|
| Accounts | 10 minutes |
| Transactions (current only, not the history variant) | 3 minutes |
| Calendar data (monthly overview, month and day occurrences) | 5 minutes |
| Categories | 30 minutes |

The calendar view model also keeps its own per-month cache. It shows a cached month immediately, refreshes a
stale month in the background and prefetches the neighbouring months.

### Error handling

* `ApiService` catches every exception and returns a null, false or empty result, so view models cannot
  distinguish a network error from an empty result or read the server's reason. A 409 on category creation is
  treated as a duplicate name.
* View models show field-level validation errors from `UIValidator` and a general error message for failed
  requests.
* The API now returns RFC 7807 problem details for errors. The client does not parse them yet, see
  [section 10](#10-known-limitations-and-tech-debt).

### Platform-specific code

| Concern | Android | iOS |
|---------|---------|-----|
| Swipe on lists | RecyclerView touch listener with a fling detector | Swipe gesture recognizers |
| Tab bar top line and tab nudge | `BottomNavigationView` layout listener | `UITabBarController` extension |
| Floating button | Added to the activity's decor view | Added to the key window |
| Sign-in redirect | `MsalActivity` with intent filter | Not present in this repository **(unverified)** |

---

## 4. Tech stack

| Component | Version |
|-----------|---------|
| .NET target | `net9.0-android36.0`, `net9.0-ios` |
| Build SDK | .NET 10 SDK with the Android workload (36.x), see [section 7](#7-getting-started) |
| .NET MAUI (`Microsoft.Maui.Controls`) | 9.0.10 |
| CommunityToolkit.Maui | 9.1.0 |
| CommunityToolkit.Mvvm | 8.3.2 |
| Microsoft.Identity.Client (MSAL) | 4.82.1 (through `Microsoft.Identity.Client.Extensions.Msal` 4.82.1) |
| Microsoft.Extensions.Http, Caching.Memory, Logging.Debug | 9.0.0 |
| System.Text.Json | 9.0.0 |
| Fonts | Cinzel Bold, Inter Regular and SemiBold (files in `Resources/Fonts`) |
| Android | Target API 36 (Android 16), minimum API 24 |
| iOS | Minimum 14.2 |
| Release build options | Partial trimming, profiled AOT, concurrent GC (Android); LLVM (iOS) |

---

## 5. Authentication

The app signs in with **Microsoft Entra External ID** (a CIAM tenant) using MSAL's public client flow.

1. The user taps "Sign In / Register". `AuthService.LoginAsync` first tries `AcquireTokenSilent` with any cached
   account.
2. If an interactive sign-in is needed (`MsalUiRequiredException`), MSAL opens the hosted Entra page in a
   browser tab (on Android with the current activity as parent). Registration happens on the same page.
3. The access token is set as the bearer token on the API client.
4. The app calls `POST auth/sync` so the API creates or loads the user record, and stores the returned user.
5. `AuthStateChanged` fires on the main thread. `AppShell` switches to the main tab bar and `App` adds the
   floating button.
6. On Android the redirect `msal{ClientId}://auth` returns through `MsalActivity`, and `MainActivity` forwards
   activity results to MSAL.

**Configuration keys** (`Config/EntraAuthConfig.cs`, filled in `MauiProgram`):

| Key | Source | Notes |
|-----|--------|-------|
| `ClientId` | `AuthSecrets.ClientId` | App registration (client) ID. The redirect URI is `msal{ClientId}://auth`. |
| `TenantId` | `AuthSecrets.TenantId` | Used to build the authority `https://financeplannerapp.ciamlogin.com/{TenantId}`. |
| `ApiClientId` | `AuthSecrets.ApiClientId` | The API's app registration. The requested scope is `api://{ApiClientId}/access`. |
| `TenantDomain` | Hard-coded in `MauiProgram` | Set but not used by the sign-in code. |

`Config/AuthSecrets.cs` is git-ignored. CI writes it from GitHub secrets (`HEXFUND_CLIENT_ID`,
`HEXFUND_TENANT_ID`, `HEXFUND_API_CLIENT_ID`). For local builds you create it yourself, see
[section 7](#7-getting-started).

**Sign-out** removes the MSAL accounts and calls `POST auth/logout`, then clears the token and the current user.
**Account deletion** calls `DELETE auth/user` first and only clears local state if that succeeds.

Tokens are not stored by the app itself apart from the `token_expiry` preference. The app does not refresh the
token on a timer: a silent acquire runs at the next sign-in. Behaviour when the access token expires during a
session is not handled explicitly in the code **(unverified)**.

---

## 6. Backend contract

The API is [HexStackIO/HexFund.API](https://github.com/HexStackIO/HexFund.API) (ASP.NET Core, .NET 10). The
client uses a named `HttpClient` with a 30 second timeout and gzip and deflate decompression.

**Base URL**

| Build | URL |
|-------|-----|
| Debug, Android emulator | `https://10.0.2.2:5001/api/` |
| Debug, other | `https://localhost:5001/api/` |
| Release | The hosted Azure App Service URL in `AppConstants` |
| Override | The `API_BASE_URL` environment variable, if set |

Debug builds accept any server certificate so the local development certificate works.

**Endpoints called** (paths relative to the base URL; all require the bearer token)

| Method and path | Used for |
|-----------------|----------|
| `POST auth/sync` | After sign-in: create or fetch the user. |
| `PUT auth/profile` | Save first and last name. |
| `POST auth/logout` | Sign out. |
| `DELETE auth/user` | Permanently delete the user and all data. |
| `GET accounts` | List accounts (Home, Accounts, Add entry). |
| `POST accounts` | Create an account. |
| `PUT accounts/{accountId}` | Rename or activate and deactivate. |
| `DELETE accounts/{accountId}` | Delete an account. |
| `GET accounts/{accountId}/transactions` | Current transactions. `?includeHistory=true` adds superseded versions. |
| `POST accounts/{accountId}/transactions` | Create a transaction. |
| `PUT accounts/{accountId}/transactions/{transactionId}` | Edit, activate or deactivate, restore. |
| `DELETE accounts/{accountId}/transactions/{transactionId}` | Delete. |
| `POST accounts/{accountId}/transactions/{transactionId}/amend` | Amend from an effective date. Returns the successor. |
| `GET accounts/{accountId}/monthly-overview?year=&month=` | Month totals, daily breakdown and category breakdown. |
| `GET accounts/{accountId}/transactions-for-month?year=&month=` | Occurrences grouped by date (`yyyy-MM-dd` keys). |
| `GET accounts/{accountId}/transactions-for-date?date=` | Occurrences of one day. |
| `POST feedback` | Submit a feedback report with optional diagnostics. Returns 202 with the report id. |
| `GET categories` | List the user's categories. |
| `POST categories` | Create a category (409 means duplicate). |
| `DELETE categories/{categoryId}` | Delete a category. |

**Contract notes**

* Occurrences returned by the calendar endpoints carry the `TransactionId` of the template they came from.
* The daily-balance, rolling-balance, balance-at-date and transactions-with-balances endpoints were removed from
  the API. This client does not call them.
* The API returns errors as RFC 7807 problem details. The client does not read the body.
* `ApiService` also defines `auth/login`, `auth/register` and `GET accounts/{id}`, and models for them. They are
  not called by the current sign-in flow (unused).

---

## 7. Getting started

### Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.x. The Android 16 (API 36) support ships with the .NET 10 Android workload. The project itself targets `net9.0`. |
| Workloads | `maui-android` and `android` (add `maui-ios` on macOS). |
| JDK | 17 |
| Android SDK | Platform 36 (Android 16) and build tools. Android Studio or the VS installer provides them. |
| Emulator or device | Android 7.0 or newer. An API 36 emulator is needed to test Android 16 behaviour. |
| Xcode | Only for iOS builds on macOS (iOS 14.2 deployment target). |

```bash
dotnet workload install maui-android android
```

There is no `global.json`, so the newest installed SDK is used.

### Configuration

1. Create `Config/AuthSecrets.cs` (it is git-ignored). Use the values from your Entra app registrations:

   ```csharp
   namespace HexFund.UI.Config;

   internal static class AuthSecrets
   {
       public const string ClientId = "<app registration client id>";
       public const string TenantId = "<tenant id>";
       public const string ApiClientId = "<API app registration client id>";
   }
   ```

2. Point the app at an API. Debug builds use `https://10.0.2.2:5001/api/` on an Android emulator and
   `https://localhost:5001/api/` elsewhere. To use another URL set the `API_BASE_URL` environment variable, or
   edit `Config/AppConstants.cs`. A physical device cannot reach `localhost` and needs your machine's LAN
   address.

### Run and build

```bash
dotnet restore HexFund.UI.csproj
dotnet build HexFund.UI.csproj -f net9.0-android36.0
dotnet run   --project HexFund.UI.csproj -f net9.0-android36.0     # with an emulator or device attached

dotnet publish HexFund.UI.csproj -f net9.0-android36.0 -c Release  # trimming and profiled AOT
dotnet build HexFund.UI.csproj -f net9.0-ios                        # macOS with Xcode only
```

### Tests

There is no automated test project in this repository. Verification is by building and manual testing.

### CI and distribution

`.github/workflows/android-distribution.yml` runs on every push to `master` and on manual dispatch. It runs on
`ubuntu-latest` and:

1. Computes the version from the run number.
2. Installs JDK 17, the .NET 10 SDK and the MAUI Android workload (cached).
3. Writes `Config/AuthSecrets.cs` from secrets and patches the version into the csproj and `AppConstants`.
4. Publishes a `net9.0-android36.0` AAB in Release, signs it with `jarsigner` using the keystore from secrets,
   and uploads it as a build artifact.
5. Uploads the AAB to the Google Play `alpha` (closed testing) track with `whatsnew/` as release notes.

Required repository secrets: `HEXFUND_CLIENT_ID`, `HEXFUND_TENANT_ID`, `HEXFUND_API_CLIENT_ID`,
`KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_PASSWORD`, `KEY_ALIAS`, `SERVICE_ACCOUNT_JSON`.

---

## 8. Release process

| Step | Detail |
|------|--------|
| Versioning | The workflow sets `ApplicationVersion` (Android version code) to Minor and `ApplicationDisplayVersion` to `{VERSION_MAJOR}.{Minor}`, where Minor is `github.run_number + MINOR_OFFSET` (currently -9, so run 14 was 1.5). Change `VERSION_MAJOR` and the offset in the workflow to bump the major version. |
| In-app version | The workflow also rewrites `AppVersion` in `Config/AppConstants.cs`. `BuildNumber` there is a manual display value. |
| Committed values | `HexFund.UI.csproj` currently says 1.5 / 5. These are placeholders that CI overwrites. |
| Release notes | `whatsnew/whatsnew-en-US` is sent to Google Play with each upload. Update it before pushing to `master`. |
| In-app changelog | Prepend an entry to `Resources/changelog.json` so "What's New" shows it. It is a manual step. |
| Track | Google Play `alpha` (closed testing). Promotion to other tracks is done in the Play Console. |
| Target API | Google Play requires new releases to target Android 16 (API 36) or higher. The project targets 36. |
| Signing | Release signing uses a keystore from GitHub secrets. Do not commit it. |

---

## 9. Privacy and data handling

Based on `privacy-policy.html` (last updated October 20, 2026), `delete-account.html` and the code.

**Data the app handles**

| Data | Where it goes |
|------|---------------|
| Email, first and last name | Entra External ID, and the HexFund API for the user record. The name is editable in the app. |
| Accounts, balances, transactions, categories | The HexFund API (Azure, United States according to the privacy policy). |
| Authentication tokens | Managed by MSAL on the device. |
| Preferences (theme, view mode, onboarding flag, token expiry time) | Device-local preferences. |
| API responses | In memory only, cleared on restart or "Clear Cache". |
| Feedback text and optional diagnostics | The HexFund API, which stores them in its database and copies them into a private, maintainer-only GitHub issue. The feedback text is analyzed by Anthropic's Claude to categorize and summarize it. |
| Unsent feedback draft | Device-local preferences (not encrypted). Cleared on success, sign-out and account deletion. |

The app does not connect to bank accounts, and the code contains no analytics or advertising SDKs. The
privacy policy says data is not sold, and names the service providers it relies on (Microsoft, GitHub and
Anthropic).

**Feedback and diagnostics**

* The diagnostics switch is off by default and can be changed for each report. The "What is sent" preview is
  rendered from the same object that is transmitted.
* Diagnostics contain: app version, build number, platform, OS version, device model, the current tab, the theme
  and up to 20 recent failed requests (method, a sanitized path with ids replaced by `{id}`, status or exception
  type name, correlation id). They never contain email, name, user id, tokens, account names, amounts,
  transaction descriptions, category names, query strings or response bodies.
* The issue created from a report carries an opaque reference and no user id, email or name. Database rows are
  deleted after 12 months or with the account. Issues are not deleted with the account: they are de-identified,
  and the maintainer redacts and closes the issue of a deleted user on request.
* The failure buffer lives in memory only and is lost when the app restarts.

**Account deletion**

* In the app: Settings, "Delete My Account". The user types `DELETE MY ACCOUNT`, confirms a second alert, and
  the app calls `DELETE auth/user`. On success it removes the MSAL accounts, clears local state and returns to
  the login screen. If the server call fails nothing local is cleared and the user can retry.
* The hosted page `delete-account.html` also describes an email request path for users who cannot open the app.
* Deleting the HexFund account does not delete the user's Microsoft account.

**Documentation**

* `privacy-policy.html`, `delete-account.html` and the new `support.html` are served by GitHub Pages from the
  `master` branch.
* The deletion page describes the in-app steps (Home gear, Settings, Danger Zone) and says deletion is
  immediate, which matches the API: one call removes the Entra identity and the database rows.
* The policy mentions "improving app performance and reliability", but no telemetry code exists in this client.
* The deletion page footer still links to `hexstack.io/privacy` and `hexstack.io/terms`, which are outside this
  repository.

---

## 10. Known limitations and tech debt

| Area | Issue |
|------|-------|
| Errors | Every `ApiService` call goes through one `SendAsync` that returns an `ApiResult` (kind, status, parsed problem details, `Retry-After`, correlation id). Only `SubmitFeedbackAsync` exposes it. The older methods still collapse failures to null, false or empty, so those screens cannot tell a failed request from an empty list or show the API's problem details. |
| Session expiry | The access token is set once at sign-in. No refresh or 401 handling was found in the code **(unverified)**. |
| Calendar list mode | `CalendarViewMode` (List or Grid) and a view model flag exist, but no screen lets the user change it and the Calendar page only renders the grid. |
| Colour picker | View models support a transaction colour, but the current pages have no colour picker (it was removed from the XAML). Colours set elsewhere still render. |
| Restore is not atomic | Restoring a previous version deletes the successor and then updates the predecessor in two calls. If the second fails, the successor is already gone. |
| Dead code | `ApiService.LoginAsync`, `RegisterAsync` and `GetAccountAsync`, plus `AuthResponse`, `LoginRequest` and `RegisterRequest`, are unused. `EntraAuthConfig.TenantDomain` is unused. |
| Hard-coded values | The CIAM authority host (`financeplannerapp.ciamlogin.com`), currency `USD` and the production API URL are constants in code. |
| Fonts | `MauiProgram` registers OpenSans fonts, but only Cinzel and Inter files are in `Resources/Fonts` **(unverified at runtime)**. |
| Deprecated API | `Application.MainPage` is used in several view models (MAUI deprecation warnings). |
| Warnings | The build reports about 58 warnings (uncompiled bindings with `Source`, `MainPage`, unused variables, MVVM Toolkit field access). |
| Android 16 | Edge-to-edge is enforced when targeting 36. The floating button uses fixed bottom margins and was not verified against system bar insets **(unverified)**. Predictive back relies on Shell's back handling. These need a test on an API 36 device. |
| Android 16 and 35 | MAUI 9.0.10 is not officially validated against API 36. A move to .NET 10 and MAUI 10 is the long-term path. |
| iOS | No pipeline, and the iOS sign-in redirect configuration is not in this repository **(unverified)**. |
| Tests | No unit or UI tests. |
| Partial amend semantics | The client only snaps the effective date. Whether an amend before the transaction's start or after its end is rejected is decided by the API **(unverified)**. |
| Restore and history | History is rebuilt client-side from the `includeHistory` list, so large histories load all versions. |

---

## 11. Roadmap and enhancement ideas

**These are suggestions grounded in what the code suggests. They are not commitments and none of them exists.**

| Idea | Why the code suggests it |
|------|--------------------------|
| Show API error messages from problem details | The API now returns RFC 7807 errors and the client discards them. |
| Token refresh and 401 handling | Sessions currently rely on the sign-in token. |
| Move to .NET 10 and MAUI 10 | The build toolchain is already .NET 10, and API 36 is officially supported there. |
| Calendar list mode and a setting to switch | The setting and the view mode enum exist without UI. |
| Colour picker for transactions | Models, requests and rendering already carry a colour. |
| Atomic restore | A server-side "restore version" endpoint would remove the delete-then-update gap. |
| Multi-currency accounts | `Currency` is a field on accounts but the client always sends USD. |
| Offline cache | Only an in-memory cache exists, so the app is empty without a network. |
| Budgets and goals | The deletion page mentions "budgets", which the app does not implement. |
| Reminders for upcoming bills | The Home page already computes upcoming occurrences. |
| Export (CSV or PDF) | Not present. |
| Tablet and landscape layouts | Android 16 removes orientation restrictions on large screens, and the pages are phone layouts. |
| Telemetry and crash reporting | Only debug logging exists today. If added, update the privacy policy. |
| Automated tests and a CI test step | None exist. |
| iOS release pipeline | iOS code exists without a build or store process. |
| Localization | All strings are hard-coded in English, and amounts use the device culture currency format. |

---

## 12. Glossary

| Term | Meaning |
|------|---------|
| Account | A named balance container (wallet, bank account, savings). Has an initial balance and a current balance. Can be active or inactive. |
| Selected account | The account all pages currently show. Held by `AccountStateService`. |
| Transaction | A one-time or recurring income or expense definition, also called a template. Amount is positive for income and negative for an expense. |
| Occurrence | One dated instance generated from a transaction. Carries the `TransactionId` of its template. |
| Frequency | How a transaction repeats, see the frequency table. |
| Start date, end date | The range in which a transaction produces occurrences. A one-time transaction ends the day it starts. |
| Amend | Change a recurring transaction from an effective date forward. The old version ends and a new version starts. |
| Effective date | The date from which an amendment applies. Snapped to the next scheduled date by the client. |
| Predecessor | The earlier version of an amended transaction (`PredecessorTransactionId` on the newer one). |
| Successor | The newer version created by an amendment. |
| Superseded | A version that has been replaced by a successor and has ended. Hidden from the main list. |
| History | The chain of predecessors shown under a transaction. |
| Restore | Make a previous version the current one again by deleting its successor. |
| Active, inactive | A switch on a transaction or account. Inactive items stay stored. Inactive accounts are left out of the Home dashboard and the add-entry account picker. |
| Category | A user-defined label for a transaction. "Uncategorized" means none. |
| Monthly overview | The API summary of a month: starting and ending balance, income, expenses, net change and breakdowns. |
| Net change | Income minus expenses for a period. |
| Daily snapshot | One day of the monthly overview with start and end balances and totals. |
| Vault, vault theme | The fixed dark design system. Themes only change the accent. |
| Accent, AccentInk, AccentGlow | Theme tokens: the main accent color, a light tint for text on the accent, and a translucent glow. |
| Hex button, FAB | The floating hexagon button that opens the add-entry page. |
| Entra External ID (CIAM) | The Microsoft customer identity service used for sign-in. |
| MSAL | Microsoft Authentication Library, used for token acquisition. |
| Closed testing, alpha track | The Google Play track the app is released to. |
