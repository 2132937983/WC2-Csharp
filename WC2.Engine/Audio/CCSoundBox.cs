using Microsoft.Xna.Framework.Audio;
using NLayer;

namespace WC2;

/// <summary>
/// Sound effects (wav via SoundEffect) and background music. Music files are MP3; they are
/// decoded with the managed NLayer decoder into a looping SoundEffectInstance, which works the
/// same on desktop and Android.
/// </summary>
public sealed class CCSoundBox
{
    private static CCSoundBox s_instance;
    public static CCSoundBox GetInstance() => s_instance ??= new CCSoundBox();

    public int seVolume = 100;
    public int musicVolume = 100;

    private readonly Dictionary<string, SoundEffect> effects = new(StringComparer.OrdinalIgnoreCase);
    private bool audioAvailable = true;

    // music
    private string currentMusicName = "";
    private SoundEffect musicEffect;
    private SoundEffectInstance musicInstance;
    private bool musicLoop;
    private bool musicPlayRequested;
    private bool musicPaused;
    private System.Threading.Tasks.Task<SoundEffect> musicDecodeTask;
    private int musicGeneration;
    private readonly object musicLock = new();

    private CCSoundBox() { }

    public void InitSoundSystem() { }
    public void DestroySoundSystem() => Destroy();

    public void Destroy()
    {
        UnloadMusic();
        foreach (var e in effects.Values) e.Dispose();
        effects.Clear();
    }

    /// <summary>Called once per frame: starts music whose decoding has finished.</summary>
    public void UpdateSound()
    {
        var task = musicDecodeTask;
        if (task == null || !task.IsCompleted) return;
        int generation;
        lock (musicLock) { generation = musicGeneration; musicDecodeTask = null; }
        if (task.IsFaulted || task.Result == null)
        {
            Log("Music decode failed: " + currentMusicName + " " + task.Exception?.GetBaseException().Message);
            return;
        }
        Log($"Music decoded: {currentMusicName} ({task.Result.Duration.TotalSeconds:F1}s)");
        if (generation != musicGeneration) { task.Result.Dispose(); return; }
        musicEffect = task.Result;
        if (musicPlayRequested) StartMusicInstance();
    }

    // ---- sound effects ----------------------------------------------------------------

    public void LoadSE(string seName)
    {
        if (!audioAvailable || string.IsNullOrEmpty(seName) || effects.ContainsKey(seName)) return;
        byte[] data = AssetSystem.ReadAllBytes(seName);
        if (data == null) { Log("Sound missing: " + seName); return; }
        try
        {
            using var ms = new MemoryStream(data, writable: false);
            effects[seName] = SoundEffect.FromStream(ms);
        }
        catch (NoAudioHardwareException)
        {
            audioAvailable = false;
        }
        catch (Exception ex)
        {
            Log("LoadSE failed for " + seName + ": " + ex.Message);
        }
    }

    public void UnloadSE(string seName)
    {
        if (seName != null && effects.Remove(seName, out var e)) e.Dispose();
    }

    public void PlaySE(string seName)
    {
        if (!audioAvailable || string.IsNullOrEmpty(seName)) return;
        if (!effects.TryGetValue(seName, out var effect)) { LoadSE(seName); effects.TryGetValue(seName, out effect); }
        if (effect == null) return;
        try { effect.Play(Math.Clamp(seVolume / 100f, 0f, 1f), 0f, 0f); } catch { }
    }

    public void StopAllSE() { }

    public void SetSEVolume(int volume) => seVolume = Math.Clamp(volume, 0, 100);

    // ---- music -------------------------------------------------------------------------

    public void LoadMusic(string musicName, string unused = null)
    {
        if (!audioAvailable || string.IsNullOrEmpty(musicName)) return;
        if (currentMusicName == musicName && (musicEffect != null || musicDecodeTask != null)) return;
        UnloadMusic();
        string path = FindMusicFile(musicName);
        if (path == null) { Log("Music missing: " + musicName); return; }
        currentMusicName = musicName;
        int generation;
        lock (musicLock) { generation = ++musicGeneration; }
        musicDecodeTask = System.Threading.Tasks.Task.Run(() => DecodeMusic(path));
    }

    private static string FindMusicFile(string musicName)
    {
        return AssetSystem.Resolve(musicName)
            ?? AssetSystem.Resolve(WithExt(musicName, ".mp3"))
            ?? AssetSystem.Resolve(WithExt(musicName, ".wav"));
    }

    /// <summary>
    /// Decodes an MP3 file to 16-bit little endian PCM (used by the game and by the --test-audio
    /// self test). The game's music is MPEG 2.5 Layer III at 8 kHz, which NLayer mis-frames
    /// (half the samples, clipped), so MP3Sharp (a JavaLayer port) is the primary decoder and
    /// NLayer only a fallback.
    /// </summary>
    public static byte[] DecodeMp3ToPcm(string path, out int sampleRate, out int channels, out string info)
    {
        // a rooted path is a file on disk (debug tools), anything else an asset
        byte[] data = Path.IsPathRooted(path) ? File.ReadAllBytes(path) : AssetSystem.ReadAllBytes(path);
        if (data == null) throw new FileNotFoundException("music not found", path);
        try
        {
            return DecodeWithMp3Sharp(data, out sampleRate, out channels, out info);
        }
        catch (Exception ex)
        {
            Log("MP3Sharp failed for " + path + ": " + ex.Message + " (falling back to NLayer)");
            return DecodeWithNLayer(data, out sampleRate, out channels, out info);
        }
    }

    private static byte[] DecodeWithMp3Sharp(byte[] data, out int sampleRate, out int channels, out string info)
    {
        using var mp3 = new MP3Sharp.MP3Stream(new MemoryStream(data, writable: false));
        sampleRate = mp3.Frequency;
        // The PCM layout follows Format, not ChannelCount: mono streams are delivered as
        // duplicated stereo. Keep the left channel of those so the data is really mono.
        int pcmChannels = mp3.Format == MP3Sharp.SoundFormat.Pcm16BitMono ? 1 : 2;
        bool downmix = pcmChannels == 2 && mp3.ChannelCount == 1;
        channels = downmix ? 1 : pcmChannels;
        info = $"mp3sharp: rate={sampleRate} ch={mp3.ChannelCount} format={mp3.Format}";
        if (sampleRate <= 0 || channels <= 0) return Array.Empty<byte>();
        using var pcm = new MemoryStream();
        var buffer = new byte[16384];
        int read;
        while ((read = mp3.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (!downmix) { pcm.Write(buffer, 0, read); continue; }
            for (int i = 0; i + 3 < read; i += 4) pcm.Write(buffer, i, 2);
        }
        return pcm.ToArray();
    }

    private static byte[] DecodeWithNLayer(byte[] data, out int sampleRate, out int channels, out string info)
    {
        using var mpeg = new MpegFile(new MemoryStream(data, writable: false));
        channels = mpeg.Channels;
        sampleRate = mpeg.SampleRate;
        info = $"nlayer: rate={mpeg.SampleRate} ch={mpeg.Channels} duration={mpeg.Duration.TotalSeconds:F1}s";
        if (channels <= 0 || sampleRate <= 0) return Array.Empty<byte>();
        var floats = new float[sampleRate * channels];
        using var pcm = new MemoryStream();
        var bytes = new byte[floats.Length * 2];
        int read;
        while ((read = mpeg.ReadSamples(floats, 0, floats.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                float f = floats[i];
                if (f > 1f) f = 1f; else if (f < -1f) f = -1f;
                short s = (short)(f * 32767f);
                bytes[i * 2] = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            pcm.Write(bytes, 0, read * 2);
        }
        return pcm.ToArray();
    }

    /// <summary>Decodes an MP3 (or loads a WAV) into a SoundEffect. Runs on a worker thread.</summary>
    private static SoundEffect DecodeMusic(string path)
    {
        try
        {
            if (Ext(path) == ".wav")
            {
                using var fs = new MemoryStream(AssetSystem.ReadAllBytes(path) ?? Array.Empty<byte>(), writable: false);
                return SoundEffect.FromStream(fs);
            }
            var pcm = DecodeMp3ToPcm(path, out int sampleRate, out int channels, out _);
            if (pcm.Length == 0) return null;
            return new SoundEffect(pcm, sampleRate, channels >= 2 ? AudioChannels.Stereo : AudioChannels.Mono);
        }
        catch (Exception ex)
        {
            Log("DecodeMusic failed for " + path + ": " + ex.Message);
            return null;
        }
    }

    private void StartMusicInstance()
    {
        if (musicEffect == null) return;
        try
        {
            musicInstance?.Stop();
            musicInstance?.Dispose();
            musicInstance = musicEffect.CreateInstance();
            musicInstance.IsLooped = musicLoop;
            musicInstance.Volume = Math.Clamp(musicVolume / 100f, 0f, 1f);
            musicInstance.Play();
            musicPaused = false;
            Log($"Music playing: {currentMusicName} ({musicEffect.Duration.TotalSeconds:F1}s, loop={musicLoop}, volume={musicVolume})");
        }
        catch (Exception ex)
        {
            Log("PlayMusic failed: " + ex.Message);
        }
    }

    public void UnloadMusic()
    {
        StopMusic();
        lock (musicLock) { musicGeneration++; musicDecodeTask = null; }
        musicInstance?.Dispose();
        musicInstance = null;
        musicEffect?.Dispose();
        musicEffect = null;
        currentMusicName = "";
        musicPlayRequested = false;
    }

    public void PlayMusic(bool loop)
    {
        if (!audioAvailable) return;
        musicLoop = loop;
        musicPlayRequested = true;
        if (musicEffect != null) StartMusicInstance();
    }

    public void StopMusic()
    {
        musicPlayRequested = false;
        try { musicInstance?.Stop(); } catch { }
    }

    public void PauseMusic()
    {
        try
        {
            if (musicInstance != null && musicInstance.State == SoundState.Playing) { musicInstance.Pause(); musicPaused = true; }
        }
        catch { }
    }

    public void ResumeMusic()
    {
        try
        {
            if (musicInstance != null && musicPaused) { musicInstance.Resume(); musicPaused = false; }
            else if (musicPlayRequested && musicEffect != null && (musicInstance == null || musicInstance.State == SoundState.Stopped)) StartMusicInstance();
        }
        catch { }
    }

    public void SetMusicVolume(int volume)
    {
        musicVolume = Math.Clamp(volume, 0, 100);
        try { if (musicInstance != null) musicInstance.Volume = musicVolume / 100f; } catch { }
    }
}

public enum SND_EFFECT
{
    DRAFT_WAV, MOVE_WAV, OCCUPY_WAV, CANNON_WAV, ROCKET_WAV, FIRE_WAV, EXP_WAV, STRIKE_WAV,
    SELECT_WAV, CANCEL_WAV, SHIP_WAV, CELEBRATE_WAV, AFTER_WAR_WAV, BUFF_WAV, BUILD_WAV,
    CARD_INTERFACE_WAV, GAS_WAV, LV_UP_WAV, MACHINE_GUN_WAV, NAVAL_GUN_WAV, POP_WAV, SUPPLY_WAV
}

/// <summary>Preloaded in-game sound effects addressed by the SND_EFFECT enum.</summary>
public sealed class CSoundRes
{
    public static readonly string[] FileNames =
    {
        "draft.wav", "move.wav", "occupy.wav", "cannon.wav", "rocket.wav", "fire.wav", "exp.wav", "strike.wav",
        "select.wav", "cancel.wav", "ship.wav", "celebrate.wav", "after_war.wav", "buff.wav", "build.wav",
        "card_interface.wav", "gas.wav", "lvup.wav", "machine_gun.wav", "naval_gun.wav", "pop.wav", "supply.wav",
    };

    public bool loaded;

    public void Load()
    {
        var box = CCSoundBox.GetInstance();
        foreach (var name in FileNames) box.LoadSE(name);
        loaded = true;
    }

    public void Unload()
    {
        loaded = false;
    }

    public void PlayCharSE(SND_EFFECT se)
    {
        int i = (int)se;
        if (i < 0 || i >= FileNames.Length) return;
        CCSoundBox.GetInstance().PlaySE(FileNames[i]);
    }
}
