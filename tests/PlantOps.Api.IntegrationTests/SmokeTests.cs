using System.Net;
using PlantOps.Api.IntegrationTests.Infrastructure;

namespace PlantOps.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SmokeTests(PlantOpsApiFactory factory)
{
    [Fact]
    public async Task Signed_in_user_can_list_plant_units_from_the_real_database()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Staff);

        var response = await client.GetAsync("/api/plant-units", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
