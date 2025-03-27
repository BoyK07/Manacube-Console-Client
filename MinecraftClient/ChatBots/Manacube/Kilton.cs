using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using MinecraftClient.Protocol;
using MinecraftClient.Scripting;

namespace MinecraftClient.ChatBots.Manacube
{
    public class Kilton : ChatBot
    {
        // Regex to detect Kilton progress messages.
        private static readonly Regex kiltonRegex = new Regex(
            @"\[KILTON SR.\] .* contributed \$[0-9,]+ towards summoning Kilton Sr\. \$([0-9,]+) Left",
            RegexOptions.Compiled);

        // Configurable parameters loaded from manacube.ini.
        private string webhookUrl = "";

        // We'll store ping configuration using an enum and an ID.
        private enum PingType { None, Everyone, Role, User }
        private PingType currentPingType = PingType.None;
        private string pingId = "";

        // This flag is true if the kilton event is enabled.
        private bool kiltonEnabled = false;

        public override void Initialize()
        {
            LoadConfig();
            LogToConsole("[ManacubeBot] Initialized");
        }

        public override void GetText(string text)
        {
            if (kiltonEnabled)
                CheckKiltonProgress(text);
        }

        /// <summary>
        /// Checks for Kilton progress messages and sends a Discord alert if ready.
        /// </summary>
        /// <param name="text">The chat message received.</param>
        private void CheckKiltonProgress(string text)
        {
            var match = kiltonRegex.Match(text);
            if (match.Success)
            {
                string leftStr = match.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(leftStr, out decimal leftAmount) && leftAmount <= 20000000)
                {
                    LogToConsole($"[ManacubeBot] ${leftAmount:N0} Left! Sending Discord alert...");
                    string message = FormatPingTarget() + $"**Kilton Sr. is (almost) ready to be summoned!** (${leftAmount:N0} left!) (/warp kiltonsr)";
                    SendDiscordMessage(message);
                }
            }
        }

        /// <summary>
        /// Returns the ping text to prepend to the message.
        /// </summary>
        private string FormatPingTarget()
        {
            switch (currentPingType)
            {
                case PingType.Everyone:
                    return "@everyone ";
                case PingType.Role:
                    return $"<@&{pingId}> ";
                case PingType.User:
                    return $"<@{pingId}> ";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Constructs the allowed_mentions JSON based on ping configuration.
        /// </summary>
        private string BuildAllowedMentionsJson()
        {
            switch (currentPingType)
            {
                case PingType.Everyone:
                    return "\"parse\":[\"everyone\"]";
                case PingType.Role:
                    return "\"roles\":[\"" + pingId + "\"]";
                case PingType.User:
                    return "\"users\":[\"" + pingId + "\"]";
                default:
                    return "\"parse\":[]";
            }
        }

        /// <summary>
        /// Sends a Discord webhook message using the ProxiedWebRequest.
        /// </summary>
        /// <param name="message">The message content.</param>
        private void SendDiscordMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
                return;

            // Build a JSON payload using the allowed_mentions configuration.
            string allowedMentions = BuildAllowedMentionsJson();
            string payload = $"{{\"content\":\"{EscapeJson(message)}\",\"allowed_mentions\":{{{allowedMentions}}}}}";

            try
            {
                var request = new ProxiedWebRequest(webhookUrl);
                request.Accept = "application/json";
                var response = request.Post("application/json", payload);
                LogToConsole($"[Discord] Message sent. Response: {response.Body}");
            }
            catch (Exception ex)
            {
                LogToConsole($"[Discord] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Escapes special characters in JSON.
        /// </summary>
        private string EscapeJson(string input)
        {
            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "");
        }

        /// <summary>
        /// Loads the configuration from manacube.ini.
        /// Expected section for Kilton is:
        /// [kilton]
        /// enabled = true
        /// webhook_url = <your_webhook_url>
        /// ping_target = everyone | none | role:<role_id> | user:<user_id>
        /// </summary>
        private void LoadConfig()
        {
            string path = "manacube.ini";
            if (!File.Exists(path))
            {
                // Generate a default configuration file.
                string defaultConfig =
"[kilton]\n" +
"enabled = true\n" +
"webhook_url = <discord_webhook_url>\n" +
"ping_target = everyone          # everyone, role:123456789012345678, user:123456789012345678, none\n";
                File.WriteAllText(path, defaultConfig);

                LogToConsole("[Config] manacube.ini not found. Default config generated.");
            }
            else
            {
                LogToConsole("[Config] manacube.ini was found.");
            }

            string section = "";
            foreach (var rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLower();
                    continue;
                }

                int commentIndex = line.IndexOfAny(new char[] { '#', ';' });
                if (commentIndex >= 0)
                    line = line.Substring(0, commentIndex).Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(new char[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                string key = parts[0].Trim().ToLower();
                string value = parts[1].Trim();

                if (section == "kilton")
                {
                    switch (key)
                    {
                        case "webhook_url":
                            webhookUrl = value;
                            break;
                        case "ping_target":
                            {
                                string v = value.Trim();
                                if (string.Equals(v, "none", StringComparison.OrdinalIgnoreCase))
                                {
                                    currentPingType = PingType.None;
                                    pingId = "";
                                }
                                else if (string.Equals(v, "everyone", StringComparison.OrdinalIgnoreCase))
                                {
                                    currentPingType = PingType.Everyone;
                                    pingId = "";
                                }
                                else if (v.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
                                {
                                    currentPingType = PingType.Role;
                                    pingId = v.Substring("role:".Length).Trim();
                                }
                                else if (v.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
                                {
                                    currentPingType = PingType.User;
                                    pingId = v.Substring("user:".Length).Trim();
                                }
                                else if (Regex.IsMatch(v, @"^\d{17,20}$"))
                                {
                                    // Default to role if just a numeric value is provided.
                                    currentPingType = PingType.Role;
                                    pingId = v;
                                }
                                else
                                {
                                    currentPingType = PingType.None;
                                    pingId = "";
                                }
                            }
                            break;
                        case "enabled":
                            kiltonEnabled = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
            }
        }

        public override string ToString()
        {
            return "Kilton Bot";
        }
    }
}
