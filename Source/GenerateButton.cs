using ColossalFramework.UI;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ProceduralCities
{
    class GenerateButton : UIButton
    {
        public Builder BuilderInstance => Builder.Instance;

        public override void Start()
        {
            base.Start();
            name = "GenerateButton";
            text = $"Generation Stopped {ProceduralCitiesMod.Version}";
            size = new Vector2(300, 50);
            absolutePosition = new Vector2(50, 50);
            normalBgSprite = "ButtonMenu";
            hoveredBgSprite = "ButtonMenuHovered";
            pressedBgSprite = "ButtonMenuPressed";
            isInteractive = true;
            Show();

            eventClicked += OnEventClicked;
        }

        private void OnEventClicked(UIComponent component, UIMouseEventParameter param)
        {
            DebugThing();
            return;

            Debug.Log($"Click: {BuilderInstance.IsRunning}");
            if (BuilderInstance.IsRunning)
            {
                BuilderInstance.Stop();
                text = $"Generation Stopped {ProceduralCitiesMod.Version}";
            }
            else
            {
                BuilderInstance.Start();
                text = $"Generation Running {ProceduralCitiesMod.Version}";
            }
        }

        private void DebugThing()
        {
            List<uint> prefabIds = new List<uint> { 144, 58, 54, 146 };
            Dictionary<uint, string> prefabNames = GetPrefabNames(prefabIds);
            PrintPrefabNames(prefabNames);

        }

        public Dictionary<uint, string> GetPrefabNames(List<uint> prefabIds)
        {
            Dictionary<uint, string> prefabNames = new Dictionary<uint, string>();

            for (uint i = 0; i < PrefabCollection<NetInfo>.PrefabCount(); i++)
            {
                NetInfo prefab = PrefabCollection<NetInfo>.GetPrefab(i);
                if (prefab != null && prefabIds.Contains(i))
                {
                    prefabNames[i] = prefab.name;
                }
            }

            return prefabNames;
        }

        public void PrintPrefabNames(Dictionary<uint, string> prefabNames)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Prefab IDs and Names:");

            foreach (KeyValuePair<uint, string> pair in prefabNames)
            {
                sb.AppendLine($"ID: {pair.Key}, Name: {pair.Value}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
