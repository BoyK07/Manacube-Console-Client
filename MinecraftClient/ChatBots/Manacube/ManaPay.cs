using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MinecraftClient.Scripting;
using System.Linq;

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
                // Start the background task for periodic mana checks after a delay to connect to the gamemode
                Task.Delay(10000).ContinueWith(_ => CheckManaRoutine());
            }
        }

        private async Task CheckManaRoutine()
        {
            while (true)
            {
                // If 6 or more hours have passed, send the /stats command
                if ((DateTime.Now - lastStatsCheck).TotalHours >= 6)
                {
                    SendText("/stats");
                    lastStatsCheck = DateTime.Now;
                    // Wait a bit for the server's response
                    await Task.Delay(2000);
                }
                // Wait 6 hours between checks
                await Task.Delay(TimeSpan.FromHours(6));
            }
        }

        public override void GetText(string text, string json)
        {
            // Strip Minecraft formatting codes (e.g. §r, §b, etc.)
            text = Regex.Replace(text, @"\u00A7.", "");

            if (text.Contains("Mana:"))
            {
                int idx = text.IndexOf("Mana:") + "Mana:".Length;
                string after = text.Substring(idx).Trim();

                string rawValue = new string(after.TakeWhile(c => char.IsDigit(c) || c == ',').ToArray());

                string cleaned = rawValue.Replace(",", "");
                if (int.TryParse(cleaned, out manaToSend) && manaToSend >= 1000)
                {
                    LogToConsole($"Detected {manaToSend} mana — sending to {targetPlayer}");
                    SendText($"/mana pay {targetPlayer} {manaToSend}");
                }
            }
        }
    }
}
