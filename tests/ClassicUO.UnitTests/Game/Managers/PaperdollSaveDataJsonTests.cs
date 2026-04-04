using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers
{
    public class PaperdollSaveDataJsonTests
    {
        [Fact]
        public void PaperdollSaveData_round_trip_with_items_uses_source_context()
        {
            var original = new PaperdollSaveData
            {
                BodyId = 0x0190,
                IsFemale = false,
                Race = (byte)RaceType.HUMAN,
                NameHue = 0x03b2,
                Items = new Dictionary<string, PaperdollItem>
                {
                    ["4000111222"] = new PaperdollItem
                    {
                        Serial = 4000111222,
                        Layer = Layer.OneHanded,
                        Graphic = 0x0F5E,
                        Hue = 0,
                        AnimID = 0x0133,
                        IsPartialHue = false
                    }
                }
            };

            string json = JsonSerializer.Serialize(original, typeof(PaperdollSaveData), GameManagersJsonContext.Default);
            Assert.Contains("\"Items\"", json);
            Assert.Contains("4000111222", json);
            Assert.Matches(new Regex("\"Layer\"\\s*:\\s*1"), json);

            var copy = JsonSerializer.Deserialize(json, typeof(PaperdollSaveData), GameManagersJsonContext.Default) as PaperdollSaveData;
            Assert.NotNull(copy);
            Assert.NotNull(copy.Items);
            Assert.Single(copy.Items);
            Assert.Equal(0x0190, copy.BodyId);
            Assert.Equal(Layer.OneHanded, copy.Items["4000111222"].Layer);
            Assert.Equal(0x0F5E, copy.Items["4000111222"].Graphic);
        }

        [Fact]
        public void PaperdollSaveData_writes_file_with_items()
        {
            string path = Path.Combine(Path.GetTempPath(), "paperdollSelectCharManager_test_" + Path.GetRandomFileName() + ".json");
            try
            {
                var data = new PaperdollSaveData
                {
                    BodyId = 0x0191,
                    IsFemale = true,
                    Race = (byte)RaceType.HUMAN,
                    NameHue = 0,
                    Items = new Dictionary<string, PaperdollItem>
                    {
                        ["99"] = new PaperdollItem
                        {
                            Serial = 99,
                            Layer = Layer.Helmet,
                            Graphic = 0x1408,
                            Hue = 1,
                            AnimID = 10,
                            IsPartialHue = true
                        }
                    }
                };

                string json = JsonSerializer.Serialize(data, typeof(PaperdollSaveData), GameManagersJsonContext.Default);
                File.WriteAllText(path, json);

                Assert.True(File.Exists(path));
                string read = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize(read, typeof(PaperdollSaveData), GameManagersJsonContext.Default) as PaperdollSaveData;
                Assert.NotNull(loaded?.Items);
                Assert.True(loaded.Items.ContainsKey("99"));
                Assert.Equal(Layer.Helmet, loaded.Items["99"].Layer);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
