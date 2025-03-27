using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using MinecraftClient.Protocol;
using MinecraftClient.Scripting;
using System.Collections.Generic;
using System.Timers;

namespace MinecraftClient.ChatBots.Manacube
{
    public class MagicPond : ChatBot
    {
        // HttpClient for Discord API requests
        private static readonly HttpClient HttpClient = new HttpClient();
        
        // Store Manacube configuration
        private readonly Settings.MainConfigHelper.MainConfig.ManacubeConfig manacubeConfig = Settings.Config.Main.Manacube;
        private readonly Settings.MainConfigHelper.MainConfig.ManacubeConfig.MagicPondConfig magicPondConfig = Settings.Config.Main.Manacube.MagicPond;
        
        // Timer for checking upcoming events
        private Timer checkTimer;
        
        // EST TimeZoneInfo
        private readonly TimeZoneInfo estTimeZone;
        
        // List of daily event times (in EST)
        private readonly List<TimeSpan> eventTimes = new List<TimeSpan>
        {
            new TimeSpan(3, 0, 0),  // 3:00 AM
            new TimeSpan(6, 0, 0),  // 6:00 AM
            new TimeSpan(10, 0, 0), // 10:00 AM
            new TimeSpan(15, 0, 0), // 3:00 PM
            new TimeSpan(18, 0, 0), // 6:00 PM
            new TimeSpan(22, 0, 0)  // 10:00 PM
        };
        
        // Track the last notified event to avoid duplicate notifications
        private DateTime lastNotifiedEvent = DateTime.MinValue;
        
        public MagicPond()
        {
            try
            {
                // Try to find EST timezone
                estTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch (Exception ex)
            {
                LogToConsole($"§cError finding EST timezone: {ex.Message}");
                // Fallback to a -5 hours offset from UTC
                estTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                    "EST",
                    new TimeSpan(-5, 0, 0),
                    "Eastern Standard Time",
                    "Eastern Standard Time");
            }
        }
        
        public override void Initialize()
        {   
            // Start timer to check for upcoming events every minute
            checkTimer = new Timer(60000); // 60 seconds
            checkTimer.Elapsed += CheckUpcomingEvents;
            checkTimer.AutoReset = true;
            checkTimer.Start();
            
            LogToConsole("§2Magic Pond event tracker initialized");
            
            // Perform an initial check
            CheckUpcomingEvents(null, null);
        }
        
        private void CheckUpcomingEvents(object sender, ElapsedEventArgs e)
        {
            try
            {
                // Get current time in EST
                DateTime estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estTimeZone);
                
                // Get notification offset in minutes
                int minutesBeforeNotifying = magicPondConfig.MagicPondTimeBeforeNotifying;
                
                // Check each event time
                foreach (TimeSpan eventTime in eventTimes)
                {
                    // Calculate today's event time
                    DateTime todayEvent = estNow.Date.Add(eventTime);
                    
                    // If event has already passed today, check tomorrow's event
                    if (todayEvent < estNow)
                    {
                        todayEvent = todayEvent.AddDays(1);
                    }
                    
                    // Calculate when we should notify (event time minus notification offset)
                    DateTime notifyTime = todayEvent.AddMinutes(-minutesBeforeNotifying);
                    
                    // Calculate time difference between now and when we should notify
                    TimeSpan timeDifference = notifyTime - estNow;
                    
                    // If it's time to notify (within one minute) and we haven't already notified for this event
                    if (timeDifference.TotalMinutes >= 0 && timeDifference.TotalMinutes < 1 && 
                        todayEvent != lastNotifiedEvent)
                    {
                        // Send notification
                        string message = $"Magic Pond event starting in {minutesBeforeNotifying} minutes! (at {todayEvent.ToString("h:mm tt")} EST)";
                        LogToConsole($"§6{message}");
                        
                        // Send Discord notification
                        _ = SendDiscordNotification(message);
                        
                        // Update last notified event
                        lastNotifiedEvent = todayEvent;
                    }
                }
            }
            catch (Exception ex)
            {
                LogToConsole($"§cError checking for upcoming events: {ex.Message}");
            }
        }
        
        private async Task SendDiscordNotification(string message)
        {
            // Exit early if Bot Token or Channel ID is not configured
            if (string.IsNullOrEmpty(manacubeConfig.DiscordBotToken) || 
                string.IsNullOrEmpty(magicPondConfig.MagicPondMessageChannel))
            {
                LogToConsole("§eDiscord notification not sent: bot token or channel ID not configured");
                return;
            }
            
            try
            {
                // Prepare ping prefix based on configuration
                string pingPrefix = "";
                
                if (!string.IsNullOrEmpty(magicPondConfig.MagicPondPingTarget) && 
                    magicPondConfig.MagicPondPingTarget.ToLower() != "none")
                {
                    // Format the ping based on the target type
                    if (magicPondConfig.MagicPondPingTarget.ToLower() == "everyone")
                    {
                        pingPrefix = "@everyone ";
                    }
                    else if (magicPondConfig.MagicPondPingTarget.StartsWith("role:"))
                    {
                        string roleId = magicPondConfig.MagicPondPingTarget.Substring(5);
                        pingPrefix = $"<@&{roleId}> ";
                    }
                    else if (magicPondConfig.MagicPondPingTarget.StartsWith("user:"))
                    {
                        string userId = magicPondConfig.MagicPondPingTarget.Substring(5);
                        pingPrefix = $"<@{userId}> ";
                    }
                    else
                    {
                        // Assume it's a direct ping format
                        pingPrefix = $"{magicPondConfig.MagicPondPingTarget} ";
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
                    RequestUri = new Uri($"https://discord.com/api/v10/channels/{magicPondConfig.MagicPondMessageChannel}/messages"),
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                
                // Add authorization header with bot token
                request.Headers.Add("Authorization", $"Bot {manacubeConfig.DiscordBotToken}");
                
                // Send the request
                HttpResponseMessage response = await HttpClient.SendAsync(request);
                
                // Check the response
                if (response.IsSuccessStatusCode)
                {
                    LogToConsole("§2Successfully sent Magic Pond notification to Discord");
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