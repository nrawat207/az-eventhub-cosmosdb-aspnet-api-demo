# Infrastructure

This folder contains modular Bicep for the dev environment.

The dev Azure Container Registry keeps public network access enabled so local builds and pipeline pushes can reach it without extra private networking. For production, disable ACR public network access and use private endpoints or trusted network paths for image pushes and pulls.
