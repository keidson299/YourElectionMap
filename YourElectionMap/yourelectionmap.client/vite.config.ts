/// <reference types="vitest/config" />
import { spawnSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { env } from 'node:process'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Port the ASP.NET Core host expects the SPA dev server on (see SpaProxyServerUrl
// in YourElectionMap.Server.csproj). Keep the two in sync.
const devServerPort = 52462

// Requests matching these prefixes are forwarded to the ASP.NET Core server instead
// of being handled by Vite. Add new server routes here as the API grows.
const apiProxyRoutes = ['/api']

// The dev server runs over HTTPS with the same certificate ASP.NET Core uses, so the
// browser does not complain when the SPA and the API share an origin.
function httpsDevCertificate() {
  const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
      ? `${env.APPDATA}/ASP.NET/https`
      : `${env.HOME}/.aspnet/https`
  const certificateName = 'yourelectionmap.client'
  const certFilePath = path.join(baseFolder, `${certificateName}.pem`)
  const keyFilePath = path.join(baseFolder, `${certificateName}.key`)

  if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true })
  }

  if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    const exported = spawnSync(
      'dotnet',
      [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password',
      ],
      { stdio: 'inherit' },
    )

    if (exported.status !== 0) {
      throw new Error(
        'Could not export the ASP.NET Core HTTPS development certificate. ' +
          'Run "dotnet dev-certs https --trust" and try again.',
      )
    }
  }

  return {
    key: fs.readFileSync(keyFilePath),
    cert: fs.readFileSync(certFilePath),
  }
}

// Where the ASP.NET Core server is listening. Set by SpaProxy when Visual Studio or
// "dotnet run" starts the SPA; falls back to the https launch profile otherwise.
const serverTarget = env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
  : env.ASPNETCORE_URLS
    ? env.ASPNETCORE_URLS.split(';')[0]
    : 'https://localhost:7132'

// https://vite.dev/config/
export default defineConfig(({ command, mode }) => {
  // Only the dev server needs a certificate; skip it for "vite build" and for Vitest.
  const serving = command === 'serve' && mode !== 'test' && env.VITEST === undefined

  return {
    plugins: [react()],
    server: {
      port: devServerPort,
      // Fail instead of silently moving to another port, which would break the proxy.
      strictPort: true,
      ...(serving ? { https: httpsDevCertificate() } : {}),
      proxy: Object.fromEntries(
        apiProxyRoutes.map((route) => [
          route,
          {
            target: serverTarget,
            changeOrigin: true,
            // The ASP.NET Core dev certificate is self-signed.
            secure: false,
          },
        ]),
      ),
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      include: ['src/**/*.{test,spec}.{ts,tsx}'],
      reporters: ['default'],
    },
  }
})
