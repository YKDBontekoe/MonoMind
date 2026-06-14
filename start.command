#!/bin/bash
# Move to the directory where this script is located
cd "$(dirname "$0")"

clear
echo "========================================="
echo "   Building and Launching Autonocraft    "
echo "========================================="
echo ""

# Run the game using dotnet run
export DYLD_LIBRARY_PATH=/opt/homebrew/lib:$DYLD_LIBRARY_PATH
dotnet run --project src/Autonocraft "$@"

# Pause terminal output on exit so the user can read any logs/errors if it closes
echo ""
echo "Press any key to exit..."
read -n 1
