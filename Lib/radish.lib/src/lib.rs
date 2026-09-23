use std::ffi::{CStr, CString};
use std::os::raw::c_char;

pub mod benchmark;
mod hash;
mod image;
mod utils; // 性能测试模块（从 test_lib 迁移）

// FFI 错误只通过返回码交给宿主记录；禁止在这里打印路径、文本或底层异常。

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
        Err(_) => {
            return -1;
        }
    };

    let output = match unsafe { CStr::from_ptr(output_path).to_str() } {
        Ok(s) => s,
        Err(_) => {
            return -1;
        }
    };

    let watermark_text = match unsafe { CStr::from_ptr(text).to_str() } {
        Ok(s) => s,
        Err(_) => {
            return -1;
        }
    };

    // Call internal implementation
    match image::watermark::add_watermark(
        input,
        output,
        watermark_text,
        font_size,
        opacity,
        position,
    ) {
        Ok(_) => 0,
        Err(_) => -1,
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
        Err(_) => {
            return -1;
        }
    };

    match hash::file_hash::calculate_sha256(path) {
        Ok(hash) => {
            let c_hash = match CString::new(hash) {
                Ok(s) => s,
                Err(_) => {
                    return -1;
                }
            };

            let bytes = c_hash.as_bytes_with_nul();
            if bytes.len() <= output_len {
                unsafe {
                    std::ptr::copy_nonoverlapping(
                        bytes.as_ptr(),
                        hash_output as *mut u8,
                        bytes.len(),
                    );
                }
                0
            } else {
                -2
            }
        }
        Err(_) => -1,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ffi_errors_are_silent_and_keep_return_codes() {
        // 子进程以 --nocapture 执行真实 FFI；捕获原生 stderr，防止测试框架掩盖裸输出。
        let output = std::process::Command::new(std::env::current_exe().unwrap())
            .args(["--exact", "tests::ffi_error_probe", "--nocapture"])
            .env("RADISH_FFI_ERROR_PROBE", "1")
            .output()
            .unwrap();
        assert!(output.status.success(), "FFI probe failed");
        assert!(output.stderr.is_empty(), "FFI must not print diagnostics");
        assert!(!String::from_utf8_lossy(&output.stdout).contains("FFI_PRIVATE_SENTINEL"));
    }

    #[test]
    fn ffi_error_probe() {
        if std::env::var_os("RADISH_FFI_ERROR_PROBE").is_none() {
            return;
        }
        let directory =
            std::env::temp_dir().join(format!("radish-ffi-probe-{}", std::process::id()));
        std::fs::create_dir_all(&directory).unwrap();
        let missing =
            CString::new(directory.join("FFI_PRIVATE_SENTINEL").to_str().unwrap()).unwrap();
        let invalid_utf8 = [0xff_u8, 0];
        let mut buffer = [42 as c_char; 65];
        assert_eq!(
            calculate_file_sha256(missing.as_ptr(), buffer.as_mut_ptr(), buffer.len()),
            -1
        );
        assert_eq!(
            calculate_file_sha256(
                invalid_utf8.as_ptr().cast(),
                buffer.as_mut_ptr(),
                buffer.len()
            ),
            -1
        );
        assert_eq!(buffer, [42 as c_char; 65]);
        assert_eq!(
            add_text_watermark(
                missing.as_ptr(),
                missing.as_ptr(),
                missing.as_ptr(),
                12,
                0.5,
                0
            ),
            -1
        );
        assert_eq!(
            add_text_watermark(
                invalid_utf8.as_ptr().cast(),
                missing.as_ptr(),
                missing.as_ptr(),
                12,
                0.5,
                0
            ),
            -1
        );
        assert_eq!(
            add_text_watermark(
                missing.as_ptr(),
                invalid_utf8.as_ptr().cast(),
                missing.as_ptr(),
                12,
                0.5,
                0
            ),
            -1
        );
        assert_eq!(
            add_text_watermark(
                missing.as_ptr(),
                missing.as_ptr(),
                invalid_utf8.as_ptr().cast(),
                12,
                0.5,
                0
            ),
            -1
        );

        let file = directory.join("hash.txt");
        std::fs::write(&file, b"abc").unwrap();
        let path = CString::new(file.to_str().unwrap()).unwrap();
        assert_eq!(
            calculate_file_sha256(path.as_ptr(), buffer.as_mut_ptr(), 64),
            -2
        );
        assert_eq!(buffer, [42 as c_char; 65]);
        assert_eq!(
            calculate_file_sha256(path.as_ptr(), buffer.as_mut_ptr(), 65),
            0
        );
        let hash = unsafe { CStr::from_ptr(buffer.as_ptr()) }.to_str().unwrap();
        assert_eq!(
            hash,
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
        );
        std::fs::remove_file(file).unwrap();
        std::fs::remove_dir(directory).unwrap();
    }
}
