# Deploying WordPuzzleApi to the VPS

Companion to the plan at `tender-fluttering-bentley.md`. Run everything below
**on the VPS** (RDP + PowerShell), against the existing `Default Web Site`
(physical path `%SystemDrive%\inetpub\wwwroot`, same site that already serves
`portal.rsconline.org` / `portal.rscglobal.org` and the static `/wordpuzzle/`
files).

## 1. Database

```powershell
sqlcmd -S localhost\SQLEXPRESS -Q "CREATE DATABASE [WordPuzzle]"
sqlcmd -S localhost\SQLEXPRESS -d WordPuzzle -i C:\path\to\schema.sql
```

`schema.sql` also sets the recovery model to SIMPLE — no SQL Agent needed.

## 2. Get the source onto the VPS and publish

Pull the `WordPuzzleApi/` folder from the repo (same method used for the
static file — download or `git pull` once this repo is cloned there), then:

```powershell
cd C:\path\to\WordPuzzleApi
dotnet publish -c Release -o C:\inetpub\apps\wordpuzzle-api\publish
```

Deliberately outside the static `wordpuzzle` folder for clean NTFS/backup scoping.

## 3. One-time IIS prerequisite

Confirm the ASP.NET Core Hosting Bundle is installed (separate from the SDK):

```powershell
Get-ChildItem "$env:ProgramFiles\dotnet\shared\Microsoft.AspNetCore.App"
```

If it's missing, install the **ASP.NET Core 8.0.x Hosting Bundle** (not the SDK)
from Microsoft, then:

```powershell
iisreset
```

Verify `AspNetCoreModuleV2` shows up in IIS Manager → Modules.

## 4. Create the IIS Application + app pool

This **must** be its own app pool set to "No Managed Code" — it cannot share
a pool with the existing classic-ASP.NET site.

```powershell
Import-Module WebAdministration

New-WebAppPool -Name "wordpuzzle-api-pool"
Set-ItemProperty "IIS:\AppPools\wordpuzzle-api-pool" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\wordpuzzle-api-pool" -Name processModel.identityType -Value ApplicationPoolIdentity

New-WebApplication -Site "Default Web Site" -Name "wordpuzzle/api" `
  -PhysicalPath "C:\inetpub\apps\wordpuzzle-api\publish" `
  -ApplicationPool "wordpuzzle-api-pool"

icacls "C:\inetpub\apps\wordpuzzle-api\publish" /grant "IIS AppPool\wordpuzzle-api-pool:(OI)(CI)RX"
```

## 5. Database access (no secret needed)

```powershell
sqlcmd -S localhost\SQLEXPRESS -Q "CREATE LOGIN [IIS AppPool\wordpuzzle-api-pool] FROM WINDOWS"
sqlcmd -S localhost\SQLEXPRESS -d WordPuzzle -Q "CREATE USER [IIS AppPool\wordpuzzle-api-pool] FOR LOGIN [IIS AppPool\wordpuzzle-api-pool]; ALTER ROLE db_datareader ADD MEMBER [IIS AppPool\wordpuzzle-api-pool]; ALTER ROLE db_datawriter ADD MEMBER [IIS AppPool\wordpuzzle-api-pool];"
```

This is why `Db.cs` defaults to `Trusted_Connection=True` — no password anywhere.

## 6. Set the host key (and confirm the connection string) via IIS config, not a file

```powershell
cd "$env:windir\system32\inetsrv"
.\appcmd.exe set config "Default Web Site/wordpuzzle/api" -section:system.webServer/aspNetCore /+"environmentVariables.[name='WORDPUZZLE_HOST_KEY',value='<choose-a-long-random-value>']" /commit:apphost
.\appcmd.exe set config "Default Web Site/wordpuzzle/api" -section:system.webServer/aspNetCore /+"environmentVariables.[name='WORDPUZZLE_CONNECTION_STRING',value='Server=localhost\SQLEXPRESS;Database=WordPuzzle;Trusted_Connection=True;TrustServerCertificate=True;']" /commit:apphost
```

Whatever value you choose for `WORDPUZZLE_HOST_KEY` is what you type into the
"Host key" field on the word search host page — keep it private, share it
only with people who should be able to create/start rounds.

## 7. Recycle and smoke-test

```powershell
Restart-WebAppPool -Name "wordpuzzle-api-pool"
```

From a machine **off** the VPS (confirms it's actually reachable through
Cloudflare, and not caught by the existing site's login wall):

```powershell
Invoke-RestMethod https://portal.rsconline.org/wordpuzzle/api/health
```

Should return `{"status":"ok"}`. If it 404s or redirects to the login page
instead, stop here and report back — that means the subfolder assumption
needs revisiting (see plan §5 fallback).

Then exercise the real endpoints, e.g.:

```powershell
$body = @{ seed='test-seed-1'; tier='Unit'; category='junior'; gridSize=10;
  words=@('BOOK','READ'); participantCount=3; hostKey='<your host key>' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri https://portal.rsconline.org/wordpuzzle/api/rounds -Body $body -ContentType 'application/json'
```

## 8. Backups (not covered by the existing job — add explicitly)

New Scheduled Task, same style as the existing rclone backup:

```powershell
sqlcmd -S localhost\SQLEXPRESS -Q "BACKUP DATABASE [WordPuzzle] TO DISK = N'C:\Backups\WordPuzzle\WordPuzzle_$(Get-Date -Format yyyyMMdd_HHmmss).bak' WITH INIT"
```

Wrap that in a `.ps1`, register it in Task Scheduler alongside the existing
backup job, and extend (or duplicate) the rclone sync step to also push
`C:\Backups\WordPuzzle\` to the same Google Drive destination. Add basic
retention cleanup for old `.bak` files. Run it once manually and confirm the
file lands both locally and in Drive before considering this done.
