# Google Drive CLI

A .NET command-line tool to sync, search, and upload files to Google Drive.

## Setup

1. Install .NET 8 SDK.
2. Clone the repo.
3. Place `client_secret.json` in the project root (same folder as `.csproj`).
4. Run `dotnet build`.

## Usage

- `dotnet run -- sync` – download all files to ./Downloads
- `dotnet run -- search "term"` – search files by name
- `dotnet run -- upload /path/to/file "Drive/Folder"` – upload a file

## Architecture

- OAuth token cached in `~/.google-drive-cli-token`
- 5 parallel downloads for efficient sync
- Thread-safe stats via `Interlocked.Increment`
- Separate services for auth, Drive API, and CLI
