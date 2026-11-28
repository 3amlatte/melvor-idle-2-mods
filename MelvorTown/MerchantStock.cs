using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Data.Currencies;
using GamesByMalcs.Melvor2.Entities.Bank;
using Godot;
using MelvorModLoader;

namespace MelvorTown;

internal static class MerchantStock
{
    internal sealed record TownItem(string Id, string Name, string Description, long Price);

    internal sealed record PlaceholderItem(string Id, string Name, string Description, long Price);

    internal static readonly TownItem[] TownItems =
    {
        new("sickle", "Golden Sickle", "Farms produce 25% more food.", 750),
        new("charter", "Town Charter", "+3 population capacity.", 900),
        new("banner", "Festival Banner", "Villagers arrive 20% faster.", 850),
        new("ledger", "Merchant's Ledger", "10% off everything the merchant sells.", 1100),
        new("horseshoe", "Lucky Horseshoe", "Merchants visit 25% more often.", 650),
        new("quarrydeed", "Quarry Deed", "+1 food from every farm.", 1000),
    };

    // Placeholder regular stock until the item database exists. Buying these only
    // records the sale; the real items will be wired up later.
    static readonly PlaceholderItem[] PlaceholderItems =
    {
        new("iron-ore", "Iron Ore", "Placeholder stock - item database coming later.", 45),
        new("oak-logs", "Oak Logs", "Placeholder stock - item database coming later.", 30),
        new("coal", "Coal", "Placeholder stock - item database coming later.", 60),
        new("healing-potion", "Healing Potion", "Placeholder stock - item database coming later.", 120),
        new("leather", "Leather", "Placeholder stock - item database coming later.", 80),
        new("silver-bar", "Silver Bar", "Placeholder stock - item database coming later.", 200),
        new("carrot-seeds", "Carrot Seeds", "Placeholder stock - item database coming later.", 25),
        new("linen", "Linen", "Placeholder stock - item database coming later.", 70),
    };

    static Game? CurrentGame => GameAccess.CurrentGame;

    static BankEntity? Bank() =>
        CurrentGame?.Entities.GetAllObjects().OfType<BankEntity>().FirstOrDefault();

    static Currency? Gold()
    {
        Game? game = CurrentGame;
        return game != null && game.Data.TryGetObject<Currency>("MelvorBase:GoldPieces", out Currency? gold)
            ? gold
            : null;
    }

    public static long GoldAmount()
    {
        BankEntity? bank = Bank();
        Currency? gold = Gold();
        return bank == null || gold == null ? 0 : bank.Wallet.GetQuantity(gold);
    }

    static bool SpendGold(long amount)
    {
        BankEntity? bank = Bank();
        Currency? gold = Gold();
        if (bank == null || gold == null || bank.Wallet.GetQuantity(gold) < amount) return false;
        bank.Wallet.RemoveQuantity(gold, amount);
        return true;
    }

    public static long PriceOf(TownState state, MerchantOffer offer) =>
        state.HasPerk("ledger") ? (long)Math.Ceiling(offer.Price * 0.9) : offer.Price;

    public static void Roll(TownState state)
    {
        var offers = new List<MerchantOffer>();

        foreach (TownItem town in TownItems
                     .Where(t => !state.HasPerk(t.Id))
                     .OrderBy(_ => Random.Shared.Next())
                     .Take(2))
        {
            offers.Add(new MerchantOffer
            {
                Id = town.Id,
                Name = town.Name,
                Description = town.Description,
                Price = town.Price,
                Quantity = 1,
                TownItem = true,
            });
        }

        foreach (PlaceholderItem item in PlaceholderItems.OrderBy(_ => Random.Shared.Next()))
        {
            if (offers.Count >= 6) break;
            int quantity = Random.Shared.Next(1, 5);
            offers.Add(new MerchantOffer
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price * quantity,
                Quantity = quantity,
            });
        }

        state.Offers = offers.Take(6).ToList();
        state.Save();
    }

    public static bool Buy(TownState state, MerchantOffer offer, out string message)
    {
        if (offer.Bought >= offer.Quantity) { message = "Sold out."; return false; }

        long price = PriceOf(state, offer);
        if (!SpendGold(price)) { message = "Not enough gold."; return false; }

        if (offer.TownItem)
        {
            state.Perks.Add(offer.Id);
            message = "Relic acquired.";
        }
        else
        {
            message = "Placeholder purchase recorded. Item wiring comes with the database.";
        }

        offer.Bought += 1;
        state.Save();
        return true;
    }
}
