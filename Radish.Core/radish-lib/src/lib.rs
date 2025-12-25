use std::ffi::{CStr, CString};
use std::os::raw::c_char;

mod image;
mod hash;
mod utils;
pub mod benchmark;  // 性能测试模块（从 test_lib 迁移）

/// Add text watermark to an image
///
/// # Parameters
/// - `input_path`: Path to the input image
/// - `output_path`: Path to save the watermarked image
/// - `text`: Watermark text
/// - `font_size`: Font size in pixels
/// - `opacity`: Opacity (0.0 - 1.0)
/// - `position`: Position (0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight, 4=Center)
///
/// # Returns
/// - 0: Success
/// - -1: Error
#[unsafe(no_mangle)]
pub extern "C" fn add_text_watermark(
    input_path: *const c_char,
    output_path: *const c_char,
    text: *const c_char,
    font_size: u32,
    opacity: f32,
    position: u8,
) -> i32 {
    // Safety: Convert C strings to Rust strings
    let input = match unsafe { CStr::from_ptr(input_path).to_str() } {
        Ok(s) => s,
        Err(e) => {
            eprintln!("Invalid input path: {}", e);
            return -1;
        }
    };

    let output = match unsafe { CStr::from_ptr(output_path).to_str() } {
        Ok(s) => s,
        Err(e) => {
            eprintln!("Invalid output path: {}", e);
            return -1;
        }
    };

    let watermark_text = match unsafe { CStr::from_ptr(text).to_str() } {
        Ok(s) => s,
        Err(e) => {
            eprintln!("Invalid watermark text: {}", e);
            return -1;
        }
    };

    // Call internal implementation
    match image::watermark::add_watermark(input, output, watermark_text, font_size, opacity, position) {
        Ok(_) => 0,
        Err(e) => {
            eprintln!("Watermark error: {}", e);
            -1
        }
    }
}

/// Calculate SHA256 hash of a file
///
/// # Parameters
/// - `file_path`: Path to the file
/// - `hash_output`: Buffer to store the hash (must be at least 65 bytes)
/// - `output_len`: Length of the output buffer
///
/// # Returns
/// - 0: Success
/// - -1: Error
/// - -2: Buffer too small
#[unsafe(no_mangle)]
pub extern "C" fn calculate_file_sha256(
    file_path: *const c_char,
    hash_output: *mut c_char,
    output_len: usize,
) -> i32 {
    let path = match unsafe { CStr::from_ptr(file_path).to_str() } {
        Ok(s) => s,
        Err(e) => {
            eprintln!("Invalid file path: {}", e);
            return -1;
        }
    };

    match hash::file_hash::calculate_sha256(path) {
        Ok(hash) => {
            let c_hash = match CString::new(hash) {
                Ok(s) => s,
                Err(e) => {
                    eprintln!("Failed to create C string: {}", e);
                    return -1;
                }
            };

            let bytes = c_hash.as_bytes_with_nul();
            if bytes.len() <= output_len {
                unsafe {
                    std::ptr::copy_nonoverlapping(bytes.as_ptr(), hash_output as *mut u8, bytes.len());
                }
                0
            } else {
                eprintln!("Buffer too small: need {} bytes, got {}", bytes.len(), output_len);
                -2
            }
        }
        Err(e) => {
            eprintln!("Hash calculation error: {}", e);
            -1
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_basic() {
        // Basic smoke test
        assert_eq!(2 + 2, 4);
    }
}
