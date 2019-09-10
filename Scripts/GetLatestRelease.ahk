SetWorkingDir %A_ScriptDir%  ; Ensures a consistent starting directory.

UrlDownloadToFile, https://github.com/robinschiro/MonopolyDeal/releases/latest/download/MonopolyDeal.zip, MonopolyDeal.zip

FileRemoveDir MonopolyDeal Recurse
RunWait, %comspec% /c UpdateTools\7z.exe e MonopolyDeal.zip -oMonopolyDeal

FileRemoveDir MonopolyDeal\MonopolyDeal
FileDelete MonopolyDeal.zip