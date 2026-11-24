using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Veterinar.E2E.Tests;

public sealed class SeleniumFixture : IDisposable
{
    public SeleniumFixture()
    {
        BaseUrl = (Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5173").TrimEnd('/');
        using var http = new HttpClient();
        var demo = http.GetFromJsonAsync<JsonElement>($"{BaseUrl}/api/dev/status").GetAwaiter().GetResult();
        if (!demo.GetProperty("demo").GetBoolean()) throw new InvalidOperationException("Run npm run qa against its isolated demo environment.");

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--window-size=1440,1200");
        options.SetLoggingPreference(LogType.Browser, LogLevel.All);

        Driver = new ChromeDriver(options);
        Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(12));
    }

    public string BaseUrl { get; }
    public IWebDriver Driver { get; }
    public WebDriverWait Wait { get; }

    public void Check(string scenario, Action action)
    {
        try
        {
            action();
            var logs = Driver.Manage().Logs.GetLog(LogType.Browser);
            Assert.DoesNotContain(logs, entry => Regex.IsMatch(entry.Message, @"Uncaught (?:TypeError|ReferenceError|SyntaxError|Error)"));
            Capture(scenario);
        }
        catch { try { Capture(scenario + "-FAILED"); } catch { /* Preserve original test failure. */ } throw; }
    }
    public void CheckAtWidth(string scenario, int width, Action action)
    {
        var chrome = (ChromeDriver)Driver;
        chrome.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride", new Dictionary<string, object> { ["width"] = width, ["height"] = 844, ["deviceScaleFactor"] = 1, ["mobile"] = true });
        try { Check(scenario, action); }
        finally { chrome.ExecuteCdpCommand("Emulation.clearDeviceMetricsOverride", new Dictionary<string, object>()); }
    }
    private void Capture(string scenario)
    {
        var directory = Environment.GetEnvironmentVariable("E2E_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var name = Regex.Replace(scenario, "[^a-zA-Z0-9_-]", "_");
        ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(Path.Combine(directory, name + ".png"));
        File.WriteAllText(Path.Combine(directory, name + ".html"), Driver.PageSource);
    }

    public void Dispose()
    {
        Driver.Quit();
        Driver.Dispose();
    }
}
