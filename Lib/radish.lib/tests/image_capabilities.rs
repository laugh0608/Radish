//! R0：验证精确依赖的关键能力，不把 codec 自测冒充应用集成通过。
use ab_glyph::{Font, FontRef, GlyphId, PxScale, VariableFont};
use image::{AnimationDecoder, ImageDecoder, RgbaImage};
use std::io::Cursor;

#[test]
fn bundled_chinese_variable_font_shapes_and_rasterizes_same_weight() {
    let bytes =
        include_bytes!("../../../Clients/radish.flutter/assets/fonts/noto/NotoSansSC-VF.ttf");
    for weight in [400.0, 700.0] {
        let mut raster = FontRef::try_from_slice(bytes).unwrap();
        assert!(raster.set_variation(b"wght", weight));
        let mut face = rustybuzz::Face::from_slice(bytes, 0).unwrap();
        face.set_variations(&[rustybuzz::Variation {
            tag: rustybuzz::ttf_parser::Tag::from_bytes(b"wght"),
            value: weight,
        }]);
        let mut buffer = rustybuzz::UnicodeBuffer::new();
        buffer.push_str("萝卜 Radish，水印 2026");
        buffer.guess_segment_properties();
        let shaped = rustybuzz::shape(&face, &[], buffer);
        assert!(!shaped.glyph_infos().is_empty());
        let mut ink = 0_u64;
        for info in shaped.glyph_infos() {
            assert_ne!(
                info.glyph_id, 0,
                "fixture must have complete glyph coverage"
            );
            let glyph = GlyphId(info.glyph_id as u16).with_scale(PxScale::from(32.0));
            if let Some(outline) = raster.outline_glyph(glyph) {
                outline.draw(|_, _, coverage| ink += (coverage * 255.0) as u64);
            }
        }
        assert!(ink > 10_000);
    }
}

#[test]
fn apng_roundtrip_keeps_frames_delays_and_alpha() {
    let mut bytes = Vec::new();
    {
        let mut encoder = png::Encoder::new(&mut bytes, 2, 2);
        encoder.set_color(png::ColorType::Rgba);
        encoder.set_depth(png::BitDepth::Eight);
        encoder.set_animated(2, 3).unwrap();
        encoder.set_blend_op(png::BlendOp::Source).unwrap();
        let mut writer = encoder.write_header().unwrap();
        writer.set_frame_delay(1, 10).unwrap();
        writer
            .write_image_data(&[255, 0, 0, 128].repeat(4))
            .unwrap();
        writer.set_frame_delay(1, 5).unwrap();
        writer
            .write_image_data(&[0, 0, 255, 255].repeat(4))
            .unwrap();
        writer.finish().unwrap();
    }
    let reader = png::Decoder::new(Cursor::new(&bytes)).read_info().unwrap();
    assert_eq!(reader.info().animation_control.unwrap().num_plays, 3);
    let decoder = image::codecs::png::PngDecoder::new(Cursor::new(&bytes)).unwrap();
    assert!(decoder.is_apng().unwrap());
    let frames = decoder
        .apng()
        .unwrap()
        .into_frames()
        .collect_frames()
        .unwrap();
    assert_eq!(frames.len(), 2);
    assert_eq!(frames[0].delay().numer_denom_ms(), (100, 1));
    assert_eq!(frames[1].delay().numer_denom_ms(), (200, 1));
    assert_eq!(frames[0].buffer().get_pixel(0, 0).0, [255, 0, 0, 128]);
    assert_eq!(frames[1].buffer().get_pixel(0, 0).0, [0, 0, 255, 255]);
}

#[test]
fn ico_roundtrip_keeps_multiple_entries_and_alpha() {
    let mut icon = ico::IconDir::new(ico::ResourceType::Icon);
    for size in [16, 32] {
        let image = ico::IconImage::from_rgba_data(
            size,
            size,
            [80, 160, 240, 128].repeat((size * size) as usize),
        );
        icon.add_entry(ico::IconDirEntry::encode_as_png(&image).unwrap());
    }
    let mut bytes = Vec::new();
    icon.write(&mut bytes).unwrap();
    let decoded = ico::IconDir::read(Cursor::new(&bytes)).unwrap();
    assert_eq!(decoded.entries().len(), 2);
    assert_eq!(decoded.entries()[1].decode().unwrap().rgba_data()[3], 128);
}

#[test]
fn icc_srgb_roundtrip_handles_rgb16_without_quantizing_to_eight_bits() {
    let profile = lcms2::Profile::new_srgb();
    let profile_bytes = profile.icc().unwrap();
    let parsed = lcms2::Profile::new_icc(&profile_bytes).unwrap();
    let transform = lcms2::Transform::<[u16; 3], [u16; 3]>::new(
        &parsed,
        lcms2::PixelFormat::RGB_16,
        &parsed,
        lcms2::PixelFormat::RGB_16,
        lcms2::Intent::RelativeColorimetric,
    )
    .unwrap();
    let input = [[1234_u16, 23456, 54321]];
    let mut output = [[0_u16; 3]];
    transform.transform_pixels(&input, &mut output);
    for channel in 0..3 {
        assert!(input[0][channel].abs_diff(output[0][channel]) <= 2);
    }
}

#[test]
fn webp_native_codec_encodes_lossy_animation_with_loop_and_alpha() {
    use libwebp_sys::*;
    // Fixed small buffers, initialized structs and paired ownership below.
    unsafe {
        assert_eq!(WebPGetEncoderVersion(), 0x010600);
        let mut options: WebPAnimEncoderOptions = std::mem::zeroed();
        assert_ne!(
            WebPAnimEncoderOptionsInitInternal(&mut options, WEBP_MUX_ABI_VERSION as i32),
            0
        );
        options.anim_params.loop_count = 3;
        let encoder = WebPAnimEncoderNewInternal(8, 8, &options, WEBP_MUX_ABI_VERSION as i32);
        assert!(!encoder.is_null());
        let mut config: WebPConfig = std::mem::zeroed();
        assert_ne!(
            WebPConfigInitInternal(
                &mut config,
                WebPPreset::WEBP_PRESET_DEFAULT,
                85.0,
                WEBP_ENCODER_ABI_VERSION as i32
            ),
            0
        );
        config.lossless = 0;
        for (timestamp, color) in [(0, [255, 0, 0, 128]), (100, [0, 0, 255, 255])] {
            let pixels = color.repeat(64);
            let mut picture: WebPPicture = std::mem::zeroed();
            assert_ne!(
                WebPPictureInitInternal(&mut picture, WEBP_ENCODER_ABI_VERSION as i32),
                0
            );
            picture.width = 8;
            picture.height = 8;
            picture.use_argb = 1;
            assert_ne!(WebPPictureImportRGBA(&mut picture, pixels.as_ptr(), 32), 0);
            assert_ne!(
                WebPAnimEncoderAdd(encoder, &mut picture, timestamp, &config),
                0
            );
            WebPPictureFree(&mut picture);
        }
        assert_ne!(
            WebPAnimEncoderAdd(encoder, std::ptr::null_mut(), 300, std::ptr::null()),
            0
        );
        let mut data: WebPData = std::mem::zeroed();
        assert_ne!(WebPAnimEncoderAssemble(encoder, &mut data), 0);
        let bytes = std::slice::from_raw_parts(data.bytes, data.size).to_vec();
        WebPDataClear(&mut data);
        WebPAnimEncoderDelete(encoder);
        let decoder = image::codecs::webp::WebPDecoder::new(Cursor::new(bytes)).unwrap();
        assert!(decoder.has_animation());
        let frames = decoder.into_frames().collect_frames().unwrap();
        assert_eq!(frames.len(), 2);
        assert_eq!(frames[0].delay().numer_denom_ms(), (100, 1));
        assert_eq!(frames[1].delay().numer_denom_ms(), (200, 1));
        assert_eq!(frames[0].buffer().get_pixel(0, 0)[3], 128);
    }
}

#[test]
fn content_identification_and_png16_do_not_depend_on_extension() {
    let mut bytes = Cursor::new(Vec::new());
    image::DynamicImage::ImageLuma16(image::ImageBuffer::from_pixel(
        2,
        2,
        image::Luma([1234_u16]),
    ))
    .write_to(&mut bytes, image::ImageFormat::Png)
    .unwrap();
    let reader = image::ImageReader::new(Cursor::new(bytes.into_inner()))
        .with_guessed_format()
        .unwrap();
    let decoder = reader.into_decoder().unwrap();
    assert_eq!(decoder.color_type(), image::ColorType::L16);
    let decoded = image::DynamicImage::from_decoder(decoder).unwrap();
    assert_eq!(decoded.to_luma16().get_pixel(0, 0)[0], 1234);
}

#[test]
fn gif_encoder_can_preserve_loop_and_frame_durations() {
    let mut bytes = Vec::new();
    {
        let mut encoder = image::codecs::gif::GifEncoder::new(&mut bytes);
        encoder
            .set_repeat(image::codecs::gif::Repeat::Finite(3))
            .unwrap();
        for (duration, color) in [(100, [255, 0, 0, 255]), (200, [0, 0, 255, 255])] {
            encoder
                .encode_frame(image::Frame::from_parts(
                    RgbaImage::from_pixel(4, 4, image::Rgba(color)),
                    0,
                    0,
                    image::Delay::from_numer_denom_ms(duration, 1),
                ))
                .unwrap();
        }
    }
    let frames = image::codecs::gif::GifDecoder::new(Cursor::new(&bytes))
        .unwrap()
        .into_frames()
        .collect_frames()
        .unwrap();
    assert_eq!(frames.len(), 2);
    assert_eq!(frames[1].delay().numer_denom_ms(), (200, 1));
    let reader = gif::DecodeOptions::new()
        .read_info(Cursor::new(bytes))
        .unwrap();
    assert_eq!(reader.repeat(), gif::Repeat::Finite(3));
}
