var builder = DistributedApplication.CreateBuilder(args);

var githubAppId = builder.AddParameter("github-app-id", secret: true);
var githubAppClientId = builder.AddParameter("github-app-client-id", secret: true);
var githubAppClientSecret = builder.AddParameter("github-app-client-secret", secret: true);
var githubAppPrivateKeyPem = builder.AddParameter("github-app-private-key-pem", secret: true);
var githubAppCallbackBaseUri = builder.AddParameter("github-app-callback-base-uri");
var internalApiToken = builder.AddParameter("internal-api-token", secret: true);

var apiService = builder.AddProject<Projects.HugoMatters_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("GitHubApp__AppId", githubAppId)
    .WithEnvironment("GitHubApp__PrivateKeyPem", githubAppPrivateKeyPem)
    .WithEnvironment("InternalApi__SharedSecret", internalApiToken);

builder.AddProject<Projects.HugoMatters_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithEnvironment("GitHubAuth__HostedGitHubAppClientId", githubAppClientId)
    .WithEnvironment("GitHubAuth__HostedGitHubAppClientSecret", githubAppClientSecret)
    .WithEnvironment("GitHubAuth__HostedSignInCallbackBaseUri", githubAppCallbackBaseUri)
    .WithEnvironment("InternalApi__SharedSecret", internalApiToken)
    .WaitFor(apiService);

builder.Build().Run();
