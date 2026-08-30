import Flutter
import UIKit
import XCTest
@testable import Runner

class RunnerTests: XCTestCase {
  func testAuthCallbackPayloadParsesLoginCode() throws {
    let payload = RadishNativeAuthCallbackPayloads.payload(
      for: try XCTUnwrap(
        URL(string: "radish://oidc/callback?code=native-code-1&state=native-state-1")
      )
    )

    XCTAssertEqual(
      try decode(payload),
      [
        "type": "login",
        "code": "native-code-1",
        "state": "native-state-1",
      ]
    )
  }

  func testAuthCallbackPayloadParsesBrowserCancellation() throws {
    let payload = RadishNativeAuthCallbackPayloads.payload(
      for: try XCTUnwrap(
        URL(
          string: "radish://oidc/callback?error=access_denied&error_description=User%20canceled&state=native-state-2"
        )
      )
    )

    XCTAssertEqual(
      try decode(payload),
      [
        "type": "login",
        "state": "native-state-2",
        "error": "access_denied",
        "errorDescription": "User canceled",
      ]
    )
  }

  func testAuthCallbackPayloadParsesLogoutCompletion() throws {
    let payload = RadishNativeAuthCallbackPayloads.payload(
      for: try XCTUnwrap(URL(string: "radish://oidc/logout-complete"))
    )

    XCTAssertEqual(try decode(payload), ["type": "logout"])
  }

  func testAuthCallbackPayloadRejectsUntrustedUris() throws {
    let urls = [
      "https://localhost/callback?code=1",
      "radish://other/callback?code=1",
      "radish://oidc/unknown?code=1",
    ]

    for rawURL in urls {
      XCTAssertNil(
        RadishNativeAuthCallbackPayloads.payload(
          for: try XCTUnwrap(URL(string: rawURL))
        )
      )
    }
  }

  private func decode(_ payload: String?) throws -> [String: String] {
    let data = try XCTUnwrap(payload?.data(using: .utf8))
    return try XCTUnwrap(
      JSONSerialization.jsonObject(with: data) as? [String: String]
    )
  }
}
