# Security

The MVP is designed for local development and controlled deployments.

## Localhost Only

The HTTP server binds to `127.0.0.1` by default. It must not be exposed on LAN or public interfaces in the MVP.

## CORS

Allowed origins are configured explicitly. The default configuration includes common local development origins only:

- `http://localhost:5173`
- `https://localhost:5173`

The agent does not allow all origins by default.

## Request Limits

The host limits request body size. The MVP does not accept arbitrary file paths and does not execute arbitrary commands.

## Risks

A local print agent can be abused by any allowed web origin to print unwanted content or trigger a drawer kick. Production deployments should add pairing, local tokens, user confirmation during setup, audit logs, and stronger policy controls.

## Future Security Roadmap

- Pairing flow between frontend and local agent.
- Local access token.
- Origin-specific permissions.
- Signed job metadata.
- Exportable audit logs.
- Optional operator confirmation for sensitive actions.
