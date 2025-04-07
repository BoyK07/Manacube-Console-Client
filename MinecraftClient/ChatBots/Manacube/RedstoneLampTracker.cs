using System;
using System.Threading.Tasks;
using MinecraftClient;
using MinecraftClient.ChatBots;
using MinecraftClient.Mapping;
using MinecraftClient.Scripting;

namespace MinecraftClient.ChatBots.Manacube
{
    public class RedstoneLampTracker : ChatBot
    {
        private readonly Location lampLocation = new Location(622, 80, 577);
        private Material? lastBlockMaterial;
        private bool isLampReady = false;

        public override void Initialize()
        {
            LogToConsole("[LampMonitor] Starting async redstone lamp watcher...");
            _ = RunDelayedLampWatcher(); // Fire-and-forget task
        }

        private async Task RunDelayedLampWatcher()
        {
            while (!isLampReady)
            {
                try
                {
                    Block block = GetWorld().GetBlock(lampLocation);

                    // This is a safe way to check if it's loaded
                    if (block.Type != Material.Air)
                    {
                        LogToConsole($"[DEBUG] Block at {lampLocation}: {block.Type}");

                        if (block.Type == Material.RedstoneLamp)
                        {
                            isLampReady = true;
                            lastBlockMaterial = block.Type;
                            LogToConsole($"[LampMonitor] Redstone lamp found: {block.Type}");
                            break;
                        }
                    }
                    else
                    {
                        LogToConsole($"[LampMonitor] Block at {lampLocation} is AIR. Waiting for chunk load...");
                    }
                }
                catch (Exception ex)
                {
                    LogToConsole($"[LampMonitor] Error checking block: {ex.Message}");
                }

                await Task.Delay(10000);
            }
        }


        public override void OnBlockChange(Location location, Block block)
        {
            if (!isLampReady)
                return;

            if (location == lampLocation)
            {
                Material currentMaterial = block.Type;

                if (currentMaterial != lastBlockMaterial)
                {
                    LogToConsole($"[LampMonitor] Lamp changed from {lastBlockMaterial} to {currentMaterial}");
                    lastBlockMaterial = currentMaterial;
                }
            }
        }
    }
}
