# Combined Arms Launcher

A launcher and updater application for OpenRA Combined Arms built with C# .NET and Avalonia UI.

## Features

- **Automatic Updates**: Checks for new releases and automatically downloads them when enabled
- **Version Management**: Download and manage multiple versions of Combined Arms
- **Version Selection**: Choose which version to launch from a dropdown
- **Portable Installation**: Downloads and extracts portable releases to a `Releases` subfolder

## How it Works

1. The launcher checks the GitHub API at `https://api.github.com/repos/Inq8/CAmod/releases` for new releases
2. It filters for Windows portable releases (files ending with `-x64-winportable.zip`)
3. Each release is extracted to `Releases/{version}` folder
4. The launcher can start any installed version by running `CombinedArms.exe` in the respective folder

## Building

1. Ensure you have .NET 8.0 SDK installed
2. Clone this repository
3. Run `dotnet restore` to restore packages
4. Run `dotnet build` to build the application
5. Run `dotnet run` to start the launcher

## Usage

1. **Version Selection**: Use the dropdown to select which version to launch
2. **Play**: Click the Play button to launch the selected version
3. **Manual Update Check**: Click "Check for Updates" to manually check for new releases
4. **Update Now**: Click "Update Now" to download the newest release
5. **Include Test Releases**: Toggles whether to include DevTest and PreRelease versions

## Settings

Settings are automatically saved to `settings.json` in the application directory:
- Auto-update preference
- Selected version
- Last update check time

## Dependencies

- .NET 8.0
- Avalonia UI 11.0.10
- Newtonsoft.Json 13.0.3
