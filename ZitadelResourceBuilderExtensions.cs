#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using BBelius.Aspire.Hosting.ApplicationModel;
using BBelius.Aspire.Hosting.Zitadel;

namespace BBelius.Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Zitadel resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class ZitadelResourceBuilderExtensions
{
    // Environment variable names for Zitadel configuration
    private const string ExternalDomainEnvVarName = "ZITADEL_EXTERNALDOMAIN";
    private const string ExternalPortEnvVarName = "ZITADEL_EXTERNALPORT";
    private const string ExternalSecureEnvVarName = "ZITADEL_EXTERNALSECURE";
    private const string TlsEnabledEnvVarName = "ZITADEL_TLS_ENABLED";
    private const string TlsCertPathEnvVarName = "ZITADEL_TLS_CERTPATH";
    private const string TlsKeyPathEnvVarName = "ZITADEL_TLS_KEYPATH";
    private const string AdminUsernameEnvVarName = "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME";
    private const string AdminPasswordEnvVarName = "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORD";
    private const string AdminPasswordChangeRequiredEnvVarName = "ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORDCHANGEREQUIRED";
    private const string LoginV2RequiredEnvVarName = "ZITADEL_DEFAULTINSTANCE_FEATURES_LOGINV2_REQUIRED";
    private const string ForceMfaEnvVarName = "ZITADEL_DEFAULTINSTANCE_LOGINPOLICY_FORCEMFA";
    private const string ForceMfaLocalOnlyEnvVarName = "ZITADEL_DEFAULTINSTANCE_LOGINPOLICY_FORCEMFALOCALONLY";

    // Database configuration environment variables
    private const string DatabaseHostEnvVarName = "ZITADEL_DATABASE_POSTGRES_HOST";
    private const string DatabasePortEnvVarName = "ZITADEL_DATABASE_POSTGRES_PORT";
    private const string DatabaseNameEnvVarName = "ZITADEL_DATABASE_POSTGRES_DATABASE";
    private const string DatabaseAdminUsernameEnvVarName = "ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME";
    private const string DatabaseAdminPasswordEnvVarName = "ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD";
    private const string DatabaseAdminSslModeEnvVarName = "ZITADEL_DATABASE_POSTGRES_ADMIN_SSL_MODE";
    private const string DatabaseUserUsernameEnvVarName = "ZITADEL_DATABASE_POSTGRES_USER_USERNAME";
    private const string DatabaseUserPasswordEnvVarName = "ZITADEL_DATABASE_POSTGRES_USER_PASSWORD";
    private const string DatabaseUserSslModeEnvVarName = "ZITADEL_DATABASE_POSTGRES_USER_SSL_MODE";

    private const int DefaultContainerPort = 8080;

    /// <summary>
    /// Adds a Zitadel container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">The host port that the underlying container is bound to when running locally.</param>
    /// <param name="adminUsername">The parameter used as the admin username for the Zitadel resource. If <see langword="null"/> a default value will be used.</param>
    /// <param name="adminPassword">The parameter used as the admin password for the Zitadel resource. If <see langword="null"/> a default password will be used.</param>
    /// <param name="masterKey">The parameter used as the master key for encryption. If <see langword="null"/> a default key will be generated. Must be exactly 32 characters.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// The container exposes HTTPS on port 8080 by default. TLS certificates must be configured using
    /// <see cref="WithDevCertificate"/> or <see cref="WithTls"/>.
    /// </para>
    /// <para>
    /// This version of the package defaults to the <inheritdoc cref="ZitadelContainerImageTags.Tag"/> tag of the
    /// <inheritdoc cref="ZitadelContainerImageTags.Registry"/>/<inheritdoc cref="ZitadelContainerImageTags.Image"/> container image.
    /// </para>
    /// <para>
    /// Zitadel requires a PostgreSQL database. Use <see cref="WithPostgres"/> to configure the database connection.
    /// </para>
    /// <para>
    /// <strong>Default credentials:</strong> admin@zitadel.localhost with auto-generated password (stored in Aspire secrets).
    /// Password change and MFA are disabled for easier development.
    /// </para>
    /// <example>
    /// Use in application host
    /// <code lang="csharp">
    /// var postgres = builder.AddPostgres("postgres")
    ///                       .AddDatabase("zitadel");
    ///
    /// var zitadel = builder.AddZitadel("zitadel", port: 8443)
    ///                      .WithPostgres(postgres)
    ///                      .WithDevCertificate();
    ///
    /// var myService = builder.AddProject&lt;Projects.MyService&gt;()
    ///                        .WithReference(zitadel);
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<ZitadelResource> AddZitadel(
        this IDistributedApplicationBuilder builder,
        string name,
        int? port = null,
        IResourceBuilder<ParameterResource>? adminUsername = null,
        IResourceBuilder<ParameterResource>? adminPassword = null,
        IResourceBuilder<ParameterResource>? masterKey = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Use Aspire's standard password parameter (stored in secrets)
        var passwordParameter = adminPassword?.Resource ??
            ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", special: false);

        // Master key must be exactly 32 characters - generate a secure key without special characters
        var masterKeyParameter = masterKey?.Resource ??
            ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-masterkey", special: false);

        var resource = new ZitadelResource(name, adminUsername?.Resource, passwordParameter, masterKeyParameter);

        // Capture the port for use in the environment callback
        var externalPort = port;

        var zitadel = builder
            .AddResource(resource)
            .WithImage(ZitadelContainerImageTags.Image)
            .WithImageRegistry(ZitadelContainerImageTags.Registry)
            .WithImageTag(ZitadelContainerImageTags.Tag)
            .WithHttpsEndpoint(port: port, targetPort: DefaultContainerPort, name: ZitadelResource.PrimaryEndpointName)
            .WithOtlpExporter()
            .WithEnvironment(context =>
            {
                // External access configuration - HTTPS only
                context.EnvironmentVariables[ExternalDomainEnvVarName] = "localhost";
                context.EnvironmentVariables[ExternalSecureEnvVarName] = "true";
                context.EnvironmentVariables[TlsEnabledEnvVarName] = "true";

                // Set the external port so Zitadel knows how to generate URLs
                // If a fixed port was specified, use it directly; otherwise use the endpoint's allocated port
                if (externalPort.HasValue)
                {
                    context.EnvironmentVariables[ExternalPortEnvVarName] = externalPort.Value.ToString();
                }
                else
                {
                    var endpoint = resource.GetEndpoint(ZitadelResource.PrimaryEndpointName);
                    context.EnvironmentVariables[ExternalPortEnvVarName] = endpoint.Property(EndpointProperty.Port);
                }

                // Use Login V1 (built-in) instead of Login V2 (requires separate container)
                context.EnvironmentVariables[LoginV2RequiredEnvVarName] = "false";

                // Disable MFA requirement for easier dev experience
                context.EnvironmentVariables[ForceMfaEnvVarName] = "false";
                context.EnvironmentVariables[ForceMfaLocalOnlyEnvVarName] = "false";

                // Admin credentials - username default: admin@zitadel.localhost, password from Aspire secrets
                context.EnvironmentVariables[AdminUsernameEnvVarName] = resource.AdminUsernameReference;
                context.EnvironmentVariables[AdminPasswordEnvVarName] = resource.AdminPasswordParameter;
                // Don't require password change on first login
                context.EnvironmentVariables[AdminPasswordChangeRequiredEnvVarName] = "false";
            })
            .WithArgs(context =>
            {
                context.Args.Add("start-from-init");
                context.Args.Add("--masterkey");
                context.Args.Add(resource.MasterKeyParameter);
            })
            .WithHttpHealthCheck("/healthz");  // Required for health checks

        return zitadel;
    }

    /// <summary>
    /// Configures the Zitadel resource to use a PostgreSQL database.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="database">The PostgreSQL database resource to use.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// Zitadel requires PostgreSQL as its backing database. This method configures the necessary
    /// environment variables for Zitadel to connect to the specified PostgreSQL database.
    /// <example>
    /// Configure Zitadel with PostgreSQL
    /// <code lang="csharp">
    /// var postgres = builder.AddPostgres("postgres")
    ///                       .AddDatabase("zitadel");
    ///
    /// var zitadel = builder.AddZitadel("zitadel")
    ///                      .WithPostgres(postgres);
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<ZitadelResource> WithPostgres(
        this IResourceBuilder<ZitadelResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        return builder
            .WithReference(database)
            .WaitFor(database)
            .WithEnvironment(context =>
            {
                var parentPostgres = database.Resource.Parent;

                context.EnvironmentVariables[DatabaseHostEnvVarName] = parentPostgres.PrimaryEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables[DatabasePortEnvVarName] = parentPostgres.PrimaryEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables[DatabaseNameEnvVarName] = database.Resource.DatabaseName;

                // Admin connection (uses parent PostgreSQL credentials)
                context.EnvironmentVariables[DatabaseAdminUsernameEnvVarName] = parentPostgres.UserNameReference;
                context.EnvironmentVariables[DatabaseAdminPasswordEnvVarName] = parentPostgres.PasswordParameter;
                context.EnvironmentVariables[DatabaseAdminSslModeEnvVarName] = "disable";

                // User connection (uses same credentials for simplicity in local dev)
                context.EnvironmentVariables[DatabaseUserUsernameEnvVarName] = parentPostgres.UserNameReference;
                context.EnvironmentVariables[DatabaseUserPasswordEnvVarName] = parentPostgres.PasswordParameter;
                context.EnvironmentVariables[DatabaseUserSslModeEnvVarName] = "disable";
            });
    }

    /// <summary>
    /// Configures the external domain for the Zitadel resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="domain">The external domain that users will use to access Zitadel.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// The external domain is important for OIDC token generation and callback URLs.
    /// Zitadel always uses HTTPS.
    /// <example>
    /// Configure external domain
    /// <code lang="csharp">
    /// var zitadel = builder.AddZitadel("zitadel", port: 8443)
    ///                      .WithPostgres(postgres)
    ///                      .WithDevCertificate()
    ///                      .WithExternalDomain("auth.example.com");
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<ZitadelResource> WithExternalDomain(
        this IResourceBuilder<ZitadelResource> builder,
        string domain)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(domain);

        return builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables[ExternalDomainEnvVarName] = domain;
        });
    }

    /// <summary>
    /// Configures TLS certificates for the Zitadel resource using PEM certificate files.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="certPath">The path to the TLS certificate file (PEM format) on the host.</param>
    /// <param name="keyPath">The path to the TLS private key file (PEM format) on the host.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// This method configures TLS certificates for Zitadel. Zitadel requires PEM format certificates.
    /// <para>
    /// <strong>Important:</strong> Either <c>WithTls</c> or <c>WithDevCertificate</c> must be called
    /// to provide TLS certificates, as Zitadel is configured to use HTTPS only.
    /// </para>
    /// <example>
    /// Enable TLS with custom certificates
    /// <code lang="csharp">
    /// var zitadel = builder.AddZitadel("zitadel", port: 8443)
    ///                      .WithPostgres(postgres)
    ///                      .WithTls("./certs/cert.pem", "./certs/key.pem");
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<ZitadelResource> WithTls(
        this IResourceBuilder<ZitadelResource> builder,
        string certPath,
        string keyPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(certPath);
        ArgumentException.ThrowIfNullOrEmpty(keyPath);

        const string containerCertPath = "/etc/zitadel/certs/cert.pem";
        const string containerKeyPath = "/etc/zitadel/certs/key.pem";

        return builder
            .WithBindMount(certPath, containerCertPath, isReadOnly: true)
            .WithBindMount(keyPath, containerKeyPath, isReadOnly: true)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables[TlsCertPathEnvVarName] = containerCertPath;
                context.EnvironmentVariables[TlsKeyPathEnvVarName] = containerKeyPath;
            });
    }

    /// <summary>
    /// Configures TLS certificates for the Zitadel resource using the ASP.NET Core development certificate.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// This method exports the ASP.NET Core development certificate and mounts it into the container.
    /// The dev certificate must be trusted on your machine (run <c>dotnet dev-certs https --trust</c>).
    /// <para>
    /// This is the recommended way to configure TLS for local development. Note that the dev cert
    /// is only valid for <c>localhost</c>.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> Either <c>WithDevCertificate</c> or <c>WithTls</c> must be called
    /// to provide TLS certificates, as Zitadel is configured to use HTTPS only.
    /// </para>
    /// <example>
    /// Enable TLS with development certificate
    /// <code lang="csharp">
    /// var zitadel = builder.AddZitadel("zitadel", port: 8443)
    ///                      .WithPostgres(postgres)
    ///                      .WithDevCertificate();
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<ZitadelResource> WithDevCertificate(
        this IResourceBuilder<ZitadelResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Create a deterministic temp directory for the exported cert
        var certDir = Path.Combine(Path.GetTempPath(), "aspire-zitadel-devcert");
        Directory.CreateDirectory(certDir);

        var certPath = Path.Combine(certDir, "cert.pem");
        var keyPath = Path.Combine(certDir, "key.pem");

        // Export the dev cert if it doesn't exist or is older than 1 day
        if (!File.Exists(certPath) || !File.Exists(keyPath) ||
            File.GetLastWriteTimeUtc(certPath) < DateTime.UtcNow.AddDays(-1))
        {
            ExportDevCertificate(certPath, keyPath);
        }

        return builder.WithTls(certPath, keyPath);
    }

    private static void ExportDevCertificate(string certPath, string keyPath)
    {
        // Export the dev certificate to PEM format
        var pfxPath = Path.Combine(Path.GetDirectoryName(certPath)!, "devcert.pfx");
        var password = Guid.NewGuid().ToString("N")[..16];

        // Export to PFX first
        var exportProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"dev-certs https --export-path \"{pfxPath}\" --password \"{password}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        exportProcess?.WaitForExit();

        if (exportProcess?.ExitCode != 0 || !File.Exists(pfxPath))
        {
            throw new InvalidOperationException(
                "Failed to export ASP.NET Core development certificate. " +
                "Make sure it exists by running 'dotnet dev-certs https' first.");
        }

        // Convert PFX to PEM format
        try
        {
#pragma warning disable SYSLIB0057 // X509Certificate2 constructor is obsolete
            using var pfxCert = new X509Certificate2(
                pfxPath, password,
                X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057

            // Export certificate
            var certPem = pfxCert.ExportCertificatePem();
            File.WriteAllText(certPath, certPem);

            // Export private key - use AsymmetricAlgorithm to get the key
            using var privateKey = pfxCert.GetRSAPrivateKey() as AsymmetricAlgorithm
                ?? pfxCert.GetECDsaPrivateKey() as AsymmetricAlgorithm;

            if (privateKey is null)
            {
                throw new InvalidOperationException("Failed to extract private key from development certificate.");
            }

            if (privateKey is RSA rsa)
            {
                var keyPem = rsa.ExportRSAPrivateKeyPem();
                File.WriteAllText(keyPath, keyPem);
            }
            else if (privateKey is ECDsa ecdsa)
            {
                var keyPem = ecdsa.ExportECPrivateKeyPem();
                File.WriteAllText(keyPath, keyPem);
            }
            else
            {
                throw new InvalidOperationException("Unsupported private key type in development certificate.");
            }
        }
        finally
        {
            // Clean up PFX file
            if (File.Exists(pfxPath))
            {
                File.Delete(pfxPath);
            }
        }
    }

    /// <summary>
    /// Injects the appropriate environment variables to allow the resource to enable sending telemetry to the dashboard.
    /// </summary>
    /// <param name="builder">The Zitadel resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{ZitadelResource}"/>.</returns>
    public static IResourceBuilder<ZitadelResource> WithOtlpExporter(this IResourceBuilder<ZitadelResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Configure Zitadel to use OpenTelemetry
        builder.WithEnvironment("ZITADEL_TRACING_TYPE", "otel");
        builder.WithEnvironment("ZITADEL_METRICS_TYPE", "otel");
        OtlpConfigurationExtensions.WithOtlpExporter(builder);

        return builder;
    }

    /// <summary>
    /// Injects the appropriate environment variables to allow the resource to enable sending telemetry to the dashboard.
    /// </summary>
    /// <param name="builder">The Zitadel resource builder.</param>
    /// <param name="protocol">The protocol to use for the OTLP exporter. If not set, it will try gRPC then Http.</param>
    /// <returns>The <see cref="IResourceBuilder{ZitadelResource}"/>.</returns>
    public static IResourceBuilder<ZitadelResource> WithOtlpExporter(
        this IResourceBuilder<ZitadelResource> builder,
        OtlpProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Configure Zitadel to use OpenTelemetry
        builder.WithEnvironment("ZITADEL_TRACING_TYPE", "otel");
        builder.WithEnvironment("ZITADEL_METRICS_TYPE", "otel");
        OtlpConfigurationExtensions.WithOtlpExporter(builder, protocol);

        return builder;
    }
}
