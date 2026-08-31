#!/bin/bash
set -e

echo "=== MEPBMmanager VPS Deployment ==="

# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker if missing
if ! command -v docker &> /dev/null; then
    echo "Installing Docker..."
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh
    sudo usermod -aG docker $USER
    rm -f get-docker.sh
fi

# Install Docker Compose plugin if missing
if ! docker compose version &> /dev/null; then
    echo "Installing Docker Compose plugin..."
    sudo apt install -y docker-compose-plugin
fi

# Create app directory
sudo mkdir -p /opt/mepbm
sudo chown $USER:$USER /opt/mepbm

# Copy the whole monorepo
echo "Copying project files..."
cp -r . /opt/mepbm/

cd /opt/mepbm/frontend

# Build and start services (postgres + backend .NET + web)
echo "Building and starting services..."
docker compose up -d --build

# Wait for PostgreSQL
echo "Waiting for PostgreSQL..."
sleep 10

# Apply EF Core migrations (creates the schema)
cd /opt/mepbm/backend
dotnet tool restore 2>/dev/null || true
dotnet ef database update --project MEPBMmanager.Infrastructure \
  --startup-project MEPBMmanager.Api 2>/dev/null || echo "⚠ Migrations will apply on first backend startup"

echo ""
echo "=== Deployment Complete ==="
echo "Frontend:    http://$(hostname -I | awk '{print $1}')"
echo "Backend API: http://$(hostname -I | awk '{print $1}'):5171"
echo ""
echo "To view logs: cd /opt/mepbm/frontend && docker compose logs -f"
echo "To stop:      cd /opt/mepbm/frontend && docker compose down"
