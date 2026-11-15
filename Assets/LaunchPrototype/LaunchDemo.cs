using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Interactive, procedural rocket launch demo. Add this component to a scene root.</summary>
public sealed partial class LaunchDemo : MonoBehaviour
{
    private static readonly Color Midnight = new Color(0.025f, 0.055f, 0.12f);
    private static readonly Color Cyan = new Color(0.22f, 0.82f, 0.97f);

    private GameObject separatedRocketModel;
    private Transform rocket;
    private Transform upperStage;
    private Transform firstStage;
    private Transform[] boosters = new Transform[2];
    private Light exhaustLight;
    private ParticleSystem[] enginePlume;
    private float firstStageNozzleY;
    private float capsuleNozzleY;
    private Camera launchCamera;
    private float fuel = 0f;
    private float launchElapsed;
    private float rocketHeight;
    private string status = "CARGA COMBUSTIBLE PARA PREPARAR EL DESPEGUE";
    private bool boostersSeparated;
    private bool firstStageSeparated;
    public float SpeedMultiplier { get; private set; } = 1f;
    public enum FlightState { Preparing, Countdown, Flying, Coasting, Complete, Failed }
    public FlightState State { get; private set; }
    public float Fuel => fuel;
    public float Altitude => Simulation == null ? 0 : (float)Simulation.Altura;
    public float Speed { get; private set; }
    public float WindSpeed => Simulation == null ? 0 : (float)Artemis.Viento.Evaluar(Simulation.Altura, (double)Simulation.Datos.Fisica["clima"][Simulation.Parametros.Clima]["viento"]);
    public float WindDirection => Mathf.Repeat(235f + Mathf.Sin(Time.time * 0.07f) * 18f, 360f);
    public float Countdown { get; private set; }
    public float FlightTime => Simulation == null ? 0 : (float)Simulation.Tiempo;
    public string Status => State == FlightState.Countdown ? status : Simulation?.Mensaje ?? status;
    public const float TargetAltitude = 600f;
    private readonly List<Transform> separatedStages = new List<Transform>();
    private readonly List<Object> generatedResources = new List<Object>();
    private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    private readonly Dictionary<string, Material> rocketFinishMaterials = new Dictionary<string, Material>();
    private ParticleSystem padSmoke;
    private ParticleSystem starField;
    private ParticleSystem.Particle[] starParticles;
    private Color[] starParticleColors;
    private float[] starTwinkleOffsets;
    private AudioSource engineAudio;
    private Material fireMaterial, smokeMaterial;
    private Texture2D rocketFinishAlbedo, rocketFinishNormal;
    private Material terrainMaterial, starMaterial;
    private Texture2D terrainAlbedo, terrainNormal, starTexture;
    private LaunchHud hud;

    private void Start()
    {
        InitializeMission();
        BuildWorld();
        BuildRocket();
        BuildCamera();
        BuildLights();
        BuildAtmosphere();
        BuildAudio();
        BuildMissionScenery();
        hud = gameObject.AddComponent<LaunchHud>();
        hud.Initialize(this);
        Application.targetFrameRate = 60;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && hud != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame) hud.HandleEscape();
            // Text focus suppresses shortcuts only: it must never freeze the simulation.
            if (!hud.BlocksFlightShortcuts)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame && State == FlightState.Preparing) Simulation.Abordar();
                if (Keyboard.current.spaceKey.wasPressedThisFrame) Launch();
                if (Keyboard.current.rKey.wasPressedThisFrame) ResetDemo();
                if (Keyboard.current.pKey.wasPressedThisFrame) TogglePause();
            }
        }
#endif
        if (rocket == null || exhaustLight == null) return;
        if (State == FlightState.Countdown && !Simulation.Pausada && !WelcomeVisible)
        {
            Countdown = Mathf.Max(0f, Countdown - Time.deltaTime);
            status = Countdown > 2f ? "SECUENCIA AUTOMÁTICA EN CURSO" : "ENCENDIDO DE MOTORES";
            if (Countdown <= 0f)
            {
                State = Simulation.Lanzar() ? FlightState.Flying : FlightState.Preparing;
            }
        }
        AdvanceMission(Time.deltaTime);
        UpdateVehiclePose();
        bool firing = State == FlightState.Flying && Simulation.Fase == Artemis.Fase.Despegue && !Simulation.CoreSeparado && Simulation.Resultado == null || (State == FlightState.Countdown && Countdown <= 2f);
        if (enginePlume != null)
        {
            for (int i = 0; i < enginePlume.Length; i++)
            {
                ParticleSystem plume = enginePlume[i];
                if (firing && !plume.isPlaying) plume.Play();
                else if (!firing && plume.isPlaying) plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        exhaustLight.transform.localPosition = new Vector3(0f, firstStageSeparated ? capsuleNozzleY : firstStageNozzleY, 0f);
        exhaustLight.intensity = firing ? 14f + Mathf.PerlinNoise(Time.time * 21f, 0f) * 10f : 0f;
        if (firing && rocketHeight < 14f) { if (!padSmoke.isPlaying) padSmoke.Play(); }
        else if (padSmoke.isPlaying) padSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        engineAudio.volume = Mathf.MoveTowards(engineAudio.volume, firing ? 0.35f : 0f, Time.deltaTime * 0.5f);
        engineAudio.pitch = 0.85f + Mathf.Clamp01(Speed / 20f) * 0.3f;
        UpdateCamera(firing);
        UpdateMissionScenery();
    }

    private void UpdateCamera(bool firing)
    {
        if (WelcomeVisible)
        {
            launchCamera.transform.SetPositionAndRotation(new Vector3(0, 5500, -20), Quaternion.identity);
            launchCamera.backgroundColor = new Color(.002f,.004f,.012f);
            RenderSettings.fogDensity = 0;
            UpdateStarVisibility(1f);
            return;
        }
        float framing = Mathf.SmoothStep(0f, 1f, rocketHeight / 40f);
        Vector3 target = new Vector3(-1.2f, 7.3f + rocketHeight, 0f);
        Vector3 offset = Vector3.Lerp(new Vector3(11f, 4.8f, -20f), new Vector3(12f, 1.8f, -23f), framing);
        Vector3 shake = firing ? new Vector3(Mathf.PerlinNoise(Time.time * 17f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 19f) - 0.5f, 0f) * 0.055f : Vector3.zero;
        if (firstStageSeparated && crewCabin != null) { target = crewCabin.position + Vector3.left * 0.8f; offset = new Vector3(7f, 2.8f, -12f); }
        if (serviceReleased && crewCabin != null) { target = crewCabin.position + Vector3.left * .6f; offset = new Vector3(4.5f, 2.4f, -9f); }
        if (CloseCamera && crewCabin != null) { target=crewCabin.position; offset=new Vector3(3.4f,1.2f,-5.5f); }
        if (Simulation.Fase == Artemis.Fase.Aterrizaje && !CloseCamera && crewCabin != null)
        {
            target = crewCabin.position + new Vector3(-.6f,1.6f,0);
            offset = new Vector3(7f,3f,-16f);
        }
        launchCamera.transform.position = target + offset + shake;
        launchCamera.transform.LookAt(target + shake);
        float spaceBlend = Mathf.SmoothStep(0f, 1f, Altitude / 100000f);
        launchCamera.backgroundColor = Color.Lerp(new Color(0.12f, 0.23f, 0.32f), new Color(0.002f,0.004f,0.012f), spaceBlend);
        RenderSettings.fogDensity = Mathf.Lerp(0.0012f,0f,spaceBlend);
        UpdateStarVisibility(Mathf.Lerp(.18f, 1f, Mathf.SmoothStep(0f, 1f, Altitude / 80000f)));
    }

    private void BuildWorld()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.38f, 0.45f, 0.57f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.12f, 0.23f, 0.32f);
        RenderSettings.fogDensity = 0.0012f;

        GameObject launchSite = Make("Launch Site", PrimitiveType.Plane, new Vector3(0f, -0.15f, 0f), new Vector3(15f, 1f, 15f), new Color(0.22f, 0.29f, 0.36f));
        ApplyLandingTerrainFinish(launchSite);
        Make("Reinforced launch deck", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(7.5f, 0.45f, 7.5f), new Color(0.16f, 0.21f, 0.27f));
        Make("Deck inset", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(3.1f, 0.12f, 3.1f), new Color(0.36f, 0.43f, 0.49f));

        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            Vector3 p = new Vector3(Mathf.Cos(a) * 5.2f, 2.7f, Mathf.Sin(a) * 5.2f);
            Make("Service gantry", PrimitiveType.Cube, p, new Vector3(0.4f, 5.4f, 0.4f), new Color(0.24f, 0.31f, 0.38f));
            Make("Gantry crossbar", PrimitiveType.Cube, p + Vector3.up * 1.8f, new Vector3(1.4f, 0.16f, 0.16f), new Color(0.29f, 0.38f, 0.45f));
            Make("Warning beacon", PrimitiveType.Sphere, p + Vector3.up * 2.8f, Vector3.one * 0.22f, new Color(1f, 0.33f, 0.08f), true);
        }
        for (int i = 0; i < 20; i++)
        {
            float a = i * Mathf.PI * 2f / 20f;
            GameObject mark = Make("Deck safety marking", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 3.45f, 0.49f, Mathf.Sin(a) * 3.45f),
                new Vector3(0.14f, 0.06f, 0.58f), new Color(1f, 0.58f, 0.12f));
            mark.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
        }
        // Service tower with open lattice bracing, behind the launch vehicle.
        for (int side = -1; side <= 1; side += 2)
        {
            Make("Tower upright", PrimitiveType.Cube, new Vector3(-3f + side * 0.65f, 5.2f, 2f), new Vector3(0.14f, 10.4f, 0.2f), new Color(0.21f, 0.28f, 0.32f));
            for (int level = 0; level < 6; level++)
            {
                var brace = Make("Tower diagonal", PrimitiveType.Cube, new Vector3(-3f, 0.8f + level * 1.65f, 2f), new Vector3(0.1f, 2.05f, 0.12f), new Color(0.27f, 0.34f, 0.38f));
                brace.transform.rotation = Quaternion.Euler(0f, 0f, side * 39f);
            }
        }
        Make("Access platform", PrimitiveType.Cube, new Vector3(-2.4f, 6.8f, 2f), new Vector3(2.7f, 0.18f, 1.4f), new Color(0.32f, 0.38f, 0.41f));
        Make("Tower beacon", PrimitiveType.Sphere, new Vector3(-3f, 10.55f, 2f), Vector3.one * 0.22f, new Color(1f, 0.18f, 0.03f), true);
        Make("Access road", PrimitiveType.Cube, new Vector3(0f, -0.08f, 36f), new Vector3(5f, 0.08f, 65f), new Color(0.11f, 0.15f, 0.18f));
        for (int i = 0; i < 12; i++)
        {
            Make("Road stripe", PrimitiveType.Cube, new Vector3(0f, -0.025f, 9f + i * 4f), new Vector3(0.12f, 0.02f, 1.5f), new Color(0.6f, 0.63f, 0.6f));
            float a = i * Mathf.PI / 6f;
            Make("Perimeter light", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 11f, 0.15f, Mathf.Sin(a) * 11f), Vector3.one * 0.13f, Cyan, true);
        }
        for (int i = 0; i < 7; i++)
        {
            Make("Distant ridge", PrimitiveType.Sphere, new Vector3(-100f + i * 32f, -5f, 100f + i % 2 * 20f), new Vector3(65f, 18f + i % 3 * 9f, 45f), new Color(0.13f, 0.21f, 0.25f));
        }
        Make("Fuel tank", PrimitiveType.Cylinder, new Vector3(13f, 1.6f, 10f), new Vector3(2.8f, 1.6f, 2.8f), new Color(0.5f, 0.58f, 0.61f));
        Make("Fuel tank cap", PrimitiveType.Sphere, new Vector3(13f, 3.2f, 10f), new Vector3(2.8f, 0.8f, 2.8f), new Color(0.5f, 0.58f, 0.61f));
        GameObject earth = Make("Earth horizon", PrimitiveType.Sphere, new Vector3(0f, -2500f, 0f), Vector3.one * 5000f, new Color(0.035f, 0.16f, 0.29f));
        Destroy(earth.GetComponent<Collider>());
        BuildStars();
    }

    private void BuildStars()
    {
        const int count = 4800;
        var random = new System.Random(2026);
        Color[] palette = { new Color(0.72f, 0.84f, 1f), new Color(0.48f, 0.68f, 1f), new Color(1f, 0.86f, 0.68f), Color.white };
        var starObject = new GameObject("High altitude star field");
        starObject.transform.SetParent(transform, false);
        starField = starObject.AddComponent<ParticleSystem>();
        starField.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = starField.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 100000f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0f;
        main.startLifetime = 100000f;
        main.maxParticles = count;
        var emission = starField.emission;
        emission.enabled = false;
        var shape = starField.shape;
        shape.enabled = false;
        var renderer = starField.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Shader starShader = Resources.Load<Shader>("AuroraStars");
        if (starShader == null) starShader = Shader.Find("Aurora/Stars");
        if (starShader == null)
        {
            Debug.LogError("Could not load the Aurora/Stars shader.", this);
            return;
        }
        starMaterial = new Material(starShader) { name = "Distant stars" };
        starTexture = CreateStarTexture();
        starMaterial.SetTexture("_MainTex", starTexture);
        renderer.sharedMaterial = starMaterial;
        generatedResources.Add(starMaterial);
        generatedResources.Add(starTexture);

        starParticles = new ParticleSystem.Particle[count];
        starParticleColors = new Color[count];
        starTwinkleOffsets = new float[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float elevation = Mathf.Asin(Mathf.Lerp(-1f, 1f, (float)random.NextDouble()));
            float distance = Mathf.Lerp(2400f, 3200f, (float)random.NextDouble());
            float horizontalDistance = Mathf.Cos(elevation) * distance;
            Vector3 position = new Vector3(Mathf.Sin(angle) * horizontalDistance, Mathf.Sin(elevation) * distance, Mathf.Cos(angle) * horizontalDistance);
            float size = i % 19 == 0 ? 18f : Mathf.Lerp(6f, 12f, (float)random.NextDouble());
            Color color = palette[random.Next(palette.Length)];
            starParticleColors[i] = color;
            starTwinkleOffsets[i] = (float)random.NextDouble() * Mathf.PI * 2f;
            starParticles[i] = new ParticleSystem.Particle
            {
                position = position,
                startColor = Color.clear,
                startSize = size,
                startLifetime = 100000f,
                remainingLifetime = 100000f,
                velocity = Vector3.zero
            };
        }
        starField.SetParticles(starParticles, starParticles.Length);
        starField.Play();
    }

    private void UpdateStarVisibility(float visibility)
    {
        if (starField == null || starParticles == null) return;
        starField.transform.position = launchCamera.transform.position;
        for (int i = 0; i < starParticles.Length; i++)
        {
            float twinkle = 0.8f + Mathf.Sin(Time.time * (0.7f + (i % 7) * 0.11f) + starTwinkleOffsets[i]) * 0.2f;
            Color color = starParticleColors[i];
            color.a = visibility * twinkle;
            starParticles[i].startColor = color;
        }
        starField.SetParticles(starParticles, starParticles.Length);
    }

    private Texture2D CreateStarTexture()
    {
        const int resolution = 32;
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
        {
            name = "Four point star glow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / resolution * 2f - 1f);
                float dy = Mathf.Abs((y + 0.5f) / resolution * 2f - 1f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Exp(-radius * radius * 18f);
                float rays = Mathf.Max(Mathf.Pow(Mathf.Clamp01(1f - dx), 18f) * Mathf.Clamp01(1f - dy * 7f),
                    Mathf.Pow(Mathf.Clamp01(1f - dy), 18f) * Mathf.Clamp01(1f - dx * 7f));
                float alpha = Mathf.Clamp01(core * 0.65f + rays * 0.85f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private void BuildRocket()
    {
        rocket = new GameObject("AURORA  •  launch vehicle").transform;
        rocket.SetParent(transform, false);
        rocket.position = new Vector3(0f, 4.2f, 0f);
        separatedRocketModel = LoadSeparatedRocketModel();
        if (separatedRocketModel == null)
        {
            status = "NO SE ENCONTRÓ EL MODELO SEPARADO DEL COHETE";
            Debug.LogError("Could not load Assets/Resources/Modelado Cohete Separado.fbx.", this);
            return;
        }
        if (!BuildSeparatedModelStages())
        {
            status = "NO SE PUDIERON ENSAMBLAR LAS ETAPAS DEL MODELO";
            return;
        }
        CreateRocketEngineLight();
    }

    private void CreateRocketEngineLight()
    {
        Vector3 enginePosition = new Vector3(0f, firstStageNozzleY, 0f);
        enginePlume = CreateEnginePlume(rocket, "Rocket main engine", enginePosition, 0.7f);
        exhaustLight = new GameObject("Flickering engine light").AddComponent<Light>();
        exhaustLight.type = LightType.Point;
        exhaustLight.color = new Color(1f, 0.38f, 0.11f);
        exhaustLight.range = 12f;
        exhaustLight.intensity = 0f;
        exhaustLight.transform.SetParent(rocket, false);
        exhaustLight.transform.localPosition = enginePosition;
    }

    private bool BuildSeparatedModelStages()
    {
        Transform modelRoot = Instantiate(separatedRocketModel, rocket, false).transform;
        modelRoot.name = "Separated rocket model";
        foreach (Camera importedCamera in modelRoot.GetComponentsInChildren<Camera>(true)) importedCamera.enabled = false;
        foreach (Light importedLight in modelRoot.GetComponentsInChildren<Light>(true)) importedLight.enabled = false;
        foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
            if (child.name == "Cube") child.gameObject.SetActive(false);

        Transform body = FindModelPart(modelRoot, "Modelo 1");
        Transform leftBooster = FindModelPart(modelRoot, "Modelo 2");
        Transform rightBooster = FindModelPart(modelRoot, "Modelo 3");
        Transform capsule = FindModelPart(modelRoot, "Modelo 4");
        if (body == null || leftBooster == null || rightBooster == null || capsule == null)
        {
            var parts = new List<Transform>();
            foreach (MeshFilter filter in modelRoot.GetComponentsInChildren<MeshFilter>(true))
                if (filter.gameObject.activeInHierarchy && filter.name != "Cube" && !parts.Contains(filter.transform)) parts.Add(filter.transform);
            if (parts.Count < 4)
            {
                Debug.LogError("The separated rocket FBX must contain four separate mesh objects.", this);
                Destroy(modelRoot.gameObject);
                return false;
            }
            parts.Sort((a, b) => PartHeight(b).CompareTo(PartHeight(a)));
            body = parts[0]; leftBooster = parts[1]; rightBooster = parts[2]; capsule = parts[3];
        }

        if (!TryGetPartBounds(body, out Bounds bodyBounds) || !TryGetPartBounds(capsule, out Bounds capsuleBounds) ||
            !TryGetPartBounds(leftBooster, out Bounds leftBounds) || !TryGetPartBounds(rightBooster, out Bounds rightBounds))
        {
            Debug.LogError("Could not read the separate rocket stages from the FBX.", this);
            Destroy(modelRoot.gameObject);
            return false;
        }

        float bodyHeight = bodyBounds.size.y;
        float capsuleHeight = capsuleBounds.size.y;
        float boosterHeight = Mathf.Max(leftBounds.size.y, rightBounds.size.y);
        float bodyWidth = bodyBounds.size.x;
        float boosterWidth = Mathf.Max(leftBounds.size.x, rightBounds.size.x);
        MovePartCenter(modelRoot, body, new Vector3(0f, bodyHeight * 0.5f, 0f));
        MovePartCenter(modelRoot, capsule, new Vector3(0f, bodyHeight + capsuleHeight * 0.5f + 0.08f, 0f));
        MovePartCenter(modelRoot, leftBooster, new Vector3(-(bodyWidth + boosterWidth) * 0.5f - 0.12f, boosterHeight * 0.5f, 0f));
        MovePartCenter(modelRoot, rightBooster, new Vector3((bodyWidth + boosterWidth) * 0.5f + 0.12f, boosterHeight * 0.5f, 0f));
        float modelScale = 6f / Mathf.Max(bodyHeight + capsuleHeight + 0.08f, 0.01f);
        modelRoot.localScale = Vector3.one * modelScale;

        firstStageNozzleY = 0.02f * modelScale;
        capsuleNozzleY = (bodyHeight + 0.08f) * modelScale;
        ApplyRealisticRocketFinish(body, "Primary stage", new Color(0.55f, 0.67f, 0.76f), 0.78f, 0.76f);
        ApplyRealisticRocketFinish(leftBooster, "Booster", new Color(0.26f, 0.40f, 0.53f), 0.84f, 0.72f);
        ApplyRealisticRocketFinish(rightBooster, "Booster", new Color(0.26f, 0.40f, 0.53f), 0.84f, 0.72f);
        ApplyRealisticRocketFinish(capsule, "Crew capsule", new Color(0.78f, 0.84f, 0.89f), 0.68f, 0.82f);

        firstStage = body;
        upperStage = capsule;
        BuildUpperModules(capsule);
        boosters[0] = leftBooster;
        boosters[1] = rightBooster;
        boostersSeparated = false;
        return true;
    }

    private static GameObject LoadSeparatedRocketModel()
    {
        GameObject bestModel = null;
        int bestPartCount = 0;
        foreach (GameObject candidate in Resources.LoadAll<GameObject>("Modelado Cohete Separado"))
        {
            int partCount = candidate.GetComponentsInChildren<MeshFilter>(true).Length;
            if (partCount <= bestPartCount) continue;
            bestModel = candidate;
            bestPartCount = partCount;
        }
        return bestModel;
    }

    private static Transform FindModelPart(Transform root, string token)
    {
        string search = token.ToLowerInvariant();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.name.ToLowerInvariant().Contains(search)) return filter.transform;
        return null;
    }

    private static bool TryGetPartBounds(Transform part, out Bounds bounds)
    {
        Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { bounds = default; return false; }
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds.size.sqrMagnitude > 0.0001f;
    }

    private float PartHeight(Transform part) => TryGetPartBounds(part, out Bounds bounds) ? bounds.size.y : 0f;

    private static void MovePartCenter(Transform modelRoot, Transform part, Vector3 targetCenter)
    {
        if (!TryGetPartBounds(part, out Bounds bounds) || part.parent == null) return;
        Vector3 targetWorld = modelRoot.TransformPoint(targetCenter);
        Vector3 worldDelta = targetWorld - bounds.center;
        part.localPosition += part.parent.InverseTransformVector(worldDelta);
    }

    private void BuildCamera()
    {
        GameObject go = new GameObject("Launch Camera • 1080p");
        go.transform.SetParent(transform, false);
        go.tag = "MainCamera";
        launchCamera = go.AddComponent<Camera>();
        launchCamera.fieldOfView = 36f;
        launchCamera.farClipPlane = 8000f;
        launchCamera.allowHDR = true;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        launchCamera.backgroundColor = Midnight;
        launchCamera.clearFlags = CameraClearFlags.SolidColor;
        go.transform.position = new Vector3(10f, 8f, -18f);
        go.transform.LookAt(new Vector3(0f, 3.5f, 0f));
        go.AddComponent<AudioListener>();
        UpdateCamera(false);
    }

    private void BuildLights()
    {
        GameObject sun = new GameObject("Launch key light");
        sun.transform.SetParent(transform, false);
        Light key = sun.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.5f;
        key.color = new Color(1f, 0.86f, 0.69f);
        key.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(45f, -28f, 0f);
    }

    private void SeparateBoosters()
    {
        boostersSeparated = true;
        for (int i = 0; i < boosters.Length; i++)
        {
            Transform part = boosters[i];
            part.SetParent(transform, true);
            separatedStages.Add(part);
            part.gameObject.AddComponent<FallingStage>().Initialize(new Vector3(i == 0 ? -2.2f : 2.2f, 1.1f, i == 0 ? -0.8f : 0.8f), new Vector3(0f, 0f, i == 0 ? -18f : 18f), this);
        }
        status = "PROPULSORES LATERALES SEPARADOS";
    }

    private void SeparateFirstStage()
    {
        firstStageSeparated = true;
        Vector3 capsuleEnginePosition = new Vector3(0f, capsuleNozzleY, 0f);
        if (enginePlume != null)
        {
            for (int i = 0; i < enginePlume.Length; i++)
            {
                enginePlume[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                enginePlume[i].transform.localPosition = capsuleEnginePosition;
            }
        }
        if (exhaustLight != null) exhaustLight.transform.localPosition = capsuleEnginePosition;
        Transform part = firstStage;
        part.SetParent(transform, true);
        separatedStages.Add(part);
        part.gameObject.AddComponent<FallingStage>().Initialize(new Vector3(0f, -2.2f, 0f), new Vector3(0f, 0f, 12f), this);
        status = "PRIMERA ETAPA SEPARADA · CÚPULA DE LOS ASTRONAUTAS EN ASCENSO";
    }

    public void AddFuel()
    {
        if (State != FlightState.Preparing) return;
        fuel = Mathf.Min(100f, fuel + 25f);
        status = fuel >= 100f ? "TANQUES LLENOS · LISTO PARA DESPEGAR" : "COMBUSTIBLE CARGADO · RECOMENDADO: 100%";
    }

    public void SetFuelLevel(float level)
    {
        if (State != FlightState.Preparing) return;
        fuel = Mathf.Clamp(level, 0f, 100f);
        status = fuel >= 25f ? "NIVEL DE COMBUSTIBLE CONFIGURADO · LISTO PARA DESPEGAR" : "SELECCIONA AL MENOS 25% PARA INICIAR";
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        SpeedMultiplier = Mathf.Clamp(multiplier, 1f, 20f);
    }

    public void Launch()
    {
        if (WelcomeVisible || State != FlightState.Preparing) return;
        if (!Simulation.PrepararLanzamiento()) return;
        State = FlightState.Countdown;
        Countdown = 5f;
        launchElapsed = rocketHeight = 0f;
        status = "INICIANDO CUENTA REGRESIVA";
    }

    public void AbortMission()
    {
        if (State != FlightState.Countdown && State != FlightState.Flying && State != FlightState.Coasting) return;
        Simulation.Abortar();
        State = FlightState.Failed;
        Speed = 0f;
        Countdown = 0f;
        status = "MISIÓN ABORTADA POR CONTROL DE TIERRA";
        if (padSmoke != null) padSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void ResetDemo()
    {
        NewMission(Simulation.Parametros.Semilla, Simulation.Parametros.NumeroMision);
        foreach (Transform piece in separatedStages) if (piece != null) Destroy(piece.gameObject);
        separatedStages.Clear();
        foreach (Mesh mesh in generatedResources.FindAll(resource => resource is Mesh && resource != celestialSphere).ConvertAll(resource => (Mesh)resource))
        {
            generatedResources.Remove(mesh);
            if (mesh != null) Destroy(mesh);
        }
        rocket.gameObject.SetActive(false);
        Destroy(rocket.gameObject);
        fuel = 100f; boostersSeparated = firstStageSeparated = false;
        rocketHeight = launchElapsed = Speed = Countdown = 0f;
        State = FlightState.Preparing;
        SpeedMultiplier = 1f;
        ResetFlightPresentation();
        padSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        engineAudio.volume = 0f;
        BuildRocket();
        if (parachutes != null) { Destroy(parachutes); parachutes = null; }
        UpdateCamera(false);
        status = "CARGA COMBUSTIBLE PARA PREPARAR EL DESPEGUE";
    }

    private GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color, bool emissive = false)
    {
        GameObject obj = GameObject.CreatePrimitive(shape); obj.name = name; obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position; obj.transform.localScale = scale; ApplyMaterial(obj, color, emissive); return obj;
    }

    private GameObject Make(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color, bool emissive = false)
    {
        GameObject obj = GameObject.CreatePrimitive(shape); obj.name = name; obj.transform.SetParent(transform, false); obj.transform.position = position; obj.transform.localScale = scale; ApplyMaterial(obj, color, emissive); return obj;
    }

    private GameObject ConePart(Transform parent, string name, Vector3 localPosition, float radius, float height, Color color)
    {
        const int sides = 24;
        Vector3[] vertices = new Vector3[sides + 2];
        int[] triangles = new int[sides * 6];
        vertices[0] = new Vector3(0f, height * 0.5f, 0f);
        vertices[sides + 1] = new Vector3(0f, -height * 0.5f, 0f);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, -height * 0.5f, Mathf.Sin(angle) * radius);
            int next = (i + 1) % sides;
            int t = i * 6;
            triangles[t] = 0; triangles[t + 1] = next + 1; triangles[t + 2] = i + 1;
            triangles[t + 3] = sides + 1; triangles[t + 4] = i + 1; triangles[t + 5] = next + 1;
        }
        Mesh mesh = new Mesh { name = name + " mesh", vertices = vertices, triangles = triangles };
        generatedResources.Add(mesh);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false); obj.transform.localPosition = localPosition;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>();
        ApplyMaterial(obj, color);
        return obj;
    }

    private void ApplyMaterial(GameObject obj, Color color, bool emissive = false)
    {
        Color key = color; key.a = emissive ? 0.5f : 1f;
        if (!materials.TryGetValue(key, out Material material))
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            material.SetFloat("_Smoothness", 0.38f);
            material.SetFloat("_Metallic", 0.32f);
            if (emissive) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 3f); }
            materials.Add(key, material);
            generatedResources.Add(material);
        }
        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void ApplyRealisticRocketFinish(Transform part, string finishName, Color tint, float metallic, float smoothness)
    {
        if (part == null) return;
        if (!rocketFinishMaterials.TryGetValue(finishName, out Material material))
        {
            material = CreateRocketFinishMaterial(finishName, tint, metallic, smoothness);
            rocketFinishMaterials.Add(finishName, material);
            generatedResources.Add(material);
        }

        foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
        {
            int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            Material[] finishMaterials = new Material[slotCount];
            for (int i = 0; i < finishMaterials.Length; i++) finishMaterials[i] = material;
            renderer.sharedMaterials = finishMaterials;
        }
    }

    private Material CreateRocketFinishMaterial(string finishName, Color tint, float metallic, float smoothness)
    {
        Shader shader = FindLitShader();
        var material = new Material(shader) { name = "Rocket finish • " + finishName, color = tint };
        material.SetColor("_BaseColor", tint);
        material.SetColor("_Color", tint);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Glossiness", smoothness);
        EnsureRocketFinishTextures();
        material.SetTexture("_BaseMap", rocketFinishAlbedo);
        material.SetTexture("_MainTex", rocketFinishAlbedo);
        material.SetTextureScale("_BaseMap", new Vector2(1.8f, 2.4f));
        material.SetTextureScale("_MainTex", new Vector2(1.8f, 2.4f));
        material.SetTexture("_BumpMap", rocketFinishNormal);
        material.SetFloat("_BumpScale", 0.28f);
        material.EnableKeyword("_NORMALMAP");
        return material;
    }

    private void ApplyLandingTerrainFinish(GameObject launchSite)
    {
        if (launchSite == null) return;
        if (terrainMaterial == null)
        {
            EnsureTerrainTextures();
            terrainMaterial = new Material(FindLitShader())
            {
                name = "Weathered launch terrain",
                color = new Color(0.72f, 0.74f, 0.73f)
            };
            terrainMaterial.SetColor("_BaseColor", terrainMaterial.color);
            terrainMaterial.SetColor("_Color", terrainMaterial.color);
            terrainMaterial.SetFloat("_Metallic", 0.08f);
            terrainMaterial.SetFloat("_Smoothness", 0.32f);
            terrainMaterial.SetFloat("_Glossiness", 0.32f);
            terrainMaterial.SetTexture("_BaseMap", terrainAlbedo);
            terrainMaterial.SetTexture("_MainTex", terrainAlbedo);
            terrainMaterial.SetTexture("_BumpMap", terrainNormal);
            terrainMaterial.SetFloat("_BumpScale", 0.55f);
            terrainMaterial.EnableKeyword("_NORMALMAP");
            terrainMaterial.SetTextureScale("_BaseMap", new Vector2(18f, 18f));
            terrainMaterial.SetTextureScale("_MainTex", new Vector2(18f, 18f));
            generatedResources.Add(terrainMaterial);
        }
        launchSite.GetComponent<Renderer>().sharedMaterial = terrainMaterial;
    }

    private static Shader FindLitShader()
    {
        Shader shader = GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }

    private void EnsureTerrainTextures()
    {
        if (terrainAlbedo != null) return;
        const int resolution = 256;
        terrainAlbedo = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false)
        {
            name = "Launch terrain weathered concrete",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        terrainNormal = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
        {
            name = "Launch terrain roughness relief",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };

        var heights = new float[resolution * resolution];
        var colors = new Color[heights.Length];
        var normals = new Color[heights.Length];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (float)x / resolution;
                float v = (float)y / resolution;
                float broad = TilingPerlin(u, v, 6f, 3.1f, 8.7f);
                float medium = TilingPerlin(u, v, 25f, 11.2f, 2.4f);
                float grain = TilingPerlin(u, v, 91f, 5.6f, 19.3f);
                float crackNoise = Mathf.Abs(TilingPerlin(u, v, 17f, 4.3f, 13.1f) - 0.5f);
                float cracks = Mathf.SmoothStep(0.012f, 0.045f, crackNoise);
                float height = 0.48f + broad * 0.2f + medium * 0.13f + grain * 0.04f;
                heights[y * resolution + x] = height;
                float albedo = (0.59f + broad * 0.22f + medium * 0.13f + (grain - 0.5f) * 0.08f) * Mathf.Lerp(0.72f, 1f, cracks);
                colors[y * resolution + x] = new Color(albedo, albedo * 0.99f, albedo * 0.96f, 1f);
            }
        }

        for (int y = 0; y < resolution; y++)
        {
            int previousY = (y + resolution - 1) % resolution;
            int nextY = (y + 1) % resolution;
            for (int x = 0; x < resolution; x++)
            {
                int previousX = (x + resolution - 1) % resolution;
                int nextX = (x + 1) % resolution;
                float dx = heights[y * resolution + nextX] - heights[y * resolution + previousX];
                float dy = heights[nextY * resolution + x] - heights[previousY * resolution + x];
                Vector3 normal = new Vector3(-dx * 5f, -dy * 5f, 1f).normalized;
                normals[y * resolution + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z, 1f);
            }
        }
        terrainAlbedo.SetPixels(colors);
        terrainAlbedo.Apply(true, true);
        terrainNormal.SetPixels(normals);
        terrainNormal.Apply(true, true);
        generatedResources.Add(terrainAlbedo);
        generatedResources.Add(terrainNormal);
    }

    private static float TilingPerlin(float u, float v, float frequency, float offsetU, float offsetV)
    {
        float x = u * frequency + offsetU;
        float y = v * frequency + offsetV;
        float blendU = Mathf.SmoothStep(0f, 1f, u);
        float blendV = Mathf.SmoothStep(0f, 1f, v);
        float lower = Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - frequency, y), blendU);
        float upper = Mathf.Lerp(Mathf.PerlinNoise(x, y - frequency), Mathf.PerlinNoise(x - frequency, y - frequency), blendU);
        return Mathf.Lerp(lower, upper, blendV);
    }

    private void EnsureRocketFinishTextures()
    {
        if (rocketFinishAlbedo != null) return;
        const int resolution = 256;
        rocketFinishAlbedo = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false)
        {
            name = "Rocket ceramic panel grain",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        rocketFinishNormal = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
        {
            name = "Rocket subtle surface relief",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };

        var random = new System.Random(412);
        var colorPixels = new Color[resolution * resolution];
        var normalPixels = new Color[colorPixels.Length];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float grain = 0.965f + (float)random.NextDouble() * 0.055f;
                bool horizontalJoint = y % 48 < 2;
                bool verticalJoint = x % 64 < 2 && y % 48 > 5;
                float panelTone = 0.985f + (float)((x / 64 + (y / 48) * 3) % 4) * 0.009f;
                float seam = horizontalJoint || verticalJoint ? 0.84f : 1f;
                float albedo = grain * panelTone * seam;
                colorPixels[y * resolution + x] = new Color(albedo, albedo, albedo, 1f);

                float reliefX = ((float)random.NextDouble() - 0.5f) * 0.025f;
                float reliefY = ((float)random.NextDouble() - 0.5f) * 0.025f;
                if (horizontalJoint) reliefY = 0.08f;
                if (verticalJoint) reliefX = 0.08f;
                normalPixels[y * resolution + x] = new Color(0.5f + reliefX, 0.5f + reliefY, 1f, 1f);
            }
        }
        rocketFinishAlbedo.SetPixels(colorPixels);
        rocketFinishAlbedo.Apply(true, true);
        rocketFinishNormal.SetPixels(normalPixels);
        rocketFinishNormal.Apply(true, true);
        generatedResources.Add(rocketFinishAlbedo);
        generatedResources.Add(rocketFinishNormal);
    }

    private ParticleSystem[] CreateEnginePlume(Transform parent, string label, Vector3 localPosition, float sizeScale)
    {
        return new[]
        {
            CreatePlumeLayer(parent, label + " • turbulent flame", localPosition, sizeScale, new Color(1f, 0.65f, 0.12f, 0.72f), new Color(0.9f, 0.08f, 0.004f, 0f), 0.38f, 0.68f, 0.15f, 0.26f, 260f, 5f, 17f),
            CreatePlumeLayer(parent, label + " • white hot core", localPosition, sizeScale, new Color(1f, 0.96f, 0.72f, 1f), new Color(1f, 0.32f, 0.025f, 0f), 0.2f, 0.38f, 0.07f, 0.13f, 220f, 2f, 19f),
            CreatePlumeLayer(parent, label + " • blue nozzle", localPosition, sizeScale, new Color(0.3f, 0.6f, 1f, 0.7f), new Color(0.7f, 0.8f, 1f, 0f), 0.22f, 0.3f, 0.03f, 0.06f, 150f, 1f, 11f),
            CreateSmoke(parent, label + " • exhaust smoke", localPosition, sizeScale, false)
        };
    }

    private ParticleSystem CreatePlumeLayer(Transform parent, string label, Vector3 localPosition, float sizeScale,
        Color startColor, Color endColor, float minSize, float maxSize, float minLife, float maxLife, float rate, float coneAngle, float speed)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 900;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.78f, speed * 1.24f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize * sizeScale, maxSize * sizeScale);
        main.startColor = Color.white;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.angle = Mathf.Min(coneAngle, 0.35f);
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;
        shape.radius = 0.12f * sizeScale;
        shape.radiusThickness = 1f;
        var colorLife = ps.colorOverLifetime;
        colorLife.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
            new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(startColor.a * 0.72f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colorLife.color = new ParticleSystem.MinMaxGradient(gradient);
        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        AnimationCurve taper = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f));
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, taper);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        if (fireMaterial == null)
        {
            fireMaterial = new Material(Resources.Load<Shader>("AuroraExhaust"));
            fireMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
            fireMaterial.SetFloat("_Intensity", 2.8f);
            generatedResources.Add(fireMaterial);
        }
        renderer.sharedMaterial = fireMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var noise = ps.noise;
        noise.enabled = false;
        return ps;
    }

    private ParticleSystem CreateSmoke(Transform parent, string label, Vector3 position, float scale, bool ground)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(ground ? -90f : 90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = ground ? 2.4f : 3.4f;
        main.startSpeed = ground ? new ParticleSystem.MinMaxCurve(1f, 3.5f) : new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.1f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 450;
        var emission = ps.emission; emission.rateOverTime = ground ? 32f : 26f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = ground ? 55f : 0.35f; shape.radius = ground ? 0.75f : 0.04f * scale;
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(0.65f, 0.61f, 0.54f), 0f), new GradientColorKey(new Color(0.3f, 0.36f, 0.4f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.12f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, ground ? 8f : 5f));
        var noise = ps.noise;
        noise.enabled = false;
        noise.strength = 0.16f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.12f;
        var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 0f;
        velocity.y = ground ? 0.18f : -0.35f;
        if (smokeMaterial == null)
        {
            smokeMaterial = new Material(Resources.Load<Shader>("AuroraExhaust"));
            smokeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            smokeMaterial.SetFloat("_Intensity", 1f);
            generatedResources.Add(smokeMaterial);
        }
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
        return ps;
    }

    private void BuildAtmosphere()
    {
        padSmoke = CreateSmoke(transform, "Launch pad rolling smoke", new Vector3(0f, 0.7f, 0f), 1.5f, true);
        var volumeObject = new GameObject("Launch atmosphere");
        volumeObject.transform.SetParent(transform, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 10f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        generatedResources.Add(profile);
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.value = 1f; bloom.intensity.value = 0.65f; bloom.scatter.value = 0.65f;
        var tone = profile.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.ACES;
        var vignette = profile.Add<Vignette>(true); vignette.intensity.value = 0.22f;
        generatedResources.Add(bloom); generatedResources.Add(tone); generatedResources.Add(vignette);
    }

    private void BuildAudio()
    {
        const int sampleRate = 22050;
        var clip = AudioClip.Create("Engine rumble", sampleRate * 3, 1, sampleRate, false);
        var samples = new float[sampleRate * 3];
        var random = new System.Random(74);
        float low = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.09f);
            float t = (float)i / sampleRate;
            float fade = Mathf.Min(1f, Mathf.Min(i / 200f, (samples.Length - 1 - i) / 200f));
            samples[i] = (low * 0.8f + Mathf.Sin(t * Mathf.PI * 2f * 43f) * 0.12f) * fade;
        }
        clip.SetData(samples, 0); generatedResources.Add(clip);
        engineAudio = gameObject.AddComponent<AudioSource>();
        engineAudio.clip = clip; engineAudio.loop = true; engineAudio.volume = 0f;
        engineAudio.spatialBlend = 0f; engineAudio.Play();
    }

    private void OnDestroy()
    {
        foreach (Object resource in generatedResources) if (resource != null) Destroy(resource);
    }

    private sealed class FallingStage : MonoBehaviour
    {
        private Vector3 velocity, spin;
        private LaunchDemo mission;
        public void Initialize(Vector3 initialVelocity, Vector3 initialSpin, LaunchDemo controller) { velocity = initialVelocity; spin = initialSpin; mission = controller; }
        private void Update()
        {
            if (mission != null && (mission.WelcomeVisible || mission.Simulation.Pausada)) return;
            float delta = Time.deltaTime * (mission != null ? mission.SpeedMultiplier : 1f);
            velocity += Vector3.down * 2.2f * delta;
            transform.position += velocity * delta;
            transform.Rotate(spin * delta, Space.Self);
            if (transform.position.y < -25f) Destroy(gameObject);
        }
    }
}
