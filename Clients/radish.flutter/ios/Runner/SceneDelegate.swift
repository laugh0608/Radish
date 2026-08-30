import Flutter
import UIKit

class SceneDelegate: FlutterSceneDelegate {
  override func scene(
    _ scene: UIScene,
    willConnectTo session: UISceneSession,
    options connectionOptions: UIScene.ConnectionOptions
  ) {
    super.scene(
      scene,
      willConnectTo: session,
      options: connectionOptions
    )

    for context in connectionOptions.urlContexts {
      if appDelegate?.receiveAuthCallback(context.url) == true {
        break
      }
    }
  }

  override func scene(
    _ scene: UIScene,
    openURLContexts URLContexts: Set<UIOpenURLContext>
  ) {
    let handled = URLContexts.contains { context in
      appDelegate?.receiveAuthCallback(context.url) == true
    }
    if !handled {
      super.scene(scene, openURLContexts: URLContexts)
    }
  }

  private var appDelegate: AppDelegate? {
    return UIApplication.shared.delegate as? AppDelegate
  }
}
