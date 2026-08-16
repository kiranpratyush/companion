# Hello Companion — Product and Technical Specification

Status: First version implemented; hardening remains  
Last updated: 2026-08-16

## 1. Product idea

Hello Companion is a lightweight Windows companion that lives primarily in the notification area (system tray). It stays out of the way and periodically greets the user.

The first useful version should prove three things:

1. The app can remain available without keeping a normal window on the taskbar.
2. It can run a reliable interval schedule while the user is signed in.
3. It can show a friendly Windows notification saying “Hello 👋”.

“Say hello” is provisionally interpreted as a visual Windows notification. Spoken text-to-speech is a possible later feature.

## 2. Terminology

- **Notification area/system tray:** the icon area near the clock. This is the intended home of the companion.
- **Taskbar button:** the normal button shown for an open window. The app should not keep one visible while running in the background.
- **Greeting:** the periodic “Hello 👋” notification.

## 3. MVP experience

### First launch

- Open a small WinUI 3 settings window.
- Explain that the app can continue running after the window is closed.
- Start with greetings enabled.
- Use a provisional interval of 15 minutes until we choose a final default.

### While running

- Show a notification-area icon.
- Closing the settings window hides it; it does not terminate the app.
- At each due time, ask one available desktop pet to stop and show “Hello 👋” in an anchored speech bubble.
- If desktop pets are disabled or unavailable, show a Windows app notification titled “Hello Companion” with body “Hello 👋”.
- Do not replay missed greetings in a burst after sleep, hibernation, or sign-out.
- Schedule the next greeting from the time the app resumes.

### Notification-area menu

- Open Hello Companion
- Pause greetings / Resume greetings
- Say hello now
- Exit

### Settings window

- Greetings-enabled toggle
- Interval value and unit
- Next greeting time
- Save/apply action
- Clear explanation of how to exit the app

## 4. Functional requirements

| ID | Requirement |
|---|---|
| FR-01 | Only one instance of the app may run per signed-in user. |
| FR-02 | The app exposes a notification-area icon and context menu. |
| FR-03 | The settings window can be shown, hidden, and restored from the icon. |
| FR-04 | The user can choose an interval from 1 minute to 24 hours. |
| FR-05 | The app displays one greeting from an available pet when the interval elapses, with a Windows notification fallback. |
| FR-06 | The user can pause and resume scheduled greetings. |
| FR-07 | “Say hello now” triggers a greeting without changing the schedule. |
| FR-08 | The chosen interval and enabled state persist between launches. |
| FR-09 | Explicit Exit removes the tray icon, stops scheduling, and terminates cleanly. |
| FR-10 | Sleep/resume never causes multiple missed greetings to fire at once. |

## 5. Non-functional requirements

- Idle CPU usage should be effectively zero between timer events.
- Idle memory should remain modest; the first measurement target is under 150 MB.
- Startup target: under two seconds on a typical Windows 11 machine.
- No administrator privileges.
- Settings writes should survive a crash without corrupting the previous settings.
- The tray icon must be removed during normal shutdown and cleanup.
- The settings UI should be keyboard accessible and support high contrast.

## 6. Proposed technology

- Language: C#
- UI: WinUI 3
- Runtime: .NET 10
- Windows platform: Windows App SDK 2.3.1 stable
- Initial packaging: single-project MSIX
- Initial architectures: x64 and ARM64
- Minimum OS: Windows 10 version 1809 (build 17763)

The scaffold follows Microsoft’s packaged WinUI 3 model. MSIX gives the app package identity, clean install/uninstall, and a solid foundation for Windows notifications. We can switch to an unpackaged self-contained build later if portable distribution matters more than MSIX integration.

## 7. Proposed architecture

    App
    ├── MainWindow                    WinUI settings/status surface
    ├── TrayIconService              Shell_NotifyIcon lifecycle and menu
    ├── GreetingScheduler            Interval and next-due calculation
    ├── GreetingNotificationService  Windows notification delivery
    ├── SettingsService              Load/save preferences
    └── SingleInstanceService        Redirect duplicate activation

### Implementation notes

- WinUI 3 has no first-party managed tray-icon control. Use Win32 Shell_NotifyIcon interop behind TrayIconService.
- Use a cancellable asynchronous timer; never spin or continuously poll.
- Marshal UI work to the WinUI dispatcher.
- Intercept window close so it hides unless the user explicitly chose Exit.
- Keep window and tray lifecycles separate.
- Store settings as JSON in the app’s local data directory.

## 8. Suggested settings model

    public sealed record CompanionSettings(
        bool GreetingsEnabled,
        TimeSpan GreetingInterval);

NextGreetingAt is runtime state and does not need to be persisted in the MVP.

## 9. Edge cases

- Invalid settings fall back to defaults and create a diagnostic log entry.
- If Windows notifications are disabled, keep running and show status in the settings window.
- If the tray icon cannot be created, keep the window visible and offer Exit.
- Applying a new interval cancels the old wait and schedules from that moment.
- Clock or time-zone changes must not trigger a greeting burst.
- Recreate the tray icon after Windows Explorer restarts.

## 10. Privacy and security

- No account, analytics, network access, or cloud storage in the MVP.
- No microphone permission for visual greetings.
- No elevated privileges.
- Logs must not capture unrelated user activity.

## 11. Testing

Unit tests:

- Interval validation and next-due calculation
- Pause/resume transitions
- Settings serialization and fallback behavior
- Sleep/resume policy

Integration/manual tests:

- Tray icon creation/removal and Explorer restart recovery
- Restore window and close-to-tray behavior
- Notification delivery and single-instance activation
- MSIX install, upgrade, and uninstall
- x64 and ARM64 package builds

Use an injectable clock so timer tests are instant and deterministic.

## 12. Milestones

### M0 — Foundation

- Agree on this specification.
- Confirm name, wording, interval, and packaging.

### M1 — Vertical slice

- [x] Add tray icon and menu.
- [x] Add in-memory scheduler.
- [x] Show “Hello 👋” as a Windows notification.
- [x] Implement close-to-tray and explicit Exit.

### M2 — Preferences

- [x] Add settings UI and persistence.
- [x] Add pause/resume and “Say hello now”.
- [x] Add single-instance behavior.

### M3 — Hardening

- Handle sleep/resume and Explorer restart.
- Add tests and diagnostics.
- Add final icons and produce a signed MSIX.

Later ideas: text-to-speech, quiet hours, custom greetings, sign-in startup, animations, and context-aware greetings.

## 13. First-slice acceptance criteria

1. Launch creates exactly one notification-area icon.
2. Closing the window removes its taskbar button but leaves the app running.
3. “Say hello now” displays exactly one greeting.
4. A short test interval displays exactly one greeting when due.
5. Pause prevents scheduled greetings.
6. The tray menu can restore the settings window.
7. Exit removes the icon and process.

## 14. Decisions to make together

1. Is “Hello Companion” the final name?
2. Should “say hello” be visual, spoken, or both?
3. What is the default interval?
4. Should the MVP start automatically at sign-in?
5. Should closing the window always hide it to the tray?
6. Do we prefer MSIX or a portable/self-contained executable?
7. Should greetings respect quiet hours from the first version?

