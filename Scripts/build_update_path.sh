#!/bin/bash

function main()
{
	if [[ "$#" -ne "1" ]]; then
		echo "Error: missing target path"
		exit 1
	fi

	build-update-path $1
}

function build-update-path() {

	build-update-xplatform-engine-bin $1
	update-cli-wrapper $1
	update-github-actions $1
}

function build-update-xplatform-engine-bin() {

	if [[ -d $1/bin/gaius ]]; then
		echo "Deleting existing Gaius engine binaries in $1/bin/gaius ..."
		rm -rf $1/bin/gaius
	fi

	mkdir -p $1/bin/gaius

	echo "Updating Gaius engine binaries, CLI wrapper, and Github Actions in $1/bin/gaius..."
	./build_xplatform.sh $1/bin/gaius
}

function update-cli-wrapper() {

	echo "Updating CLI wrapper..."
	cp ../CLI/* $1/
}

function update-github-actions() {

	if [[ -d $1/.github/workflows ]]; then
		echo "Deleting existing Github Actions in $1/.github/workflows..."
		rm -rf $1/.github/workflows
	fi

	mkdir -p $1/.github/workflows

	echo "Updating Github Actions..."
	cp ../GithubActions/* $1/.github/workflows/
}

main "$@"