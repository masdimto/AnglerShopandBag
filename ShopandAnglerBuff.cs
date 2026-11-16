using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
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
        public override Version Version => new Version(1, 3, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "Angler Biome Crate Bonus, Princess Boss Bag Shop (/bagshop), dan 2 Switch Witch Doctor Potion & Flask Shop (/potionshop, /flaskshop).";

        private Dictionary<int, int> lastAnglerQuests = new Dictionary<int, int>();

        // Status mode toko Princess per pemain
        private bool[] bagShopMode = new bool[Main.maxPlayers];

        // Mode toko Witch Doctor per pemain:
        // 0 = Normal, 1 = Potion Utama (/potionshop), 2 = Flask & Potion Ekstra (/flaskshop)
        private int[] witchDoctorShopMode = new int[Main.maxPlayers];

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

            Commands.ChatCommands.Add(new Command("shopangler.potionshop", TogglePotionShopCommand, "potionshop", "pshop")
            {
                HelpText = "Switch 1 Witch Doctor: Mengganti toko Witch Doctor antara normal dan Semua Potion Utama."
            });

            Commands.ChatCommands.Add(new Command("shopangler.flaskshop", ToggleFlaskShopCommand, "flaskshop", "fshop")
            {
                HelpText = "Switch 2 Witch Doctor: Mengganti toko Witch Doctor antara normal dan Semua Flask + Potion Ekstra."
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
                witchDoctorShopMode[who] = 0;

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
                witchDoctorShopMode[who] = 0;
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

        private void TogglePotionShopCommand(CommandArgs args)
        {
            int who = args.Player.Index;
            if (who < 0 || who >= Main.maxPlayers)
                return;

            if (witchDoctorShopMode[who] == 1)
            {
                witchDoctorShopMode[who] = 0;
                args.Player.SendInfoMessage("[Witch Doctor Shop] NONAKTIF. Toko Witch Doctor kembali menjual item normal.");
            }
            else
            {
                witchDoctorShopMode[who] = 1;
                string statusHardmode = Main.hardMode
                    ? "Termasuk Potion Hardmode!"
                    : "Mode Pre-Hardmode (Potion Hardmode terkunci sampai memasuki Hardmode).";
                args.Player.SendSuccessMessage($"[Witch Doctor Switch 1: Potion Utama] AKTIF! Buka toko Witch Doctor untuk membeli potion. ({statusHardmode})");
            }
        }

        private void ToggleFlaskShopCommand(CommandArgs args)
        {
            int who = args.Player.Index;
            if (who < 0 || who >= Main.maxPlayers)
                return;

            if (witchDoctorShopMode[who] == 2)
            {
                witchDoctorShopMode[who] = 0;
                args.Player.SendInfoMessage("[Witch Doctor Shop] NONAKTIF. Toko Witch Doctor kembali menjual item normal.");
            }
            else
            {
                witchDoctorShopMode[who] = 2;
                string statusHardmode = Main.hardMode
                    ? "Termasuk Flask & Potion Hardmode!"
                    : "Mode Pre-Hardmode (Flask Ichor/Cursed/Venom/Nanites & Super Potion terkunci sampai Hardmode).";
                args.Player.SendSuccessMessage($"[Witch Doctor Switch 2: Flask & Potion Ekstra] AKTIF! Buka toko Witch Doctor untuk membeli Flask & Potion tambahan. ({statusHardmode})");
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

                // 1. Cek Angler Quest selesai -> Beri Biome Crate
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

                // 2. Sinkronisasi Toko Kustom saat pemain berbicara dengan NPC
                if (Main.GameUpdateCount % 10 == 0 && player.talkNPC >= 0 && player.talkNPC < Main.maxNPCs)
                {
                    NPC talkNpc = Main.npc[player.talkNPC];
                    if (talkNpc.active)
                    {
                        if (talkNpc.type == NPCID.Princess && bagShopMode[i])
                        {
                            ApplyShopList(tsPlayer, GetUnlockedBossBags());
                        }
                        else if (talkNpc.type == NPCID.WitchDoctor)
                        {
                            if (witchDoctorShopMode[i] == 1)
                            {
                                ApplyShopList(tsPlayer, GetPrimaryPotions());
                            }
                            else if (witchDoctorShopMode[i] == 2)
                            {
                                ApplyShopList(tsPlayer, GetFlasksAndExtraPotions());
                            }
                        }
                    }
                }
            }
        }

        private void ApplyShopList(TSPlayer tsPlayer, List<(int itemId, int price)> items)
        {
            int maxSlots = Math.Min(items.Count, 40);

            for (int slot = 0; slot < maxSlots; slot++)
            {
                SendCustomShopSlot(tsPlayer, slot, items[slot].itemId, items[slot].price);
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
                writer.Write((short)0); // Placeholder panjang paket
                writer.Write((byte)PacketTypes.ShopOverride); // Packet ID 104
                writer.Write((byte)slot);
                writer.Write((short)itemType);
                writer.Write((short)(itemType > 0 ? 1 : 0)); // Stack = 1
                writer.Write((byte)0); // Prefix = 0
                writer.Write(price); // Harga dalam copper coins
                writer.Write((byte)0); // Flags

                byte[] data = ms.ToArray();
                short length = (short)data.Length;
                BitConverter.GetBytes(length).CopyTo(data, 0);

                tsPlayer.SendRawData(data);
            }
        }

        // SWITCH 1 (/potionshop): 40 Potion Utama (Pre-Hardmode + Hardmode)
        private List<(int itemId, int price)> GetPrimaryPotions()
        {
            var list = new List<(int itemId, int price)>();

            void AddItem(int itemId, int fallbackGold = 1)
            {
                Item temp = new Item();
                temp.SetDefaults(itemId);
                int price = temp.value > 0 ? temp.value : Item.buyPrice(0, fallbackGold, 0, 0);
                list.Add((itemId, price));
            }

            // Pre-Hardmode (36 Potion)
            AddItem(ItemID.HealingPotion);
            AddItem(ItemID.ManaPotion);
            AddItem(ItemID.RestorationPotion);
            AddItem(ItemID.IronskinPotion);
            AddItem(ItemID.RegenerationPotion);
            AddItem(ItemID.SwiftnessPotion);
            AddItem(ItemID.EndurancePotion);
            AddItem(ItemID.WrathPotion);
            AddItem(ItemID.RagePotion);
            AddItem(ItemID.SummoningPotion);
            AddItem(ItemID.MagicPowerPotion);
            AddItem(ItemID.ManaRegenerationPotion);
            AddItem(ItemID.ArcheryPotion);
            AddItem(ItemID.AmmoReservationPotion);
            AddItem(ItemID.HeartreachPotion);
            AddItem(ItemID.ThornsPotion);
            AddItem(ItemID.TitanPotion);
            AddItem(ItemID.BattlePotion);
            AddItem(ItemID.CalmingPotion);
            AddItem(ItemID.BuilderPotion);
            AddItem(ItemID.MiningPotion);
            AddItem(ItemID.SpelunkerPotion);
            AddItem(ItemID.HunterPotion);
            AddItem(ItemID.DangersensePotion);
            AddItem(ItemID.NightOwlPotion);
            AddItem(ItemID.ShinePotion);
            AddItem(ItemID.ObsidianSkinPotion);
            AddItem(ItemID.GillsPotion);
            AddItem(ItemID.FlipperPotion);
            AddItem(ItemID.WaterWalkingPotion);
            AddItem(ItemID.FeatherfallPotion);
            AddItem(ItemID.GravitationPotion);
            AddItem(ItemID.WarmthPotion);
            AddItem(ItemID.LuckPotion);
            AddItem(ItemID.RecallPotion);
            AddItem(ItemID.WormholePotion);

            // Hardmode (4 Potion)
            if (Main.hardMode)
            {
                AddItem(ItemID.GreaterHealingPotion, 2);
                AddItem(ItemID.GreaterManaPotion, 2);
                AddItem(ItemID.LifeforcePotion, 5);
                AddItem(ItemID.InfernoPotion, 5);
            }

            return list;
        }

        // SWITCH 2 (/flaskshop): Semua Flask + Potion Mancing, Teleport, & Super Potion
        private List<(int itemId, int price)> GetFlasksAndExtraPotions()
        {
            var list = new List<(int itemId, int price)>();

            void AddItem(int itemId, int fallbackGold = 1)
            {
                Item temp = new Item();
                temp.SetDefaults(itemId);
                int price = temp.value > 0 ? temp.value : Item.buyPrice(0, fallbackGold, 0, 0);
                list.Add((itemId, price));
            }

            // === PRE-HARDMODE FLASKS & EXTRA POTIONS ===
            AddItem(ItemID.FlaskofPoison, 2);
            AddItem(ItemID.FlaskofFire, 2);
            AddItem(ItemID.FlaskofParty, 2);
            AddItem(ItemID.FishingPotion, 1);
            AddItem(ItemID.SonarPotion, 1);
            AddItem(ItemID.CratePotion, 1);
            AddItem(ItemID.PotionOfReturn, 2);
            AddItem(ItemID.LesserHealingPotion, 1);
            AddItem(ItemID.LesserManaPotion, 1);
            AddItem(ItemID.LesserRestorationPotion, 1);
            AddItem(ItemID.LesserLuckPotion, 1);
            AddItem(ItemID.InvisibilityPotion, 1);
            AddItem(ItemID.TrapsightPotion, 1); // Dangersense alias / cadangan jika butuh, atau BiomeSight
            AddItem(ItemID.BiomeSightPotion, 2);
            AddItem(ItemID.LovePotion, 1);
            AddItem(ItemID.StinkPotion, 1);
            AddItem(ItemID.GenderChangePotion, 5);

            // === HARDMODE FLASKS & SUPER POTIONS (Hanya Terbuka Jika Sudah Hardmode) ===
            if (Main.hardMode)
            {
                AddItem(ItemID.FlaskofIchor, 5);
                AddItem(ItemID.FlaskofCursedFlames, 5);
                AddItem(ItemID.FlaskofVenom, 6);
                AddItem(ItemID.FlaskofNanites, 6);
                AddItem(ItemID.FlaskofGold, 5);
                AddItem(ItemID.TeleportationPotion, 3);
                AddItem(ItemID.GreaterLuckPotion, 4);
                AddItem(ItemID.SuperHealingPotion, 8);
                AddItem(ItemID.SuperManaPotion, 8);
            }

            return list;
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
                    return Main.hardMode ? ItemID.JungleCrateHard : ItemID.JungleCrate;

                case ItemID.ColdSnap:
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
                case ItemID.Fishotaur:
                case ItemID.GuideVoodooFish:
                case ItemID.Hungerfish:
                    return Main.hardMode ? ItemID.LavaCrateHard : ItemID.LavaCrate;

                default:
                    return Main.hardMode ? ItemID.IronCrateHard : ItemID.IronCrate;
            }
        }
    }
}
