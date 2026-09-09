using System.ServiceProcess;

namespace WindowsDevApp.Services;

public class ServiceInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "";
}

/// <summary>
/// Thin wrapper over ServiceController for listing, querying, and restarting
/// local Windows services. Restarting a service normally requires the process
/// to be elevated (see app.manifest).
/// </summary>
public static class ServiceControlService
{
    public static List<ServiceInfo> ListServices()
    {
        return ServiceController.GetServices()
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new ServiceInfo { Name = s.ServiceName, DisplayName = s.DisplayName, Status = s.Status.ToString() })
            .ToList();
    }

    public static string GetStatus(string serviceName)
    {
        using var sc = new ServiceController(serviceName);
        sc.Refresh();
        return sc.Status.ToString();
    }

    public static (bool Success, string Message) Restart(string serviceName, TimeSpan timeout)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            sc.Refresh();

            if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.Paused)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }

            sc.Refresh();
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }

            return (true, $"Service '{serviceName}' restarted successfully.");
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            return (false, $"Timed out waiting for service '{serviceName}' to change status.");
        }
        catch (InvalidOperationException ex)
        {
            return (false, $"Could not control service '{serviceName}': {ex.Message}. Make sure the app is running as Administrator.");
        }
        catch (Exception ex)
        {
            return (false, $"Restart failed: {ex.Message}");
        }
    }
}
