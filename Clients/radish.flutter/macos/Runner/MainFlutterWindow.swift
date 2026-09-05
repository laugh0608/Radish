import Cocoa
import FlutterMacOS

class MainFlutterWindow: NSWindow {
  override func awakeFromNib() {
    let flutterViewController = FlutterViewController()
    let windowFrame = self.frame
    self.contentViewController = flutterViewController
    self.setFrame(windowFrame, display: true)
    self.contentMinSize = NSSize(width: 390, height: 600)
    self.setFrameAutosaveName("RadishMainWindow")

    RegisterGeneratedPlugins(registry: flutterViewController)
    (NSApplication.shared.delegate as? AppDelegate)?.configurePlatformChannels(
      messenger: flutterViewController.engine.binaryMessenger
    )

    super.awakeFromNib()
  }
}
