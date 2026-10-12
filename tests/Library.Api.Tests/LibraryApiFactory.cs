using System.Net.Http.Headers;
using System.Net.Http.Json;
using Library.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Library.Api.Tests;

/// <summary>
/// Hosts the real API in memory against a private in-memory SQLite database and a controllable clock.
/// Migrations and seeding run exactly as in production start-up.
/// </summary>
public sealed class LibraryApiFactory : WebApplicationFactory<Program>
{
    public const string LibrarianEmail = "librarian@test.local";
    public const string LibrarianPassword = "Librarian123";

    private readonly string _connectionString = $"Data Source=library-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;

    public LibraryApiFactory()
    {
        // An in-memory SQLite database lives as long as at least one connection to it is open.
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Library", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:LibrarianEmail", LibrarianEmail);
        builder.UseSetting("Seed:LibrarianPassword", LibrarianPassword);

        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public async Task<HttpClient> CreateClientAsAsync(string email, string password)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<AccessToken>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return client;
    }

    public Task<HttpClient> CreateLibrarianClientAsync() => CreateClientAsAsync(LibrarianEmail, LibrarianPassword);

    /// <summary>Registers a new member (unique email unless given) and returns a client signed in as them.</summary>
    public async Task<HttpClient> CreateMemberClientAsync(string? email = null)
    {
        email ??= $"member-{Guid.NewGuid():N}@example.com";
        var register = await CreateClient().PostAsJsonAsync("/api/auth/register",
            new { name = "Test Member", email, password = "Password123" });
        register.EnsureSuccessStatusCode();
        return await CreateClientAsAsync(email, "Password123");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
