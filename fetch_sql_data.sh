#!/bin/bash
#
# Fetch experiment data from the psiTurk server to this machine.
#
# Standalone: copy it anywhere (e.g. ~/bin/fetch_sql_data) and run it from any directory. It
# does not read anything from a repo. It calls ~/fetch_sql_data.py on the server, has it write
# to a per-run directory there, and rsyncs that directory back.
#
# Works for any experiment the remote script knows about -- run --list to see them.
#
# Usage:
#   ./fetch_sql_data.sh --list                     # what's available; fetches nothing
#   ./fetch_sql_data.sh VCOnline
#   ./fetch_sql_data.sh dirFRU
#   ./fetch_sql_data.sh dirFR --dry-run            # report only, copy nothing back
#   ./fetch_sql_data.sh VCOnline --min-id 1 --max-id 99
#   ./fetch_sql_data.sh some_table                 # unknown names are read as table names
#
# Extra arguments pass straight through to the remote fetch_sql_data.py.
#
# Config via environment:
#   PSITURK_HOST           ssh target   (default maint@cmlpsiturk.compmemlab.org)
#   PSITURK_DATA_DIR       local output (default ~/psiturk_data)
#   PSITURK_REMOTE_SCRIPT  remote path  (default ~/fetch_sql_data.py)
#   PSITURK_KEEP_REMOTE=1  keep the per-run directory on the server (default: delete it)

set -euo pipefail

HOST="${PSITURK_HOST:-maint@cmlpsiturk.compmemlab.org}"
LOCAL_ROOT="${PSITURK_DATA_DIR:-$HOME/psiturk_data}"
REMOTE_SCRIPT="${PSITURK_REMOTE_SCRIPT:-~/fetch_sql_data.py}"

usage() {
    sed -n '3,26p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

if [ $# -eq 0 ]; then
    usage >&2
    echo "error: give an experiment name, or --list to see what's available." >&2
    exit 1
fi

# Quote arguments for the remote shell so values with spaces survive the ssh round trip.
quote_remote() {
    local out="" sep=""
    for a in "$@"; do out="${out}${sep}$(printf '%q' "$a")"; sep=" "; done
    printf '%s' "$out"
}

# Reuse one authenticated connection for the run and the download, so a password is asked at
# most once. The socket path must stay short: a Unix domain socket is capped near 104 bytes,
# and macOS $TMPDIR alone (/var/folders/../T/) plus ssh's random suffix blows past that. %C is
# a short hash of the connection parameters, which keeps this well inside the limit.
CTRL_DIR="$HOME/.ssh"
mkdir -p "$CTRL_DIR" 2>/dev/null || CTRL_DIR="/tmp"
CTRL_PATH="$CTRL_DIR/cm-%C"

SSH_OPTS=(-o ConnectTimeout=15)
MUX_OPTS=(-o ControlMaster=auto -o ControlPath="$CTRL_PATH" -o ControlPersist=120)
RSYNC_SSH="ssh"
MUX=0

cleanup() {
    [ "$MUX" = "1" ] && ssh "${SSH_OPTS[@]}" -O exit "$HOST" 2>/dev/null
    return 0
}
trap cleanup EXIT

echo "Connecting to ${HOST}..."
# Multiplexing is only an optimisation, so fall back to plain ssh rather than failing if the
# control socket can't be set up.
if ssh "${MUX_OPTS[@]}" "${SSH_OPTS[@]}" "$HOST" true 2>/dev/null; then
    SSH_OPTS=("${MUX_OPTS[@]}" "${SSH_OPTS[@]}")
    RSYNC_SSH="ssh -o ControlPath=$CTRL_PATH"
    MUX=1
elif ssh "${SSH_OPTS[@]}" "$HOST" true; then
    echo "note: connection multiplexing unavailable; you may be prompted to authenticate" >&2
    echo "      more than once." >&2
else
    echo "error: could not connect to ${HOST}." >&2
    echo "       Set PSITURK_HOST if your ssh target differs (e.g. an alias from ~/.ssh/config)." >&2
    exit 1
fi

if ! ssh "${SSH_OPTS[@]}" "$HOST" "test -f ${REMOTE_SCRIPT}"; then
    echo "error: ${REMOTE_SCRIPT} not found on ${HOST}." >&2
    echo "       Upload it, or set PSITURK_REMOTE_SCRIPT to its path." >&2
    exit 1
fi

# --list / --help just print; there is nothing to fetch or copy back.
case "$1" in
    --list|--help|-h)
        ssh "${SSH_OPTS[@]}" "$HOST" "python3 ${REMOTE_SCRIPT} $1"
        exit 0
        ;;
esac

EXPERIMENT="$1"
shift
EXTRA_ARGS=("$@")

# This script owns --out, since it has to know what to rsync back.
for arg in ${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"}; do
    case "$arg" in
        --out|--out=*)
            echo "error: --out is managed by this script; use PSITURK_DATA_DIR to choose the" >&2
            echo "       local destination instead." >&2
            exit 1
            ;;
    esac
done

# --dry-run writes no files, so run it and stop rather than rsyncing an empty directory.
for arg in ${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"}; do
    if [ "$arg" = "--dry-run" ]; then
        echo "Running '${EXPERIMENT}' (dry run)..."
        ssh "${SSH_OPTS[@]}" "$HOST" \
            "python3 ${REMOTE_SCRIPT} $(quote_remote "$EXPERIMENT" ${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"})"
        exit 0
    fi
done

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RUN_NAME="${EXPERIMENT}_${TIMESTAMP}"
# A per-run remote directory keeps the rsync source pinned to exactly what this invocation
# produced, rather than a shared path that another run could have overwritten.
REMOTE_DIR="psiturk_fetch/${RUN_NAME}"
LOCAL_DIR="${LOCAL_ROOT}/${RUN_NAME}"

echo "Rebuilding '${EXPERIMENT}' on ${HOST}..."
ssh "${SSH_OPTS[@]}" "$HOST" \
    "python3 ${REMOTE_SCRIPT} $(quote_remote "$EXPERIMENT") --out $(quote_remote "$REMOTE_DIR") $(quote_remote ${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"})"

echo "Copying results down..."
mkdir -p "$LOCAL_DIR"
# Plain -az only: macOS ships openrsync / rsync 2.6.9, which lack --info (rsync 3.1+). The
# summary below is produced locally instead, so this stays portable.
if ! rsync -az -e "$RSYNC_SSH" "${HOST}:${REMOTE_DIR}/" "${LOCAL_DIR}/"; then
    echo "error: rsync failed. The data is still on the server at ${REMOTE_DIR}" >&2
    echo "       (not deleted), so you can retry or copy it by hand." >&2
    rmdir "$LOCAL_DIR" 2>/dev/null || true
    exit 1
fi

if [ -z "$(ls -A "$LOCAL_DIR" 2>/dev/null)" ]; then
    echo "warning: no files came back -- the query matched no rows." >&2
    rmdir "$LOCAL_DIR" 2>/dev/null || true
    exit 0
fi

if [ "${PSITURK_KEEP_REMOTE:-0}" != "1" ]; then
    ssh "${SSH_OPTS[@]}" "$HOST" "rm -rf $(quote_remote "$REMOTE_DIR")"
fi

FILE_COUNT=$(find "$LOCAL_DIR" -type f | wc -l | tr -d ' ')
echo
echo "Saved ${FILE_COUNT} file(s) to: ${LOCAL_DIR}"
find "$LOCAL_DIR" -type f -exec ls -lh {} \; 2>/dev/null \
    | awk -v root="${LOCAL_DIR}/" '{p=$NF; sub(root,"",p); print "  " $5 "\t" p}' | head -20
if [ "$FILE_COUNT" -gt 20 ]; then
    echo "  ... and $((FILE_COUNT - 20)) more"
fi
