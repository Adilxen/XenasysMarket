namespace Xenasys.Core.Entities;

/// <summary>
/// Automation target kinds — INVENTORY and ACCOUNTS_PAYABLE must remain unique.
/// </summary>
public static class ControlAccountKinds
{
    public const string Inventory = "INVENTORY";

    public const string AccountsPayable = "ACCOUNTS_PAYABLE";

    public const string Other = "OTHER";
}
