#!/bin/bash
# Default build, package, and deploy script for Gaius

DIR=$(dirname $(readlink -f $0))
ENGINE_ROOT_DIR="$DIR/.."
ARTIFACTS_DIR="$ENGINE_ROOT_DIR/build-artifacts"

if [[ -d $ARTIFACTS_DIR ]]; then
    rm -rf $ARTIFACTS_DIR
fi

mkdir -p $ARTIFACTS_DIR

# Deploy Gaius artifacts to the main Build Artifacts directory for packaging into Zip files
./deploy_artifacts.sh $ARTIFACTS_DIR
./zip_artifacts.sh

ADDL_ARTIFACTS_TARGETS=( $ENGINE_ROOT_DIR/../gaius-docs $ENGINE_ROOT_DIR/../gaius-starter $ENGINE_ROOT_DIR/../rstrube.github.io )

for i in "${ADDL_ARTIFACTS_TARGETS[@]}"
do
	./deploy_artifacts.sh $i
    cd $i
    ./gaius.sh process-test -y
    cd $DIR
done



# Deploy Gaius artifacts to the Gaius Documentation Site
#./deploy_artifacts.sh $GAIUS_ENGINE_TOPLVL_DIR/../gaius-docs

# Deploy Gaius artifacts to the Gaius Starter Site
#./deploy_artifacts.sh $GAIUS_ENGINE_TOPLVL_DIR/../gaius-starter
