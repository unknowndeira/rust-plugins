using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Beds Cooldowns", "judeiras", "1.1.6")]
    [Description("Allows to change cooldowns for respawns on bags and beds")]
    public class BedsCooldowns : RustPlugin
    {
        private const string BED_IDENTIFIER = "bed";
        private Dictionary<string, SettingsEntry> _playerSettings;
        
        #region Oxide Hooks

        private void Init()
        {
            _playerSettings = new Dictionary<string, SettingsEntry>();
            config.list.ForEach(value => permission.RegisterPermission(value.perm, this));
        }

        private void OnServerInitialized() => 
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);

        private void OnEntitySpawned(SleepingBag entity)
        {
            if (entity == null) return;
            var settings = GetSettings(entity.OwnerID.ToString());
            SetCooldown(entity, settings);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            CheckPlayer(player);
        }

        #endregion

        #region Core

        private void CheckPlayer(BasePlayer player)
        {
            var settings = GetSettings(player.UserIDString);
            if (settings == null) return;
            ServerMgr.Instance.StartCoroutine(CheckBags(player.userID, settings));
        }
        
        private void SetCooldown(SleepingBag entity, SettingsEntry info)
        {
            if (info == null || entity == null) return;

            bool isBed = entity.ShortPrefabName.Contains(BED_IDENTIFIER);
            entity.secondsBetweenReuses = isBed ? info.bed : info.bag;
            entity.unlockTime = (isBed ? info.unlockTimeBed : info.unlockTimeBag) + Time.realtimeSinceStartup;
            entity.SendNetworkUpdate();
            
            var player = BasePlayer.FindByID(entity.OwnerID);
            player?.SendRespawnOptions();
        }

        private SettingsEntry GetSettings(string playerID)
        {
            if (string.IsNullOrEmpty(playerID)) return null;
            
            if (_playerSettings.TryGetValue(playerID, out var cachedSettings))
                return cachedSettings;

            var settings = config.list
                .Where(value => permission.UserHasPermission(playerID, value.perm))
                .OrderByDescending(x => x.priority)
                .FirstOrDefault();

            _playerSettings[playerID] = settings;
            return settings;
        }

        private IEnumerator CheckBags(ulong playerID, SettingsEntry settings)
        {
            if (settings == null) yield break;

            foreach (var entity in SleepingBag.sleepingBags.Where(x => x.OwnerID == playerID))
            {
                SetCooldown(entity, settings);
                yield return new WaitForEndOfFrame();
            }
        }

        #endregion
        
        #region Configuration

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "List")]
            public List<SettingsEntry> list = new List<SettingsEntry>();
        }
        
        private class SettingsEntry
        {
            [JsonProperty(PropertyName = "Permission")]
            public string perm;
            
            [JsonProperty(PropertyName = "Priority")]
            public int priority;
                
            [JsonProperty(PropertyName = "Sleeping bag cooldown")]
            public float bag;
                
            [JsonProperty(PropertyName = "Bed cooldown")]
            public float bed;

            [JsonProperty(PropertyName = "Sleeping bag unlock time")]
            public float unlockTimeBag;

            [JsonProperty(PropertyName = "Bed unlock time")]
            public float unlockTimeBed;
        }

        private ConfigData GetDefaultConfig() => new ConfigData
        {
            list = new List<SettingsEntry>
            {
                new SettingsEntry
                {
                    perm = "bedscooldowns.vip1",
                    priority = 1,
                    bag = 100,
                    bed = 100,
                    unlockTimeBag = 50,
                    unlockTimeBed = 50,
                },
                new SettingsEntry
                {
                    perm = "bedscooldowns.vip2",
                    priority = 2,
                    bag = 75,
                    bed = 75,
                    unlockTimeBag = 50,
                    unlockTimeBed = 50,
                },
                new SettingsEntry
                {
                    perm = "bedscooldowns.vip3",
                    priority = 3,
                    bag = 0,
                    bed = 0,
                    unlockTimeBag = 50,
                    unlockTimeBed = 50,
                }
            }
        };

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("Configuration file is corrupt! Unloading plugin...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = GetDefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}