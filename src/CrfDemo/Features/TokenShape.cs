namespace CrfDemo.Features;

public static class TokenShape
{
    public static string Build(string token)
    {
        if (token.Length == 0)
            return "empty";

        var chars = token.Select(c =>
        {
            if (char.IsUpper(c)) return 'A';
            if (char.IsLower(c)) return 'a';
            if (char.IsDigit(c)) return '0';
            return c;
        });

        return new string(chars.ToArray());
    }
}
