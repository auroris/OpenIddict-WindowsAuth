# OpenIddict-WindowsAuth

An OpenID Connect authorization server that uses Windows Integrated Authentication as its identity source, built on the [OpenIddict](https://documentation.openiddict.com/) library.

> **Note:** "IdentityServer" in this project refers to the assembly name and configuration section — it is **not** related to Duende IdentityServer.

## Contents

- [Rationale](#rationale)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
  - [Example appsettings.json](#example-appsettingsjson)
  - [Configuration keys](#configuration-keys)
    - [Serilog:MinimumLevel](#serilogminimumlevel)
    - [IdentityServer:PersistKeys](#identityserverpersistkeys)
    - [IdentityServer:DataPath](#identityserverdatapath)
    - [IdentityServer:AccessTokenLifetime](#identityserveraccesstokenlifetime)
    - [IdentityServer:IdentityTokenLifetime](#identityserveridentitytokenlifetime)
    - [IdentityServer:AuthorizationCodeLifetime](#identityserverauthorizationcodelifetime)
- [Supported flows, scopes, and claims](#supported-flows-scopes-and-claims)
- [Testing](#testing)
  - [Testing with oidcdebugger.com](#testing-with-oidcdebuggercom)
- [Troubleshooting](#troubleshooting)
- [License](#license)

## Rationale

A drop-in OpenID Connect authorization server that requires no database, no certificate management, and no persistent state. It performs Windows Integrated Authentication against the local machine or Active Directory and returns the resulting identity as standard OIDC tokens.

**Tradeoffs to be aware of:**

- By default, signing and encryption keys are ephemeral — regenerated on every application start. Set `IdentityServer:PersistKeys` to `true` to survive restarts (see [IdentityServer:PersistKeys](#identityserverpersistkeys)).
- There is no refresh token flow: tokens have a fixed lifetime and clients must re-authenticate when they expire.
- Suitable for intranet scenarios where a short-lived token model is acceptable and the user population is already authenticated to Windows.

## Prerequisites

- Windows Server or Windows 10/11 with IIS
- .NET 10.0 runtime (ASP.NET Core Hosting Bundle)
- IIS with both **Windows Authentication** and **Anonymous Authentication** roles/features installed
- For Active Directory integration: the host machine must be domain-joined (local-only accounts are supported with reduced claims — see [Supported flows, scopes, and claims](#supported-flows-scopes-and-claims))

## Installation

1. Publish the project (`dotnet publish -c Release`) and copy the output to your IIS server.
2. Create an IIS site or application pointing at the publish folder. The application pool must run under an identity with permission to query Active Directory (typically `ApplicationPoolIdentity` works on domain-joined machines).
3. In IIS Manager, open the site's **Authentication** feature and enable **both**:
   - **Windows Authentication** (required — this is how users are authenticated)
   - **Anonymous Authentication** (required — the `/connect/token` and `/.well-known/*` endpoints must be reachable without a Windows challenge)
4. If `IdentityServer:PersistKeys` is `false` (the default), tokens do not survive application restarts. To minimise disruption, configure the app pool's recycle settings:
   - Disable idle timeout or set **Idle Time-out Action** to `Suspend` (rather than `Terminate`)
   - Move the daily recycle to a low-traffic hour, or disable it in favor of a fixed schedule
   
   If `PersistKeys` is `true`, the app pool identity needs write access to the key storage directory (see [IdentityServer:DataPath](#identityserverdatapath)) and recycle timing is no longer a token-validity concern.

## Configuration

Configuration is read from `appsettings.json`. Environment variables and command-line arguments are also supported through the standard ASP.NET Core configuration pipeline.

### Example appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    }
  },
  "IdentityServer": {
    "ServerUri": "*",
    "UseForwardedHeaders": false,
    "PersistKeys": false,
    "AccessTokenLifetime": "01:00:00",
    "IdentityTokenLifetime": "01:00:00",
    "AuthorizationCodeLifetime": "00:05:00",
    "Hosts": [
      "http://localhost",
      "https://localhost",
      "https://myserver.com",
      "https://oidcdebugger.com"
    ],
    "Clients": [
      { "ClientId": "my-public-app" },
      { "ClientId": "my-confidential-app", "ClientSecret": "changeme" }
    ],
    "Groups": [
      "^MyServer .*$",
      "^Domain Admins$"
    ]
  }
}
```

### Configuration keys

#### Serilog:MinimumLevel

Controls the verbosity of application logging. Logs are written to the console and to daily rolling files under the `logs/` directory (e.g. `logs/identityserver-20260416.log`), with the last 14 days retained.

The `Default` level applies to all log sources that are not explicitly overridden. The `Override` map lets you set a different level per namespace — useful for quieting noisy framework components without suppressing your own application logs.

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

Available levels, from most to least verbose:

| Level | When to use |
|---|---|
| `Verbose` | Fine-grained tracing; very high volume — development only |
| `Debug` | Diagnostic detail useful during development and troubleshooting |
| `Information` | Normal operational events (startup, requests, configuration) — recommended default |
| `Warning` | Unexpected conditions that the application handled but that warrant attention |
| `Error` | Failures that prevented an operation from completing |
| `Fatal` | Unrecoverable failures that force the application to shut down |

Setting `Default` to `Warning` or higher is appropriate for production when log volume or storage is a concern. Setting it to `Debug` captures the detailed token, claim, and Active Directory diagnostics that are useful when troubleshooting authentication issues.

#### IdentityServer:ServerUri

The full URI the server should report in the `issuer` and endpoint fields of `/.well-known/openid-configuration`. If the app is installed at `https://myserver.com/IdentityServer/`, that is what should go here.

Set to `"*"` (the default) to auto-detect the issuer from the incoming request URL. Convenient when the deployment address isn't known in advance, but the issuer will vary if the app is reached via multiple hostnames.

#### IdentityServer:UseForwardedHeaders

Set to `true` to enable ASP.NET Core's forwarded-headers middleware, which rewrites the request scheme and host from `X-Forwarded-Proto` and `X-Forwarded-Host`. Enable this when the app sits behind a reverse proxy (IIS ARR, nginx, etc.) so issuer auto-detection and redirect URIs reflect the public-facing address. Defaults to `false`.

#### IdentityServer:PersistKeys

Controls whether signing and encryption keys are persisted across application restarts.

- `false` (default) — ephemeral keys are generated fresh on every startup. Tokens issued before a restart cannot be validated afterwards.
- `true` — keys are written to disk (see [IdentityServer:DataPath](#identityserverdatapath)) and reloaded on startup, so existing tokens remain valid across app pool recycles.

When `true`, key files are encrypted at rest with the Windows Data Protection API (`ProtectedData`, machine scope). Only processes running on the same machine can decrypt them; the raw private key bytes are never written to disk in plaintext.

#### IdentityServer:DataPath

The directory where key files are stored when `IdentityServer:PersistKeys` is `true`. Defaults to a `keys` subfolder inside the application's base directory.

The IIS app pool identity needs **read and write** access to this path. You can place it outside the web root to keep key files away from the published application files:

```json
"DataPath": "C:\\inetpub\\IdentityServerKeys"
```

#### IdentityServer:AccessTokenLifetime

How long an access token remains valid after it is issued. Specified as `"hh:mm:ss"`. Defaults to `"01:00:00"` (1 hour).

Access tokens are bearer credentials sent with every API request. A shorter lifetime limits the window of exposure if a token is intercepted, at the cost of more frequent re-authentication. On an intranet with Windows Authentication, re-authentication is silent, so shorter lifetimes are low-friction.

#### IdentityServer:IdentityTokenLifetime

How long an ID token remains valid after it is issued. Specified as `"hh:mm:ss"`. Defaults to `"01:00:00"` (1 hour).

The ID token is the OIDC-specific token that carries the user's identity claims to the client application (as opposed to the access token, which is presented to resource servers). In most flows both tokens are issued together and it makes sense to keep their lifetimes in sync.

#### IdentityServer:AuthorizationCodeLifetime

How long an authorization code remains valid for exchange at the token endpoint. Specified as `"hh:mm:ss"`. Defaults to `"00:05:00"` (5 minutes).

Authorization codes are short-lived single-use values exchanged immediately for tokens. There is rarely a reason to lengthen this beyond a few minutes; increasing it primarily widens the window for a code-interception attack.

#### IdentityServer:Hosts

An allowlist of hosts that may appear in a client's `redirect_uri`. Authorization requests whose `redirect_uri` resolves to a host not in this list are rejected. Only the host portion of each URL is compared — scheme, port, and path are ignored during validation.

This list also serves as the CORS origin allowlist. Browser-based (SPA) clients that call the token endpoint directly must have their origin (e.g. `https://myapp.com`) listed here, or the browser will block the response.

#### IdentityServer:Clients

List of permitted clients. Each entry must have a `ClientId`. An optional `ClientSecret` can be provided for confidential clients — if present, it will be verified on token requests.

- Use `"*"` as a `ClientId` to accept any client without enumerating them.
- Use `"*"` as a `ClientSecret` value to accept any secret without validation (useful for dev/test).

```json
"Clients": [
  { "ClientId": "my-public-app" },
  { "ClientId": "my-confidential-app", "ClientSecret": "changeme" },
  { "ClientId": "*" }
]
```

If this key is absent or empty, any `client_id` is accepted (open access).

#### IdentityServer:Groups

A list of **.NET regular expressions** (case-insensitive) matched against the authenticating user's Active Directory group names. Matching groups are returned as `role` claims in the token.

```json
"Groups": [
  "^MyServer .*$",
  "^Domain Admins$",
  ".*-Readers$"
]
```

Only groups whose common name matches at least one pattern are emitted. This keeps tokens small and prevents leaking internal group membership to relying parties.

## Supported flows, scopes, and claims

**Supported OAuth 2.0 / OIDC flows:**

| Flow | `response_type` values |
|---|---|
| Authorization Code | `code` |
| Implicit | `id_token`, `token`, `id_token token` |
| Hybrid | `code id_token`, `code token`, `code id_token token` |

PKCE is supported for the Authorization Code flow and recommended for public clients.

**Supported scopes and the claims each adds to the token:**

| Scope | Claims |
|---|---|
| `openid` | `sub` (Windows SID), `name` |
| `profile` | `windowsaccountname`; plus `givenname`, `surname`, `homephone` when available from Active Directory |
| `email` | `email` (from AD `mail` attribute, falling back to `username@localhost`) |
| `roles` | `role` (one per AD group matching `IdentityServer:Groups`) |

Profile, email, and role claims that require Active Directory are only populated for domain users. For users logged on with a local machine account, only `sub`, `name`, `windowsaccountname`, and a synthetic `email` of `username@localhost` are returned.

All claims are included in both the access token and the ID token.

## Testing

If you run the project in Visual Studio, the OpenID configuration document is available at:

* http://localhost:5000/.well-known/openid-configuration

### Testing with oidcdebugger.com

[oidcdebugger.com](https://oidcdebugger.com/) is a browser-based tool for constructing and sending OpenID Connect authorization requests and inspecting the results. Fill in the form fields as described below, then click **Send Request**. Your browser will be redirected to the authorization endpoint, Windows authentication will occur transparently, and the debugger will display the tokens or authorization code returned.

#### Common fields (all flows)

| Field | Value |
|---|---|
| Authorize URI | `http://localhost:5000/connect/authorize` |
| Redirect URI | `https://oidcdebugger.com/debug` |
| Client ID | `my-public-app` |
| Scope | `openid profile email roles` |
| Nonce | *(leave as auto-generated)* |

The `https://oidcdebugger.com` host is already in the default `IdentityServer:Hosts` allowlist, so no configuration change is needed.

#### Implicit flow — returns an ID token directly

| Field | Value |
|---|---|
| Response type | `id_token` |
| Response mode | `form_post` |

Use `token` instead of `id_token` to receive an access token, or check both to receive both in a single response.

#### Authorization code flow — returns a code for server-side exchange

| Field | Value |
|---|---|
| Response type | `code` |
| Response mode | `query` or `form_post` |
| Token URI | `http://localhost:5000/connect/token` *(required only if using PKCE; see below)* |

The debugger will display the authorization `code`. For **public clients** (`my-public-app`), enable **Use PKCE?** (SHA-256 is recommended) — the debugger will auto-generate the code verifier and challenge and can perform the token exchange automatically when Token URI is provided. For **confidential clients** (`my-confidential-app`), the debugger cannot supply `client_secret`, so use a tool such as Postman or `curl` to exchange the code manually:

```
POST http://localhost:5000/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&code=<code from debugger>
&redirect_uri=https://oidcdebugger.com/debug
&client_id=my-confidential-app
&client_secret=changeme
```

#### Hybrid flow — returns a code and tokens together

| Field | Value |
|---|---|
| Response type | `code` + `id_token` (check both) |
| Response mode | `form_post` |
| Token URI | `http://localhost:5000/connect/token` |

You can also combine `code` + `token` or all three (`code`, `token`, `id_token`) depending on what the client needs.

![Successful authorization response in oidcdebugger.com](docs/images/oidcdebugger-success.png)

## Troubleshooting

**SPA client receives a 431 (Request Header Fields Too Large) error.**
This happens when a user belongs to many AD groups that match `IdentityServer:Groups` — each matching group becomes a `role` claim and the resulting token can exceed IIS's default header size limit. Tighten the regex patterns in `IdentityServer:Groups` to emit only the groups the relying party actually needs, or increase IIS's `maxRequestEntityAllowed` / `requestLimits` settings.

**Firefox does not support Windows Integrated Authentication (SSO).**
Firefox does not negotiate Kerberos or NTLM automatically. When using Firefox, a credential popup will appear — enter your domain credentials (`DOMAIN\username` and password) for AD accounts, or your local Windows credentials for local testing. Use Chrome or Edge for transparent single sign-on.

**Browser prompts for Windows credentials repeatedly (401 loop).**
Anonymous Authentication is likely disabled in IIS, or Windows Authentication is not enabled at all. Both must be turned on. Also confirm the browser trusts the site for integrated authentication (for IE/Edge/Chrome, the site must be in the Local Intranet zone or explicitly whitelisted).

**Tokens issued before a restart fail validation afterwards.**
Expected when `IdentityServer:PersistKeys` is `false` (the default). Set it to `true` to persist keys across restarts. If you prefer ephemeral keys, configure the IIS app pool to suspend rather than terminate on idle, and to recycle on a predictable schedule.

**Issuer in tokens doesn't match the URL clients use.**
Either set `IdentityServer:ServerUri` to the canonical public URL explicitly, or — if behind a reverse proxy — set `IdentityServer:UseForwardedHeaders` to `true` and ensure the proxy is sending `X-Forwarded-Proto` and `X-Forwarded-Host`.

**Authorization request rejected with `invalid_client`.**
Either the `client_id` is not in `IdentityServer:Clients`, or the `redirect_uri` host is not in `IdentityServer:Hosts`. The application log records which check failed and the offending value.

**Expected role claims are missing.**
Verify the user is on a domain-joined machine (local accounts do not get role claims) and that the group names match the regex patterns in `IdentityServer:Groups`. Remember the patterns are regex — plain strings like `"MyServer Admins"` will match, but a pattern like `"MyServer *"` does **not** mean glob-style wildcard; it means the literal letter `r` zero or more times. Use `"MyServer .*"` for "starts with 'MyServer '".

**Logs show a domain user authenticated via NTLM instead of Kerberos.**
This usually means the server's SPN is not registered in Active Directory. Without a matching SPN, the browser cannot obtain a Kerberos ticket and falls back to NTLM. Register the SPN with `setspn -S HTTP/yourserver.domain.com DOMAIN\AppPoolAccount` and restart the application pool. NTLM is expected and normal for local machine accounts — the concern is only when a domain account shows up as NTLM in the logs.

**Windows authentication succeeds sometimes but fails intermittently with Kerberos.**
The most common cause is clock skew — Kerberos requires the server and domain controller clocks to be within 5 minutes of each other. Check `w32tm /query /status` on the server and compare against the DC. VMs are especially prone to clock drift after snapshots or resume from suspend.

## License

[MIT](LICENSE)
