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
        private static float _progressCurrent;
        private static float _progressTarget;

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
                _progressCurrent = 0f;
                _progressTarget = 0f;
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
                _progressTarget = Math.Max(_progressTarget, ResolveProgressTarget(_status));
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
                        lock (_textLock)
                        {
                            _progressTarget = 100f;
                        }
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

                    SDL.SDL_SetRenderDrawColorFloat(renderer, 0f, 0f, 0f, 1f);
                    SDL.SDL_RenderClear(renderer);

                    string status;
                    string notice;
                    float progress;
                    lock (_textLock)
                    {
                        status = _status;
                        notice = _notice;
                        _progressCurrent = MathF.Min(_progressTarget, _progressCurrent + 0.9f);
                        progress = _progressCurrent;
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

                    float barX = 20f;
                    float barY = WinH - 44f;
                    float barW = WinW - 40f;
                    float barH = 14f;

                    if (!string.IsNullOrEmpty(status))
                    {
                        SDL.SDL_SetRenderDrawColorFloat(renderer, 1f, 1f, 1f, 1f);
                        DrawCenteredDebugText(renderer, Truncate(status, 56), barY - 18f);
                    }

                    DrawProgressBar(renderer, progress, barX, barY, barW, barH);

                    SDL.SDL_SetRenderDrawColorFloat(renderer, 0.72f, 0.72f, 0.72f, 1f);
                    string versionText = $"Dust {CUOEnviroment.Version}";
                    DrawBottomRightDebugText(renderer, versionText, 8f, 8f);

                    float textY = WinH - 100f;
                    foreach (string line in WrapForDebugText(notice, 52))
                    {
                        DrawCenteredDebugText(renderer, line, textY);
                        textY += 12f;
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

                SDL.SDL_SetTextureBlendMode(tex, 1u);

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

        private static void DrawProgressBar(IntPtr renderer, float progress, float barX, float barY, float barW, float barH)
        {
            int pct = (int)Math.Clamp(progress, 0f, 100f);
            float fillW = (barW * pct) / 100f;

            SDL.SDL_SetRenderDrawColorFloat(renderer, 0.09f, 0.09f, 0.09f, 1f);
            var bg = new SDL.SDL_FRect { x = barX, y = barY, w = barW, h = barH };
            SDL.SDL_RenderFillRect(renderer, ref bg);

            if (fillW > 0f)
            {
                SDL.SDL_SetRenderDrawColorFloat(renderer, 0.85f, 0.15f, 0.15f, 1f);
                var fill = new SDL.SDL_FRect { x = barX + 1f, y = barY + 1f, w = Math.Max(1f, fillW - 2f), h = barH - 2f };
                SDL.SDL_RenderFillRect(renderer, ref fill);
            }

            SDL.SDL_SetRenderDrawColorFloat(renderer, 0.22f, 0.22f, 0.22f, 1f);
            SDL.SDL_RenderRect(renderer, ref bg);

            SDL.SDL_SetRenderDrawColorFloat(renderer, 1f, 1f, 1f, 1f);
            DrawCenteredDebugText(renderer, $"{pct}%", barY + 3f);
        }

        private static float ResolveProgressTarget(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return _progressTarget;
            }

            if (status.StartsWith("Loading client files", StringComparison.OrdinalIgnoreCase))
            {
                return 35f;
            }

            if (status.StartsWith("Starting up", StringComparison.OrdinalIgnoreCase))
            {
                return 80f;
            }

            return Math.Min(95f, _progressTarget + 12f);
        }

        private static void DrawCenteredDebugText(IntPtr renderer, string text, float y)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            float x = Math.Max(8f, (WinW - (text.Length * 8f)) * 0.5f);
            SDL.SDL_RenderDebugText(renderer, x, y, text);
        }

        private static void DrawBottomRightDebugText(IntPtr renderer, string text, float rightPadding, float bottomPadding)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            float x = Math.Max(4f, WinW - (text.Length * 8f) - rightPadding);
            float y = Math.Max(4f, WinH - 8f - bottomPadding);
            SDL.SDL_RenderDebugText(renderer, x, y, text);
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
