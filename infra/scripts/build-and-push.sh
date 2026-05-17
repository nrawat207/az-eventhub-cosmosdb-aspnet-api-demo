#!/bin/bash

#
# build-and-push.sh
#
# Build Docker images and push them to Azure Container Registry (ACR).
# Designed for use in CI/CD pipelines or local development.
#
# Prerequisites:
#   - Docker installed and running
#   - Azure CLI installed and authenticated (az login)
#   - Access to the target ACR
#
# Usage:
#   ./build-and-push.sh --environment dev --environment dev --acr-name azlearnacrdev
#   ./build-and-push.sh --environment prod --acr-name prodacr --image-tag latest
#
# Environment Variables:
#   ACR_NAME       - Azure Container Registry name
#   ENVIRONMENT    - Deployment environment (dev, staging, prod)
#   IMAGE_TAG      - Docker image tag (default: environment name)
#

set -euo pipefail

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

print_section() {
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

# Default values
ENVIRONMENT="${ENVIRONMENT:-dev}"
ACR_NAME=""
IMAGE_TAG=""

# Parse command-line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --acr-name)
            ACR_NAME="$2"
            shift 2
            ;;
        --image-tag)
            IMAGE_TAG="$2"
            shift 2
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Validate required parameters
if [[ -z "$ACR_NAME" ]]; then
    print_error "ACR name is required. Use --acr-name <name> or set ACR_NAME environment variable"
    exit 1
fi

# Set image tag if not provided
if [[ -z "$IMAGE_TAG" ]]; then
    IMAGE_TAG="$ENVIRONMENT"
fi

print_section "Building and Pushing Docker Images to ACR"
print_info "Environment: $ENVIRONMENT"
print_info "ACR: $ACR_NAME"
print_info "Image Tag: $IMAGE_TAG"
echo ""

# Step 1: Verify Docker is running
print_section "Step 1: Verifying Docker"
if ! docker ps > /dev/null 2>&1; then
    print_error "Docker is not running. Please start Docker and try again."
    exit 1
fi
print_success "Docker is running"

# Step 2: Verify Azure CLI authentication
print_section "Step 2: Verifying Azure CLI Authentication"
if ! az account show > /dev/null 2>&1; then
    print_error "Not authenticated with Azure CLI. Run 'az login' first."
    exit 1
fi
CURRENT_SUBSCRIPTION=$(az account show --query id -o tsv)
print_success "Authenticated with subscription: $CURRENT_SUBSCRIPTION"

# Step 3: Get ACR login server
print_section "Step 3: Getting ACR Login Server"
ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer -o tsv 2>/dev/null || true)
if [[ -z "$ACR_LOGIN_SERVER" ]]; then
    print_error "Could not find ACR '$ACR_NAME'. Ensure it exists and you have access."
    exit 1
fi
print_success "ACR Login Server: $ACR_LOGIN_SERVER"

# Step 4: Login to ACR
print_section "Step 4: Logging in to ACR"
if az acr login --name "$ACR_NAME" > /dev/null 2>&1; then
    print_success "Successfully logged in to ACR"
else
    print_error "Failed to log in to ACR. Check your credentials and permissions."
    exit 1
fi

# Step 5: Build BatchProcessor.Api image
print_section "Step 5: Building BatchProcessor.Api Docker Image"
BATCH_PROCESSOR_IMAGE="$ACR_LOGIN_SERVER/batchprocessor-api:$IMAGE_TAG"
print_info "Image: $BATCH_PROCESSOR_IMAGE"

if docker build \
    -t "$BATCH_PROCESSOR_IMAGE" \
    --build-arg BUILDKIT_INLINE_CACHE=1 \
    -f "$PROJECT_ROOT/src/BatchProcessor.Api/Dockerfile" \
    "$PROJECT_ROOT"; then
    print_success "BatchProcessor.Api image built successfully"
else
    print_error "Failed to build BatchProcessor.Api image"
    exit 1
fi

# Step 6: Build ProgressReceiver.Api image
print_section "Step 6: Building ProgressReceiver.Api Docker Image"
PROGRESS_RECEIVER_IMAGE="$ACR_LOGIN_SERVER/progressreceiver-api:$IMAGE_TAG"
print_info "Image: $PROGRESS_RECEIVER_IMAGE"

if docker build \
    -t "$PROGRESS_RECEIVER_IMAGE" \
    --build-arg BUILDKIT_INLINE_CACHE=1 \
    -f "$PROJECT_ROOT/src/ProgressReceiver.Api/Dockerfile" \
    "$PROJECT_ROOT"; then
    print_success "ProgressReceiver.Api image built successfully"
else
    print_error "Failed to build ProgressReceiver.Api image"
    exit 1
fi

# Step 7: Push images to ACR
print_section "Step 7: Pushing Images to ACR"

print_info "Pushing $BATCH_PROCESSOR_IMAGE..."
if docker push "$BATCH_PROCESSOR_IMAGE"; then
    print_success "BatchProcessor.Api image pushed successfully"
else
    print_error "Failed to push BatchProcessor.Api image"
    exit 1
fi

print_info "Pushing $PROGRESS_RECEIVER_IMAGE..."
if docker push "$PROGRESS_RECEIVER_IMAGE"; then
    print_success "ProgressReceiver.Api image pushed successfully"
else
    print_error "Failed to push ProgressReceiver.Api image"
    exit 1
fi

# Step 8: Print summary
print_section "Build and Push Summary"
cat << EOF
✓ Successfully built and pushed Docker images!

Images in ACR:
  - $BATCH_PROCESSOR_IMAGE
  - $PROGRESS_RECEIVER_IMAGE

Next Steps:
  1. Deploy container apps (if not already deployed):
     az deployment group create \\
       --name "azlearn-deploy-apps-\\\$(date +%s)" \\
       --resource-group <resource-group-name> \\
       --template-file $PROJECT_ROOT/infra/main.bicep \\
       --parameters $PROJECT_ROOT/infra/parameters/$ENVIRONMENT.bicepparam \\
       --parameters deployContainerApps=true

  2. Or update existing container apps:
     az containerapp update --name azlearn-batchprocessor-$ENVIRONMENT \\
       --resource-group <resource-group-name> \\
       --image $BATCH_PROCESSOR_IMAGE
     az containerapp update --name azlearn-progressreceiver-$ENVIRONMENT \\
       --resource-group <resource-group-name> \\
       --image $PROGRESS_RECEIVER_IMAGE

EOF

print_success "Build and push completed successfully!"
