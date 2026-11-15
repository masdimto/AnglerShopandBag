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
        public override System.Version Version => new System.Version(1, 2, 1);
        public override string Author => "CustomPlugin";
        public override string Description => "Wizard Soul Shop, Angler Biome Crate Bonus, dan Princess Boss Treasure Bag Shop (/bagshop).";

        private int[] lastAnglerQuests = new int[Main.maxPlayers];
        private int[] currentShopNpc = new int[Main.maxPlayers];
        private bool[] bagShopMode = new bool[Main.maxPlayers];

        public ShopAndAnglerBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);

            Commands.ChatCommands.Add(new Command("shopangler.bagshop", ToggleBagShopCommand, "bagshop", "bshop")
            {
                HelpText = "Mengganti toko Princess antara toko normal dan toko Boss Treasure Bag."
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            }
            base.Dispose(disposing);
        }

        private void ToggleBagShopCommand(CommandArgs args)
        {
            int idx = args.Player.Index;
            bagShopMode[idx] = !bagShopMode[idx];

            if (bagShopMode[idx])
                args.Player.SendSuccessMessage("[Boss Bag Shop] AKTIF! Buka toko Princess untuk membeli Treasure Bag boss yang sudah dikalahkan.");
            else
                args.Player.SendInfoMessage("[Boss Bag Shop] NONAKTIF. Toko Princess kembali menjual item normal.");
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            if (args.Who >= 0 && args.Who < Main.maxPlayers && TShock.Players[args.Who]?.TPlayer != null)
            {
                lastAnglerQuests[args.Who] = TShock.Players[args.Who].TPlayer.anglerQuestsFinished;
                currentShopNpc[args.Who] = -1;
                bagShopMode[args.Who] = false;
            }
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            if (args.Who >= 0 && args.Who < Main.maxPlayers)
            {
                lastAnglerQuests[args.Who] = 0;
                currentShopNpc[args.Who] = -1;
                bagShopMode[args.Who] = false;
            }
        }

        private void OnGetData(GetDataEventArgs args)
        {
            if (args.Handled || args.Msg.whoAmI < 0 || args.Msg.whoAmI >= Main.maxPlayers)
                return;

            TSPlayer tsPlayer = TShock.Players[args.Msg.whoAmI];
            if (tsPlayer == null || !tsPlayer.Active)
                return;

            // Packet 40: NpcTalk
            if (args.MsgID == PacketTypes.NpcTalk)
            {
                using (MemoryStream ms = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    byte playerIndex = reader.ReadByte();
                    short npcIndex = reader.ReadInt16();

                    if (npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].active)
                    {
                        currentShopNpc[tsPlayer.Index] = Main.npc[npcIndex].type;

                        // Jika NPC adalah Wizard atau Princess (mode bagshop), kirim isi toko kustom sesaat setelah dibuka
                        if (Main.npc[npcIndex].type == NPCID.Wizard)
                        {
                            SendCustomShopSlot(tsPlayer, 38, ItemID.SoulofLight, Item.buyPrice(0, 0, 20, 0));
                            SendCustomShopSlot(tsPlayer, 39, ItemID.SoulofNight, Item.buyPrice(0, 0, 20, 0));
                        }
                        else if (Main.npc[npcIndex].type == NPCID.Princess && bagShopMode[tsPlayer.Index])
                        {
                            ApplyPrincessBagShop(tsPlayer);
                        }
                    }
                    else
                    {
                        currentShopNpc[tsPlayer.Index] = -1;
                    }
                }
            }
            // Packet 104: ShopItem (saat client membeli atau menyinkronkan slot toko)
            else if ((int)args.MsgID == 104)
            {
                int npcType = currentShopNpc[tsPlayer.Index];

                if (npcType == NPCID.Wizard)
                {
                    SendCustomShopSlot(tsPlayer, 38, ItemID.SoulofLight, Item.buyPrice(0, 0, 20, 0));
                    SendCustomShopSlot(tsPlayer, 39, ItemID.SoulofNight, Item.buyPrice(0, 0, 20, 0));
                }
                else if (npcType == NPCID.Princess && bagShopMode[tsPlayer.Index])
                {
                    ApplyPrincessBagShop(tsPlayer);
                }
            }
        }

        private void ApplyPrincessBagShop(TSPlayer tsPlayer)
        {
            List<(int itemId, int price)> unlockedBags = GetUnlockedBossBags();

            for (int slot = 0; slot < 40; slot++)
            {
                if (slot < unlockedBags.Count)
                {
                    SendCustomShopSlot(tsPlayer, slot, unlockedBags[slot].itemId, unlockedBags[slot].price);
                }
                else
                {
                    SendCustomShopSlot(tsPlayer, slot, 0, 0);
                }
            }
        }

        private List<(int itemId, int price)> GetUnlockedBossBags()
        {
            var bags = new List<(int, int)>();

            // Pre-Hardmode Bosses (12 Gold 50 Silver)
            if (NPC.downedSlimeKing) bags.Add((ItemID.KingSlimeBossBag, Item.buyPrice(0, 12, 50, 0)));
            if (NPC.downedBoss1) bags.Add((ItemID.EyeOfCthulhuBossBag, Item.buyPrice(0, 12, 50, 0)));
            if (NPC.downedBoss2)
            {
                bags.Add((ItemID.EaterOfWorldsBossBag, Item.buyPrice(0, 12, 50, 0)));
                bags.Add((ItemID.BrainOfCthulhuBossBag, Item.buyPrice(0, 12, 50, 0)));
            }
            if (NPC.downedQueenBee) bags.Add((ItemID.QueenBeeBossBag, Item.buyPrice(0, 12, 50, 0)));
            if (NPC.downedBoss3) bags.Add((ItemID.SkeletronBossBag, Item.buyPrice(0, 12, 50, 0)));
            if (NPC.downedDeerclops) bags.Add((ItemID.DeerclopsBossBag, Item.buyPrice(0, 12, 50, 0)));
            if (Main.hardMode) bags.Add((ItemID.WallOfFleshBossBag, Item.buyPrice(0, 17, 50, 0)));

            // Hardmode Bosses (25 Gold - 37 Gold 50 Silver)
            if (NPC.downedQueenSlime) bags.Add((ItemID.QueenSlimeBossBag, Item.buyPrice(0, 25, 0, 0)));
            if (NPC.downedMechBoss1) bags.Add((ItemID.DestroyerBossBag, Item.buyPrice(0, 25, 0, 0)));
            if (NPC.downedMechBoss2) bags.Add((ItemID.TwinsBossBag, Item.buyPrice(0, 25, 0, 0)));
            if (NPC.downedMechBoss3) bags.Add((ItemID.SkeletronPrimeBossBag, Item.buyPrice(0, 25, 0, 0)));
            if (NPC.downedPlantBoss) bags.Add((ItemID.PlanteraBossBag, Item.buyPrice(0, 32, 50, 0)));
            if (NPC.downedGolemBoss) bags.Add((ItemID.GolemBossBag, Item.buyPrice(0, 32, 50, 0)));
            if (NPC.downedFishron) bags.Add((ItemID.FishronBossBag, Item.buyPrice(0, 37, 50, 0)));
            if (NPC.downedEmpressOfLight) bags.Add((ItemID.FairyQueenBossBag, Item.buyPrice(0, 37, 50, 0)));
            if (NPC.downedAncientCultist) bags.Add((ItemID.CultistBossBag, Item.buyPrice(0, 37, 50, 0)));

            // Endgame (50 Gold)
            if (NPC.downedMoonlord) bags.Add((ItemID.MoonLordBossBag, Item.buyPrice(0, 50, 0, 0)));

            return bags;
        }

        private void SendCustomShopSlot(TSPlayer player, int slot, int itemType, int price)
        {
            if (Main.instance != null && Main.instance.shop != null && Main.instance.shop.Length > 0)
            {
                int shopIdx = player.TPlayer.talkNPC >= 0 ? 1 : 0;
                if (shopIdx < Main.instance.shop.Length && Main.instance.shop[shopIdx] != null && slot < Main.instance.shop[shopIdx].item.Length)
                {
                    if (itemType > 0)
                    {
                        Main.instance.shop[shopIdx].item[slot].SetDefaults(itemType);
                        Main.instance.shop[shopIdx].item[slot].shopCustomPrice = price;
                    }
                    else
                    {
                        Main.instance.shop[shopIdx].item[slot].TurnToAir();
                    }
                }
            }

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)slot);
                writer.Write((short)itemType);
                writer.Write((short)(itemType > 0 ? 1 : 0)); // Stack
                writer.Write((byte)0);  // Prefix
                writer.Write((int)price);
                writer.Write((byte)0);  // Flags

                byte[] payload = ms.ToArray();
                using (MemoryStream packetMs = new MemoryStream())
                using (BinaryWriter packetWriter = new BinaryWriter(packetMs))
                {
                    packetWriter.Write((ushort)(payload.Length + 3));
                    packetWriter.Write((byte)104); // Packet 104: ShopItem
                    packetWriter.Write(payload);

                    player.SendRawData(packetMs.ToArray());
                }
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

                if (player.anglerQuestsFinished > lastAnglerQuests[i])
                {
                    lastAnglerQuests[i] = player.anglerQuestsFinished;

                    int questFishId = (Main.anglerQuest >= 0 && Main.anglerQuest < Main.anglerQuestItemNetIDs.Length)
                        ? Main.anglerQuestItemNetIDs[Main.anglerQuest]
                        : -1;

                    int crateId = GetBiomeCrateForQuestFish(questFishId);
                    if (crateId > 0)
                    {
                        tsPlayer.GiveItem(crateId, 1);
                        string crateName = Lang.GetItemNameValue(crateId);
                        tsPlayer.SendSuccessMessage($"[Angler Bonus] Kamu mendapatkan 1x {crateName} sesuai biome Quest Fish!");
                    }
                }
                else if (player.anglerQuestsFinished < lastAnglerQuests[i])
                {
                    lastAnglerQuests[i] = player.anglerQuestsFinished;
                }
            }
        }

        private int GetBiomeCrateForQuestFish(int questFishId)
        {
            bool hm = Main.hardMode;

            switch (questFishId)
            {
                // 1. Honey Quest Fish -> Bramble Crate (4877)
                case ItemID.BumblebeeTuna:
                    return 4877; // Bramble Crate

                // 2. Jungle Quest Fish -> Bramble Crate (4877) / Jungle Crate (3208)
                case ItemID.Catfish:
                case ItemID.Derpfish:
                case ItemID.Mudfish:
                case ItemID.TropicalBarracuda:
                    return hm ? 4877 : 3208;

                // 3. Corruption Quest Fish -> Defiled Crate (3982) / Corrupt Crate (3203)
                case ItemID.Cursedfish:
                case ItemID.EaterofPlankton:
                case ItemID.InfectedScabbardfish:
                    return hm ? 3982 : 3203;

                // 4. Crimson Quest Fish -> Hematic Crate (3983) / Crimson Crate (3204)
                case ItemID.BloodyManowar:
                case ItemID.Ichorfish:
                    return hm ? 3983 : 3204;

                // 5. Hallow Quest Fish -> Divine Crate (3986) / Hallowed Crate (3207)
                case ItemID.MirageFish:
                case ItemID.Pixiefish:
                case ItemID.UnicornFish:
                    return hm ? 3986 : 3207;

                // 6. Snow / Tundra / Ice Quest Fish -> Boreal Crate (4406) / Frozen Crate (4405)
                case ItemID.Fishron:
                case ItemID.MutantFlinxfin:
                case ItemID.Pengfish:
                case ItemID.TundraTrout:
                    return hm ? 4406 : 4405;

                // 7. Desert / Oasis Quest Fish -> Mirage Crate (4408) / Oasis Crate (4407)
                case ItemID.ScarabFish:
                case ItemID.ScorpioFish:
                    return hm ? 4408 : 4407;

                // 8. Sky / Floating Island Quest Fish -> Azure Crate (3985) / Sky Crate (3206)
                case ItemID.Angelfish:
                case ItemID.Cloudfish:
                case ItemID.FallenStarfish:
                case ItemID.Harpyfish:
                case ItemID.Wyverntail:
                    return hm ? 3985 : 3206;

                // 9. Dungeon Quest Fish -> Stockade Crate (3984) / Dungeon Crate (3205)
                case ItemID.Bonefish:
                    return hm ? 3984 : 3205;

                // 10. Ocean / Beach Quest Fish -> Seaside Crate (5003) / Ocean Crate (5002)
                case ItemID.CapnTunabeard:
                case ItemID.Clownfish:
                    return hm ? 5003 : 5002;

                // 11. Forest, Underground, & Cavern -> Titanium Crate (3981) / Golden Crate (2336)
                default:
                    return hm ? 3981 : 2336;
            }
        }
    }
}
