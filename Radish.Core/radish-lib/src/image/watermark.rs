use image::{DynamicImage, GenericImageView, Rgba, RgbaImage};
use imageproc::drawing::draw_text_mut;
use rusttype::{Font, Scale};
use anyhow::{Result, Context};

/// Watermark position
#[derive(Debug, Clone, Copy)]
pub enum WatermarkPosition {
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center,
}

impl From<u8> for WatermarkPosition {
    fn from(value: u8) -> Self {
        match value {
            0 => WatermarkPosition::TopLeft,
            1 => WatermarkPosition::TopRight,
            2 => WatermarkPosition::BottomLeft,
            3 => WatermarkPosition::BottomRight,
            4 => WatermarkPosition::Center,
            _ => WatermarkPosition::BottomRight, // Default
        }
    }
}

/// Add text watermark to an image
pub fn add_watermark(
    input_path: &str,
    output_path: &str,
    text: &str,
    font_size: u32,
    opacity: f32,
    position: u8,
) -> Result<()> {
    // Load image
    let img = image::open(input_path)
        .context(format!("Failed to open image: ", input_path))?;

    // Convert to RGBA for watermark processing
    let mut img_rgba = img.to_rgba8();

    // Load embedded font (DejaVu Sans)
    // Note: You need to add a font file to the fonts directory
    // For now, we'll use a simple approach without external fonts
    let font_data = include_bytes!("../../fonts/DejaVuSans.ttf");
    let font = Font::try_from_bytes(font_data as &[u8])
        .context("Failed to load font")?;

    // Calculate text position
    let (img_width, img_height) = img_rgba.dimensions();
    let scale = Scale::uniform(font_size as f32);

    let position_enum = WatermarkPosition::from(position);
    let (x, y) = calculate_text_position(
        position_enum,
        img_width,
        img_height,
        text,
        &font,
        scale,
    );

    // Create watermark color with opacity
    let alpha = (255.0 * opacity.clamp(0.0, 1.0)) as u8;
    let color = Rgba([255u8, 255u8, 255u8, alpha]);

    // Draw text
    draw_text_mut(&mut img_rgba, color, x as i32, y as i32, scale, &font, text);

    // Save image
    img_rgba.save(output_path)
        .context(format!("Failed to save image: {}", output_path))?;

    Ok(())
}

/// Calculate text position based on watermark position
fn calculate_text_position(
    position: WatermarkPosition,
    img_width: u32,
    img_height: u32,
    text: &str,
    font: &Font,
    scale: Scale,
) -> (u32, u32) {
    // Estimate text width (rough approximation)
    let text_width = (text.len() as f32 * scale.x * 0.6) as u32;
    let text_height = scale.y as u32;

    let padding = 20u32;

    match position {
        WatermarkPosition::TopLeft => (padding, padding),
        WatermarkPosition::TopRight => {
            let x = img_width.saturating_sub(text_width + padding);
            (x, padding)
        }
        WatermarkPosition::BottomLeft => {
            let y = img_height.saturating_sub(text_height + padding);
            (padding, y)
        }
        WatermarkPosition::BottomRight => {
            let x = img_width.saturating_sub(text_width + padding);
            let y = img_height.saturating_sub(text_height + padding);
            (x, y)
        }
        WatermarkPosition::Center => {
            let x = (img_width / 2).saturating_sub(text_width / 2);
            let y = (img_height / 2).saturating_sub(text_height / 2);
            (x, y)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_position_conversion() {
        assert!(matches!(WatermarkPosition::from(0), WatermarkPosition::TopLeft));
        assert!(matches!(WatermarkPosition::from(3), WatermarkPosition::BottomRight));
    }
}
