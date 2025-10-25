import process from 'node:process'
import fs from 'node:fs'
import path from 'node:path'
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

  // Decide HTTPS strategy: prefer basic-ssl plugin; else try local certs; else fallback to HTTP
  const useHttps = process.env.DEV_HTTPS !== '0'
  /** @type {import('vite').PluginOption | undefined} */
  let httpsPlugin
  /** @type {false | import('vite').HttpsServerOptions | true | undefined} */
  let httpsOption = serverDefaults.https

  if (useHttps && httpsOption === undefined) {
    try {
      // Lazy import to avoid hard dependency when offline
      const mod = await import('@vitejs/plugin-basic-ssl')
      const basicSsl = (mod && (mod.default ?? mod))
      httpsPlugin = basicSsl()
      httpsOption = true
      console.log('[dev] HTTPS enabled via @vitejs/plugin-basic-ssl')
    } catch {
      const certDir = path.join(root, 'script', 'certs')
      const keyFile = path.join(certDir, 'localhost.key')
      const crtFile = path.join(certDir, 'localhost.crt')
      if (fs.existsSync(keyFile) && fs.existsSync(crtFile)) {
        httpsOption = {
          key: fs.readFileSync(keyFile),
          cert: fs.readFileSync(crtFile),
        }
        console.log('[dev] HTTPS enabled using script/certs/localhost.{key,crt}')
      } else {
        httpsOption = false
        console.warn('[dev] HTTPS requested but no certs found and @vitejs/plugin-basic-ssl not installed. Falling back to HTTP. To enable HTTPS, install dev dep: "npm i -D @vitejs/plugin-basic-ssl" or put certs in react/script/certs/.')
      }
    }
  }

  const overrides = {
    configFile: false,
    root,
    server: {
      ...serverDefaults,
      host: serverDefaults.host ?? 'localhost',
      port,
      open: serverDefaults.open ?? false,
      strictPort: serverDefaults.strictPort ?? false,
      https: httpsOption,
    },
  }

  const finalConfig = mergeConfig(
    // merge plugins if we added https plugin
    httpsPlugin
      ? { ...userConfig, plugins: [...(userConfig.plugins ?? []), httpsPlugin] }
      : userConfig,
    overrides,
  )

  const server = await createServer(finalConfig)
  await server.listen()
  server.printUrls()
  server.bindCLIShortcuts({ print: true })
}

bootstrap().catch((error) => {
  console.error('Failed to start dev server:', error)
  process.exitCode = 1
})
