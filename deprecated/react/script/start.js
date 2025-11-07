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

  // Decide HTTPS strategy: prefer repo-root dev-certs; else react/script/certs; else basic-ssl; else HTTP
  // 默认使用 HTTP；只有设置 DEV_HTTPS=1 时才启用 HTTPS
  const useHttps = process.env.DEV_HTTPS === '1'
  /** @type {import('vite').PluginOption | undefined} */
  let httpsPlugin
  /** @type {false | import('vite').HttpsServerOptions | true | undefined} */
  let httpsOption = serverDefaults.https

  if (useHttps && httpsOption === undefined) {
    /**
     * Try loading certificate pair from given directory.
     * @param {string} base
     */
    const loadPair = (base) => {
      const keyFile = path.join(base, 'localhost.key')
      const crtFile = path.join(base, 'localhost.crt')
      if (fs.existsSync(keyFile) && fs.existsSync(crtFile)) {
        return { key: fs.readFileSync(keyFile), cert: fs.readFileSync(crtFile) }
      }
      return undefined
    }

    // 1) repo root: ../dev-certs
    const monoRoot = path.resolve(root, '..')
    const pairRoot = loadPair(path.join(monoRoot, 'dev-certs'))
    if (pairRoot) {
      httpsOption = pairRoot
      console.log('[dev] HTTPS enabled using ../dev-certs/localhost.{key,crt}')
    }

    // 2) legacy location: react/script/certs
    if (httpsOption === undefined) {
      const pairScript = loadPair(path.join(root, 'script', 'certs'))
      if (pairScript) {
        httpsOption = pairScript
        console.log('[dev] HTTPS enabled using script/certs/localhost.{key,crt}')
      }
    }

    // 3) fall back to basic-ssl plugin (self-signed, not trusted by default)
    if (httpsOption === undefined) {
      try {
        const mod = await import('@vitejs/plugin-basic-ssl')
        const basicSsl = (mod && (mod.default ?? mod))
        httpsPlugin = basicSsl()
        httpsOption = true
        console.log('[dev] HTTPS enabled via @vitejs/plugin-basic-ssl')
      } catch {
        httpsOption = false
        console.warn('[dev] HTTPS requested but no certs found and @vitejs/plugin-basic-ssl not installed. Falling back to HTTP. To enable HTTPS, put certs in ../dev-certs/ or react/script/certs/, or install the plugin.')
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
  // 自定义输出：仅打印首选本地地址，避免多 host 造成困惑
  const urls = server.resolvedUrls
  if (urls && (urls.local?.length || urls.network?.length)) {
    const locals = urls.local ?? []
    const primary =
      locals.find((u) => u.includes('https://localhost')) ||
      locals.find((u) => u.includes('http://localhost')) ||
      locals[0] ||
      (urls.network ?? [])[0]
    if (primary) console.log(`\n  ➜  Local: ${primary}\n`)
  } else {
    server.printUrls()
  }
  server.bindCLIShortcuts({ print: true })
}

bootstrap().catch((error) => {
  console.error('Failed to start dev server:', error)
  process.exitCode = 1
})
