using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Veterinar.E2E.Tests;

public sealed class MobileWebsiteTests(SeleniumFixture fixture) : IClassFixture<SeleniumFixture>
{
    private IWebDriver Driver => fixture.Driver;
    private IWebElement Find(string selector) => fixture.Wait.Until(driver => driver.FindElement(By.CssSelector(selector)));
    private void Click(IWebElement element)
    {
        ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element);
        element.Click();
    }
    private void FitsViewport() => Assert.True((bool)((IJavaScriptExecutor)Driver).ExecuteScript(
        "return document.documentElement.scrollWidth <= window.innerWidth + 1 && [...document.querySelectorAll('main, .topbar, .siteFooter')].every(el => el.getBoundingClientRect().right <= window.innerWidth + 1);"));

    [E2EFact]
    public void Public_Pages_Navigation_And_Booking_Work_At_Three_Mobile_Widths()
    {
        var chrome = (ChromeDriver)Driver;
        try
        {
            foreach (var width in new[] { 320, 390, 768 })
            {
                chrome.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride", new Dictionary<string, object> { ["width"] = width, ["height"] = 844, ["deviceScaleFactor"] = 1, ["mobile"] = true });
                fixture.Check($"mobile-{width}-home", () =>
                {
                    Driver.Navigate().GoToUrl(fixture.BaseUrl);
                    Find(".hero h1");
                    var consent = Driver.FindElements(By.XPath("//button[normalize-space()='Pouze nezbytné']")).FirstOrDefault();
                    if (consent is not null) Click(consent);
                    Assert.Equal((long)width, (long)((IJavaScriptExecutor)Driver).ExecuteScript("return window.innerWidth;"));
                    var menu = Find(".publicMenuToggle");
                    Assert.True(menu.Displayed); Assert.Equal("false", menu.GetDomAttribute("aria-expanded"));
                    Assert.False(Find(".publicNav").Displayed);
                    Assert.True(Convert.ToDouble(((IJavaScriptExecutor)Driver).ExecuteScript("return document.querySelector('.topbar').getBoundingClientRect().height;")) < 120);
                    FitsViewport();
                });
                fixture.Check($"mobile-{width}-menu", () =>
                {
                    Click(Find(".publicMenuToggle"));
                    Assert.Equal("true", Find(".publicMenuToggle").GetDomAttribute("aria-expanded"));
                    Assert.True(Find(".publicNavBooking").Displayed);
                    FitsViewport();
                });
                fixture.Check($"mobile-{width}-booking", () =>
                {
                    Click(Find(".publicNavBooking"));
                    fixture.Wait.Until(driver => driver.FindElement(By.CssSelector(".publicMenuToggle")).GetDomAttribute("aria-expanded") == "false");
                    fixture.Wait.Until(driver => !driver.FindElement(By.CssSelector(".mobileBookingActions")).Displayed);
                    Click(Find("form .reservationStep .button"));
                    Find(".slot.selected");
                    Assert.True((bool)((IJavaScriptExecutor)Driver).ExecuteScript("return [...document.querySelectorAll('.reservationFields input, .reservationFields select')].every(el => parseFloat(getComputedStyle(el).fontSize) >= 16);"));
                    Assert.True((bool)((IJavaScriptExecutor)Driver).ExecuteScript("return [...document.querySelectorAll('.slot, .reasonOption')].every(el => el.getBoundingClientRect().height >= 44);"));
                    Assert.True((bool)((IJavaScriptExecutor)Driver).ExecuteScript("const form = document.querySelector('form.reservationPanel'); return form.getBoundingClientRect().left >= 0 && form.getBoundingClientRect().right <= window.innerWidth;"));
                    FitsViewport();
                });
                fixture.Check($"mobile-{width}-article", () =>
                {
                    Driver.Navigate().GoToUrl(fixture.BaseUrl + "/clanky");
                    Click(Find(".blogCard h3 a"));
                    Find(".articleContent");
                    // Fixture content probes wrapping without changing the demo database.
                    ((IJavaScriptExecutor)Driver).ExecuteScript("document.querySelector('.articleDetailHero h1').textContent='Delší nadpis článku o přípravě mazlíčka na první návštěvu ordinace'; document.querySelector('.articleContent').insertAdjacentHTML('beforeend', '<p>' + 'dlouhysouvislytext'.repeat(12) + '</p><table tabindex=\"0\" aria-label=\"Testovací tabulka\"><tr><th style=\"min-width:600px\">Široká tabulka</th><th>Další sloupec</th></tr></table>');");
                    FitsViewport();
                    Assert.True((bool)((IJavaScriptExecutor)Driver).ExecuteScript("const table = document.querySelector('.articleContent table'); return table.getBoundingClientRect().right <= window.innerWidth + 1 && getComputedStyle(table).overflowX === 'auto';"));
                    Assert.True(Find(".mobileBookingActions").Displayed);
                });
            }
        }
        finally
        {
            chrome.ExecuteCdpCommand("Emulation.clearDeviceMetricsOverride", new Dictionary<string, object>());
            Driver.Manage().Window.Size = new System.Drawing.Size(1440, 1200);
        }
    }
}
