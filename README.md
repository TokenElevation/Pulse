<div align="center">

<img src="OptimizationTool/UI/Assets/pulse_logo.png" alt="Pulse logo" width="140" />

# PULSE

**A fast, good-looking Windows optimizer for lower latency and smoother gaming.**
One window. Toggles for the tweaks that matter. Every change can be reverted.

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-WinForms-239120?style=for-the-badge&logo=csharp&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-00D6FF?style=for-the-badge)

</div>

---

## Table of contents

- [What is Pulse?](#what-is-pulse)
- [Features](#features)
- [Tweaks](#tweaks)
- [Safety and reverting](#safety-and-reverting)
- [Getting started](#getting-started)
- [Usage](#usage)
- [Settings](#settings)
- [Project structure](#project-structure)
- [Adding your own tweak](#adding-your-own-tweak)
- [Under the hood](#under-the-hood)
- [FAQ](#faq)
- [Disclaimer](#disclaimer)
- [Contributing](#contributing)
- [License](#license)

---

## What is Pulse?

Pulse is a desktop app that bundles the Windows tweaks gamers actually reach for, such as network latency settings, GPU scheduling, CPU parking, power plans, telemetry and background-app cleanup, into a single clean interface. Each tweak is a card you can switch on or off, and one-shot actions like cache cleanup run with a single click and show you the result right on the button.

It's also a showcase of what a fully custom-drawn **WinForms** interface can look like: neon-blue theme, heartbeat-style animations, and a spring-physics animation engine.

> This is a portfolio project. It works on my machines, but your system is not my system. Read [Safety and reverting](#safety-and-reverting) before applying anything.

---

## Features

### Optimization
- **Dozens of tweaks across six categories:** General, Precision, Ping, GPU, FPS and CPU.
- **Toggles and actions:** toggles apply and revert a setting, actions (like clearing caches) run once.
- **Revert support:** each toggle has a matching revert that restores the original Windows values.
- **Optimise everything / Undo all:** apply or revert every tweak in one go (risky tweaks are always skipped and must be enabled individually).
- **Restart prompts:** tweaks that need a reboot tell you, and offer to restart.
- **Risky tweak warnings:** advanced tweaks (like disabling CPU mitigations) are marked **(Risk)**, pulse red, and ask for confirmation first.
- **Live status:** a header counter shows how many tweaks are currently active.

### Interface
- **Pulse theme:** deep blue to cyan, pulled straight from the logo.
- **Living cards:** active tweaks show a flatline that turns into a heartbeat that sweeps across the card, with the border lighting up wherever the pulse is.
- **Action feedback:** EXECUTE buttons switch to *Running*, then *Done* or *Failed*, with no popups.
- **Themes:** Pulse, Mono, Violet, Ocean, Crimson and Emerald, applied instantly.
- **Custom background:** use your own image with an adjustable dim level.
- **Themed dialogs:** custom message boxes with severity colors and vector icons instead of the default Windows ones.
- **Smooth everywhere:** spring-based animations, flash-free window open, minimize and restore.
- **Animations toggle:** turn them all off in Settings to save a little CPU.

### Extras
- **Discord Rich Presence:** optionally show your optimization status (`X/Y Tweaks Active`) on your profile.
- **Settings that persist:** theme, background, dim level and options are saved between runs.

---

## Tweaks

Names below match the in-app cards. Each card shows a short description of exactly what it changes.

| Category | Tweak | Type |
|---|---|---|
| **General** | DNS cache flush | Action |
| | Temp file cleanup | Action |
| | Prefetch cleanup | Action |
| | Event log clear | Action |
| | Visual effects | Toggle |
| | Empty recycle bin | Action |
| | Disable Bing search | Toggle |
| | Browser cache cleanup | Action |
| | Disable telemetry | Toggle *(reboot)* |
| | Disable background apps | Toggle |
| | Disable tips and suggestions | Toggle |
| **Precision** | System responsiveness | Toggle |
| | Overlay widget | Toggle |
| | Mouse and keyboard optimizations | Toggle |
| | Storage optimization | Toggle |
| | Windows settings | Toggle |
| | Game Bar recordings | Toggle |
| | SuperFetch | Toggle |
| | Windows indexing | Toggle |
| | Disable startup delay | Toggle |
| | Menu show delay | Toggle |
| | Disable hibernation | Toggle *(reboot)* |
| **Ping** | Network tweaks | Toggle |
| | Network throttling | Toggle *(reboot)* |
| | Network responsiveness | Toggle *(reboot)* |
| **GPU** | Hardware-accelerated GPU scheduling | Toggle *(reboot)* |
| | GPU priority optimization | Toggle *(reboot)* |
| **FPS** | Game Mode | Toggle |
| | Power plan | Toggle |
| **CPU** | Core parking disable | Toggle |
| | Priority separation | Toggle |
| | Disable CPU mitigations | Toggle *(reboot, **risky**)* |

---

## Safety and reverting

Pulse changes Windows registry values, power plans and services. That's powerful, so please be careful.

- **Create a System Restore point first.** Search "Create a restore point" in Windows and make one before applying tweaks.
- **Apply a few at a time** and test, rather than switching everything on at once.
- **Every toggle can be reverted** from its card, and **Undo all** in Settings reverts everything. Pulse restores the original Windows default values.
- **Risky tweaks** (marked **(Risk)**, red pulse) trade security or stability for performance. You'll get a warning before enabling one, and they are *never* included in "Optimise everything".
- **Actions are not undoable.** Clearing event logs or caches deletes data permanently. Event log clear in particular removes troubleshooting history.
- Revert works in the cases I tested, but I can't promise it works on every machine.

---

## Getting started

### Requirements

- Windows 10 or Windows 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Administrator rights (needed to change system-wide registry values and services)

### Download

Grab the latest build from the **[Releases](../../releases)** page, extract it, and run `OptimizationTool.exe` as administrator.

### Build from source

You'll need [Visual Studio 2022](https://visualstudio.microsoft.com/) with the **.NET desktop development** workload, or the .NET 8 SDK.

```bash
git clone https://github.com/<your-username>/<your-repo>.git
cd <your-repo>

# build
dotnet build -c Release

# or publish a ready-to-run folder
dotnet publish -c Release -r win-x64 --self-contained false
```

In Visual Studio: open the `.sln`, set the configuration to **Release**, then **Build → Build Solution**. Run the app as administrator.

> Make sure `UI/Assets/pulse_logo.png` has **Copy to Output Directory** set to *Copy if newer*, or the logo won't appear in the title bar.

---

## Usage

1. **Pick a category** with the tabs at the bottom: General, Precision, Ping, GPU, FPS, CPU.
2. **Toggle** a card to apply or revert a tweak. Active cards glow and pulse.
3. **Run an action** with its **EXECUTE** button. It shows *Running*, then *Done* or *Failed*.
4. **Watch the counter** in the top-right: it shows how many tweaks are active.
5. **Open Settings** (gear icon in the title bar) for themes, backgrounds, bulk actions and options.
6. **Restart** when a tweak says it needs one. Pulse will offer to do it for you.

Press **Esc** to close Settings. The **?** button in the title bar opens the FAQ and safety guide.

---

## Settings

| Section | What's in it |
|---|---|
| **Appearance** | Theme picker, custom background image, background dim slider |
| **General** | Animations on/off, confirm bulk actions, Discord Rich Presence |
| **Actions** | Optimise everything, Undo all |
| **About** | Open the settings folder, reset all settings |

Settings are stored in a small file in your user profile (the About page shows the exact folder) and are loaded on startup.

---

## Project structure

```
OptimizationTool/
├── Core/
│   ├── Interfaces/        ITweak
│   ├── Services/          Registry, process and backup services, TweakManager
│   └── Tweaks/            One class per tweak (+ registry/power helpers)
├── UI/
│   ├── Assets/            Logo and images
│   ├── Controls/          Custom-drawn controls
│   │   ├── CustomTweakCard    The pulse cards
│   │   ├── DockBar            Category tab bar
│   │   ├── PillButton         Tabs and buttons
│   │   ├── SettingsOverlay    Settings screen
│   │   ├── SliderBar / ToggleSwitch
│   │   └── Tween              Spring-physics animation engine
│   ├── MainForm.cs        Main window, animations, tweak wiring
│   ├── Theme.cs           Theme presets and color system
│   ├── ThemedDialog.cs    Custom message boxes
│   ├── Titlebar.cs        Custom title bar
│   ├── Appsettings.cs     Persistent settings
│   └── Backgroundmanager.cs
├── DiscordRpc.cs          Discord Rich Presence
├── Program.cs
└── app.manifest
```

---

## Adding your own tweak

Every tweak implements `ITweak`. The UI picks it up automatically from the list in `MainForm`.

```csharp
public class MyExampleTweak : ITweak
{
    public string Id => "MyExample";
    public string Title => "My example";
    public string Description => "One short sentence about what this changes.";

    public bool IsAction => false; // true = one-shot EXECUTE button, false = toggle

    public bool IsApplied() { /* read the current state */ }
    public void Apply()     { /* change the setting   */ }
    public void Revert()    { /* restore the original */ }
}
```

Then:

1. Add `new MyExampleTweak()` to the right category in `MainForm`'s `tweaksByCategory`.
2. If it needs a restart, add its `Id` to `RebootRequiredTweaks`.
3. If it's risky, add its `Id` to `RiskyTweaks` so it gets the warning and red styling.

> Check `ITweak.cs` for the exact members in your version, and use the existing registry helpers and backup classes so `Revert` stays reliable.

---

## Under the hood

A few things that might interest you if you like reading code:

- **Custom-drawn UI.** Nearly every control is painted with GDI+ rather than stock WinForms controls, including the pulse cards, switches, sliders, dock and dialogs.
- **Spring-physics animations.** `Tween` is a critically damped spring driven by the display's vsync (`DwmFlush`), so hovers and transitions feel natural and never overshoot.
- **Pulse rendering.** Cards draw a flatline that becomes an ECG waveform made from a sum of Gaussians, with a travelling glow and an edge highlight that follows it. All of it is painted under the text.
- **Flash-free windows.** The main window and dialogs avoid the usual WinForms white and black flashes by keeping layered-window opacity between 1% and 99.9%, disabling DWM show transitions, and painting before revealing.
- **Custom minimize, restore and close** animations while keeping native taskbar behavior.
- **One shared animation clock** that only runs while something is actually animating, and pauses behind dialogs.
- **Async actions.** EXECUTE tweaks run off the UI thread, so the window never freezes.

---

## FAQ

**Do I need to run it as administrator?**
Yes. Most tweaks change system-wide registry values and services.

**Will this definitely improve my FPS or ping?**
No. Results vary a lot between systems. Some tweaks matter on some hardware and do nothing on others.

**Can I undo everything?**
Toggles can be reverted individually or all at once with *Undo all*. Actions like clearing caches and logs can't be undone.

**Why is "Optimise everything" skipping some tweaks?**
Risky tweaks and one-shot actions are never included. Risky ones have to be enabled on their own, after a warning.

**Is it safe?**
It's built to be reversible, but it's still a system tweaking tool. Make a restore point first.

**Where are my settings saved?**
In your user profile. *Settings → About → Open folder* takes you there.

---

## Disclaimer

Pulse modifies Windows system settings. It is provided **as is**, with **no warranty** of any kind. You use it at your own risk. The author is not responsible for data loss, instability or any other damage. This is a portfolio project and may still contain bugs.

---

## Contributing

Issues and pull requests are welcome. If you add a tweak, please include a clear description, a working `Revert`, and a note if it needs a reboot or carries risk.

1. Fork the repo
2. Create a branch: `git checkout -b feature/my-tweak`
3. Commit your changes
4. Open a pull request

---

## License

Distributed under the **MIT License**. See [`LICENSE`](LICENSE) for details.

<div align="center">

Made by TokenElevation · If you like it, drop a ⭐

</div>
