use super::model::*;
use image::ImageEncoder;
use std::io::{BufWriter, Write};

struct LimitedWriter<W> {
    inner: W,
    written: u64,
    maximum: u64,
    exceeded: bool,
}
impl<W: Write> Write for LimitedWriter<W> {
    fn write(&mut self, bytes: &[u8]) -> std::io::Result<usize> {
        if self.written.saturating_add(bytes.len() as u64) > self.maximum {
            self.exceeded = true;
            return Err(std::io::Error::other("output limit"));
        }
        let count = self.inner.write(bytes)?;
        self.written += count as u64;
        Ok(count)
    }
    fn flush(&mut self) -> std::io::Result<()> {
        self.inner.flush()
    }
}

struct PendingOutput<'a> {
    path: &'a str,
    committed: bool,
}
impl Drop for PendingOutput<'_> {
    fn drop(&mut self) {
        if !self.committed {
            let _ = std::fs::remove_file(self.path);
        }
    }
}

pub fn save_bytes(bytes: &[u8], path: &str, ctx: &Context<'_>) -> Result<()> {
    ctx.check()?;
    if bytes.len() as u64 > ctx.limits.max_output_bytes {
        return Err(Error::LimitExceeded);
    }
    let file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|_| Error::IoWrite)?;
    let mut pending = PendingOutput {
        path,
        committed: false,
    };
    let mut file = file;
    file.write_all(bytes).map_err(|_| Error::IoWrite)?;
    file.sync_all().map_err(|_| Error::IoWrite)?;
    ctx.check()?;
    pending.committed = true;
    Ok(())
}

pub fn save(
    frames: &[Frame],
    output: &Output,
    loop_count: u32,
    exif: Option<&[u8]>,
    ctx: &Context<'_>,
) -> Result<Info> {
    ctx.check()?;
    let first = frames.first().ok_or(Error::InvalidImage)?;
    let file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&output.path)
        .map_err(|_| Error::IoWrite)?;
    let mut pending = PendingOutput {
        path: &output.path,
        committed: false,
    };
    let mut writer = LimitedWriter {
        inner: BufWriter::new(file),
        written: 0,
        maximum: ctx.limits.max_output_bytes,
        exceeded: false,
    };
    let result = (|| {
        match output.format.as_str() {
            "JPEG" => {
                if frames.len() != 1 {
                    return Err(Error::UnsupportedFeature);
                }
                let mut encoder =
                    image::codecs::jpeg::JpegEncoder::new_with_quality(&mut writer, output.quality);
                if let Some(exif) = exif {
                    encoder
                        .set_exif_metadata(exif.to_vec())
                        .map_err(|_| Error::UnsupportedFeature)?;
                }
                let rgba = first.image.to_rgba8();
                let rgb = image::RgbImage::from_fn(rgba.width(), rgba.height(), |x, y| {
                    let p = rgba.get_pixel(x, y).0;
                    let a = u32::from(p[3]);
                    image::Rgb(
                        [0, 1, 2]
                            .map(|i| ((u32::from(p[i]) * a + 255 * (255 - a) + 127) / 255) as u8),
                    )
                });
                encoder.encode_image(&rgb).map_err(|_| Error::IoWrite)?;
            }
            "PNG" if frames.len() == 1 => {
                let mut encoder = image::codecs::png::PngEncoder::new(&mut writer);
                if let Some(exif) = exif {
                    encoder
                        .set_exif_metadata(exif.to_vec())
                        .map_err(|_| Error::UnsupportedFeature)?;
                }
                first
                    .image
                    .write_with_encoder(encoder)
                    .map_err(|_| Error::IoWrite)?;
            }
            "PNG" => {
                let mut encoder =
                    png::Encoder::new(&mut writer, first.image.width(), first.image.height());
                encoder.set_color(png::ColorType::Rgba);
                encoder.set_depth(png::BitDepth::Eight);
                encoder
                    .set_animated(frames.len() as u32, loop_count)
                    .map_err(|_| Error::InvalidArgument)?;
                encoder
                    .set_blend_op(png::BlendOp::Source)
                    .map_err(|_| Error::InvalidArgument)?;
                encoder
                    .set_dispose_op(png::DisposeOp::None)
                    .map_err(|_| Error::InvalidArgument)?;
                let mut png = encoder.write_header().map_err(|_| Error::IoWrite)?;
                if let Some(exif) = exif {
                    png.write_chunk(png::chunk::eXIf, exif)
                        .map_err(|_| Error::IoWrite)?;
                }
                for frame in frames {
                    ctx.check()?;
                    // Millisecond precision; format bounds are checked, never wrap a delay.
                    let (num, den) = rational_delay(frame.delay_ms)?;
                    png.set_frame_delay(num, den).map_err(|_| Error::IoWrite)?;
                    png.write_image_data(frame.image.to_rgba8().as_raw())
                        .map_err(|_| Error::IoWrite)?;
                }
                png.finish().map_err(|_| Error::IoWrite)?;
            }
            "GIF" => {
                let mut encoder = image::codecs::gif::GifEncoder::new(&mut writer);
                let repeat = if loop_count == 0 {
                    image::codecs::gif::Repeat::Infinite
                } else {
                    image::codecs::gif::Repeat::Finite(
                        u16::try_from(loop_count - 1).map_err(|_| Error::InvalidArgument)?,
                    )
                };
                encoder.set_repeat(repeat).map_err(|_| Error::IoWrite)?;
                for frame in frames {
                    ctx.check()?;
                    encoder
                        .encode_frame(image::Frame::from_parts(
                            frame.image.to_rgba8(),
                            0,
                            0,
                            image::Delay::from_numer_denom_ms(frame.delay_ms, 1),
                        ))
                        .map_err(|_| Error::IoWrite)?;
                }
            }
            "WEBP" => {
                let bytes = webp(frames, output.quality, loop_count, exif, ctx)?;
                writer.write_all(&bytes).map_err(|_| Error::IoWrite)?;
            }
            "BMP" => {
                if frames.len() != 1 {
                    return Err(Error::UnsupportedFeature);
                }
                first
                    .image
                    .to_rgba8()
                    .write_with_encoder(image::codecs::bmp::BmpEncoder::new(&mut writer))
                    .map_err(|_| Error::IoWrite)?;
            }
            "ICO" => {
                let mut icon = ico::IconDir::new(ico::ResourceType::Icon);
                for frame in frames {
                    ctx.check()?;
                    let rgba = frame.image.to_rgba8();
                    if rgba.width() > 256 || rgba.height() > 256 {
                        return Err(Error::LimitExceeded);
                    }
                    icon.add_entry(
                        ico::IconDirEntry::encode_as_png(&ico::IconImage::from_rgba_data(
                            rgba.width(),
                            rgba.height(),
                            rgba.into_raw(),
                        ))
                        .map_err(|_| Error::IoWrite)?,
                    );
                }
                icon.write(&mut writer).map_err(|_| Error::IoWrite)?;
            }
            _ => return Err(Error::UnsupportedFormat),
        }
        ctx.check()?;
        writer.flush().map_err(|_| Error::IoWrite)?;
        writer
            .inner
            .get_ref()
            .sync_all()
            .map_err(|_| Error::IoWrite)?;
        Ok(Info {
            width: first.image.width(),
            height: first.image.height(),
            display_width: first.image.width(),
            display_height: first.image.height(),
            format: output.format.clone(),
            file_size: writer.written,
            bits_per_pixel: first.image.color().bits_per_pixel(),
            orientation: 1,
            has_exif: exif.is_some(),
            has_icc_profile: false,
            frame_count: frames.len(),
            duration_ms: frames.iter().map(|f| u64::from(f.delay_ms)).sum(),
        })
    })();
    let result = if writer.exceeded {
        Err(Error::LimitExceeded)
    } else {
        result
    };
    drop(writer);
    pending.committed = result.is_ok();
    result
}

fn rational_delay(ms: u32) -> Result<(u16, u16)> {
    let mut a = ms;
    let mut b = 1000;
    while b != 0 {
        (a, b) = (b, a % b);
    }
    let gcd = a.max(1);
    Ok((
        u16::try_from(ms / gcd).map_err(|_| Error::LimitExceeded)?,
        (1000 / gcd) as u16,
    ))
}

unsafe extern "C" fn webp_progress(_percent: i32, picture: *const libwebp_sys::WebPPicture) -> i32 {
    if picture.is_null() {
        return 0;
    }
    let context = unsafe { (*picture).user_data.cast::<Context<'_>>() };
    if context.is_null() {
        return 0;
    }
    i32::from(unsafe { &*context }.check().is_ok())
}

fn webp(
    frames: &[Frame],
    quality: u8,
    loop_count: u32,
    exif: Option<&[u8]>,
    ctx: &Context<'_>,
) -> Result<Vec<u8>> {
    use libwebp_sys::*;
    struct Encoder(*mut WebPAnimEncoder);
    impl Drop for Encoder {
        fn drop(&mut self) {
            unsafe { WebPAnimEncoderDelete(self.0) }
        }
    }
    struct Picture(WebPPicture);
    impl Drop for Picture {
        fn drop(&mut self) {
            unsafe { WebPPictureFree(&mut self.0) }
        }
    }
    struct Data(WebPData);
    impl Drop for Data {
        fn drop(&mut self) {
            unsafe { WebPDataClear(&mut self.0) }
        }
    }
    // Native structs are initialized by the versioned libwebp initializer.
    // Each allocation is paired with its matching library destructor.
    unsafe {
        let first = &frames[0].image;
        let mut options: WebPAnimEncoderOptions = std::mem::zeroed();
        if WebPAnimEncoderOptionsInitInternal(&mut options, WEBP_MUX_ABI_VERSION as i32) == 0 {
            return Err(Error::Internal);
        }
        options.anim_params.loop_count =
            i32::try_from(loop_count).map_err(|_| Error::InvalidArgument)?;
        let encoder = Encoder(WebPAnimEncoderNewInternal(
            first.width() as i32,
            first.height() as i32,
            &options,
            WEBP_MUX_ABI_VERSION as i32,
        ));
        if encoder.0.is_null() {
            return Err(Error::LimitExceeded);
        }
        if let Some(exif) = exif {
            let data = WebPData {
                bytes: exif.as_ptr(),
                size: exif.len(),
            };
            if WebPAnimEncoderSetChunk(encoder.0, c"EXIF".as_ptr(), &data, 1)
                != WebPMuxError::WEBP_MUX_OK
            {
                return Err(Error::IoWrite);
            }
        }
        let mut config: WebPConfig = std::mem::zeroed();
        if WebPConfigInitInternal(
            &mut config,
            WebPPreset::WEBP_PRESET_DEFAULT,
            f32::from(quality),
            WEBP_ENCODER_ABI_VERSION as i32,
        ) == 0
        {
            return Err(Error::Internal);
        }
        config.lossless = 0;
        let mut timestamp = 0_i32;
        for frame in frames {
            ctx.check()?;
            let pixels = frame.image.to_rgba8();
            let mut picture = Picture(std::mem::zeroed());
            if WebPPictureInitInternal(&mut picture.0, WEBP_ENCODER_ABI_VERSION as i32) == 0 {
                return Err(Error::Internal);
            }
            picture.0.width = pixels.width() as i32;
            picture.0.height = pixels.height() as i32;
            picture.0.use_argb = 1;
            picture.0.progress_hook = Some(webp_progress);
            picture.0.user_data = (ctx as *const Context<'_>).cast_mut().cast();
            if WebPPictureImportRGBA(&mut picture.0, pixels.as_ptr(), pixels.width() as i32 * 4)
                == 0
                || WebPAnimEncoderAdd(encoder.0, &mut picture.0, timestamp, &config) == 0
            {
                ctx.check()?;
                return Err(Error::IoWrite);
            }
            timestamp = timestamp
                .checked_add(
                    i32::try_from(frame.delay_ms.max(1)).map_err(|_| Error::LimitExceeded)?,
                )
                .ok_or(Error::LimitExceeded)?;
        }
        if WebPAnimEncoderAdd(encoder.0, std::ptr::null_mut(), timestamp, std::ptr::null()) == 0 {
            return Err(Error::IoWrite);
        }
        let mut data = Data(std::mem::zeroed());
        if WebPAnimEncoderAssemble(encoder.0, &mut data.0) == 0 {
            return Err(Error::IoWrite);
        }
        if data.0.size as u64 > ctx.limits.max_output_bytes {
            return Err(Error::LimitExceeded);
        }
        Ok(std::slice::from_raw_parts(data.0.bytes, data.0.size).to_vec())
    }
}
