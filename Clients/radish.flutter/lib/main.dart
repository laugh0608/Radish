import 'app/bootstrap.dart';
import 'app/local_logging.dart';

Future<void> main() => runWithLocalLogging(const RadishBootstrap().run);
