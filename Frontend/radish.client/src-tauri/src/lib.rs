use std::process::Command;
use std::sync::Mutex;

use serde::Serialize;
use tauri::{Emitter, Manager, WindowEvent};
use tauri_plugin_deep_link::DeepLinkExt;

#[derive(Default)]
struct PendingDeepLinks(Mutex<Vec<String>>);

#[derive(Serialize, Clone)]
struct DeepLinkPayload {
    urls: Vec<String>,
}

#[derive(Serialize)]
struct TauriSpikeInfo {
    app: &'static str,
    shell: &'static str,
}

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

#[tauri::command]
fn get_tauri_spike_info() -> TauriSpikeInfo {
    TauriSpikeInfo {
        app: "radish.client",
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

#[cfg(target_os = "windows")]
fn open_url_with_system_browser(url: &str) -> Result<(), String> {
    Command::new("cmd")
        .args(["/C", "start", "", url])
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
            take_pending_deep_links
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Radish Tauri desktop spike");
}
