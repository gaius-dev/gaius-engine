#!/bin/bash

if [ "$#" -ne "1" ]; then
	echo "Error: target path required"
	exit 1
fi

RID=osx-x64

echo "Building $RID platform specific gaius engine binary..."
dotnet publish ../Gaius/Gaius.csproj -c Release -r $RID --self-contained false -p:PublishSingleFile=true -p:DebugType=None -o $1

echo "$RID gaius engine binary built and copied to: $1"
