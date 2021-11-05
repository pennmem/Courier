#!/bin/bash

#set -o xtrace

if [[ $# -ne 2 ]]; then
    echo "Error: Folder path not provided"
    echo "Please use the script as follows"
    echo "./HelperScripts/moveToPsiturkServer.sh <rhino username> <relativePathToCopy>"
    echo "ex: ./HelperScripts/moveToPsiturkServer.sh jbruska Psiturk_Wrapper/static/js/Unity/build"
fi

currDir=${PWD##*/}
if [[ $currDir != "Courier" ]]; then
    echo "Error: Script executed from wrong directory"
    echo "Please run this script from the Courier_Online directory"
fi

destPath=$(echo $2 | grep -o ".*/")

copyToPsiturk="cd Courier_Online; scp -r $2 maint@psiturk.sas.upenn.edu:~/Courier_Online/$destPath"
sshToMaint="ssh maint@localhost"
copyToMaint="cd Courier_Online; scp -r $2 maint@localhost:~/Courier_Online/$destPath"
sshToRhino="ssh $1@rhino2.psych.upenn.edu"
copyToRhino="whoami; hostname"
copyToRhino="scp -r $2 $1@rhino2.psych.upenn.edu:~/Courier_Online/$destPath"


localToPsiturk="$copyToRhino; $sshToRhino \"$copyToMaint; $sshToMaint \\\"$copyToPsiturk\\\"\""
#echo $localToPsiturk
eval $localToPsiturk
