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
        
        // HttpClient for Discord API requests
        private static readonly HttpClient HttpClient = new HttpClient();
        
        // Store Manacube configuration at class level for easy access
        private readonly Settings.MainConfigHelper.MainConfig.ManacubeConfig manacubeConfig = Settings.Config.Main.Manacube;
        private readonly Settings.MainConfigHelper.MainConfig.ManacubeConfig.KiltonConfig kiltonConfig = Settings.Config.Main.Manacube.Kilton;

        public override void Initialize()
        {
            // ...
        }
        
        public override void GetText(string text, string json)
        {
            // Only process if Kilton messages are enabled
            if (!kiltonConfig.EnableKiltonMessage)
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
            // Exit early if Bot Token or Channel ID is not configured
            if (string.IsNullOrEmpty(manacubeConfig.DiscordBotToken) || string.IsNullOrEmpty(kiltonConfig.KiltonMessageChannel))
            {
                LogToConsole("§eKilton message detected, but Discord bot token or channel ID is not configured.");
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
            
            // Send message via Discord bot (fire and forget)
            _ = SendDiscordBotMessage(originalMessage, amountLeft);
        }
        
        private async Task SendDiscordBotMessage(string message, long amountLeft)
        {
            try
            {
                // Prepare the Discord message
                string pingPrefix = "";
                
                // Get the ping threshold and convert to a comparable number by removing commas and dots
                string strippedPingAmount = kiltonConfig.KiltonPingAmount.ToString().Replace(",", "").Replace(".", "");
                if (!long.TryParse(strippedPingAmount, out long pingThreshold))
                {
                    LogToConsole($"§cError: Could not parse KiltonPingAmount as a number: {kiltonConfig.KiltonPingAmount}");
                    pingThreshold = long.MaxValue; // Default to a high value to avoid pinging
                }
                
                // Check if we should ping
                if (amountLeft <= pingThreshold && 
                    !string.IsNullOrEmpty(kiltonConfig.KiltonPingTarget) && 
                    kiltonConfig.KiltonPingTarget.ToLower() != "none")
                {
                    // Format the ping based on the target type
                    if (kiltonConfig.KiltonPingTarget.ToLower() == "everyone")
                    {
                        pingPrefix = "@everyone ";
                    }
                    else if (kiltonConfig.KiltonPingTarget.StartsWith("role:"))
                    {
                        string roleId = kiltonConfig.KiltonPingTarget.Substring(5);
                        pingPrefix = $"<@&{roleId}> ";
                    }
                    else if (kiltonConfig.KiltonPingTarget.StartsWith("user:"))
                    {
                        string userId = kiltonConfig.KiltonPingTarget.Substring(5);
                        pingPrefix = $"<@{userId}> ";
                    }
                    else
                    {
                        // Assume it's a direct ping format
                        pingPrefix = $"{kiltonConfig.KiltonPingTarget} ";
                    }
                }
                
                // Prepare the message payload
                var payload = new
                {
                    content = pingPrefix + message,
                };
                
                // Serialize to JSON
                string jsonPayload = JsonSerializer.Serialize(payload);
                
                // Create request with proper headers
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://discord.com/api/v10/channels/{kiltonConfig.KiltonMessageChannel}/messages"),
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                
                // Add authorization header with bot token
                request.Headers.Add("Authorization", $"Bot {manacubeConfig.DiscordBotToken}");
                
                // Send the request
                HttpResponseMessage response = await HttpClient.SendAsync(request);
                
                // Check the response
                if (response.IsSuccessStatusCode)
                {
                    LogToConsole("§2Successfully sent Kilton notification to Discord.");
                }
                else
                {
                    LogToConsole($"§cFailed to send Discord message: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                LogToConsole($"§cError sending Discord message: {ex.Message}");
            }
        }
    }
}