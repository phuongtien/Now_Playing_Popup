# Now Playing Popup 🎵

A beautiful desktop application that displays your currently playing Spotify track with customizable popup overlays and stunning visual effects.

## ✨ Features

- **Real-time Spotify Integration** - Displays currently playing track information
- **Customizable Popups** - Beautiful, non-intrusive overlay windows
- **Album Art Display** - High-quality album artwork with smooth animations
- **Multiple Themes** - Various visual styles and color schemes
- **Position Control** - Place popups anywhere on your screen
- **Auto-hide Options** - Smart visibility controls based on activity
- **Hotkey Support** - Quick show/hide with keyboard shortcuts
- **Low Resource Usage** - Minimal impact on system performance

## 🖼️ Screenshots



## 🚀 Getting Started

### Prerequisites

- Windows 10/11 (primary support)
- .NET 6.0 Runtime or later
- Spotify Premium account (required for API access)
- Visual Studio 2022 (if building from source)

### Installation

#### Option 1: Download Pre-built Releases
1. Go to [Releases](../../releases)
2. Download the latest version for your operating system
3. Run the installer and follow the setup wizard

#### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/yourusername/now-playing-popup.git
cd now-playing-popup

# Open in Visual Studio or build via command line
dotnet restore
dotnet build --configuration Release

# Run the application
dotnet run --project NowPlayingPopup
```

## 🔧 Configuration

### Spotify Setup

1. **Create a Spotify App:**
   - Go to [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
   - Create a new app
   - Note down your `Client ID` and `Client Secret`

2. **Configure Redirect URI:**
   - Add `http://localhost:8888/callback` to your app's redirect URIs

3. **First Launch:**
   - Launch Now Playing Popup
   - Enter your Spotify credentials when prompted
   - Authorize the application

### Customization Options

- **Appearance:** Choose from multiple themes and color schemes
- **Position:** Drag popups to your preferred screen location
- **Size:** Adjust popup dimensions to fit your workflow
- **Timing:** Configure show/hide duration and delays
- **Hotkeys:** Set custom keyboard shortcuts

## 🎛️ Usage

1. **Launch the application** from your desktop or start menu
2. **Sign in to Spotify** when prompted
3. **Start playing music** in Spotify
4. **Enjoy** beautiful popups showing your current track!

### Keyboard Shortcuts

- `Ctrl/Cmd + Shift + P` - Toggle popup visibility
- `Ctrl/Cmd + Shift + S` - Open settings
- `Ctrl/Cmd + Shift + Q` - Quit application

## 🛠️ Development

### Tech Stack

- **Desktop Framework:** .NET 6.0/7.0 with WPF
- **Frontend:** HTML5, CSS3, JavaScript (WebView2)
- **Backend:** C# (.NET)
- **APIs:** Spotify Web API
- **Build Tools:** Visual Studio, MSBuild

### Development Setup

```bash
# Clone and setup
git clone https://github.com/phuongtien/Now_Playing_Popup
cd now-playing-popup

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build

# Run in development mode
dotnet run --project NowPlayingPopup

# Build for release
dotnet publish --configuration Release --self-contained true
```

### Project Structure

```
NowPlayingPopup/
├── bin/                    # Compiled binaries
│   └── Debug/             # Debug build output
├── obj/                    # Build intermediate files
├── wwwroot/               # Web assets
│   ├── index.html         # Main UI file
│   ├── style.css          # Styling
│   └── app.js             # Frontend JavaScript
├── Properties/            # Project properties
├── Assets/                # Application resources
├── Models/                # Data models
├── Services/              # Business logic
├── ViewModels/            # MVVM view models
├── Views/                 # WPF views/windows
├── MainWindow.xaml        # Main application window
├── MainWindow.xaml.cs     # Main window code-behind
├── App.xaml               # Application definition
├── App.xaml.cs            # Application startup logic
└── NowPlayingPopup.csproj # Project file
```

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### How to Contribute

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow C# coding conventions and naming standards
- Use async/await patterns for API calls
- Implement proper MVVM architecture
- Write clear commit messages
- Add XML documentation for public methods
- Update documentation as needed

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🐛 Bug Reports & Feature Requests

- **Bug Reports:** Please use the [Issue Tracker](../../issues) with the "bug" label
- **Feature Requests:** Use the [Issue Tracker](../../issues) with the "enhancement" label
- **Questions:** Check [Discussions](../../discussions) for community support

## 📋 Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes.

## 🙏 Acknowledgments

- [Spotify Web API](https://developer.spotify.com/documentation/web-api/) for music data
- [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) for modern web rendering
- [.NET WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) for the desktop framework
- All contributors and users who help improve this project

## 📞 Support
phuongtien.dev@gmail.com


---

**Made with ❤️ by Phuong Tien**

*If you find this project helpful, please consider giving it a ⭐ star on GitHub!*