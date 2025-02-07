#!/bin/bash
# SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

#exit when any command fails
set -e
LAUNCHER_PATH=$RELEASE_DIRECTORY/launcher
APP_NAME=Nethermind.Launcher

echo =======================================================
echo Building Nethermind Launcher
echo =======================================================

cd $LAUNCHER_PATH
npm i
pkg index.js -t latest-linux-x64 -o $APP_NAME && mv $APP_NAME $RELEASE_DIRECTORY/$LIN_RELEASE
pkg index.js -t latest-macos-x64 -o $APP_NAME && mv $APP_NAME $RELEASE_DIRECTORY/$OSX_RELEASE && cp $RELEASE_DIRECTORY/$OSX_RELEASE/$APP_NAME $RELEASE_DIRECTORY/$OSX_ARM64_RELEASE/$APP_NAME
pkg index.js -t latest-win-x64 -o $APP_NAME.exe && mv $APP_NAME.exe $RELEASE_DIRECTORY/$WIN_RELEASE

echo =======================================================
echo Building Nethermind Launcher completed
echo =======================================================
