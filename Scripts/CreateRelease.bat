set releaseGetter=GetLatestRelease
set zipName=MonopolyDeal

REM Compile AHK script the retrieves latest release from Github
Ahk2Exe.exe /in %releaseGetter%.ahk

REM Zip contents
7z a -tzip %zipName%.zip ..\Release %releaseGetter%.exe UpdateTools
sleep 1
7z rn %zipName%.zip Release\ %zipName%\

del %releaseGetter%.exe
