using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// U4.2 is additive. NEVER call the old U3 Install Team command after expanding:
    /// it replaces the team. This menu creates only missing species and preserves
    /// positions, the existing 5 animations, trainer choices, and combat components.
    /// </summary>
    public static class MonsterTacticsExtraMonsterMenu
    {
        private const string TeamName = "MonsterTactics U3 Monster Team";
        private const string ArtFolder = "Assets/MonsterTactics/Art/MonsterAnimated/";
        private const string Tool = "MonsterTactics/Tools/DownloadExtraMonsterSprites.ps1";

        private struct Extra
        {
            public string Name, Types, Role;
            public int HP, Column, Row;
            public Extra(string name, string types, string role, int hp, int col, int row)
            {
                Name = name; Types = types; Role = role; HP = hp; Column = col; Row = row;
            }
        }

        private static readonly Extra[] NewRoster =
        {
            new Extra("Pebblin", "Rock / Ground", "Tank", 950, 0, 0),
            new Extra("Mosslet", "Grass / Flying", "Ranged", 420, 2, 0),
            new Extra("Mysprit", "Psychic", "Mage", 370, 4, 0),
            new Extra("Tusklet", "Dragon", "Fighter", 680, 6, 0),
            new Extra("Dunebit", "Dragon / Ground", "Assassin", 550, 7, 0)
        };

        [MenuItem("MonsterTactics/Monster/U4.2/1 - Download Matching Sprites + Add Five", priority = 120)]
        public static void DownloadAndInstall()
        {
            if (!CanEdit()) return;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.2", "Automatic GIF conversion requires Windows PowerShell.\n\nUse the Add Five From Existing PNG Frames command instead, after putting frame_000.png, etc. in the documented asset folders.", "OK");
                return;
            }
            string script = Path.Combine(Application.dataPath,
                Tool.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.2", "Missing downloader:\n" + script, "OK");
                return;
            }

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            try
            {
                EditorUtility.DisplayProgressBar("MonsterTactics U4.2", "Fetching Black/White-style animated sprites...", 0.25f);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                        " -ProjectRoot " + Quote(project),
                    WorkingDirectory = project,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new InvalidOperationException("Couldn't start PowerShell.");
                    if (!process.WaitForExit(180000))
                    {
                        process.Kill();
                        throw new TimeoutException("Sprite download exceeded 3 minutes.");
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(output)) Debug.Log("MonsterTactics U4.2 sprites:\n" + output);
                    if (process.ExitCode != 0)
                        Debug.LogWarning("MonsterTactics U4.2 sprite download incomplete:\n" + errors);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MonsterTactics U4.2 downloader: " + ex.Message);
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AddOrRefresh();
        }

        [MenuItem("MonsterTactics/Monster/U4.2/2 - Add Five From Existing PNG Frames", priority = 121)]
        public static void AddOrRefresh()
        {
            if (!CanEdit()) return;
            GameObject arena = GameObject.Find("MonsterTactics Arena");
            GameObject team = GameObject.Find(TeamName);
            MonsterPlacementController placement = team != null ?
                team.GetComponent<MonsterPlacementController>() : null;
            if (arena == null || placement == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.2",
                    "Open the scene containing your working U1 arena and U3 team. Do not reinstall U3 or regenerate the board.", "OK");
                return;
            }
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.2", "No arena camera was found.", "OK");
                return;
            }

            var board = new Dictionary<Vector2Int, HexTile>();
            foreach (HexTile tile in arena.GetComponentsInChildren<HexTile>(true))
                board[new Vector2Int(tile.Column, tile.Row)] = tile;
            if (board.Count != 64)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.2", "Expected 64 U1 hexagons but found " + board.Count, "OK");
                return;
            }

            MonsterUnit[] existingUnits = team.GetComponentsInChildren<MonsterUnit>(true);
            var byName = new Dictionary<string, MonsterUnit>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<Vector2Int>();
            foreach (MonsterUnit unit in existingUnits)
            {
                if (unit == null) continue;
                byName[unit.MonsterName] = unit;
                if (unit.OccupiedTile != null)
                    used.Add(new Vector2Int(unit.OccupiedTile.Column, unit.OccupiedTile.Row));
            }

            // Prevalidate ALL assets before creating any objects, to avoid half-installed teams.
            var sprites = new Dictionary<string, Sprite[]>();
            var timings = new Dictionary<string, float[]>();
            var destinations = new Dictionary<string, HexTile>();
            var reserved = new HashSet<Vector2Int>(used);
            foreach (Extra entry in NewRoster)
            {
                string folder = ArtFolder + entry.Name.ToLowerInvariant();
                string absoluteFolder = Path.Combine(Application.dataPath, "MonsterTactics", "Art",
                    "MonsterAnimated", entry.Name.ToLowerInvariant());
                if (!Directory.Exists(absoluteFolder))
                {
                    MissingArt(entry.Name, folder); return;
                }
                string[] files = Directory.GetFiles(absoluteFolder, "frame_*.png")
                    .OrderBy(f => f, StringComparer.Ordinal).ToArray();
                if (files.Length == 0) { MissingArt(entry.Name, folder); return; }
                Sprite[] sequence = files.Select(file => PrepareSprite(folder + "/" + Path.GetFileName(file)))
                    .Where(sprite => sprite != null).ToArray();
                if (sequence.Length != files.Length || sequence.Length == 0)
                {
                    MissingArt(entry.Name, folder); return;
                }
                sprites[entry.Name] = sequence;
                timings[entry.Name] = ReadTimings(Path.Combine(absoluteFolder, "timings.txt"), sequence.Length);

                if (byName.ContainsKey(entry.Name)) continue;
                Vector2Int preferred = new Vector2Int(entry.Column, entry.Row);
                HexTile choice = null;
                if (board.TryGetValue(preferred, out HexTile preferredTile) &&
                    preferredTile.IsPlayerSide && !reserved.Contains(preferred)) choice = preferredTile;
                if (choice == null)
                    foreach (HexTile tile in board.Values.OrderBy(t => t.Row).ThenBy(t => t.Column))
                    {
                        Vector2Int coordinate = new Vector2Int(tile.Column, tile.Row);
                        if (tile.IsPlayerSide && !reserved.Contains(coordinate))
                        {
                            choice = tile;
                            break;
                        }
                    }
                if (choice == null)
                {
                    EditorUtility.DisplayDialog("MonsterTactics U4.2",
                        "No empty green hexagon available for " + entry.Name + ". Move a Monster to free a tile first.", "OK");
                    return;
                }
                destinations[entry.Name] = choice;
                reserved.Add(new Vector2Int(choice.Column, choice.Row));
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add MonsterTactics five new Monster");
            int added = 0;
            int refreshed = 0;
            foreach (Extra entry in NewRoster)
            {
                MonsterUnit unit;
                if (byName.TryGetValue(entry.Name, out MonsterUnit existing))
                {
                    unit = existing;
                    refreshed++;
                }
                else
                {
                    GameObject root = new GameObject(entry.Name);
                    Undo.RegisterCreatedObjectUndo(root, "Add " + entry.Name);
                    root.transform.SetParent(team.transform, false);
                    GameObject visual = new GameObject("Pixel Sprite (camera-facing)");
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = new Vector3(0f, .68f, 0f);
                    visual.transform.localScale = Vector3.one * 1.6f;
                    var renderer = visual.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprites[entry.Name][0];
                    renderer.sortingOrder = 30;
                    BoxCollider hitbox = root.AddComponent<BoxCollider>();
                    hitbox.center = new Vector3(0f, .68f, 0f);
                    hitbox.size = new Vector3(.79f, 1.19f, .79f);
                    hitbox.isTrigger = false;
                    unit = root.AddComponent<MonsterUnit>();
                    unit.Configure(entry.Name, entry.Types, entry.Role, entry.HP,
                        destinations[entry.Name], renderer, camera);
                    added++;
                }

                SpriteRenderer image = unit.GetComponentInChildren<SpriteRenderer>(true);
                if (image == null)
                {
                    Debug.LogWarning("MonsterTactics U4.2: " + entry.Name + " has no SpriteRenderer; skip animation refresh.");
                    continue;
                }
                MonsterFrameAnimator player = image.GetComponent<MonsterFrameAnimator>();
                if (player == null) player = Undo.AddComponent<MonsterFrameAnimator>(image.gameObject);
                Undo.RecordObject(image, "Refresh Monster sprite");
                Undo.RecordObject(player, "Refresh Monster animation");
                player.SetAnimation(image, sprites[entry.Name], timings[entry.Name]);
                image.transform.localScale = Vector3.one * 1.6f;
                EditorUtility.SetDirty(image);
                EditorUtility.SetDirty(player);
            }
            MonsterUnit[] teamNow = team.GetComponentsInChildren<MonsterUnit>(true);
            Undo.RecordObject(placement, "Update MonsterTactics U3 team roster");
            placement.Configure(camera, teamNow);
            EditorUtility.SetDirty(placement);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = team;
            Debug.Log("MonsterTactics U4.2: Added " + added + ", refreshed " + refreshed +
                ". Total deployed Monster: " + teamNow.Length + ". Other five, trainer skins and camera unchanged.");
            EditorUtility.DisplayDialog("MonsterTactics U4.2",
                "Added " + added + " Monster; refreshed " + refreshed + " animations.\n" +
                "Total Monster: " + teamNow.Length + ".\nSave (Ctrl+S), then press Play.", "OK");
        }

        [MenuItem("MonsterTactics/Monster/U4.2/3 - Verify Ten-Monster Combat Rules", priority = 122)]
        public static void Verify()
        {
            foreach (Extra entry in NewRoster)
            {
                MonsterTacticsCombatRules.Stats stats = MonsterTacticsCombatRules.ForMonster(entry.Name);
                MonsterTacticsCombatRules.AbilityProfile ability = MonsterTacticsCombatRules.AbilityFor(entry.Name, stats);
                if (stats.Ability != ability.Name || stats.ManaCost <= 0)
                    throw new InvalidOperationException(entry.Name + " ability or mana is incorrect.");
                if (entry.Name == "Pebblin" && (ability.ShieldRaw <= 0f || ability.PrimaryRaw != 0f))
                    throw new InvalidOperationException("Pebblin Rock Guard must grant shield, not damage.");
                if (entry.Name != "Pebblin" && ability.PrimaryRaw <= 0f)
                    throw new InvalidOperationException(entry.Name + " has no ability damage.");
            }
            if (MonsterTacticsCombatRules.ForMonster("Pebblin").Armor != 75 ||
                MonsterTacticsCombatRules.ForMonster("Mosslet").Range != 4 ||
                MonsterTacticsCombatRules.ForMonster("Dunebit").Attack != 85f)
                throw new InvalidOperationException("Old roster balance did not import correctly.");
            Debug.Log("MonsterTactics U4.2 PASS: five added Monster, scaling, shield, mana, range and balance.");
        }

        private static bool CanEdit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.2", "Stop Play mode first.", "OK");
                return false;
            }
            return true;
        }

        private static void MissingArt(string monster, string path)
        {
            EditorUtility.DisplayDialog("MonsterTactics U4.2",
                "Missing usable PNG sprite frames for " + monster + ".\n\n" +
                "Run '1 - Download Matching Sprites + Add Five' with internet, or place frame_000.png " +
                "(and optional extra frames) in:\n" + path + "\n\nNo Monster were added.", "OK");
        }

        private static Sprite PrepareSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 96f) ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 96f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static float[] ReadTimings(string path, int count)
        {
            var durations = Enumerable.Repeat(.11f, count).ToArray();
            if (!File.Exists(path)) return durations;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < count && i < lines.Length; i++)
                if (float.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out float seconds)) durations[i] = Mathf.Clamp(seconds, .055f, 1f);
            return durations;
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
