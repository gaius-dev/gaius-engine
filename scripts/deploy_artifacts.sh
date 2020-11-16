#!/bin/bash
# Script deploy Gaius engine binaries, Gaius server binaries, CLI wrappers, and Github Actions to a specific location

function main()
{
	if [[ "$#" -ne "1" ]]; then
		echo "Error: missing target path"
		exit 1
	fi

	deploy-artifacts $1
}

function deploy-artifacts() {

	build-deploy-engine-bin $1
	build-deploy-server-bin $1
	deploy-cli-wrapper $1
	deploy-github-actions $1
}

function build-deploy-engine-bin() {

	if [[ -d $1/bin/gaius ]]; then
		echo "Deleting existing Gaius engine binaries in '$1/bin/gaius'..."
		rm -rf $1/bin/gaius
	fi

	mkdir -p $1/bin/gaius

	echo "Deploying Gaius engine binaries in '$1/bin/gaius'..."
	./build_xplatform-engine.sh $1/bin/gaius
}

function build-deploy-server-bin() {

	if [[ -d $1/bin/gaius-server ]]; then
		echo "Deleting existing Gaius server binaries in '$1/bin/gaius-server'..."
		rm -rf $1/bin/gaius-server
	fi

	mkdir -p $1/bin/gaius-server

	echo "Deploying Gaius server binaries in '$1/bin/gaius-server'..."
	./build_xplatform-server.sh $1/bin/gaius-server
}

function deploy-cli-wrapper() {

	echo "Deploying CLI wrappers in '$1'..."
	cp ../cli/* $1/
}

function deploy-github-actions() {

	if [[ -d $1/.github/workflows ]]; then
		echo "Deleting existing Github Actions in '$1/.github/workflows'..."
		rm -rf $1/.github/workflows
	fi

	mkdir -p $1/.github/workflows

	echo "Deploying Github Actions in '$1/.github/workflows'..."
	cp ../github-actions/* $1/.github/workflows/
}

main "$@"