using System.Text.Json.Nodes;
using AlfaCredit.Core.Features.Scoring;
using AlfaCredit.Infrastructure.Scoring;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

public sealed class LogRegArtifactTests
{
    [Fact]
    public void Embedded_logreg_loads_the_bundled_artifact_and_hash()
    {
        var model = new EmbeddedScoringService(Options.Create(new ScoringOptions { ModelVersion = LogRegScoringService.ModelVersion }));
        model.ModelVersion.ShouldBe(LogRegScoringService.ModelVersion);
        model.ArtifactSha256!.Length.ShouldBe(64);
    }

    [Theory]
    [InlineData("scale")]
    [InlineData("columns")]
    [InlineData("grades")]
    public void Corrupt_artifact_is_rejected_before_inference(string field)
    {
        var path = Path.GetTempFileName();
        try
        {
            var data = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scoring", "logreg-synthetic-0.1.json")))!;
            if (field == "scale") data["scaling"]!["scale"]!["log_revenue"] = 0;
            else if (field == "columns") data["columns"]![0] = "unknown_feature";
            else data["grades"]![0]!["min_score"] = 75;
            File.WriteAllText(path, data.ToJsonString());
            Should.Throw<InvalidOperationException>(() => LogRegScoringService.Artifact.Load(path));
        }
        finally { File.Delete(path); }
    }
}
