#!/bin/bash
# Script deploy Gaius engine binaries, CLI wrappers, and Github Actions to a specific location

function main()
{
	if [[ "$#" -ne "1" ]]; then
		echo "Error: missing target path"
		exit 1
	fi

	deploy-artifacts $1
}

function deploy-artifacts() {

	build-deploy-xplatform-engine-bin $1
	deploy-cli-wrapper $1
	deploy-github-actions $1
}

function build-deploy-xplatform-engine-bin() {

	if [[ -d $1/bin/gaius ]]; then
		echo "Deleting existing Gaius engine binaries in '$1/bin/gaius'..."
		rm -rf $1/bin/gaius
	fi

	mkdir -p $1/bin/gaius

	echo "Deploying Gaius engine binaries in '$1/bin/gaius'..."
	./build_xplatform.sh $1/bin/gaius
}

function deploy-cli-wrapper() {

	echo "Deploying CLI wrappers in '$1'..."
	cp ../CLI/* $1/
}

function deploy-github-actions() {

	if [[ -d $1/.github/workflows ]]; then
		echo "Deleting existing Github Actions in '$1/.github/workflows'..."
		rm -rf $1/.github/workflows
	fi

	mkdir -p $1/.github/workflows

	echo "Deploying Github Actions in '$1/.github/workflows'..."
	cp ../GithubActions/* $1/.github/workflows/
}

main "$@"