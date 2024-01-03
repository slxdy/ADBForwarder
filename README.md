# ADBForwarder

Console application designed to handle forwarding TCP Ports (using [ADB](https://developer.android.com/studio/command-line/adb)) between your PC and Android devices, over USB

Specifically made for use with [ALVR](https://github.com/alvr-org/ALVR), 
  but config.json can be configured to work with any port needed for any other purposes.

Supports any ADB compatible device

### [Download here!](https://github.com/alvr-org/ADBForwarder/releases)

## Usage

* [Download the latest release](https://github.com/alvr-org/ADBForwarder/releases)
* Extract the archive somewhere convenient in a separate directory
* Run the program and ALVR, order does not matter
* ALVR may (or may not) restart
* You should see your device's serial ID show up in the console, if it says the following, all is well!
    * `Successfully forwarded device: ...`

## Problems?

Don't hesitate to raise an [issue](https://github.com/alvr-org/ADBForwarder/issues) if you encounter problems!

## Attributions

Thank you. [AtlasTheProto](https://github.com/AtlasTheProto), for OG ADBForwarder codebase

Thank you, [Mantas-2155X](https://github.com/Mantas-2155X), for iterating and refactoring AtlasTheProto's work, to bring Linux support!

Thank you, [Quamotion](https://github.com/quamotion), for [SharpADBClient](https://github.com/quamotion/madb)!
