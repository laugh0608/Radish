use super::model::*;
use image::{AnimationDecoder, DynamicImage, ImageDecoder, ImageReader};
use std::io::{Cursor, Read};

pub fn load(path: &str, ctx: &Context<'_>) -> Result<Document> {
    ctx.check()?;
    let file = std::fs::File::open(path).map_err(|_| Error::IoRead)?;
    if file.metadata().map_err(|_| Error::IoRead)?.len() > ctx.limits.max_input_bytes {
        return Err(Error::LimitExceeded);
    }
    let mut bytes = Vec::new();
    file.take(ctx.limits.max_input_bytes + 1)
        .read_to_end(&mut bytes)
        .map_err(|_| Error::IoRead)?;
    if bytes.len() as u64 > ctx.limits.max_input_bytes {
        return Err(Error::LimitExceeded);
    }
    let format = image::guess_format(&bytes).map_err(|_| Error::InvalidImage)?;
    if !matches!(
        format,
        image::ImageFormat::Jpeg
            | image::ImageFormat::Png
            | image::ImageFormat::Gif
            | image::ImageFormat::WebP
            | image::ImageFormat::Bmp
            | image::ImageFormat::Ico
    ) {
        return Err(Error::UnsupportedFormat);
    }
    if format == image::ImageFormat::Jpeg {
        super::metadata::reject_cmyk_jpeg(&bytes)?;
    }
    let mut reader = ImageReader::with_format(Cursor::new(&bytes), format);
    let mut limits = image::Limits::default();
    limits.max_image_width = Some(ctx.limits.max_dimension);
    limits.max_image_height = Some(ctx.limits.max_dimension);
    limits.max_alloc = Some(ctx.limits.max_working_bytes / 3);
    reader.limits(limits.clone());
    let mut decoder = reader.into_decoder().map_err(|_| Error::InvalidImage)?;
    let (width, height) = decoder.dimensions();
    let pixels = ctx.limits.dimensions(width, height)?;
    let bits_per_pixel = decoder.color_type().bits_per_pixel();
    // Reserve source pixels, transform/encode scratch and the compressed input.
    if pixels * 24 + bytes.len() as u64 > ctx.limits.max_working_bytes {
        return Err(Error::LimitExceeded);
    }
    let exif = decoder.exif_metadata().map_err(|_| Error::InvalidImage)?;
    let icc = decoder.icc_profile().map_err(|_| Error::InvalidImage)?;
    if exif.as_ref().map_or(0, Vec::len) + icc.as_ref().map_or(0, Vec::len) > 4 * 1024 * 1024 {
        return Err(Error::LimitExceeded);
    }
    if let Some(bytes) = &icc {
        let profile = lcms2::Profile::new_icc(bytes).map_err(|_| Error::InvalidImage)?;
        if !matches!(
            profile.color_space(),
            lcms2::ColorSpaceSignature::RgbData | lcms2::ColorSpaceSignature::GrayData
        ) {
            return Err(Error::UnsupportedFeature);
        }
    }
    let orientation = decoder
        .orientation()
        .map_err(|_| Error::InvalidImage)?
        .to_exif();
    let mut document = Document {
        info: Info {
            width,
            height,
            display_width: if orientation >= 5 { height } else { width },
            display_height: if orientation >= 5 { width } else { height },
            format: format!("{format:?}").to_uppercase(),
            file_size: bytes.len() as u64,
            bits_per_pixel,
            orientation,
            has_exif: exif.is_some(),
            has_icc_profile: icc.is_some(),
            frame_count: 0,
            duration_ms: 0,
        },
        frames: vec![],
        loop_count: 1,
        exif,
        icc,
        is_icon: format == image::ImageFormat::Ico,
    };
    match format {
        image::ImageFormat::Gif => {
            let gif = gif::DecodeOptions::new()
                .read_info(Cursor::new(&bytes))
                .map_err(|_| Error::InvalidImage)?;
            document.loop_count = match gif.repeat() {
                gif::Repeat::Infinite => 0,
                gif::Repeat::Finite(n) => u32::from(n) + 1,
            };
            let mut decoder = image::codecs::gif::GifDecoder::new(Cursor::new(&bytes))
                .map_err(|_| Error::InvalidImage)?;
            decoder
                .set_limits(limits)
                .map_err(|_| Error::LimitExceeded)?;
            read_frames(decoder.into_frames(), &mut document, ctx)?;
        }
        image::ImageFormat::WebP => {
            let mut decoder = image::codecs::webp::WebPDecoder::new(Cursor::new(&bytes))
                .map_err(|_| Error::InvalidImage)?;
            decoder
                .set_limits(limits)
                .map_err(|_| Error::LimitExceeded)?;
            if decoder.has_animation() {
                // The container background is a display hint. Transparent canvas is
                // required for disposal; the decoder default otherwise retains pixels.
                decoder
                    .set_background_color(image::Rgba([0, 0, 0, 0]))
                    .map_err(|_| Error::InvalidImage)?;
                document.loop_count = webp_loop_count(&bytes)?;
                read_frames(decoder.into_frames(), &mut document, ctx)?;
            } else {
                push_frame(
                    &mut document,
                    DynamicImage::from_decoder(decoder).map_err(|_| Error::InvalidImage)?,
                    0,
                    ctx,
                )?;
            }
        }
        image::ImageFormat::Png => {
            let decoder = image::codecs::png::PngDecoder::with_limits(Cursor::new(&bytes), limits)
                .map_err(|_| Error::InvalidImage)?;
            if decoder.is_apng().map_err(|_| Error::InvalidImage)? {
                let mut png_reader = png::Decoder::new(Cursor::new(&bytes));
                png_reader.set_limits(png::Limits {
                    bytes: (ctx.limits.max_working_bytes / 3) as usize,
                });
                let metadata = png_reader.read_info().map_err(|_| Error::InvalidImage)?;
                document.loop_count = metadata.info().animation_control.map_or(0, |v| v.num_plays);
                read_frames(
                    decoder
                        .apng()
                        .map_err(|_| Error::InvalidImage)?
                        .into_frames(),
                    &mut document,
                    ctx,
                )?;
            } else {
                push_frame(
                    &mut document,
                    DynamicImage::from_decoder(decoder).map_err(|_| Error::InvalidImage)?,
                    0,
                    ctx,
                )?;
            }
        }
        image::ImageFormat::Ico => {
            let icon = ico::IconDir::read(Cursor::new(&bytes)).map_err(|_| Error::InvalidImage)?;
            if icon.entries().len() > ctx.limits.max_frames {
                return Err(Error::LimitExceeded);
            }
            for entry in icon.entries() {
                ctx.limits.dimensions(entry.width(), entry.height())?;
                let decoded = entry.decode().map_err(|_| Error::InvalidImage)?;
                let image = image::RgbaImage::from_raw(
                    decoded.width(),
                    decoded.height(),
                    decoded.rgba_data().to_vec(),
                )
                .ok_or(Error::InvalidImage)?;
                push_frame(&mut document, DynamicImage::ImageRgba8(image), 0, ctx)?;
            }
            document.frames.sort_by_key(|f| {
                std::cmp::Reverse(u64::from(f.image.width()) * u64::from(f.image.height()))
            });
        }
        _ => push_frame(
            &mut document,
            DynamicImage::from_decoder(decoder).map_err(|_| Error::InvalidImage)?,
            0,
            ctx,
        )?,
    }
    if document.frames.is_empty() {
        return Err(Error::InvalidImage);
    }
    document.info.frame_count = document.frames.len();
    document.info.duration_ms = document.frames.iter().map(|f| u64::from(f.delay_ms)).sum();
    Ok(document)
}

fn read_frames(
    frames: image::Frames<'_>,
    document: &mut Document,
    ctx: &Context<'_>,
) -> Result<()> {
    for frame in frames {
        ctx.check()?;
        let frame = frame.map_err(|_| Error::InvalidImage)?;
        let (num, den) = frame.delay().numer_denom_ms();
        let delay = num / den.max(1);
        push_frame(
            document,
            DynamicImage::ImageRgba8(frame.into_buffer()),
            delay,
            ctx,
        )?;
    }
    Ok(())
}

fn push_frame(
    document: &mut Document,
    image: DynamicImage,
    delay_ms: u32,
    ctx: &Context<'_>,
) -> Result<()> {
    ctx.check()?;
    let pixels = ctx.limits.dimensions(image.width(), image.height())?;
    let total_pixels = document
        .frames
        .iter()
        .map(|f| u64::from(f.image.width()) * u64::from(f.image.height()))
        .sum::<u64>()
        + pixels;
    let stored_bytes = document
        .frames
        .iter()
        .map(|f| f.image.as_bytes().len() as u64)
        .sum::<u64>()
        + image.as_bytes().len() as u64;
    if document.frames.len() >= ctx.limits.max_frames
        || total_pixels > ctx.limits.max_total_pixels
        || stored_bytes + pixels * 20 + document.info.file_size > ctx.limits.max_working_bytes
    {
        return Err(Error::LimitExceeded);
    }
    document.frames.push(Frame { image, delay_ms });
    Ok(())
}

fn webp_loop_count(bytes: &[u8]) -> Result<u32> {
    let mut offset = 12_usize;
    while offset.checked_add(8).is_some_and(|end| end <= bytes.len()) {
        let len = u32::from_le_bytes(
            bytes[offset + 4..offset + 8]
                .try_into()
                .map_err(|_| Error::InvalidImage)?,
        ) as usize;
        let end = offset
            .checked_add(8)
            .and_then(|v| v.checked_add(len))
            .ok_or(Error::InvalidImage)?;
        if end > bytes.len() {
            return Err(Error::InvalidImage);
        }
        if &bytes[offset..offset + 4] == b"ANIM" && len >= 6 {
            return Ok(u32::from(u16::from_le_bytes([
                bytes[offset + 12],
                bytes[offset + 13],
            ])));
        }
        offset = end.checked_add(len % 2).ok_or(Error::InvalidImage)?;
    }
    Err(Error::InvalidImage)
}
