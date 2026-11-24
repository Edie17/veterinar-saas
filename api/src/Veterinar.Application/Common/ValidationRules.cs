namespace Veterinar.Application.Common;

public static class ValidationRules
{
    public static void Check(Action validate)
    {
        try { validate(); }
        catch (InvalidOperationException) { throw new RequestException("Zkontrolujte název, časy a délku návštěvy."); }
    }
}
