# Deploying TheCallAttendanceApi to the VPS

Fully separate from WordPuzzleApi -- its own database, its own IIS
Application/app pool, no shared code. Run everything below **on the VPS**
(RDP + a real interactive PowerShell window, not the actions-runner console
which can't show password prompts).

## 1. Database

```powershell
sqlcmd -S localhost\SQLEXPRESS -U <admin> -Q "CREATE DATABASE [TheCallAttendance]"
sqlcmd -S localhost\SQLEXPRESS -U <admin> -d TheCallAttendance -i schema.sql
```

## 2. Get the source and publish

```powershell
Invoke-WebRequest -Uri "https://github.com/faseencp/WordPuzzle/archive/refs/heads/main.zip" -OutFile "$env:TEMP\wordpuzzle.zip"
Expand-Archive -Path "$env:TEMP\wordpuzzle.zip" -DestinationPath "$env:TEMP\wordpuzzle-extract" -Force

cd "$env:TEMP\wordpuzzle-extract\WordPuzzle-main\TheCallAttendanceApi"
dotnet publish -c Release -o C:\inetpub\apps\thecall-api\publish
```

If you're redeploying after a code change (not the first time), delete
`obj`/`bin` first and stop the pool before publishing -- a stale
incremental build silently reusing old compiled output cost real time
during WordPuzzleApi's deployment:

```powershell
Remove-Item -Recurse -Force "$env:TEMP\wordpuzzle-extract\WordPuzzle-main\TheCallAttendanceApi\obj" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$env:TEMP\wordpuzzle-extract\WordPuzzle-main\TheCallAttendanceApi\bin" -ErrorAction SilentlyContinue
Stop-WebAppPool -Name "thecall-api-pool"
Start-Sleep -Seconds 5
# ... then dotnet publish as above, then:
Start-WebAppPool -Name "thecall-api-pool"
```

## 3. Create the IIS Application + app pool

Own dedicated pool, "No Managed Code" (required for ASP.NET Core), under the
existing site's binding -- no new binding/DNS/SSL cert needed:

```powershell
Import-Module WebAdministration

New-WebAppPool -Name "thecall-api-pool"
Set-ItemProperty "IIS:\AppPools\thecall-api-pool" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\thecall-api-pool" -Name processModel.identityType -Value ApplicationPoolIdentity

New-WebApplication -Site "Default Web Site" -Name "thecall/api" `
  -PhysicalPath "C:\inetpub\apps\thecall-api\publish" `
  -ApplicationPool "thecall-api-pool"

icacls "C:\inetpub\apps\thecall-api\publish" /grant "IIS AppPool\thecall-api-pool:(OI)(CI)M"
```

(Granting Modify, not just Read+Execute, so stdout logging can actually
write -- another thing that cost time to discover during WordPuzzleApi's
deployment.)

## 4. Database access (Windows Integrated Auth, no password to store)

```powershell
sqlcmd -S localhost\SQLEXPRESS -U <admin> -Q "CREATE LOGIN [IIS AppPool\thecall-api-pool] FROM WINDOWS"
sqlcmd -S localhost\SQLEXPRESS -U <admin> -d TheCallAttendance -Q "CREATE USER [IIS AppPool\thecall-api-pool] FOR LOGIN [IIS AppPool\thecall-api-pool]; ALTER ROLE db_datareader ADD MEMBER [IIS AppPool\thecall-api-pool]; ALTER ROLE db_datawriter ADD MEMBER [IIS AppPool\thecall-api-pool];"
```

## 5. Recycle and smoke-test

```powershell
Restart-WebAppPool -Name "thecall-api-pool"
```

From off the VPS:
```powershell
Invoke-RestMethod https://portal.rsconline.org/thecall/api/health
```
Should return `{"status":"ok"}`.

## 6. Deploy the static page (two locations: form + dashboard)

```powershell
New-Item -ItemType Directory -Force -Path "$env:SystemDrive\inetpub\wwwroot\thecall"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/faseencp/WordPuzzle/main/the-call-attendance.html" -OutFile "$env:SystemDrive\inetpub\wwwroot\thecall\index.html"

New-Item -ItemType Directory -Force -Path "$env:SystemDrive\inetpub\wwwroot\thecall\dashboard"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/faseencp/WordPuzzle/main/the-call-attendance.html" -OutFile "$env:SystemDrive\inetpub\wwwroot\thecall\dashboard\index.html"
```

- Participant link: `https://portal.rsconline.org/thecall/`
- Dashboard link: `https://portal.rsconline.org/thecall/dashboard/`

## 7. Backups

Not covered by any existing job -- add a Scheduled Task mirroring the
existing rclone backup pattern:

```powershell
sqlcmd -S localhost\SQLEXPRESS -U <admin> -Q "BACKUP DATABASE [TheCallAttendance] TO DISK = N'C:\Backups\TheCallAttendance\TheCallAttendance_$(Get-Date -Format yyyyMMdd_HHmmss).bak' WITH INIT"
```
