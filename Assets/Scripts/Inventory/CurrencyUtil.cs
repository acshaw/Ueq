using System.Text;

public static class CurrencyUtil
{
    public static string Format(int copper)
    {
        if (copper <= 0) return "free";
        var sb = new StringBuilder();
        int pp = copper / 1000; copper %= 1000;
        int gp = copper / 100;  copper %= 100;
        int sp = copper / 10;   copper %= 10;
        if (pp > 0) { sb.Append(pp); sb.Append("pp "); }
        if (gp > 0) { sb.Append(gp); sb.Append("gp "); }
        if (sp > 0) { sb.Append(sp); sb.Append("sp "); }
        if (copper > 0) { sb.Append(copper); sb.Append("cp"); }
        return sb.ToString().Trim();
    }
}
