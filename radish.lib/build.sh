#!/bin/bash
# Build script for radish-lib (Linux/macOS)

set -e  # Exit on error

echo "========================================="
echo "Building radish-lib (Rust Native Extensions)"
echo "========================================="

# Get the script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Check if Rust is installed
if ! command -v cargo &> /dev/null; then
    echo "Error: Rust is not installed. Please install Rust first:"
    echo "  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh"
    exit 1
fi

# Check if font file exists
if [ ! -f "fonts/DejaVuSans.ttf" ]; then
    echo "Warning: Font file not found at fonts/DejaVuSans.ttf"
    echo "Watermark functionality may not work properly."
    echo "See fonts/README.md for instructions."
fi

# Build in release mode
echo ""
echo "Building in release mode..."
cargo build --release

# Check build result
if [ $? -eq 0 ]; then
    echo ""
    echo "✓ Build successful!"
    echo ""

    # Determine the library extension based on OS
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        LIB_EXT="so"
        LIB_NAME="libradish_lib.so"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        LIB_EXT="dylib"
        LIB_NAME="libradish_lib.dylib"
    else
        echo "Warning: Unknown OS type: $OSTYPE"
        LIB_EXT="so"
        LIB_NAME="libradish_lib.so"
    fi

    # Copy to Radish.Api output directory
    API_OUTPUT_DIR="./Radish.Api/bin/Debug/net10.0"

    if [ -d "$API_OUTPUT_DIR" ]; then
        echo "Copying $LIB_NAME to $API_OUTPUT_DIR..."
        cp "target/release/$LIB_NAME" "$API_OUTPUT_DIR/"
        echo "✓ Library copied successfully!"
    else
        echo "Warning: API output directory not found: $API_OUTPUT_DIR"
        echo "You may need to build Radish.Api first or copy the library manually."
    fi

    echo ""
    echo "Library location: target/release/$LIB_NAME"
    echo ""
    echo "========================================="
    echo "Build complete!"
    echo "========================================="
else
    echo ""
    echo "✗ Build failed!"
    exit 1
fi
