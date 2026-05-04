use std::io::{Read, Write};
use std::net::TcpListener;
use std::process::Command;
use std::sync::Mutex;
use std::thread;
use std::time::{Duration, Instant};

use serde::Serialize;
use tauri::{Emitter, Manager, WindowEvent};
use tauri_plugin_deep_link::DeepLinkExt;

#[derive(Default)]
struct PendingDeepLinks(Mutex<Vec<String>>);

#[derive(Serialize, Clone)]
struct DeepLinkPayload {
    urls: Vec<String>,
}

#[derive(Serialize, Clone)]
struct OidcLoopbackPayload {
    urls: Vec<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct OidcLoopbackListenerInfo {
    redirect_uri: String,
}

#[derive(Serialize)]
struct TauriSpikeInfo {
    app: &'static str,
    shell: &'static str,
}

const OIDC_LOOPBACK_HOST: &str = "127.0.0.1";
const OIDC_LOOPBACK_PORT: u16 = 48801;
const OIDC_LOOPBACK_TIMEOUT: Duration = Duration::from_secs(5 * 60);
const OIDC_CALLBACK_PATH: &str = "/oidc/callback";
const OIDC_LOGOUT_COMPLETE_PATH: &str = "/oidc/logout-complete";

fn collect_radish_urls(args: impl IntoIterator<Item = String>) -> Vec<String> {
    args.into_iter()
        .filter(|arg| arg.starts_with("radish://"))
        .collect()
}

fn push_deep_links(app: &tauri::AppHandle, urls: Vec<String>) {
    if urls.is_empty() {
        return;
    }

    if let Some(state) = app.try_state::<PendingDeepLinks>() {
        if let Ok(mut pending) = state.0.lock() {
            pending.extend(urls.clone());
        }
    }

    let _ = app.emit("radish-deep-link", DeepLinkPayload { urls });
}

fn push_oidc_loopback_urls(app: &tauri::AppHandle, urls: Vec<String>) {
    if urls.is_empty() {
        return;
    }

    if let Some(state) = app.try_state::<PendingDeepLinks>() {
        if let Ok(mut pending) = state.0.lock() {
            pending.extend(urls.clone());
        }
    }

    let _ = app.emit("radish-oidc-loopback", OidcLoopbackPayload { urls });
}

#[tauri::command]
fn get_tauri_spike_info() -> TauriSpikeInfo {
    TauriSpikeInfo {
        app: "radish-tauri",
        shell: "tauri-desktop-spike",
    }
}

#[tauri::command]
fn take_pending_deep_links(
    state: tauri::State<'_, PendingDeepLinks>,
) -> Result<Vec<String>, String> {
    let mut pending = state
        .0
        .lock()
        .map_err(|_| "Failed to read pending deep links.".to_string())?;

    Ok(std::mem::take(&mut *pending))
}

#[tauri::command]
fn open_external_url(url: String) -> Result<(), String> {
    let parsed = url::Url::parse(&url).map_err(|err| format!("Invalid URL: {err}"))?;
    match parsed.scheme() {
        "http" | "https" => {}
        scheme => return Err(format!("Unsupported external URL scheme: {scheme}")),
    }

    open_url_with_system_browser(&url)
}

fn build_loopback_redirect_uri(path: &str) -> Result<String, String> {
    match path {
        OIDC_CALLBACK_PATH | OIDC_LOGOUT_COMPLETE_PATH => Ok(format!(
            "http://{OIDC_LOOPBACK_HOST}:{OIDC_LOOPBACK_PORT}{path}"
        )),
        _ => Err(format!("Unsupported OIDC loopback path: {path}")),
    }
}

fn parse_loopback_request_url(request_line: &str) -> Option<url::Url> {
    let target = request_line.split_whitespace().nth(1)?;
    if target.starts_with("http://") || target.starts_with("https://") {
        return url::Url::parse(target).ok();
    }

    url::Url::parse(&format!(
        "http://{OIDC_LOOPBACK_HOST}:{OIDC_LOOPBACK_PORT}{target}"
    ))
    .ok()
}

fn write_loopback_response(stream: &mut std::net::TcpStream, status: &str, body: &str) {
    let response = format!(
        "HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\nContent-Length: {}\r\n\r\n{body}",
        body.as_bytes().len()
    );

    let _ = stream.write_all(response.as_bytes());
    let _ = stream.flush();
}

fn accept_oidc_loopback_once(
    app: tauri::AppHandle,
    listener: TcpListener,
    expected_path: String,
) -> Result<(), String> {
    listener
        .set_nonblocking(true)
        .map_err(|err| format!("Failed to configure OIDC loopback listener: {err}"))?;

    let started_at = Instant::now();
    while started_at.elapsed() <= OIDC_LOOPBACK_TIMEOUT {
        match listener.accept() {
            Ok((mut stream, _addr)) => {
                let mut buffer = [0_u8; 8192];
                let bytes_read = stream
                    .read(&mut buffer)
                    .map_err(|err| format!("Failed to read OIDC loopback request: {err}"))?;
                let request = String::from_utf8_lossy(&buffer[..bytes_read]);
                let request_line = request.lines().next().unwrap_or_default();
                let Some(url) = parse_loopback_request_url(request_line) else {
                    write_loopback_response(
                        &mut stream,
                        "400 Bad Request",
                        "<!doctype html><title>Radish</title><p>Invalid Radish desktop callback.</p>",
                    );
                    continue;
                };

                if url.path() != expected_path {
                    write_loopback_response(
                        &mut stream,
                        "404 Not Found",
                        "<!doctype html><title>Radish</title><p>Radish desktop callback path was not found.</p>",
                    );
                    continue;
                }

                write_loopback_response(
                    &mut stream,
                    "200 OK",
                    "<!doctype html><title>Radish</title><p>Radish desktop login completed. You can return to the desktop app.</p>",
                );
                push_oidc_loopback_urls(&app, vec![url.to_string()]);
                return Ok(());
            }
            Err(err) if err.kind() == std::io::ErrorKind::WouldBlock => {
                thread::sleep(Duration::from_millis(100));
            }
            Err(err) => {
                return Err(format!("Failed to accept OIDC loopback request: {err}"));
            }
        }
    }

    Err("OIDC loopback listener timed out.".to_string())
}

#[tauri::command]
fn start_oidc_loopback_listener(
    app: tauri::AppHandle,
    path: String,
) -> Result<OidcLoopbackListenerInfo, String> {
    let redirect_uri = build_loopback_redirect_uri(&path)?;
    let listener = TcpListener::bind((OIDC_LOOPBACK_HOST, OIDC_LOOPBACK_PORT)).map_err(|err| {
        format!(
            "Failed to bind OIDC loopback listener on {OIDC_LOOPBACK_HOST}:{OIDC_LOOPBACK_PORT}: {err}"
        )
    })?;

    thread::spawn(move || {
        if let Err(err) = accept_oidc_loopback_once(app, listener, path) {
            eprintln!("[RadishTauriSpike] {err}");
        }
    });

    Ok(OidcLoopbackListenerInfo { redirect_uri })
}

#[cfg(target_os = "windows")]
fn open_url_with_system_browser(url: &str) -> Result<(), String> {
    Command::new("rundll32")
        .args(["url.dll,FileProtocolHandler", url])
        .spawn()
        .map(|_| ())
        .map_err(|err| format!("Failed to open system browser: {err}"))
}

#[cfg(target_os = "macos")]
fn open_url_with_system_browser(url: &str) -> Result<(), String> {
    Command::new("open")
        .arg(url)
        .spawn()
        .map(|_| ())
        .map_err(|err| format!("Failed to open system browser: {err}"))
}

#[cfg(all(unix, not(target_os = "macos")))]
fn open_url_with_system_browser(url: &str) -> Result<(), String> {
    Command::new("xdg-open")
        .arg(url)
        .spawn()
        .map(|_| ())
        .map_err(|err| format!("Failed to open system browser: {err}"))
}

#[cfg(not(any(target_os = "windows", target_os = "macos", unix)))]
fn open_url_with_system_browser(_url: &str) -> Result<(), String> {
    Err("Opening external URLs is not supported on this platform.".to_string())
}

pub fn run() {
    tauri::Builder::default()
        .manage(PendingDeepLinks::default())
        .plugin(tauri_plugin_single_instance::init(|app, args, _cwd| {
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.show();
                let _ = window.set_focus();
            }

            push_deep_links(app, collect_radish_urls(args));
        }))
        .plugin(tauri_plugin_deep_link::init())
        .plugin(tauri_plugin_opener::init())
        .setup(|app| {
            let handle = app.handle().clone();
            app.deep_link().on_open_url(move |event| {
                let urls = event.urls().iter().map(ToString::to_string).collect();
                push_deep_links(&handle, urls);
            });

            Ok(())
        })
        .on_window_event(|window, event| match event {
            WindowEvent::CloseRequested { .. } => {
                println!("[RadishTauriSpike] close requested: {}", window.label());
            }
            WindowEvent::Focused(focused) => {
                println!(
                    "[RadishTauriSpike] focus changed: {} focused={focused}",
                    window.label()
                );
            }
            WindowEvent::Resized(size) => {
                println!(
                    "[RadishTauriSpike] resized: {} {}x{}",
                    window.label(),
                    size.width,
                    size.height
                );
            }
            _ => {}
        })
        .invoke_handler(tauri::generate_handler![
            get_tauri_spike_info,
            open_external_url,
            start_oidc_loopback_listener,
            take_pending_deep_links
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Radish Tauri desktop spike");
}
