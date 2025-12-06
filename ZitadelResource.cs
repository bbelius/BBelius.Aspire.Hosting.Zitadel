#nullable enable

using System;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace BBelius.Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Zitadel resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="adminUsername">A parameter that contains the Zitadel admin username, or <see langword="null"/> to use a default value.</param>
/// <param name="adminPassword">A parameter that contains the Zitadel admin password.</param>
/// <param name="masterKey">A parameter that contains the Zitadel master key for encryption.</param>
public sealed class ZitadelResource(
    string name,
    ParameterResource? adminUsername,
    ParameterResource adminPassword,
    ParameterResource masterKey)
    : ContainerResource(name), IResourceWithServiceDiscovery
{
    /// <summary>
    /// The default admin username for Zitadel.
    /// </summary>
    public const string DefaultAdminUsername = "admin@zitadel.localhost";

    internal const string PrimaryEndpointName = "https";

    /// <summary>
    /// Gets the parameter that contains the Zitadel admin username.
    /// </summary>
    public ParameterResource? AdminUserNameParameter { get; } = adminUsername;

    internal ReferenceExpression AdminUsernameReference =>
        AdminUserNameParameter is not null ?
            ReferenceExpression.Create($"{AdminUserNameParameter}") :
            ReferenceExpression.Create($"{DefaultAdminUsername}");

    /// <summary>
    /// Gets the parameter that contains the Zitadel admin password.
    /// </summary>
    public ParameterResource AdminPasswordParameter { get; } = adminPassword ?? throw new ArgumentNullException(nameof(adminPassword));

    /// <summary>
    /// Gets the parameter that contains the Zitadel master key used for encryption.
    /// </summary>
    /// <remarks>
    /// The master key must be exactly 32 characters long.
    /// </remarks>
    public ParameterResource MasterKeyParameter { get; } = masterKey ?? throw new ArgumentNullException(nameof(masterKey));
}
