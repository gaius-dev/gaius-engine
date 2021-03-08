#!/bin/bash
# Creates ZIP files from the Build Artifacts for Release in Github

DIR=$(dirname $(readlink -f $0))
ROOT_SRC_DIR="$DIR/.."
BUILD_ARTIFACTS_DIR="$ROOT_SRC_DIR/build-artifacts"
BUILD_ZIP_DIR="$ROOT_SRC_DIR/build-zip"

if [[ -d $BUILD_ZIP_DIR ]]; then
    rm -rf $BUILD_ZIP_DIR
fi

mkdir -p $BUILD_ZIP_DIR

echo "Gaius Solution Dir: $ROOT_SRC_DIR"
echo "Build Artifacts Dir: $BUILD_ARTIFACTS_DIR"
echo "Build Zip Dir: $BUILD_ZIP_DIR"

cd $BUILD_ARTIFACTS_DIR/bin/gaius
zip $BUILD_ZIP_DIR/gaius-bin.zip ./*
cd $DIR

#cd $BUILD_ARTIFACTS_DIR
#zip $BUILD_ZIP_DIR/gaius-cli.zip ./gaius.sh
#cd $DIR

cd $BUILD_ARTIFACTS_DIR/.github/workflows
zip $BUILD_ZIP_DIR/gaius-github-actions.zip ./*
cd $DIR