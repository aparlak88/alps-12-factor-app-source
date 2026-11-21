# syntax=docker/dockerfile:1
# ==============================================================================
# 12-Factor Principle: Factor V — Build, Release, Run
# Workload: Catalog.Api
# Build Context: Repository root (.)
#   docker build -f src/Catalog.Api/Dockerfile \
#     --build-arg SOURCE_REVISION_ID="$(git rev-parse --short HEAD)" \
#     -t bookstore/catalog-api:local .
#
# Architectural Separation of Stages:
#   1. BUILD stage (SDK container): Restores pinned NuGet dependencies in
#      --locked-mode (Factor II) and compiles C# source code into IL assemblies.
#   2. PUBLISH stage (SDK container): Stamps the exact Git commit SHA into the
#      compiled assembly metadata (SourceRevisionId) and emits a self-consistent
#      deployment bundle without native host-specific apphost wrappers.
#   3. RUNTIME stage (ASP.NET runtime-only container): Contains ONLY the minimal
#      CLR/ASP.NET runtime and immutable published binaries, running under the
#      unprivileged $APP_UID account. No compiler or SDK exists at runtime,
#      making in-place code mutation structurally impossible.
# ==============================================================================

# ------------------------------------------------------------------------------
# Stage 1: Build — Deterministic dependency restore and compilation
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy centralized MSBuild and NuGet governance manifests first to maximize
# Docker layer cache reuse when only application C# files change.
COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/Bookstore.Contracts/Bookstore.Contracts.csproj src/Bookstore.Contracts/packages.lock.json ./src/Bookstore.Contracts/
COPY src/Bookstore.ServiceDefaults/Bookstore.ServiceDefaults.csproj src/Bookstore.ServiceDefaults/packages.lock.json ./src/Bookstore.ServiceDefaults/
COPY src/Catalog.Api/Catalog.Api.csproj src/Catalog.Api/packages.lock.json ./src/Catalog.Api/

# Enforce Factor II & Factor V repeatability: fail immediately if any NuGet
# transitive dependency drifts from its committed packages.lock.json graph.
RUN dotnet restore ./src/Catalog.Api/Catalog.Api.csproj --locked-mode

# Copy shared and workload source trees and compile in Release configuration.
COPY src/Bookstore.Contracts/ ./src/Bookstore.Contracts/
COPY src/Bookstore.ServiceDefaults/ ./src/Bookstore.ServiceDefaults/
COPY src/Catalog.Api/ ./src/Catalog.Api/

ARG SOURCE_REVISION_ID=unknown-revision
RUN dotnet build ./src/Catalog.Api/Catalog.Api.csproj \
    -c Release \
    --no-restore \
    /p:UseAppHost=false \
    /p:SourceRevisionId=${SOURCE_REVISION_ID}

# ------------------------------------------------------------------------------
# Stage 2: Publish — Stamp build provenance (SourceRevisionId) into bundle
# ------------------------------------------------------------------------------
FROM build AS publish
ARG SOURCE_REVISION_ID=unknown-revision
RUN dotnet publish ./src/Catalog.Api/Catalog.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:SourceRevisionId=${SOURCE_REVISION_ID}

# ------------------------------------------------------------------------------
# Stage 3: Runtime — Immutable, compiler-free execution image
# Why no compiler/SDK tools ship in the runtime stage:
# Factor V dictates that code can never be mutated in the Run stage because
# runtime edits cannot propagate back to the Build stage and would be wiped on
# the next pod reschedule or horizontal scale-out. Shipping only the minimal
# ASP.NET Core runtime image and executing as non-root ($APP_UID) guarantees
# immutable release artifacts produced by scripts/release.sh.
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Run as the built-in non-root container user provided by the Alpine base image.
USER $APP_UID

# Copy only the immutable published artifact from the publish stage.
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
