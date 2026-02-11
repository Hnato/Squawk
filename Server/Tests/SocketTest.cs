using System;
using System.Net.NetworkInformation;
using System.Linq;

namespace Squawk.Server.Tests
{
    public static class SocketValidator
    {
        public static bool IsPortOpen(int port)
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = properties.GetActiveTcpListeners();
            return tcpListeners.Any(l => l.Port == port);
        }

        public static void ValidateOnlyTargetPort(int targetPort)
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = properties.GetActiveTcpListeners();
            
            Console.WriteLine("\n[VALIDATION] Checking active TCP listeners...");
            bool foundOther = false;
            
            foreach (var listener in tcpListeners)
            {
                if (listener.Port != targetPort)
                {
                    // Ignore common system ports or other known ports if necessary
                    if (listener.Port > 1024 && listener.Port != targetPort)
                    {
                        Console.WriteLine($"[WARNING] Unexpected port open: {listener.Port} ({listener.Address})");
                        foundOther = true;
                    }
                }
                else
                {
                    Console.WriteLine($"[OK] Target port found: {listener.Port} ({listener.Address})");
                }
            }

            if (!foundOther)
            {
                Console.WriteLine("[SUCCESS] No unexpected application ports found.");
            }
        }
    }
}
