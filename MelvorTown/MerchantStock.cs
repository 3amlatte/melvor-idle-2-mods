using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Inventories;
using GamesByMalcs.Melvor2.Data.Currencies;
using GamesByMalcs.Melvor2.Data.Items;
using GamesByMalcs.Melvor2.Entities.Bank;
using Godot;
using MelvorModLoader;

namespace MelvorTown;

internal static class MerchantStock
{
    internal sealed record TownItem(string Id, string Name, string Description, long Price);

    static readonly TownItem[] TownItems =
    {
        new("sickle", "Golden Sickle", "Farms produce 25% more food.", 750),
        new("charter", "Town Charter", "+3 population capacity.", 900),
        new("banner", "Festival Banner", "Villagers arrive 20% faster.", 850),
        new("ledger", "Merchant's Ledger", "10% off everything the merchant sells.", 1100),
        new("horseshoe", "Lucky Horseshoe", "Merchants visit 25% more often.", 650),
        new("quarrydeed", "Quarry Deed", "+1 food from every farm.", 1000),
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

    public static Texture2D? IconFor(string id) => FindItem(id)?.Icon;

    static Item? FindItem(string id)
    {
        Game? game = CurrentGame;
        return game != null && game.Data.TryGetObject<Item>(id, out Item? item) ? item : null;
    }

    public static void Roll(TownState state)
    {
        Game? game = CurrentGame;
        if (game == null) return;

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

        List<Item> pool = game.Data.GetDataRegistryOutput<Item>().GetAllObjects()
            .Where(i => i != Item.Placeholder && i.SellsFor.Quantity > 0 &&
                        !string.IsNullOrWhiteSpace(i.Id) && i.DisplayName.ToString().Length > 0)
            .OrderBy(_ => Random.Shared.Next())
            .Take(12)
            .ToList();

        foreach (Item item in pool)
        {
            if (offers.Count >= 6) break;
            int quantity = Random.Shared.Next(1, 5);
            long price = (long)(item.SellsFor.Quantity * quantity * 2.5);
            offers.Add(new MerchantOffer
            {
                Id = item.Id,
                Name = item.DisplayName.ToString(),
                Description = "",
                Price = Math.Clamp(price, 25, 4000),
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
        if (GoldAmount() < price) { message = "Not enough gold."; return false; }

        Item? item = offer.TownItem ? null : FindItem(offer.Id);
        BankEntity? bank = Bank();

        if (!offer.TownItem && (item == null || bank == null))
        {
            message = "That item is no longer available.";
            return false;
        }

        if (!SpendGold(price)) { message = "Not enough gold."; return false; }

        if (offer.TownItem)
        {
            state.Perks.Add(offer.Id);
        }
        else
        {
            bank!.Inventory.AddQuantity(item!, 1, AddItemFlags.Found);
        }

        offer.Bought += 1;
        state.Save();
        message = "Bought.";
        return true;
    }
}
