# One-shot release pipeline: build (Unity batch mode) -> package -> upload to S3.
#
# Replaces the old manual dance (open Editor, click Stamp/Build Server/Build Client one at a time,
# switch shells for tar, remember the exact Compress-Archive/aws s3 cp commands). Requires the Unity
# Editor to be CLOSED first - Unity refuses to batch-build a project that's already open.
#
# Does NOT push or trigger the Deploy workflow itself - that's a separate, deliberate action left to
# you (git push, or the GitHub Actions tab), same as every other risky/shared-state step in this repo.
#
# Usage:
#   .\deploy\release.ps1                 # full release: stamp + Linux server + Windows client
#   .\deploy\release.ps1 -ClientOnly     # routine client-only change (no stamp, no server rebuild -
#                                        # only safe if this change doesn't affect anything client/
#                                        # server need to agree on; see DEPLOY-AWS.md section 15b)
#   .\deploy\release.ps1 -SkipBuild      # re-package/upload an existing C:\Builds\Ueq output, no Unity

param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.4.12f1\Editor\Unity.exe",
    [string]$ProjectPath = "C:\Users\acsha\source\Ueq",
    [switch]$ClientOnly,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$buildsRoot = "C:\Builds\Ueq"
$logFile = Join-Path $buildsRoot "release-build.log"

if (-not $env:DEPLOY_BUCKET) {
    Write-Error "DEPLOY_BUCKET env var is not set. Set it first, e.g.: `$env:DEPLOY_BUCKET = 'your-bucket-name'"
    exit 1
}

if (-not $SkipBuild) {
    if (Get-Process -Name "Unity" -ErrorAction SilentlyContinue) {
        Write-Error "The Unity Editor is currently running - close it first. Batch-mode builds can't run against an already-open project."
        exit 1
    }
    if (-not (Test-Path $UnityPath)) {
        Write-Error "Unity not found at '$UnityPath'. Pass -UnityPath if it's installed somewhere else."
        exit 1
    }

    New-Item -ItemType Directory -Force -Path $buildsRoot | Out-Null

    $method = if ($ClientOnly) { "ServerBuildTools.ReleaseClientOnly" } else { "ServerBuildTools.ReleaseAll" }
    $expectedSuccesses = if ($ClientOnly) { 1 } else { 2 }

    Write-Host "Building ($method) - this can take a few minutes, longer on a fresh platform switch..."
    # Start-Process -Wait, not the & call operator: Unity.exe is a GUI-subsystem executable, and &
    # returns as soon as it's launched rather than waiting for it to exit - the script would otherwise
    # race ahead and check results before the build has even really started.
    $arguments = @(
        "-batchmode", "-quit",
        "-projectPath", "`"$ProjectPath`"",
        "-executeMethod", $method,
        "-logFile", "`"$logFile`""
    )
    $proc = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru
    $exitCode = $proc.ExitCode

    $succeeded = 0
    if (Test-Path $logFile) {
        $succeeded = (Select-String -Path $logFile -Pattern "Result: Succeeded" -ErrorAction SilentlyContinue).Count
    }
    if ($exitCode -ne 0 -or $succeeded -lt $expectedSuccesses) {
        Write-Error "Build failed or incomplete (exit code $exitCode, $succeeded/$expectedSuccesses succeeded). Check $logFile for details."
        exit 1
    }
    Write-Host "Build succeeded ($succeeded/$expectedSuccesses)." -ForegroundColor Green
}

if (-not $ClientOnly) {
    Write-Host "Packaging Linux dedicated server..."
    Push-Location (Join-Path $buildsRoot "ServerLinux")
    tar czf (Join-Path $buildsRoot "gameserver.tar.gz") .
    Pop-Location

    Write-Host "Uploading gameserver.tar.gz to s3://$env:DEPLOY_BUCKET ..."
    aws s3 cp (Join-Path $buildsRoot "gameserver.tar.gz") "s3://$env:DEPLOY_BUCKET/gameserver.tar.gz"
}

Write-Host "Packaging Windows client..."
Compress-Archive -Path (Join-Path $buildsRoot "Client\*") -DestinationPath (Join-Path $buildsRoot "client.zip") -Force

Write-Host "Uploading client.zip to s3://$env:DEPLOY_BUCKET ..."
aws s3 cp (Join-Path $buildsRoot "client.zip") "s3://$env:DEPLOY_BUCKET/client.zip"

Write-Host ""
Write-Host "Upload complete. Push to main (or run the Deploy workflow manually from the GitHub Actions tab) to finish." -ForegroundColor Green
