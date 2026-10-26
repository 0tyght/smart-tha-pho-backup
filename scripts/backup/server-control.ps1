param([ValidateSet('Start','Stop')][string]$Action='Start')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Set-Location -LiteralPath $root
$runtime=Join-Path $root '.runtime\backup'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$statusPath=Join-Path $runtime 'status.json'
$statePath=Join-Path $runtime 'processes.json'
$utf8=New-Object Text.UTF8Encoding($false)
$state=@{api=$null;tunnel=$null}
$status=@{phase='starting';message='กำลังเริ่มระบบ';database='checking';api='waiting';online='waiting';url='';portalUrl='https://0tyght.github.io/smart-tha-pho-backup/';updatedAt=''}
function Write-Status {
  $status.updatedAt=[DateTime]::UtcNow.ToString('o')
  [IO.File]::WriteAllText($statusPath,($status|ConvertTo-Json -Depth 5),$utf8)
}
function Save-State { [IO.File]::WriteAllText($statePath,($state|ConvertTo-Json -Depth 5),$utf8) }
function Process-Record($process) { @{id=$process.Id;startTime=$process.StartTime.ToUniversalTime().ToString('o');path=$process.Path} }
function Stop-Owned($record) {
  if(!$record){return}
  $p=Get-Process -Id $record.id -ErrorAction SilentlyContinue
  if($p -and $p.Path -eq $record.path -and $p.StartTime.ToUniversalTime().ToString('o') -eq $record.startTime){Stop-Process -Id $p.Id -Force}
}
function Get-Health($url) { try { Invoke-RestMethod -Uri $url -TimeoutSec 4 } catch { $null } }
$mutex=New-Object Threading.Mutex($false,'Local\SmartThaPhoBackupControl')
if(!$mutex.WaitOne(0)){throw 'Another backup control operation is running'}
try {
  if(Test-Path -LiteralPath $statePath){$saved=Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json;$state.api=$saved.api;$state.tunnel=$saved.tunnel}
  if($Action -eq 'Stop'){
    Stop-Owned $state.tunnel; Stop-Owned $state.api
    $state.api=$null;$state.tunnel=$null;Save-State
    $status.phase='stopped';$status.message='หยุดเซิร์ฟเวอร์สำรองแล้ว';$status.database='shared';$status.api='stopped';$status.online='stopped';Write-Status
    exit 0
  }
  Stop-Owned $state.tunnel;Stop-Owned $state.api
  $state.api=$null;$state.tunnel=$null;Save-State;Write-Status
  $config=Get-Content -LiteralPath (Join-Path $runtime 'machine.json') -Raw|ConvertFrom-Json
  foreach($p in @($config.node,$config.git,$config.cloudflared,$config.mysqld)){if(!(Test-Path -LiteralPath $p)){throw "Required program not found: $p"}}
  if(!(Test-Path -LiteralPath (Join-Path $root '.env'))){throw 'Missing private .env file'}
  if(!(Test-Path -LiteralPath (Join-Path $root '.runtime\site\index.html'))){throw 'Website build is missing'}
  $status.message='ตรวจสอบฐานข้อมูล';Write-Status
  $client=New-Object Net.Sockets.TcpClient
  try{$connected=$client.ConnectAsync('127.0.0.1',3306).Wait(1000)}catch{$connected=$false}finally{$client.Dispose()}
  if(!$connected){Start-Process -FilePath $config.mysqld -ArgumentList @('--defaults-file=C:\xampp\mysql\bin\my.ini','--standalone','--bind-address=127.0.0.1') -WindowStyle Hidden | Out-Null;Start-Sleep -Seconds 3}
  $status.database='checking';$status.message='เปิด API และตรวจสอบข้อมูล';Write-Status
  $occupied=Get-NetTCPConnection -LocalPort 4200 -State Listen -ErrorAction SilentlyContinue
  if($occupied){throw 'Port 4200 is used by another process. No unrelated process was stopped.'}
  $api=Start-Process -FilePath $config.node -ArgumentList 'scripts/backup/backup-api.mjs' -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'api.log') -RedirectStandardError (Join-Path $runtime 'api-error.log')
  $state.api=Process-Record $api;Save-State
  $healthy=$false
  for($i=0;$i -lt 30;$i++){
    if($api.HasExited){throw 'API stopped unexpectedly; see api-error.log'}
    $health=Get-Health 'http://127.0.0.1:4200/api/health'
    if($health.database -eq 'ready'){$healthy=$true;break}
    Start-Sleep -Milliseconds 500
  }
  if(!$healthy){throw 'API/database readiness timed out'}
  $status.database='ready';$status.api='ready';$status.message='เปิดช่องทางออนไลน์';Write-Status
  $tunnel=Start-Process -FilePath $config.cloudflared -ArgumentList @('tunnel','--no-autoupdate','--protocol','http2','--url','http://127.0.0.1:4200') -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'tunnel-output.log') -RedirectStandardError (Join-Path $runtime 'tunnel.log')
  $state.tunnel=Process-Record $tunnel;Save-State
  $url=''
  for($i=0;$i -lt 90;$i++){
    if($tunnel.HasExited){throw 'Tunnel stopped unexpectedly; see tunnel.log'}
    $log=Get-Content -LiteralPath (Join-Path $runtime 'tunnel.log') -Raw -ErrorAction SilentlyContinue
    if($log -match 'https://[a-z0-9-]+\.trycloudflare\.com'){$url=$matches[0];$public=Get-Health "$url/api/health";if($public.database -eq 'ready'){break}}
    Start-Sleep -Seconds 1
  }
  if(!$url -or $public.database -ne 'ready'){throw 'Public tunnel is not ready; local server is still available'}
  $status.url=$url;$status.online='ready';$status.message='อัปเดตลิงก์เว็บไซต์สำรองบน GitHub';Write-Status
  $runtimeConfig=@{apiBaseUrl="$url/api";portalApiBaseUrl="$url/api";portalUrl=$status.portalUrl;updatedAt=[DateTime]::UtcNow.ToString('o')}
  $paths=@('runtime-config.json')
  [IO.File]::WriteAllText((Join-Path $root 'runtime-config.json'),($runtimeConfig|ConvertTo-Json),$utf8)
  foreach($app in @('portal','prms-tsm','waste-management','disaster-management','waterworks-management')){
    $relative="apps/$app/public/runtime-config.json";$paths+=$relative
    [IO.File]::WriteAllText((Join-Path $root $relative),($runtimeConfig|ConvertTo-Json),$utf8)
  }
  $remote=& $config.git remote get-url origin
  if($remote.Trim() -ne 'https://github.com/0tyght/smart-tha-pho-backup.git'){throw 'Unexpected Git origin; refusing to push'}
  & $config.git add -- $paths
  if($LASTEXITCODE -ne 0){throw 'Could not stage runtime configuration'}
  & $config.git diff --cached --quiet
  if($LASTEXITCODE -eq 1){
    $staged=@(& $config.git diff --cached --name-only)
    foreach($file in $staged){if($file -notin $paths){throw 'Unrelated staged files detected; refusing automatic commit'}}
    & $config.git commit -m 'Update backup server API address'
    if($LASTEXITCODE -ne 0){throw 'Runtime configuration commit failed'}
  }
  $env:GIT_TERMINAL_PROMPT='0'
  & $config.git push origin main
  if($LASTEXITCODE -ne 0){throw 'Git push failed. Use the direct online URL while resolving Git access.'}
  $status.phase='ready';$status.message='พร้อมใช้งาน • รอ GitHub Pages อัปเดตประมาณ 1–3 นาที';Write-Status
} catch {
  $status.phase='error';$status.message=$_.Exception.Message;Write-Status
  Write-Error $_
  exit 1
} finally { $mutex.ReleaseMutex();$mutex.Dispose() }
