# MonopolyDeal
Welcome to Monopoly Deal! This is a digital recreation of America's favorite version of America's most-hated board game.

[![Build status](https://ci.appveyor.com/api/projects/status/0a26o51ugctxmfex/branch/master?svg=true)](https://ci.appveyor.com/project/robinschiro/monopolydeal/branch/master)

## Prerequisites
You will need to be running Windows 7+ with the .NET Framework 4.5+ installed. You can install this version of the .NET Framework [here](https://www.microsoft.com/en-us/download/confirmation.aspx?id=30653).

## Setup
### Installation
To install the latest release of Monopoly Deal, follow [this link](https://github.com/robinschiro/MonopolyDeal/releases/latest/download/MonopolyDealInstaller.msi). The game will be installed to your 'Program Files (x86)' folder.

### Running the game
Once you have installed the game, you should see a shortcut called 'MonopolyDeal' on your desktop. Open it to start to playing! It will allow you to join a server you created or a friend's server.

### Running the server
If you would like to host your own game server, you can do so by running the 'GameServer.exe' file that is located in the MonopolyDeal installation folder. This executable will create a server on your machine that will allow you and your friends to play together. You will need to set up port forwarding to whichever port you choose (port 14242 is suggested by default) to allow connections to your server.

## Building the code
To build the installer, you will need the WIX toolset. You can install it [here](https://wixtoolset.org/releases/).
All of the other project dependencies are located in the 'Libraries' folder.
