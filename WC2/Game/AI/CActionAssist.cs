namespace WC2;

/// <summary>
/// AI helpers: path search, purchase decisions, target evaluation (CActionAssist.h).
/// Methods are split over CActionAssistUnits.cs, CActionAssistTools.cs and CActionAssistBuyCard.cs.
/// </summary>
public sealed partial class CActionAssist
{
    private static CActionAssist _instance;
    public static CActionAssist Instance() => _instance ??= new CActionAssist();

    public struct SearchNode
    {
        public int areaId;
        public int previous;
    }

    public int searchHead;
    public int searchTail;
    public bool[] searchNodeVisited = new bool[1950];
    public SearchNode[] searchQueue = new SearchNode[512];
    public int[] getNeighbourId = new int[50];
    public int totalSeaAreaCount;
    public int targetNodeType;
    public ActionNode actionNode = new();
    public List<NODE> actionToNextAreaTargetIdList = new();
    public NODE targetNode;

    public CActionAssist()
    {
        actionToNextAreaTargetIdList.Clear();
    }
}
