$ErrorActionPreference = "Stop"
node (Join-Path $PSScriptRoot "../../Scripts/build-native-images.mjs") @args
exit $LASTEXITCODE
