#!/bin/sh

if [ "$#" -ne "1" ]; then
	echo "Error: gaius build script requires [path] location which specifies where to deploy the gaius binary."
	echo "Usage: ./build-osx-x64.sh [path]"
	echo "Example: ./build-osx-x64.sh ~/MySites/SampleSite"
	exit 1
fi

BUILD_PATH="$1"
RID=osx-x64

echo ""
echo "Building platform specific gaius binary for "$RID"..."
echo ""

dotnet publish ./Gaius/Gaius.csproj -c Release -r "$RID" --self-contained false -p:PublishSingleFile=true -p:DebugType=None -o "$BUILD_PATH"

echo ""
echo ""$RID" gaius binary built and copied to: '"$BUILD_PATH"'"
