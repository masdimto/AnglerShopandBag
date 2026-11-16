using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace ShopAndAnglerBuffPlugin
{
    [ApiVersion(2, 1)]
    public class ShopAndAnglerBuff : TerrariaPlugin
    {
        public override string Name => "ShopAndAnglerBuff";
        public override Version Version => new Version(1, 0, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "Wizard Soul Shop, Angler Biome Crate Bonus, dan Princess Boss Treasure Bag Shop (/bagshop).";

        private Dictionary<int, int> lastAnglerQuests = new Dictionary<int, int>();
        private bool[] bagShopMode = new bool[Main.maxPlayers];

        public ShopAndAnglerBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

            Commands.ChatCommands.Add(new Command("shopangler.bagshop", ToggleBagShopCommand, "bagshop", "bshop")
            {
                HelpText = "Mengganti toko Princess antara toko normal dan toko Boss Treasure Bag."
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }
            base.Dispose(disposing);
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            int who = args.Who;
            if (who >= 0 && who < Main.maxPlayers)
            {
                bagShopMode[who] = false;
                TSPlayer player = TShock.Players[who];
                if (player != null && player.TPlayer != null)
                {
                    lastAnglerQuests[who] = player.TPlayer.anglerQuestsFinished;
                }
            }
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            int who = args.Who;
            if (who >= 0 && who < Main.maxPlayers)
            {
                bagShopMode[who] = false;
                lastAnglerQuests.Remove(who);
            }
        }

        private void ToggleBagShopCommand(CommandArgs args)
        {
            int who = args.Player.Index;
            if (who < 0 || who >= Main.maxPlayers)
                return;

            bagShopMode[who] = !bagShopMode[who];
            if (bagShopMode[who])
            {
                args.Player.SendSuccessMessage("[Boss Bag Shop] AKTIF! Buka toko Princess untuk membeli Treasure Bag boss yang sudah dikalahkan.");
            }
            else
            {
                args.Player.SendInfoMessage("[Boss Bag Shop] NONAKTIF. Toko Princess kembali menjual item normal.");
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                TSPlayer tsPlayer = TShock.Players[i];
                if (tsPlayer == null || !tsPlayer.Active || tsPlayer.TPlayer == null)
                    continue;

                Player player = tsPlayer.TPlayer;

                int currentQuests = player.anglerQuestsFinished;
                if (!lastAnglerQuests.TryGetValue(i, out int prevQuests))
                {
                    lastAnglerQuests[i] = currentQuests;
                }
                else if (currentQuests > prevQuests)
                {
                    lastAnglerQuests[i] = currentQuests;
                    int questFishId = Main.anglerQuestItemNetIDs[Main.anglerQuest];
                    int crateId = GetBiomeCrateForQuestFish(questFishId);
                    if (crateId > 0)
                    {
                        tsPlayer.GiveItem(crateId, 1);
                        string crateName = Lang.GetItemNameValue(crateId);
                        tsPlayer.SendSuccessMessage($"[Angler Bonus] Kamu mendapatkan 1x {crateName} sesuai biome Quest Fish!");
                    }
                }

                if (Main.GameUpdateCount % 10 == 0 && player.talkNPC >= 0 && player.talkNPC < Main.maxNPCs)
                {
                    NPC talkNpc = Main.npc[player.talkNPC];
                    if (talkNpc.active && talkNpc.type == NPCID.Princess && bagShopMode[i])
                    {
                        ApplyPrincessBagShop(tsPlayer);
                    }
                }
            }
        }

        private void ApplyPrincessBagShop(TSPlayer tsPlayer)
        {
            var bags = GetUnlockedBossBags();
            int maxSlots = Math.Min(bags.Count, 40);

            for (int slot = 0; slot < maxSlots; slot++)
            {
                SendCustomShopSlot(tsPlayer, slot, bags[slot].itemId, bags[slot].price);
            }
            for (int slot = maxSlots; slot < 40; slot++)
            {
                SendCustomShopSlot(tsPlayer, slot, 0, 0);
            }
        }

        private void SendCustomShopSlot(TSPlayer tsPlayer, int slot, int itemType, int price)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((short)0);
                writer.Write((byte)104);
                writer.Write((byte)slot);
                writer.Write((short)itemType);
                writer.Write((short)(itemType > 0 ? 1 : 0));
                writer.Write((byte)0);
                writer.Write(price);
                writer.Write((byte)0);

                byte[] data = ms.ToArray();
                short length = (short)data.Length;
                BitConverter.GetBytes(length).CopyTo(data, 0);

                tsPlayer.SendRawData(data);
            }
        }

        private List<(int itemId, int price)> GetUnlockedBossBags()
        {
            var list = new List<(int itemId, int price)>();

            void AddBag(bool condition, int itemId, int gold)
            {
                if (condition)
                    list.Add((itemId, Item.buyPrice(0, gold, 0, 0)));
            }

            AddBag(NPC.downedSlimeKing, ItemID.KingSlimeBossBag, 10);
            AddBag(NPC.downedBoss1, ItemID.EyeOfCthulhuBossBag, 15);
            AddBag(NPC.downedBoss2, ItemID.EaterOfWorldsBossBag, 20);
            AddBag(NPC.downedBoss2, ItemID.BrainOfCthulhuBossBag, 20);
            AddBag(NPC.downedQueenBee, ItemID.QueenBeeBossBag, 25);
            AddBag(NPC.downedBoss3, ItemID.SkeletronBossBag, 30);
            AddBag(NPC.downedDeerclops, ItemID.DeerclopsBossBag, 30);
            AddBag(Main.hardMode, ItemID.WallOfFleshBossBag, 40);
            AddBag(NPC.downedQueenSlime, ItemID.QueenSlimeBossBag, 50);
            AddBag(NPC.downedMechBoss1, ItemID.DestroyerBossBag, 60);
            AddBag(NPC.downedMechBoss2, ItemID.TwinsBossBag, 60);
            AddBag(NPC.downedMechBoss3, ItemID.SkeletronPrimeBossBag, 60);
            AddBag(NPC.downedPlantBoss, ItemID.PlanteraBossBag, 80);
            AddBag(NPC.downedGolemBoss, ItemID.GolemBossBag, 90);
            AddBag(NPC.downedFishron, ItemID.FishronBossBag, 100);
            AddBag(NPC.downedEmpressOfLight, ItemID.FairyQueenBossBag, 100);
            AddBag(NPC.downedAncientCultist, ItemID.CultistBossBag, 120);
            AddBag(NPC.downedMoonlord, ItemID.MoonLordBossBag, 150);

            return list;
        }

        private int GetBiomeCrateForQuestFish(int questFishId)
        {
            switch (questFishId)
            {
                case ItemID.AmanitaFungifin:
                case ItemID.Batfish:
                case ItemID.Bonefish:
                case ItemID.Dirtfish:
                case ItemID.Jewelfish:
                case ItemID.Spiderfish:
                    return Main.hardMode ? ItemID.GoldenCrateHard : ItemID.GoldenCrate;

                case ItemID.Bunnyfish:
                case ItemID.DynamiteFish:
                case ItemID.Harpyfish:
                case ItemID.Slimefish:
                case ItemID.Wyverntail:
                case ItemID.FallenStarfish:
                case ItemID.TheFishofCthulu:
                    return Main.hardMode ? ItemID.FloatingIslandFishingCrateHard : ItemID.FloatingIslandFishingCrate;

                case ItemID.CapnTunabeard:
                case ItemID.Clownfish:
                case ItemID.BumblebeeTuna:
                    return Main.hardMode ? ItemID.OceanCrateHard : ItemID.OceanCrate;

                case ItemID.Catfish:
                case ItemID.Derpfish:
                case ItemID.Mudfish:
                case ItemID.TropicalBarracuda:
                    return Main.hardMode ? ItemID.JungleFishingCrateHard : ItemID.JungleFishingCrate;

                case ItemID.Fishron:
                case ItemID.MutantFlinxfin:
                case ItemID.Pengfish:
                case ItemID.TundraTrout:
                    return Main.hardMode ? ItemID.FrozenCrateHard : ItemID.FrozenCrate;

                case ItemID.ScarabFish:
                case ItemID.ScorpioFish:
                    return Main.hardMode ? ItemID.OasisCrateHard : ItemID.OasisCrate;

                case ItemID.Cursedfish:
                case ItemID.EaterofPlankton:
                case ItemID.InfectedScabbardfish:
                    return Main.hardMode ? ItemID.CorruptFishingCrateHard : ItemID.CorruptFishingCrate;

                case ItemID.BloodyManowar:
                case ItemID.Ichorfish:
                    return Main.hardMode ? ItemID.CrimsonFishingCrateHard : ItemID.CrimsonFishingCrate;

                case ItemID.MirageFish:
                case ItemID.Pixiefish:
                case ItemID.UnicornFish:
                    return Main.hardMode ? ItemID.HallowedFishingCrateHard : ItemID.HallowedFishingCrate;

                case ItemID.DemonicHellfish:
                case ItemID.GuideVoodooFish:
                case ItemID.Hungerfish:
                    return Main.hardMode ? ItemID.LavaCrateHard : ItemID.LavaCrate;

                default:
                    return Main.hardMode ? ItemID.IronCrateHard : ItemID.IronCrate;
            }
        }
    }
}
