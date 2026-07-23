# Db-Restore-Tool

A fast, cross-platform command-line tool to restore SQL Server and PostgreSQL databases from backup files (`.bak`, `.dump`, `.sql`). Supports password-protected `.7z` and `.zip` archives with automatic extraction.

## Features

- **SQL Server & PostgreSQL** restore from a single tool
- **Archive support** — extracts `.7z`, `.zip`, `.rar` files (password-protected supported)
- **Parallel Postgres restore** — uses `pg_restore -j 4` for faster restores
- **Real-time progress** — live 5% increment progress during restore
- **Auto DB name detection** — reads database name from backup header
- **Custom DB rename** — optionally rename the database during restore
- **Post-restore queries** — automatically executes custom SQL after restore
- **Buffer tuning** — configurable `BUFFERCOUNT` and `MAXTRANSFERSIZE` for optimal throughput
- **Clipboard copy** — DB name copied to clipboard after restore
- **Config layering** — safe defaults in git, local overrides via `appsettings-development.json`

## Prerequisites

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or SDK to build from source)
- **SQL Server** — `sqlcmd` or `RESTORE DATABASE` permissions on the target instance
- **PostgreSQL** (optional) — `pg_restore` and `psql` must be on PATH
- **7-Zip** — bundled in `zip/7z.exe` for archive extraction

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/Mr-Neupane/Db-Restore-Tool.git
cd Db-Restore-Tool
```

### 2. Configure your local settings

Copy the sample config and fill in your values:

```bash
cp appsettings.json appsettings-development.json
```

Edit `appsettings-development.json` with your server details:

```json
{
  "MsSqlConnection": {
    "ServerName": "YOUR_SERVER",
    "Username": "sa",
    "Password": "YOUR_PASSWORD",
    "OutputDirectory": "D:\\Temp Dbs\\AutoRestores",
    "ZipPassword": ["your_zip_password"],
    "DataLocation": "D:\\Backups",
    "BufferCount": 1024,
    "MaxTransferSize": 4194304
  }
}
```

> **Note:** `appsettings-development.json` is gitignored and stays local. `appsettings.json` contains safe defaults and is safe to commit.

### 3. Run the tool

```bash
# Interactive mode
dotnet run

# Or pass the backup file as argument
dotnet run -- "path/to/backup.7z"
```

### 4. Build a standalone executable

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output will be in `bin\Release\net8.0\win-x64\`. Copy the entire folder to your target location.

## Configuration

All settings are in `appsettings.json` (defaults) and `appsettings-development.json` (your local overrides).

### RestoreSetting

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SelectionForServer` | bool | `false` | Prompt to choose SQL Server or PostgreSQL at runtime |
| `IsPostgres` | bool | `false` | Set `true` to restore to PostgreSQL instead of SQL Server |
| `RawQueryFilePath` | string | `""` | Path to a `.sql` file to execute after restore |
| `DbRenameOption` | bool | `false` | Prompt for a custom database name during restore |

### MsSqlConnection

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ServerName` | string | required | SQL Server instance name or IP |
| `Username` | string | required | SQL Server login username |
| `Password` | string | required | SQL Server login password |
| `OutputDirectory` | string | required | Temp folder for extracted backup files |
| `ZipPassword` | string[] | `[]` | Passwords to try when extracting archives |
| `DataLocation` | string | required | Path where `.mdf` / `.ldf` files are restored |
| `AppPath` | string | required | Target install path for environment setup |
| `BufferCount` | int | `1024` | SQL Server BUFFERCOUNT for restore |
| `MaxTransferSize` | int | `4194304` | SQL Server MAXTRANSFERSIZE in bytes |

### PostgresConnection

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Host` | string | `localhost` | PostgreSQL host |
| `Port` | string | `5432` | PostgreSQL port |
| `Database` | string | `postgres` | Default database for connections |
| `Username` | string | `postgres` | PostgreSQL username |
| `Password` | string | required | PostgreSQL password |
| `ZipPassword` | string | `""` | Password for password-protected archives |

## Performance Tuning

See [BUILDCOUNT_REFERENCE.md](BUILDCOUNT_REFERENCE.md) for detailed `BUFFERCOUNT` and `MAXTRANSFERSIZE` tuning guidelines.

### Quick Reference

| Scenario | BufferCount | MaxTransferSize |
|----------|-------------|-----------------|
| Small DB (< 1 GB) | 256 | 1048576 |
| Medium DB (1-10 GB) | 1024 | 4194304 |
| Large DB (10-50 GB) | 1024 | 4194304 |
| Very Large DB (50 GB+) | 4096 | 4194304 |

## Project Structure

```
Db-Restore-Tool/
├── Program.cs                          # Entry point
├── Config.cs                           # Configuration models
├── EnvSetup.cs                         # Environment setup (publish + PATH)
├── Model/
│   ├── MsSqlSettings.cs                # SQL Server config model
│   ├── PsqlSettings.cs                 # PostgreSQL config model
│   └── RestoreSetting.cs               # Restore options model
├── Services/
│   ├── RestoreService.cs               # Main restore orchestration
│   └── Interfaces/
│       ├── IRestoreService.cs
│       ├── IRawQueryService.cs
│       └── RawQueryService.cs          # Post-restore query execution
├── Helpers/
│   ├── RestoreHelper.cs                # SQL Server / PostgreSQL restore logic
│   ├── FileExtractionHelper.cs         # Archive extraction (7z.exe)
│   ├── ClipboardCopyHelper.cs          # Copy DB name to clipboard
│   └── Interfaces/
├── Providers/
│   ├── Implementations/
│   │   ├── DbConnectionProvider.cs     # SQL Server / PostgreSQL connection factory
│   └── Interfaces/
├── Validator/
│   ├── ArchiveValidator.cs             # File type detection
│   └── Interface/
├── zip/
│   ├── 7z.exe                          # Bundled 7-Zip for extraction
│   └── 7z.dll
├── appsettings.json                    # Default config (committed)
├── appsettings-development.json        # Local overrides (gitignored)
├── BUILDCOUNT_REFERENCE.md             # Performance tuning guide
└── DbRestoreTool.csproj
```

## Usage Examples

### Restore from a `.7z` backup

```bash
dotnet run -- "D:\Backups\MyDatabase__2083_04_07__12_30_00_PM__Auto_Backup.bak.7z"
```

### Restore with custom database name

Set `"DbRenameOption": true` in config, then the tool will prompt:

```
Add New DbName
MyDatabase_Restored
```

### Restore to PostgreSQL

Set `"IsPostgres": true` in config, or use runtime selection with `"SelectionForServer": true`.

## How It Works

1. **Extract** — The backup archive (`.7z`, `.zip`, `.rar`) is extracted using the bundled 7-Zip. Password-protected archives are tried against all configured passwords.
2. **Detect** — The database name is read from the backup header (`RESTORE HEADERONLY` for SQL Server, `pg_restore -l` for PostgreSQL).
3. **Drop** — If the database already exists, existing connections are killed and the database is dropped.
4. **Restore** — The backup is restored with optimized buffer settings. Progress is reported in real-time.
5. **Post-query** — If a `RawQueryFilePath` is configured and exists, the SQL file is executed against the restored database.
6. **Cleanup** — Extracted temporary files are deleted.

## License

MIT
