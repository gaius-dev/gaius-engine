# Gaius CLI wrapper for Windows

$version = "0.0.60"

function Main() {

    if( -not ( Test-Path .\bin\gaius -PathType Container ) )
    {
        "No Gaius engine binaries detected in .\bin.\gaius ..."
        Exit 1
    }

    if( -not ( Test-Path .\bin\gaius-server -PathType Container ) )
    {
        "No Gaius server binaries detected in .\bin.\gaius-server ..."
        Exit 1
    }

    if($args[0] -eq "version")
    {
        dotnet .\bin\gaius\gaius.dll version
        dotnet .\bin\gaius-server\gaius-server.dll version
        "Gaius CLI wrapper version $version"
        Exit
    }

    if($args[0] -eq "update-all") 
    {
        Update-Engine
        Update-Server
        Update-CLI
        Update-Github-Actions
        Exit
    }

    if($args[0] -eq "update-engine") 
    {
        Update-Engine
        Exit
    }

    if($args[0] -eq "update-server") 
    {
        Update-Server
        Exit
    }

    if($args[0] -eq "update-cli") 
    {
        Update-CLI
        Exit
    }
    
    if($args[0] -eq "update-github-actions") 
    {
        Update-Github-Actions
        Exit
    }

    if($args[0] -eq "test") 
    {
        dotnet .\bin\gaius\gaius.dll process-test -y
        dotnet .\bin\gaius-server\gaius-server.dll
    }
    else
    {
        dotnet .\bin\gaius\gaius.dll $args
    }
}

function Update-Engine() {

    if( Test-Path .\bin\gaius -PathType Container )
    {
        "Deleting existing Gaius engine binaries in .\bin\gaius ..."
        Remove-Item .\bin\gaius -Recurse
    }

    New-Item -Path ".\bin" -Name "gaius" -ItemType "directory"

    "Downloading latest release of Gaius engine binaries..."
    Invoke-WebRequest -Uri https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-engine-bin.zip -OutFile .\gaius-engine-bin.zip

    "Extracting latest release of Gaius engine binaries to .\bin\gaius ..."
    Expand-Archive -Path .\gaius-engine-bin.zip -DestinationPath .\bin\gaius
    Remove-Item .\gaius-engine-bin.zip
}

function Update-Server() {

    if( Test-Path .\bin\gaius-server -PathType Container )
    {
        "Deleting existing Gaius server binaries in .\bin\gaius-server ..."
        Remove-Item .\bin\gaius-server -Recurse
    }

    New-Item -Path ".\bin" -Name "gaius-server" -ItemType "directory"

    "Downloading latest release of Gaius server binaries..."
    Invoke-WebRequest -Uri https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-server-bin.zip -OutFile .\gaius-server-bin.zip

    "Extracting latest release of Gaius server binaries to .\bin\gaius-server ..."
    Expand-Archive -Path .\gaius-server-bin.zip -DestinationPath .\bin\gaius-server
    Remove-Item .\gaius-server-bin.zip
}

function Update-CLI() {

    "Downloading latest release of Gaius CLI wrapper..."
    Invoke-WebRequest -Uri https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-cli.zip -OutFile .\gaius-cli.zip

    "Extracting latest release of Gauis CLI wrapper..."
    Expand-Archive -Path .\gaius-cli.zip -DestinationPath .\ -Force
    Remove-Item .\gaius-cli.zip
}

function Update-Github-Actions() {

    if( Test-Path .\bin\gaius -PathType Container )
    {
        "Deleting existing Gaius Github actions in .\.github\workflows..."
        Remove-Item .\.github\workflows -Recurse
    }

    New-Item -Path ".\.github" -Name "workflows" -ItemType "directory"

    "Downloading latest release of Gaius Github actions..."
    Invoke-WebRequest -Uri https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-github-actions.zip -OutFile .\gaius-github-actions.zip

    "Extracting latest release of Gaius Github actions to .\.github\workflows ..."
    Expand-Archive -Path .\gaius-github-actions.zip -DestinationPath .\.github\workflows
    Remove-Item .\gaius-github-actions.zip
}

Main $args