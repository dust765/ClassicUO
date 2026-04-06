using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Dust765
{
    internal static class SplashScreenManager
    {
#if WINDOWS
        private const int WinW = 420;
        private const int WinH = 348;

        private static Thread _thread;
        private static volatile bool _ready;
        private static volatile bool _closeRequested;
        private static readonly object _textLock = new object();
        private static string _status = string.Empty;
        private static string _notice = string.Empty;

        public static void Show()
        {
            if (_thread != null)
            {
                return;
            }

            _ready = false;
            _closeRequested = false;
            lock (_textLock)
            {
                _status = string.Empty;
                _notice = string.Empty;
            }

            _thread = new Thread(SplashThreadMain)
            {
                Name = "CUO_SPLASH_SDL",
                IsBackground = true
            };
            _thread.Start();

            int waited = 0;
            while (!_ready && waited < 2000)
            {
                Thread.Sleep(20);
                waited += 20;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    string n = SplashReleaseChecker.TryGetUpdateNotice();
                    if (string.IsNullOrEmpty(n))
                    {
                        return;
                    }

                    lock (_textLock)
                    {
                        _notice = n;
                    }
                }
                catch
                {
                }
            });
        }

        public static void SetStatus(string text)
        {
            lock (_textLock)
            {
                _status = text ?? string.Empty;
            }
        }

        public static void Close()
        {
            _closeRequested = true;
            _thread?.Join(8000);
            _thread = null;
        }

        private static void SplashThreadMain()
        {
            IntPtr window = IntPtr.Zero;
            IntPtr renderer = IntPtr.Zero;
            IntPtr logoTex = IntPtr.Zero;

            SDL.SDL_SetMainReady();

            if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
            {
                _ready = true;
                return;
            }

            try
            {
                SDL.SDL_WindowFlags wflags =
                    SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN
                    | SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS
                    | SDL.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;

                if (!SDL.SDL_CreateWindowAndRenderer("ClassicUO", WinW, WinH, wflags, out window, out renderer))
                {
                    return;
                }

                uint disp = SDL.SDL_GetPrimaryDisplay();
                if (SDL.SDL_GetDisplayBounds(disp, out SDL.SDL_Rect bounds))
                {
                    int x = bounds.x + Math.Max(0, (bounds.w - WinW) / 2);
                    int y = bounds.y + Math.Max(0, (bounds.h - WinH) / 2);
                    SDL.SDL_SetWindowPosition(window, x, y);
                }

                int tw = 0, th = 0;
                logoTex = TryLoadLogoTexture(renderer, out tw, out th);

                SDL.SDL_ShowWindow(window);

                float opacity = 0f;

                _ready = true;

                while (true)
                {
                    SDL.SDL_Event evt;
                    while (SDL.SDL_PollEvent(out evt))
                    {
                        if (evt.type == (uint)SDL.SDL_EventType.SDL_EVENT_QUIT
                            || evt.type == (uint)SDL.SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED)
                        {
                            _closeRequested = true;
                        }
                    }

                    if (_closeRequested)
                    {
                        opacity -= 0.11f;
                        if (opacity < 0.02f)
                        {
                            break;
                        }
                    }
                    else if (opacity < 1f)
                    {
                        opacity = Math.Min(1f, opacity + 0.09f);
                    }

                    SDL.SDL_SetWindowOpacity(window, Math.Clamp(opacity, 0f, 1f));

                    SDL.SDL_SetRenderDrawColorFloat(renderer, 0.055f, 0.039f, 0.039f, 1f);
                    SDL.SDL_RenderClear(renderer);

                    string status;
                    string notice;
                    lock (_textLock)
                    {
                        status = _status;
                        notice = _notice;
                    }

                    if (logoTex != IntPtr.Zero && tw > 0 && th > 0)
                    {
                        float maxW = WinW - 48f;
                        float scale = Math.Min(1f, maxW / tw);
                        float dw = tw * scale;
                        float dh = th * scale;
                        float dx = (WinW - dw) * 0.5f;
                        float dy = 28f;
                        var src = new SDL.SDL_FRect { x = 0, y = 0, w = tw, h = th };
                        var dst = new SDL.SDL_FRect { x = dx, y = dy, w = dw, h = dh };
                        SDL.SDL_RenderTexture(renderer, logoTex, ref src, ref dst);
                    }

                    DrawProgressBar(renderer);

                    float textY = WinH - 78f;
                    foreach (string line in WrapForDebugText(notice, 52))
                    {
                        SDL.SDL_RenderDebugText(renderer, 10f, textY, line);
                        textY += 12f;
                    }

                    if (!string.IsNullOrEmpty(notice) && !string.IsNullOrEmpty(status))
                    {
                        textY += 4f;
                    }

                    if (!string.IsNullOrEmpty(status))
                    {
                        SDL.SDL_RenderDebugText(renderer, 10f, textY, Truncate(status, 64));
                    }

                    SDL.SDL_RenderPresent(renderer);
                    Thread.Sleep(16);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SplashScreen] {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (logoTex != IntPtr.Zero)
                {
                    SDL.SDL_DestroyTexture(logoTex);
                }

                if (renderer != IntPtr.Zero)
                {
                    SDL.SDL_DestroyRenderer(renderer);
                }

                if (window != IntPtr.Zero)
                {
                    SDL.SDL_DestroyWindow(window);
                }

                _ready = true;
            }
        }

        private static IntPtr TryLoadLogoTexture(IntPtr renderer, out int tw, out int th)
        {
            tw = 0;
            th = 0;

            try
            {
                string logoPath = Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Client", "logodust.png");
                if (!File.Exists(logoPath))
                {
                    return IntPtr.Zero;
                }

                using Image<Rgba32> image = Image.Load<Rgba32>(logoPath);
                tw = image.Width;
                th = image.Height;
                if (tw <= 0 || th <= 0)
                {
                    return IntPtr.Zero;
                }

                var pixels = new Rgba32[tw * th];
                image.CopyPixelDataTo(pixels);

                IntPtr tex = SDL.SDL_CreateTexture(
                    renderer,
                    SDL.SDL_PixelFormat.SDL_PIXELFORMAT_RGBA8888,
                    SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC,
                    tw,
                    th);

                if (tex == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                GCHandle h = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    IntPtr ptr = h.AddrOfPinnedObject();
                    var rect = new SDL.SDL_Rect { x = 0, y = 0, w = tw, h = th };
                    int pitch = tw * 4;
                    if (!SDL.SDL_UpdateTexture(tex, ref rect, ptr, pitch))
                    {
                        SDL.SDL_DestroyTexture(tex);
                        return IntPtr.Zero;
                    }
                }
                finally
                {
                    h.Free();
                }

                return tex;
            }
            catch
            {
                tw = 0;
                th = 0;
                return IntPtr.Zero;
            }
        }

        private static void DrawProgressBar(IntPtr renderer)
        {
            float barX = 40f;
            float barY = WinH - 40f;
            float barW = WinW - 80f;
            float barH = 8f;

            SDL.SDL_SetRenderDrawColorFloat(renderer, 0.17f, 0.086f, 0.086f, 1f);
            var bg = new SDL.SDL_FRect { x = barX, y = barY, w = barW, h = barH };
            SDL.SDL_RenderFillRect(renderer, ref bg);

            int span = Math.Max(1, (int)barW - 40);
            int phase = (int)((SDL.SDL_GetTicks() / 18) % (ulong)span);
            float segW = 56f;
            float x = barX + phase;
            SDL.SDL_SetRenderDrawColorFloat(renderer, 0.9f, 0.27f, 0.27f, 1f);
            var hi = new SDL.SDL_FRect { x = x, y = barY + 1f, w = segW, h = barH - 2f };
            if (hi.x + hi.w > barX + barW)
            {
                hi.w = barX + barW - hi.x;
            }

            if (hi.w > 1f)
            {
                SDL.SDL_RenderFillRect(renderer, ref hi);
            }

            SDL.SDL_SetRenderDrawColorFloat(renderer, 0.47f, 0.12f, 0.12f, 1f);
            SDL.SDL_RenderRect(renderer, ref bg);
        }

        private static string Truncate(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxChars)
            {
                return s;
            }

            return s.Substring(0, maxChars - 1) + "…";
        }

        private static IEnumerable<string> WrapForDebugText(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            string[] words = text.Split(' ');
            var line = string.Empty;
            foreach (string w in words)
            {
                if (string.IsNullOrEmpty(w))
                {
                    continue;
                }

                string next = string.IsNullOrEmpty(line) ? w : line + " " + w;
                if (next.Length <= maxCharsPerLine)
                {
                    line = next;
                }
                else
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        yield return line;
                    }

                    line = w.Length > maxCharsPerLine ? Truncate(w, maxCharsPerLine) : w;
                }
            }

            if (!string.IsNullOrEmpty(line))
            {
                yield return line;
            }
        }
#else
        public static void Show()
        {
        }

        public static void SetStatus(string text)
        {
        }

        public static void Close()
        {
        }
#endif
    }
}
