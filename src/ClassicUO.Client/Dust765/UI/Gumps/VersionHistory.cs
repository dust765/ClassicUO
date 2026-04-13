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
                "/c[yellow]v3.0.11/cd",
                "",
                "- Fix: restore SuppressDraw + Thread.Sleep(1) frame limiter (matching upstream CUO)",
                "- Fix: Texture2D.FromStream moved to main thread in GumpPicExternalUrl",
                "- Fix: MobileStatusRequestQueue GameActions now dispatched via MainThreadQueue",
                "- Fix: network backpressure Sleep(0) restored to Sleep(1) to avoid CPU spin",
                "",
                "/c[yellow]v3.0.10/cd",
                "",
                "- Update for net core 10",
                "- Update for fna to 26.01",
                "- Custom inputs for ms turn movement",
                "- Counters same experience as oficial cuo version",
                "- Nameplateoverhead same experience as oficial cuo version",
                "- Fixes for new cuo client 114",
                "- Color picker same experience as oficial cuo version",
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
