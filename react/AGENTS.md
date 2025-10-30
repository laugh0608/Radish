# Repository Guidelines

## Project Structure & Module Organization
The Vite workspace centers on `src/`, which contains entry points such as `main.tsx`, shared layout components, and global styles in `index.css`. Feature folders should live under `src/` with colocated hooks, tests, and styles. Static assets that must pass through the bundler belong in `src/assets`; place web-visible files (favicons, robots.txt) in `public/`. The root `start.ps1` script is maintained by ABP Studio; you can ignore it when adjusting onboarding steps.

## Build, Test, and Development Commands
- `npm run dev` - launches the Vite dev server with hot module reload. Ignore the root `start.ps1`; ABP Studio owns that automation.
- `npm run build` - runs TypeScript project references (`tsconfig.app.json`, `tsconfig.node.json`) and produces the production bundle in `dist/`.
- `npm run preview` - serves the latest build for manual QA checks.
- `npm run lint` - executes the root ESLint configuration; resolve all warnings before committing.
- `npm run typecheck` - runs TypeScript project references without emitting files.
- `npm run lint:fix` - lints and applies safe autofixes.
- `npm run format` / `npm run format:check` - Prettier formatting (write/check).

## Coding Style & Naming Conventions
Use TypeScript and React function components with 2-space indentation, single quotes, and trailing commas where practical to match existing files. Name components in PascalCase (`UserBadge.tsx`), hooks in camelCase prefixed with `use`, and utility modules under `src/lib`. Keep styles local to the feature folder; reserve `index.css` for global tokens and resets. Run ESLint before pushing; autofix with `--fix` when changes are unambiguous.

## Testing Guidelines
Automated tests are not yet provisioned; add Vitest or React Testing Library specs alongside the source as `ComponentName.test.tsx` when you introduce new behavior. Cover rendering states, user interactions, and boundary cases. Document manual test steps in the pull request when automation is not available.

## Commit & Pull Request Guidelines
Commits follow a Conventional Commit prefix (`feat:`, `fix:`, `docs:`) as seen in git history; keep subjects imperative and under 72 characters, ideally in English to aid cross-team search. Pull requests should describe scope, reference backlog IDs, and include screenshots or clips for UI changes. Ensure `npm run build` and `npm run lint` pass locally, request reviews promptly, and capture follow-up tasks in the PR checklist.

## CI Checks
GitHub Actions (`.github/workflows/react-checks.yml`) runs on pushes/PRs touching `react/**` and validates install, lint/typecheck, and production build. Keep scripts (`lint`, `typecheck`, `build`) green before requesting review.
