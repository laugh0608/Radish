# Radish Console

Radish Console is a small React + TypeScript + Vite application used as the management console frontend under the Radish Gateway.

## Features

- Shows an overview table of core services (frontend webOS, docs, API, Scalar UI, console).
- Intended as the entry point for future admin/monitoring features.

## Development

From the repo root:

```bash
npm install --prefix radish.console  # first time only
npm run dev --prefix radish.console
```

The dev server runs on:

- https://localhost:3002/

## Access through Gateway

When both Gateway and the console dev server are running, you can access the console via:

- https://localhost:5001/console

The Gateway forwards `/console/**` to `https://localhost:3002`.

You can also use the unified start scripts from the repo root:

```bash
pwsh ./start.ps1   # Windows/PowerShell
./start.sh         # Linux/macOS
```

Choose the menu item for `radish.console` or a combination that includes the console (for example: "Gateway + API + frontend + console").

## Notes

- This project is a plain Vite React app; there is no backend code here.
- API base URLs and routing are configured on the Gateway side (YARP) rather than in this project.
- You can customize the console UI freely as long as it remains accessible via `/console` behind the Gateway.
