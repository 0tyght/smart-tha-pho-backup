param([string]$OutputPath)
$ErrorActionPreference='Stop'
if(!$OutputPath){$OutputPath=Join-Path ([Environment]::GetFolderPath('Desktop')) 'Smart Tha Pho Backup Server.exe'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/out:$OutputPath" (Join-Path $PSScriptRoot 'BackupServerMonitor.cs')
if($LASTEXITCODE -ne 0){throw 'Monitor compilation failed'}
Write-Output "Created: $OutputPath"
