use super::{decode, encode, metadata, model::*, text};
use image::{DynamicImage, GenericImageView, Pixel};
use std::sync::atomic::AtomicBool;
use std::time::Instant;

struct OwnedOutputs(Vec<String>);
impl Drop for OwnedOutputs {
    fn drop(&mut self) {
        for path in &self.0 {
            let _ = std::fs::remove_file(path);
        }
    }
}

pub fn execute(request: &Request, cancelled: &AtomicBool) -> Response {
    let mut owned = OwnedOutputs(Vec::new());
    match process(request, cancelled, &mut owned.0) {
        Ok(response) => {
            owned.0.clear();
            response
        }
        Err(error) => Response::fail(error),
    }
}

fn process(request: &Request, cancelled: &AtomicBool, owned: &mut Vec<String>) -> Result<Response> {
    request.limits.validate()?;
    if request.schema_version != 1
        || request.input_path.contains('\0')
        || request.outputs.len() > 8
        || !matches!(
            request.operation.as_str(),
            "info"
                | "validate"
                | "thumbnail"
                | "resize"
                | "compress"
                | "watermark"
                | "stripMetadata"
                | "multiple"
        )
    {
        return Err(Error::InvalidArgument);
    }
    let ctx = Context {
        limits: &request.limits,
        cancelled,
        started: Instant::now(),
    };
    for (index, output) in request.outputs.iter().enumerate() {
        if output.path.contains('\0')
            || output.path == request.input_path
            || std::path::Path::new(&output.path).exists()
            || !(1..=100).contains(&output.quality)
            || request.outputs[..index]
                .iter()
                .any(|prior| prior.path.eq_ignore_ascii_case(&output.path))
        {
            return Err(Error::InvalidArgument);
        }
    }
    let mut document = decode::load(&request.input_path, &ctx)?;
    if matches!(request.operation.as_str(), "info" | "validate") {
        return Ok(Response {
            success: true,
            error_code: None,
            info: Some(document.info),
            outputs: vec![],
        });
    }
    if request.outputs.is_empty() {
        return Err(Error::InvalidArgument);
    }
    if request.operation == "stripMetadata"
        && document.info.orientation == 1
        && document.icc.is_none()
        && request.outputs.len() == 1
        && request.outputs[0].format == document.info.format
        && (!request.outputs[0].first_frame || document.info.frame_count == 1)
    {
        use std::io::Read;
        let mut input = Vec::new();
        std::fs::File::open(&request.input_path)
            .map_err(|_| Error::IoRead)?
            .take(ctx.limits.max_input_bytes + 1)
            .read_to_end(&mut input)
            .map_err(|_| Error::IoRead)?;
        if input.len() as u64 > ctx.limits.max_input_bytes {
            return Err(Error::LimitExceeded);
        }
        if let Some(bytes) = metadata::strip(&input, &document.info.format)? {
            encode::save_bytes(&bytes, &request.outputs[0].path, &ctx)?;
            owned.push(request.outputs[0].path.clone());
            let mut info = document.info.clone();
            info.has_exif = false;
            info.file_size = bytes.len() as u64;
            return Ok(Response {
                success: true,
                error_code: None,
                info: Some(document.info),
                outputs: vec![info],
            });
        }
    }
    let orientation = image::metadata::Orientation::from_exif(document.info.orientation)
        .ok_or(Error::InvalidImage)?;
    let profile = document
        .icc
        .as_deref()
        .map(lcms2::Profile::new_icc)
        .transpose()
        .map_err(|_| Error::InvalidImage)?;
    let source_bytes = document
        .frames
        .iter()
        .map(|f| f.image.as_bytes().len() as u64)
        .sum::<u64>();
    let largest_pixels = document
        .frames
        .iter()
        .map(|f| u64::from(f.image.width()) * u64::from(f.image.height()))
        .max()
        .unwrap_or(0);
    // Orientation, ICC conversion and encoder scratch coexist with decoded frames.
    if source_bytes + largest_pixels * 24 + ctx.limits.max_input_bytes
        > ctx.limits.max_working_bytes
    {
        return Err(Error::LimitExceeded);
    }
    for frame in &mut document.frames {
        ctx.check()?;
        frame.image.apply_orientation(orientation);
        if let Some(profile) = &profile {
            convert_srgb(&mut frame.image, profile, &ctx)?;
        }
    }
    let exif = if request.operation == "stripMetadata" {
        None
    } else {
        document.exif.take().map(normalize_exif).transpose()?
    };
    let source_bytes = document
        .frames
        .iter()
        .map(|f| f.image.as_bytes().len() as u64)
        .sum::<u64>();
    let mut watermark_limits = ctx.limits.clone();
    watermark_limits.max_working_bytes = ctx
        .limits
        .max_working_bytes
        .checked_sub(source_bytes + largest_pixels * 24)
        .filter(|remaining| *remaining > 0)
        .ok_or(Error::LimitExceeded)?;
    let watermark_ctx = Context {
        limits: &watermark_limits,
        cancelled,
        started: ctx.started,
    };
    let watermark = if request.operation == "watermark" {
        let options = request.watermark.as_ref().ok_or(Error::InvalidArgument)?;
        validate_watermark(options)?;
        Some(if options.kind == "text" {
            text::render(options, &watermark_ctx)?
        } else if options.kind == "image" {
            let source = decode::load(&options.image_path, &watermark_ctx)?;
            if source.frames.len() != 1 {
                return Err(Error::UnsupportedFeature);
            }
            let mut image = source
                .frames
                .into_iter()
                .next()
                .ok_or(Error::InvalidImage)?
                .image;
            image.apply_orientation(
                image::metadata::Orientation::from_exif(source.info.orientation)
                    .ok_or(Error::InvalidImage)?,
            );
            if let Some(bytes) = source.icc {
                convert_srgb(
                    &mut image,
                    &lcms2::Profile::new_icc(&bytes).map_err(|_| Error::InvalidImage)?,
                    &ctx,
                )?;
            }
            image.to_rgba8()
        } else {
            return Err(Error::InvalidArgument);
        })
    } else {
        None
    };
    let mask_bytes = watermark
        .as_ref()
        .map_or(0, |mask| mask.as_raw().len() as u64);
    let mut outputs = Vec::new();
    let mut total_output = 0;
    for output in &request.outputs {
        ctx.check()?;
        let mut frames = Vec::new();
        for source in &document.frames {
            ctx.check()?;
            if output.first_frame && !frames.is_empty() {
                break;
            }
            let accumulated = frames
                .iter()
                .map(|f: &Frame| f.image.as_bytes().len() as u64)
                .sum::<u64>();
            let source_pixels = u64::from(source.image.width()) * u64::from(source.image.height());
            let retained = source_bytes + accumulated + mask_bytes + ctx.limits.max_input_bytes;
            // RGBA32F input, horizontal filter intermediate and output may coexist.
            let target = if matches!(
                request.operation.as_str(),
                "thumbnail" | "resize" | "multiple"
            ) {
                dimensions(
                    source.image.dimensions(),
                    output.width,
                    output.height,
                    output.keep_aspect_ratio,
                )?
            } else {
                source.image.dimensions()
            };
            let target_pixels = ctx.limits.dimensions(target.0, target.1)?;
            let intermediate = u64::from(source.image.width()) * u64::from(target.1);
            if retained + source_pixels * 32 + target_pixels * 32 + intermediate * 16
                > ctx.limits.max_working_bytes
            {
                return Err(Error::LimitExceeded);
            }
            let mut image = source.image.clone();
            if matches!(
                request.operation.as_str(),
                "thumbnail" | "resize" | "multiple"
            ) {
                let (width, height) = dimensions(
                    image.dimensions(),
                    output.width,
                    output.height,
                    output.keep_aspect_ratio,
                )?;
                let pixels = ctx.limits.dimensions(width, height)?;
                if pixels * 32 + retained > ctx.limits.max_working_bytes {
                    return Err(Error::LimitExceeded);
                }
                image = resize(&image, width, height);
            }
            if let (Some(mask), Some(options)) = (&watermark, &request.watermark) {
                let mut mask = mask.clone();
                if options.kind == "image" {
                    let width = (image.width() as f32 * options.scale).floor().max(1.0) as u32;
                    let height = (u64::from(width) * u64::from(mask.height())
                        / u64::from(mask.width()))
                    .max(1) as u32;
                    let mark_pixels = ctx.limits.dimensions(width, height)?;
                    if retained
                        + image.as_bytes().len() as u64 * 3
                        + mask_bytes * 8
                        + mark_pixels * 48
                        + u64::from(mask.width()) * u64::from(height) * 16
                        > ctx.limits.max_working_bytes
                    {
                        return Err(Error::LimitExceeded);
                    }
                    mask = resize(&DynamicImage::ImageRgba8(mask), width, height).to_rgba8();
                    for pixel in mask.pixels_mut() {
                        pixel[3] = (f32::from(pixel[3]) * options.opacity).round() as u8;
                    }
                }
                let (x, y) = position(
                    image.dimensions(),
                    mask.dimensions(),
                    options.position,
                    options.padding,
                )?;
                let mut rgba = image.to_rgba8();
                image::imageops::overlay(&mut rgba, &mask, i64::from(x), i64::from(y));
                image = DynamicImage::ImageRgba8(rgba);
            }
            let accumulated = frames
                .iter()
                .map(|f: &Frame| f.image.as_bytes().len() as u64)
                .sum::<u64>();
            let source_bytes = document
                .frames
                .iter()
                .map(|f| f.image.as_bytes().len() as u64)
                .sum::<u64>();
            if accumulated + source_bytes + image.as_bytes().len() as u64 * 3
                > ctx.limits.max_working_bytes
            {
                return Err(Error::LimitExceeded);
            }
            frames.push(Frame {
                image,
                delay_ms: source.delay_ms,
            });
        }
        if document.is_icon && output.format != "ICO" && !output.first_frame {
            return Err(Error::UnsupportedFeature);
        }
        let info = encode::save(&frames, output, document.loop_count, exif.as_deref(), &ctx)?;
        owned.push(output.path.clone());
        total_output += info.file_size;
        if total_output > 96 * 1024 * 1024 {
            return Err(Error::LimitExceeded);
        }
        outputs.push(info);
    }
    Ok(Response {
        success: true,
        error_code: None,
        info: Some(document.info),
        outputs,
    })
}

fn dimensions(source: (u32, u32), width: u32, height: u32, keep_ratio: bool) -> Result<(u32, u32)> {
    if width == 0 && height == 0 {
        return Err(Error::InvalidArgument);
    }
    let (sw, sh) = (u64::from(source.0), u64::from(source.1));
    let (w, h) = match (width, height) {
        (0, h) => ((u64::from(h) * sw / sh).max(1), u64::from(h)),
        (w, 0) => (u64::from(w), (u64::from(w) * sh / sw).max(1)),
        (w, h) if keep_ratio && u64::from(w) * sh <= u64::from(h) * sw => {
            (u64::from(w), (u64::from(w) * sh / sw).max(1))
        }
        (_w, h) if keep_ratio => ((u64::from(h) * sw / sh).max(1), u64::from(h)),
        (w, h) => (u64::from(w), u64::from(h)),
    };
    Ok((
        u32::try_from(w).map_err(|_| Error::LimitExceeded)?,
        u32::try_from(h).map_err(|_| Error::LimitExceeded)?,
    ))
}

fn resize(image: &DynamicImage, width: u32, height: u32) -> DynamicImage {
    // Keep high bit depth through resampling; premultiply so transparent RGB does not bleed.
    let mut pixels = image.to_rgba32f();
    for p in pixels.pixels_mut() {
        for i in 0..3 {
            p[i] *= p[3];
        }
    }
    let mut pixels = image::imageops::resize(
        &pixels,
        width,
        height,
        image::imageops::FilterType::Lanczos3,
    );
    for p in pixels.pixels_mut() {
        p[3] = p[3].clamp(0.0, 1.0);
        for i in 0..3 {
            p[i] = if p[3] > 0.0 {
                (p[i] / p[3]).clamp(0.0, 1.0)
            } else {
                0.0
            };
        }
    }
    let resized = DynamicImage::ImageRgba32F(pixels);
    if image.color().bits_per_pixel() / image.color().channel_count() as u16 > 8 {
        DynamicImage::ImageRgba16(resized.to_rgba16())
    } else {
        DynamicImage::ImageRgba8(resized.to_rgba8())
    }
}

fn position(
    canvas: (u32, u32),
    mark: (u32, u32),
    position: u8,
    padding: u32,
) -> Result<(u32, u32)> {
    if mark.0.saturating_add(padding.saturating_mul(2)) > canvas.0
        || mark.1.saturating_add(padding.saturating_mul(2)) > canvas.1
    {
        return Err(Error::LimitExceeded);
    }
    Ok(match position {
        0 => (padding, padding),
        1 => (canvas.0 - mark.0 - padding, padding),
        2 => (padding, canvas.1 - mark.1 - padding),
        3 => (canvas.0 - mark.0 - padding, canvas.1 - mark.1 - padding),
        4 => ((canvas.0 - mark.0) / 2, (canvas.1 - mark.1) / 2),
        _ => return Err(Error::InvalidArgument),
    })
}
fn validate_watermark(options: &Watermark) -> Result<()> {
    if !options.opacity.is_finite()
        || !(0.0..=1.0).contains(&options.opacity)
        || !options.font_size.is_finite()
        || !(1.0..=512.0).contains(&options.font_size)
        || !options.scale.is_finite()
        || options.scale <= 0.0
        || options.scale > 1.0
        || options.position > 4
    {
        return Err(Error::InvalidArgument);
    }
    Ok(())
}

fn convert_srgb(image: &mut DynamicImage, input: &lcms2::Profile, ctx: &Context<'_>) -> Result<()> {
    let output = lcms2::Profile::new_srgb();
    let original_depth = image.color().bits_per_pixel() / image.color().channel_count() as u16;
    let is_gray = matches!(
        image.color(),
        image::ColorType::L8
            | image::ColorType::La8
            | image::ColorType::L16
            | image::ColorType::La16
    );
    let rgb = if input.color_space() == lcms2::ColorSpaceSignature::RgbData {
        Some(
            lcms2::Transform::<[u16; 3], [u16; 3]>::new(
                input,
                lcms2::PixelFormat::RGB_16,
                &output,
                lcms2::PixelFormat::RGB_16,
                lcms2::Intent::RelativeColorimetric,
            )
            .map_err(|_| Error::InvalidImage)?,
        )
    } else {
        None
    };
    let gray = if input.color_space() == lcms2::ColorSpaceSignature::GrayData && is_gray {
        Some(
            lcms2::Transform::<u16, [u16; 3]>::new(
                input,
                lcms2::PixelFormat::GRAY_16,
                &output,
                lcms2::PixelFormat::RGB_16,
                lcms2::Intent::RelativeColorimetric,
            )
            .map_err(|_| Error::InvalidImage)?,
        )
    } else {
        None
    };
    if rgb.is_none() && gray.is_none() {
        return Err(Error::UnsupportedFeature);
    }
    let mut pixels = image.to_rgba16();
    for row in pixels.rows_mut() {
        ctx.check()?;
        let mut row: Vec<_> = row.collect();
        let mut converted = vec![[0; 3]; row.len()];
        if let Some(transform) = &rgb {
            let values: Vec<[u16; 3]> = row.iter().map(|p| [p[0], p[1], p[2]]).collect();
            transform.transform_pixels(&values, &mut converted);
        } else if let Some(transform) = &gray {
            let values: Vec<u16> = row.iter().map(|p| p[0]).collect();
            transform.transform_pixels(&values, &mut converted);
        }
        for (pixel, rgb) in row.iter_mut().zip(converted) {
            pixel.channels_mut()[..3].copy_from_slice(&rgb);
        }
    }
    *image = if original_depth > 8 {
        DynamicImage::ImageRgba16(pixels)
    } else {
        DynamicImage::ImageRgba8(DynamicImage::ImageRgba16(pixels).to_rgba8())
    };
    Ok(())
}

fn normalize_exif(mut bytes: Vec<u8>) -> Result<Vec<u8>> {
    // Reset only orientation in the main TIFF IFD. Unknown/corrupt metadata is
    // rejected rather than copied with an orientation contradicting the pixels.
    let offset = if bytes.starts_with(b"Exif\0\0") { 6 } else { 0 };
    let data = bytes.get_mut(offset..).ok_or(Error::InvalidImage)?;
    let little = match data.get(..2) {
        Some(b"II") => true,
        Some(b"MM") => false,
        _ => return Err(Error::InvalidImage),
    };
    let word = |slice: &[u8]| -> Result<u16> {
        let value: [u8; 2] = slice.try_into().map_err(|_| Error::InvalidImage)?;
        Ok(if little {
            u16::from_le_bytes(value)
        } else {
            u16::from_be_bytes(value)
        })
    };
    let dword = |slice: &[u8]| -> Result<u32> {
        let value: [u8; 4] = slice.try_into().map_err(|_| Error::InvalidImage)?;
        Ok(if little {
            u32::from_le_bytes(value)
        } else {
            u32::from_be_bytes(value)
        })
    };
    if data.len() < 8 {
        return Err(Error::InvalidImage);
    }
    let start = dword(&data[4..8])? as usize;
    let count = word(data.get(start..start + 2).ok_or(Error::InvalidImage)?)? as usize;
    for i in 0..count {
        let entry = start + 2 + i * 12;
        let item = data.get_mut(entry..entry + 12).ok_or(Error::InvalidImage)?;
        if word(&item[..2])? == 0x112 {
            if word(&item[2..4])? != 3 || dword(&item[4..8])? != 1 {
                return Err(Error::InvalidImage);
            }
            item[8..10].copy_from_slice(&if little {
                1_u16.to_le_bytes()
            } else {
                1_u16.to_be_bytes()
            });
        }
    }
    Ok(bytes)
}
