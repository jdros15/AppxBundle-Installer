# AppxBundle Installer

A lightweight, native Windows desktop application for managing Appx, AppxBundle, MSIX, and MSIXBundle packages.

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)
![Windows 10+](https://img.shields.io/badge/Windows-10%2F11-blue)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

### 📦 Package Installation
- **Drag and drop** any `.appx`, `.appxbundle`, `.msix`, or `.msixbundle` file
- Validates file type and architecture compatibility before install
- Shows package name, version, publisher, and signature status
- Real-time installation progress

### 📋 Installed Apps Browser
- Lists all installed Appx/MSIX packages
- Filter by publisher (Microsoft / Third-party)
- Search by name, family name, or publisher
- View version, install location, and scope

### 🗑️ Safe Uninstallation
- One-click uninstall with confirmation
- System-protected package detection and warnings
- Progress tracking and result feedback

### 🔍 Diagnostics
- Full technical logging for troubleshooting
- Human-readable error messages
- Export logs to file

### 🎨 Modern UI
- Fluent Design / WinUI-style interface
- Light and dark mode support
- Keyboard and mouse friendly

## Requirements

- **Windows 10 version 1809** or later (including Windows 11)
- Works on Enterprise and IoT LTSC editions
- **.NET 8.0 Runtime** (Windows Desktop)
- For unsigned packages: **Developer Mode** or **Sideloading** enabled

## Building

### Prerequisites
1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Install [Visual Studio 2022](https://visualstudio.microsoft.com/) with:
   - .NET Desktop Development workload
   - Windows 10 SDK (10.0.19041.0 or later)

### Build Commands
```powershell
# Clone and navigate to project
cd "AppxBundle Installer"

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project AppxBundleInstaller
```

### Publish as Single Executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Usage

### Installing a Package
1. Launch the application
2. Drag and drop a package file onto the drop zone, or click **Browse**
3. Review the package information (name, version, publisher, signature)
4. Click **Install**
5. Monitor progress - you'll see a success or error message

### Managing Installed Apps
1. Click **Installed Apps** in the navigation
2. Use the search box to find specific packages
3. Filter by publisher type if needed
4. Select a package to see details
5. Click **Uninstall** to remove (with confirmation)

### Troubleshooting
1. Click **Diagnostics** in the navigation
2. Review the log for errors
3. Check system info (sideloading status, elevation)
4. Export logs for support

## Architecture

```
AppxBundleInstaller/
├── Models/           # Data models (PackageInfo, OperationResult)
├── Services/         # Business logic
│   ├── PackageManagerService      # Install/uninstall operations
│   ├── PackageEnumerationService  # List installed packages
│   ├── PackageValidationService   # Validate package files
│   ├── ErrorDecoderService        # Human-readable errors
│   ├── DiagnosticsService         # Logging
│   └── ElevationService           # UAC handling
├── ViewModels/       # MVVM ViewModels
├── Views/            # XAML user controls
├── Converters/       # XAML value converters
└── Themes/           # Light/Dark theme resources
```

## Security

- ✅ **No arbitrary script execution** - Uses only Windows APIs
- ✅ **Signature verification** - Warns about unsigned packages
- ✅ **UAC consent** - Clear prompts before elevation
- ✅ **No background services** - All operations are user-initiated
- ✅ **No telemetry** - Zero network calls except for package operations
- ✅ **No ads**

## Common Error Codes

| Code | Meaning |
|------|---------|
| 0x80073CF3 | Missing dependencies |
| 0x80073CFF | Blocked by policy |
| 0x800B0100 | Unsigned package (enable Developer Mode) |
| 0x80070005 | Administrator privileges required |

## License

MIT License - feel free to use and modify.

## Contributing

Contributions welcome! Please ensure any changes:
- Follow existing code style
- Include appropriate error handling
- Work on Windows 10 1809+
