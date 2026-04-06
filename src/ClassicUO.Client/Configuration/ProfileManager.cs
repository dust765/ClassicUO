#region license

// Copyright (c) 2021, andreakarasho
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicUO.Game;
using ClassicUO.Game.Scenes;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;

namespace ClassicUO.Configuration
{
    public static class ProfileManager
    {
        public static Profile CurrentProfile { get; private set; }
        public static string ProfilePath { get; private set; }

        private static string _rootPath;
        private static string RootPath
        {
            get
            {
                if (string.IsNullOrEmpty(_rootPath))
                {
                    _rootPath = string.IsNullOrWhiteSpace(Settings.GlobalSettings.ProfilesPath)
                        ? Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Profiles")
                        : Settings.GlobalSettings.ProfilesPath;
                }
                return _rootPath;
            }
        }

        public static void SetProfileAsDefault(Profile profile)
        {
            ConfigurationResolver.Save(profile, Path.Combine(RootPath, "default.json"), ProfileJsonContext.DefaultToUse);
        }

        public static Profile NewFromDefault()
        {
            return ConfigurationResolver.Load<Profile>(Path.Combine(RootPath, "default.json"), ProfileJsonContext.DefaultToUse) ?? new Profile();
        }

        public static List<string> GetAllProfilePaths()
        {
            var results = new List<string>();
            if (!Directory.Exists(RootPath))
                return results;

            foreach (string dir in Directory.GetDirectories(RootPath, "*", SearchOption.AllDirectories))
            {
                string profileFile = Path.Combine(dir, "profile.json");
                if (File.Exists(profileFile) && !dir.Equals(ProfilePath, System.StringComparison.OrdinalIgnoreCase))
                    results.Add(dir);
            }
            return results;
        }

        public static List<string> GetSameServerProfilePaths()
        {
            if (CurrentProfile == null || string.IsNullOrEmpty(CurrentProfile.ServerName))
                return new List<string>();

            return GetAllProfilePaths()
                .Where(p => p.IndexOf(CurrentProfile.ServerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        public static void OverrideProfiles(Profile profile, List<string> targetPaths)
        {
            foreach (string path in targetPaths)
            {
                ConfigurationResolver.Save(profile, Path.Combine(path, "profile.json"), ProfileJsonContext.DefaultToUse);
            }
        }

        public static string GetPaperdollSelectCharJsonPath(string username, string serverName, string characterName)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                username = "default";
            }
            else
            {
                username = username.Trim();
            }

            if (string.IsNullOrWhiteSpace(serverName) || string.Equals(serverName.Trim(), "_", StringComparison.Ordinal))
            {
                serverName = "server";
            }
            else
            {
                serverName = serverName.Trim();
            }

            if (string.IsNullOrWhiteSpace(characterName))
            {
                characterName = "player";
            }
            else
            {
                characterName = characterName.Trim();
            }

            string dir = FileSystemHelper.CreateFolderIfNotExists(RootPath, username, serverName, characterName);
            return Path.Combine(dir, "paperdollSelectCharManager.json");
        }

        private static string SanitizeProfileSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return FileSystemHelper.RemoveInvalidChars(value.Trim());
        }

        private static string TryPaperdollFileUnder(string accountDir, string serverDirName, string characterDirName)
        {
            if (string.IsNullOrEmpty(accountDir) || string.IsNullOrEmpty(serverDirName) || string.IsNullOrEmpty(characterDirName))
            {
                return null;
            }

            string p = Path.Combine(accountDir, serverDirName, characterDirName, "paperdollSelectCharManager.json");
            return File.Exists(p) ? p : null;
        }

        private static string FindAccountDirectoryRoot(string sanitizedUsername)
        {
            if (string.IsNullOrEmpty(sanitizedUsername) || !Directory.Exists(RootPath))
            {
                return null;
            }

            string direct = Path.Combine(RootPath, sanitizedUsername);
            if (Directory.Exists(direct))
            {
                return direct;
            }

            try
            {
                foreach (string candidate in Directory.GetDirectories(RootPath))
                {
                    if (string.Equals(Path.GetFileName(candidate), sanitizedUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static string GetPaperdollSelectCharJsonReadPath(string username, string serverName, string characterName)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(serverName) || string.IsNullOrEmpty(characterName))
            {
                return null;
            }

            string u = SanitizeProfileSegment(username);
            string s = SanitizeProfileSegment(serverName);
            string c = SanitizeProfileSegment(characterName);
            if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(s) || string.IsNullOrEmpty(c))
            {
                return null;
            }

            return Path.Combine(RootPath, u, s, c, "paperdollSelectCharManager.json");
        }

        public static string ResolveLoginServerNameForPaperdoll()
        {
            string server = World.ServerName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(server) || server == "_")
            {
                server = (Settings.GlobalSettings.LastServerName ?? string.Empty).Trim();
            }

            if (string.IsNullOrEmpty(server) || server == "_")
            {
                server = "server";
            }

            return server;
        }

        public static IEnumerable<string> EnumerateLoginUsernamesForPaperdoll()
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(LoginScene.Account))
            {
                string u = LoginScene.Account.Trim();
                if (seen.Add(u))
                {
                    yield return u;
                }
            }

            if (!string.IsNullOrWhiteSpace(Settings.GlobalSettings.Username))
            {
                string u = Settings.GlobalSettings.Username.Trim();
                if (seen.Add(u))
                {
                    yield return u;
                }
            }
        }

        public static string FindExistingPaperdollSelectCharJson(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return null;
            }

            string charSan = SanitizeProfileSegment(characterName);
            if (string.IsNullOrEmpty(charSan))
            {
                return null;
            }

            string server = ResolveLoginServerNameForPaperdoll();
            string serverSan = string.IsNullOrEmpty(server) ? string.Empty : SanitizeProfileSegment(server);

            foreach (string username in EnumerateLoginUsernamesForPaperdoll())
            {
                string userSan = SanitizeProfileSegment(username);
                if (string.IsNullOrEmpty(userSan))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(serverSan))
                {
                    string p = GetPaperdollSelectCharJsonReadPath(username, server, characterName);
                    if (p != null && File.Exists(p))
                    {
                        return p;
                    }

                    string legacyUser = SanitizeProfileSegment(username);
                    string legacy = Path.Combine(
                        CUOEnviroment.ExecutablePath,
                        "Data",
                        "Profiles",
                        legacyUser,
                        serverSan,
                        charSan,
                        "paperdollSelectCharManager.json");
                    if (File.Exists(legacy))
                    {
                        return legacy;
                    }
                }

                string accRoot = FindAccountDirectoryRoot(userSan);
                if (accRoot == null)
                {
                    continue;
                }

                try
                {
                    foreach (string serverDir in Directory.GetDirectories(accRoot))
                    {
                        string serverLeaf = Path.GetFileName(serverDir);
                        if (!string.IsNullOrEmpty(serverSan) && !string.Equals(SanitizeProfileSegment(serverLeaf), serverSan, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string hit = TryPaperdollFileUnder(accRoot, serverLeaf, charSan);
                        if (hit != null)
                        {
                            return hit;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public static string ResolvePrimaryPaperdollSelectCharJsonReadPath(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return null;
            }

            string charSan = SanitizeProfileSegment(characterName);
            string username = !string.IsNullOrWhiteSpace(LoginScene.Account)
                ? LoginScene.Account.Trim()
                : (Settings.GlobalSettings.Username ?? string.Empty).Trim();
            string userSan = SanitizeProfileSegment(username);
            string server = ResolveLoginServerNameForPaperdoll();
            string serverSan = SanitizeProfileSegment(server);

            if (string.IsNullOrEmpty(userSan) || string.IsNullOrEmpty(charSan))
            {
                return null;
            }

            if (string.IsNullOrEmpty(serverSan))
            {
                return null;
            }

            return Path.Combine(RootPath, userSan, serverSan, charSan, "paperdollSelectCharManager.json");
        }

        public static void Load(string servername, string username, string charactername)
        {
            string path = FileSystemHelper.CreateFolderIfNotExists(RootPath, username, servername, charactername);
            string fileToLoad = Path.Combine(path, "profile.json");

            ProfilePath = path;
            CurrentProfile = ConfigurationResolver.Load<Profile>(fileToLoad, ProfileJsonContext.DefaultToUse) ?? NewFromDefault();

            CurrentProfile.Username = username;
            CurrentProfile.ServerName = servername;
            CurrentProfile.CharacterName = charactername;

            ValidateFields(CurrentProfile);

            Pathfinder.FastRotation = CurrentProfile.FastRotation;
            ClassicUO.Game.Managers.IgnoreManager.Initialize();
        }


        private static void ValidateFields(Profile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(profile.ServerName))
            {
                throw new InvalidDataException();
            }

            if (string.IsNullOrEmpty(profile.Username))
            {
                throw new InvalidDataException();
            }

            if (string.IsNullOrEmpty(profile.CharacterName))
            {
                throw new InvalidDataException();
            }

            if (profile.WindowClientBounds.X < ClassicUO.Game.Constants.MIN_GAME_WINDOW_WIDTH)
            {
                profile.WindowClientBounds = new Point(ClassicUO.Game.Constants.MIN_GAME_WINDOW_WIDTH, profile.WindowClientBounds.Y);
            }

            if (profile.WindowClientBounds.Y < ClassicUO.Game.Constants.MIN_GAME_WINDOW_HEIGHT)
            {
                profile.WindowClientBounds = new Point(profile.WindowClientBounds.X, ClassicUO.Game.Constants.MIN_GAME_WINDOW_HEIGHT);
            }

            profile.EnsurePerformanceFeaturesEnabled();
        }

        public static void UnLoadProfile()
        {
            CurrentProfile = null;
            ProfilePath = null;
        }
    }
}