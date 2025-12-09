# BBelius.Aspire.Hosting.Zitadel

> **IMPORTANT**
> This library was "vibe-coded" based on `Aspire.Hosting.Keycloak`.
> While being tested, no in-depth review has been done yet.
> Use at your own risk.

Provides extension methods and resource definitions for an Aspire AppHost to configure a Zitadel resource.

## Getting started

### Install the package

In your AppHost project, install the Aspire Zitadel Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package BBelius.Aspire.Hosting.Zitadel
```

## Usage example

In the _Program.cs_ file of your AppHost, add a Zitadel resource with a PostgreSQL database and TLS certificates:

```csharp
var postgres = builder.AddPostgres("postgres")
                      .WithDataVolume()
                      .AddDatabase("zitadel");

var masterKey = builder.AddParameter("zitadel-masterkey", secret: true);

var zitadel = builder.AddZitadel("zitadel", masterKey, port: 8443)
                     .WithPostgres(postgres)
                     .WithDevCertificate();

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(zitadel);
```

**Important:**
- Zitadel requires a **master key** parameter (exactly 32 characters) for encrypting secrets. This is a required parameter.
- Zitadel is configured to use **HTTPS only**. You must call either `WithDevCertificate()` or `WithTls()` to provide TLS certificates.

**Recommendation:** Use a stable port for the Zitadel resource (8443 in the example above). This avoids issues with browser cookies that persist OIDC tokens (which include the authority URL with port) beyond the lifetime of the AppHost.

## Default Credentials

After Zitadel starts, you can log in with:

| Field | Value |
|-------|-------|
| **Username** | `admin@zitadel.localhost` |
| **Password** | *(see Aspire dashboard or secrets)* |

The password is auto-generated and stored in Aspire secrets. You can find it in:
- The **Aspire dashboard** under the Zitadel resource details
- The `secrets.json` file (usually at `~/.microsoft/usersecrets/<user-secrets-id>/secrets.json`)

These credentials give you full admin access to configure projects, OIDC applications, users, and more through the Zitadel console.

> **Note:** The Zitadel database is persistent (when using `WithDataVolume()` on PostgreSQL). You only need to configure your OIDC apps once per development machine.

### Developer-Friendly Defaults

For a smoother development experience, the following are disabled by default:
- Password change requirement on first login
- MFA/2FA enforcement

## TLS Configuration (Required)

Zitadel serves **HTTPS only**. You must configure TLS certificates using one of the following methods:

### Using the ASP.NET Core Development Certificate (Recommended)

The easiest way to configure TLS for local development:

```csharp
var zitadel = builder.AddZitadel("zitadel", masterKey, port: 8443)
                     .WithPostgres(postgres)
                     .WithDevCertificate();
```

Make sure your dev cert is trusted:

```bash
dotnet dev-certs https --trust
```

### Using Custom PEM Certificates

For production or custom certificates, provide PEM files:

```csharp
var zitadel = builder.AddZitadel("zitadel", port: 8443)
                     .WithPostgres(postgres)
                     .WithTls("./certs/cert.pem", "./certs/key.pem");
```

## Configuration Options

### External Domain

Configure the external domain for OIDC token generation:

```csharp
var zitadel = builder.AddZitadel("zitadel", port: 8443)
                     .WithPostgres(postgres)
                     .WithDevCertificate()
                     .WithExternalDomain("auth.example.com");
```

## Setting Up OIDC Applications

After Zitadel starts:

1. Open the Zitadel console at `https://localhost:8443` (or your configured port)
2. Log in with `admin@zitadel.localhost` and password from Aspire secrets
3. Create a new Project
4. Add an OIDC Application to the project
5. Configure redirect URIs, grant types, etc.
6. Copy the Client ID (and Client Secret if applicable) to your application

The configuration is stored in PostgreSQL and persists across restarts.

## Custom Admin Credentials

You can override the default admin credentials by providing custom parameters:

```csharp
var masterKey = builder.AddParameter("zitadel-masterkey", secret: true);
var adminPassword = builder.AddParameter("zitadel-admin-password", secret: true);

var zitadel = builder.AddZitadel("zitadel", masterKey, port: 8443,
        adminPassword: adminPassword)
    .WithPostgres(postgres)
    .WithDevCertificate();
```
