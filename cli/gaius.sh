#!/bin/bash
# Gaius CLI wrapper for Linux/OSX

VERSION="0.0.80"

function main() {

    if [[ ! -d ./bin/gaius ]]; then
        echo "No Gaius engine binaries detected in ./bin/gaius ..."
        exit 1
    fi

    if [[ ! -d ./bin/gaius-server ]]; then
        echo "No Gaius server binaries detected in ./bin/gaius-server ..."
        exit 1
    fi

    if [[ "$1" == "version" ]]; then
        dotnet ./bin/gaius/gaius.dll version
        dotnet ./bin/gaius-server/gaius-server.dll version
        echo "Gaius CLI wrapper version $VERSION"
        exit 0
    fi

    if [[ "$1" == "update-all" ]]; then
        update-engine
        update-server
        update-github-actions
        update-cli
        exit 0
    fi

    if [[ "$1" == "update-engine" ]]; then
        update-engine
        exit 0
    fi

    if [[ "$1" == "update-server" ]]; then
        update-server
        exit 0
    fi

    if [[ "$1" == "update-cli" ]]; then
        update-cli
        exit 0
    fi

    if [[ "$1" == "update-github-actions" ]]; then
        update-github-actions
        exit 0
    fi

    if [[ "$1" == "test" ]]; then
        dotnet ./bin/gaius/gaius.dll process-test -y
        dotnet ./bin/gaius-server/gaius-server.dll
    else
        dotnet ./bin/gaius/gaius.dll "$@"
    fi
}

function update-engine() {

    if [[ -d ./bin/gaius ]]; then
        echo "Deleting existing Gaius engine binaries in ./bin/gaius ..."
        rm -rf ./bin/gaius
    fi

    mkdir -p ./bin/gaius

    echo "Downloading latest release of Gaius engine binaries..."
    curl -O -L https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-engine-bin.zip

    echo "Extracting latest release of Gaius engine binaries to ./bin/gaius ..."
    unzip gaius-engine-bin.zip -d ./bin/gaius
    rm gaius-engine-bin.zip
}

function update-server() {

    if [[ -d ./bin/gaius-server ]]; then
        echo "Deleting existing Gaius server binaries in ./bin/gaius-server ..."
        rm -rf ./bin/gaius-server
    fi

    mkdir -p ./bin/gaius-server

    echo "Downloading latest release of Gaius server binaries..."
    curl -O -L https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-server-bin.zip

    echo "Extracting latest release of Gaius server binaries to ./bin/gaius-server ..."
    unzip gaius-server-bin.zip -d ./bin/gaius-server
    rm gaius-server-bin.zip
}

function update-cli() {

    echo "Downloading latest release of Gaius CLI..."
    curl -O -L https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-cli.zip

    echo "Extracting latest release of Gauis CLI..."
    unzip -o gaius-cli.zip -d .
    rm gaius-cli.zip
}

function update-github-actions() {

    if [[ -d ./.github/workflows ]]; then
        echo "Deleting existing Gaius Github actions in ./.github/workflows..."
        rm -rf ./.github/workflows
    fi

    mkdir -p ./.github/workflows

    echo "Downloading latest release of Gaius Github actions..."
    curl -O -L https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-github-actions.zip

    echo "Extracting latest release of Gaius engine binaries to ./.github/workflows ..."
    unzip gaius-github-actions -d ./.github/workflows
    rm gaius-github-actions.zip
}

main "$@"