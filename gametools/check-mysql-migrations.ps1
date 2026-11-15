<#
.SYNOPSIS
    Checks that every Rasa.NET MySQL migration has been applied to the Auth, Char and World databases.

.DESCRIPTION
    Reads the migration ids from the checked-out repository (src\Rasa.DBL\Migrations\MySqlAuth, MySqlChar and
    MySqlWorld: each migration's [Migration("...")] attribute) and compares them with the ids in each database's
    __EFMigrationsHistory table. Nothing is changed in the databases; it only reads.

    For each database it reports:
      - pending:  in the repository, not in the database (not applied yet);
      - unknown:  in the database, not in the repository (applied from a newer checkout, or an id a later
                  migration renamed and the history kept).

    Connection settings are read as the servers read them: databasesettings.json, overlaid with
    databasesettings.env.json if present, overlaid with environment variables (Databases__World__Password and the
    like, as compose.mysql.yml sets them).

    check-mysql-migrations.sh is the same check for Linux.

    Exit code: 0 all applied, 1 something pending, 2 error (no repository, settings, mysql client or connection).

.PARAMETER RepoRoot
    The Rasa.NET checkout. Default: the current folder, else the one this script's gametools folder is in.

.PARAMETER SettingsDir
    The folder holding databasesettings.json. Default: <RepoRoot>\out\game if it has one, else <RepoRoot>\src\Rasa.DBL.

.PARAMETER MySql
    Path to mysql.exe. Default: the one on PATH, else the newest under Program Files\MySQL or Program Files\MariaDB*.

.PARAMETER Databases
    Which databases to check: any of Auth, Char, World. Default: all three.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File gametools\check-mysql-migrations.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File gametools\check-mysql-migrations.ps1 -SettingsDir D:\RasaServer -Databases World
#>
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$SettingsDir,
    [string]$MySql,
    [ValidateSet('Auth', 'Char', 'World')]
    [string[]]$Databases = @('Auth', 'Char', 'World')
)

$ErrorActionPreference = 'Stop'

function Fail([string]$message) {
    Write-Host "ERROR: $message" -ForegroundColor Red
    exit 2
}

function Test-Repo([string]$path) {
    return $path -and (Test-Path -LiteralPath (Join-Path $path 'src\Rasa.DBL\Migrations'))
}

# The repository.
if ($RepoRoot) {
    if (-not (Test-Repo $RepoRoot)) { Fail "No src\Rasa.DBL\Migrations under '$RepoRoot'." }
} elseif (Test-Repo (Get-Location).Path) {
    $RepoRoot = (Get-Location).Path
} elseif (Test-Repo (Split-Path -Parent $PSScriptRoot)) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
} else {
    Fail "Run this from the Rasa.NET folder, or pass -RepoRoot <path to Rasa.NET>."
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

# The settings.
if (-not $SettingsDir) {
    $published = Join-Path $RepoRoot 'out\game'
    if (Test-Path -LiteralPath (Join-Path $published 'databasesettings.json')) {
        $SettingsDir = $published
    } else {
        $SettingsDir = Join-Path $RepoRoot 'src\Rasa.DBL'
    }
}
$settingsFile = Join-Path $SettingsDir 'databasesettings.json'
$fromEnvironment = @(Get-ChildItem env: | Where-Object { $_.Name -like 'Databases__*' }).Count -gt 0
$settings = $null
if (Test-Path -LiteralPath $settingsFile) {
    $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
} elseif (-not $fromEnvironment) {
    Fail "No databasesettings.json in '$SettingsDir', and no Databases__* environment variables."
}
$envFile = Join-Path $SettingsDir 'databasesettings.env.json'
$overlay = $null
if (Test-Path -LiteralPath $envFile) { $overlay = Get-Content -LiteralPath $envFile -Raw | ConvertFrom-Json }

function Get-Setting([string]$database, [string]$key) {
    $value = [System.Environment]::GetEnvironmentVariable("Databases__$($database)__$key")
    if ($null -ne $value) { return $value }
    if ($overlay -and $overlay.Databases -and $overlay.Databases.$database -and $null -ne $overlay.Databases.$database.$key) {
        $value = $overlay.Databases.$database.$key
    } elseif ($settings -and $settings.Databases -and $settings.Databases.$database) {
        $value = $settings.Databases.$database.$key
    }
    return $value
}

# The mysql client.
if (-not $MySql) {
    $onPath = Get-Command mysql.exe -ErrorAction SilentlyContinue
    if (-not $onPath) { $onPath = Get-Command mysql -ErrorAction SilentlyContinue }
    if ($onPath) {
        $MySql = $onPath.Source
    } else {
        $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ }
        $found = foreach ($root in $roots) {
            Get-ChildItem -Path (Join-Path $root 'MySQL\*\bin\mysql.exe'), (Join-Path $root 'MariaDB*\bin\mysql.exe') -ErrorAction SilentlyContinue
        }
        $newest = $found | Sort-Object { $_.VersionInfo.FileVersion -as [version] }, FullName | Select-Object -Last 1
        if ($newest) { $MySql = $newest.FullName }
    }
}
if (-not $MySql -or -not (Test-Path -LiteralPath $MySql)) {
    Fail "mysql.exe not found. Pass -MySql 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe' (or wherever yours is)."
}

# Runs one query; returns the output lines, or throws with mysql's own error.
function Invoke-MySql([string]$database, [string]$query) {
    $optionFile = [System.IO.Path]::GetTempFileName()
    try {
        $password = [string](Get-Setting $database 'Password')
        $quoted = '"' + $password.Replace('\', '\\').Replace('"', '\"') + '"'
        $lines = @(
            '[client]',
            "host=$(Get-Setting $database 'Host')",
            "port=$(Get-Setting $database 'Port')",
            "user=$(Get-Setting $database 'User')",
            "password=$quoted"
        )
        [System.IO.File]::WriteAllText($optionFile, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))

        $start = New-Object System.Diagnostics.ProcessStartInfo
        $start.FileName = $MySql
        $start.Arguments = "--defaults-extra-file=`"$optionFile`" --batch --skip-column-names " +
            "--database=`"$(Get-Setting $database 'Database')`" --execute=`"$query`""
        $start.UseShellExecute = $false
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.CreateNoWindow = $true
        $process = [System.Diagnostics.Process]::Start($start)
        $stderr = $process.StandardError.ReadToEndAsync()
        $stdout = $process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            $message = @($stderr.Result -split "`r?`n" | Where-Object { $_ -match '^ERROR' }) | Select-Object -First 1
            if (-not $message) { $message = $stderr.Result.Trim() }
            if ($message -match 'ERROR 1146') {
                $message = "it has no __EFMigrationsHistory table: no migration was ever applied there, or this is not the right database. ($message)"
            }
            throw $message
        }
        return @($stdout -split "`r?`n" | Where-Object { $_ -ne '' })
    } finally {
        Remove-Item -LiteralPath $optionFile -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Repository: $RepoRoot"
$head = $null
try { $head = (& git -C $RepoRoot log --oneline -1 2>$null) } catch { }
if ($head) { Write-Host "Checked out: $head" }
$sources = @()
if ($settings) { $sources += $settingsFile }
if ($overlay) { $sources += 'databasesettings.env.json' }
if ($fromEnvironment) { $sources += 'Databases__* environment variables' }
Write-Host "Settings:   $($sources -join ' + ')"
Write-Host "Client:     $MySql"
Write-Host ''

$pendingTotal = 0
$errors = 0
foreach ($database in $Databases) {
    $folder = Join-Path $RepoRoot "src\Rasa.DBL\Migrations\MySql$database"
    if (-not (Test-Path -LiteralPath $folder)) { Fail "No migrations folder '$folder'." }
    $ids = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    Get-ChildItem -LiteralPath $folder -Filter '*.cs' |
        Select-String -Pattern '\[Migration\("([^"]+)"\)\]' |
        ForEach-Object { [void]$ids.Add($_.Matches[0].Groups[1].Value) }
    [string[]]$expected = @($ids)
    [System.Array]::Sort($expected, [System.StringComparer]::Ordinal)

    $name = Get-Setting $database 'Database'
    $label = "$database ($name on $(Get-Setting $database 'Host'):$(Get-Setting $database 'Port'))"

    try {
        $applied = Invoke-MySql $database 'SELECT MigrationId FROM __EFMigrationsHistory'
    } catch {
        Write-Host "$label" -ForegroundColor Red
        Write-Host "  could not read __EFMigrationsHistory: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ''
        $errors++
        continue
    }

    $appliedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([string[]]$applied), ([System.StringComparer]::Ordinal)
    $expectedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([string[]]$expected), ([System.StringComparer]::Ordinal)
    $pending = @($expected | Where-Object { -not $appliedSet.Contains($_) })
    $unknown = @($applied | Where-Object { -not $expectedSet.Contains($_) } | Sort-Object)
    $latestApplied = @($expected | Where-Object { $appliedSet.Contains($_) }) | Select-Object -Last 1

    $colour = 'Green'
    if ($pending.Count -gt 0) { $colour = 'Yellow' }
    Write-Host $label -ForegroundColor $colour
    Write-Host ("  {0} of {1} applied, {2} pending" -f ($expected.Count - $pending.Count), $expected.Count, $pending.Count) -ForegroundColor $colour

    foreach ($id in $pending) {
        $note = ''
        if ($latestApplied -and [string]::CompareOrdinal($id, $latestApplied) -lt 0) {
            $note = '   (older than the newest applied one: a migration landed with an earlier timestamp)'
        }
        Write-Host "    pending  $id$note" -ForegroundColor Yellow
    }
    foreach ($id in $unknown) {
        Write-Host "    unknown  $id   (in the database, not in this checkout)" -ForegroundColor DarkGray
    }
    Write-Host ''
    $pendingTotal += $pending.Count
}

if ($errors -gt 0) {
    Write-Host "Could not check $errors database(s)." -ForegroundColor Red
    exit 2
}
if ($pendingTotal -gt 0) {
    Write-Host "$pendingTotal migration(s) pending." -ForegroundColor Yellow
    exit 1
}
Write-Host 'All MySQL migrations are applied.' -ForegroundColor Green
exit 0
