using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dapper;
using DcMateH5Api.BackgroundService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DcMateH5ApiTest.Form;

[Collection("DatabaseIntegration")]
public sealed class MultipleMappingComponentApiIntegrationTests
{
    private static readonly Guid FormMasterId = Guid.Parse("837CAD09-413D-4D35-AAD2-3A865B233B54");
    private const string BaseId = "202601211451707";
    private const string MappingRowId = "156202957262999";
    private const string ExpectedTargetColumn = "DESC";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RuntimeAndDesignerApis_ShouldExposeAndRoundTripComponentTargetValue()
    {
        var connectionString = LoadConnectionString();
        var original = await LoadMappingRowSnapshotAsync(connectionString);

        Assert.Equal(ExpectedTargetColumn, original.ComponentTargetColumn);
        Assert.Contains(original.Value, new[] { "DEMO-A", "DEMO-B" });

        var testValue = string.Equals(original.Value, "DEMO-A", StringComparison.Ordinal)
            ? "DEMO-B"
            : "DEMO-A";

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    var cleanupService = services.FirstOrDefault(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType == typeof(FormOrphanCleanupHostedService));

                    if (cleanupService != null)
                    {
                        services.Remove(cleanupService);
                    }
                });
            });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        try
        {
            var before = await QueryRuntimeAsync(client);
            Assert.Equal(ExpectedTargetColumn, before.MappingComponentTargetColumnName);
            Assert.Equal(original.Value, before.CurrentValue);
            Assert.Equal(6, before.ControlType);
            Assert.Equal(new[] { "DEMO-A", "DEMO-B" }, before.OptionValues);

            var designer = await QueryDesignerAsync(client);
            Assert.Equal(ExpectedTargetColumn, designer.MappingComponentTargetColumnName);
            Assert.Equal(original.Value, designer.CurrentValue);

            var updateResponse = await client.PutAsJsonAsync(
                $"/Form/FormMultipleMapping/{FormMasterId}/mapping-components/{MappingRowId}/value",
                new { Value = testValue });
            updateResponse.EnsureSuccessStatusCode();

            using (var updateJson = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync()))
            {
                Assert.Equal(1, updateJson.RootElement.GetProperty("Affected").GetInt32());
            }

            var updated = await QueryRuntimeAsync(client);
            Assert.Equal(testValue, updated.CurrentValue);

            var restoreResponse = await client.PutAsJsonAsync(
                $"/Form/FormMultipleMapping/{FormMasterId}/mapping-components/{MappingRowId}/value",
                new { Value = original.Value });
            restoreResponse.EnsureSuccessStatusCode();

            var restored = await QueryRuntimeAsync(client);
            Assert.Equal(original.Value, restored.CurrentValue);

            Console.WriteLine(
                $"FormMasterId={FormMasterId}; BaseId={BaseId}; MappingRowId={MappingRowId}; " +
                $"TargetColumn={restored.MappingComponentTargetColumnName}; " +
                $"RoundTrip={original.Value}->{testValue}->{restored.CurrentValue}");
        }
        finally
        {
            await RestoreMappingRowSnapshotAsync(connectionString, original);
        }
    }

    private static async Task<ComponentApiSnapshot> QueryRuntimeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            $"/Form/FormMultipleMapping/{FormMasterId}/items/query",
            new
            {
                BaseId,
                Type = 1,
                OrderBySeqAscending = true
            });
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var component = root
            .GetProperty("ComponentsByMappingRowId")
            .GetProperty(MappingRowId);

        return ReadComponentSnapshot(root, component);
    }

    private static async Task<ComponentApiSnapshot> QueryDesignerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            $"/Form/FormDesignerMultipleMapping/{FormMasterId}/mapping-components/query",
            new
            {
                BaseId,
                OrderBySeqAscending = true
            });
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var component = root
            .GetProperty("ComponentsByMappingRowId")
            .GetProperty(MappingRowId);

        return ReadComponentSnapshot(root, component);
    }

    private static ComponentApiSnapshot ReadComponentSnapshot(JsonElement root, JsonElement component)
    {
        var optionValues = component
            .GetProperty("Options")
            .EnumerateArray()
            .Select(option => option.GetProperty("Value").GetString() ?? string.Empty)
            .ToArray();

        return new ComponentApiSnapshot(
            root.GetProperty("MappingComponentTargetColumnName").GetString(),
            component.GetProperty("CurrentValue").GetString(),
            component.GetProperty("ControlType").GetInt32(),
            optionValues);
    }

    private static async Task<MappingRowDatabaseSnapshot> LoadMappingRowSnapshotAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<MappingRowDatabaseSnapshot>(@"
SELECT formMaster.MAPPING_COMPONENT_TARGET_COLUMN_NAME AS ComponentTargetColumn,
       mappingRow.[DESC] AS Value,
       mappingRow.EDIT_USER AS EditUser,
       mappingRow.EDIT_TIME AS EditTime
  FROM dbo.FORM_FIELD_MASTER AS formMaster
  JOIN dbo.WIP_ROUTE_OPERATION AS mappingRow
    ON mappingRow.WIP_ROUTE_OPERATION_SID = @MappingRowId
 WHERE formMaster.ID = @FormMasterId;",
            new
            {
                FormMasterId,
                MappingRowId = decimal.Parse(MappingRowId)
            });
    }

    private static async Task RestoreMappingRowSnapshotAsync(
        string connectionString,
        MappingRowDatabaseSnapshot snapshot)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(@"
UPDATE dbo.WIP_ROUTE_OPERATION
   SET [DESC] = @Value,
       EDIT_USER = @EditUser,
       EDIT_TIME = @EditTime
 WHERE WIP_ROUTE_OPERATION_SID = @MappingRowId;",
            new
            {
                snapshot.Value,
                snapshot.EditUser,
                snapshot.EditTime,
                MappingRowId = decimal.Parse(MappingRowId)
            });
    }

    private static string LoadConnectionString()
    {
        var directory = FindSolutionRoot(Environment.CurrentDirectory)
                        ?? FindSolutionRoot(AppContext.BaseDirectory)
                        ?? FindSolutionRoot(Path.GetDirectoryName(GetSourceFilePath())!)
                        ?? throw new DirectoryNotFoundException("找不到方案根目錄。");

        var json = File.ReadAllText(Path.Combine(
            directory.FullName,
            "src",
            "DcMateH5Api",
            "appsettings.Development.json"));
        using var document = JsonDocument.Parse(json);
        return document.RootElement
                   .GetProperty("ConnectionStrings")
                   .GetProperty("Connection")
                   .GetString()
               ?? throw new InvalidOperationException("找不到資料庫連線字串。");
    }

    private static DirectoryInfo? FindSolutionRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DcMateH5Api.sln")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string GetSourceFilePath([CallerFilePath] string filePath = "") => filePath;

    private sealed record ComponentApiSnapshot(
        string? MappingComponentTargetColumnName,
        string? CurrentValue,
        int ControlType,
        string[] OptionValues);

    private sealed class MappingRowDatabaseSnapshot
    {
        public string? ComponentTargetColumn { get; init; }
        public string? Value { get; init; }
        public string? EditUser { get; init; }
        public DateTime? EditTime { get; init; }
    }
}
