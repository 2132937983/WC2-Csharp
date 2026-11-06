namespace WC2;

/// <summary>Multiplayer peer; networking is not part of the desktop port, kept as data only.</summary>
public sealed class CPlayer
{
    public int lastPacketIndex;
    public bool hasPacket;
    public string playerName = "";
    public string deviceName = "";
    public int uuid;
    public int connected;
    public bool local;
    public bool host;
    public bool loadFinished;
    public string commanderName = "";
    public CommanderData commanderData;
    public int side;
    public int index;

    public void Init(bool localIn, string playerNameIn) { local = localIn; playerName = playerNameIn ?? ""; }
    public void SendPacket(PACKET_ID id, object data, int size) { }
    public void PushPacket(PACKET_ID id, object data, int size) { }
    public void SendFirst() { }
    public void ClearPacketList() { }
    public void SendAck(int idx) { }
    public void RecvAck(int idx) { }
}

/// <summary>Multiplayer session manager; every operation is a no-op on desktop.</summary>
public sealed class CPlayerManager
{
    public CPlayer playerLocal = new CPlayer { local = true, playerName = "Player", deviceName = "Desktop" };
    public CPlayer hostPlayer;
    public List<CPlayer> listPlayerRemote = new();
    public Corps[] leftCorps = new Corps[2];
    public Corps[] rightCorps = new Corps[2];
    public int corpsCount;
    public bool hasCorps;
    public bool matchRequested;
    public int matchStarted;
    public List<CountryAction> dequeCountryAction = new();

    public void Init() { }
    public void SetupSession(bool matchRequestedIn) { matchRequested = matchRequestedIn; }
    public void DestroySession() { matchRequested = false; }
    public void InitCorps(int levelId) { }
    public void StartMatch() { }
    public void StopMatch() { }
    public void SendStartLoadGame() { }
    public void SendPacketToAllPlayers(PACKET_ID id, object data, int size) { }
    public void SendPacketToAllPlayers(object data, int size) { }
    public void SetPlayerOfCountry(CCountry country, int index) { }
    public void SetPlayerOfCountry(CCountry country) { }
    public CPlayer AddPlayer(bool local, string playerName) { var p = new CPlayer(); p.Init(local, playerName); return p; }
    public CPlayer FindPlayer(string playerName) => null;
    public void Connect(string hostPlayerName, int peerNameI2) { }
    public void CancelConnect() { }
}
