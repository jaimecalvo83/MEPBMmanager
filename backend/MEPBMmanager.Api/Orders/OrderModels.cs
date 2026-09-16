using System.Text.Json;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Api.Orders;

/// <summary>HTTP shapes for the orders endpoints (no logic).</summary>
public record OrderFieldOptionDto(string Value, string Label);
public record OrderFieldSpecDto(string Key, string Label, string Kind, bool Required,
    int? Min = null, int? Max = null, string? Def = null, List<OrderFieldOptionDto>? Options = null);
public record EstimateAfterOrderDto(int Code, System.Text.Json.JsonElement? Parameters);
public record EstimateOrderRequest(string CharacterId, int Code, System.Text.Json.JsonElement? Parameters,
    string? ArmyId, string? NavyId, EstimateAfterOrderDto? AfterOrder);

public record SubmitOrderRequest(string CharacterId, int Code, System.Text.Json.JsonElement? Parameters, string? ArmyId, string? NavyId);
public record OrderValidationResult
{
    public string OrderId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public int OrderCode { get; set; }
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = new();
}


/// <summary>Resolved context for one estimate: game graph + effective location + params + language.</summary>
public sealed class EstimateCtx
{
    public EstimateCtx(Game game, Nation nation, Character ch, Army? army, Navy? navy,
        string? effLoc, Dictionary<string, System.Text.Json.JsonElement> pars, string lang)
    { Game = game; Nation = nation; Ch = ch; Army = army; Navy = navy; EffLoc = effLoc; Pars = pars; Lang = lang; }
    public Game Game { get; }
    public Nation Nation { get; }
    public Character Ch { get; }
    public Army? Army { get; }
    public Navy? Navy { get; }
    public string? EffLoc { get; }
    public Dictionary<string, System.Text.Json.JsonElement> Pars { get; }
    public string Lang { get; }
}

/// <summary>Usage already committed by other pending orders this turn.</summary>
public sealed class PendingUsage
{
    public Dictionary<string, int> RecruitsByPc = new();
    public Dictionary<string, int> BuyByProduct = new();
    public int SellGoldUsed;
    public HashSet<string> FortifiedPcs = new();
    public HashSet<string> DoubleAgentNations = new();
    public HashSet<string> ChallengedTargets = new();
    public HashSet<string> BribedTargets = new();
    public HashSet<string> MovedArtifacts = new();
}

/// <summary>Service-level failure mapped to an HTTP status by the controller.</summary>
public sealed class OrderRequestException : Exception
{
    public int Status { get; }
    public OrderRequestException(int status, string message) : base(message) { Status = status; }
}
