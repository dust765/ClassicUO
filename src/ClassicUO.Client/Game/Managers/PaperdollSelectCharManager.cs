using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Scenes;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClassicUO.Game.Managers
{
    public class PaperdollItem
    {
        public uint Serial { get; set; }
        public Layer Layer { get; set; }
        public ushort Graphic { get; set; }
        public ushort Hue { get; set; }
        public ushort AnimID { get; set; }
        public bool IsPartialHue { get; set; }
    }

    internal sealed class PaperdollSaveData
    {
        public ushort BodyId { get; set; }
        public bool IsFemale { get; set; }
        public byte Race { get; set; }
        public ushort NameHue { get; set; }
        public Dictionary<string, PaperdollItem> Items { get; set; }
    }

    internal class PaperdollSelectCharManager
    {
        public static PaperdollSelectCharManager Instance => instance ??= new PaperdollSelectCharManager();

        public Dictionary<string, PaperdollItem> items = new Dictionary<string, PaperdollItem>();

        public ushort BodyId { get; private set; }
        public bool IsFemale { get; private set; }
        public byte Race { get; private set; }

        private string savePath
        {
            get
            {
                if (!string.IsNullOrEmpty(ProfileManager.ProfilePath))
                {
                    return Path.Combine(ProfileManager.ProfilePath, "paperdollSelectCharManager.json");
                }

                string username = !string.IsNullOrWhiteSpace(LoginScene.Account)
                    ? LoginScene.Account.Trim()
                    : (Settings.GlobalSettings.Username ?? string.Empty).Trim();
                string server = ProfileManager.ResolveLoginServerNameForPaperdoll();
                string character = World.Player?.Name;
                if (string.IsNullOrWhiteSpace(character))
                {
                    character = "player";
                }

                return ProfileManager.GetPaperdollSelectCharJsonPath(username, server, character);
            }
        }

        private static PaperdollSelectCharManager instance;

        public static void Reset()
        {
            instance = null;
        }

        private PaperdollSelectCharManager()
        {
            Load();
        }

        public void AddItem(string key, Layer layer, ushort graphic, ushort hue, uint serial, ushort animID, bool isPartialHue)
        {
            if (layer != Layer.Bracelet && layer != Layer.Earrings && layer != Layer.Ring && layer != Layer.Backpack)
            {
                if (items.ContainsKey(key))
                {
                    items[key] = new PaperdollItem
                    {
                        Layer = layer,
                        Graphic = graphic,
                        Hue = hue,
                        Serial = serial,
                        AnimID = animID,
                        IsPartialHue = isPartialHue
                    };
                }
                else
                {
                    items.Add(key, new PaperdollItem
                    {
                        Layer = layer,
                        Graphic = graphic,
                        Hue = hue,
                        Serial = serial,
                        AnimID = animID,
                        IsPartialHue = isPartialHue
                    });
                }
            }
            
        }

        public void Save()
        {
            try
            {
                if (World.Player == null)
                {
                    return;
                }

                items.Clear();
                Mobile mobile = World.Mobiles.Get(World.Player.Serial);

                if (mobile != null)
                {
                    BodyId = (ushort)mobile.Graphic;
                    IsFemale = mobile.IsFemale;
                    Race = (byte)mobile.Race;

                    foreach (Layer layer in Enum.GetValues<Layer>())
                    {
                        Item item = mobile.FindItemByLayer(layer);

                        if (item != null)
                        {
                            if (item.Layer != Layer.Bracelet && item.Layer != Layer.Earrings && item.Layer != Layer.Ring && item.Layer != Layer.Backpack)
                            {
                                if (mobile.Serial == World.Player.Serial)
                                {
                                    if (layer != Layer.Bracelet && layer != Layer.Earrings && layer != Layer.Ring && layer != Layer.Backpack)
                                    {
                                        AddItem(item.Serial.ToString(), item.Layer, item.Graphic, item.Hue, item.Serial, item.ItemData.AnimID, item.ItemData.IsPartialHue);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"PaperdollSelectCharManager.Save: {ex}");
            }
        }

        public void SaveJson()
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (File.Exists(savePath))
                {
                    File.WriteAllText(savePath, string.Empty);
                }

                PaperdollSaveData saveData = new PaperdollSaveData
                {
                    BodyId = BodyId,
                    IsFemale = IsFemale,
                    Race = Race,
                    NameHue = World.Player != null ? Notoriety.GetHue(World.Player.NotorietyFlag) : (ushort)0,
                    Items = new Dictionary<string, PaperdollItem>(items)
                };

                string json = JsonSerializer.Serialize(saveData, typeof(PaperdollSaveData), GameManagersJsonContext.Default);

                File.WriteAllText(savePath, json);

                items.Clear();
                Log.Info($"PaperdollSelectCharManager: wrote {savePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"PaperdollSelectCharManager.SaveJson: {ex}");
            }
        }

        public void Load()
        {
            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);

                    PaperdollSaveData saveData =
                        JsonSerializer.Deserialize(json, typeof(PaperdollSaveData), GameManagersJsonContext.Default)
                        as PaperdollSaveData;
                    if (saveData != null && saveData.Items != null)
                    {
                        BodyId = saveData.BodyId;
                        IsFemale = saveData.IsFemale;
                        Race = saveData.Race;
                        items = saveData.Items;
                        return;
                    }

                    items =
                        JsonSerializer.Deserialize(json, typeof(Dictionary<string, PaperdollItem>), GameManagersJsonContext.Default)
                        as Dictionary<string, PaperdollItem> ?? new Dictionary<string, PaperdollItem>();
                    BodyId = 0;
                    IsFemale = false;
                    Race = (byte)RaceType.HUMAN;
                }
                catch (Exception ex)
                {
                    Log.Error($"PaperdollSelectCharManager.Load: {ex}");
                }
            }
        }
    }
}