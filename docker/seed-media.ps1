$ErrorActionPreference = "Stop"

$mediaRoot = Join-Path $PSScriptRoot "media"
$movies = Join-Path $mediaRoot "movies"
$shows = Join-Path $mediaRoot "shows\Test Show (2024)\Season 01"
$font = "C\:/Windows/Fonts/arial.ttf"

function New-Poster {
    param(
        [string]$Path,
        [string]$Color,
        [string]$Label
    )

    $dir = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    & ffmpeg -y -hide_banner -loglevel error `
        -f lavfi -i "color=c=${Color}:s=400x600:d=1" `
        -vf "drawtext=fontfile='${font}':text='${Label}':fontcolor=white:fontsize=42:x=(w-text_w)/2:y=(h-text_h)/2" `
        -frames:v 1 $Path

    if ($LASTEXITCODE -ne 0) {
        & ffmpeg -y -hide_banner -loglevel error `
            -f lavfi -i "color=c=${Color}:s=400x600:d=1" `
            -frames:v 1 $Path
    }
}

function New-Clip {
    param(
        [string]$Path,
        [string[]]$Languages,
        [string[]]$Titles
    )

    $dir = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    $ffmpegArgs = @(
        "-y", "-hide_banner", "-loglevel", "error",
        "-f", "lavfi", "-i", "color=c=0x1b2838:s=640x360:d=3"
    )

    for ($i = 0; $i -lt $Languages.Count; $i++) {
        $freq = 440 + ($i * 110)
        $ffmpegArgs += @("-f", "lavfi", "-i", "sine=f=${freq}:d=3")
    }

    $ffmpegArgs += @("-map", "0:v")
    for ($i = 0; $i -lt $Languages.Count; $i++) {
        $ffmpegArgs += @("-map", "$($i + 1):a")
        $title = if ($Titles -and $i -lt $Titles.Count) { $Titles[$i] } else { $Languages[$i] }
        $ffmpegArgs += @("-metadata:s:a:$i", "language=$($Languages[$i])")
        $ffmpegArgs += @("-metadata:s:a:$i", "title=$title")
    }

    $ffmpegArgs += @(
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac",
        "-shortest", $Path
    )

    & ffmpeg @ffmpegArgs
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed for $Path" }
}

Write-Host "Generating test media..."

$voMovie = Join-Path $movies "Test Movie VO (2024)"
New-Poster -Path (Join-Path $voMovie "poster.jpg") -Color "0x8b1e3f" -Label "MOVIE VO"
New-Clip -Path (Join-Path $voMovie "Test Movie VO (2024).mp4") -Languages @("eng")

$vfMovie = Join-Path $movies "Test Movie VF (2024)"
New-Poster -Path (Join-Path $vfMovie "poster.jpg") -Color "0x1b4332" -Label "MOVIE VF"
New-Clip -Path (Join-Path $vfMovie "Test Movie VF (2024).mp4") -Languages @("fra")

$vfqMovie = Join-Path $movies "Test Movie VFQ (2024)"
New-Poster -Path (Join-Path $vfqMovie "poster.jpg") -Color "0x1e4d8c" -Label "MOVIE VFQ"
New-Clip -Path (Join-Path $vfqMovie "Test Movie VFQ (2024).mp4") -Languages @("fra") -Titles @("VFQ")

$dualMovie = Join-Path $movies "Test Movie Dual (2024)"
New-Poster -Path (Join-Path $dualMovie "poster.jpg") -Color "0x1d3557" -Label "MOVIE DUAL"
New-Clip -Path (Join-Path $dualMovie "Test Movie Dual (2024).mp4") -Languages @("eng", "fra")

New-Poster -Path (Join-Path $shows "poster.jpg") -Color "0x3d2b1f" -Label "SHOW"
New-Clip -Path (Join-Path $shows "Test Show - S01E01 - VO.mp4") -Languages @("eng")
New-Clip -Path (Join-Path $shows "Test Show - S01E02 - VF.mp4") -Languages @("fra")

Write-Host "Test media ready in $mediaRoot"
Write-Host "  Movies: VO (pill), VFQ (blue pill), VF (no badge), Dual (no badge)"
Write-Host "  Shows:  S01E01 VO (badge), S01E02 VF (no badge)"
