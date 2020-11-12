#!/bin/bash

./build_update_path.sh ../../gaius-docs
./build_update_path.sh ../../gaius-starter

mkdir -p ../BuildArtifacts/zip

./build_update_path.sh ../BuildArtifacts

zip -r ../BuildArtifacts/zip/gaius-engine-bin.zip ../BuildArtifacts/bin/gaius/
zip ../BuildArtifacts/zip/gaius-cli.zip ../BuildArtifacts/gaius.sh ../BuildArtifacts/gaius.ps1
zip ../BuildArtifacts/zip/gaius-github-actions.zip ../BuildArtifacts/.github/workflows/