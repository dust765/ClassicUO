using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    internal class AutoLootManager
    {
        public static AutoLootManager Instance { get; private set; } = new AutoLootManager();
        public bool IsLoaded { get { return loaded; } }
        public List<AutoLootItem> AutoLootList { get => autoLootItems; set => autoLootItems = value; }

        private static ConcurrentQueue<uint> lootItems = new ConcurrentQueue<uint>();

        private List<AutoLootItem> autoLootItems = new List<AutoLootItem>();
        private bool loaded = false;
        private string savePath = Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Profiles", "AutoLoot.json");
        private bool lootTaskRunning = false;
        private int _lootTaskRunningFlag = 0;
        private ProgressBarGump progressBarGump;
        private int currentLootTotalCount = 0;

        private AutoLootManager() { Load(); }

        /// <summary>
        /// This method will, in another thread, start looting the items on the loot list.
        /// This is called after adding items via CheckAndLoot method.
        /// </summary>
        public void StartLooting()
        {
            if (loaded && System.Threading.Interlocked.CompareExchange(ref _lootTaskRunningFlag, 1, 0) == 0)
            {
                int delay = ProfileManager.CurrentProfile == null ? 1000 : ProfileManager.CurrentProfile.MoveMultiObjectDelay;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        lootTaskRunning = true;
                        await Task.Delay(delay).ConfigureAwait(false);
                        if (lootItems != null && !lootItems.IsEmpty)
                        {
                            int totalToLoot = Math.Max(1, currentLootTotalCount);
                            if (ProfileManager.CurrentProfile.EnableAutoLootProgressBar && (progressBarGump == null || progressBarGump.IsDisposed))
                            {
                                progressBarGump = new ProgressBarGump("Auto looting...", 0) {
                                    Y = (ProfileManager.CurrentProfile.GameWindowPosition.Y + ProfileManager.CurrentProfile.GameWindowSize.Y) - 150
                                };
                                progressBarGump.CenterXInViewPort();
                                UIManager.Add(progressBarGump);
                            }

                            while (lootItems.TryDequeue(out uint moveItem))
                            {
                                if (progressBarGump != null && !progressBarGump.IsDisposed)
                                {
                                    int remaining = lootItems.Count;
                                    int done = totalToLoot - remaining;
                                    double pct = 1.0 - ((double)remaining / (double)totalToLoot);
                                    MainThreadQueue.EnqueueAction(() =>
                                    {
                                        if (progressBarGump != null && !progressBarGump.IsDisposed)
                                        {
                                            progressBarGump.CurrentPercentage = Math.Max(0, Math.Min(1, pct));
                                            progressBarGump.SetTitle($"Auto looting... ({done}/{totalToLoot})");
                                        }
                                    });
                                }

                                Item m = World.Items.Get(moveItem);
                                if (m != null)
                                {
                                    GameActions.GrabItem(m, m.Amount);
                                    await Task.Delay(delay).ConfigureAwait(false);
                                }
                            }
                        }
                        lootTaskRunning = false;
                        MainThreadQueue.EnqueueAction(() => { progressBarGump?.Dispose(); progressBarGump = null; });
                        currentLootTotalCount = 0;
                    }
                    catch
                    {
                        lootTaskRunning = false;
                        MainThreadQueue.EnqueueAction(() => { progressBarGump?.Dispose(); progressBarGump = null; });
                        currentLootTotalCount = 0;
                        System.Threading.Interlocked.Exchange(ref _lootTaskRunningFlag, 0);
                    }
                });
            }
        }

        /// <summary>
        /// Check an item against the loot list, if it needs to be auto looted it will be.
        /// I reccomend running this method in a seperate thread if it's a lot of items.
        /// </summary>
        public void CheckAndLoot(Item i)
        {
            if (!loaded) return;

            if (IsOnLootList(i))
            {
                currentLootTotalCount++;
                GameActions.Print($"SAL Looting: {i.Name} {i.Graphic} x {i.Amount}");
                lootItems.Enqueue(i);
            }
        }

        /// <summary>
        /// Check if an item is on the auto loot list.
        /// </summary>
        /// <param name="i">The item to check the loot list against</param>
        /// <returns></returns>
        public bool IsOnLootList(Item i)
        {
            if (!loaded) return false;

            foreach (var entry in autoLootItems)
            {
                if (entry.Match(i))
                {
                    return true;
                }
            }
            return false;
        }

        public AutoLootItem GetLootItem(string ID)
        {
            foreach (var item in autoLootItems)
            {
                if (item.UID == ID)
                {
                    return item;
                }
            }

            return null;
        }

        public AutoLootItem AddLootItem(ushort graphic = 0, ushort hue = ushort.MaxValue, string name = "")
        {
            foreach(AutoLootItem entry in autoLootItems)
            {
                if(entry.Graphic == graphic && entry.Hue == hue)
                {
                    return entry;
                }
            }

            AutoLootItem item = new AutoLootItem() { Graphic = graphic, Hue = hue, Name = name };

            autoLootItems.Add(item);

            return item;
        }

        public void HandleCorpse(Item corpse)
        {
            if (corpse != null && ProfileManager.CurrentProfile.EnableAutoLoot && corpse.IsCorpse)
            {
                for (LinkedObject i = corpse.Items; i != null; i = i.Next)
                {
                    CheckAndLoot((Item)i);
                }
                if (ProfileManager.CurrentProfile.HueCorpseAfterAutoloot)
                    corpse.Hue = 73;
                StartLooting();
            }
        }

        public void TryRemoveLootItem(string UID)
        {
            int removeAt = -1;

            for (int i = 0; i < autoLootItems.Count; i++)
            {
                if (autoLootItems[i].UID == UID)
                {
                    removeAt = i;
                }
            }

            if (removeAt > -1)
            {
                autoLootItems.RemoveAt(removeAt);
            }
        }

        public void OnSceneLoad()
        {

        }

        private void Load()
        {
            _ = Task.Run(() =>
            {
                if (!File.Exists(savePath))
                {
                    autoLootItems = new List<AutoLootItem>();
                    loaded = true;
                }
                else
                {
                    try
                    {
                        string data = File.ReadAllText(savePath);
                        var parsed = JsonSerializer.Deserialize(
                            data,
                            typeof(List<AutoLootItem>),
                            GameManagersJsonContext.Default) as List<AutoLootItem>;
                        autoLootItems = parsed ?? new List<AutoLootItem>();
                        loaded = true;
                    }
                    catch
                    {
                        GameActions.Print("There was an error loading your auto loot config file, please check it with a json validator.", 32);
                        loaded = false;
                    }

                }
            });
        }

        public void Save()
        {
            if (loaded)
            {
                try
                {
                    string fileData = JsonSerializer.Serialize(
                        autoLootItems,
                        typeof(List<AutoLootItem>),
                        GameManagersJsonContext.Default);

                    File.WriteAllText(savePath, fileData);
                }
                catch (Exception e)
                {
                    Log.Error($"AutoLootManager.Save: {e}");
                }
            }
        }

        public class AutoLootItem
        {
            public string Name { get; set; } = "";
            public ushort Graphic { get; set; } = 0;
            public ushort Hue { get; set; } = ushort.MaxValue;
            /// <summary>
            /// Do not set this manually.
            /// </summary>
            public string UID { get; set; } = Guid.NewGuid().ToString();

            public bool Match(Item compareTo)
            {
                if (Graphic == compareTo.Graphic) //Graphic matches
                {
                    return HueCheck(compareTo.Hue);
                }
                return false;
            }

            private bool HueCheck(ushort value)
            {
                if (Hue == ushort.MaxValue) //Ignore hue, only check graphic.
                {
                    return true;
                }
                else if (Hue == value) //Hue must match, and it does
                {
                    return true;
                }
                else //Hue is not ignored, and does not match
                {
                    return false;
                }
            }
        }
    }
}
