namespace Veterinar.E2E.Tests;

public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("E2E_RUN"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Selenium E2E tests are opt-in. Set E2E_RUN=true and run against a live frontend.";
        }
    }
}
