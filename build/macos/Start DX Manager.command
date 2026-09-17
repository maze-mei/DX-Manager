#!/bin/bash

set -u

launcher_directory="$(cd "$(dirname "$0")" && pwd)"
application="$launcher_directory/DXManager.Mac"

cd "$launcher_directory" || exit 1
export PATH="$launcher_directory/tools/scrcpy:/usr/bin:/bin:/usr/sbin:/sbin"

if [[ ! -x "$application" ]]; then
    echo "DX Manager could not start because its executable is missing or is not executable."
    echo "Extract the complete ZIP again and keep every file in the same folder."
    read -r -p "Press Return to close this window. "
    exit 1
fi

"$application" "$@"
exit_code=$?

if [[ $exit_code -ne 0 ]]; then
    echo
    echo "DX Manager exited with status $exit_code."
    echo "Review the message above, then press Return to close this window."
    read -r
fi

exit "$exit_code"
