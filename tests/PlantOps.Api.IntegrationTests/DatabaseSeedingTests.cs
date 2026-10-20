using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Infrastructure.Identity;
using PlantOps.Infrastructure.Persistence;

namespace PlantOps.Api.IntegrationTests;

/// <summary>
/// The seeding commands that deployments run (`seed-demo`, `create-admin`). Each test uses its own empty database on the
/// shared SQL Server container, because the demo accounts reuse the fixed ids of the shared test users.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DatabaseSeedingTests(PlantOpsApiFactory factory)
{
    private const string DemoPassword = "Demo-Seed-Pw1-first";
    private const string ChangedDemoPassword = "Demo-Seed-Pw1-second";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Demo_seed_creates_the_public_accounts_without_Admin_and_the_sample_work_orders()
    {
        await using var api = ApiOnDatabase("PlantOpsSeedDemo", DemoPassword);
        await MigrateAsync(api.Services);

        await api.Services.SeedDemoDataAsync(Ct);

        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var accounts = await users.Users.OrderBy(u => u.Email).ToListAsync(Ct);
        accounts.Select(u => u.Email).Should().Equal(
            "staff@plantops.test", "supervisor@plantops.test", "tech.a@plantops.test", "tech.b@plantops.test");
        foreach (var account in accounts)
        {
            (await users.GetRolesAsync(account)).Should().ContainSingle().Which.Should().NotBe(Roles.Admin);
            (await users.GetLockoutEnabledAsync(account)).Should().BeFalse("the password is public, so lockout would only let visitors lock each other out");
            (await users.CheckPasswordAsync(account, DemoPassword)).Should().BeTrue();
        }

        (await scope.ServiceProvider.GetRequiredService<PlantOpsDbContext>().WorkOrders.CountAsync(Ct)).Should().Be(10);
    }

    [Fact]
    public async Task Demo_seed_runs_again_safely_and_applies_a_changed_password()
    {
        const string database = "PlantOpsSeedTwice";
        await using (var first = ApiOnDatabase(database, DemoPassword))
        {
            await MigrateAsync(first.Services);
            await first.Services.SeedDemoDataAsync(Ct);
        }

        await using var second = ApiOnDatabase(database, ChangedDemoPassword);
        await second.Services.SeedDemoDataAsync(Ct);

        await using var scope = second.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.Users.CountAsync(Ct)).Should().Be(4);
        var staff = await users.FindByEmailAsync("staff@plantops.test");
        (await users.CheckPasswordAsync(staff!, ChangedDemoPassword)).Should().BeTrue();
        (await users.CheckPasswordAsync(staff!, DemoPassword)).Should().BeFalse();
        (await scope.ServiceProvider.GetRequiredService<PlantOpsDbContext>().WorkOrders.CountAsync(Ct)).Should().Be(10);
    }

    [Fact]
    public async Task Demo_seed_refuses_a_password_the_policy_rejects_and_keeps_the_old_one()
    {
        const string database = "PlantOpsSeedWeakPassword";
        await using (var first = ApiOnDatabase(database, DemoPassword))
        {
            await MigrateAsync(first.Services);
            await first.Services.SeedDemoDataAsync(Ct);
        }

        // Too short and no symbol: Identity's policy rejects it, and the accounts must not be left without a password.
        await using var second = ApiOnDatabase(database, "weak");
        var seed = () => second.Services.SeedDemoDataAsync(Ct);

        await seed.Should().ThrowAsync<InvalidOperationException>().WithMessage("*staff@plantops.test*");
        await using var scope = second.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var staff = await users.FindByEmailAsync("staff@plantops.test");
        (await users.CheckPasswordAsync(staff!, DemoPassword)).Should().BeTrue();
    }

    [Fact]
    public async Task Create_admin_run_again_with_a_new_password_sets_it()
    {
        const string database = "PlantOpsCreateAdminTwice";
        await using (var first = ApiOnDatabase(database, DemoPassword, builder => builder
            .UseSetting(DatabaseInitializer.AdminEmailKey, "owner@example.test")
            .UseSetting(DatabaseInitializer.AdminPasswordKey, "Owner-Admin-Pw1")))
        {
            await MigrateAsync(first.Services);
            await first.Services.CreateAdminAsync();
        }

        await using var second = ApiOnDatabase(database, DemoPassword, builder => builder
            .UseSetting(DatabaseInitializer.AdminEmailKey, "owner@example.test")
            .UseSetting(DatabaseInitializer.AdminPasswordKey, "Owner-Admin-Pw2-new"));
        await second.Services.CreateAdminAsync();

        await using var scope = second.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = (await users.Users.ToListAsync(Ct)).Should().ContainSingle().Subject;
        (await users.CheckPasswordAsync(admin, "Owner-Admin-Pw2-new")).Should().BeTrue();
        (await users.CheckPasswordAsync(admin, "Owner-Admin-Pw1")).Should().BeFalse();
    }

    [Fact]
    public async Task Create_admin_adds_one_Admin_and_running_it_again_changes_nothing()
    {
        await using var api = ApiOnDatabase("PlantOpsCreateAdmin", DemoPassword, builder => builder
            .UseSetting(DatabaseInitializer.AdminEmailKey, "owner@example.test")
            .UseSetting(DatabaseInitializer.AdminPasswordKey, "Owner-Admin-Pw1"));
        await MigrateAsync(api.Services);

        await api.Services.CreateAdminAsync();
        await api.Services.CreateAdminAsync();

        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = (await users.Users.ToListAsync(Ct)).Should().ContainSingle().Subject;
        admin.Email.Should().Be("owner@example.test");
        (await users.GetRolesAsync(admin)).Should().Equal(Roles.Admin);
        (await users.GetLockoutEnabledAsync(admin)).Should().BeTrue("the Admin's password is private, so lockout stays on");
    }

    [Fact]
    public async Task Demo_seed_removes_a_role_a_demo_account_should_not_have()
    {
        await using var api = ApiOnDatabase("PlantOpsSeedExtraRole", DemoPassword);
        await MigrateAsync(api.Services);
        await api.Services.SeedDemoDataAsync(Ct);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var staff = await users.FindByEmailAsync("staff@plantops.test");
            (await users.AddToRoleAsync(staff!, Roles.Admin)).Succeeded.Should().BeTrue();
        }

        await api.Services.SeedDemoDataAsync(Ct);

        await using var check = api.Services.CreateAsyncScope();
        var checkUsers = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reseeded = await checkUsers.FindByEmailAsync("staff@plantops.test");
        (await checkUsers.GetRolesAsync(reseeded!)).Should().Equal(Roles.Staff);
    }

    [Fact]
    public async Task Create_admin_refuses_a_demo_account()
    {
        // No migration: the check comes before any database access, so reaching the database would fail this test.
        await using var api = ApiOnDatabase("PlantOpsCreateAdminDemoEmail", DemoPassword, builder => builder
            .UseSetting(DatabaseInitializer.AdminEmailKey, "Staff@PlantOps.test")
            .UseSetting(DatabaseInitializer.AdminPasswordKey, "Owner-Admin-Pw1"));

        var createAdmin = () => api.Services.CreateAdminAsync();

        await createAdmin.Should().ThrowAsync<InvalidOperationException>().WithMessage("*demo account*");
    }

    private WebApplicationFactory<Program> ApiOnDatabase(string database, string demoPassword, Action<IWebHostBuilder>? configure = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:PlantOps",
                new SqlConnectionStringBuilder(factory.ConnectionString) { InitialCatalog = database }.ConnectionString);
            builder.UseSetting(DatabaseInitializer.DemoPasswordKey, demoPassword);
            configure?.Invoke(builder);
        });

    private static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync(Ct);
        await scope.ServiceProvider.GetRequiredService<PlantOpsDbContext>().Database.MigrateAsync(Ct);
    }
}
