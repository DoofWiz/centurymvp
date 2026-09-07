using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Century.Campaign.Sim;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// Three save slots. JSON files under the persistent data path on desktop; PlayerPrefs on WebGL,
    /// where the virtual file system does not survive a page reload without extra plumbing. The
    /// campaign layer talks to this through <see cref="ISaveSlots"/> and never sees a path.
    /// </summary>
    public sealed class SaveService : ISaveSlots
    {
        private const int Slots = 3;
        private const string PrefsKeyPrefix = "century.save.slot";

        private readonly GameDirector _director;
        private readonly CampaignSaveData[] _headers = new CampaignSaveData[Slots];
        private readonly bool[] _headerRead = new bool[Slots];

        public SaveService(GameDirector director)
        {
            _director = director ?? throw new ArgumentNullException(nameof(director));
        }

        public int SlotCount => Slots;

        public bool CanSaveNow => _director.HasCampaign;

        public SaveSlotInfo Describe(int slot)
        {
            var info = new SaveSlotInfo { Slot = slot, IsEmpty = true };
            if (slot < 0 || slot >= Slots) return info;

            CampaignSaveData header = Header(slot);
            if (header == null) return info;

            info.IsEmpty = false;
            info.Title = string.IsNullOrEmpty(header.Title) ? $"SLOT {slot + 1}" : header.Title.ToUpperInvariant();
            info.Detail = header.Detail ?? string.Empty;
            info.SavedAt = FormatStamp(header.SavedAtUtc);
            return info;
        }

        public bool Save(int slot)
        {
            if (slot < 0 || slot >= Slots || !_director.HasCampaign) return false;

            try
            {
                string json = CampaignSerializer.ToJson(_director.Campaign, _director.EventLog);
                Write(slot, json);
                _headerRead[slot] = false;
                Debug.Log($"[Save] Slot {slot + 1} written ({json.Length / 1024} KB).");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Slot {slot + 1} could not be written: {e.Message}");
                return false;
            }
        }

        public bool Load(int slot)
        {
            if (slot < 0 || slot >= Slots) return false;

            string json = Read(slot);
            if (string.IsNullOrEmpty(json)) return false;

            Campaign.Model.CampaignState state = CampaignSerializer.FromJson(json, out List<SavedEvent> events);
            if (state == null) return false;

            Debug.Log($"[Save] Slot {slot + 1} loaded: {CampaignSerializer.TitleFor(state)}.");
            _director.LoadCampaign(state, events);
            return true;
        }

        public bool Delete(int slot)
        {
            if (slot < 0 || slot >= Slots) return false;
            Remove(slot);
            _headerRead[slot] = false;
            return true;
        }

        // --- Storage ------------------------------------------------------------------------------

        private CampaignSaveData Header(int slot)
        {
            if (_headerRead[slot]) return _headers[slot];
            _headerRead[slot] = true;
            _headers[slot] = CampaignSerializer.ReadHeader(Read(slot));
            return _headers[slot];
        }

        private static string PathFor(int slot) =>
            Path.Combine(Application.persistentDataPath, "saves", $"slot{slot + 1}.json");

        private static string Read(int slot)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string key = PrefsKeyPrefix + slot;
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
#else
            string path = PathFor(slot);
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
                return null;
            }
#endif
        }

        private static void Write(int slot, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(PrefsKeyPrefix + slot, json);
            PlayerPrefs.Save();
#else
            string path = PathFor(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
#endif
        }

        private static void Remove(int slot)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.DeleteKey(PrefsKeyPrefix + slot);
            PlayerPrefs.Save();
#else
            string path = PathFor(slot);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not delete {path}: {e.Message}");
            }
#endif
        }

        private static string FormatStamp(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return string.Empty;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when))
                return iso;
            return when.ToLocalTime().ToString("d MMM yyyy · HH:mm", CultureInfo.InvariantCulture).ToUpperInvariant();
        }
    }
}
