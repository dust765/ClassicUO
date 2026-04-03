#region license

// Copyright (c) 2024, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.IO;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ZLibNative;

namespace ClassicUO.Assets
{
    public class GumpsLoader : UOFileLoader
    {
        private static GumpsLoader _instance;
        private UOFile _file;
        public PixelPicker _picker;

        public GumpsLoader(PixelPicker picker)
        {
            _picker = picker;
        }

        public const int MAX_GUMP_DATA_INDEX_COUNT = 0x10000;

        private GumpsLoader(int count) { }

        public static GumpsLoader Instance =>
            _instance ?? (_instance = new GumpsLoader(MAX_GUMP_DATA_INDEX_COUNT));

        public bool UseUOPGumps = false;

        public override Task Load()
        {
            return Task.Run(() =>
            {
                string path = UOFileManager.GetUOFilePath("gumpartLegacyMUL.uop");

                if (UOFileManager.IsUOPInstallation && File.Exists(path))
                {
                    _file = new UOFileUop(path, "build/gumpartlegacymul/{0:D8}.tga", true);
                    Entries = new UOFileIndex[
                        Math.Max(((UOFileUop)_file).TotalEntriesCount, MAX_GUMP_DATA_INDEX_COUNT)
                    ];
                    UseUOPGumps = true;
                }
                else
                {
                    path = UOFileManager.GetUOFilePath("gumpart.mul");
                    string pathidx = UOFileManager.GetUOFilePath("gumpidx.mul");

                    if (!File.Exists(path))
                    {
                        path = UOFileManager.GetUOFilePath("Gumpart.mul");
                    }

                    if (!File.Exists(pathidx))
                    {
                        pathidx = UOFileManager.GetUOFilePath("Gumpidx.mul");
                    }

                    _file = new UOFileMul(path, pathidx);

                    UseUOPGumps = false;
                }

                _file.FillEntries(ref Entries);

                string pathdef = UOFileManager.GetUOFilePath("gump.def");

                if (!File.Exists(pathdef))
                {
                    return;
                }

                using (DefReader defReader = new DefReader(pathdef, 3))
                {
                    while (defReader.Next())
                    {
                        int ingump = defReader.ReadInt();

                        if (
                            ingump < 0
                            || ingump >= MAX_GUMP_DATA_INDEX_COUNT
                            || ingump >= Entries.Length
                            || Entries[ingump].Length > 0
                        )
                        {
                            continue;
                        }

                        int[] group = defReader.ReadGroup();

                        if (group == null)
                        {
                            continue;
                        }

                        for (int i = 0; i < group.Length; i++)
                        {
                            int checkIndex = group[i];

                            if (
                                checkIndex < 0
                                || checkIndex >= MAX_GUMP_DATA_INDEX_COUNT
                                || checkIndex >= Entries.Length
                                || Entries[checkIndex].Length <= 0
                            )
                            {
                                continue;
                            }

                            Entries[ingump] = Entries[checkIndex];

                            Entries[ingump].Hue = (ushort)defReader.ReadInt();

                            break;
                        }
                    }
                }
            });
        }

        public void CreateAtlas(GraphicsDevice device)
        {
            // Atlas rendering uses TextureAtlas.Shared; just ensure _spriteInfos is sized correctly
            int needed = Math.Max(Entries?.Length ?? 0, MAX_GUMP_DATA_INDEX_COUNT);
            if (needed > _spriteInfos.Length)
                Array.Resize(ref _spriteInfos, needed);
        }

        struct SpriteInfo
        {
            public Texture2D Texture;
            public Rectangle UV;
        }

        private SpriteInfo[] _spriteInfos = new SpriteInfo[MAX_GUMP_DATA_INDEX_COUNT];

        public Texture2D GetGumpTexture(ushort graphic, out Rectangle bounds)
        {
            // ## BEGIN - END ## // TAZUO
            Texture2D png = UOFileManager.Current.Png.LoadGumpTexture2d(graphic);
            if (png != null)
            {
                bounds = png.Bounds;
                return png;
            }
            // ## BEGIN - END ## // TAZUO

            if (graphic >= _spriteInfos.Length || TextureAtlas.Shared == null)
            {
                bounds = Rectangle.Empty;
                return null;
            }

            ref var spriteInfo = ref _spriteInfos[graphic];

            if (spriteInfo.Texture == null)
            {
                AddSpriteToAtlas(graphic);
            }

            bounds = spriteInfo.UV;

            return spriteInfo.Texture;
        }

        public unsafe GumpInfo GetGump(uint index)
        {
            ref UOFileIndex entry = ref GetValidRefEntry((int)index);

            if (entry.Length <= 0)
            {
                return default;
            }

            ushort color = entry.Hue;

            _file.SetData(entry.Address, entry.FileSize);
            _file.Seek(entry.Offset);

            byte[] cbuf = _file.ReadArray(entry.Length);
            ReadOnlySpan<byte> raw = cbuf;

            bool hintCompressed =
                UOFileManager.Version >= ClientVersion.CV_7010400
                || GumpDataLooksZlibCompressed(raw);

            GumpInfo gi = default;
            if (hintCompressed)
            {
                gi = TryDecodeGumpZlibBwt(cbuf, color);
            }

            if (gi.Pixels.IsEmpty)
            {
                gi = TryDecodeGumpLegacyMul(raw, entry.Width, entry.Height, color);
            }

            if (gi.Pixels.IsEmpty && !hintCompressed)
            {
                gi = TryDecodeGumpZlibBwt(cbuf, color);
            }

            return gi;
        }

        private static bool GumpDataLooksZlibCompressed(ReadOnlySpan<byte> s) =>
            s.Length >= 2 && s[0] == 0x78 && (s[1] == 0x01 || s[1] == 0x5E || s[1] == 0x9C || s[1] == 0xDA);

        private static bool TryZlibInflateToArray(ReadOnlySpan<byte> source, out byte[] inflated)
        {
            inflated = null;
            try
            {
                byte[] arr = source.ToArray();
                using var ms = new MemoryStream(arr, writable: false);
                using var ds = new ZLIBStream(ms, CompressionMode.Decompress);
                using var output = new MemoryStream();
                ds.CopyTo(output);
                inflated = output.ToArray();
                return inflated.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private unsafe GumpInfo TryDecodeGumpZlibBwt(byte[] cbuf, ushort color)
        {
            if (!TryZlibInflateToArray(cbuf, out byte[] zlibOut) || zlibOut.Length < 8)
            {
                return default;
            }

            byte[] payload;
            try
            {
                payload = BwtDecompress.Decompress(zlibOut);
            }
            catch
            {
                payload = zlibOut;
            }

            if (payload == null || payload.Length < 8)
            {
                return default;
            }

            ReadOnlySpan<byte> p = payload;
            uint w = BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(0, 4));
            uint h = BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(4, 4));
            return DecodeGumpRunLengthPixels(p.Slice(8), w, h, color);
        }

        private unsafe GumpInfo TryDecodeGumpLegacyMul(ReadOnlySpan<byte> raw, int idxW, int idxH, ushort color)
        {
            if (idxW <= 0 || idxH <= 0)
            {
                return default;
            }

            return DecodeGumpRunLengthPixels(raw, (uint)idxW, (uint)idxH, color);
        }

        private unsafe GumpInfo DecodeGumpRunLengthPixels(ReadOnlySpan<byte> runData, uint w, uint h, ushort color)
        {
            if (w == 0 || h == 0 || w > 0x4000 || h > 0x4000)
            {
                return default;
            }

            ulong pixelCount = (ulong)w * h;

            if (pixelCount > int.MaxValue)
            {
                return default;
            }

            var reader = new StackDataReader(runData);
            IntPtr dataStart = reader.PositionAddress;
            var pixels = new uint[(int)pixelCount];
            int* lookuplist = (int*)dataStart;
            int gsize;
            var len = reader.Remaining;

            if (len < (int)(h * sizeof(int)))
            {
                return default;
            }

            for (int y = 0, half_len = len >> 2; y < h; y++)
            {
                int rowLookup = lookuplist[y];

                if (y < h - 1)
                {
                    gsize = lookuplist[y + 1] - rowLookup;
                }
                else
                {
                    gsize = half_len - rowLookup;
                }

                if (rowLookup < 0 || rowLookup > half_len || gsize < 0 || rowLookup + gsize > half_len)
                {
                    return default;
                }

                GumpBlock* gmul = (GumpBlock*)(dataStart + (rowLookup << 2));

                var pos = y * w;
                var rowEnd = pos + w;

                for (int i = 0; i < gsize; i++)
                {
                    uint val = gmul[i].Value;

                    if (color != 0 && val != 0)
                    {
                        val = UOFileManager.Current.Hues.ApplyHueRgba5551(gmul[i].Value, color);
                    }

                    if (val != 0)
                    {
                        val = HuesHelper.Color16To32(gmul[i].Value) | 0xFF_00_00_00;
                    }

                    var count = gmul[i].Run;

                    if (count == 0)
                    {
                        continue;
                    }

                    if (pos + count > rowEnd || pos + count > (uint)pixels.Length)
                    {
                        return default;
                    }

                    pixels.AsSpan().Slice((int)pos, count).Fill(val);
                    pos += count;
                }

                if (pos != rowEnd)
                {
                    return default;
                }
            }

            return new GumpInfo()
            {
                Pixels = pixels,
                Width = (int)w,
                Height = (int)h
            };
        }

       

        private void AddSpriteToAtlas(uint index)
        {
            var gumpInfo = GetGump(index);

            if (gumpInfo.Pixels.IsEmpty)
            {
                return;
            }

            ref var spriteInfo = ref _spriteInfos[index];
            spriteInfo.Texture = TextureAtlas.Shared.AddSprite(
                gumpInfo.Pixels,
                gumpInfo.Width,
                gumpInfo.Height,
                out var uvRectangle
            );
            spriteInfo.UV = uvRectangle;
            _picker.Set(index, gumpInfo.Width, gumpInfo.Height, gumpInfo.Pixels);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private ref struct GumpBlock
        {
            public readonly ushort Value;
            public readonly ushort Run;
        }
    }

    public ref struct GumpInfo
    {
        public Span<uint> Pixels;
        public int Width;
        public int Height;
    }
}