$SourceFiles = Get-ChildItem -Path $PSScriptRoot -Filter *.cs | select -ExpandProperty FullName
if ($SourceFiles.Count -ne 4) {
    throw "Найдено $($SourceFiles.Count) файлов *.cs"
}
Add-Type -Path $SourceFiles -ReferencedAssemblies System.Management.Automation
