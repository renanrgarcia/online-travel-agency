# frontend

The React + TypeScript + Vite single-page app that consumes `backend/src/FlightAi.Api`'s
Server-Sent Events search stream and the booking saga's HTTP contract.

Not scaffolded yet — this lands alongside task 12/13 (the SSE API), per
`docs/specs/macro-scenario.md`. Scaffold it with:

```bash
npm create vite@latest . -- --template react-ts
```

Deployment target: Azure Static Web Apps (Free tier). See `docs/specs/deployment.md`.
