import Cocoa
import FlutterMacOS

@main
class AppDelegate: FlutterAppDelegate {
  private var nativeAuthChannel: FlutterMethodChannel?
  private let authCallbackStore = RadishNativeAuthCallbackStore()

  func configureNativeAuth(messenger: FlutterBinaryMessenger) {
    let channel = FlutterMethodChannel(
      name: "radish.flutter/native_auth",
      binaryMessenger: messenger
    )
    channel.setMethodCallHandler { [weak self] call, result in
      self?.handleNativeAuthCall(call, result: result)
    }
    nativeAuthChannel = channel
  }

  override func application(
    _ application: NSApplication,
    open urls: [URL]
  ) {
    var passthroughURLs: [URL] = []
    var receivedAuthCallback = false

    for url in urls {
      guard url.scheme?.lowercased() == "radish" else {
        passthroughURLs.append(url)
        continue
      }

      if !receivedAuthCallback {
        receivedAuthCallback = receiveAuthCallback(url)
      }
    }

    if !passthroughURLs.isEmpty {
      super.application(application, open: passthroughURLs)
    }
  }

  @discardableResult
  private func receiveAuthCallback(_ url: URL) -> Bool {
    guard authCallbackStore.receive(url) else {
      return false
    }

    mainFlutterWindow?.makeKeyAndOrderFront(nil)
    NSApp.activate(ignoringOtherApps: true)
    return true
  }

  private func takePendingAuthCallback() -> String? {
    return authCallbackStore.take()
  }

  override func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
    return true
  }

  override func applicationSupportsSecureRestorableState(_ app: NSApplication) -> Bool {
    return true
  }

  private func handleNativeAuthCall(
    _ call: FlutterMethodCall,
    result: @escaping FlutterResult
  ) {
    switch call.method {
    case "openAuthorizeUrl", "openLogoutUrl":
      guard
        let rawURL = call.arguments as? String,
        let url = URL(string: rawURL),
        let scheme = url.scheme?.lowercased(),
        scheme == "https" || scheme == "http"
      else {
        result(
          FlutterError(
            code: "invalid_url",
            message: "Authentication URL must use HTTP or HTTPS.",
            details: nil
          )
        )
        return
      }

      if NSWorkspace.shared.open(url) {
        result(nil)
      } else {
        result(
          FlutterError(
            code: "open_failed",
            message: "Unable to open authentication URL.",
            details: nil
          )
        )
      }
    case "takePendingCallback":
      result(takePendingAuthCallback())
    default:
      result(FlutterMethodNotImplemented)
    }
  }
}
