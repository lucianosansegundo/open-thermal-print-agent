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

Image/logo commands accept base64 raster data only. The API does not read local files from paths supplied by clients.

## Optional Print Token

Print token security is optional and disabled by default for development.

Configuration:

```json
{
  "Agent": {
    "Security": {
      "RequireToken": true,
      "Token": "change-this-local-token",
      "HeaderName": "X-OpenThermalPrintAgent-Token"
    }
  }
}
```

When enabled, print endpoints require either:

- `X-OpenThermalPrintAgent-Token: change-this-local-token`
- `Authorization: Bearer change-this-local-token`

`/api/v1/health` remains open with minimal agent information. `/api/v1/printers` remains open in the MVP so local setup tools can discover printers, but production deployments may choose to protect it later.

## Risks

A local print agent can be abused by any allowed web origin to print unwanted content or trigger a drawer kick if token security is disabled or the token is exposed. Production deployments should add pairing, origin-specific permissions, user confirmation during setup, audit logs, and stronger policy controls.

## Future Security Roadmap

- Pairing flow between frontend and local agent.
- Origin-specific permissions.
- Signed job metadata.
- Exportable audit logs.
- Optional operator confirmation for sensitive actions.
