using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstateApi.Tests;

public class ApiIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public ApiIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostProperty_WithValidPayload_ReturnsCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var response = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostProperty_WithoutToken_ReturnsUnauthorized()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostProperty_WithInvalidPayload_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var invalidPayload = new
        {
            name = "ab",
            address = "a",
            house = true,
            purchasePrice = -1,
            currentValue = -1,
            purchaseDate = DateTime.UtcNow.Date
        };

        var response = await client.PostAsJsonAsync("/properties", invalidPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutProperty_WhenIdDoesNotExist_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var response = await client.PutAsJsonAsync("/properties/99999", ValidPropertyPayload());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchProperty_WithPartialPayload_UpdatesOnlyProvidedFields()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var createResponse = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());
        var propertyId = await ReadIdAsync(createResponse);

        var patchPayload = new
        {
            currentValue = 3_100_000m
        };

        var patchResponse = await client.PatchAsJsonAsync($"/properties/{propertyId}", patchPayload);
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var getResponse = await client.GetAsync($"/properties/{propertyId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var doc = await getResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);

        var currentValue = GetDecimalProperty(doc!.RootElement, "currentValue");
        Assert.Equal(3_100_000m, currentValue);
    }

    [Fact]
    public async Task DeleteProperty_WithExistingContract_ReturnsConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var propertyResponse = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());
        var propertyId = await ReadIdAsync(propertyResponse);

        var contractPayload = new
        {
            tenantName = "Jan Novak",
            monthlyRent = 18_500m,
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddMonths(12),
            propertyId
        };

        var contractResponse = await client.PostAsJsonAsync("/tenantcontracts", contractPayload);
        Assert.Equal(HttpStatusCode.Created, contractResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/properties/{propertyId}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetPropertyById_WhenIdDoesNotExist_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/properties/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchTenantContract_WithInvalidPropertyId_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var propertyResponse = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());
        var propertyId = await ReadIdAsync(propertyResponse);

        var contractPayload = new
        {
            tenantName = "Petr Svoboda",
            monthlyRent = 20_000m,
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddMonths(6),
            propertyId
        };

        var contractResponse = await client.PostAsJsonAsync("/tenantcontracts", contractPayload);
        var contractId = await ReadIdAsync(contractResponse);

        var patchPayload = new
        {
            propertyId = 99999
        };

        var patchResponse = await client.PatchAsJsonAsync($"/tenantcontracts/{contractId}", patchPayload);

        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    [Fact]
    public async Task PostTenantContract_WithMissingProperty_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var payload = new
        {
            tenantName = "Lukas Cerny",
            monthlyRent = 19_000m,
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddMonths(12),
            propertyId = 99999
        };

        var response = await client.PostAsJsonAsync("/tenantcontracts", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTenantContract_WithOverlappingPeriod_ReturnsConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var propertyResponse = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());
        var propertyId = await ReadIdAsync(propertyResponse);

        var firstContractPayload = new
        {
            tenantName = "Karel Dvorak",
            monthlyRent = 17_000m,
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddMonths(12),
            propertyId
        };

        var firstContractResponse = await client.PostAsJsonAsync("/tenantcontracts", firstContractPayload);
        Assert.Equal(HttpStatusCode.Created, firstContractResponse.StatusCode);

        var secondContractPayload = new
        {
            tenantName = "Eva Horakova",
            monthlyRent = 18_000m,
            startDate = DateTime.UtcNow.Date.AddMonths(3),
            endDate = DateTime.UtcNow.Date.AddMonths(9),
            propertyId
        };

        var secondContractResponse = await client.PostAsJsonAsync("/tenantcontracts", secondContractPayload);

        Assert.Equal(HttpStatusCode.Conflict, secondContractResponse.StatusCode);
    }

    [Fact]
    public async Task PatchTenantContract_IntoOverlappingPeriod_ReturnsConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var propertyResponse = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());
        var propertyId = await ReadIdAsync(propertyResponse);

        var firstContractPayload = new
        {
            tenantName = "Jan Vesely",
            monthlyRent = 16_500m,
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddMonths(3),
            propertyId
        };

        var firstContractResponse = await client.PostAsJsonAsync("/tenantcontracts", firstContractPayload);
        Assert.Equal(HttpStatusCode.Created, firstContractResponse.StatusCode);

        var secondContractPayload = new
        {
            tenantName = "Pavel Urban",
            monthlyRent = 16_800m,
            startDate = DateTime.UtcNow.Date.AddMonths(4),
            endDate = DateTime.UtcNow.Date.AddMonths(8),
            propertyId
        };

        var secondContractResponse = await client.PostAsJsonAsync("/tenantcontracts", secondContractPayload);
        Assert.Equal(HttpStatusCode.Created, secondContractResponse.StatusCode);

        var secondContractId = await ReadIdAsync(secondContractResponse);

        var patchPayload = new
        {
            startDate = DateTime.UtcNow.Date.AddMonths(2)
        };

        var patchResponse = await client.PatchAsJsonAsync($"/tenantcontracts/{secondContractId}", patchPayload);

        Assert.Equal(HttpStatusCode.Conflict, patchResponse.StatusCode);
    }

    [Fact]
    public async Task PostPayment_WithValidPayload_ReturnsCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var propertyResponse = await client.PostAsJsonAsync("/properties", ValidPropertyPayload());
        var propertyId = await ReadIdAsync(propertyResponse);

        var contractPayload = new
        {
            tenantName = "Roman Fiala",
            monthlyRent = 21_000m,
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddMonths(12),
            propertyId
        };

        var contractResponse = await client.PostAsJsonAsync("/tenantcontracts", contractPayload);
        var contractId = await ReadIdAsync(contractResponse);

        var paymentPayload = new
        {
            amount = 21_000m,
            paymentDate = DateTime.UtcNow.Date,
            dueDate = DateTime.UtcNow.Date.AddDays(5),
            tenantContractId = contractId
        };

        var paymentResponse = await client.PostAsJsonAsync("/payments", paymentPayload);

        Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);

        var paymentId = await ReadIdAsync(paymentResponse);
        Assert.True(paymentId > 0);
    }

    [Fact]
    public async Task PostPayment_WithMissingTenantContract_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await AuthenticateAsAsync(client);

        var paymentPayload = new
        {
            amount = 21_000m,
            paymentDate = DateTime.UtcNow.Date,
            dueDate = DateTime.UtcNow.Date.AddDays(5),
            tenantContractId = 99999
        };

        var paymentResponse = await client.PostAsJsonAsync("/payments", paymentPayload);

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
    }

    private static object ValidPropertyPayload() => new
    {
        name = "Byt Brno",
        address = "Masarykova 10, Brno",
        house = false,
        purchasePrice = 2_500_000m,
        currentValue = 2_900_000m,
        purchaseDate = DateTime.UtcNow.Date
    };

    private static async Task AuthenticateAsAsync(HttpClient client, string username = "admin", string password = "Admin123!")
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            username,
            password
        });

        loginResponse.EnsureSuccessStatusCode();

        var document = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();
        if (document == null)
            throw new InvalidOperationException("Login response body is empty.");

        var token = GetStringProperty(document.RootElement, "accessToken");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<int> ReadIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        if (document == null)
            throw new InvalidOperationException("Response body is empty.");

        return document.RootElement.GetProperty("id").GetInt32();
    }

    private static decimal GetDecimalProperty(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetDecimal();
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString() ?? string.Empty;
    }
}