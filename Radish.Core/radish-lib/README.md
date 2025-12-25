# Radish Native Extensions (Rust)

High-performance native extensions for the Radish project, providing optimized image processing and file operations.

## Features

- **Image Watermarking**: Add text watermarks to images with customizable position, size, and opacity
- **File Hashing**: Fast SHA256 hash calculation for file deduplication
- **Cross-Platform**: Supports Windows, Linux, and macOS

## Architecture

```
radish-lib/
├── src/
│   ├── lib.rs              # FFI exports and main entry point
│   ├── image/              # Image processing module
│   │   ├── mod.rs
│   │   └── watermark.rs    # Watermark functionality
│   ├── hash/               # Hashing module
│   │   ├── mod.rs
│   │   └── file_hash.rs    # SHA256 file hashing
│   └── utils/              # Utility functions
│       └── mod.rs
├── fonts/                  # Font files for watermarking
│   ├── README.md
│   └── DejaVuSans.ttf     # Required font file
├── Cargo.toml              # Rust project configuration
├── build.sh                # Linux/macOS build script
├── build.ps1               # Windows build script
└── README.md               # This file
```

## Prerequisites

### Linux / WSL (系统依赖)

在 Ubuntu/Debian（含 WSL Ubuntu）上构建需要系统级的 C 工具链与基础链接文件，否则会出现 `linker \`cc\` not found` 或找不到 `crt*.o` / `-lc` 等错误。

```bash
sudo apt-get update
sudo apt-get install -y build-essential clang pkg-config
```

- `build-essential`：提供 `cc/gcc`、`libc6-dev`（含 `crt*.o`）、`make` 等
- `clang`：可选，但建议安装以便需要时切换链接/编译器
- `pkg-config`：部分依赖在探测系统库时会用到

### Rust Toolchain

```bash
# Install Rust (if not already installed)
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# Verify installation
rustc --version
cargo --version
```

### Font File

You need to place `DejaVuSans.ttf` in the `fonts/` directory. See `fonts/README.md` for instructions.

## Building

### Linux / macOS

```bash
# Make the script executable
chmod +x build.sh

# Build the library
./build.sh
```

### Windows

```powershell
# Run the build script
.\build.ps1
```

### Manual Build

```bash
# Build in release mode
cargo build --release

# The compiled library will be in:
# - Linux: target/release/libradish_lib.so
# - macOS: target/release/libradish_lib.dylib
# - Windows: target/release/radish_lib.dll
```

## C# Integration

The compiled library is automatically copied to the Radish.Api output directory by the build scripts.

### Usage in C#

```csharp
using Radish.Infrastructure.ImageProcessing;

// Use the Rust image processor
var processor = new RustImageProcessor(options);

// Add watermark
var result = await processor.AddWatermarkAsync(
    inputPath: "input.jpg",
    text: "Radish",
    options: new WatermarkOptions
    {
        FontSize = 24,
        Opacity = 0.5f,
        Position = WatermarkPosition.BottomRight
    }
);
```

## API Reference

### add_text_watermark

Add text watermark to an image.

**Parameters:**
- `input_path` (string): Path to the input image
- `output_path` (string): Path to save the watermarked image
- `text` (string): Watermark text
- `font_size` (u32): Font size in pixels
- `opacity` (f32): Opacity (0.0 - 1.0)
- `position` (u8): Position (0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight, 4=Center)

**Returns:**
- `0`: Success
- `-1`: Error

### calculate_file_sha256

Calculate SHA256 hash of a file.

**Parameters:**
- `file_path` (string): Path to the file
- `hash_output` (char*): Buffer to store the hash (must be at least 65 bytes)
- `output_len` (usize): Length of the output buffer

**Returns:**
- `0`: Success
- `-1`: Error
- `-2`: Buffer too small

## Performance

Rust implementation provides significant performance improvements over C# for:
- Large image processing (>2MB)
- Batch operations
- CPU-intensive tasks (hashing, compression)

Benchmark results will be added after performance testing.

## Testing

```bash
# Run Rust tests
cargo test

# Run with output
cargo test -- --nocapture
```

## Troubleshooting

### Font Not Found Error

Make sure `DejaVuSans.ttf` is in the `fonts/` directory. See `fonts/README.md`.

### Library Not Found (C#)

Ensure the compiled library is in the same directory as `Radish.Api.dll`:
- Windows: `radish_lib.dll`
- Linux: `libradish_lib.so`
- macOS: `libradish_lib.dylib`

### Build Errors

```bash
# Clean and rebuild
cargo clean
cargo build --release
```

## License

Apache-2.0

## Contributing

This is part of the Radish project. See the main repository for contribution guidelines.
