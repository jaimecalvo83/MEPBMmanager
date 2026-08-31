namespace MEPBMmanager.Domain.Entities;

public class MarketPrice
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string Good { get; set; } = string.Empty;
    public int BuyPrice { get; set; } = 100;
    public int SellPrice { get; set; } = 50;
    public int Supply { get; set; } = 1000;
    
    public Game Game { get; set; } = null!;
}
