using System.Collections.Generic;
using Century.Campaign.View;
using Century.Core.Ui;
using Century.Core.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.App
{
    /// <summary>
    /// The title screen: the word CENTURY standing in three dimensions over a forest clearing in
    /// the rain, and two things to do: START PROTOTYPE and LOAD SAVE. The clearing is built here in
    /// code over the scene's camera and light: a runtime terrain dressed like the overmap's ground,
    /// woods on every side, stone and undergrowth in the open.
    /// </summary>
    /// <remarks>
    /// The title letters are a legacy TextMesh in the world font, extruded by stacking darker
    /// copies behind the face. No TextMeshPro, no asset: the same pipeline the world labels use.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TitleController : MonoBehaviour
    {
        [Tooltip("Prefab wardrobe for the clearing's trees, rocks and shrubs. Optional; primitive stand-ins are used when empty.")]
        [SerializeField] private TerrainDecorProfile _decor;

        [Tooltip("Face for the 3D title. Optional; falls back to Resources/Fonts/CenturyWorld.")]
        [SerializeField] private Font _titleFont;

        [Tooltip("Hour of day the clearing is lit for.")]
        [Range(0f, 24f)] [SerializeField] private float _hour = 9.5f;

        private const float GroundSpan = 320f;

        private static readonly Color Gold = new Color(0.88f, 0.75f, 0.41f);
        private static readonly Color Bronze = new Color(0.36f, 0.27f, 0.13f);

        private SaveSlotsPanel _saves;
        private Transform _cameraRig;
        private Terrain _ground;
        private float _sway;

        private void Start()
        {
            UiInputBootstrapper.EnsureEventSystem();
            BuildUi();
            BuildScene();
        }

        // --- UI ---------------------------------------------------------------------------------

        private void BuildUi()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            UiFont.Apply(root);

            Button start = root.Q<Button>("title-start");
            if (start != null) start.clicked += StartPrototype;

            Button load = root.Q<Button>("title-load");
            if (load != null) load.clicked += () => _saves?.Open(SaveSlotsPanel.Mode.Load);

            _saves = new SaveSlotsPanel(root);

            Label notice = root.Q<Label>("title-version");
            GameDirector director = GameDirector.Instance;
            if (notice != null && director != null && !string.IsNullOrEmpty(director.TitleNotice))
            {
                notice.text = director.TitleNotice;
                notice.AddToClassList("text-danger");
                director.TitleNotice = null;
            }
        }

        private void StartPrototype()
        {
            GameDirector director = GameDirector.Instance;
            if (director == null)
            {
                Debug.LogError("[Title] No GameDirector. Enter play mode from the Boot scene.", this);
                return;
            }

            director.StartNewCampaign();
        }

        private void Update()
        {
            if (_saves != null && _saves.IsOpen && Input.GetKeyDown(KeyCode.Escape)) _saves.Close();

            // A slow breath of camera movement: the scene is alive, not a painting.
            if (_cameraRig == null) return;
            _sway += Time.deltaTime;
            _cameraRig.rotation = Quaternion.Euler(
                7f + Mathf.Sin(_sway * 0.21f) * 0.7f,
                Mathf.Sin(_sway * 0.13f) * 2.2f,
                0f);
        }

        // --- The clearing -----------------------------------------------------------------------

        private void BuildScene()
        {
            var stage = new GameObject("TitleStage").transform;
            stage.SetParent(transform, false);

            BuildGround(stage);

            // The camera stands a little above head height at the clearing's edge, looking gently
            // down the open ground at the letters, so the floor fills the lower half of the frame.
            Camera camera = Camera.main;
            if (camera != null)
            {
                _cameraRig = new GameObject("TitleCameraRig").transform;
                _cameraRig.SetParent(stage, false);
                _cameraRig.position = new Vector3(0f, GroundHeight(0f, -24f) + 4.2f, -24f);
                camera.transform.SetParent(_cameraRig, false);
                camera.transform.localPosition = Vector3.zero;
                camera.transform.localRotation = Quaternion.identity;
                camera.fieldOfView = 44f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.30f, 0.33f, 0.36f);
            }

            BuildLetters(stage);
            BuildWoods(stage);

            // A bright, wet morning: the day/night cycle poses the sun, the rain softens it a little.
            var dayNight = new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
            dayNight.transform.SetParent(stage, false);
            float hour = _hour;
            dayNight.HourSource = () => hour;

            RainEffect.Create(stage, _cameraRig != null ? _cameraRig : stage, sunDimming: 0.8f);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 120f;
        }

        /// <summary>A gently rolling runtime terrain, dressed in the overmap's mossy earth so the
        /// ground reads as ground and not as a black void beneath the trees.</summary>
        private void BuildGround(Transform parent)
        {
            var data = new TerrainData
            {
                heightmapResolution = 129,
                size = new Vector3(GroundSpan, 8f, GroundSpan)
            };
            data.name = "Title clearing";

            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = x / (float)(res - 1) * GroundSpan;
                    float wz = z / (float)(res - 1) * GroundSpan;
                    float rolling = Mathf.PerlinNoise(wx * 0.02f + 3.1f, wz * 0.02f + 7.7f) * 0.55f
                                    + Mathf.PerlinNoise(wx * 0.07f + 11f, wz * 0.07f + 5f) * 0.12f;

                    // Flat and low through the clearing itself, rising a little into the woods.
                    float dx = wx - GroundSpan * 0.5f;
                    float dz = wz - GroundSpan * 0.5f;
                    float open = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dz * dz) - 14f) / 40f);
                    heights[z, x] = 0.05f + rolling * open;
                }
            }
            data.SetHeights(0, 0, heights);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "ForestFloor";
            terrainGo.transform.SetParent(parent, false);
            terrainGo.transform.position = new Vector3(-GroundSpan * 0.5f, 0f, -GroundSpan * 0.5f);
            _ground = terrainGo.GetComponent<Terrain>();

            // A terrain made at runtime carries no material: give it the render pipeline's own,
            // or it draws in the built-in fallback, which is pink under URP.
            UnityEngine.Rendering.RenderPipelineAsset pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (_ground != null && pipeline != null && pipeline.defaultTerrainMaterial != null)
                _ground.materialTemplate = pipeline.defaultTerrainMaterial;

            // The same mossy Germania the overmap wears, a shade lighter for a screen that is a picture.
            TerrainDressing.Apply(
                low: new Color(0.20f, 0.23f, 0.13f),
                high: new Color(0.34f, 0.36f, 0.22f),
                tileSizeWorldUnits: 30f);
        }

        private float GroundHeight(float x, float z)
        {
            if (_ground == null) return 0f;
            return _ground.SampleHeight(new Vector3(x, 0f, z)) + _ground.transform.position.y;
        }

        private Vector3 Grounded(Vector3 flat)
        {
            flat.y = GroundHeight(flat.x, flat.z);
            return flat;
        }

        /// <summary>CENTURY in the world font: a lit gold face with dark slices stacked behind it,
        /// which is all the extrusion a title needs at this distance.</summary>
        private void BuildLetters(Transform parent)
        {
            Font font = _titleFont != null ? _titleFont : Resources.Load<Font>("Fonts/CenturyWorld");

            var letters = new GameObject("Title").transform;
            letters.SetParent(parent, false);
            letters.position = new Vector3(0f, GroundHeight(0f, 4f) + 4.4f, 4f);

            const int slices = 6;
            for (int i = slices - 1; i >= 0; i--)
            {
                var slice = new GameObject(i == 0 ? "Face" : $"Depth {i}");
                slice.transform.SetParent(letters, false);
                slice.transform.localPosition = new Vector3(0f, -i * 0.045f, i * 0.16f);

                TextMesh text = slice.AddComponent<TextMesh>();
                text.text = "CENTURY";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 96;
                text.characterSize = 0.34f;
                text.color = i == 0 ? Gold : Color.Lerp(Bronze, Color.black, i / (float)slices);
                if (font != null) text.font = font;

                MeshRenderer renderer = slice.GetComponent<MeshRenderer>();
                if (renderer != null && font != null && font.material != null)
                    renderer.sharedMaterial = font.material;
            }

            // A thin gold rule beneath, the way the interface underlines its headings.
            GameObject rule = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rule.name = "Rule";
            rule.transform.SetParent(letters, false);
            rule.transform.localPosition = new Vector3(0f, -2.1f, 0f);
            rule.transform.localScale = new Vector3(9.5f, 0.05f, 0.05f);
            Collider ruleCollider = rule.GetComponent<Collider>();
            if (ruleCollider != null) Destroy(ruleCollider);
            Renderer ruleRenderer = rule.GetComponent<Renderer>();
            if (ruleRenderer != null) ruleRenderer.sharedMaterial = FxMaterials.Lit(Gold);
        }

        /// <summary>Woods on every side of the clearing, thinner along the line of sight, with
        /// stone and undergrowth scattered through the open ground.</summary>
        private void BuildWoods(Transform parent)
        {
            var random = new System.Random(919);
            var trees = new List<Vector3>();
            var rocks = new List<Vector3>();
            var shrubs = new List<Vector3>();

            for (int i = 0; i < 170; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 17f + (float)random.NextDouble() * 38f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // Keep the sight line from the camera to the letters open.
                if (Mathf.Abs(p.x) < 10f && p.z > -26f && p.z < 12f) continue;
                trees.Add(Grounded(p));
            }

            for (int i = 0; i < 70; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 6f + (float)random.NextDouble() * 26f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Mathf.Abs(p.x) < 4f && p.z < 6f && p.z > -22f) continue;   // the open middle stays open
                shrubs.Add(Grounded(p));
            }

            for (int i = 0; i < 18; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 9f + (float)random.NextDouble() * 22f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Mathf.Abs(p.x) < 5f && p.z < 6f && p.z > -22f) continue;
                rocks.Add(Grounded(p));
            }

            ForestBuilder.BuildForest(parent, trees, withColliders: false, _decor);
            if (_decor != null)
            {
                ForestBuilder.ScatterClutter(parent, "Stones", rocks, _decor.Rocks, withColliders: false);
                ForestBuilder.ScatterClutter(parent, "Underbrush", shrubs, _decor.Shrubs, withColliders: false);
            }
        }
    }
}
