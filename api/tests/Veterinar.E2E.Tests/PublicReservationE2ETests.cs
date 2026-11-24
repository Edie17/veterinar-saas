using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Net.Http.Json;

namespace Veterinar.E2E.Tests;

public sealed class PublicReservationE2ETests(SeleniumFixture fixture) : IClassFixture<SeleniumFixture>
{
    private IWebDriver Driver => fixture.Driver;
    private WebDriverWait Wait => fixture.Wait;

    [E2EFact]
    public void HomePage_Renders_Core_Public_Experience()
    {
        Driver.Navigate().GoToUrl(fixture.BaseUrl);

        Wait.Until(driver => driver.FindElement(By.CssSelector(".hero h1")).Displayed);

        Assert.Contains("ordinace", Driver.FindElement(By.CssSelector(".hero h1")).Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(Driver.FindElement(By.CssSelector("form.reservationPanel")).Displayed);
        Assert.True(Driver.FindElement(By.CssSelector("footer.siteFooter")).Displayed);
    }

    [E2EFact]
    public void ReservationForm_Blocks_Script_Content_Before_Submit_To_Backend()
    {
        Driver.Navigate().GoToUrl($"{fixture.BaseUrl}/#kontakt");
        Wait.Until(driver => driver.FindElement(By.CssSelector("form.reservationPanel")).Displayed);
        SelectFirstAvailableSlotWithinTwoWeeks();

        FillTextInputByLabel("Vaše jméno", "Jan Novák");
        FillTextInputByLabel("Telefon", "+420 777 123 456");
        FillTextInputByLabel("E-mail", "jan.selenium@example.cz");
        FillTextInputByLabel("Jméno mazlíčka", "Bady");
        FillTextAreaByLabel("Zpráva", "<script>alert(1)</script>");
        Driver.FindElement(By.CssSelector(".reservationSubmit .button")).Click();

        var error = Wait.Until(driver => driver.FindElements(By.CssSelector(".fieldError"))
            .FirstOrDefault(element => element.Displayed && element.Text.Contains("Zpráva nesmí obsahovat HTML", StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(error);
        Assert.Empty(Driver.FindElements(By.CssSelector(".reservationSuccess")));
    }

    [E2EFact]
    public void ReservationForm_Creates_Reservation_For_First_Available_Slot()
    {
        Driver.Navigate().GoToUrl($"{fixture.BaseUrl}/#kontakt");
        Wait.Until(driver => driver.FindElement(By.CssSelector("form.reservationPanel")).Displayed);
        SelectFirstAvailableSlotWithinTwoWeeks();

        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        FillTextInputByLabel("Vaše jméno", $"Selenium Klient {suffix}");
        FillTextInputByLabel("Telefon", "+420 777 123 456");
        FillTextInputByLabel("E-mail", $"selenium-{suffix}@example.cz");
        FillTextInputByLabel("Jméno mazlíčka", $"Bady {suffix}");
        FillTextAreaByLabel("Zpráva", "Automatický E2E test rezervace.");
        Driver.FindElement(By.CssSelector(".reservationSubmit .button")).Click();

        var success = Wait.Until(driver => driver.FindElement(By.CssSelector(".reservationSuccess")));

        Assert.Contains("Rezervace #", success.Text);
        Assert.Contains("čeká na potvrzení", success.Text, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectFirstAvailableSlotWithinTwoWeeks()
    {
        Wait.Until(driver => driver.FindElements(By.CssSelector(".reasonOption")).Any(element => element.Enabled));
        var dateInput = Wait.Until(driver => driver.FindElement(By.CssSelector("input[type='date']")));
        var availableDate = FindNextAvailableDateViaApi();
        SetDateInputValue(dateInput, availableDate);

        var slot = Wait.Until<IWebElement?>(driver => driver.FindElements(By.CssSelector(".slotGrid .slot")).FirstOrDefault(element => element.Displayed && element.Enabled));
        if (slot is null) Assert.Fail("Available slot was found by API but not rendered in the reservation form.");
        ScrollIntoView(slot);
        slot.Click();
        Wait.Until(driver => driver.FindElement(By.CssSelector(".slot.selected")).Displayed);
    }

    private DateOnly FindNextAvailableDateViaApi()
    {
        using var http = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var offset = 0; offset <= 60; offset++)
        {
            var candidate = today.AddDays(offset);
            var slots = http.GetFromJsonAsync<List<ApiSlot>>($"/api/availability?date={candidate:yyyy-MM-dd}&durationMinutes=20")
                .GetAwaiter()
                .GetResult() ?? [];
            if (slots.Any(slot => slot.IsAvailable)) return candidate;
        }
        Assert.Fail("No available reservation slot was found within the next 60 days. Check API seed data and working hours.");
        return today;
    }

    private void SetDateInputValue(IWebElement input, DateOnly date)
    {
        ScrollIntoView(input);
        var value = date.ToString("yyyy-MM-dd");
        ((IJavaScriptExecutor)Driver).ExecuteScript(
            "Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(arguments[0], arguments[1]); arguments[0].dispatchEvent(new Event('input', { bubbles: true })); arguments[0].dispatchEvent(new Event('change', { bubbles: true }));",
            input,
            value);
    }

    private void FillTextInputByLabel(string labelText, string value)
    {
        var input = FindControlByLabelText(labelText, "input");
        ScrollIntoView(input);
        input.Clear();
        input.SendKeys(value);
    }

    private void FillTextAreaByLabel(string labelText, string value)
    {
        var textarea = FindControlByLabelText(labelText, "textarea");
        ScrollIntoView(textarea);
        textarea.Clear();
        textarea.SendKeys(value);
    }

    private IWebElement FindControlByLabelText(string labelText, string controlTag)
    {
        return Wait.Until(driver => driver.FindElement(By.XPath($"//label[contains(@class,'field')][.//span[normalize-space()='{labelText}']]//{controlTag}")));
    }

    private void ScrollIntoView(IWebElement element)
    {
        ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({ block: 'center', inline: 'nearest' });", element);
    }

    private sealed record ApiSlot(DateTimeOffset StartAt, DateTimeOffset EndAt, bool IsAvailable);
}
