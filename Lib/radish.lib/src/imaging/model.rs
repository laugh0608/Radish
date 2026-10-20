use image::DynamicImage;
use serde::{Deserialize, Serialize};
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Instant;

pub type Result<T> = std::result::Result<T, Error>;

#[derive(Debug, Clone, Copy, Serialize)]
pub enum Error {
    InvalidArgument,
    InvalidImage,
    UnsupportedFormat,
    UnsupportedFeature,
    LimitExceeded,
    FontUnavailable,
    MissingGlyph,
    IoRead,
    IoWrite,
    Cancelled,
    #[serde(rename = "InternalError")]
    Internal,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Request {
    pub schema_version: u32,
    pub operation: String,
    pub input_path: String,
    #[serde(default)]
    pub outputs: Vec<Output>,
    #[serde(default)]
    pub watermark: Option<Watermark>,
    #[serde(default)]
    pub limits: Limits,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Output {
    pub path: String,
    pub format: String,
    #[serde(default)]
    pub width: u32,
    #[serde(default)]
    pub height: u32,
    #[serde(default = "yes")]
    pub keep_aspect_ratio: bool,
    #[serde(default = "quality")]
    pub quality: u8,
    #[serde(default)]
    pub first_frame: bool,
}
fn yes() -> bool {
    true
}
fn quality() -> u8 {
    85
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Watermark {
    pub kind: String,
    #[serde(default)]
    pub text: String,
    #[serde(default)]
    pub image_path: String,
    #[serde(default)]
    pub font_paths: Vec<String>,
    pub font_size: f32,
    #[serde(default)]
    pub bold: bool,
    #[serde(default)]
    pub italic: bool,
    pub opacity: f32,
    pub position: u8,
    pub color: [u8; 4],
    pub padding: u32,
    pub scale: f32,
}

#[derive(Debug, Deserialize, Clone)]
#[serde(rename_all = "camelCase", default, deny_unknown_fields)]
pub struct Limits {
    pub max_input_bytes: u64,
    pub max_dimension: u32,
    pub max_pixels: u64,
    pub max_frames: usize,
    pub max_total_pixels: u64,
    pub max_working_bytes: u64,
    pub max_output_bytes: u64,
    pub timeout_ms: u64,
}
impl Default for Limits {
    fn default() -> Self {
        Self {
            max_input_bytes: 5 * 1024 * 1024,
            max_dimension: 16384,
            max_pixels: 40_000_000,
            max_frames: 300,
            max_total_pixels: 120_000_000,
            max_working_bytes: 512 * 1024 * 1024,
            max_output_bytes: 32 * 1024 * 1024,
            timeout_ms: 30_000,
        }
    }
}
impl Limits {
    pub fn dimensions(&self, width: u32, height: u32) -> Result<u64> {
        let pixels = u64::from(width) * u64::from(height);
        if width == 0
            || height == 0
            || width > self.max_dimension
            || height > self.max_dimension
            || pixels > self.max_pixels
        {
            return Err(Error::LimitExceeded);
        }
        Ok(pixels)
    }
    pub fn validate(&self) -> Result<()> {
        let maximum = Self::default();
        if self.max_input_bytes == 0
            || self.max_input_bytes > 50 * 1024 * 1024
            || self.max_dimension == 0
            || self.max_dimension > maximum.max_dimension
            || self.max_pixels == 0
            || self.max_pixels > maximum.max_pixels
            || self.max_frames == 0
            || self.max_frames > maximum.max_frames
            || self.max_total_pixels == 0
            || self.max_total_pixels > maximum.max_total_pixels
            || self.max_working_bytes == 0
            || self.max_working_bytes > maximum.max_working_bytes
            || self.max_output_bytes == 0
            || self.max_output_bytes > maximum.max_output_bytes
            || self.timeout_ms == 0
            || self.timeout_ms > maximum.timeout_ms
        {
            return Err(Error::InvalidArgument);
        }
        Ok(())
    }
}

#[derive(Debug, Serialize, Default, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Info {
    pub width: u32,
    pub height: u32,
    pub display_width: u32,
    pub display_height: u32,
    pub format: String,
    pub file_size: u64,
    pub bits_per_pixel: u16,
    pub orientation: u8,
    pub has_exif: bool,
    pub has_icc_profile: bool,
    pub frame_count: usize,
    pub duration_ms: u64,
}
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Response {
    pub success: bool,
    pub error_code: Option<Error>,
    pub info: Option<Info>,
    pub outputs: Vec<Info>,
}
impl Response {
    pub fn fail(error: Error) -> Self {
        Self {
            success: false,
            error_code: Some(error),
            info: None,
            outputs: vec![],
        }
    }
}

pub struct Frame {
    pub image: DynamicImage,
    pub delay_ms: u32,
}
pub struct Document {
    pub info: Info,
    pub frames: Vec<Frame>,
    pub loop_count: u32,
    pub exif: Option<Vec<u8>>,
    pub icc: Option<Vec<u8>>,
    pub is_icon: bool,
}

pub struct Context<'a> {
    pub limits: &'a Limits,
    pub cancelled: &'a AtomicBool,
    pub started: Instant,
}
impl Context<'_> {
    pub fn check(&self) -> Result<()> {
        if self.cancelled.load(Ordering::Relaxed)
            || self.started.elapsed().as_millis() >= u128::from(self.limits.timeout_ms)
        {
            Err(Error::Cancelled)
        } else {
            Ok(())
        }
    }
}
