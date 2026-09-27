#!/usr/bin/env bash
set -euo pipefail

REPO="jamescanady/ce-platform-symplr-cli"
INSTALL_DIR="/usr/local/bin"

# ─── version ─────────────────────────────────────────────────────────────────

TAG="${1:-}"
if [[ -z "$TAG" ]]; then
    TAG=$(curl -fsSLI -o /dev/null -w '%{url_effective}' \
        "https://github.com/${REPO}/releases/latest" | sed 's|.*/tag/||')
fi

# Normalise: ensure the tag has a 'v' prefix
[[ "$TAG" != v* ]] && TAG="v${TAG}"
VERSION="${TAG#v}"

# ─── platform detection ───────────────────────────────────────────────────────

OS=$(uname -s)
ARCH=$(uname -m)

case "$OS" in
    Linux*)  OS_KEY="linux" ;;
    Darwin*) OS_KEY="osx" ;;
    *)       echo "error: unsupported OS '${OS}'" >&2; exit 1 ;;
esac

case "$ARCH" in
    x86_64)        ARCH_KEY="x64" ;;
    aarch64|arm64) ARCH_KEY="arm64" ;;
    *)             echo "error: unsupported architecture '${ARCH}'" >&2; exit 1 ;;
esac

RID="${OS_KEY}-${ARCH_KEY}"

case "$RID" in
    linux-x64|osx-x64|osx-arm64) ;;
    *) echo "error: no release artifact available for '${RID}'" >&2; exit 1 ;;
esac

# ─── download & install ───────────────────────────────────────────────────────

FILENAME="symplr-${VERSION}-${RID}.tar.gz"
URL="https://github.com/${REPO}/releases/download/${TAG}/${FILENAME}"

echo "Installing symplr ${TAG} (${RID})..."

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

curl -fSL --progress-bar "$URL" -o "${TMP}/${FILENAME}"
tar xzf "${TMP}/${FILENAME}" -C "$TMP"

if [[ "$EUID" -ne 0 ]]; then
    sudo install -m 755 "${TMP}/symplr" "${INSTALL_DIR}/symplr"
else
    install -m 755 "${TMP}/symplr" "${INSTALL_DIR}/symplr"
fi

echo "Installed: ${INSTALL_DIR}/symplr"
