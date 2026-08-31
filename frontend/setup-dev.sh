#!/bin/bash
set -e

echo "=== MEPBMmanager - Local Development Setup ==="

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Please start Docker Desktop first"
    exit 1
fi

# Start PostgreSQL and Redis
echo "Starting PostgreSQL and Redis..."
docker-compose up -d postgres redis

# Wait for services
echo "Waiting for services to be ready..."
sleep 5

# Install dependencies
echo "Installing dependencies..."
npm install

# Generate Prisma client
echo "Generating Prisma client..."
npm run db:generate

# Push database schema
echo "Pushing database schema..."
npm run db:push

echo ""
echo "=== Setup Complete ==="
echo ""
echo "Start development with:"
echo "  Terminal 1: cd packages/server && npm run dev"
echo "  Terminal 2: cd packages/web && npm run dev"
echo ""
echo "Server: http://localhost:3001"
echo "Frontend: http://localhost:5173"
