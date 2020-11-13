#!/bin/bash
# Bash script to update all nuget packages

function read_solution() {
    echo "Parsing solution $1"

    while IFS='' read -r line || [[ -n "$line" ]]; do
            if [[ $line =~ \"([^\"]*.csproj)\" ]]; then
                    project="${BASH_REMATCH[1]}"

                    read_project "$(echo "$project"|tr '\\' '/')"
            fi
    done < "$1"
}

function read_project() {
    echo "Parsing project $1"
    package_regex='PackageReference Include="([^"]*)" Version="([^"]*)"'

    while IFS='' read -r line || [[ -n "$line" ]]; do
            if [[ $line =~ $package_regex ]]; then
                    name="${BASH_REMATCH[1]}"
                    version="${BASH_REMATCH[2]}"

                    if [[ $version != *-* ]]; then
                            dotnet add "$1" package "$name"
                    fi
            fi
    done < $1
}

function dotnet_update_packages() {
    has_read=0

    if [[ $1 =~ \.sln$ ]]; then
            read_solution "$1"
            return 0
    elif [[ $1 =~ \.csproj$ ]]; then
            read_project "$1"
            return 0
    elif [[ $1 != "" ]]; then
            echo "Invalid file $1"
            return 1
    fi


    for solution in ./*.sln; do
            if [ ! -f ${solution} ]; then
                    continue
            fi

            read_solution "${solution}"
            has_read=1
    done

    if [[ $has_read -eq 1 ]]; then
            return 0
    fi

    for project in ./*.csproj; do
            if [ ! -f ${project} ]; then
                    continue
            fi

            read_project "${project}"
    done
}

dotnet_update_packages "$@"