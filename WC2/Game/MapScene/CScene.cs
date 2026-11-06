namespace WC2;

/// <summary>
/// The map: areas, adjacency, camera, selection and rendering (CScene.cpp).
/// The other parts of the class live in CSceneAdjacency.cs, CSceneData.cs, CSceneBomber.cs,
/// CSceneSelection.cs, CSceneCamera.cs, CSceneRender.cs and MapExtension.cs.
/// </summary>
public sealed partial class CScene
{
    public List<CArea> areaList = new();
    public List<RenderAreaEntry> renderAreaList = new();
    public CArea areaSelected;
    public float flashTimer;
    public float flashSpeed;
    public int[] fightFlashAreaId = { -1, -1 };
    public AreaData[] areaData;
    public ecTextureRes areaTextureRes = new();
    public ecImage[] zoneImage;
    public int areaCount;
    public CAreaMark areaMark = new();
    public CBackground background;
    public CCamera camera = new();
    public CBomber bomber;
    public List<CMedal> medalList = new();
    public float verticalArrowTimer;
    public float verticalArrowSpeed;
    public float arrowTimer;
    public float arrowSpeed;
    public int[] adjoinRaw;
    public int[] adjoin;
    public float[] topLeftPos = new float[2];
    public float[] size = new float[2];
    public bool newRoundFlashing;
    public CombatSpatialIndex combatSpatialIndex = new();
    // Cache of the areas inside the current camera view, shared by every render layer.
    public List<RenderAreaEntry> visibleRenderAreaList = new();
    public float[] visibleCameraCenter = new float[2];
    public float visibleCameraScale;
    public bool visibleRenderAreaListValid;

    /// <summary>
    /// Constructs the map scene and initialises its dynamic resource references and animation timers.
    /// Containers, the texture resource, the area mark and the camera are constructed by their initialisers.
    /// </summary>
    public CScene()
    {
        background = null;
        bomber = null;
        areaData = null;
        areaCount = 0;
        adjoinRaw = null;
        adjoin = null;
        verticalArrowTimer = 0.0f;
        verticalArrowSpeed = -100.0f;
        arrowTimer = 0.2f;
        arrowSpeed = 0.9f;
        visibleCameraCenter[0] = 0.0f;
        visibleCameraCenter[1] = 0.0f;
        visibleCameraScale = 0.0f;
        visibleRenderAreaListValid = false;
    }

    /// <summary>Releases the dynamic resources held by the scene.</summary>
    public void Release()
    {
        medalList.Clear();

        if (bomber != null) bomber.Release();
        bomber = null;

        if (background != null) background.Release();
        background = null;

        if (zoneImage != null)
        {
            for (int areaId = 0; areaId < zoneImage.Length; ++areaId)
                zoneImage[areaId] = null;
            zoneImage = null;
        }

        areaData = null;
        areaCount = 0;
        visibleRenderAreaList.Clear();
        visibleRenderAreaListValid = false;

        // Rebuild (empty) the index held by CScene so the next scene cannot reuse stale data.
        BuildCombatSpatialIndex();

        areaMark.Release();
        areaTextureRes.Release();
        ClearAreas();
    }

    /// <summary>Updates the camera, the animations and every area.</summary>
    /// <param name="deltaTimeS">Seconds elapsed since the previous frame.</param>
    public void Update(float deltaTimeS)
    {
        camera.Upate(deltaTimeS);

        if (bomber != null)
            bomber.Update(deltaTimeS);

        for (int medalIndex = 0; medalIndex < medalList.Count;)
        {
            CMedal medal = medalList[medalIndex];
            if (medal == null || medal.animationCompleted)
            {
                medalList.RemoveAt(medalIndex);
            }
            else
            {
                medal.Upate(deltaTimeS);
                ++medalIndex;
            }
        }

        UpdateFlashTimer(deltaTimeS);

        verticalArrowTimer += deltaTimeS * verticalArrowSpeed;
        if (verticalArrowTimer < -20.0f)
        {
            verticalArrowTimer = -20.0f;
            verticalArrowSpeed = -verticalArrowSpeed;
        }
        if (verticalArrowTimer > 0.0f)
        {
            verticalArrowTimer = 0.0f;
            verticalArrowSpeed = -verticalArrowSpeed;
        }

        if (arrowTimer >= 1.0f)
        {
            float nextArrowTimer = arrowTimer + deltaTimeS * arrowSpeed * 0.4f;
            arrowTimer = nextArrowTimer >= 1.5f ? 0.2f : nextArrowTimer;
        }
        else
        {
            arrowTimer += deltaTimeS * arrowSpeed;
        }

        int validAreaCount = Math.Min(areaCount, areaList.Count);
        for (int areaId = 0; areaId < validAreaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            // CArea.Update() only drives these three running animations. Static areas do not
            // need to enter the function every frame; once an animation completes the area is
            // skipped again on the next frame.
            if (area != null &&
                (area.armyDrafting != null ||
                 area.armyMovingIn != null ||
                 area.armyMovingToFront != null))
            {
                area.Update(deltaTimeS);
            }
        }
    }

    public void UpdateFlashTimer(float deltaTimeS)
    {
        flashTimer += deltaTimeS * flashSpeed;
        if (flashTimer < 0.5f)
        {
            flashTimer = 0.5f;
            flashSpeed = -flashSpeed;
        }
        if (flashTimer > 0.8f)
        {
            flashTimer = 0.8f;
            flashSpeed = -flashSpeed;
        }
    }

    /// <summary>Initialises the map scene and its subsystems.</summary>
    /// <param name="areasEnable">Optional area enable file name; null uses the map defaults.</param>
    /// <param name="mapId">Map id to initialise.</param>
    public void Init(string areasEnable, int mapId)
    {
        areaMark.Init(mapId);
        LoadAreaData(mapId);
        LoadAdjoin(mapId);
        InitAreas(mapId, areasEnable);
        CheckAdjacentData();
        BuildCombatSpatialIndex();

        CalSceneRect(out topLeftPos[0], out topLeftPos[1], out size[0], out size[1]);
        CreateRenderAreaList();

        int fullMapWidth = areaMark.size[0] * 8;
        int fullMapHeight = areaMark.size[1] * 8;
        int alignedMapWidth = fullMapWidth - fullMapWidth % 100;
        int alignedMapHeight = fullMapHeight - fullMapHeight % 100;

        background = new CBackground();
        background.Init(mapId, 0, 0, alignedMapWidth, alignedMapHeight,
                        topLeftPos[0], topLeftPos[1], size[0], size[1]);

        InitAreaImage(mapId);
        camera.Init(topLeftPos[0], topLeftPos[1], size[0], size[1]);

        bomber = new CBomber();
        bomber.Init();

        areaSelected = null;
        newRoundFlashing = false;
        flashTimer = 0.8F;
        flashSpeed = -1.0F;
        fightFlashAreaId[0] = -1;
        fightFlashAreaId[1] = -1;
    }

    /// <summary>BFS over the adjacency graph with an add predicate; depth increments by one per step.</summary>
    public List<int> BFS(int startAreaId, int maxDepth, Func<AreaBFSNode<int>, bool> addPredicate)
        => BFS(startAreaId, maxDepth, addPredicate, (node, nextId) => node.depth + 1);

    /// <summary>BFS with custom depth update.</summary>
    public List<int> BFS<T>(int startAreaId, T maxDepth, Func<AreaBFSNode<T>, bool> addPredicate, Func<AreaBFSNode<T>, int, T> updatePredicate) where T : IComparable<T>
    {
        var q = new Queue<AreaBFSNode<T>>();
        var visited = new HashSet<int>();
        q.Enqueue(new AreaBFSNode<T> { id = startAreaId, depth = default });
        visited.Add(startAreaId);
        var res = new List<int>();
        while (q.Count > 0)
        {
            var node = q.Dequeue();
            if (addPredicate(node)) res.Add(node.id);
            if (node.depth.CompareTo(maxDepth) >= 0) continue;
            int neighborCount = GetNumAdjacentAreas(node.id);
            for (int i = 0; i < neighborCount; ++i)
            {
                int nextId = GetAdjacentAreaID(node.id, i);
                if (visited.Add(nextId))
                    q.Enqueue(new AreaBFSNode<T> { id = nextId, depth = updatePredicate(node, nextId) });
            }
        }
        return res;
    }

    /// <summary>BFS where the depth is the accumulated map distance.</summary>
    public List<int> BFS(int startAreaId, float maxDepth, Func<AreaBFSNode<float>, bool> addPredicate)
        => BFS(startAreaId, maxDepth, addPredicate, (node, nextId) => node.depth + GetTwoAreasDistance(node.id, nextId));
}

public struct AreaBFSNode<T>
{
    public int id;
    public T depth;
}
