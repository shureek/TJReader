function Get-TJRecord {
    [CmdletBinding()]
    param(
        [string]$Path = '.',
        [string]$ProcessName,
        [int]$ProcessID,
        [DateTime]$After,
        [DateTime]$Before
    )
    
    begin {
        $reader = New-Object -TypeName 'TJLib.TJReader'
        Register-ObjectEvent -InputObject $reader -EventName ErrorOccured -Action { Write-Error -Exception ($EventArgs.Error) } | Out-Null
    }

    process {
        Get-ChildItem -Path $Path -Filter ????????.log -Recurse | %{
            $FullName = $_.FullName
            $FileName = $_.Name
            $FolderName = $_.Directory.Name

            if ($FolderName -notmatch '^(?<Name>[a-zA-Z0-9]+)_(?<ID>\d+)$') {
                Write-Warning "Wrong folder name $FolderName"
                continue
            }
            $mProcessName = $Matches.Name
            [int]$mProcessID = $Matches.ID

            if ($FileName -notmatch '^(?<Year>\d\d)(?<Month>\d\d)(?<Day>\d\d)(?<Hour>\d\d).log$') {
                Write-Warning "Wrong file name $FileName"
                continue
            }
            [int]$Month = $Matches.Month
            [int]$Hour = $Matches.Hour
            [int]$Day = $Matches.Day
            [int]$Year = 2000 + [int]$Matches.Year
            $FileDate = Get-Date -Year $Year -Month $Month -Day $Day -Hour $Hour -Minute 0 -Second 0 -Millisecond 0

            $FilterOK = $true
            if ($PSBoundParameters.ContainsKey('ProcessID') -and $mProcessID -ne $ProcessID) {
                $FilterOK = $false
            }
            elseif ($PSBoundParameters.ContainsKey('ProcessName') -and $mProcessName -ne $ProcessName) {
                $FilterOK = $false
            }
            elseif ($PSBoundParameters.ContainsKey('Before') -and $FileDate -gt $Before) {
                $FilterOK = $false
            }
            elseif ($PSBoundParameters.ContainsKey('After') -and $FileDate -lt $After.Date.AddHours($After.Hour)) {
                $FilterOK = $false
            }
            elseif ($_.Length -le 3) {
                $FilterOK = $false
            }

            if ($FilterOK) {
                [PSCustomObject]@{
                    FileName = $FullName
                    FileDate = $FileDate
                    ProcessName = $mProcessName
                    ProcessID = $mProcessID
                    Length = $_.Length
                }
            }
            elseif ($_.Length -gt 3) {
                Write-Verbose "Skipping ${FullName}: $mProcessName ($mProcessID), $FileDate, $($_.Length) bytes"
            }
        } | sort FileDate,ProcessID | %{
            Write-Verbose "Reading $($_.FileName): $($_.ProcessName) ($($_.ProcessID)), $($_.FileDate), $($_.Length) bytes"
            $stopwatch = [Diagnostics.StopWatch]::StartNew()
            $reader.Open($_.FileName)
            $reader.ProcessName = $_.ProcessName
            $reader.ProcessID = $_.ProcessID
            $reader.FileDate = $_.FileDate
            $reader.ComputerName = '.'

            [long]$count = 0
            [long]$skipped = 0

            if ($PSBoundParameters.ContainsKey('After') -and $_.FileDate -lt $After -and $_.FileDate.AddHours(1) -gt $After) {
                # Skipping first events, filtered by 'After'
                while($true) {
                    $record = $reader.ReadRecord();
                    if ($null -eq $record) {
                        break
                    }
                    
                    if ($record.Date -ge $After) {
                        Write-Output $record
                        $count++
                        break
                    }
                    else {
                        $skipped++
                    }
                }
            }

            if ($skipped -gt 0) {
                Write-Verbose "Skipped $skipped events"
            }

            $FilterBefore = $PSBoundParameters.ContainsKey('Before') -and $_.FileDate.AddHours(1) -gt $Before
            $WroteProgress = $false

            while($true) {
                $record = $reader.ReadRecord();
                if ($null -eq $record) {
                    break
                }
                elseif ($FilterBefore -and $record.Date -gt $Before) {
                    break
                }

                Write-Output $record
                $count++

                if ($count % 1000 -eq 0 -and $stopwatch.ElapsedMilliseconds -gt 5000) {
                    $seconds = $stopwatch.ElapsedMilliseconds / 1000
                    Write-Progress -Activity "Reading $($_.FileName)" -Status "Read $count records ($([Math]::Round($count / $seconds))/sec), $($reader.Position) bytes ($([Math]::Round($reader.Position / $seconds))/sec)" -PercentComplete ($reader.Position / $_.Length * 100)
                    $WroteProgress = $true
                }
            }
            $stopwatch.Stop()
            if ($WroteProgress) {
                Write-Progress -Activity "Reading $($_.FileName)" -Completed
            }

            $seconds = $stopwatch.ElapsedMilliseconds / 1000

            if ($seconds -gt 0) {
                Write-Verbose "Read $count records ($([Math]::Round($count / $seconds))/sec), $($reader.Position) bytes ($([Math]::Round($reader.Position / $seconds))/sec) in $($stopwatch.Elapsed)"
            }
            else {
                Write-Verbose "Read $count records, $($reader.Position) bytes in $($stopwatch.Elapsed)"
            }

            $reader.Close()
        }
    }

    end {
        $reader.Close()
    }
}

function Format-PlanSQL {
    param(
        [Parameter(Mandatory, Position=0, ValueFromPipeline)]
        [string]$text
    )

    $recTemplate = @{}

    $text -split "`n" | %{
        $line = [string]$_
        if (-not [String]::IsNullOrWhiteSpace($line)) {
            $treeIndex = $line.IndexOf('|')
            $parts = $line.Substring(0, $treeIndex).Split(',')
            if ($parts.Count -eq 9) {
                # MSSQL
                $recTemplate['RecordsFact'] = [Int64]::Parse($parts[0])
                $recTemplate['CallsFact'] = [Int64]::Parse($parts[1].Substring(1))
                $recTemplate['RecordsPlan'] = [Double]::Parse($parts[2].Substring(1), [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture)
                $recTemplate['InputOutput'] = [Double]::Parse($parts[3].Substring(1), [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture)
                $recTemplate['CPU'] = [Double]::Parse($parts[4].Substring(1), [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture)
                $recTemplate['RecordSizeAvg'] = [Double]::Parse($parts[5].Substring(1), [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture)
                $recTemplate['Cost'] = [Double]::Parse($parts[6].Substring(1), [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture)
                $recTemplate['CallsPlan'] = [Double]::Parse($parts[7].Substring(1), [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture)
                $recTemplate['Operator'] = $parts[8].Substring(1) + $line.Substring($treeIndex)
                $rec = [PSCustomObject]$recTemplate
                $rec.PSObject.TypeNames.Insert(0, 'SQLPlanRecord')
                $rec
            }
            else {
                Write-Verbose "line: '$line'"
                Write-Verbose "parts: $($parts.Count)"
                Write-Error -Message "Непонятная структура строки" -ErrorAction Stop
            }
        }
    }
}

Export-ModuleMember -Function Get-TJRecord
Export-ModuleMember -Function Format-PlanSQL
