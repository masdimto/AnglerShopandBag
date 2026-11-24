using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace BagShop
{
    [ApiVersion(2, 1)]
    public class BagShopPlugin : TerrariaPlugin
    {
        public override string Name => "BagShop";
        public override string Author => "you";
        public override string Description => "Clothier jual treasure bag (.bs / .bagshop)";
        public override Version Version => new Version(1, 0, 0);

        public BagShopPlugin(Main game) : base(game) { }

        class Entry
        {
            public int Type;
            public string Name;
            public int Price; // dalam copper
            public Entry(int type, string name, int price) { Type = type; Name = name; Price = price; }
        }

        static int G(int gold) => gold * 10000;
        static int P(int plat, int gold = 0) => plat * 1000000 + gold * 10000;

        static readonly List<Entry> Shop = new List<Entry>
        {
            // Pre-Hardmode (25-50 gold)
            new Entry(ItemID.KingSlimeBossBag,       "King Slime",        G(25)),
            new Entry(ItemID.EyeOfCthulhuBossBag,    "Eye of Cthulhu",    G(30)),
            new Entry(ItemID.EaterOfWorldsBossBag,   "Eater of Worlds",   G(30)),
            new Entry(ItemID.BrainOfCthulhuBossBag,  "Brain of Cthulhu",  G(30)),
            new Entry(ItemID.QueenBeeBossBag,        "Queen Bee",         G(35)),
            new Entry(ItemID.DeerclopsBossBag,       "Deerclops",         G(40)),
            new Entry(ItemID.SkeletronBossBag,       "Skeletron",         G(50)),
            // Wall of Flesh
            new Entry(ItemID.WallOfFleshBossBag,     "Wall of Flesh",     G(80)),
            // Hardmode
            new Entry(ItemID.QueenSlimeBossBag,      "Queen Slime",       P(1, 17)),
            new Entry(ItemID.TwinsBossBag,           "The Twins",         P(1, 31)),
            new Entry(ItemID.DestroyerBossBag,       "The Destroyer",     P(1, 31)),
            new Entry(ItemID.SkeletronPrimeBossBag,  "Skeletron Prime",   P(1, 31)),
            new Entry(ItemID.PlanteraBossBag,        "Plantera",          P(1, 78)),
            new Entry(ItemID.GolemBossBag,           "Golem",             P(2, 31)),
            new Entry(ItemID.BossBagBetsy,           "Betsy",             P(4, 51)),
            new Entry(ItemID.FishronBossBag,         "Duke Fishron",      P(7, 89)),
            new Entry(ItemID.FairyQueenBossBag,      "Empress of Light",  P(10, 23)),
            new Entry(ItemID.MoonLordBossBag,        "Moon Lord",         P(50)),
        };

        readonly HashSet<int> shopMode = new HashSet<int>();

        const float MaxDistancePx = 30 * 16f; // 30 tile dari Clothier

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(BagShopCmd, "bagshop", "bs")
            {
                HelpText = ".bs = on/off mode, .bs list, .bs beli <no> [jumlah]"
            });
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                Commands.ChatCommands.RemoveAll(c => c.Names.Contains("bagshop"));
            }
            base.Dispose(disposing);
        }

        void OnLeave(LeaveEventArgs e) => shopMode.Remove(e.Who);

        // Pemain ngobrol sama NPC (packet 40) -> kalau mode on & NPC = Clothier, tampilkan daftar
        void OnGetData(GetDataEventArgs args)
        {
            if (args.Handled || args.MsgID != PacketTypes.NpcTalk) return;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {
                    r.ReadByte();
                    short npc = r.ReadInt16();
                    if (npc < 0 || npc >= Main.maxNPCs) return;
                    if (Main.npc[npc].type != NPCID.Clothier) return;
                    var p = TShock.Players[args.Msg.whoAmI];
                    if (p == null || !shopMode.Contains(p.Index)) return;
                    ShowList(p);
                }
            }
            catch { }
        }

        void BagShopCmd(CommandArgs a)
        {
            var p = a.Player;
            if (!p.RealPlayer) { p.SendErrorMessage("Hanya bisa dipakai in-game."); return; }

            if (!NPC.downedBoss3)
            {
                p.SendErrorMessage("Bag Shop belum kebuka. Kalahkan Skeletron dulu.");
                return;
            }

            if (a.Parameters.Count == 0)
            {
                if (shopMode.Remove(p.Index))
                    p.SendInfoMessage("Bag Shop OFF. Clothier balik ke toko biasa.");
                else
                {
                    shopMode.Add(p.Index);
                    p.SendSuccessMessage("Bag Shop ON. Dekati Clothier, ketik .bs beli <no> [jumlah].");
                    ShowList(p);
                }
                return;
            }

            string sub = a.Parameters[0].ToLower();
            if (sub == "list")
            {
                ShowList(p);
                return;
            }

            if (sub == "beli" || sub == "buy")
            {
                if (!shopMode.Contains(p.Index))
                {
                    p.SendErrorMessage("Nyalain dulu mode-nya dengan .bs");
                    return;
                }
                if (!NearClothier(p))
                {
                    p.SendErrorMessage("Kamu terlalu jauh dari Clothier.");
                    return;
                }
                if (a.Parameters.Count < 2 || !int.TryParse(a.Parameters[1], out int no) || no < 1 || no > Shop.Count)
                {
                    p.SendErrorMessage("Format: .bs beli <no> [jumlah]. Cek nomor lewat .bs list");
                    return;
                }
                int amount = 1;
                if (a.Parameters.Count >= 3 && (!int.TryParse(a.Parameters[2], out amount) || amount < 1 || amount > 99))
                {
                    p.SendErrorMessage("Jumlah harus 1-99.");
                    return;
                }

                var e = Shop[no - 1];
                long total = (long)e.Price * amount;
                if (total > int.MaxValue) { p.SendErrorMessage("Jumlah terlalu banyak."); return; }

                if (!p.TPlayer.CanAfford((int)total))
                {
                    p.SendErrorMessage($"Uangmu kurang. Butuh {Fmt((int)total)}.");
                    return;
                }
                if (!p.TPlayer.BuyItem((int)total))
                {
                    p.SendErrorMessage("Pembayaran gagal.");
                    return;
                }
                SyncMoneySlots(p);
                p.GiveItem(e.Type, amount);
                p.SendSuccessMessage($"Beli {amount}x Treasure Bag {e.Name} seharga {Fmt((int)total)}.");
                return;
            }

            p.SendErrorMessage("Perintah: .bs | .bs list | .bs beli <no> [jumlah]");
        }

        bool NearClothier(TSPlayer p)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                var n = Main.npc[i];
                if (n.active && n.type == NPCID.Clothier && p.TPlayer.Distance(n.Center) <= MaxDistancePx)
                    return true;
            }
            return false;
        }

        void ShowList(TSPlayer p)
        {
            p.SendInfoMessage("=== Clothier Bag Shop ===");
            for (int i = 0; i < Shop.Count; i++)
                p.SendInfoMessage($"{i + 1}. {Shop[i].Name} - {Fmt(Shop[i].Price)}");
            p.SendInfoMessage("Beli: .bs beli <no> [jumlah]");
        }

        static string Fmt(int copper)
        {
            int plat = copper / 1000000;
            int gold = copper / 10000 % 100;
            int silver = copper / 100 % 100;
            int cop = copper % 100;
            var parts = new List<string>();
            if (plat > 0) parts.Add(plat + "p");
            if (gold > 0) parts.Add(gold + "g");
            if (silver > 0) parts.Add(silver + "s");
            if (cop > 0) parts.Add(cop + "c");
            return parts.Count == 0 ? "0" : string.Join(" ", parts);
        }

        // Sinkron inventory + piggy/safe/forge/void supaya koin di client langsung berkurang
        static void SyncMoneySlots(TSPlayer p)
        {
            for (int s = 0; s <= 58; s++)
                p.SendData(PacketTypes.PlayerSlot, "", p.Index, s);
            for (int s = 99; s <= 259; s++)
                p.SendData(PacketTypes.PlayerSlot, "", p.Index, s);
        }
    }
}
