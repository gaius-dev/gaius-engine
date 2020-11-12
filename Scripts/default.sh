#!/bin/bash
# Default build, deploy, and package script for Gaius

DIR=$(dirname $(readlink -f $0))
GAIUS_ENGINE_TOPLVL_DIR="$DIR/.."
BUILD_ARTIFACTS_DIR="$GAIUS_ENGINE_TOPLVL_DIR/BuildArtifacts"

if [[ -d $BUILD_ARTIFACTS_DIR ]]; then
    rm -rf $BUILD_ARTIFACTS_DIR
fi

mkdir -p $BUILD_ARTIFACTS_DIR

# Deploy Gaius artifacts to the main Build Artifacts directory for packaging into Zip files
./deploy_artifacts.sh $BUILD_ARTIFACTS_DIR
./zip_artifacts.sh

# Deploy Gaius artifacts to the Gaius Documentation Site
./deploy_artifacts.sh $GAIUS_ENGINE_TOPLVL_DIR/../gaius-docs

# Deploy Gaius artifacts to the Gaius Starter Site
./deploy_artifacts.sh $GAIUS_ENGINE_TOPLVL_DIR/../gaius-starter
