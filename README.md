# 🚀 AutoSAM — Steam Achievement Automator

<p align="center">
  <img src="assets/auto.gif" alt="AutoSAM demo GIF" />
</p>

<p align="center">
  <a href="https://github.com/olucasmf/AutoSAM/releases">
    <img src="https://img.shields.io/github/v/release/olucasmf/AutoSAM?label=release&logo=github&style=for-the-badge" alt="Latest Release" />
  </a>
  <a href="https://github.com/olucasmf/AutoSAM/issues">
    <img src="https://img.shields.io/github/issues/olucasmf/AutoSAM?style=for-the-badge&logo=github" alt="Issues" />
  </a>
  <a href="https://github.com/olucasmf/AutoSAM/blob/main/LICENSE.txt">
    <img src="https://img.shields.io/github/license/olucasmf/AutoSAM?style=for-the-badge" alt="License" />
  </a>
  <img src="https://img.shields.io/badge/Built%20With-C%23-178600?style=for-the-badge&logo=csharp&logoColor=white" alt="Built with C#" />
  <img src="https://img.shields.io/badge/Platform-Windows-blue?style=for-the-badge&logo=windows&logoColor=white" alt="Platform" />
</p>

**AutoSAM is a modern, secure, and customizable automation tool for managing Steam achievements.**  
Designed as a robust upgrade to the original Steam Achievement Manager (SAM), AutoSAM brings enhanced features, improved performance, and complete transparency.

---

## ✨ Features

- ✅ **Secure & Offline** — No internet access, no telemetry, and no data collection.
- 🧠 **Smart Timer** — Set custom intervals between unlocks to mimic natural behavior.
- 📊 **Dynamic Progress Bar** — Track your unlocking progress in real time.
- 🖱️ **Drag & Drop Interface** — Easily reorder achievement unlock sequences.
- 🧩 **Modular Architecture** — Clean, scalable codebase for easier maintenance and contributions.
- 🛡️ **Open Source** — Fully auditable code, free from obfuscation or hidden behavior.
- 🌐 **Localization-Ready** — Built with internationalization in mind.
- 🐞 **Improved Stability** — Bug fixes and interface enhancements over the original SAM.

---

## 🚀 Quick Start

1. Download the latest release from the [Releases page](https://github.com/olucasmf/AutoSAM/releases) or the `upload` folder.
2. Extract the contents of the `.zip` file.
3. **Launch the official Steam desktop client** (not the browser version).
4. Make sure your game library is fully loaded.
5. Run `AutoSAM.Game.exe` to start the application.
6. *(Optional)* Use `AutoSAM.Picker.exe` to select a specific game.

> ⚠️ **Important:** AutoSAM requires the official **Steam desktop client** to be running with your **library loaded**. The web client is not supported.

---

## 🖥️ Installation Notes

AutoSAM is a portable application. No installation required. Simply extract and run.

> ❗ If your antivirus flags the app, this is a **false positive** due to the use of unmanaged code interacting with Steam processes. See the [Antivirus Notice](#-antivirus-notice) below.

---

## 🔐 Security & Trust

AutoSAM was built with transparency and user safety as top priorities:

- 🔓 100% Open Source
- 📡 Zero Internet Access
- 🧾 No Logging or Tracking
- ⚙️ No Runtime Modifications to Steam

You are encouraged to review the source code and compile your own binaries for maximum trust.

---

## 🛠️ Technologies Used

- C# (.NET Framework)
- Windows Forms (WinForms)
- Steamworks API
- Custom memory reading/writing modules

---

## 🧪 Antivirus Notice

When running `AutoSAM.Game.exe` or extracting the `.zip`, your antivirus or Windows Defender may issue a warning.

This is expected and is caused by:

- The application being **unsigned**.
- Direct interaction with external processes (Steam).
- Inclusion of custom memory utilities.

> ✅ **AutoSAM does not contain malware, does not access the internet, and does not modify your system.**  
> You can compile the application from source if you prefer full control.

---

## 📜 License

AutoSAM is released under the same license as the original [Steam Achievement Manager](https://github.com/gibbed/SteamAchievementManager).  
See `LICENSE.txt` for full details.

---

## 🙏 Credits

- 🧩 Original Project: [Steam Achievement Manager by gibbed](https://github.com/gibbed/SteamAchievementManager)
- 👨‍💻 This version: Modified and maintained by [olucasmf](https://github.com/olucasmf)
- 👥 Thanks to the community for feedback, testing, and support

---

## 🤝 Contributing

We welcome pull requests, feature suggestions, and issue reports!

If you’d like to contribute:

1. Fork the repository
2. Create a feature branch
3. Submit a Pull Request with a clear description

> For major changes, please open an issue first to discuss what you’d like to change.

---

## 📬 Contact

For issues, suggestions, or collaboration, open a GitHub [Issue](https://github.com/olucasmf/AutoSAM/issues) or reach out via the repository.

---
