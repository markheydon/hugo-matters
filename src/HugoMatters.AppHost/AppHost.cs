var builder = DistributedApplication.CreateBuilder(args);

var githubAppId = builder.AddParameter("github-app-id", secret: true);
var githubAppClientId = builder.AddParameter("github-app-client-id", secret: true);
var githubAppClientSecret = builder.AddParameter("github-app-client-secret", secret: true);
var githubAppPrivateKeyPem = builder.AddParameter("github-app-private-key-pem", secret: true);
var githubAppRedirectUri = builder.AddParameter(
    "github-app-redirect-uri",
    "http://localhost:5253/connect");

var apiService = builder.AddProject<Projects.HugoMatters_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("GitHubApp__AppId", githubAppId)
    .WithEnvironment("GitHubApp__ClientId", githubAppClientId)
    .WithEnvironment("GitHubApp__ClientSecret", githubAppClientSecret)
    .WithEnvironment("GitHubApp__PrivateKeyPem", githubAppPrivateKeyPem)
    .WithEnvironment("GitHubApp__RedirectUri", githubAppRedirectUri);

builder.AddProject<Projects.HugoMatters_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
