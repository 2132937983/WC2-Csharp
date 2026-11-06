namespace WC2;

/// <summary>
/// Engine-level globals and free functions that the original libeasytech exported
/// (functions.h). Game-level globals (g_Scene, g_GameManager, ...) live in <see cref="GameGlobals"/>.
/// </summary>
public static class Globals
{
    // ------------------------------------------------------------------
    // Content scale / layout
    // ------------------------------------------------------------------

    /// <summary>2.0 = HD ("@2x" / "_hd") assets, 1.0 = standard assets.</summary>
    public static float g_contenscalefactor = 2.0f;

    /// <summary>Uniform text scale used by the intro style screens.</summary>
    public static float g_introTextScale = 0.7f;

    // ------------------------------------------------------------------
    // Paths
    // ------------------------------------------------------------------

    public static string LangDir = "";
    public static string DocumentFilePath = "";
    public static string APKFilePath = "";
    public static string strVersionName = "1.3.8";

    /// <summary>Where the base asset package was mounted from (folder or APK); informational only.</summary>
    public static string AssetsRoot = "";

    public static void SetLangDir(string langDir)
    {
        LangDir = langDir ?? "";
        AssetSystem.LanguageFolder = LangDir;
        // the package folder holding this language ("lang/cn"; "zh_CN.lproj" in a flat legacy folder)
        AssetSystem.LanguagePath = "";
        string settings = LangDir.Length > 0 ? AssetSystem.Resolve(LangDir + "/Localizable.strings") : null;
        int slash = settings?.LastIndexOf('/') ?? -1;
        AssetSystem.LanguagePath = slash > 0 ? settings.Substring(0, slash) : "";
    }
    public static void SetDocumentPath(string documentFilePath)
    {
        DocumentFilePath = documentFilePath ?? "";
        if (DocumentFilePath.Length > 0) Directory.CreateDirectory(DocumentFilePath);
    }
    public static void SetAPKFilePath(string apkFilePath) => APKFilePath = apkFilePath ?? "";

    /// <summary>
    /// Returns the asset path for a name, as the original GetPath(fileName, unused) did: the
    /// language folder variant first, then the name itself (see <see cref="AssetSystem.Resolve"/>).
    /// The result is a relative asset path, read through <see cref="AssetSystem"/>, ecFile or
    /// TiXmlDocument; a name that does not exist comes back normalised so loaders fail cleanly.
    /// The scenario export "game.xml" is redirected to the user data folder (the remake writes
    /// it to the Download folder) so it can be edited by hand without touching the assets.
    /// </summary>
    public static string GetPath(string fileName, string unused = null)
    {
        if (string.IsNullOrEmpty(fileName)) return fileName;
        if (Path.IsPathRooted(fileName)) return fileName;
        if (fileName == "game.xml" || fileName == "game.json") return GetDocumentPath(fileName);
        return AssetSystem.Resolve(fileName) ?? AssetSystem.Normalize(fileName);
    }

    public static string Get2XPath(string fileName, string unused = null) => GetPath(fileName, unused);

    /// <summary>Returns the full path of a writable user-data file (saves, settings).</summary>
    public static string GetDocumentPath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return fileName;
        if (Path.IsPathRooted(fileName)) return fileName;
        return Path.Combine(DocumentFilePath, fileName);
    }

    public static string GetEditorResPath(string unused = null) => AssetsRoot;

    // ------------------------------------------------------------------
    // Random numbers (rand() / drand48 style, two independent streams)
    // ------------------------------------------------------------------

    private static Random s_rand = new Random();
    private static Random s_aiRand = new Random();
    private static int s_randSeed;
    private static int s_aiRandSeed;

    public static void SetRandSeed(int seed) { s_randSeed = seed; s_rand = new Random(seed); }
    public static int GetRandSeed() => s_randSeed;
    /// <summary>Non-negative pseudo random integer, like C rand().</summary>
    public static int GetRand() => s_rand.Next();

    public static void SetAIRandSeed(int seed) { s_aiRandSeed = seed; s_aiRand = new Random(seed); }
    public static int GetAIRandSeed() => s_aiRandSeed;
    public static int GetAIRand() => s_aiRand.Next();

    /// <summary>
    /// Replay seed (<c>--seed N</c>). When set, every seed the game draws (lrand48, the battle seed)
    /// comes from one generator started at this value, so a run can be repeated exactly.
    /// </summary>
    public static int? SeedOverride { get; private set; }
    private static Random s_seedSource = new Random();

    public static void SetSeedOverride(int seed)
    {
        SeedOverride = seed;
        s_seedSource = new Random(seed);
    }

    /// <summary>lrand48(): non-negative long random, used for seeding.</summary>
    public static int lrand48() => SeedOverride.HasValue ? s_seedSource.Next() : Random.Shared.Next();

    /// <summary>Seed taken from the clock (srand(time(0))), or from the replay seed when one is set.</summary>
    public static int TimeSeed() => SeedOverride.HasValue ? s_seedSource.Next() : unchecked((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    // ------------------------------------------------------------------
    // Math helpers from functions.h
    // ------------------------------------------------------------------

    public static float InvSqrt(float x) => x <= 0f ? 0f : 1f / MathF.Sqrt(x);

    public static void ColorClamp(ref float color)
    {
        if (color < 0f) color = 0f;
        else if (color > 1f) color = 1f;
    }

    /// <summary>Wall clock in milliseconds (the original _getTime()).</summary>
    public static long _getTime() => Environment.TickCount64;

    // ------------------------------------------------------------------
    // Platform hooks that used to go through JNI. They are harmless no-ops on desktop.
    // ------------------------------------------------------------------

    public static Action OnExitRequested;

    public static void AppJavaExit() => OnExitRequested?.Invoke();
    public static void ecGameShutdown() => OnExitRequested?.Invoke();
    public static void ecApplicationTerminate() => OnExitRequested?.Invoke();
    public static void ecAppWillTerminate() { }
    public static void showWebsiteJNI(string url) { TryOpenUrl(url); }
    public static void ecLaunchAppStore(string url) { TryOpenUrl(url); }
    public static void ecShowReview() { }
    public static void ecMailServiceJNI() { }
    public static void GetDataFromServerJNI() { }
    public static void MainMenuLoadedJNI() { }
    public static void InAppPurchase(int productId) { }
    public static void ecInAppPurchase(int productId) { }
    public static void ecPurchasedProduct(int productId) { }
    public static string ecGetProductPrice(int productId) => "";
    public static void ecSetProductPrice(int productId, string price) { }
    public static bool isAndroidEmulator() => false;
    public static string getVersionName() => strVersionName;
    public static void ecSetIdleTimerDisabled(bool disabled) { }
    public static bool ecIsFullScreen() => s_fullScreen;
    public static void ecSetFullScreen(bool fullScreen) { s_fullScreen = fullScreen; OnFullScreenChanged?.Invoke(fullScreen); }
    private static bool s_fullScreen;
    public static Action<bool> OnFullScreenChanged;

    /// <summary>
    /// The original code looked Java methods up through JNI; on desktop nothing exists,
    /// so the lookup always fails and callers skip the Java-only behaviour.
    /// </summary>
    public static bool getStaticMethodInfo(MethodInfo result, string className, string methodName, string signature) => false;
    public static bool getMethodInfo_DefaultClassLoader(MethodInfo result, string className, string methodName, string signature) => false;

    private static void TryOpenUrl(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    // ------------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------------

    public static string LogFile;
    private static readonly object s_logLock = new object();

    public static void Log(string message)
    {
        try { Console.WriteLine(message); } catch { }
        System.Diagnostics.Debug.WriteLine(message);
        if (!string.IsNullOrEmpty(LogFile))
        {
            try { lock (s_logLock) File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss.fff ") + message + Environment.NewLine); } catch { }
        }
    }

    public static void LogError(string message) => Log("ERROR: " + message);

    /// <summary>printf replacement used by ported code.</summary>
    public static void printf(string format, params object[] args) => Log(Sprintf(format, args).TrimEnd('\n'));
}

/// <summary>JNI method lookup result; kept only so ported code compiles.</summary>
public sealed class MethodInfo
{
    public object env;
    public object clazz;
    public object methodId;
}
