using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace ShopandAnglerBuffPlugin
{
    [ApiVersion(2, 1)]
    public class ShopandAnglerBuff : TerrariaPlugin
    {
        public override string Name => "ShopandAnglerBuff";
        public override Version Version => new Version(2, 0, 1);
        public override string Author => "CustomPlugin";
        public override string Description => "Clothier menjual Treasure Bag boss yang sudah dikalahkan dengan harga custom & hadiah 2x Crate sesuai bioma saat menyelesaikan quest Angler.";

        private Dictionary<int, int> lastQuestCount = new Dictionary<int, int>();
        private static HashSet<int> defeatedMiniBosses = new HashSet<int>();

        public ShopandAnglerBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            ServerApi.Hooks.NpcAIUpdate.Register(this, OnNpcAIUpdate);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
                ServerApi.Hooks.NpcAIUpdate.Deregister(this, OnNpcAIUpdate);
            }
            base.Dispose(disposing);
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            int who = args.Who;
            if (who >= 0 && who < Main.maxPlayers)
            {
                TSPlayer player = TShock.Players[who];
                if (player != null && player.TPlayer != null)
                    lastQuestCount[who] = player.TPlayer.anglerQuestsFinished;
            }
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            lastQuestCount.Remove(args.Who);
        }

        private void OnNpcKilled(NpcKilledEventArgs args)
        {
            if (args.npc == null) return;
            int type = args.npc.type;

            if (type == NPCID.DD2Betsy)
            {
                defeatedMiniBosses.Add(type);
            }
        }

        private int Coins(int platinum, int gold, int silver = 0, int copper = 0)
        {
            return (platinum * 1000000) + (gold * 10000) + (silver * 100) + copper;
        }

        // Mengisi toko Clothier dengan Treasure Bag dari Boss yang sudah dikalahkan
        private void OnNpcAIUpdate(NpcAiUpdateEventArgs args)
        {
            NPC npc = args.Npc;
            if (npc == null || !npc.active || npc.type != NPCID.Clothier)
                return;

            bool anyPlayerTalking = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p != null && p.active && p.talkNPC == npc.whoAmI)
                {
                    anyPlayerTalking = true;
                    break;
                }
            }

            if (!anyPlayerTalking) return;

            Chest shop = Main.instance.shop[Main.npcShop];
            if (shop == null || shop.item == null) return;

            List<(int bagId, int price)> availableBags = GetDefeatedBossBags();
            if (availableBags.Count == 0) return;

            int maxSlots = shop.item.Length;
            int slot = 0;

            while (slot < maxSlots && shop.item[slot] != null && shop.item[slot].type > 0)
            {
                if (shop.item[slot].type == availableBags[0].bagId)
                    return;
                slot++;
            }

            foreach (var entry in availableBags)
            {
                if (slot >= maxSlots) break;

                Item bagItem = new Item();
                bagItem.SetDefaults(entry.bagId);
                bagItem.shopCustomPrice = entry.price;
                shop.item[slot] = bagItem;
                slot++;
            }
        }

        private List<(int bagId, int price)> GetDefeatedBossBags()
        {
            var list = new List<(int bagId, int price)>();

            // === PRE-HARDMODE (25 - 50 Gold) ===
            if (NPC.downedSlimeKing)
                list.Add((ItemID.KingSlimeBossBag, Coins(0, 25)));

            if (NPC.downedBoss1) // Eye of Cthulhu
                list.Add((ItemID.EyeOfCthulhuBossBag, Coins(0, 30)));

            if (NPC.downedBoss2) // EoW / BoC
            {
                if (WorldGen.crimson)
                    list.Add((ItemID.BrainOfCthulhuBossBag, Coins(0, 35)));
                else
                    list.Add((ItemID.EaterOfWorldsBossBag, Coins(0, 35)));
            }

            if (NPC.downedQueenBee)
                list.Add((ItemID.QueenBeeBossBag, Coins(0, 40)));

            if (NPC.downedBoss3) // Skeletron
                list.Add((ItemID.SkeletronBossBag, Coins(0, 45)));

            if (NPC.downedDeerclops)
                list.Add((ItemID.DeerclopsBossBag, Coins(0, 50)));

            // === WALL OF FLESH (80 Gold) ===
            if (Main.hardMode)
                list.Add((ItemID.WallOfFleshBossBag, Coins(0, 80)));

            // === HARDMODE BOSSES ===
            if (NPC.downedQueenSlime)
                list.Add((ItemID.QueenSlimeBossBag, Coins(1, 17)));

            if (NPC.downedMechBoss1) // The Destroyer
                list.Add((ItemID.DestroyerBossBag, Coins(1, 31)));

            if (NPC.downedMechBoss2) // The Twins
                list.Add((ItemID.TwinsBossBag, Coins(1, 31)));

            if (NPC.downedMechBoss3) // Skeletron Prime
                list.Add((ItemID.SkeletronPrimeBossBag, Coins(1, 31)));

            if (NPC.downedPlantBoss)
                list.Add((ItemID.PlanteraBossBag, Coins(1, 78)));

            if (NPC.downedGolemBoss)
                list.Add((ItemID.GolemBossBag, Coins(2, 31)));

            if (defeatedMiniBosses.Contains(NPCID.DD2Betsy) || Terraria.GameContent.Events.DD2Event.DownedInvasionT3)
                list.Add((ItemID.BossBagBetsy, Coins(4, 51)));

            if (NPC.downedFishron)
                list.Add((ItemID.FishronBossBag, Coins(7, 89)));

            if (NPC.downedEmpressOfLight)
                list.Add((ItemID.FairyQueenBossBag, Coins(10, 23)));

            if (NPC.downedMoonlord)
                list.Add((ItemID.MoonLordBossBag, Coins(50, 0)));

            return list;
        }

        // Deteksi penyelesaian quest Angler -> Beri 2x Crate sesuai bioma ikan quest
        private void OnGameUpdate(EventArgs args)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                TSPlayer tsPlayer = TShock.Players[i];
                if (tsPlayer == null || !tsPlayer.Active || tsPlayer.TPlayer == null)
                    continue;

                int currentQuests = tsPlayer.TPlayer.anglerQuestsFinished;
                if (!lastQuestCount.TryGetValue(i, out int prevQuests))
                {
                    lastQuestCount[i] = currentQuests;
                    continue;
                }

                if (currentQuests > prevQuests)
                {
                    lastQuestCount[i] = currentQuests;

                    int questFishType = Main.anglerQuestItemNetIDs[Main.anglerQuest];
                    int crateId = GetCrateForQuestFish(questFishType, Main.hardMode);

                    if (crateId > 0)
                    {
                        tsPlayer.GiveItem(crateId, 2);
                        string crateName = Lang.GetItemNameValue(crateId);
                        tsPlayer.SendSuccessMessage($"[Angler Reward] Quest selesai! Kamu mendapatkan bonus 2x {crateName}!");
                    }
                }
            }
        }

        private int GetCrateForQuestFish(int fishId, bool isHardmode)
        {
            switch (fishId)
            {
                // Sky / Floating Island
                case ItemID.Angelfish:
                case ItemID.Cloudfish:
                case ItemID.FallenStarfish:
                case ItemID.TheFishofCthulu:
                case ItemID.Wyverntail:
                    return isHardmode ? ItemID.FloatingIslandFishingCrateHard : ItemID.FloatingIslandFishingCrate;

                // Ocean
                case ItemID.CapnTunabeard:
                case ItemID.Clownfish:
                case ItemID.DemonicHellfish:
                case ItemID.Fishron:
                case ItemID.Pengfish:
                case ItemID.Pixiefish:
                case ItemID.Slimefish:
                case ItemID.TropicalBarracuda:
                    return isHardmode ? ItemID.OceanCrateHard : ItemID.OceanCrate;

                // Jungle
                case ItemID.Catfish:
                case ItemID.Derpfish:
                case ItemID.Mudfish:
                    return isHardmode ? ItemID.JungleFishingCrateHard : ItemID.JungleFishingCrate;

                // Snow / Tundra
                case ItemID.Fishotron:
                case ItemID.MutantFlinxfin:
                case ItemID.TundraTrout:
                    return isHardmode ? ItemID.FrozenCrateHard : ItemID.FrozenCrate;

                // Desert / Oasis
                case ItemID.ScarabFish:
                case ItemID.ScorpioFish:
                    return isHardmode ? ItemID.OasisCrateHard : ItemID.OasisCrate;

                // Corruption
                case ItemID.Cursedfish:
                case ItemID.EaterofPlankton:
                case ItemID.InfectedScabbardfish:
                    return isHardmode ? ItemID.CorruptFishingCrateHard : ItemID.CorruptFishingCrate;

                // Crimson
                case ItemID.BloodyManowar:
                case ItemID.Ichorfish:
                    return isHardmode ? ItemID.CrimsonFishingCrateHard : ItemID.CrimsonFishingCrate;

                // Hallow
                case ItemID.MirageFish:
                case ItemID.UnicornFish:
                    return isHardmode ? ItemID.HallowedFishingCrateHard : ItemID.HallowedFishingCrate;

                // Underworld / Lava
                case ItemID.GuideVoodooFish:
                case ItemID.Hungerfish:
                    return isHardmode ? ItemID.LavaCrateHard : ItemID.LavaCrate;

                // Dungeon
                case ItemID.Bonefish:
                    return isHardmode ? ItemID.DungeonFishingCrateHard : ItemID.DungeonFishingCrate;

                // Forest / Underground / Cavern Default
                default:
                    return isHardmode ? ItemID.IronCrateHard : ItemID.IronCrate;
            }
        }
    }
}
