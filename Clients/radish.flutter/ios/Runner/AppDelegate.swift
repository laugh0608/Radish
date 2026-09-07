import Flutter
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  private var pendingAuthCallbackPayload: String?

  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)

    let messenger = engineBridge.applicationRegistrar.messenger()
    FlutterMethodChannel(
      name: "radish.flutter/native_auth",
      binaryMessenger: messenger
    ).setMethodCallHandler { [weak self] call, result in
      self?.handleNativeAuthCall(call, result: result)
    }

    FlutterMethodChannel(
      name: "radish.flutter/forum_follow_up",
      binaryMessenger: messenger
    ).setMethodCallHandler { call, result in
      if call.method == "takePendingHandoff" {
        result(nil)
      } else {
        result(FlutterMethodNotImplemented)
      }
    }
  }

  override func application(
    _ app: UIApplication,
    open url: URL,
    options: [UIApplication.OpenURLOptionsKey: Any] = [:]
  ) -> Bool {
    if receiveAuthCallback(url) {
      return true
    }
    return super.application(app, open: url, options: options)
  }

  func receiveAuthCallback(_ url: URL) -> Bool {
    guard let payload = RadishNativeAuthCallbackPayloads.payload(for: url) else {
      return false
    }

    pendingAuthCallbackPayload = payload
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

      UIApplication.shared.open(url) { opened in
        if opened {
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
      }
    case "takePendingCallback":
      let payload = pendingAuthCallbackPayload
      pendingAuthCallbackPayload = nil
      result(payload)
    default:
      result(FlutterMethodNotImplemented)
    }
  }
}
