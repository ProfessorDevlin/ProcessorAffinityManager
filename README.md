<p align="center">
  <img src="icon-source.jpg" width="120" alt="Prozessor Affinity Manager icon">
</p>

<h1 align="center">Prozessor Affinity Manager</h1>

<p align="center">
  Pin Windows programs to specific CPU cores – automatically, every time they start.
</p>

---

Windows lets you set a program's processor affinity in Task Manager, but the setting is lost
as soon as the program restarts. **Prozessor Affinity Manager** remembers it for you: save a rule
once (for example `audiodg → CPU 3`), and the tool re-applies it in the background whenever needed.

## Features

- **Rules that stick** – rules are checked every 10 seconds and only changed when the affinity differs
- **Pick from running programs** – searchable list with the current affinity of every process
- **Apply once** – set an affinity temporarily without saving a rule
- **Start with Windows** – starts 60 seconds after sign-in via Task Scheduler (no UAC prompt)
- **System tray** – optionally start hidden in the tray; closing the window keeps it running
- **Light & dark mode** – follows the Windows app theme live
- **22 languages** – English, 中文, हिन्दी, Español, العربية, Français, বাংলা, Português, Русский,
  Bahasa Indonesia, اردو, Deutsch, 日本語, मराठी, Tiếng Việt, తెలుగు, Türkçe, தமிழ், 한국어,
  Italiano, Polski, Ελληνικά

## Installation

Download the latest `Prozessor-Affinity-Manager-Setup-x.y.z.exe` from
[Releases](../../releases) and run it.

Requirements: Windows 10 or 11 (the .NET Framework 4.8 it needs is built in).
The tool asks for administrator rights because changing the affinity of system processes such as
`audiodg.exe` requires them.

## Usage

1. Select a program in **Running programs**.
2. Tick the CPUs it should use.
3. Click **Save as rule** – done. The rule is applied now and every time the program starts again.

Settings are stored in `%AppData%\Prozessor Affinity Manager\config.txt`.

## Building from source

- Visual Studio 2022 (or the .NET SDK) – open `Prozessor Affinity Manager.sln` and build
- Installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php) – compile `installer/setup.iss`
  after a Release build

Every push to `main` is built by GitHub Actions. Pushing a tag like `v1.0.0` creates a release with
the installer attached.

## License

[MIT](LICENSE)
