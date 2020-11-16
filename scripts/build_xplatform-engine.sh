#!/bin/bash

if [ "$#" -ne "1" ]; then
	echo "Error: target path required"
	exit 1
fi

echo "Building xplatform Gaius engine binaries..."
dotnet publish ../src/Gaius/Gaius.csproj -c Release --self-contained false -p:DebugType=None -o $1

echo "Xplatform Gaius engine binaries built and copied to: $1"
