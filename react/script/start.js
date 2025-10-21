import process from 'node:process'
import { createServer, loadConfigFromFile, mergeConfig } from 'vite'

async function bootstrap() {
  const root = process.cwd()
  const command = 'serve'
  const mode = process.env.NODE_ENV ?? 'development'

  const loaded = await loadConfigFromFile({ command, mode }, undefined, root)
  const userConfig = loaded?.config ?? {}
  const serverDefaults = userConfig.server ?? {}

  const parsedPort = Number.parseInt(process.env.PORT ?? serverDefaults.port ?? '5173', 10)
  const port = Number.isNaN(parsedPort) ? 5173 : parsedPort

  const overrides = {
    configFile: false,
    root,
    server: {
      ...serverDefaults,
      host: serverDefaults.host ?? 'localhost',
      port,
      open: serverDefaults.open ?? false,
      strictPort: serverDefaults.strictPort ?? false,
    },
  }

  const finalConfig = mergeConfig(userConfig, overrides)

  const server = await createServer(finalConfig)
  await server.listen()
  server.printUrls()
  server.bindCLIShortcuts({ print: true })
}

bootstrap().catch((error) => {
  console.error('Failed to start dev server:', error)
  process.exitCode = 1
})
