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
        public override Version Version => new Version(1, 4, 3);
        public override string Author => "CustomPlugin";
        public override string Description => "Angler Biome Crate Bonus, Princess Boss Bag Shop (/bs), dan 2 Switch Witch Doctor Potion & Flask Shop (/ps, /fs).";

        private Dictionary<int, int> lastAnglerQuests = new Dictionary<int, int>();
        private bool[] bagShopMode = new bool[Main.maxPlayers];
        private int[] witchDoctorShopMode = new int[Main.maxPlayers];
        private int[] lastTalkNpc = new int[Main.maxPlayers];
        private int[] shopSyncTicks = new int[Main.maxPlayers];

        public ShopAndAnglerBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                lastTalkNpc[i] = -1;
                shopSyncTicks[i] = -1;
            }

            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);

            Commands.ChatCommands.Add(new Command("shopangler.bagshop", ToggleBagShopCommand, "bagshop", "bshop", "bs")
            {
                HelpText = "Mengganti toko Princess antara toko normal dan toko Boss Treasure Bag."
            });

            Commands.ChatCommands.Add(new Command("shopangler.potionshop", TogglePotionShopCommand, "potionshop", "pshop", "ps")
            {
                HelpText = "Switch 1 Witch Doctor: Mengganti toko Witch Doctor antara normal dan Semua Potion Utama."
            });

            Commands.ChatCommands.Add(new Command("shopangler.flaskshop", ToggleFlaskShopCommand, "flaskshop", "fshop", "fs")
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
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
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
                lastTalkNpc[who] = -1;
                shopSyncTicks[who] = -1;

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
                lastTalkNpc[who] = -1;
                shopSyncTicks[who] = -1;
                lastAnglerQuests.Remove(who);
            }
        }

        private void OnGetData(GetDataEventArgs args)
        {
            if (args.Handled)
                return;

            if ((int)args.MsgID == 40)
            {
                int plrIdx = args.Msg.whoAmI;
                if (plrIdx >= 0 && plrIdx < Main.maxPlayers)
                {
                    shopSyncTicks[plrIdx] = 0;
                }
            }
        }

        private void ToggleBagShopCommand(CommandArgs args)
        {
            int who = args.Player.Index;
            if (who < 0 || who >= Main.maxPlayers)
                return;

            bagShopMode[who] = !bagShopMode[who];
            lastTalkNpc[who] = -1;
            shopSyncTicks[who] = 0;

            if (bagShopMode[who])
                args.Player.SendSuccessMessage("[Boss Bag Shop] AKTIF! Buka toko Princess untuk membeli Treasure Bag boss yang sudah dikalahkan.");
            else
                args.Player.SendInfoMessage("[Boss Bag Shop] NONAKTIF. Toko Princess kembali menjual item normal.");
        }

        private void TogglePotionShopCommand(CommandArgs args)
        {
            int who = args.Player.Index;
            if (who < 0 || who >= Main.maxPlayers)
                return;

            lastTalkNpc[who] = -1;
            shopSyncTicks[who] = 0;

            if (witchDoctorShopMode[who] == 1)
            {
                witchDoctorShopMode[who] = 0;
                args.Player.SendInfoMessage("[Witch Doctor Shop] NONAKTIF. Toko Witch Doctor kembali menjual item normal.");
            }
            else
            {
                witchDoctorShopMode[who] = 1;
                string statusHardmode = Main.hardMode ? "Termasuk Potion Hardmode!" : "Mode Pre-Hardmode.";
                args.Player.SendSuccessMessage($"[Witch Doctor Switch 1: Potion Utama] AKTIF! ({statusHardmode})");
            }
        }

        private void ToggleFlaskShopCommand(CommandArgs args)
        {
            int who = args.Player.Index;
            if (who < 0 || who >= Main.maxPlayers)
                return;

            lastTalkNpc[who] = -1;
            shopSyncTicks[who] = 0;

            if (witchDoctorShopMode[who] == 2)
            {
                witchDoctorShopMode[who] = 0;
                args.Player.SendInfoMessage("[Witch Doctor Shop] NONAKTIF. Toko Witch Doctor kembali menjual item normal.");
            }
            else
            {
                witchDoctorShopMode[who] = 2;
                string statusHardmode = Main.hardMode ? "Termasuk Flask & Potion Hardmode!" : "Mode Pre-Hardmode.";
                args.Player.SendSuccessMessage($"[Witch Doctor Switch 2: Flask & Potion Ekstra] AKTIF! ({statusHardmode})");
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                TSPlayer tsPlayer = TShock.Players[i];
                if (tsPlayer == null || !tsPlayer.Active || tsPlayer.TPlayer == null)
                {
                    lastTalkNpc[i] = -1;
                    shopSyncTicks[i] = -1;
                    continue;
                }

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

                int currentTalkNpc = player.talkNPC;
                if (currentTalkNpc != lastTalkNpc[i])
                {
                    lastTalkNpc[i] = currentTalkNpc;
                    shopSyncTicks[i] = currentTalkNpc >= 0 ? 0 : -1;
                }

                if (currentTalkNpc >= 0 && currentTalkNpc < Main.maxNPCs && shopSyncTicks[i] >= 0)
                {
                    int tick = shopSyncTicks[i];
                    if (tick == 2 || tick == 10 || tick == 25)
                    {
                        NPC talkNpc = Main.npc[currentTalkNpc];
                        if (talkNpc.active)
                        {
                            if (talkNpc.type == NPCID.Princess && bagShopMode[i])
                                ApplyShopList(tsPlayer, GetUnlockedBossBags());
                            else if (talkNpc.type == NPCID.WitchDoctor)
                            {
                                if (witchDoctorShopMode[i] == 1)
                                    ApplyShopList(tsPlayer, GetPrimaryPotions());
                                else if (witchDoctorShopMode[i] == 2)
                                    ApplyShopList(tsPlayer, GetFlasksAndExtraPotions());
                            }
                        }
                    }

                    if (tick <= 30)
                        shopSyncTicks[i]++;
                    else
                        shopSyncTicks[i] = -1;
                }
            }
        }

        private void ApplyShopList(TSPlayer tsPlayer, List<(int itemId, int price)> items)
        {
            int maxSlots = Math.Min(items.Count, 40);
            for (int slot = 0; slot < maxSlots; slot++)
                SendCustomShopSlot(tsPlayer, slot, items[slot].itemId, items[slot].price);
            for (int slot = maxSlots; slot < 40; slot++)
                SendCustomShopSlot(tsPlayer, slot, 0, 0);
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
            AddItem(ItemID.TrapsightPotion);
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

            if (Main.hardMode)
            {
                AddItem(ItemID.GreaterHealingPotion, 2);
                AddItem(ItemID.GreaterManaPotion, 2);
                AddItem(ItemID.LifeforcePotion, 5);
                AddItem(ItemID.InfernoPotion, 5);
            }

            return list;
        }

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
            AddItem(ItemID.LuckPotionLesser, 1);
            AddItem(ItemID.InvisibilityPotion, 1);
            AddItem(ItemID.BiomeSightPotion, 2);
            AddItem(ItemID.LovePotion, 1);
            AddItem(ItemID.StinkPotion, 1);
            AddItem(ItemID.GenderChangePotion, 5);

            if (Main.hardMode)
            {
                AddItem(ItemID.FlaskofIchor, 5);
                AddItem(ItemID.FlaskofCursedFlames, 5);
                AddItem(ItemID.FlaskofVenom, 6);
                AddItem(ItemID.FlaskofNanites, 6);
                AddItem(ItemID.FlaskofGold, 5);
                AddItem(ItemID.TeleportationPotion, 3);
                AddItem(ItemID.LuckPotionGreater, 4);
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

            AddBag(NPC.downedSlimeKing, ItemID.KingSlimeBossBag, 4);
            AddBag(NPC.downedBoss1, ItemID.EyeOfCthulhuBossBag, 6);
            AddBag(NPC.downedBoss2, ItemID.EaterOfWorldsBossBag, 8);
            AddBag(NPC.downedBoss2, ItemID.BrainOfCthulhuBossBag, 8);
            AddBag(NPC.downedQueenBee, ItemID.QueenBeeBossBag, 10);
            AddBag(NPC.downedBoss3, ItemID.SkeletronBossBag, 12);
            AddBag(NPC.downedDeerclops, ItemID.DeerclopsBossBag, 12);
            AddBag(Main.hardMode, ItemID.WallOfFleshBossBag, 16);
            AddBag(NPC.downedQueenSlime, ItemID.QueenSlimeBossBag, 20);
            AddBag(NPC.downedMechBoss1, ItemID.DestroyerBossBag, 24);
            AddBag(NPC.downedMechBoss2, ItemID.TwinsBossBag, 24);
            AddBag(NPC.downedMechBoss3, ItemID.SkeletronPrimeBossBag, 24);
            AddBag(NPC.downedPlantBoss, ItemID.PlanteraBossBag, 32);
            AddBag(NPC.downedGolemBoss, ItemID.GolemBossBag, 36);
            AddBag(NPC.downedFishron, ItemID.FishronBossBag, 40);
            AddBag(NPC.downedEmpressOfLight, ItemID.FairyQueenBossBag, 40);
            AddBag(NPC.downedAncientCultist, ItemID.CultistBossBag, 48);
            AddBag(NPC.downedMoonlord, ItemID.MoonLordBossBag, 60);

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
