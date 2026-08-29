# frontend

The React + TypeScript + Vite single-page app that consumes `backend/src/FlightAi.Api`'s Server-Sent
Events search stream and the booking saga's HTTP contract — presented as a chat.

Not scaffolded yet. It's specified as its own feature:
[`docs/features/02-frontend/`](../docs/features/02-frontend/README.md), tasks F01–F08. Task F01
scaffolds it with:

```bash
npm create vite@latest . -- --template react-ts
```

Deployment target: Azure Static Web Apps (Free tier), provisioned by Bicep in task F08. See
[`docs/deployment.md`](../docs/deployment.md).
