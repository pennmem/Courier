#!/bin/bash
# Rebuild Courier Online data on cmlpsiturk and copy it down to a local folder.
# Usage: ./fetch_courier.sh [table]
#   ./fetch_courier.sh            -> fetches the "vconline" table
#   ./fetch_courier.sh vconline   -> same, explicit
#
# Prereq: ~/fetch_courier.py must exist on cmlpsiturk (scp it there once), and an
# `ssh cmlpsiturk` alias must reach the server (via the rhino2 hop in ~/.ssh/config).

set -e  # stop on any error

TABLE="${1:-vconline}"
LOCAL_DIR=~/Documents/Courier\ Online/courier_data
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")

mkdir -p "$LOCAL_DIR"

echo "Rebuilding Courier data on cmlpsiturk for table '$TABLE'..."
ssh cmlpsiturk "python3 ~/fetch_courier.py $TABLE"

echo "Copying reconstructed files to local..."
rsync -avz "cmlpsiturk:courier_data/data/" "${LOCAL_DIR}/data_${TIMESTAMP}/"

echo "Done. Saved to ${LOCAL_DIR}/data_${TIMESTAMP}/"
