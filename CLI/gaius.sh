#!/bin/bash
# Gaius CLI wrapper for Linux/OSX
# v0.0.4

function main() {

    if [[ "$1" == "update-all" ]]; then
        update-engine
        update-github-actions
        update-cli
        exit 0
    fi

    if [[ "$1" == "update-engine" ]]; then
        update-engine
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

    if [[ ! -d ./bin/gaius ]]; then
        echo "No Gaius engine binaries detected in ./bin/gaius ..."
        update-engine
    fi

    dotnet ./bin/gaius/gaius.dll "$@"
}

function update-engine() {

    if [[ -d ./bin/gaius ]]; then
        echo "Deleting existing Gaius engine binaries in ./bin/gaius ..."
        rm -rf ./bin/gaius
    fi

    echo "Downloading latest release of Gaius engine binaries..."
    curl -O -L https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-engine-bin.zip

    mkdir -p ./bin/gaius

    echo "Extracting latest release of Gaius engine binaries to ./bin/gaius ..."
    unzip gaius-engine-bin.zip -d ./bin/gaius
    rm gaius-engine-bin.zip
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

    echo "Downloading latest release of Gaius Github actions..."
    curl -O -L https://github.com/gaius-dev/gaius-engine/releases/latest/download/gaius-github-actions.zip

    mkdir -p ./.github/workflows

    echo "Extracting latest release of Gaius engine binaries to ./bin/gaius ..."
    unzip gaius-github-actions -d ./.github/workflows
    rm gaius-github-actions.zip
}

main "$@"