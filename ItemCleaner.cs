using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ItemCleaner", "judeiras", "1.0.0")]
    [Description("Removes dropped items from the map with announcement")]
    public class ItemCleaner : RustPlugin
    {
        private const float CLEANUP_INTERVAL = 300f; // 5 minutes
        private const float WARNING_TIME = 30f; // 30 seconds warning
        private bool isWarningActive = false;
        private Configuration config;

        class Configuration
        {
            [JsonProperty("Warning Message")]
            public string WarningMessage = "<color=#ff0000>Cosmic Cleaner</color>\nAll dropped items going to be deleted in 30 seconds.";

            [JsonProperty("Second Warning Message")]
            public string SecondWarningMessage = "<color=#ff0000>Cosmic Cleaner</color>\nAll dropped items going to be deleted in 10 seconds.";

            [JsonProperty("Cleanup Message")]
            public string CleanupMessage = "<color=#ff0000>Cosmic Cleaner</color>\nCleaned up <color=#349eeb>{0}</color> dropped items from the map.";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        void Init()
        {
            LoadConfig();
            timer.Every(CLEANUP_INTERVAL, () => StartCleanupSequence());
        }

        private void StartCleanupSequence()
        {
            if (!isWarningActive)
            {
                var droppedItems = BaseNetworkable.serverEntities.OfType<DroppedItem>().ToList();
                if (droppedItems.Count < 10) return;

                isWarningActive = true;
                Server.Broadcast(config.WarningMessage);
                timer.Once(WARNING_TIME - 10f, () =>
                {
                    var currentItems = BaseNetworkable.serverEntities.OfType<DroppedItem>().Any();
                    if (currentItems)
                    {
                        Server.Broadcast(config.SecondWarningMessage);
                    }
                });
                timer.Once(WARNING_TIME, () =>
                {
                    CleanupItems();
                    isWarningActive = false;
                });
            }
        }

        private void CleanupItems()
        {
            var droppedItems = BaseNetworkable.serverEntities.OfType<DroppedItem>().ToList();
            var count = 0;

            foreach (var item in droppedItems)
            {
                if (!item.IsDestroyed)
                {
                    item.Kill();
                    count++;
                }
            }

            if (count > 0)
            {
                Server.Broadcast(string.Format(config.CleanupMessage, count));
            }
        }
    }
} 