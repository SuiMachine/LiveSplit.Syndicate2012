#!/bin/sh

VERSION=$1
if [ -z "$VERSION" ]; then
    echo "Must specify version."
    exit 1
fi

zip LiveSplit.DXHR_v${VERSION}.zip LiveSplit.DXHR.dll ../readme.txt
