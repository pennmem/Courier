#! /bin/bash

if [ $# -ne 1 ]; then
    echo "Wrong number of arguments (requires one)"
    echo "Please provide the path to the file that should be changed"
    echo "ex: ./fixBooleans.sh ~/Desktop/data/NiclsCourierClosedLoop/J/session_1/session.jsonl"
    exit
fi

if ! test -f "$1"; then
    echo "File does not exist. Please try again with a new file"
    exit
fi

sed -i '' 's/:true/:1/g' "$1" 
sed -i '' 's/:false/:0/g' "$1"

