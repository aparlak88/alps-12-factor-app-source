#!/usr/bin/env bash
# ==============================================================================
# 12-Factor Principle: Factor V — Build, Release, Run
# Strictly separate build and run stages.
#
# Pipeline Formula:
#   Release = Build(Commit SHA) + Config(Environment)
#
# Stage Breakdown Implemented by This Script:
#   1. BUILD Stage (skipped when --render-only is supplied):
#      Compiles the exact checked-out Git revision (`GIT_SHA`) inside isolated
#      multi-stage Alpine SDK containers (`--locked-mode`), stamping
#      `--build-arg SOURCE_REVISION_ID="${GIT_SHA}"` into every assembly and
#      producing SDK-free runtime container images tagged with `:${GIT_SHA}`
#      and `:${BOOKSTORE_RELEASE_ID}`.
#   2. RELEASE Stage:
#      Pairs those immutable build images with the Kustomize environment
#      configuration under `deploy/k8s/base`, stamping a unique, immutable
#      `BOOKSTORE_RELEASE_ID` (`<yyyyMMdd.HHmm>-<shortSha>` or semantic
#      `vX.Y.Z-<shortSha>`). Kustomize generates a content-hashed ConfigMap
#      (`bookstore-release-metadata-<hash>`) and rewrites workload image tags
#      without touching tracked Git files.
#   3. RUN & Zero-Rebuild Rollback Mechanics:
#      Applying the rendered manifest (`./scripts/release.sh | kubectl apply -f -`)
#      launches the Run stage. Because builds and release manifests are
#      append-only and immutable, rolling back a faulty deployment NEVER runs
#      `dotnet build` again—an operator simply re-renders or re-applies a known
#      good `BOOKSTORE_RELEASE_ID`:
#        BOOKSTORE_RELEASE_ID="20261025.1200-a1b2c3d" ./scripts/release.sh --render-only | kubectl apply -f -
# ==============================================================================
set -euo pipefail

RENDER_ONLY=false
OUTPUT_PATH=""

usage() {
  cat >&2 <<'EOF'
Usage: ./scripts/release.sh [--render-only] [--output <manifest.yaml>]

Options:
  --render-only          Render the immutable Kustomize release manifest without running docker build.
  -o, --output <path>    Write the rendered Kubernetes release manifest to <path> instead of stdout.
  -h, --help             Show this help message.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --render-only)
      RENDER_ONLY=true
      shift
      ;;
    -o|--output)
      if [[ $# -lt 2 ]]; then
        echo "ERROR: $1 requires a file path argument." >&2
        exit 1
      fi
      OUTPUT_PATH="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "ERROR: Unknown argument '$1'." >&2
      usage
      exit 1
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

GIT_SHA="$(git rev-parse --short HEAD)"
BOOKSTORE_RELEASE_ID="${BOOKSTORE_RELEASE_ID:-$(date -u +%Y%m%d.%H%M)-$(git rev-parse --short HEAD)}"

# Enforce Factor V immutability: reject mutable tags like 'latest', 'dev-unreleased',
# whitespace, or identifiers missing a 7..40 character hex commit SHA suffix.
RELEASE_ID_REGEX='^([0-9]{8}\.[0-9]{4}|v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?)-[0-9a-fA-F]{7,40}$'
if [[ ! "${BOOKSTORE_RELEASE_ID}" =~ ${RELEASE_ID_REGEX} ]]; then
  echo "ERROR: Invalid BOOKSTORE_RELEASE_ID '${BOOKSTORE_RELEASE_ID}'. Factor V requires an immutable '<yyyyMMdd.HHmm>-<hexSha>' or 'v<Major>.<Minor>.<Patch>-<hexSha>' identifier (never mutable tags like 'latest' or 'dev-unreleased')." >&2
  exit 1
fi

SERVICES=(
  "catalog-api:src/Catalog.Api/Dockerfile"
  "ordering-api:src/Ordering.Api/Dockerfile"
  "fulfillment-worker:src/Fulfillment.Worker/Dockerfile"
)

if [[ "${RENDER_ONLY}" == "false" ]]; then
  echo "[Factor V :: BUILD] Building immutable workload images for commit ${GIT_SHA} (release ${BOOKSTORE_RELEASE_ID})..." >&2
  for entry in "${SERVICES[@]}"; do
    service_name="${entry%%:*}"
    dockerfile_rel="${entry#*:}"
    image_repo="bookstore/${service_name}"

    echo "[Factor V :: BUILD] -> ${image_repo}:${GIT_SHA} (${dockerfile_rel})" >&2
    docker build \
      -f "${REPO_ROOT}/${dockerfile_rel}" \
      --build-arg SOURCE_REVISION_ID="${GIT_SHA}" \
      -t "${image_repo}:${GIT_SHA}" \
      -t "${image_repo}:${BOOKSTORE_RELEASE_ID}" \
      "${REPO_ROOT}" >&2
  done
fi

# Assemble an ephemeral Kustomize release overlay in a temporary directory so
# tracked files under deploy/k8s/base remain completely untouched in Git.
WORK_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

cp -R "${REPO_ROOT}/deploy/k8s/base" "${WORK_DIR}/base"
mkdir -p "${WORK_DIR}/release"

cat > "${WORK_DIR}/release/kustomization.yaml" <<EOF
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: twelve-factor-bookstore

resources:
  - ../base

configMapGenerator:
  - name: bookstore-release-metadata
    behavior: replace
    options:
      annotations:
        twelve-factor.io/release-id: "${BOOKSTORE_RELEASE_ID}"
        twelve-factor.io/source-revision-id: "${GIT_SHA}"
    literals:
      - BOOKSTORE_RELEASE_ID=${BOOKSTORE_RELEASE_ID}

images:
  - name: bookstore/catalog-api
    newTag: "${BOOKSTORE_RELEASE_ID}"
  - name: bookstore/ordering-api
    newTag: "${BOOKSTORE_RELEASE_ID}"
  - name: bookstore/fulfillment-worker
    newTag: "${BOOKSTORE_RELEASE_ID}"
EOF

echo "[Factor V :: RELEASE] Stamping immutable release ${BOOKSTORE_RELEASE_ID} (commit ${GIT_SHA})..." >&2

if [[ -n "${OUTPUT_PATH}" ]]; then
  mkdir -p "$(dirname "${OUTPUT_PATH}")"
  kubectl kustomize "${WORK_DIR}/release" > "${OUTPUT_PATH}"
  echo "[Factor V :: RELEASE] Wrote immutable release manifest to ${OUTPUT_PATH}" >&2
else
  kubectl kustomize "${WORK_DIR}/release"
fi
