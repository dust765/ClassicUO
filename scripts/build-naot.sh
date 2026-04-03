#!/bin/bash

set -e

# Ensure vswhere.exe is in PATH (required for NativeAOT linker on Windows)
export PATH="$PATH:/c/Program Files (x86)/Microsoft Visual Studio/Installer"

# Define paths and project details
bootstrap_project="../src/ClassicUO.Bootstrap/src/ClassicUO.Bootstrap.csproj"
client_project="../src/ClassicUO.Client"
output_directory="../bin/dist"
target=""

# Determine the platform
platform=$(uname -s)

# Build for the appropriate platform
case $platform in
  Linux)
    # Add Linux-specific build commands here
    target="linux-x64"
    ;;
  Darwin)
    # Add macOS-specific build commands here
   target="osx-x64"
    ;;
  MINGW* | CYGWIN* | MSYS*)
    # Add Windows-specific build commands here
    target="win-x64"
    ;;
  *)
    echo "Unsupported platform: $platform"
    exit 1
    ;;
esac


dotnet publish "$bootstrap_project" -c Release -o "$output_directory"
dotnet publish "$client_project" -c Release -p:NativeLib=Shared -p:OutputType=Library -r $target -o "$output_directory"
