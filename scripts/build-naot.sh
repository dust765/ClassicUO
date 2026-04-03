#!/bin/bash

set -e

# Ensure vswhere.exe is in PATH (required for NativeAOT linker on Windows)
export PATH="$PATH:/c/Program Files (x86)/Microsoft Visual Studio/Installer"

# Define paths and project details
bootstrap_project="../src/ClassicUO.Bootstrap/src/ClassicUO.Bootstrap.csproj"
client_project="../src/ClassicUO.Client/ClassicUO.Client.csproj"
output_directory="../bin/dist"
target=""
tfm=""

# Determine the platform
platform=$(uname -s)

# Build for the appropriate platform
case $platform in
  Linux)
    target="linux-x64"
    tfm="net10.0"
    ;;
  Darwin)
    target="osx-x64"
    tfm="net10.0"
    ;;
  MINGW* | CYGWIN* | MSYS*)
    target="win-x64"
    tfm="net10.0-windows"
    ;;
  *)
    echo "Unsupported platform: $platform"
    exit 1
    ;;
esac


dotnet publish "$bootstrap_project" -c Release -o "$output_directory"
dotnet publish "$client_project" -c Release -f "$tfm" -r "$target" -p:NativeLib=Shared -p:OutputType=Library -o "$output_directory"
