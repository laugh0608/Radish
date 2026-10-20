use super::*;
use image::{DynamicImage, Rgba, RgbaImage};
use serde_json::json;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};

static NEXT: AtomicU64 = AtomicU64::new(0);
struct Fixture(PathBuf);
impl Fixture {
    fn new() -> Self {
        let path = std::env::temp_dir().join(format!(
            "radish-image-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        std::fs::create_dir(&path).unwrap();
        let image = DynamicImage::ImageRgba8(RgbaImage::from_fn(128, 96, |x, y| {
            Rgba([x as u8, y as u8, 100, 128])
        }));
        image
            .save_with_format(path.join("input.tmp"), image::ImageFormat::Png)
            .unwrap();
        Self(path)
    }
    fn request(&self, operation: &str, output: &str, format: &str) -> serde_json::Value {
        json!({"schemaVersion":1,"operation":operation,"inputPath":self.0.join("input.tmp"),
            "outputs":[{"path":self.0.join(output),"format":format,"width":64,"height":64,"quality":85,"firstFrame":false}]})
    }
    fn execute(&self, value: serde_json::Value) -> Response {
        let request = serde_json::from_value(value).unwrap();
        execute(&request, &AtomicBool::new(false))
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = std::fs::remove_dir_all(&self.0);
    }
}

#[test]
fn all_static_encoders_write_real_format_and_resize_dimensions() {
    let f = Fixture::new();
    for (format, expected) in [
        ("JPEG", image::ImageFormat::Jpeg),
        ("PNG", image::ImageFormat::Png),
        ("WEBP", image::ImageFormat::WebP),
        ("BMP", image::ImageFormat::Bmp),
        ("GIF", image::ImageFormat::Gif),
        ("ICO", image::ImageFormat::Ico),
    ] {
        let result = f.execute(f.request("resize", format, format));
        assert!(result.success, "{format}: {:?}", result.error_code);
        let bytes = std::fs::read(f.0.join(format)).unwrap();
        assert_eq!(image::guess_format(&bytes).unwrap(), expected);
        let image = image::load_from_memory(&bytes).unwrap();
        assert_eq!((image.width(), image.height()), (64, 48));
    }
}

#[test]
fn cancellation_and_batch_failure_do_not_leave_successful_partial_outputs() {
    let f = Fixture::new();
    let request: Request =
        serde_json::from_value(f.request("resize", "cancel.png", "PNG")).unwrap();
    let response = execute(&request, &AtomicBool::new(true));
    assert!(!response.success);
    assert!(!f.0.join("cancel.png").exists());
    let mut request = f.request("multiple", "first.png", "PNG");
    request["outputs"]
        .as_array_mut()
        .unwrap()
        .push(json!({"path":f.0.join("missing/second.png"),"format":"PNG","width":32,"height":32}));
    let response = f.execute(request);
    assert!(!response.success);
    assert!(!f.0.join("first.png").exists());
}

#[test]
fn invalid_input_and_excessive_pixels_are_distinct_failures() {
    let f = Fixture::new();
    let mut request = f.request("validate", "unused", "PNG");
    request["limits"] = json!({"maxPixels":100});
    let response = f.execute(request);
    assert!(matches!(
        response.error_code,
        Some(model::Error::LimitExceeded)
    ));
    std::fs::write(f.0.join("input.tmp"), b"PRIVATE_IMAGE_SENTINEL").unwrap();
    let response = f.execute(f.request("info", "unused", "PNG"));
    assert!(matches!(
        response.error_code,
        Some(model::Error::InvalidImage)
    ));
    assert!(
        !serde_json::to_string(&response)
            .unwrap()
            .contains("PRIVATE")
    );
}

#[test]
fn chinese_and_image_watermark_are_real_operations() {
    let f = Fixture::new();
    let mut request = f.request("watermark", "text.png", "PNG");
    request["watermark"] = json!({
        "kind":"text","text":"萝卜","fontPaths":[concat!(env!("CARGO_MANIFEST_DIR"),"/../../Clients/radish.flutter/assets/fonts/noto/NotoSansSC-VF.ttf")],
        "fontSize":16,"opacity":0.5,"position":3,"color":[255,0,0,255],"padding":4,"scale":0.1
    });
    let response = f.execute(request);
    assert!(response.success, "{:?}", response.error_code);
    let original = image::open(f.0.join("input.tmp"));
    assert!(
        original.is_err(),
        "old path-based decode must not be the implementation"
    );
    let bytes = std::fs::read(f.0.join("input.tmp")).unwrap();
    let original = image::load_from_memory(&bytes).unwrap().to_rgba8();
    let marked = image::open(f.0.join("text.png")).unwrap().to_rgba8();
    assert_ne!(marked, original);
    let mut request = f.request("watermark", "image.png", "PNG");
    request["watermark"] = json!({"kind":"image","imagePath":f.0.join("input.tmp"),
        "fontSize":16,"opacity":0.5,"position":0,"color":[255,255,255,255],"padding":4,"scale":0.2});
    assert!(f.execute(request).success);
}

#[test]
fn sixteen_bit_resize_keeps_more_than_eight_bits() {
    let f = Fixture::new();
    DynamicImage::ImageLuma16(image::ImageBuffer::from_pixel(
        128,
        96,
        image::Luma([1234_u16]),
    ))
    .save_with_format(f.0.join("input.tmp"), image::ImageFormat::Png)
    .unwrap();
    assert!(f.execute(f.request("resize", "deep.png", "PNG")).success);
    let output = image::open(f.0.join("deep.png")).unwrap();
    assert_eq!(output.color(), image::ColorType::Rgba16);
    assert!(output.to_luma16().get_pixel(0, 0)[0].abs_diff(1234) <= 2);
}

#[test]
fn privacy_strip_does_not_reencode_lossy_pixels() {
    use image::ImageEncoder;
    let f = Fixture::new();
    let original =
        image::RgbImage::from_fn(64, 32, |x, y| image::Rgb([x as u8 * 3, y as u8 * 7, 80]));
    let exif = b"II\x2a\x00\x08\x00\x00\x00\x00\x00\x00\x00\x00\x00";
    let mut bytes = Vec::new();
    let mut encoder = image::codecs::jpeg::JpegEncoder::new_with_quality(&mut bytes, 75);
    encoder.set_exif_metadata(exif.to_vec()).unwrap();
    encoder.encode_image(&original).unwrap();
    let mut with_comment = bytes[..2].to_vec();
    let secret = b"PRIVATE_GPS_SENTINEL";
    with_comment.extend_from_slice(&[0xff, 0xfe]);
    with_comment.extend_from_slice(&((secret.len() + 2) as u16).to_be_bytes());
    with_comment.extend_from_slice(secret);
    with_comment.extend_from_slice(&bytes[2..]);
    std::fs::write(f.0.join("input.tmp"), &with_comment).unwrap();
    let before = image::load_from_memory(&with_comment).unwrap().to_rgb8();
    let response = f.execute(f.request("stripMetadata", "private.jpg", "JPEG"));
    assert!(response.success, "{:?}", response.error_code);
    let stripped = std::fs::read(f.0.join("private.jpg")).unwrap();
    assert!(!stripped.windows(secret.len()).any(|w| w == secret));
    assert!(!response.outputs[0].has_exif);
    assert_eq!(
        before,
        image::load_from_memory(&stripped).unwrap().to_rgb8()
    );
}

#[test]
fn all_eight_exif_orientations_are_applied_before_privacy_strip() {
    use image::ImageEncoder;
    let f = Fixture::new();
    let pixels =
        image::RgbImage::from_fn(3, 2, |x, y| image::Rgb([(x + y * 3) as u8 * 30, 100, 200]));
    // Expected row-major indexes independently describe TIFF orientation 1..8.
    let indexes = [
        vec![0, 1, 2, 3, 4, 5],
        vec![2, 1, 0, 5, 4, 3],
        vec![5, 4, 3, 2, 1, 0],
        vec![3, 4, 5, 0, 1, 2],
        vec![0, 3, 1, 4, 2, 5],
        vec![3, 0, 4, 1, 5, 2],
        vec![5, 2, 4, 1, 3, 0],
        vec![2, 5, 1, 4, 0, 3],
    ];
    for orientation in 1_u8..=8 {
        let mut exif = vec![
            b'I',
            b'I',
            42,
            0,
            8,
            0,
            0,
            0,
            1,
            0,
            0x12,
            1,
            3,
            0,
            1,
            0,
            0,
            0,
            orientation,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
        ];
        let mut bytes = Vec::new();
        let mut encoder = image::codecs::png::PngEncoder::new(&mut bytes);
        encoder
            .set_exif_metadata(std::mem::take(&mut exif))
            .unwrap();
        encoder
            .write_image(pixels.as_raw(), 3, 2, image::ExtendedColorType::Rgb8)
            .unwrap();
        std::fs::write(f.0.join("input.tmp"), bytes).unwrap();
        let name = format!("orientation-{orientation}.png");
        let response = f.execute(f.request("stripMetadata", &name, "PNG"));
        assert!(
            response.success,
            "orientation {orientation}: {:?}",
            response.error_code
        );
        let output = image::open(f.0.join(name)).unwrap().to_rgb8();
        assert_eq!(
            output.dimensions(),
            if orientation >= 5 { (2, 3) } else { (3, 2) }
        );
        assert_eq!(
            output.pixels().map(|p| p[0] / 30).collect::<Vec<_>>(),
            indexes[orientation as usize - 1]
        );
    }
}

#[test]
fn animations_keep_display_frames_loop_duration_and_static_preview() {
    use image::AnimationDecoder;
    use std::io::Cursor;
    let f = Fixture::new();
    let colors = [[240, 0, 0, 255], [0, 220, 0, 128], [0, 0, 200, 0]];
    let mut bytes = Vec::new();
    {
        let mut encoder = png::Encoder::new(&mut bytes, 16, 12);
        encoder.set_color(png::ColorType::Rgba);
        encoder.set_depth(png::BitDepth::Eight);
        encoder.set_animated(3, 4).unwrap();
        encoder.set_blend_op(png::BlendOp::Source).unwrap();
        let mut writer = encoder.write_header().unwrap();
        for (i, color) in colors.iter().enumerate() {
            writer.set_frame_delay((i as u16 + 1) * 10, 100).unwrap();
            writer.write_image_data(&color.repeat(16 * 12)).unwrap();
        }
        writer.finish().unwrap();
    }
    std::fs::write(f.0.join("input.tmp"), bytes).unwrap();
    for format in ["PNG", "WEBP", "GIF"] {
        let name = format!("animation-{format}");
        let response = f.execute(f.request("compress", &name, format));
        assert!(response.success, "{format}: {:?}", response.error_code);
        let bytes = std::fs::read(f.0.join(&name)).unwrap();
        let frames = match format {
            "PNG" => image::codecs::png::PngDecoder::new(Cursor::new(&bytes))
                .unwrap()
                .apng()
                .unwrap()
                .into_frames()
                .collect_frames()
                .unwrap(),
            "WEBP" => {
                let mut decoder =
                    image::codecs::webp::WebPDecoder::new(Cursor::new(&bytes)).unwrap();
                decoder.set_background_color(Rgba([0, 0, 0, 0])).unwrap();
                decoder.into_frames().collect_frames().unwrap()
            }
            _ => image::codecs::gif::GifDecoder::new(Cursor::new(&bytes))
                .unwrap()
                .into_frames()
                .collect_frames()
                .unwrap(),
        };
        assert_eq!(frames.len(), 3, "{format}");
        for (i, frame) in frames.iter().enumerate() {
            assert_eq!(
                frame.delay().numer_denom_ms(),
                ((i as u32 + 1) * 100, 1),
                "{format}"
            );
            let p = frame.buffer().get_pixel(0, 0);
            if format != "GIF" {
                assert_eq!(p[3], colors[i][3], "{format} frame {i}");
            }
            if colors[i][3] == 255 {
                assert!(p[0].abs_diff(colors[i][0]) < 8);
            }
        }
        let mut request = f.request("thumbnail", &format!("preview-{format}.png"), "PNG");
        request["inputPath"] = json!(f.0.join(name));
        request["outputs"][0]["firstFrame"] = json!(true);
        assert!(f.execute(request).success);
    }
}

#[test]
fn malformed_inputs_never_panic_or_leave_outputs() {
    let f = Fixture::new();
    let seed = std::fs::read(f.0.join("input.tmp")).unwrap();
    for length in [0, 1, 7, 8, 16, 32, seed.len() / 2] {
        std::fs::write(f.0.join("input.tmp"), &seed[..length]).unwrap();
        let result = f.execute(f.request("resize", "bad.png", "PNG"));
        assert!(!result.success);
        assert!(!f.0.join("bad.png").exists());
    }
}

#[test]
fn mutation_corpus_runs_in_bounded_child_process() {
    use std::time::{Duration, Instant};
    let mut child = std::process::Command::new(std::env::current_exe().unwrap())
        .args([
            "--exact",
            "imaging::tests::mutation_fuzz_worker",
            "--nocapture",
        ])
        .env("RADISH_IMAGE_MUTATION_WORKER", "1")
        .spawn()
        .unwrap();
    let started = Instant::now();
    loop {
        if let Some(status) = child.try_wait().unwrap() {
            assert!(
                status.success(),
                "mutation worker failed; retain artifacts/native-fuzz"
            );
            break;
        }
        if started.elapsed() > Duration::from_secs(120) {
            child.kill().unwrap();
            child.wait().unwrap();
            panic!("mutation worker timed out; retain artifacts/native-fuzz");
        }
        std::thread::sleep(Duration::from_millis(20));
    }
}

#[test]
fn mutation_fuzz_worker() {
    if std::env::var_os("RADISH_IMAGE_MUTATION_WORKER").is_none() {
        return;
    }
    let f = Fixture::new();
    let evidence = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../artifacts/native-fuzz");
    std::fs::create_dir_all(&evidence).unwrap();
    let retained = evidence.join(format!("current-{}.bin", std::process::id()));
    for format in ["JPEG", "PNG", "WEBP", "GIF", "BMP", "ICO"] {
        assert!(f.execute(f.request("compress", format, format)).success);
        let seed = std::fs::read(f.0.join(format)).unwrap();
        for case in 0..128usize {
            let mut bytes = seed.clone();
            if case % 4 == 0 {
                bytes.truncate(case * bytes.len() / 128);
            } else {
                for offset in 0..1 + case % 8 {
                    let index = (case * 7919 + offset * 104729) % bytes.len();
                    bytes[index] ^= (case as u8).wrapping_mul(31).wrapping_add(1);
                }
            }
            // A crash / abort / timeout leaves the exact most recent input available.
            std::fs::write(&retained, &bytes).unwrap();
            std::fs::write(f.0.join("mutated.bin"), bytes).unwrap();
            let mut request = f.request("resize", "mutation.png", "PNG");
            request["inputPath"] = json!(f.0.join("mutated.bin"));
            request["limits"] = json!({"maxDimension":512,"maxPixels":262144,
                "maxFrames":8,"maxTotalPixels":1048576,"maxWorkingBytes":67108864});
            let result = f.execute(request);
            assert!(
                !matches!(result.error_code, Some(model::Error::Internal)),
                "internal failure: {format} case {case}"
            );
            if result.success {
                image::open(f.0.join("mutation.png")).unwrap();
                std::fs::remove_file(f.0.join("mutation.png")).unwrap();
            } else {
                assert!(!f.0.join("mutation.png").exists());
            }
        }
    }
    std::fs::remove_file(retained).unwrap();
}
