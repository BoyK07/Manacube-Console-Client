using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using MinecraftClient.Protocol;
using MinecraftClient.Scripting;

namespace MinecraftClient.ChatBots.Manacube
{
    public class ManaPay : ChatBot
    {
        private readonly Settings.MainConfigHelper.MainConfig.Advanced mainAdvancedConfig = Settings.Config.Main.Advanced;
        private DateTime lastStatsCheck = DateTime.MinValue;
        private int manaToSend = 0;
        private string targetPlayer;

        public override void Initialize()
        {
            LogToConsole("ManaPay initialized");
            _targetPlayer = mainAdvancedConfig.BotOwners[0];
            if (_targetPlayer == "player1" || _targetPlayer == "player2") { // player1 and player2 are placeholders for the actual player names in the default config.
                LogToConsole($"ManaPay: {_targetPlayer} is the owner. This is the default configuration. Please change your BotOwners in the config.");
                return;
            } else {
                targetPlayer = _targetPlayer;
                ScheduleTask(CheckManaRoutine, TimeSpan.Zero);
            }
        }

        private async Task CheckManaRoutine()
        {
            // Only run every 2 hours
            if ((DateTime.Now - lastStatsCheck).TotalHours >= 2)
            {
                SendText("/stats");
                lastStatsCheck = DateTime.Now;

                // Wait 5 seconds for the server to respond
                await Task.Delay(5000);
            }

            ScheduleTask(CheckManaRoutine, TimeSpan.FromHours(2));
        }

        public override bool OnTextReceived(string text, string json)
        {
            if (text.Contains("Mana:"))
            {
                // Parse mana value from message
                var match = Regex.Match(text, @"Mana:\s*([\d,]+)");
                if (match.Success)
                {
                    string manaStr = match.Groups[1].Value.Replace(",", "");
                    if (int.TryParse(manaStr, out manaToSend) && manaToSend >= 1000)
                    {
                        LogToConsole($"Detected {manaToSend} mana — sending to {targetPlayer}");
                        SendText($"/mana pay {targetPlayer} {manaToSend}");
                    }
                }
            }

            return base.OnTextReceived(text, json);
        }
    }
}