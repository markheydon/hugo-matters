using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HugoMatter.Infrastructure.GitHub;

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

        using var rsa = RSA.Create();
        rsa.ImportFromPem(ResolvePrivateKeyPem());

        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.AppId.ToString(),
            notBefore: now.AddSeconds(-60),
            expires: now.AddMinutes(9),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
