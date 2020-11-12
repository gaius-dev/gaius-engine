#!/bin/bash

if [ "$#" -ne "1" ]; then
	echo "Error: target path required"
	exit 1
fi

echo "Building xplatform gaius engine binaries..."
dotnet publish ../Gaius/Gaius.csproj -c Release --self-contained false -p:DebugType=None -o $1

echo "Xplatform gaius engine binaries built and copied to: $1"
