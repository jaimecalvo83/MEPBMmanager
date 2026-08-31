#!/bin/bash
set -e

echo "=== MEPBMmanager VPS Deployment ==="

# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
if ! command -v docker &> /dev/null; then
    echo "Installing Docker..."
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh
    sudo usermod -aG docker $USER
    rm get-docker.sh
fi

# Install Docker Compose
if ! command -v docker-compose &> /dev/null; then
    echo "Installing Docker Compose..."
    sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    sudo chmod +x /usr/local/bin/docker-compose
fi

# Create app directory
sudo mkdir -p /opt/mepbm
sudo chown $USER:$USER /opt/mepbm

# Copy project files
echo "Copying project files..."
cp -r . /opt/mepbm/

cd /opt/mepbm

# Create .env for production
cat > .env.production << EOF
DATABASE_URL=postgresql://postgres:mepbm_secret_password@postgres:5432/mepbm?schema=public
JWT_SECRET=$(openssl rand -hex 32)
REDIS_URL=redis://redis:6379
PORT=3001
EOF

# Build and start services
echo "Building and starting services..."
docker-compose -f docker-compose.yml --env-file .env.production up -d --build

# Wait for PostgreSQL
echo "Waiting for PostgreSQL..."
sleep 10

# Run database migrations
echo "Running database migrations..."
docker-compose exec server npx prisma db push

echo ""
echo "=== Deployment Complete ==="
echo "Frontend: http://$(hostname -I | awk '{print $1}')"
echo "Backend API: http://$(hostname -I | awk '{print $1}'):3001"
echo ""
echo "To view logs: docker-compose logs -f"
echo "To stop: docker-compose down"
