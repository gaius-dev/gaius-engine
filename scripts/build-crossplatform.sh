#!/bin/sh

if [ "$#" -ne "1" ]; then
	echo "Error: gaius build script requires [path] location which specifies where to deploy the gaius binary."
	echo "Usage: ./build-crossplatform.sh [path]"
	echo "Example: ./build-crossplatform.sh ~/path/to/mysite"
	exit 1
fi

BUILD_PATH="$1"

echo ""
echo "Building crossplatform gaius binaries for CI environments..."
echo ""

dotnet publish ./Gaius/Gaius.csproj -c Release --self-contained false -p:DebugType=None -o "$BUILD_PATH"/bin/gaius-ci

echo ""
echo "Crossplatform gaius binaries built and copied to: '"$BUILD_PATH"/bin/gaius-ci'"
