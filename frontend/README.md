# frontend

The React + TypeScript + Vite single-page app that consumes `backend/src/FlightAi.Api`'s Server-Sent
Events search stream and the booking saga's HTTP contract — presented as a chat. Built out across
tasks F01–F07 in [`docs/features/02-frontend/`](../docs/features/02-frontend/README.md); see
[`docs/reference/10-frontend-architecture.md`](../docs/reference/10-frontend-architecture.md) for how
it's put together and [`docs/reference/11-bilingual-ui.md`](../docs/reference/11-bilingual-ui.md) for
the per-turn language design specifically.

Deployed to Azure Static Web Apps (Free tier), provisioned by Bicep in infra task 02 — see
[`docs/deployment.md`](../docs/deployment.md).

## Running it locally

```bash
npm install
echo "VITE_API_BASE_URL=http://localhost:5294" > .env.development
echo "VITE_BOOKING_API_BASE_URL=http://localhost:7071" >> .env.development
npm run dev   # http://localhost:5173
```

Both base URLs are build-time configuration, read via `import.meta.env.VITE_*` in `src/config.ts` —
never hardcoded, since the deployed frontend and its two backends (`FlightAi.Api`, a separate Azure
resource from `FlightAi.Booking.Functions`) are never on the same origin. Point them at whichever
instance of each backend you're running — see the repo root [`README.md`](../README.md) for how to
start both locally.

## Testing

```bash
npm test        # one run, Vitest + Testing Library
npm run test:watch
```

One test per documented eval in `docs/features/02-frontend/tasks/*.md`, same discipline as the backend
— see [`docs/features/README.md`](../docs/features/README.md#the-eval-discipline).

## Building

```bash
npm run build   # tsc -b && vite build, output to dist/
```
