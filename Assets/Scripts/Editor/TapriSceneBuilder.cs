using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace TapriTycoon.EditorTools
{
#if UNITY_EDITOR
    /// <summary>
    /// Optional 1-Click Unity Editor Scene Builder:
    /// Adds a menu item "Tapri Tycoon -> Build 3D Tapri Scene Hierarchy" inside Unity Editor
    /// that automatically creates the Tapri Stall, Kettle + Steam ParticleSystem, 600-Drop Rain ParticleSystem,
    /// Chaiwala Player, 3 Customer Tables, Auto-Rickshaw, Isometric Camera, and GameManager!
    /// </summary>
    public static class TapriSceneBuilder
    {
        [MenuItem("Tapri Tycoon/Build 3D Tapri Scene Hierarchy")]
        public static void BuildTapriScene()
        {
            GameObject root = new GameObject("--- TAPRI TYCOON 3D SCENE ---");

            // 1. GameManager & WeatherSystem
            GameObject systemsObj = new GameObject("GameSystems");
            systemsObj.transform.SetParent(root.transform);
            GameManager gm = systemsObj.AddComponent<GameManager>();
            TableManager tm = systemsObj.AddComponent<TableManager>();
            WeatherSystem ws = systemsObj.AddComponent<WeatherSystem>();
            gm.tableManager = tm;
            gm.weatherSystem = ws;

            // 2. Isometric Camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
            }
            cam.transform.position = new Vector3(0f, 21.5f, 22.5f);
            cam.transform.LookAt(new Vector3(0f, 0.6f, 0.5f));
            cam.fieldOfView = 36f;
            gm.cameraTransform = cam.transform;
            ws.mainCamera = cam;

            // 3. Courtyard Ground & Back Road
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TapriCourtyard";
            ground.transform.SetParent(root.transform);
            ground.transform.position = new Vector3(0f, -0.08f, 1f);
            ground.transform.localScale = new Vector3(24f, 0.16f, 17f);

            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "HighwayRoad";
            road.transform.SetParent(root.transform);
            road.transform.position = new Vector3(0f, -0.05f, -6.8f);
            road.transform.localScale = new Vector3(44f, 0.16f, 3.6f);

            // 4. 3D Tapri Stall & Brass Kettle + Steam ParticleSystem
            GameObject stall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stall.name = "TapriStall";
            stall.transform.SetParent(root.transform);
            stall.transform.position = new Vector3(-6.2f, 0.625f, -1.5f);
            stall.transform.localScale = new Vector3(3.2f, 1.25f, 4.4f);

            GameObject kettle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            kettle.name = "BrassKettle";
            kettle.transform.SetParent(stall.transform);
            kettle.transform.position = new Vector3(-5.9f, 1.85f, -2.0f);
            kettle.transform.localScale = new Vector3(0.3f, 0.7f, 0.22f);
            gm.tapriKettlePoint = kettle.transform;

            GameObject steamObj = new GameObject("KettleSteamParticles");
            steamObj.transform.SetParent(kettle.transform);
            steamObj.transform.localPosition = Vector3.up * 0.5f;
            ParticleSystem steamPs = steamObj.AddComponent<ParticleSystem>();
            gm.kettleSteamParticles = steamPs;

            // 5. 600-Drop Rain ParticleSystem
            GameObject rainObj = new GameObject("RainParticleSystem_600");
            rainObj.transform.SetParent(root.transform);
            rainObj.transform.position = new Vector3(0f, 16f, 0f);
            ParticleSystem rainPs = rainObj.AddComponent<ParticleSystem>();
            ws.rainParticleSystem = rainPs;

            // 6. Chaiwala Player
            GameObject playerObj = new GameObject("ChaiwalaPlayer");
            playerObj.transform.SetParent(root.transform);
            playerObj.transform.position = new Vector3(-1.8f, 0f, 1.2f);
            playerObj.AddComponent<CharacterController>();
            ChaiwalaController chaiwala = playerObj.AddComponent<ChaiwalaController>();
            gm.player = chaiwala;

            GameObject brewZone = new GameObject("BrewZoneCenter");
            brewZone.transform.SetParent(root.transform);
            brewZone.transform.position = new Vector3(-3.8f, 0f, -1.2f);
            chaiwala.brewZoneCenter = brewZone.transform;

            // 7. 3 Customer Tables
            Vector3[] tableCoords = new Vector3[]
            {
                new Vector3(0.8f, 0f, -1.5f),
                new Vector3(6.2f, 0f, -1.5f),
                new Vector3(3.5f, 0f, 3.3f)
            };

            for (int i = 0; i < tableCoords.Length; i++)
            {
                GameObject tblObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tblObj.name = $"Table_{i + 1}";
                tblObj.transform.SetParent(root.transform);
                tblObj.transform.position = tableCoords[i] + Vector3.up * 0.5f;
                tblObj.transform.localScale = new Vector3(2.2f, 0.5f, 2.2f);

                TableSlot slot = tblObj.AddComponent<TableSlot>();
                GameObject seat = new GameObject("SeatPoint");
                seat.transform.SetParent(tblObj.transform);
                seat.transform.position = tableCoords[i] + new Vector3(0f, 0f, -1.25f);
                slot.seatPoint = seat.transform;

                GameObject drop = new GameObject("CupDropPoint");
                drop.transform.SetParent(tblObj.transform);
                drop.transform.position = tableCoords[i] + new Vector3(0f, 1.1f, 0f);
                slot.cupDropPoint = drop.transform;

                tm.tables.Add(slot);
            }

            // 8. Auto-Rickshaw
            GameObject rickshawObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rickshawObj.name = "AutoRickshaw";
            rickshawObj.transform.SetParent(root.transform);
            rickshawObj.transform.position = new Vector3(-14f, 0.6f, -6.8f);
            rickshawObj.transform.localScale = new Vector3(2.3f, 1.2f, 1.35f);
            rickshawObj.AddComponent<AutoRickshaw>();

            Selection.activeGameObject = root;
            Debug.Log("Tapri Tycoon 3D Scene Hierarchy generated! Bake NavMesh and assign prefabs in GameManager.");
        }
    }
#endif
}
