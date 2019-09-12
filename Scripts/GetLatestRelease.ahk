SetWorkingDir %A_ScriptDir%  ; Ensures a consistent starting directory.

UrlDownloadToFile, https://github.com/robinschiro/MonopolyDeal/releases/latest/download/MonopolyDeal.zip, MonopolyDeal.zip

FileRemoveDir MonopolyDeal Recurse
RunWait, %comspec% /c UpdateTools\7z.exe e MonopolyDeal.zip -oMonopolyDeal -y

; Cleanup unneeded files/folders.
FileDelete MonopolyDeal.zip
FileDelete MonopolyDeal\GetLatestRelease.exe
FileDelete MonopolyDeal\7z.exe
FileDelete MonopolyDeal\7z.dll
FileRemoveDir MonopolyDeal\UpdateTools
FileRemoveDir MonopolyDeal\MonopolyDeal
