# Fonts Directory

This directory should contain the DejaVu Sans font file for watermark text rendering.

## Required Font

- **Font Name**: DejaVu Sans
- **File Name**: `DejaVuSans.ttf`
- **License**: Free (Bitstream Vera License)

## How to Get the Font

### Option 1: Download from Official Source

```bash
# Download DejaVu fonts
wget https://github.com/dejavu-fonts/dejavu-fonts/releases/download/version_2_37/dejavu-fonts-ttf-2.37.tar.bz2

# Extract
tar -xjf dejavu-fonts-ttf-2.37.tar.bz2

# Copy the font file
cp dejavu-fonts-ttf-2.37/ttf/DejaVuSans.ttf ./DejaVuSans.ttf
```

### Option 2: Use System Font (Linux)

```bash
# On Ubuntu/Debian
cp /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf ./DejaVuSans.ttf

# On Fedora/RHEL
cp /usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf ./DejaVuSans.ttf
```

### Option 3: Use System Font (Windows)

Windows doesn't include DejaVu Sans by default. You can:
1. Download DejaVu Sans from the official source (Option 1).
2. Preserve its original license and notices when embedding or distributing it.

Do not rename a system font such as Arial to `DejaVuSans.ttf`. A different font is not an equivalent build input, and renaming does not establish redistribution rights.

## Alternative: Embedded Font

If you don't want to manage external font files, you can embed the font directly in the Rust code:

```rust
// In src/image/watermark.rs
const FONT_DATA: &[u8] = include_bytes!("../../fonts/DejaVuSans.ttf");
```

This is the approach currently used in the code.

## License

DejaVu fonts include Bitstream Vera terms, public-domain DejaVu changes, and Arev notices for applicable imported glyphs.
See: https://dejavu-fonts.github.io/License.html

The upstream license and copyright notices must accompany distributed native artifacts that embed this font. This README link alone is not the complete distribution notice. Current inventory and follow-up work: [Rust image replacement assessment](../../../Docs/features/rust-image-processing-replacement-assessment.md).
