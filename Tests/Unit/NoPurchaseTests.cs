using System.Reflection;
using System.Text.RegularExpressions;

namespace WC2.Tests;

/// <summary>
/// This build sells nothing: no in-app store, no purchase entry point, no price display, no
/// purchase hooks to a platform billing service. The original opened its medal store from the "+"
/// button of the medal counter (GUI event OTHER 13) and from the store panel GUIIap.
/// </summary>
public class NoPurchaseTests
{
    private static readonly Regex Forbidden = new(
        @"\b(GUIIap|InAppPurchase|ecInAppPurchase|ecPurchasedProduct|ecGetProductPrice|ecSetProductPrice|SetProductPrice|SetLocalePirce|BuyMedal|buymedalbutton|guiIapSort|freeGUIIap|com\.android\.vending\.BILLING|BillingClient|ProductDetails)\b",
        RegexOptions.CultureInvariant);

    [Fact]
    public void NoPurchaseCodeInTheSources()
    {
        var hits = new List<string>();
        foreach (var folder in new[] { "Engine", "Data", "Game", "Hosts" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Support.Repo, folder), "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                    file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
                if (!(file.EndsWith(".cs") || file.EndsWith(".xml") || file.EndsWith(".csproj"))) continue;
                int line = 0;
                foreach (var text in File.ReadLines(file))
                {
                    line++;
                    if (Forbidden.IsMatch(text)) hits.Add($"{Path.GetRelativePath(Support.Repo, file)}:{line}: {text.Trim()}");
                }
            }
        Assert.Empty(hits);
    }

    [Fact]
    public void TheApkAsksForNoBillingPermission()
    {
        string manifest = File.ReadAllText(Path.Combine(Support.Repo, "Hosts", "Android", "Properties", "AndroidManifest.xml"));
        Assert.DoesNotContain("BILLING", manifest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoPurchaseTypesOrMembersInTheAssemblies()
    {
        var assemblies = new[] { typeof(GUIElement).Assembly, typeof(DataPaths).Assembly, typeof(GUIMedal).Assembly };
        var names = assemblies.SelectMany(a => a.GetTypes())
            .SelectMany(t => new[] { t.Name }.Concat(t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => t.Name + "." + m.Name)))
            // (the AI's purChase*Card routines buy in-game cards with game money; they keep their original names)
            .Where(n => Regex.IsMatch(n, @"Iap\b|Iap\.|InAppPurchase|PurchasedProduct|ProductPrice|BuyMedal", RegexOptions.IgnoreCase))
            .ToList();
        Assert.Empty(names);
        // the medal counter keeps its count but has no buy button
        Assert.Null(typeof(GUIMedal).GetField("buttonAdd"));
    }
}
