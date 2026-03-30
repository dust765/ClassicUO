using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using ClassicUO.Utility.Platforms;
using Microsoft.Xna.Framework;

namespace ClassicUO.Dust765.UI.Gumps
{
    internal class VersionHistory : Gump
    {
        private const string WIKI_URL = "https://github.com/dust765/ClassicUO/wiki";
        private const string DISCORD_URL = "https://discord.gg/RG9kAkmW";
        private const int WIDTH = 480;
        private const int HEIGHT = 520;
        private const ushort HUE_TITLE = 0x0022;
        private const ushort HUE_TEXT = 0xFFFF;
        private const ushort HUE_CONTENT = 0xFFFF;

        public VersionHistory() : base(0, 0)
        {
            X = (Client.Game.Window.ClientBounds.Width - WIDTH) >> 1;
            Y = (Client.Game.Window.ClientBounds.Height - HEIGHT) >> 1;
            Width = WIDTH;
            Height = HEIGHT;
            CanCloseWithRightClick = true;
            CanMove = true;

            Add(new AlphaBlendControl(0.95f)
            {
                X = 1,
                Y = 1,
                Width = WIDTH - 2,
                Height = HEIGHT - 2,
                Hue = 999
            });

            int startY = 25;
            Add(new Label("Dust765", true, HUE_TITLE, WIDTH - 30, 1, FontStyle.None)
            {
                X = 20,
                Y = startY
            });
            startY += 22;

            Add(new Label($"Version: {CUOEnviroment.Version}", true, HUE_TITLE, WIDTH - 30, 1, FontStyle.None)
            {
                X = 20,
                Y = startY
            });
            startY += 30;

            int scrollX = 20;
            int scrollY = startY;
            int scrollW = WIDTH - 40;
            int scrollH = HEIGHT - startY - 55;

            Add(new BorderControl(scrollX - 3, scrollY - 3, scrollW + 6, scrollH + 6, 3));
            Add(new AlphaBlendControl(1f) { X = scrollX, Y = scrollY, Width = scrollW, Height = scrollH });

            ScrollArea scroll = new ScrollArea(scrollX, scrollY, scrollW, scrollH, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };

            int contentY = 0;
            string[] sections =
            {
                "/c[yellow]v3.0.9/cd",
                "",
                "/c[yellow]Performance (GC/GPU/FPS)/cd",
                "- Frustum culling: skip rendering objects outside viewport to reduce GPU load",
                "- Batch optimization: group objects by type for fewer draw calls",
                "- LOD system: reduce detail for distant objects to improve FPS",
                "- Texture streaming and occlusion culling (optional, Ultra quality)",
                "- Performance monitor: FPS and object count overlay (commands: [perftest], [perfstats], [perfquality])",
                "- SDL optimizations: high-resolution timer and hints for high FPS",
                "- Graphics quality presets: Low/Medium/High apply optimized shadow and effect settings",
                "- ArrayPool usage for buffers to reduce GC pressure in networking and rendering",
                "",
                "/c[yellow]Fixes/cd",
                "- Enable title bar stats: checkbox can now be unchecked when using CUO window style",
                "- Options: scroll area no longer overlaps OK/Apply/Default/Cancel buttons on Nameplate Options and related pages",
                "- Paperdoll: Show all equipment layers option now refreshes correctly when changed in Options",
                "- Paperdoll: equipment slots visibility - shows all 17 slots when option is checked, only 6 left slots when unchecked",
                "- Paperdoll: Show all equipment layers applies only to player's own paperdoll, not other players' paperdolls",
                "- Nameplate Options: scrollbar area adjusted to stay above bottom buttons",
                ""
            };

            foreach (string line in sections)
            {
                if (string.IsNullOrEmpty(line))
                {
                    contentY += 8;
                    continue;
                }
                string displayText = HtmlTextHelper.ConvertUoColorCodesToHtml(line).Trim();
                Label lbl = new Label(displayText, true, HUE_CONTENT, scroll.Width - 30, 1, FontStyle.None, align: TEXT_ALIGN_TYPE.TS_LEFT, ishtml: true) { Y = contentY };
                scroll.Add(lbl);
                contentY += lbl.Height + 2;
            }

            Add(scroll);

            int btnY = HEIGHT - 40;
            int btnWidth = 160;

            NiceButton wikiBtn = new NiceButton(20, btnY, btnWidth, 25, ButtonAction.Activate, "Dust765 Wiki") { IsSelectable = false };
            wikiBtn.MouseUp += (s, e) => PlatformHelper.LaunchBrowser(WIKI_URL);
            Add(wikiBtn);

            NiceButton discordBtn = new NiceButton(WIDTH - btnWidth - 20, btnY, btnWidth, 25, ButtonAction.Activate, "Discord") { IsSelectable = false };
            discordBtn.MouseUp += (s, e) => PlatformHelper.LaunchBrowser(DISCORD_URL);
            Add(discordBtn);
        }
    }
}
