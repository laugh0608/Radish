import Foundation

enum RadishNativeAuthCallbackPayloads {
  static func payload(for url: URL) -> String? {
    guard
      let components = URLComponents(url: url, resolvingAgainstBaseURL: false),
      components.scheme?.lowercased() == "radish",
      components.host?.lowercased() == "oidc"
    else {
      return nil
    }

    let values = Dictionary(
      components.queryItems?.map { ($0.name, $0.value ?? "") } ?? [],
      uniquingKeysWith: { first, _ in first }
    )
    let payload: [String: String]
    switch components.path {
    case "/callback":
      payload = compactPayload(
        type: "login",
        values: [
          "code": values["code"],
          "state": values["state"],
          "error": values["error"],
          "errorDescription": values["error_description"],
        ]
      )
    case "/logout-complete":
      payload = ["type": "logout"]
    default:
      return nil
    }

    guard let data = try? JSONSerialization.data(
      withJSONObject: payload,
      options: [.sortedKeys]
    ) else {
      return nil
    }
    return String(data: data, encoding: .utf8)
  }

  private static func compactPayload(
    type: String,
    values: [String: String?]
  ) -> [String: String] {
    var payload = ["type": type]
    for (key, value) in values {
      guard
        let normalized = value?.trimmingCharacters(
          in: .whitespacesAndNewlines
        ),
        !normalized.isEmpty
      else {
        continue
      }
      payload[key] = normalized
    }
    return payload
  }
}
