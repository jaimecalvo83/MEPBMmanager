#!/bin/bash
set -e

echo "=== MEPBMmanager - Local Development Setup ==="

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Please start Docker Desktop first"
    exit 1
fi

# Start PostgreSQL
echo "Starting PostgreSQL..."
docker compose up -d postgres

# Wait for Postgres to be ready
echo "Waiting for PostgreSQL..."
sleep 5

# Restore NuGet packages
echo "Restoring backend packages..."
cd ../backend
dotnet restore MEPBMmanager.Api/MEPBMmanager.Api.csproj

echo ""
echo "=== Setup Complete ==="
echo ""
echo "The backend runs the EF migrations + seed automatically on startup."
echo ""
echo "Start development with:"
echo "  Terminal 1: cd ../backend && dotnet run --project MEPBMmanager.Api"
echo "  Terminal 2: cd ../frontend/packages/web && npm install && npm run dev"
echo ""
echo "Backend API:  http://localhost:5171"
echo "Frontend:     http://localhost:5173"
