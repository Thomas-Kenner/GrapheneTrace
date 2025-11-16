#!/bin/bash
# GrapheneTrace Database Initialization Script
# Author: SID:2412494
#
# This script:
# 1. Starts docker-compose if not already running
# 2. Waits for PostgreSQL to be healthy
# 3. Applies EF Core migrations in order
#
# Usage: ./scripts/init-db.sh

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get the repository root directory (parent of scripts directory)
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_PROJECT="$REPO_ROOT/web-implementation"

echo -e "${BLUE}==============================================\n${NC}"
echo -e "${BLUE}GrapheneTrace Database Initialization${NC}"
echo -e "${BLUE}==============================================\n${NC}"

# Change to repo root for docker-compose commands
cd "$REPO_ROOT"

# Check if docker-compose is installed
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo -e "${RED}Error: docker-compose is not installed${NC}"
    exit 1
fi

# Determine which docker compose command to use
if docker compose version &> /dev/null 2>&1; then
    DOCKER_COMPOSE="docker compose"
else
    DOCKER_COMPOSE="docker-compose"
fi

echo -e "${YELLOW}[1/4] Checking docker-compose status...${NC}"

# Check if postgres container is running
if $DOCKER_COMPOSE ps postgres | grep -q "Up"; then
    echo -e "${GREEN}✓ PostgreSQL container is already running${NC}"
else
    echo -e "${YELLOW}Starting docker-compose services...${NC}"
    $DOCKER_COMPOSE up -d
    echo -e "${GREEN}✓ Docker-compose services started${NC}"
fi

echo -e "\n${YELLOW}[2/4] Waiting for PostgreSQL to be healthy...${NC}"

# Wait for PostgreSQL healthcheck to pass
MAX_RETRIES=30
RETRY_COUNT=0
SLEEP_INTERVAL=2

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    # Check container health status
    HEALTH_STATUS=$($DOCKER_COMPOSE ps postgres --format json | grep -o '"Health":"[^"]*"' | cut -d'"' -f4 || echo "")

    if [ "$HEALTH_STATUS" = "healthy" ]; then
        echo -e "${GREEN}✓ PostgreSQL is healthy and ready${NC}"
        break
    fi

    # Also check if container is just running without healthcheck
    if $DOCKER_COMPOSE exec -T postgres pg_isready -U graphene_user -d graphenetrace &> /dev/null; then
        echo -e "${GREEN}✓ PostgreSQL is ready${NC}"
        break
    fi

    RETRY_COUNT=$((RETRY_COUNT + 1))
    echo -e "${YELLOW}  Waiting for PostgreSQL to be ready... (attempt $RETRY_COUNT/$MAX_RETRIES)${NC}"
    sleep $SLEEP_INTERVAL
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo -e "${RED}Error: PostgreSQL did not become healthy within expected time${NC}"
    echo -e "${YELLOW}You can check the logs with: $DOCKER_COMPOSE logs postgres${NC}"
    exit 1
fi

echo -e "\n${YELLOW}[3/4] Checking for dotnet-ef tool...${NC}"

# Change to web project directory
cd "$WEB_PROJECT"

# Check if dotnet-ef is available
if ! dotnet ef --version &> /dev/null; then
    echo -e "${YELLOW}dotnet-ef not found, attempting to restore local tools...${NC}"

    # Try to restore local tools if .config/dotnet-tools.json exists
    if [ -f ".config/dotnet-tools.json" ]; then
        dotnet tool restore
        echo -e "${GREEN}✓ Local tools restored${NC}"
    else
        echo -e "${RED}Error: dotnet-ef is not installed${NC}"
        echo -e "${YELLOW}Please install it with: dotnet tool install --global dotnet-ef${NC}"
        exit 1
    fi
fi

EF_VERSION=$(dotnet ef --version)
echo -e "${GREEN}✓ dotnet-ef found: $EF_VERSION${NC}"

echo -e "\n${YELLOW}[4/4] Applying EF Core migrations...${NC}"

# Apply migrations
dotnet ef database update

if [ $? -eq 0 ]; then
    echo -e "\n${GREEN}✓ Migrations applied successfully${NC}"
else
    echo -e "\n${RED}Error: Failed to apply migrations${NC}"
    exit 1
fi

echo -e "\n${BLUE}==============================================\n${NC}"
echo -e "${GREEN}Database initialization complete!${NC}"
echo -e "${BLUE}==============================================\n${NC}"
echo -e "${YELLOW}Database connection details:${NC}"
echo -e "  Host: localhost"
echo -e "  Port: 5432"
echo -e "  Database: graphenetrace"
echo -e "  User: graphene_user"
echo -e "\n${YELLOW}Useful commands:${NC}"
echo -e "  View logs: $DOCKER_COMPOSE logs postgres"
echo -e "  Stop database: $DOCKER_COMPOSE down"
echo -e "  Stop and wipe: $DOCKER_COMPOSE down -v"
echo -e "  Connect to DB: $DOCKER_COMPOSE exec postgres psql -U graphene_user -d graphenetrace"
echo -e "${BLUE}==============================================\n${NC}"
