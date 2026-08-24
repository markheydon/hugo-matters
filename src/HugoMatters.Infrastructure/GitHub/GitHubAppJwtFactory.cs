using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HugoMatters.Infrastructure.GitHub;

/// <summary>
/// Creates short-lived JWTs for GitHub App authentication.
/// </summary>
public sealed class GitHubAppJwtFactory
{
    private readonly GitHubAppOptions _options;

    /// <summary>
    /// Creates a new <see cref="GitHubAppJwtFactory"/>.
    /// </summary>
    public GitHubAppJwtFactory(IOptions<GitHubAppOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates a JWT valid for GitHub App API calls.
    /// </summary>
    public string CreateJwt()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("GitHub App credentials are not configured.");
        }

        // Export parameters so signing does not depend on a disposable RSA instance
        // (RsaSecurityKey(RSA) retains the RSA; disposing it breaks later sign/retry).
        RSAParameters rsaParameters;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(ResolvePrivateKeyPem());
            rsaParameters = rsa.ExportParameters(includePrivateParameters: true);
        }

        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsaParameters),
            SecurityAlgorithms.RsaSha256);

        // GitHub requires iss, iat, and exp. IssuedAt must be present as claim "iat".
        var now = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.AppId.ToString(),
            IssuedAt = now,
            NotBefore = now.AddSeconds(-60),
            Expires = now.AddMinutes(9),
            SigningCredentials = credentials,
        });

        return handler.WriteToken(token);
    }

    private string ResolvePrivateKeyPem()
    {
        var value = _options.PrivateKeyPem;
        if (value.Contains("BEGIN", StringComparison.Ordinal))
        {
            return value;
        }

        if (!File.Exists(value))
        {
            throw new InvalidOperationException("GitHub App private key file was not found.");
        }

        return File.ReadAllText(value);
    }
}
