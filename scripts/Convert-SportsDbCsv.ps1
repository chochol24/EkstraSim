<#
.SYNOPSIS
Konwertuje eksport CSV z TheSportsDB na format uzywany w Database/CSV/.

.DESCRIPTION
Powody konwersji i opis obu formatow: docs/DZIALANIE.md, sekcja
"Konwersja eksportu z TheSportsDB".

Nazwy druzyn NIE sa zmieniane - kanonizuje je TeamNameAliases w warstwie importu.

Nic nie jest zapisywane, jesli jakikolwiek wiersz okaze sie niepoprawny.

.EXAMPLE
.\scripts\Convert-SportsDbCsv.ps1 -Path Database\CSV\Ekstraklasa_2026_2027.csv -InPlace

.EXAMPLE
.\scripts\Convert-SportsDbCsv.ps1 -Path pobrane.csv -OutPath Database\CSV\Ekstraklasa_2026_2027.csv
#>
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$OutPath,
    [switch]$InPlace
)

$ErrorActionPreference = 'Stop'

if (-not $InPlace -and [string]::IsNullOrWhiteSpace($OutPath)) {
    throw "Podaj -OutPath albo -InPlace."
}

if ($InPlace) {
    $OutPath = $Path
}

function Split-CsvLine([string]$line) {
    $fields = New-Object System.Collections.Generic.List[string]
    $sb = New-Object System.Text.StringBuilder
    $inQuotes = $false
    for ($i = 0; $i -lt $line.Length; $i++) {
        $c = $line[$i]
        if ($c -eq '"') {
            if ($inQuotes -and $i + 1 -lt $line.Length -and $line[$i + 1] -eq '"') {
                [void]$sb.Append('"')
                $i++
            }
            else {
                $inQuotes = -not $inQuotes
            }
        }
        elseif ($c -eq ',' -and -not $inQuotes) {
            [void]$fields.Add($sb.ToString())
            [void]$sb.Clear()
        }
        else {
            [void]$sb.Append($c)
        }
    }
    [void]$fields.Add($sb.ToString())
    return $fields
}

$cp1250 = [System.Text.Encoding]::GetEncoding(1250)
$raw = $cp1250.GetString([System.IO.File]::ReadAllBytes($Path))
$lines = $raw -split "`r`n|`n" | Where-Object { $_.Trim() -ne '' }

if ($lines.Count -lt 2) {
    throw "Plik $Path ma mniej niz 2 niepuste linie."
}

if ($lines[0] -notmatch '^idEvent,strTimestamp,Round,') {
    throw "Nieoczekiwany naglowek w ${Path}: $($lines[0]). Plik jest juz skonwertowany albo pochodzi z innego zrodla."
}

$out = New-Object System.Collections.Generic.List[string]
$problems = New-Object System.Collections.Generic.List[string]
$played = 0

for ($n = 1; $n -lt $lines.Count; $n++) {
    $line = $lines[$n]

    if ($line.StartsWith('"') -and $line.EndsWith('"')) {
        $line = $line.Substring(1, $line.Length - 2) -replace '""', '"'
    }

    $f = Split-CsvLine $line
    if ($f.Count -lt 8) {
        $problems.Add("linia $($n + 1): $($f.Count) pol, oczekiwano >=8")
        continue
    }

    $id = $f[0].Trim()
    $stamp = $f[1].Trim()
    $roundText = $f[2].Trim()
    $homeName = $f[3].Trim()
    $homeScore = $f[4].Trim()
    $awayName = $f[5].Trim()
    $awayScore = $f[6].Trim()
    $thumb = $f[$($f.Count - 1)].Trim()

    $date = [datetime]::MinValue
    if (-not [datetime]::TryParse($stamp, [cultureinfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$date)) {
        $problems.Add("linia $($n + 1): nieparsowalna data '$stamp'")
        continue
    }

    $m = [regex]::Match($roundText, '(\d+)')
    if (-not $m.Success) {
        $problems.Add("linia $($n + 1): brak numeru kolejki w '$roundText'")
        continue
    }
    $round = [int]$m.Groups[1].Value

    if ($homeName -eq '' -or $awayName -eq '') {
        $problems.Add("linia $($n + 1): brak nazwy druzyny")
        continue
    }

    # Format docelowy jest bez cudzyslowow, wiec przecinek w polu by go rozjechal.
    foreach ($v in @($id, $homeName, $awayName, $thumb)) {
        if ($v.Contains(',')) {
            $problems.Add("linia $($n + 1): pole zawiera przecinek: '$v'")
        }
    }

    if ($homeScore -ne '' -and $awayScore -ne '') {
        $played++
    }

    $out.Add("$id,$($date.ToString('yyyy-MM-dd')),$round,$homeName,$homeScore,$awayName,$awayScore,$thumb")
}

if ($problems.Count -gt 0) {
    Write-Output "PROBLEMY w ${Path}:"
    $problems | ForEach-Object { Write-Output "  $_" }
    throw "Konwersja przerwana - $($problems.Count) problemow, plik wyjsciowy nie zapisany."
}

if ($InPlace) {
    $backup = "$Path.orig"
    if (-not (Test-Path $backup)) {
        Copy-Item $Path $backup
        Write-Output "Kopia oryginalu: $backup"
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutPath, ($out -join "`r`n") + "`r`n", $utf8NoBom)

$rounds = ($out | ForEach-Object { ($_ -split ',')[2] } | Sort-Object -Unique).Count
$teams = ($out | ForEach-Object { $p = $_ -split ','; $p[3]; $p[5] } | Sort-Object -Unique).Count
Write-Output ("{0}: {1} wierszy, {2} rozegranych, {3} kolejek, {4} druzyn -> {5}" -f `
    (Split-Path $Path -Leaf), $out.Count, $played, $rounds, $teams, (Split-Path $OutPath -Leaf))
