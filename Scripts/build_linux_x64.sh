#!/bin/bash

if [ "$#" -ne "1" ]; then
	echo "Error: target path required"
	exit 1
fi

RID=linux-x64

echo "Building platform specific gaius binary for "$RID"..."
dotnet publish ../Gaius/Gaius.csproj -c Release -r $RID --self-contained false -p:PublishSingleFile=true -p:DebugType=None -o $1

echo ""$RID" gaius binary built and copied to: '$1'"
