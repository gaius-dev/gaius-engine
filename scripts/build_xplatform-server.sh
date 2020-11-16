#!/bin/bash

if [ "$#" -ne "1" ]; then
	echo "Error: target path required"
	exit 1
fi

echo "Building xplatform Gaius server binaries..."
dotnet publish ../src/GaiusServer/GaiusServer.csproj -c Release --self-contained false -p:DebugType=None -o $1

echo "Xplatform Gaius server binaries built and copied to: $1"
