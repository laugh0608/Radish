//! Privacy stripping without a second lossy generation when pixels need no change.
use super::model::*;

pub fn strip(bytes: &[u8], format: &str) -> Result<Option<Vec<u8>>> {
    match format {
        "JPEG" => jpeg(bytes).map(Some),
        "PNG" => png(bytes).map(Some),
        "WEBP" => webp(bytes).map(Some),
        _ => Ok(None),
    }
}

fn jpeg(bytes: &[u8]) -> Result<Vec<u8>> {
    if !bytes.starts_with(&[0xff, 0xd8]) {
        return Err(Error::InvalidImage);
    }
    let mut output = bytes[..2].to_vec();
    let mut offset = 2;
    while offset < bytes.len() {
        let start = offset;
        if bytes[offset] != 0xff {
            return Err(Error::InvalidImage);
        }
        while bytes.get(offset) == Some(&0xff) {
            offset += 1;
        }
        let marker = *bytes.get(offset).ok_or(Error::InvalidImage)?;
        offset += 1;
        if marker == 0xda {
            // SOS and entropy-coded scans are immutable. Validation already decoded
            // the complete input; strip metadata between progressive scans as well.
            let length = segment_length(bytes, offset)?;
            output.extend_from_slice(&bytes[start..offset + length]);
            offset += length;
            let scan = offset;
            while offset < bytes.len() {
                if bytes[offset] == 0xff {
                    let next = *bytes.get(offset + 1).ok_or(Error::InvalidImage)?;
                    if next != 0 && !(0xd0..=0xd7).contains(&next) {
                        break;
                    }
                    offset += 2;
                } else {
                    offset += 1;
                }
            }
            output.extend_from_slice(&bytes[scan..offset]);
        } else if marker == 0xd9 {
            output.extend_from_slice(&[0xff, 0xd9]);
            return Ok(output);
        } else {
            let length = segment_length(bytes, offset)?;
            // Adobe APP14 is a color decoding marker, not user metadata.
            if !(0xe0..=0xef).contains(&marker) && marker != 0xfe || marker == 0xee {
                output.extend_from_slice(&bytes[start..offset + length]);
            }
            offset += length;
        }
    }
    Err(Error::InvalidImage)
}

fn segment_length(bytes: &[u8], offset: usize) -> Result<usize> {
    let pair: [u8; 2] = bytes
        .get(offset..offset + 2)
        .ok_or(Error::InvalidImage)?
        .try_into()
        .map_err(|_| Error::InvalidImage)?;
    let length = u16::from_be_bytes(pair) as usize;
    if length < 2 || offset + length > bytes.len() {
        return Err(Error::InvalidImage);
    }
    Ok(length)
}

fn png(bytes: &[u8]) -> Result<Vec<u8>> {
    if bytes.get(..8) != Some(b"\x89PNG\r\n\x1a\n") {
        return Err(Error::InvalidImage);
    }
    let mut output = bytes[..8].to_vec();
    let mut offset = 8;
    while offset + 12 <= bytes.len() {
        let length = u32::from_be_bytes(
            bytes[offset..offset + 4]
                .try_into()
                .map_err(|_| Error::InvalidImage)?,
        ) as usize;
        let end = offset
            .checked_add(length)
            .and_then(|v| v.checked_add(12))
            .ok_or(Error::InvalidImage)?;
        if end > bytes.len() {
            return Err(Error::InvalidImage);
        }
        let kind = &bytes[offset + 4..offset + 8];
        if matches!(
            kind,
            b"IHDR"
                | b"PLTE"
                | b"IDAT"
                | b"IEND"
                | b"tRNS"
                | b"gAMA"
                | b"cHRM"
                | b"sRGB"
                | b"sBIT"
                | b"acTL"
                | b"fcTL"
                | b"fdAT"
        ) {
            output.extend_from_slice(&bytes[offset..end]);
        }
        if kind == b"IEND" {
            return Ok(output);
        }
        offset = end;
    }
    Err(Error::InvalidImage)
}

fn webp(bytes: &[u8]) -> Result<Vec<u8>> {
    if bytes.len() < 12 || &bytes[..4] != b"RIFF" || &bytes[8..12] != b"WEBP" {
        return Err(Error::InvalidImage);
    }
    let mut output = bytes[..12].to_vec();
    let mut offset = 12;
    while offset + 8 <= bytes.len() {
        let length = u32::from_le_bytes(
            bytes[offset + 4..offset + 8]
                .try_into()
                .map_err(|_| Error::InvalidImage)?,
        ) as usize;
        let end = offset
            .checked_add(length)
            .and_then(|v| v.checked_add(8 + length % 2))
            .ok_or(Error::InvalidImage)?;
        if end > bytes.len() {
            return Err(Error::InvalidImage);
        }
        let kind = &bytes[offset..offset + 4];
        if matches!(
            kind,
            b"VP8X" | b"VP8 " | b"VP8L" | b"ALPH" | b"ANIM" | b"ANMF"
        ) {
            let start = output.len();
            output.extend_from_slice(&bytes[offset..end]);
            if kind == b"VP8X" {
                if length < 10 {
                    return Err(Error::InvalidImage);
                }
                output[start + 8] &= !(0x08 | 0x04);
            }
        }
        offset = end;
    }
    if offset != bytes.len() {
        return Err(Error::InvalidImage);
    }
    let length = u32::try_from(output.len() - 8).map_err(|_| Error::LimitExceeded)?;
    output[4..8].copy_from_slice(&length.to_le_bytes());
    Ok(output)
}

// The generic JPEG decoder exposes converted RGB and cannot safely apply a
// CMYK ICC profile afterwards. Reject four-component input before that loss.
pub fn reject_cmyk_jpeg(bytes: &[u8]) -> Result<()> {
    let mut offset = 2;
    while offset < bytes.len() {
        if bytes[offset] != 0xff {
            return Err(Error::InvalidImage);
        }
        while bytes.get(offset) == Some(&0xff) {
            offset += 1;
        }
        let marker = *bytes.get(offset).ok_or(Error::InvalidImage)?;
        offset += 1;
        if marker == 0xda || marker == 0xd9 {
            return Ok(());
        }
        let length = segment_length(bytes, offset)?;
        if matches!(marker, 0xc0..=0xc3 | 0xc5..=0xc7 | 0xc9..=0xcb | 0xcd..=0xcf) {
            if length < 8 {
                return Err(Error::InvalidImage);
            }
            if bytes[offset + 7] == 4 {
                return Err(Error::UnsupportedFeature);
            }
            return Ok(());
        }
        offset += length;
    }
    Err(Error::InvalidImage)
}
