#!/bin/bash

# ==============================================================================
# Launch Script for Mafia Communication Service
# ------------------------------------------------------------------------------
# This script builds and runs the .NET application.
# It should be executed from the root directory of the project.
# ==============================================================================

# Exit immediately if any command fails
set -e

# Define the relative path to your main project directory
PROJECT_DIR="MafiaCommunicationService"

# Check if the project directory exists
if [ ! -d "$PROJECT_DIR" ]; then
  echo "Error: Project directory '$PROJECT_DIR' not found."
  echo "Please run this script from the root of your solution."
  exit 1
fi

# Print a message to the console
echo "Launching Mafia Communication Service..."

# Navigate into the project directory, run the app, and then navigate back.
# The 'dotnet run' command will automatically restore, build, and run the project.
# All arguments passed to this script (e.g., --urls) are forwarded to 'dotnet run'.
(cd "$PROJECT_DIR" && dotnet run "$@")

echo "Application has been shut down."