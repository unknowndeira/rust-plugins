// Requires: Clans

using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Clan Team", "judeiras", "1.0.8")]
    [Description("Adds clan members to the same team")]
    class ClanTeam : CovalencePlugin
    {
        #region Definitions

        [PluginReference]
        private Plugin Clans;

        private readonly Dictionary<string, HashSet<ulong>> clans = new Dictionary<string, HashSet<ulong>>();

        #endregion Definitions

        #region Functions

        private bool CompareTeams(IEnumerable<ulong> currentIds, IEnumerable<ulong> clanIds) 
            => !clanIds.Except(currentIds).Any();

        private void GenerateClanTeam(List<ulong> memberIds)
        {
            if (memberIds == null || !memberIds.Any()) return;

            var clanTag = ClanTag(memberIds[0]);
            if (string.IsNullOrEmpty(clanTag)) return;

            clans[clanTag] = new HashSet<ulong>();
            var team = RelationshipManager.ServerInstance.CreateTeam();
            var processedPlayers = new HashSet<ulong>();
            var isFirstMember = true;
            var playersToUpdate = new List<BasePlayer>();

            foreach (var memberId in memberIds)
            {
                if (processedPlayers.Contains(memberId)) continue;
                
                var player = BasePlayer.FindByID(memberId);
                if (player == null) continue;

                if (player.currentTeam != 0UL)
                {
                    var current = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    current?.RemovePlayer(player.userID);
                }

                clans[clanTag].Add(player.userID);
                processedPlayers.Add(memberId);
                playersToUpdate.Add(player);

                try
                {
                    if (isFirstMember)
                    {
                        team.SetTeamLeader(player.userID);
                        isFirstMember = false;
                    }
                    if (!team.members.Contains(player.userID))
                    {
                        team.AddPlayer(player);
                    }
                }
                catch (System.ArgumentException)
                {
                    continue;
                }
            }

            // Update UI for all players in the team
            foreach (var player in playersToUpdate)
            {
                player.SendNetworkUpdate();
                UpdateTeamUI(player);
            }
        }

        private void UpdateTeamUI(BasePlayer player)
        {
            if (player == null) return;

            // Force update team UI
            player.TeamUpdate();
            
            // Update for all players in view
            foreach (var otherPlayer in BasePlayer.activePlayerList)
            {
                if (otherPlayer == player) continue;
                
                // Update team UI for both players
                player.SendNetworkUpdate();
                otherPlayer.SendNetworkUpdate();
                
                // Force team UI update for both players
                if (player.currentTeam != 0UL)
                {
                    var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (team != null)
                    {
                        foreach (var member in team.members)
                        {
                            var memberPlayer = BasePlayer.FindByID(member);
                            memberPlayer?.TeamUpdate();
                        }
                    }
                }
                
                if (otherPlayer.currentTeam != 0UL)
                {
                    var otherTeam = RelationshipManager.ServerInstance.FindTeam(otherPlayer.currentTeam);
                    if (otherTeam != null)
                    {
                        foreach (var member in otherTeam.members)
                        {
                            var memberPlayer = BasePlayer.FindByID(member);
                            memberPlayer?.TeamUpdate();
                        }
                    }
                }
            }
        }

        private bool IsAnOwner(BasePlayer player)
        {
            if (player == null) return false;
            var clanInfo = Clans?.Call<JObject>("GetClan", Clans.Call<string>("GetClanOf", player.userID));
            return clanInfo != null && (string)clanInfo["owner"] == player.UserIDString;
        }

        private string ClanTag(ulong memberId) 
            => Clans?.Call<string>("GetClanOf", memberId);

        private List<ulong> ClanPlayers(BasePlayer player)
        {
            if (player == null) return new List<ulong>();
            var clanInfo = Clans?.Call<JObject>("GetClan", ClanTag(player.userID));
            return clanInfo?["members"]?.ToObject<List<ulong>>() ?? new List<ulong>();
        }

        private List<ulong> ClanPlayersTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return new List<ulong>();
            var clanInfo = Clans?.Call<JObject>("GetClan", tag);
            return clanInfo?["members"]?.ToObject<List<ulong>>() ?? new List<ulong>();
        }

        #endregion Functions

        #region Hooks

        private void OnClanCreate(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            timer.Once(3f, () =>
            {
                var clanPlayers = new HashSet<ulong>();
                var clanInfo = Clans?.Call<JObject>("GetClan", tag);
                var players = clanInfo?["members"] as JArray;

                if (players == null) return;

                foreach (var memberId in players.Values<string>())
                {
                    if (ulong.TryParse(memberId, out var clanId) && clanId != 0UL)
                    {
                        var player = BasePlayer.FindByID(clanId);
                        if (player != null)
                        {
                            clanPlayers.Add(clanId);
                        }
                    }
                }
                
                if (clanPlayers.Any())
                {
                    GenerateClanTeam(clanPlayers.ToList());
                }
            });
        }

        private void OnClanUpdate(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                GenerateClanTeam(ClanPlayersTag(tag));
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            var clanTag = ClanTag(player.userID);
            if (string.IsNullOrEmpty(clanTag)) return;

            timer.Once(1f, () => 
            {
                UpdateTeamUI(player);
            });
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (player == null) return;

            var clanTag = ClanTag(player.userID);
            if (string.IsNullOrEmpty(clanTag)) return;

            // Check if player is still in clan
            var clanMembers = ClanPlayersTag(clanTag);
            if (clanMembers.Contains(player.userID))
            {
                // Force rejoin team after a short delay
                timer.Once(0.5f, () =>
                {
                    GenerateClanTeam(clanMembers);
                });
            }
        }

        #endregion Hooks
    }
}
