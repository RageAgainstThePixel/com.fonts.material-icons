// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Fonts
{
    internal static class CheckDependencies
    {
        private static readonly string FontPath = "submodules\\material-design-icons\\font";
        private static readonly string PackagePath = "Packages\\com.fonts.material-icons";
        private static readonly char[] NewLines = { '\n' };
        private static readonly char[] EmptyLines = { ' ' };

        [InitializeOnLoadMethod]
        public static void Check()
        {
            var projectRootPath = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName);
            var submoduleFontPath = $"{projectRootPath}\\{FontPath}";

            if (!Directory.Exists(submoduleFontPath))
            {
                // Must be in a published package.
                return;
            }

            var packagePath = Directory.GetParent(Application.dataPath);
            var packageFontPath = $"{packagePath}\\{PackagePath}\\Runtime\\Fonts";

            if (!Directory.Exists(packageFontPath))
            {
                Directory.CreateDirectory(packageFontPath);

                var fonts = Directory.GetFiles(submoduleFontPath);

                foreach (var font in fonts)
                {
                    var newPath = font.Replace(submoduleFontPath, packageFontPath);

                    if (File.Exists(newPath))
                    {
                        File.Delete(newPath);
                    }

                    File.Copy(font, newPath);
                }

                AssetDatabase.Refresh(ImportAssetOptions.Default);

                ParseCodePoints();
            }
        }

        [MenuItem("Google/Parse Codepoints")]
        private static void ParseCodePoints()
        {
            var guids = AssetDatabase.FindAssets("MaterialIcons*");
            var assets = new Dictionary<string, Tuple<string, string, string>>(guids.Length);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (assetPath.Contains(".txt"))
                {
                    File.Delete(assetPath);
                    File.Delete($"{assetPath}.meta");
                    continue;
                }

                if (assetPath.Contains(".asmdef"))
                {
                    continue;
                }

                var fontName = Path.GetFileNameWithoutExtension(assetPath)
                    .Replace(" SDF", string.Empty);

                if (string.IsNullOrWhiteSpace(fontName))
                {
                    Debug.LogWarning($"Skipping {assetPath}");
                    continue;
                }

                if (!assets.TryGetValue(fontName, out var fontAssets))
                {
                    fontAssets = new Tuple<string, string, string>(null, null, null);
                    assets.Add(fontName, fontAssets);
                }

                var (fontPath, tmpFontPath, codePointsPath) = fontAssets;

                if (assetPath.Contains(".codepoints"))
                {
                    codePointsPath = assetPath;
                }

                if (assetPath.Contains(".ttf") ||
                    assetPath.Contains(".otf"))
                {
                    fontPath = assetPath;
                }

                if (assetPath.Contains(".asset"))
                {
                    tmpFontPath = assetPath;
                }

                if (!string.IsNullOrWhiteSpace(fontPath) ||
                    !string.IsNullOrWhiteSpace(tmpFontPath) ||
                    !string.IsNullOrWhiteSpace(codePointsPath))
                {
                    assets[fontName] = new Tuple<string, string, string>(fontPath, tmpFontPath, codePointsPath);
                }
            }

            foreach (var asset in assets)
            {
                var (fontPath, tmpFontPath, codePointsPath) = asset.Value;

                var text = File.ReadAllText(Path.GetFullPath(codePointsPath));
                var entries = text.Replace("\r", string.Empty)
                                  .Split(NewLines, StringSplitOptions.RemoveEmptyEntries);
                var glyphs = entries.Select(entry => entry.Split(EmptyLines, StringSplitOptions.RemoveEmptyEntries))
                                    .Select(codePoint => codePoint[1]).ToList();
                //var unicodeRange = string.Join(",", glyphs);
                uint[] unicode = new uint[glyphs.Count * 4];

                for (int i = 0; i < glyphs.Count; i++)
                {
                    unicode[i] = Convert.ToUInt32(glyphs[i], 16);
                }

                //File.WriteAllText($"{codePointsPath}.txt", unicodeRange);
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);

                if (string.IsNullOrWhiteSpace(tmpFontPath))
                {
                    Selection.activeObject = font;
                    #if UNITY_2023_1_OR_NEWER
                    TMP_FontAsset.CreateFontAsset(font);
                    #else
                    TMP_FontAsset_CreationMenu.CreateFontAsset();
                    #endif
                    AssetDatabase.Refresh(ImportAssetOptions.Default);
                    var folderPath = Path.GetDirectoryName(fontPath);
                    var assetName = Path.GetFileNameWithoutExtension(fontPath);

                    var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{folderPath}/{assetName} SDF.asset");

                    if (fontAsset.TryAddCharacters(unicode, out var missingUnicodes))
                    {
                        AssetDatabase.SaveAssets();
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to parse unicode range for {assetName}!\n{string.Join(",", missingUnicodes)}");
                    }
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.Default);
        }
    }
}
