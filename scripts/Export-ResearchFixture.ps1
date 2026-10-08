param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$Database = 'EkstraSimDB',
    [string]$OutPath = 'EkstraSim.Tests/Data/ekstraklasa-liga1-mecze.csv'
)

$ErrorActionPreference = 'Stop'

$filter = @'
FROM Matches m
WHERE m.LeagueId = 1
  AND m.SeasonId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM Matches u
      WHERE u.LeagueId = 1
        AND u.SeasonId = m.SeasonId
        AND (u.HomeTeamScore IS NULL OR u.AwayTeamScore IS NULL))
'@

function Invoke-Query([string]$query) {
    $flat = $query -replace '\s+', ' '
    $output = & sqlcmd -S $Server -d $Database -E -I -b -W -h -1 -s ';' -Q "SET NOCOUNT ON; $flat"
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Output $_ }
        throw "sqlcmd zakonczyl sie kodem $LASTEXITCODE - plik nie zapisany."
    }
    return @($output | Where-Object { $_.Trim() -ne '' })
}

$expected = [int]@(Invoke-Query "SELECT COUNT(*) $filter")[0]

$rows = @(Invoke-Query @"
SELECT m.Id, CONVERT(varchar(33), m.Date, 126), m.Round, m.SeasonId, m.LeagueId,
       m.HomeTeamId, m.AwayTeamId, m.HomeTeamScore, m.AwayTeamScore
$filter
ORDER BY m.Id
"@)

$out = New-Object System.Collections.Generic.List[string]
$problems = New-Object System.Collections.Generic.List[string]
$previousId = [int]::MinValue
$seasons = New-Object System.Collections.Generic.HashSet[int]

foreach ($row in $rows) {
    $f = $row -split ';'
    if ($f.Count -ne 9) {
        $problems.Add("'$row': $($f.Count) pol, oczekiwano 9")
        continue
    }

    $date = [datetime]::MinValue
    if (-not [datetime]::TryParse($f[1], [cultureinfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$date)) {
        $problems.Add("'$row': nieparsowalna data '$($f[1])'")
        continue
    }
    if ($date.TimeOfDay -ne [timespan]::Zero) {
        $problems.Add("'$row': data z godzina, format yyyy-MM-dd by ja obcial")
        continue
    }

    $values = New-Object int[] 9
    $valid = $true
    foreach ($i in @(0, 2, 3, 4, 5, 6, 7, 8)) {
        $value = 0
        if (-not [int]::TryParse($f[$i], [System.Globalization.NumberStyles]::None, [cultureinfo]::InvariantCulture, [ref]$value)) {
            $problems.Add("'$row': pole $i '$($f[$i])' nie jest liczba calkowita")
            $valid = $false
            break
        }
        $values[$i] = $value
    }
    if (-not $valid) {
        continue
    }

    if ($values[0] -le $previousId) {
        $problems.Add("'$row': Id nie rosnie")
        continue
    }
    $previousId = $values[0]
    [void]$seasons.Add($values[3])

    $out.Add(('{0};{1};{2};{3};{4};{5};{6};{7};{8}' -f `
        $values[0], $date.ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture), $values[2], $values[3], $values[4], `
        $values[5], $values[6], $values[7], $values[8]))
}

if ($out.Count -ne $expected) {
    $problems.Add("wierszy po parsowaniu: $($out.Count), w bazie: $expected")
}

if ($problems.Count -gt 0) {
    Write-Output "PROBLEMY:"
    $problems | ForEach-Object { Write-Output "  $_" }
    throw "Eksport przerwany - $($problems.Count) problemow, plik nie zapisany."
}

$target = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutPath))
$directory = Split-Path $target -Parent
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

$header = 'Id;Date;Round;SeasonId;LeagueId;HomeTeamId;AwayTeamId;HomeScore;AwayScore'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$out.Insert(0, $header)
[System.IO.File]::WriteAllText($target, ($out -join "`r`n") + "`r`n", $utf8NoBom)

Write-Output ("{0} meczow z {1} sezonow ({2}) -> {3}" -f `
    ($out.Count - 1), $seasons.Count, (($seasons | Sort-Object) -join ','), $target)
