//! Versioned control ABI. All pointers are owned by the trusted .NET binding.
use super::{engine, model::*};
use std::ffi::c_void;
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::sync::atomic::{AtomicBool, Ordering};

struct NativeResponse(Vec<u8>);
thread_local! { static IN_ABI: std::cell::Cell<bool> = const { std::cell::Cell::new(false) }; }
static PANIC_HOOK: std::sync::Once = std::sync::Once::new();
struct AbiScope(bool);
impl Drop for AbiScope {
    fn drop(&mut self) {
        IN_ABI.set(self.0);
    }
}
pub(crate) fn guard(action: impl FnOnce() -> i32) -> i32 {
    PANIC_HOOK.call_once(|| {
        let previous = std::panic::take_hook();
        std::panic::set_hook(Box::new(move |info| {
            if !IN_ABI.get() {
                previous(info);
            }
        }));
    });
    let _scope = AbiScope(IN_ABI.replace(true));
    catch_unwind(AssertUnwindSafe(action)).unwrap_or(-1)
}

#[unsafe(no_mangle)]
pub extern "C" fn radish_image_abi_version() -> u32 {
    0x00010000
}

/// # Safety
/// `output` must be writable for one pointer. The resulting handle is freed once.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn radish_image_cancel_create_v1(output: *mut *mut c_void) -> i32 {
    guard(|| {
        if output.is_null() {
            return -2;
        }
        unsafe {
            *output = Box::into_raw(Box::new(AtomicBool::new(false))).cast();
        }
        0
    })
}
/// # Safety
/// `handle` is a live cancellation handle; the owner keeps it alive during this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn radish_image_cancel_v1(handle: *mut c_void) -> i32 {
    guard(|| {
        if handle.is_null() {
            return -2;
        }
        unsafe { &*handle.cast::<AtomicBool>() }.store(true, Ordering::Relaxed);
        0
    })
}
/// # Safety
/// No execute or cancel operation may still reference this handle.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn radish_image_cancel_free_v1(handle: *mut c_void) -> i32 {
    guard(|| {
        if !handle.is_null() {
            unsafe {
                drop(Box::from_raw(handle.cast::<AtomicBool>()));
            }
        }
        0
    })
}
/// # Safety
/// `output` must be writable for one pointer. Free using response_free_v1.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn radish_image_capabilities_v1(output: *mut *mut c_void) -> i32 {
    guard(|| {
        if output.is_null() {
            return -2;
        }
        let data = br#"{"abi":65536,"formats":["JPEG","PNG","GIF","WEBP","BMP","ICO"],"operations":["info","validate","thumbnail","resize","compress","watermark","stripMetadata","multiple"],"animation":true,"webp":"1.6.0","lcms":"2.19.1"}"#.to_vec();
        unsafe {
            *output = Box::into_raw(Box::new(NativeResponse(data))).cast();
        }
        0
    })
}
/// # Safety
/// Request bytes remain readable for `request_len`; `cancel` is a live cancellation
/// handle; `output` is writable. Input pointers are borrowed only during this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn radish_image_execute_v1(
    request: *const u8,
    request_len: u64,
    cancel: *mut c_void,
    output: *mut *mut c_void,
) -> i32 {
    guard(|| {
        if output.is_null() {
            return -2;
        }
        unsafe {
            *output = std::ptr::null_mut();
        }
        if request.is_null() || cancel.is_null() || request_len == 0 || request_len > 65536 {
            return -2;
        }
        let bytes = unsafe { std::slice::from_raw_parts(request, request_len as usize) };
        let response = match serde_json::from_slice::<Request>(bytes) {
            Ok(request) => match catch_unwind(AssertUnwindSafe(|| {
                engine::execute(&request, unsafe { &*cancel.cast::<AtomicBool>() })
            })) {
                Ok(response) => response,
                Err(_) => Response::fail(Error::Internal),
            },
            Err(_) => Response::fail(Error::InvalidArgument),
        };
        let Ok(bytes) = serde_json::to_vec(&response) else {
            return -1;
        };
        if bytes.len() > 65536 {
            return -1;
        }
        unsafe {
            *output = Box::into_raw(Box::new(NativeResponse(bytes))).cast();
        }
        0
    })
}
/// # Safety
/// `handle` is live. Both output pointers are writable. Returned bytes are borrowed
/// until response_free_v1 and must not be mutated or freed by the caller.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn radish_image_response_data_v1(
    handle: *mut c_void,
    data: *mut *const u8,
    length: *mut u64,
) -> i32 {
    guard(|| {
        if handle.is_null() || data.is_null() || length.is_null() {
            return -2;
        }
        let response = unsafe { &*handle.cast::<NativeResponse>() };
        unsafe {
            *data = response.0.as_ptr();
            *length = response.0.len() as u64;
        }
        0
    })
}
/// # Safety
/// Free a response handle exactly once, after all borrowed data has been copied.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn radish_image_response_free_v1(handle: *mut c_void) -> i32 {
    guard(|| {
        if !handle.is_null() {
            unsafe {
                drop(Box::from_raw(handle.cast::<NativeResponse>()));
            }
        }
        0
    })
}

#[cfg(test)]
pub(crate) fn panic_guard_probe() -> i32 {
    guard(|| panic!("FFI_PRIVATE_SENTINEL"))
}

#[cfg(test)]
pub(crate) fn invalid_request_probe() -> i32 {
    unsafe {
        let mut cancel = std::ptr::null_mut();
        assert_eq!(radish_image_cancel_create_v1(&mut cancel), 0);
        let mut response = std::ptr::null_mut();
        let bytes = b"FFI_PRIVATE_SENTINEL";
        assert_eq!(
            radish_image_execute_v1(bytes.as_ptr(), bytes.len() as u64, cancel, &mut response),
            0
        );
        let mut data = std::ptr::null();
        let mut length = 0;
        assert_eq!(
            radish_image_response_data_v1(response, &mut data, &mut length),
            0
        );
        let value = std::slice::from_raw_parts(data, length as usize);
        assert!(!String::from_utf8_lossy(value).contains("PRIVATE"));
        assert_eq!(radish_image_response_free_v1(response), 0);
        radish_image_cancel_free_v1(cancel)
    }
}
