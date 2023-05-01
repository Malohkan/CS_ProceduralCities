using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.Plugins;
using UnityEngine;

namespace ProceduralCities
{
    struct GenStep
    {
        public int x0;
        public int x1;
        public int y0;
        public int y1;
        public bool flag;
        public int prefabIdIndex;

        public GenStep(int x0, int x1, int y0, int y1, bool flag, int prefabIdIndex)
        {
            this.x0 = x0;
            this.x1 = x1;
            this.y0 = y0;
            this.y1 = y1;
            this.flag = flag;
            this.prefabIdIndex = prefabIdIndex;
        }
    }

    class Builder : MonoBehaviour
    {
        private List<IEnumerator> genSteps = new List<IEnumerator>();
        private Coroutine activeCoroutine;
        private int currentStep = 0;
        private String Version = "";
        public GenerateButton generateButton;
        internal static Builder _instance;

        public static Builder Instance
        {
            get
            {
                // If the singleton instance hasn't been created yet, try to find an existing instance.
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Builder>();
                    if (_instance.Version != ProceduralCitiesMod.Version)
                    {
                        Debug.Log($"Removing cached builder: {_instance.Version}");
                        UnityEngine.Object.Destroy(_instance);
                        _instance = null;
                    }
                    else
                    {
                        Debug.Log($"Found cached Builder: {_instance.Version}, steps: {_instance.genSteps.Count}");
                    }
                }

                // If no existing instance is found, create a new one.
                if (_instance == null)
                {
                    GameObject go = new GameObject("Builder");
                    _instance = go.AddComponent<Builder>();
                    _instance.Version = ProceduralCitiesMod.Version;
                }
                return _instance;
            }
        }

        public static void Reset()
        {
            _instance = FindObjectOfType<Builder>();
            if (_instance)
            {
                Instance.ResetRoads();
                UnityEngine.Object.Destroy(_instance);
            }
        }

        public void ResetRoads()
        {
            var netManager = Singleton<NetManager>.instance;

            // Release all segments
            for (uint i = 1; i < netManager.m_segments.m_buffer.Length; i++)
            {
                if (netManager.m_segments.m_buffer[i].m_flags != NetSegment.Flags.None)
                {
                    // Properly release the segment
                    netManager.ReleaseSegment((ushort)i, true);
                }
            }

            // Release all nodes
            for (uint i = 1; i < netManager.m_nodes.m_buffer.Length; i++)
            {
                if (netManager.m_nodes.m_buffer[i].m_flags != NetNode.Flags.None)
                {
                    // Properly release the node
                    netManager.ReleaseNode((ushort)i);
                }
            }
        }


        public int StepCount
        {
            get
            {
                return currentStep;
            }
        }

        // Automatically called by Unity as an initializer
        public void Start()
        {

        }

        public void StartBuilding() {
            if (genSteps.Count == 0)
            {
                Debug.Log("Generating Steps");
                GenerateSteps();
            }
            Debug.Log($"Start() {Version}: {activeCoroutine == null} && {currentStep} < {genSteps.Count}");
            if (activeCoroutine == null && currentStep < genSteps.Count)
            {
                Debug.Log($"Starting Builder {Version}");
                activeCoroutine = StartCoroutine(ExecuteSteps());
            }
        }

        public void StopBuilding()
        {
            if (activeCoroutine != null)
            {
                Debug.Log($"Stopping Builder {Version}");
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }
        }

        private IEnumerator ExecuteSteps()
        {
            Debug.Log($"ExecuteSteps Start {currentStep} < {genSteps.Count}");
            while (currentStep < genSteps.Count)
            {
                Debug.Log($"ExecuteSteps {currentStep} < {genSteps.Count} => {currentStep < genSteps.Count}");

                yield return StartCoroutine(genSteps[currentStep]);
                currentStep++;
            }
            activeCoroutine = null;
            Debug.Log($"ExecuteSteps Complete");
            generateButton.RefreshState();
        }

        const float pitch = 104; //(maxOffset * 2) / gridSize;
        const float height = 0f;
        readonly private Vector3Dictionary positionToNode = new Vector3Dictionary();


        public bool IsRunning {
            get {
                return activeCoroutine != null;
            }
        }

        ushort GetNode(Vector3 position, NetInfo prefab)
        {
            var netManager = Singleton<NetManager>.instance;
            if (positionToNode.ContainsKey(position))
            {
                Debug.Log($"GetNode found match");
                return positionToNode[position];
            }
            else
            {
                ushort nodeId;
                if (netManager.CreateNode(out nodeId, ref SimulationManager.instance.m_randomizer, prefab, position, SimulationManager.instance.m_currentBuildIndex))
                {
                    //Debug.LogWarning("created node " + nodeId);
                    ++SimulationManager.instance.m_currentBuildIndex;
                    positionToNode[position] = nodeId;
                    return nodeId;
                }
                else
                {
                    throw new Exception("Error creating node " + position.x + ", " + position.y + "at" + position);
                }
            }
        }

        void MakeSegment(Vector3 start, Vector3 end, Vector3 startDirection, Vector3 endDirection, NetInfo prefab)
        {
            var netManager = Singleton<NetManager>.instance;
            ushort segmentId;
            if (netManager.CreateSegment(out segmentId, ref SimulationManager.instance.m_randomizer, prefab, GetNode(start, prefab), GetNode(end, prefab), startDirection, endDirection, SimulationManager.instance.m_currentBuildIndex, SimulationManager.instance.m_currentBuildIndex, false))
            {
                ++SimulationManager.instance.m_currentBuildIndex;
                //Debug.LogWarning("made segment");
            }
            else
            {
                throw new Exception("Error creating segment");
            }
        }

        void MakeSegment(Vector3 start, Vector3 end, bool flip, NetInfo prefab)
        {
            genSteps.Add(MakeSegmentCoroutine(start, end, flip, prefab));
            
        }

        public float GetTerrainHeightWithWater(Vector3 position)
        {
            TerrainManager terrainManager = Singleton<TerrainManager>.instance;
            bool hasWater;
            float waterOffset = 0f;
            float combinedHeight = terrainManager.SampleBlockHeightSmoothWithWater(position, true, waterOffset, out hasWater);
            return combinedHeight;
        }

        private IEnumerator MakeSegmentCoroutine(Vector3 start, Vector3 end, bool flip, NetInfo prefab)
        {
            if (flip)
            {
                var temp = start;
                start = end;
                end = temp;
            }
            var netManager = Singleton<NetManager>.instance;
            ushort segmentId;

            Vector3 direction = new Vector3(end.x - start.x, end.y - start.y, end.z - start.z).normalized;

            Vector3 modifiedStart = new Vector3(start.x, start.y + GetTerrainHeightWithWater(start), start.z);
            Vector3 modifiedEnd = new Vector3(end.x, end.y + GetTerrainHeightWithWater(end), end.z);


            ushort startNodeId = GetNode(modifiedStart, prefab);
            ushort endNodeId = GetNode(modifiedEnd, prefab);

            if (netManager.CreateSegment(out segmentId, ref SimulationManager.instance.m_randomizer, prefab, startNodeId, endNodeId, direction, -direction, SimulationManager.instance.m_currentBuildIndex, SimulationManager.instance.m_currentBuildIndex, false))
            {
                ++SimulationManager.instance.m_currentBuildIndex;
                //Debug.LogWarning("made segment");
            }
            else
            {
                throw new Exception("Error creating segment");
            }

            yield return null;
        }


        void MakeRoad(Vector3 start, Vector3 end, Vector3 startDirection, Vector3 endDirection, bool flip, NetInfo prefab)
        {
            Debug.LogWarning("making bezier road");
            if (flip)
            {
                Vector3 temp = start;
                start = end;
                end = temp;

                temp = -startDirection;
                startDirection = -endDirection;
                endDirection = temp;
            }
            float length = (end - start).magnitude;
            var curve = new Bezier3(start, start + startDirection * length / 3, end + endDirection * length / 3, end);
            Vector3 priorPos = curve.Position(0);
            Vector3 priorDir = curve.Tangent(0).normalized;
            float t = curve.Travel(0, pitch);
            Debug.LogWarning(t.ToString());

            while (t < .9999)
            {
                Vector3 pos = curve.Position(t);
                Vector3 dir = curve.Tangent(t);
                MakeSegment(priorPos, pos, priorDir, -dir, prefab);
                t = curve.Travel(t, pitch);
                Debug.LogWarning(t.ToString());
                priorPos = pos;
                priorDir = dir;
            }
            {
                Vector3 pos = curve.Position(1);
                Vector3 dir = curve.Tangent(1).normalized;
                MakeSegment(priorPos, pos, priorDir, -dir, prefab);
            }
        }

        void MakeRoad(Vector3 start, Vector3 end, bool flip, NetInfo prefab)
        {
            if (flip)
            {
                Vector3 temp = start;
                start = end;
                end = temp;
            }

            var dir = (end - start).normalized;
            if ((dir.x == 0 || dir.z == 0) && dir.y == 0)
            {
                if (dir.x > 0)
                {
                    Debug.Log("made +x vector road");
                    for (float x = start.x; x < end.x; x += pitch)
                    {
                        float clampedIncrement = x + pitch;
                        if (clampedIncrement > end.x) clampedIncrement = end.x;
                        MakeSegment(new Vector3(x, start.y, start.z), new Vector3(clampedIncrement, start.y, start.z), false, prefab);
                    }
                }
                else if (dir.x < 0)
                {
                    Debug.Log("made -x vector road");
                    for (float x = start.x; x > end.x; x -= pitch)
                    {
                        float clampedIncrement = x - pitch;
                        if (clampedIncrement < end.x) clampedIncrement = end.x;
                        MakeSegment(new Vector3(x, start.y, start.z), new Vector3(clampedIncrement, start.y, start.z), false, prefab);
                    }
                }
                else if (dir.z > 0)
                {
                    Debug.Log("made +z vector road");
                    // MakeSegment(start, end, false, prefab);
                    for (float z = start.z; z < end.z; z += pitch)
                    {
                        float clampedIncrement = z + pitch;
                        if (clampedIncrement > end.z) clampedIncrement = end.z;
                        MakeSegment(new Vector3(start.x, start.y, z), new Vector3(start.x, start.y, clampedIncrement), false, prefab);
                    }
                }
                else if (dir.z < 0)
                {
                    Debug.Log("made -z vector road");
                    //MakeSegment(start, end, false, prefab);

                    for (float z = start.z; z > end.z; z -= pitch)
                    {
                        float clampedIncrement = z - pitch;
                        if (clampedIncrement < end.z) clampedIncrement = end.z;
                        MakeSegment(new Vector3(start.x, start.y, z), new Vector3(start.x, start.y, clampedIncrement), false, prefab);
                    }
                }
                else
                {
                    throw new Exception("logic error");
                }
            }
            else
            {
                var delta = end - start;
                var direction = delta.normalized;
                var length = delta.magnitude;
                float t = 0;
                for (; t <= length - pitch; t += pitch)
                {
                    MakeSegment(start + direction * t, start + direction * (t + pitch), false, prefab);
                }
                MakeSegment(start + direction * t, end, false, prefab);
            }
        }

        void MakeBridge(int x)
        {
            MakeSegment(new Vector3(x * pitch, height, -4 * pitch), new Vector3(x * pitch, height + 10, -3 * pitch), x % 20 == 0, Prefab("Pedestrian Gravel Elevated"));
            MakeSegment(new Vector3(x * pitch, height + 10, -3 * pitch), new Vector3(x * pitch, height + 20, -2 * pitch), x % 20 == 0, Prefab("Pedestrian Gravel Elevated"));
            MakeSegment(new Vector3(x * pitch, height + 20, -2 * pitch), new Vector3(x * pitch, height + 30, -1 * pitch), x % 20 == 0, Prefab("Pedestrian Gravel Elevated"));
            MakeSegment(new Vector3(x * pitch, height + 30, -1 * pitch), new Vector3(x * pitch, height + 30, -0 * pitch), x % 20 == 0, Prefab("Pedestrian Gravel Elevated"));

            MakeSegment(new Vector3(x * pitch, height + 30, 1 * pitch), new Vector3(x * pitch, height + 30, 0 * pitch), x % 20 != 0, Prefab("Pedestrian Gravel Elevated"));
            MakeSegment(new Vector3(x * pitch, height + 20, 2 * pitch), new Vector3(x * pitch, height + 30, 1 * pitch), x % 20 != 0, Prefab("Pedestrian Gravel Elevated"));
            MakeSegment(new Vector3(x * pitch, height + 10, 3 * pitch), new Vector3(x * pitch, height + 20, 2 * pitch), x % 20 != 0, Prefab("Pedestrian Gravel Elevated"));
            MakeSegment(new Vector3(x * pitch, height, 4 * pitch), new Vector3(x * pitch, height + 10, 3 * pitch), x % 20 != 0, Prefab("Pedestrian Gravel Elevated"));

        }

        uint GetPrefabIndexByName(string name)
        {
            for (uint i = 0; i < PrefabCollection<NetInfo>.PrefabCount(); i++)
            {
                NetInfo prefab = PrefabCollection<NetInfo>.GetPrefab(i);
                if (prefab != null && prefab.name == name)
                {
                    return i;
                }
            }
            throw new Exception($"Prefab with name '{name}' not found.");
        }

        public void GenerateOneRoad()
        {
            Debug.Log("GenerateOneRoad");
            int x = 0;
            int zStart = -200;
            int zStop = 500;

            //MakeRoad(new Vector3(x * pitch, height, zStart), new Vector3(x * pitch, height, zStop), false, Prefab("Basic Road"));

            x = 2;
            //MakeRoad(new Vector3(x * pitch, height, zStart), new Vector3(x * pitch, height, zStop), x % 20 == 0, Prefab("Pedestrian Gravel"));
            x = -2;
            //MakeRoad(new Vector3(x * pitch, height, zStart), new Vector3(x * pitch, height, zStop), x % 20 != 0, Prefab("Pedestrian Pavement"));
            x = 5;
            MakeBridge(x);
        }

        private NetInfo Prefab(String name)
        {
            return PrefabCollection<NetInfo>.FindLoaded(name);
        }

        // Prefab IDs and Names:
        // ID: 54, Name: Twoway Toll Road Medium 01
        // ID: 58, Name: Highway Connection
        // ID: 68, Name: Small 4 Lane Road with Bus Lanes Elevated
        // ID: 144, Name: Pedestrian Slope
        // ID: 146, Name: Pedestrian Gravel Elevated

        void GenerateSteps()
        {
            //MakeSegment(new Vector3(0*pitch, height, -10*pitch), new Vector3(-1*pitch, height, -10*pitch), 144);
            //MakeRoad(new Vector3(0*pitch, height, -10*pitch), new Vector3(-10*pitch, height, -10*pitch), false, 144);
            Debug.Log("GenerateSteps()");
            for (int x = -50; x <= 50; x += 10)
            {
                MakeRoad(new Vector3(x * pitch, height, 4 * pitch), new Vector3(x * pitch, height, 43 * pitch), x % 20 == 0, Prefab("Pedestrian Pavement"));
                MakeRoad(new Vector3(x * pitch, height, -4 * pitch), new Vector3(x * pitch, height, -43 * pitch), x % 20 != 0, Prefab("Pedestrian Pavement"));
                MakeBridge(x);
            }

            for (int x = -40; x < 40; x += 10)
            {
                for (int y = 8; y <= 46; y += 4)
                {
                    var start = new Vector3((x + 1) * pitch, height, y * pitch);
                    var end = new Vector3((x + 9) * pitch, height, y * pitch);
                    MakeRoad(start, start + new Vector3(0.5f * pitch, 0, 0), x % 20 == 0 != (y % 8 == 0), Prefab("Twoway Toll Road Medium 01"));
                    MakeRoad(start + new Vector3(0.5f * pitch, 0, 0), end - new Vector3(0.5f * pitch, 0, 0), x % 20 == 0 != (y % 8 == 0), Prefab("Twoway Toll Road Medium 01"));
                    MakeRoad(end - new Vector3(0.5f * pitch, 0, 0), end, x % 20 == 0 != (y % 8 == 0), Prefab("Twoway Toll Road Medium 01"));

                    MakeRoad(start + new Vector3(-1, 0, y % 8 == 0 ? 1 : -1) * pitch, start, x % 20 == 0 != (y % 8 == 0), Prefab("Highway Connection"));
                    MakeRoad(end, end + new Vector3(1, 0, y % 8 == 0 ? 1 : -1) * pitch, x % 20 == 0 != (y % 8 == 0), Prefab("Highway Connection"));

                    start = new Vector3((x + 1) * pitch, height, -y * pitch);
                    end = new Vector3((x + 9) * pitch, height, -y * pitch);
                    MakeRoad(start, start + new Vector3(0.5f * pitch, 0, 0), x % 20 == 0 == (y % 8 == 0), Prefab("Twoway Toll Road Medium 01"));
                    MakeRoad(start + new Vector3(0.5f * pitch, 0, 0), end - new Vector3(0.5f * pitch, 0, 0), x % 20 == 0 == (y % 8 == 0), Prefab("Twoway Toll Road Medium 01"));
                    MakeRoad(end - new Vector3(0.5f * pitch, 0, 0), end, x % 20 == 0 == (y % 8 == 0), Prefab("Twoway Toll Road Medium 01"));

                    MakeRoad(start + new Vector3(-1, 0, y % 8 == 0 ? -1 : 1) * pitch, start, x % 20 == 0 == (y % 8 == 0), Prefab("Highway Connection"));
                    MakeRoad(end, end + new Vector3(1, 0, y % 8 == 0 ? -1 : 1) * pitch, x % 20 == 0 == (y % 8 == 0), Prefab("Highway Connection"));
                }
            }

            for (int x = -40; x < 40; x += 10)
            {
                for (int y = 4; y < 46; y += 4)
                {
                    for (int x2 = -3; x2 <= 3; ++x2)
                    {
                        bool flip = (y % 8 == 0) == (x % 20 == 0);
                        var start = new Vector3((x + 5 + x2) * pitch, height, (y + 0.5f) * pitch);
                        var end = new Vector3((x + 5 + x2) * pitch, height, (y + 3.5f) * pitch);
                        MakeRoad(start, end, false, Prefab("Small 4 Lane Road with Bus Lanes Elevated"));
                        if (y != 4)
                        {
                            MakeRoad(start + new Vector3(-0.5f, 0, -0.5f) * pitch, start, flip == false, Prefab("Highway Connection"));
                            MakeRoad(start + new Vector3(0.5f, 0, -0.5f) * pitch, start, flip == true, Prefab("Highway Connection"));
                        }
                        if (y < 42)
                        {
                            MakeRoad(end, end + new Vector3(-0.5f, 0, 0.5f) * pitch, flip == false, Prefab("Highway Connection"));
                            MakeRoad(end, end + new Vector3(0.5f, 0, 0.5f) * pitch, flip == true, Prefab("Highway Connection"));
                        }

                        start = new Vector3((x + 5 + x2) * pitch, height, -(y + 0.5f) * pitch);
                        end = new Vector3((x + 5 + x2) * pitch, height, -(y + 3.5f) * pitch);
                        MakeRoad(start, end, false, Prefab("Small 4 Lane Road with Bus Lanes Elevated"));
                        if (y != 4)
                        {
                            MakeRoad(start + new Vector3(-0.5f, 0, 0.5f) * pitch, start, flip == true, Prefab("Highway Connection"));
                            MakeRoad(start + new Vector3(0.5f, 0, 0.5f) * pitch, start, flip == false, Prefab("Highway Connection"));
                        }
                        if (y < 42)
                        {
                            MakeRoad(end, end + new Vector3(-0.5f, 0, -0.5f) * pitch, flip == true, Prefab("Highway Connection"));
                            MakeRoad(end, end + new Vector3(0.5f, 0, -0.5f) * pitch, flip == false, Prefab("Highway Connection"));
                        }
                    }
                }
            }

            Debug.LogWarning($"Generated {genSteps.Count} steps");
        }
    }
}
