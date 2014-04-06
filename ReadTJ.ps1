$SourceFiles = Get-ChildItem -Path 'C:\Data\Development\1C\TJReader\TJLib' -Filter *.cs | select -ExpandProperty FullName
Add-Type -Path $SourceFiles -ReferencedAssemblies System.Management.Automation

function Get-TJRecord {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [String]$Path
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

            if (-not ($FolderName -match '^(?<Name>[a-zA-Z0-9]+)_(?<ID>\d+)$')) {
                Write-Warning "Wrong folder name $FolderName"
                continue
            }
            $ProcessName = $Matches.Name
            [int]$ProcessID = $Matches.ID

            if (-not ($FileName -match '^(?<Year>\d\d)(?<Month>\d\d)(?<Day>\d\d)(?<Hour>\d\d).log$')) {
                Write-Warning "Wrong file name $FileName"
                continue
            }
            [int]$Month = $Matches.Month
            [int]$Hour = $Matches.Hour
            [int]$Day = $Matches.Day
            [int]$Year = 2000 + [int]$Matches.Year
            $FileDate = Get-Date -Year (2000 + [int]$Matches.Year) -Month ($Matches.Month) -Day ($Matches.Day) -Hour ($Matches.Hour) -Minute 0 -Second 0 -Millisecond 0

            Write-Verbose "Reading $FolderName`\$($FileName): $ProcessName ($ProcessID), $FileDate"
            $reader.Open($FullName)
            $reader.ProcessName = $ProcessName
            $reader.ProcessID = $ProcessID
            $reader.FileDate = $FileDate

            while($true) {
                $record = $reader.ReadRecord();
                if ($record -eq $null) {
                    break
                }
                Write-Output $record
            }

            $reader.Close()
        }
    }

    end {
        
    }
}

$Path = 'C:\Data\Work\Гарантия\logs'
Get-TJRecord $Path -Verbose