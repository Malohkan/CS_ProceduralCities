using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProceduralCities {
    public class LoadingExtension : LoadingExtensionBase {
        private GenerateButton button; 

        public override void OnCreated(ILoading loading) {
            base.OnCreated(loading);
            Debug.Log($"LoadingExtension.OnCreated {ProceduralCitiesMod.Version}");

            var go = GameObject.Find("procedural_cities");
            if (go)
            {
                Debug.Log($"Reloading button");
                Object.Destroy(go);

                OnLevelLoaded(LoadMode.LoadGame);
            }
            
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            Debug.Log("LoadingExtension.OnLevelLoad");
            button = (GenerateButton)UIView.GetAView().AddUIComponent(typeof(GenerateButton));
            var go = new GameObject("procedural_cities");
            go.AddComponent<Builder>();
        }


        public override void OnReleased() {
            Debug.Log("LoadingExtension.OnReleased");
            var go = GameObject.Find("procedural_cities");
            Object.Destroy(go);
            base.OnReleased();
        }
    }
}