using FluentAssertions;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace KartOfferService.ContractTests;

/// <summary>
/// Sanity-checks the committed contracts/api-contract.yaml still declares the operations this
/// service's controllers actually implement - catches the contract and the code silently drifting
/// apart (contracts/README.md: "never hand-edited here", only re-copied from the upstream design
/// record).
/// </summary>
public class ApiContractYamlTests
{
    private static YamlMappingNode LoadContract()
    {
        using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "Fixtures", "api-contract.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static string OperationId(YamlMappingNode root, string path, string method)
    {
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        var operation = (YamlMappingNode)((YamlMappingNode)paths.Children[new YamlScalarNode(path)]).Children[new YamlScalarNode(method)];
        return ((YamlScalarNode)operation.Children[new YamlScalarNode("operationId")]).Value!;
    }

    [Theory]
    [InlineData("/v1/coupons/validate", "post", "validateCoupon")]
    [InlineData("/v1/coupons/redeem", "post", "redeemCoupon")]
    [InlineData("/v1/coupons", "post", "issueCoupon")]
    [InlineData("/v1/coupons/{couponCode}", "get", "getCouponAdminView")]
    [InlineData("/v1/coupons/{couponCode}/deactivate", "post", "deactivateCoupon")]
    [InlineData("/v1/pricing/quote", "post", "getPricingQuote")]
    [InlineData("/v1/promotions/active", "get", "getActivePromotions")]
    [InlineData("/v1/promotions", "post", "createPromotionCampaign")]
    [InlineData("/v1/promotions/{campaignId}", "get", "getPromotionCampaignAdminView")]
    [InlineData("/v1/promotions/{campaignId}/deactivate", "post", "deactivatePromotionCampaign")]
    public void Contract_DeclaresExpectedOperationId(string path, string method, string expectedOperationId)
    {
        var root = LoadContract();

        OperationId(root, path, method).Should().Be(expectedOperationId);
    }

    [Fact]
    public void Contract_MoneySchema_RequiresAmountAndCurrency()
    {
        var root = LoadContract();
        var schemas = (YamlMappingNode)((YamlMappingNode)root.Children[new YamlScalarNode("components")]).Children[new YamlScalarNode("schemas")];
        var money = (YamlMappingNode)schemas.Children[new YamlScalarNode("Money")];
        var required = (YamlSequenceNode)money.Children[new YamlScalarNode("required")];

        required.Children.Select(n => ((YamlScalarNode)n).Value).Should().BeEquivalentTo("amount", "currency");
    }

    [Fact]
    public void Contract_CouponAdminViewSchema_IncludesVersionForOptimisticConcurrency()
    {
        var root = LoadContract();
        var schemas = (YamlMappingNode)((YamlMappingNode)root.Children[new YamlScalarNode("components")]).Children[new YamlScalarNode("schemas")];
        var couponAdminView = (YamlMappingNode)schemas.Children[new YamlScalarNode("CouponAdminView")];
        var properties = (YamlMappingNode)couponAdminView.Children[new YamlScalarNode("properties")];

        properties.Children.Should().ContainKey(new YamlScalarNode("version"));
    }
}
