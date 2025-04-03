using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MinecraftClient.Scripting;

namespace MinecraftClient.ChatBots.Manacube
{
    public class ManaPay : ChatBot
    {
        private readonly Settings.MainConfigHelper.MainConfig.AdvancedConfig mainAdvancedConfig = Settings.Config.Main.Advanced;
        private DateTime lastStatsCheck = DateTime.MinValue;
        private int manaToSend = 0;
        private string targetPlayer;

        public override void Initialize()
        {
            LogToConsole("ManaPay initialized");
            string _targetPlayer = mainAdvancedConfig.BotOwners[0];
            if (_targetPlayer == "player1" || _targetPlayer == "player2")
            {
                LogToConsole($"ManaPay: {_targetPlayer} is the owner. This is the default configuration. Please change your BotOwners in the config.");
                return;
            }
            else
            {
                targetPlayer = _targetPlayer;
                // Start the background task for periodic mana checks
                Task.Run(CheckManaRoutine);
            }
        }

        private async Task CheckManaRoutine()
        {
            while (true)
            {
                // If 2 or more hours have passed, send the /stats command
                if ((DateTime.Now - lastStatsCheck).TotalHours >= 2)
                {
                    SendText("/stats");
                    lastStatsCheck = DateTime.Now;
                    // Wait a bit for the server's response
                    await Task.Delay(5000);
                }
                // Wait 2 hours between checks
                await Task.Delay(TimeSpan.FromHours(2));
            }
        }

        public override void GetText(string text, string json)
        {
            if (text.Contains("Mana:"))
            {
                // Parse mana value from the message using regex
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
        }
    }
}
