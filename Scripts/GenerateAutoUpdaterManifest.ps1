[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)] 
    [string]$VersionNumber
)

Out-File -FilePath ..\Release\AutoUpdaterManifest.xml -InputObject @"
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>$($versionNumber)</version>
    <url>https://github.com/robinschiro/MonopolyDeal/releases/latest/download/MonopolyDealInstaller.msi</url>
    <mandatory>true</mandatory>
</item>  
"@