use super::model::*;
use ab_glyph::{Font, FontVec, GlyphId, VariableFont};
use image::{Pixel, Rgba, RgbaImage};
use unicode_segmentation::UnicodeSegmentation;

struct FontAsset {
    data: Vec<u8>,
    raster: FontVec,
    synthetic_bold: bool,
}

pub fn render(options: &Watermark, ctx: &Context<'_>) -> Result<RgbaImage> {
    if options.text.trim().is_empty()
        || options.text.len() > 8192
        || options.font_paths.is_empty()
        || options.font_paths.len() > 8
    {
        return Err(Error::InvalidArgument);
    }
    let mut fonts = Vec::new();
    let mut font_bytes = 0_u64;
    for path in &options.font_paths {
        ctx.check()?;
        let metadata = std::fs::metadata(path).map_err(|_| Error::FontUnavailable)?;
        if metadata.len() > 32 * 1024 * 1024 {
            return Err(Error::LimitExceeded);
        }
        font_bytes += metadata.len() * 2;
        if font_bytes > ctx.limits.max_working_bytes / 4 {
            return Err(Error::LimitExceeded);
        }
        let data = std::fs::read(path).map_err(|_| Error::FontUnavailable)?;
        let mut raster = FontVec::try_from_vec(data.clone()).map_err(|_| Error::FontUnavailable)?;
        let variable = raster.set_variation(b"wght", if options.bold { 700.0 } else { 400.0 });
        fonts.push(FontAsset {
            data,
            raster,
            synthetic_bold: options.bold && !variable,
        });
    }
    let mut outlines = Vec::new();
    let mut baseline = options.font_size;
    let mut min_x = 0_f32;
    let mut min_y = 0_f32;
    let mut max_x = 0_f32;
    let mut max_y = 0_f32;
    let mut glyph_count = 0;
    for line in options.text.replace("\r\n", "\n").split('\n') {
        let bidi = unicode_bidi::BidiInfo::new(line, None);
        let mut x = 0_f32;
        for paragraph in &bidi.paragraphs {
            let (levels, runs) = bidi.visual_runs(paragraph, paragraph.range.clone());
            for run in runs {
                let rtl = levels[run.start].is_rtl();
                let mut groups: Vec<(usize, String)> = vec![];
                for grapheme in line[run.clone()].graphemes(true) {
                    let index = fonts
                        .iter()
                        .position(|font| {
                            grapheme.chars().all(|ch| {
                                ch.is_whitespace()
                                    || ch == '\u{200d}'
                                    || ('\u{fe00}'..='\u{fe0f}').contains(&ch)
                                    || font.raster.glyph_id(ch).0 != 0
                            })
                        })
                        .ok_or(Error::MissingGlyph)?;
                    if let Some((previous, text)) = groups.last_mut().filter(|g| g.0 == index) {
                        let _ = previous;
                        text.push_str(grapheme);
                    } else {
                        groups.push((index, grapheme.to_owned()));
                    }
                }
                if rtl {
                    groups.reverse();
                }
                for (index, text) in groups {
                    ctx.check()?;
                    let font = &fonts[index];
                    let mut face =
                        rustybuzz::Face::from_slice(&font.data, 0).ok_or(Error::FontUnavailable)?;
                    face.set_variations(&[rustybuzz::Variation {
                        tag: rustybuzz::ttf_parser::Tag::from_bytes(b"wght"),
                        value: if options.bold { 700.0 } else { 400.0 },
                    }]);
                    let factor = options.font_size / face.units_per_em() as f32;
                    let scale = options.font_size * font.raster.height_unscaled()
                        / font.raster.units_per_em().ok_or(Error::FontUnavailable)?;
                    let mut buffer = rustybuzz::UnicodeBuffer::new();
                    buffer.push_str(&text);
                    buffer.set_direction(if rtl {
                        rustybuzz::Direction::RightToLeft
                    } else {
                        rustybuzz::Direction::LeftToRight
                    });
                    buffer.guess_segment_properties();
                    let glyphs = rustybuzz::shape(&face, &[], buffer);
                    for (glyph, position) in
                        glyphs.glyph_infos().iter().zip(glyphs.glyph_positions())
                    {
                        glyph_count += 1;
                        if glyph_count > 4096 {
                            return Err(Error::LimitExceeded);
                        }
                        if glyph.glyph_id == 0 {
                            return Err(Error::MissingGlyph);
                        }
                        let glyph = GlyphId(glyph.glyph_id as u16).with_scale_and_position(
                            scale,
                            ab_glyph::point(
                                x + position.x_offset as f32 * factor,
                                baseline - position.y_offset as f32 * factor,
                            ),
                        );
                        if let Some(outline) = font.raster.outline_glyph(glyph) {
                            let bounds = outline.px_bounds();
                            min_x = min_x.min(bounds.min.x);
                            min_y = min_y.min(bounds.min.y);
                            max_x = max_x.max(bounds.max.x);
                            max_y = max_y.max(bounds.max.y);
                            outlines.push((outline, font.synthetic_bold));
                        }
                        x += position.x_advance as f32 * factor;
                    }
                }
            }
        }
        max_x = max_x.max(x);
        baseline += options.font_size * 1.25;
    }
    let shear_width = if options.italic {
        (max_y - min_y) * 0.2
    } else {
        0.0
    };
    let width = (max_x - min_x + shear_width).ceil() as u32 + 2;
    let height = (max_y - min_y).ceil() as u32 + 2;
    let pixels = ctx.limits.dimensions(width, height)?;
    if pixels * 4 > ctx.limits.max_working_bytes / 8 {
        return Err(Error::LimitExceeded);
    }
    let mut mask = RgbaImage::new(width, height);
    for (outline, bold) in outlines {
        ctx.check()?;
        let bounds = outline.px_bounds();
        outline.draw(|dx, dy, coverage| {
            let y = (bounds.min.y - min_y) as u32 + dy;
            let shear = if options.italic {
                ((height - y) as f32 * 0.2) as u32
            } else {
                0
            };
            let x = (bounds.min.x - min_x) as u32 + dx + shear;
            let alpha = (coverage * f32::from(options.color[3]) * options.opacity).round() as u8;
            let color = Rgba([options.color[0], options.color[1], options.color[2], alpha]);
            if x < width && y < height {
                mask.get_pixel_mut(x, y).blend(&color);
            }
            if bold && x + 1 < width && y < height {
                mask.get_pixel_mut(x + 1, y).blend(&color);
            }
        });
    }
    Ok(mask)
}
