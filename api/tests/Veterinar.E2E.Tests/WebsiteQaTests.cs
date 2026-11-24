using System.Net.Http.Json;
using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Veterinar.E2E.Tests;

public sealed class WebsiteQaTests(SeleniumFixture fixture) : IClassFixture<SeleniumFixture>
{
    private IWebDriver Driver => fixture.Driver;
    private WebDriverWait Wait => fixture.Wait;
    private readonly HttpClient http = new() { BaseAddress = new Uri(fixture.BaseUrl) };
    private static string Suffix() => Guid.NewGuid().ToString("N")[..10];
    private void Go(string path) => Driver.Navigate().GoToUrl(fixture.BaseUrl + path);
    private IWebElement Find(By selector) => Wait.Until(driver => driver.FindElement(selector));
    private void Click(IWebElement element) { ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element); element.Click(); }
    private void Button(string text) => Click(Find(By.XPath($"//button[normalize-space()='{text}']")));
    private void Fill(string label, string value, string tag = "input")
    {
        var control = Find(By.XPath($"//label[.//span[normalize-space()='{label}']]//{tag}"));
        control.Clear(); control.SendKeys(value);
    }
    private void SetValue(IWebElement input, string value) => ((IJavaScriptExecutor)Driver).ExecuteScript(
        "Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(arguments[0],arguments[1]); arguments[0].dispatchEvent(new Event('input',{bubbles:true})); arguments[0].dispatchEvent(new Event('change',{bubbles:true}));", input, value);
    private void Nav(string text)
    {
        Click(Find(By.XPath($"//nav[contains(@class,'cmsNav')]//button[.//strong[normalize-space()='{text}']]")));
        Wait.Until(driver => driver.FindElements(By.CssSelector("fieldset[disabled]")).Count == 0);
    }
    private void Login()
    {
        Driver.Manage().Cookies.DeleteAllCookies(); Go("/sprava");
        Fill("E-mail", Environment.GetEnvironmentVariable("E2E_ADMIN_EMAIL") ?? "admin@example.cz");
        Fill("Heslo", Environment.GetEnvironmentVariable("E2E_ADMIN_PASSWORD") ?? "ChangeMe123!");
        Button("Přihlásit do správy"); Find(By.CssSelector(".cmsSidebar"));
    }
    private void Message(string text) => Wait.Until(driver => driver.FindElements(By.CssSelector("[role='status']")).Any(x => x.Text.Contains(text)));
    private JsonElement Site() => http.GetFromJsonAsync<JsonElement>("/api/site").GetAwaiter().GetResult();
    private void PublicPage(string path, Action check)
    {
        var original = Driver.CurrentWindowHandle;
        Driver.SwitchTo().NewWindow(WindowType.Tab); Go(path);
        try { check(); } finally { Driver.Close(); Driver.SwitchTo().Window(original); }
    }
    private void DismissCookies()
    {
        var button = Driver.FindElements(By.XPath("//button[normalize-space()='Pouze nezbytné']")).FirstOrDefault();
        if (button is not null) Click(button);
    }

    [E2EFact]
    public void Client_Can_Find_Nearest_Confirm_Move_Export_Calendar_And_Cancel() => fixture.CheckAtWidth("client-booking-management", 390, () =>
    {
        Go("/"); DismissCookies();
        Wait.Until(driver => driver.FindElements(By.CssSelector(".reasonOption")).Any(x => x.Enabled));
        Button("Najít nejbližší termín");
        Find(By.CssSelector(".slot.selected"));
        var email = "self-service-" + Suffix() + "@example.test";
        Fill("Vaše jméno", "QA Samoobsluha"); Fill("Telefon", "+420777123456"); Fill("E-mail", email); Fill("Jméno mazlíčka", "QA Nejbližší Bady");
        Button("Rezervovat termín"); Find(By.CssSelector(".reservationSuccess"));
        var messages = http.GetFromJsonAsync<JsonElement[]>("/api/dev/mailbox").GetAwaiter().GetResult()!;
        var message = messages.Single(x => x.GetProperty("email").GetString() == email);
        Driver.Navigate().GoToUrl(message.GetProperty("confirmationUrl").GetString()!);
        Button("Potvrdit rezervaci"); Message("Rezervace je potvrzena");
        Driver.Navigate().GoToUrl(message.GetProperty("managementUrl").GetString()!);
        Find(By.CssSelector(".managementSummary")); Assert.DoesNotContain("token=", Driver.Url);
        Assert.Contains("QA Nejbližší Bady", Find(By.CssSelector(".managementSummary")).Text);
        var original = Find(By.CssSelector(".managementSummary strong")).Text;
        Click(Wait.Until(driver => driver.FindElements(By.CssSelector(".managementFields .slot")).FirstOrDefault(x => x.Enabled && x.Displayed))!);
        Button("Potvrdit přesun termínu"); Message("Termín byl přesunut");
        Assert.NotEqual(original, Find(By.CssSelector(".managementSummary strong")).Text);
        Button("Přidat do kalendáře");
        // The export is local; the management link remains private and the visit stays confirmed.
        Assert.Contains("Potvrzeno", Find(By.CssSelector(".managementSummary")).Text);
        Button("Zrušit rezervaci"); Button("Ponechat rezervaci");
        Find(By.CssSelector(".managementSummary"));
        Button("Zrušit rezervaci"); Button("Ano, zrušit rezervaci"); Message("Rezervace byla zrušena");
        Assert.Empty(Driver.FindElements(By.CssSelector(".managementSummary")));
        Driver.Navigate().GoToUrl(message.GetProperty("managementUrl").GetString()!);
        Message("Odkaz pro správu rezervace je neplatný");
    });

    [E2EFact]
    public void Public_Navigation_Articles_And_Mobile_Layout() => fixture.Check("public-mobile-articles", () =>
    {
        Go("/"); Find(By.CssSelector(".hero h1")); DismissCookies();
        Click(Find(By.CssSelector("a[href='#kontakt']")));
        Assert.True(Find(By.CssSelector("form.reservationPanel")).Displayed);
        Go("/clanky"); Click(Find(By.CssSelector(".blogCard h3 a")));
        Assert.Contains("Před návštěvou", Find(By.CssSelector(".articleContent")).Text);
        Go("/clanky/neexistujici-qa-clanek"); fixture.Wait.Until(driver => driver.FindElements(By.CssSelector("main h1")).Any(heading => heading.Text.Contains("Článek nebyl nalezen")));
        Driver.Manage().Window.Size = new System.Drawing.Size(390, 844); Go("/");
        Find(By.CssSelector(".hero h1"));
        Assert.True(Find(By.CssSelector(".mobileBookingActions a[href='/#kontakt']")).Displayed);
        Click(Find(By.CssSelector(".mobileBookingActions a[href='/#kontakt']")));
        Wait.Until(driver => !driver.FindElement(By.CssSelector(".mobileBookingActions")).Displayed);
        Find(By.CssSelector("form.reservationPanel"));
        Assert.True((bool)((IJavaScriptExecutor)Driver).ExecuteScript("return document.documentElement.scrollWidth <= window.innerWidth + 1;"));
        Driver.Manage().Window.Size = new System.Drawing.Size(1440, 1200);
    });

    [E2EFact]
    public void Reservation_Email_Confirmation_Admin_Calendar_And_Cancellation() => fixture.Check("booking-confirm-cancel", () =>
    {
        Go("/"); DismissCookies();
        Wait.Until(driver => driver.FindElements(By.CssSelector(".reasonOption")).Any(x => x.Enabled));
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Prague").DateTime).AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        SetValue(Find(By.CssSelector("input[type='date']")), date.ToString("yyyy-MM-dd"));
        Click(Wait.Until(driver => driver.FindElements(By.CssSelector(".slotGrid .slot")).FirstOrDefault(x => x.Displayed && x.Enabled))!);
        Find(By.CssSelector(".slot.selected"));
        var name = "QA Klient " + Suffix(); var email = "qa-" + Suffix() + "@example.test";
        Fill("Vaše jméno", name); Fill("Telefon", "+420 777 123 456"); Fill("E-mail", email); Fill("Jméno mazlíčka", "QA Bady");
        Button("Rezervovat termín"); Find(By.CssSelector(".reservationSuccess"));
        var messages = http.GetFromJsonAsync<JsonElement[]>("/api/dev/mailbox").GetAwaiter().GetResult()!;
        var link = messages.Single(x => x.GetProperty("email").GetString() == email).GetProperty("confirmationUrl").GetString()!;
        Driver.Navigate().GoToUrl(link); Button("Potvrdit rezervaci"); Message("Rezervace je potvrzena");
        Assert.DoesNotContain("token=", Driver.Url);
        Login(); Nav("Objednávky");
        SetValue(Find(By.CssSelector("input[aria-label='Vybrat týden v kalendáři']")), date.ToString("yyyy-MM-dd"));
        var card = Find(By.XPath($"//article[contains(@class,'cmsCalendarAppointment')][contains(.,'{name}')]/parent::div"));
        Assert.Contains("Potvrzeno", card.Text); Click(card.FindElement(By.TagName("button")));
        Message("Rezervace byla zrušena");
        Assert.Empty(Driver.FindElements(By.XPath($"//article[contains(@class,'cmsCalendarAppointment')][contains(.,'{name}')]")));
        Assert.Contains("Zrušeno nebo vypršelo", Find(By.XPath($"//article[contains(@class,'cmsAppointmentRow')][contains(.,'{name}')]")).Text);
        Button("Odhlásit"); Find(By.CssSelector(".cmsLoginCard")); Go("/sprava"); Find(By.CssSelector(".cmsLoginCard"));
    });

    [E2EFact]
    public void Admin_Content_Service_And_Hours_Are_Saved_And_Visible_Publicly() => fixture.Check("admin-content-settings-hours", () =>
    {
        Login(); Nav("Obsah webu");
        var title = "QA ordinace " + Suffix();
        Fill("Hlavní nadpis", title); Button("Uložit a publikovat"); Message("Obsah webu byl uložen");
        PublicPage("/", () => Wait.Until(_ => Find(By.CssSelector(".hero h1")).Text == title));
        Nav("Nastavení");
        var service = "QA služba " + Suffix(); Fill("Název důvodu", service); Button("Uložit"); Message("Důvod návštěvy byl uložen");
        Assert.Contains(http.GetFromJsonAsync<JsonElement[]>("/api/appointment-types").GetAwaiter().GetResult()!, x => x.GetProperty("title").GetString() == service);
        var row = Find(By.XPath($"//article[contains(@class,'cmsEventRow')][.//strong[normalize-space()='{service}']]"));
        Click(row.FindElement(By.XPath(".//button[normalize-space()='Upravit']")));
        Click(Find(By.XPath("//label[.//span[normalize-space()='Zobrazit ve formuláři']]//input"))); Button("Uložit");
        Wait.Until(_ => !http.GetFromJsonAsync<JsonElement[]>("/api/appointment-types").GetAwaiter().GetResult()!.Any(x => x.GetProperty("title").GetString() == service));
        row = Find(By.XPath($"//article[contains(@class,'cmsEventRow')][.//strong[normalize-space()='{service}']]"));
        Click(row.FindElement(By.XPath(".//button[normalize-space()='Smazat']"))); Message("Důvod návštěvy byl smazán");
        Nav("Ordinační hodiny"); Button("Uložit ordinační hodiny"); Message("Ordinační hodiny byly uloženy");
        var eventName = "QA výjimka " + Suffix(); Fill("Název eventu", eventName); Button("Uložit event"); Message("Event provozní doby byl uložen");
        var eventRow = Find(By.XPath($"//article[contains(@class,'cmsEventRow')][.//strong[normalize-space()='{eventName}']]"));
        Click(eventRow.FindElement(By.XPath(".//button[normalize-space()='Smazat']")));
        Wait.Until(driver => !driver.FindElements(By.CssSelector(".cmsEventRow")).Any(x => x.Text.Contains(eventName)));
    });

    [E2EFact]
    public void Admin_Manual_Booking_Uses_Availability_And_Appears_In_Calendar() => fixture.Check("admin-manual-booking", () =>
    {
        Login(); Nav("Objednávky");
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Prague").DateTime).AddDays(2);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        SetValue(Find(By.XPath("//label[.//span[normalize-space()='Datum']]//input")), date.ToString("yyyy-MM-dd"));
        var name = "QA Telefon " + Suffix();
        Fill("Jméno klienta", name); Fill("Telefon", "+420 777 123 456"); Fill("E-mail", "manual-" + Suffix() + "@example.test"); Fill("Jméno pacienta", "QA Míca");
        Click(Wait.Until(driver => driver.FindElements(By.CssSelector(".cmsManualBooking .slot")).FirstOrDefault(x => x.Displayed && x.Enabled))!);
        Find(By.CssSelector(".cmsManualBooking .slot.selected")); Button("Uložit do kalendáře"); Message("Pacient byl ručně objednaný");
        SetValue(Find(By.CssSelector("input[aria-label='Vybrat týden v kalendáři']")), date.ToString("yyyy-MM-dd"));
        var card = Find(By.XPath($"//article[contains(@class,'cmsCalendarAppointment')][contains(.,'{name}')]/parent::div"));
        Assert.Contains("Potvrzeno", card.Text); Click(card.FindElement(By.TagName("button"))); Message("Rezervace byla zrušena");
    });

    [E2EFact]
    public void Admin_Article_Editor_Publish_And_Delete() => fixture.Check("admin-article-editor", () =>
    {
        Login(); Nav("Články");
        var section = "QA sekce " + Suffix(); var article = "QA článek " + Suffix();
        Fill("Název sekce", section); Click(Find(By.CssSelector(".cmsSectionEditorCard button:not([type='button'])"))); Message("Sekce článků byla uložena");
        new SelectElement(Find(By.XPath("//label[.//span[normalize-space()='Sekce']]//select"))).SelectByText(section);
        Fill("Název článku", article);
        var iframe = Find(By.CssSelector(".tox-edit-area iframe")); Driver.SwitchTo().Frame(iframe);
        var body = fixture.Wait.Until(driver => driver.FindElements(By.TagName("body")).FirstOrDefault(element => element.GetDomAttribute("contenteditable") == "true"))!;
        body.Click(); body.SendKeys("Automatický QA obsah článku.");
        fixture.Wait.Until(_ => body.Text.Contains("Automatický QA obsah článku."));
        Driver.SwitchTo().DefaultContent();
        Click(Find(By.CssSelector(".cmsArticleEditorCard button:not([type='button'])"))); Message("Článek byl uložen");
        var published = Site().GetProperty("articleSections").EnumerateArray().SelectMany(x => x.GetProperty("articles").EnumerateArray()).Single(x => x.GetProperty("title").GetString() == article);
        PublicPage("/clanky/" + published.GetProperty("slug").GetString(), () => Assert.Contains("Automatický QA obsah", Find(By.CssSelector(".articleContent")).Text));
        var row = Find(By.XPath($"//div[contains(@class,'cmsArticleRow')][.//strong[normalize-space()='{article}']]"));
        Click(row.FindElement(By.XPath(".//button[normalize-space()='Smazat']"))); Message("Článek byl smazán");
        Assert.DoesNotContain(Site().GetProperty("articleSections").EnumerateArray().SelectMany(x => x.GetProperty("articles").EnumerateArray()), x => x.GetProperty("title").GetString() == article);
        var sectionRow = Find(By.XPath($"//article[contains(@class,'cmsArticleSection')][.//strong[normalize-space()='{section}']]"));
        Click(sectionRow.FindElement(By.XPath(".//div[contains(@class,'cmsArticleSectionTitle')]//button[normalize-space()='Smazat']"))); Message("Sekce byla smazána");
    });
}
