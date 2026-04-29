#!/usr/bin/env bash
set -ex
SCRIPT_DIR="$( cd -- "$( dirname -- "${BASH_SOURCE[0]:-$0}"; )" &> /dev/null && pwd 2> /dev/null; )";
pushd "$SCRIPT_DIR/source"
if git apply --reverse --check ../patches/fmp4-record-mode.patch >/dev/null 2>&1; then
    echo "WebUI fMP4 patch already applied"
else
    git apply ../patches/fmp4-record-mode.patch
fi
export BASE_URL="./"
export VITE_EMBEDDED_BUILD="true"
npm ci && npx vite build
rm -R ../../BililiveRecorder.Web/embeded/ui 2>/dev/null || true
cp -R dist ../../BililiveRecorder.Web/embeded/ui
popd
