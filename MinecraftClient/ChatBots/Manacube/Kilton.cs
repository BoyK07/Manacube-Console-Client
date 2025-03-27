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
    public class Kilton : ChatBot
    {
        // Regex pattern to match Kilton messages
        private static readonly Regex KiltonPattern = new Regex(
            @"\[KILTON SR\.\] (?<player>.+) contributed \$(?<amount>\d+) towards summoning Kilton Sr\. \$(?<amountLeft>[\d,\.]+) Left \(/warp kiltonsr\)",
            RegexOptions.Compiled);
        
        // HttpClient for Discord webhook requests
        private static readonly HttpClient HttpClient = new HttpClient();
        
        // Store Manacube configuration at class level for easy access
        private readonly Settings.MainConfigHelper.MainConfig.ManacubeConfig manacubeConfig = Settings.Config.Main.Manacube;

        public override void Initialize()
        {
            // Print all Manacube-related configuration settings
            PrintManacubeConfig();
        }

        private void PrintManacubeConfig()
        {
            LogToConsole("§6======= Manacube Configuration Settings =======");
            
            // Print each property with proper formatting
            LogToConsole($"§3Enable Kilton Message: §f{manacubeConfig.EnableKiltonMessage}");
            LogToConsole($"§3Discord Webhook: §f{(string.IsNullOrEmpty(manacubeConfig.DiscordWebhook) ? "(not set)" : manacubeConfig.DiscordWebhook)}");
            LogToConsole($"§3Discord Ping Target: §f{(string.IsNullOrEmpty(manacubeConfig.DiscordPingTarget) ? "(not set)" : manacubeConfig.DiscordPingTarget)}");
            LogToConsole($"§3Kilton Ping Amount: §f{manacubeConfig.KiltonPingAmount}");
            
            LogToConsole("§6=============================================");
        }
        
        public override void GetText(string text, string json)
        {
            // Only process if Kilton messages are enabled
            if (!manacubeConfig.EnableKiltonMessage)
                return;
                
            // Strip color codes and other formatting for better matching
            string cleanText = GetVerbatim(text);
            
            // Check if the message matches the Kilton pattern
            var match = KiltonPattern.Match(cleanText);
            if (match.Success)
            {
                ProcessKiltonMessage(match, cleanText);
            }
        }
        
        private void ProcessKiltonMessage(Match match, string originalMessage)
        {
            // Exit early if webhook is not configured
            if (string.IsNullOrEmpty(manacubeConfig.DiscordWebhook))
            {
                LogToConsole("§eKilton message detected, but Discord webhook is not configured.");
                return;
            }
            
            // Extract values from the match
            string playerName = match.Groups["player"].Value;
            string contributionAmount = match.Groups["amount"].Value;
            string amountLeftStr = match.Groups["amountLeft"].Value;
            
            // For numeric comparison, strip commas and dots from amountLeft
            string strippedAmountLeft = amountLeftStr.Replace(",", "").Replace(".", "");
            
            // Convert to integers for comparison
            if (!long.TryParse(strippedAmountLeft, out long amountLeft))
            {
                LogToConsole($"§cError: Could not parse amount left as a number: {amountLeftStr}");
                return;
            }
            
            // Log the detected message
            LogToConsole($"§6Kilton contribution detected from §e{playerName}§6: §a${contributionAmount}§6, §c${amountLeftStr}§6 left");
            
            // Send webhook (fire and forget)
            _ = SendDiscordWebhook(originalMessage, amountLeft);
        }
        
        private async Task SendDiscordWebhook(string message, long amountLeft)
        {
            try
            {
                // Prepare the webhook message
                string pingPrefix = "";
                
                // Get the ping threshold and convert to a comparable number by removing commas and dots
                string strippedPingAmount = manacubeConfig.KiltonPingAmount.ToString().Replace(",", "").Replace(".", "");
                if (!long.TryParse(strippedPingAmount, out long pingThreshold))
                {
                    LogToConsole($"§cError: Could not parse KiltonPingAmount as a number: {manacubeConfig.KiltonPingAmount}");
                    pingThreshold = long.MaxValue; // Default to a high value to avoid pinging
                }
                
                // Check if we should ping
                if (amountLeft <= pingThreshold && 
                    !string.IsNullOrEmpty(manacubeConfig.DiscordPingTarget) && 
                    manacubeConfig.DiscordPingTarget.ToLower() != "none")
                {
                    // Format the ping based on the target type
                    if (manacubeConfig.DiscordPingTarget.ToLower() == "everyone")
                    {
                        pingPrefix = "@everyone ";
                    }
                    else if (manacubeConfig.DiscordPingTarget.StartsWith("role:"))
                    {
                        string roleId = manacubeConfig.DiscordPingTarget.Substring(5);
                        pingPrefix = $"<@&{roleId}> ";
                    }
                    else if (manacubeConfig.DiscordPingTarget.StartsWith("user:"))
                    {
                        string userId = manacubeConfig.DiscordPingTarget.Substring(5);
                        pingPrefix = $"<@{userId}> ";
                    }
                    else
                    {
                        // Assume it's a direct ping format
                        pingPrefix = $"{manacubeConfig.DiscordPingTarget} ";
                    }
                }
                
                // Prepare the webhook payload
                var payload = new
                {
                    content = pingPrefix + message,
                    username = "Manacube Kilton Alert"
                };
                
                // Serialize to JSON
                string jsonPayload = JsonSerializer.Serialize(payload);
                
                // Send the webhook request
                HttpContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await HttpClient.PostAsync(manacubeConfig.DiscordWebhook, content);
                
                // Check the response
                if (response.IsSuccessStatusCode)
                {
                    LogToConsole("§2Successfully sent Kilton notification to Discord.");
                }
                else
                {
                    LogToConsole($"§cFailed to send Discord webhook: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                LogToConsole($"§cError sending Discord webhook: {ex.Message}");
            }
        }
    }
}