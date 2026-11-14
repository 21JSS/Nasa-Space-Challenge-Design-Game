using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Interactive, procedural rocket launch demo. Add this component to a scene root.</summary>
public sealed class LaunchDemo : MonoBehaviour
{
    private static readonly Color Midnight = new Color(0.025f, 0.055f, 0.12f);
    private static readonly Color Cyan = new Color(0.22f, 0.82f, 0.97f);

    private Transform rocket;
    private Transform upperStage;
    private Transform firstStage;
    private Transform[] boosters = new Transform[2];
    private ParticleSystem[] firstStageFlames;
    private ParticleSystem[] upperStageFlames;
    private ParticleSystem[][] boosterFlames = new ParticleSystem[2][];
    private Light exhaustLight;
    private Camera launchCamera;
    private float fuel = 0f;
    private float launchElapsed;
    private float rocketHeight;
    private string status = "CARGA COMBUSTIBLE PARA PREPARAR EL DESPEGUE";
    private bool boostersSeparated;
    private bool firstStageSeparated;
    public enum FlightState { Preparing, Countdown, Flying, Coasting, Complete, Failed }
    public FlightState State { get; private set; }
    public float Fuel => fuel;
    public float Altitude => rocketHeight;
    public float Speed { get; private set; }
    public float Countdown { get; private set; }
    public float FlightTime => launchElapsed;
    public string Status => status;
    public const float TargetAltitude = 600f;
    private readonly List<Transform> separatedStages = new List<Transform>();
    private readonly List<Object> generatedResources = new List<Object>();
    private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    private ParticleSystem padSmoke;
    private AudioSource engineAudio;
    private Material fireMaterial, smokeMaterial;
    private LaunchHud hud;

    private void Start()
    {
        BuildWorld();
        BuildRocket();
        BuildCamera();
        BuildLights();
        BuildAtmosphere();
        BuildAudio();
        hud = gameObject.AddComponent<LaunchHud>();
        hud.Initialize(this);
        Application.targetFrameRate = 60;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.fKey.wasPressedThisFrame) AddFuel();
            if (Keyboard.current.spaceKey.wasPressedThisFrame) Launch();
            if (Keyboard.current.rKey.wasPressedThisFrame) ResetDemo();
        }
#endif
        if (State == FlightState.Countdown)
        {
            Countdown = Mathf.Max(0f, Countdown - Time.deltaTime);
            status = Countdown > 2f ? "SECUENCIA AUTOMÁTICA EN CURSO" : "ENCENDIDO DE MOTORES";
            if (Countdown <= 2f) SetEngines(true);
            if (Countdown <= 0f)
            {
                State = FlightState.Flying;
                status = "DESPEGUE · ASCENSO NOMINAL";
            }
        }
        if (State == FlightState.Flying || State == FlightState.Coasting)
        {
            launchElapsed += Time.deltaTime;
            fuel = Mathf.Max(0f, fuel - Time.deltaTime * 3f);
            if (fuel > 0f) Speed += (3.8f + launchElapsed * 0.38f) * Time.deltaTime;
            else
            {
                State = FlightState.Coasting;
                Speed = Mathf.Max(0f, Speed - 9.81f * Time.deltaTime);
                status = "SIN COMBUSTIBLE · ASCENSO POR INERCIA";
            }
            rocketHeight += Speed * Time.deltaTime;
            if (!boostersSeparated && launchElapsed >= 8f) SeparateBoosters();
            if (!firstStageSeparated && launchElapsed >= 16f) SeparateFirstStage();
            if (rocketHeight >= TargetAltitude)
            {
                State = FlightState.Complete;
                status = "MISIÓN CUMPLIDA · ALTITUD OBJETIVO ALCANZADA";
                Speed = 0f;
            }
            else if (fuel <= 0f && Speed <= 0f)
            {
                State = FlightState.Failed;
                status = "VUELO TERMINADO · PRUEBA CON MÁS COMBUSTIBLE";
            }
            SetEngines(State == FlightState.Flying);
        }
        rocket.position = new Vector3(0f, 4.2f + rocketHeight, 0f);
        bool firing = State == FlightState.Flying || (State == FlightState.Countdown && Countdown <= 2f);
        exhaustLight.transform.localPosition = firstStageSeparated ? new Vector3(0f, 0.55f, 0f) : new Vector3(0f, -3.05f, 0f);
        exhaustLight.intensity = firing ? 14f + Mathf.PerlinNoise(Time.time * 21f, 0f) * 10f : 0f;
        if (firing && rocketHeight < 14f) { if (!padSmoke.isPlaying) padSmoke.Play(); }
        else if (padSmoke.isPlaying) padSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        engineAudio.volume = Mathf.MoveTowards(engineAudio.volume, firing ? 0.35f : 0f, Time.deltaTime * 0.5f);
        engineAudio.pitch = 0.85f + Mathf.Clamp01(Speed / 90f) * 0.3f;
        UpdateCamera(firing);
    }

    private void SetEngines(bool on)
    {
        SetFlames(firstStageFlames, on && !firstStageSeparated);
        SetFlames(upperStageFlames, on && firstStageSeparated);
        foreach (var plume in boosterFlames) SetFlames(plume, on && !boostersSeparated);
    }

    private void UpdateCamera(bool firing)
    {
        float framing = Mathf.SmoothStep(0f, 1f, rocketHeight / 40f);
        Vector3 target = new Vector3(-2.5f, 4.4f + rocketHeight, 0f);
        Vector3 offset = Vector3.Lerp(new Vector3(11f, 4.8f, -20f), new Vector3(12f, 1.8f, -23f), framing);
        Vector3 shake = firing ? new Vector3(Mathf.PerlinNoise(Time.time * 17f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 19f) - 0.5f, 0f) * 0.055f : Vector3.zero;
        launchCamera.transform.position = target + offset + shake;
        launchCamera.transform.LookAt(target + shake);
        launchCamera.backgroundColor = Color.Lerp(new Color(0.12f, 0.23f, 0.32f), Midnight, rocketHeight / TargetAltitude);
    }

    private void BuildWorld()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.38f, 0.45f, 0.57f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.12f, 0.23f, 0.32f);
        RenderSettings.fogDensity = 0.007f;

        Make("Launch Site", PrimitiveType.Plane, new Vector3(0f, -0.15f, 0f), new Vector3(15f, 1f, 15f), new Color(0.22f, 0.29f, 0.36f));
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
    }

    private void BuildRocket()
    {
        rocket = new GameObject("AURORA  •  launch vehicle").transform;
        rocket.SetParent(transform, false);
        rocket.position = new Vector3(0f, 4.2f, 0f);

        firstStage = new GameObject("Core first stage").transform;
        firstStage.SetParent(rocket, false);
        firstStage.localPosition = new Vector3(0f, -0.95f, 0f);
        Part(firstStage, "LOX / RP-1 core tank", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0f), new Vector3(1.05f, 1.3f, 1.05f), new Color(0.83f, 0.88f, 0.91f));
        Part(firstStage, "Interstage collar", PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(1.06f, 0.16f, 1.06f), new Color(0.22f, 0.29f, 0.35f));
        Part(firstStage, "Tank band", PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f), new Vector3(1.055f, 0.055f, 1.055f), Cyan);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            Part(firstStage, "Engine bell", PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.38f, -1.48f, Mathf.Sin(a) * 0.38f), new Vector3(0.2f, 0.28f, 0.2f), new Color(0.22f, 0.27f, 0.32f));
        }
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            GameObject fin = Part(firstStage, "Titanium grid fin", PrimitiveType.Cube,
                new Vector3(Mathf.Cos(a) * 0.57f, -0.92f, Mathf.Sin(a) * 0.57f), new Vector3(0.12f, 0.76f, 0.58f), new Color(0.19f, 0.3f, 0.39f));
            fin.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, -8f);
        }

        upperStage = new GameObject("Second stage and payload fairing").transform;
        upperStage.SetParent(rocket, false);
        upperStage.localPosition = new Vector3(0f, 0.95f, 0f);
        Part(upperStage, "Second stage tank", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.88f, 1.05f, 0.88f), new Color(0.84f, 0.89f, 0.92f));
        Part(upperStage, "Stage separation ring", PrimitiveType.Cylinder, new Vector3(0f, -0.65f, 0f), new Vector3(0.92f, 0.12f, 0.92f), new Color(0.18f, 0.28f, 0.36f));
        Part(upperStage, "Payload fairing", PrimitiveType.Cylinder, new Vector3(0f, 1.38f, 0f), new Vector3(0.9f, 0.56f, 0.9f), new Color(0.88f, 0.92f, 0.95f));
        ConePart(upperStage, "Payload fairing nose", new Vector3(0f, 2.45f, 0f), 0.45f, 1.02f, new Color(0.82f, 0.88f, 0.92f));
        Part(upperStage, "Nose cap", PrimitiveType.Sphere, new Vector3(0f, 2.96f, 0f), new Vector3(0.08f, 0.08f, 0.08f), Cyan, true);
        Part(upperStage, "Telemetry band", PrimitiveType.Cylinder, new Vector3(0f, 0.79f, 0f), new Vector3(0.89f, 0.08f, 0.89f), Cyan);
        Part(upperStage, "Cabin window", PrimitiveType.Sphere, new Vector3(0f, 1.35f, -0.43f), new Vector3(0.28f, 0.28f, 0.08f), new Color(0.03f, 0.12f, 0.2f), true);
        upperStageFlames = CreateEnginePlume(upperStage, "Vacuum engine plume", new Vector3(0f, -0.85f, 0f), 0.72f);

        for (int i = 0; i < boosters.Length; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Transform booster = new GameObject(i == 0 ? "Left solid rocket booster" : "Right solid rocket booster").transform;
            booster.SetParent(rocket, false);
            booster.localPosition = new Vector3(side * 1.08f, -0.6f, 0f);
            boosters[i] = booster;
            Part(booster, "Booster casing", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.38f, 1.25f, 0.38f), new Color(0.78f, 0.83f, 0.87f));
            Part(booster, "Booster nose", PrimitiveType.Sphere, new Vector3(0f, 1.25f, 0f), new Vector3(0.38f, 0.36f, 0.38f), new Color(0.16f, 0.45f, 0.65f));
            Part(booster, "Booster nozzle", PrimitiveType.Cylinder, new Vector3(0f, -1.32f, 0f), new Vector3(0.24f, 0.18f, 0.24f), new Color(0.2f, 0.25f, 0.3f));
            Part(booster, "Booster stripe", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0f), new Vector3(0.39f, 0.07f, 0.39f), Cyan);
            boosterFlames[i] = CreateEnginePlume(booster, "Solid booster plume", new Vector3(0f, -1.48f, 0f), 0.46f);
            SetFlames(boosterFlames[i], false);
        }

        firstStageFlames = CreateEnginePlume(firstStage, "Core engine plume", new Vector3(0f, -1.7f, 0f), 1f);
        SetFlames(firstStageFlames, false);
        SetFlames(upperStageFlames, false);
        exhaustLight = new GameObject("Flickering engine light").AddComponent<Light>();
        exhaustLight.type = LightType.Point;
        exhaustLight.color = new Color(1f, 0.38f, 0.11f);
        exhaustLight.range = 12f;
        exhaustLight.intensity = 0f;
        exhaustLight.transform.SetParent(rocket, false);
    }

    private void BuildCamera()
    {
        GameObject go = new GameObject("Launch Camera • 1080p");
        go.transform.SetParent(transform, false);
        go.tag = "MainCamera";
        launchCamera = go.AddComponent<Camera>();
        launchCamera.fieldOfView = 36f;
        launchCamera.farClipPlane = 1500f;
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
            SetFlames(boosterFlames[i], false);
            part.SetParent(transform, true);
            separatedStages.Add(part);
            part.gameObject.AddComponent<FallingStage>().Initialize(new Vector3(i == 0 ? -2.2f : 2.2f, 1.1f, i == 0 ? -0.8f : 0.8f), new Vector3(0f, 0f, i == 0 ? -18f : 18f));
        }
        status = "PROPULSORES LATERALES SEPARADOS";
    }

    private void SeparateFirstStage()
    {
        firstStageSeparated = true;
        Transform part = firstStage;
        part.SetParent(transform, true);
        separatedStages.Add(part);
        part.gameObject.AddComponent<FallingStage>().Initialize(new Vector3(0f, -2.2f, 0f), new Vector3(0f, 0f, 12f));
        SetFlames(firstStageFlames, false);
        SetFlames(upperStageFlames, fuel > 0f);
        status = "PRIMERA ETAPA SEPARADA  •  SEGUNDA ETAPA ACTIVA";
    }

    public void AddFuel()
    {
        if (State != FlightState.Preparing) return;
        fuel = Mathf.Min(100f, fuel + 25f);
        status = fuel >= 100f ? "TANQUES LLENOS · LISTO PARA DESPEGAR" : "COMBUSTIBLE CARGADO · RECOMENDADO: 100%";
    }

    public void Launch()
    {
        if (State != FlightState.Preparing) return;
        if (fuel < 25f) { status = "AÑADE COMBUSTIBLE ANTES DEL DESPEGUE"; return; }
        State = FlightState.Countdown;
        Countdown = 5f;
        launchElapsed = rocketHeight = 0f;
        status = "INICIANDO CUENTA REGRESIVA";
    }

    public void ResetDemo()
    {
        SetEngines(false);
        foreach (Transform piece in separatedStages) if (piece != null) Destroy(piece.gameObject);
        separatedStages.Clear();
        rocket.gameObject.SetActive(false);
        Destroy(rocket.gameObject);
        fuel = 0f; boostersSeparated = firstStageSeparated = false;
        rocketHeight = launchElapsed = Speed = Countdown = 0f;
        State = FlightState.Preparing;
        padSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        engineAudio.volume = 0f;
        BuildRocket();
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
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
        shape.angle = coneAngle;
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
        noise.enabled = true; noise.strength = 0.18f; noise.frequency = 2f; noise.scrollSpeed = 1.5f;
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
        main.startLifetime = ground ? 5f : 3.4f;
        main.startSpeed = ground ? new ParticleSystem.MinMaxCurve(1f, 3.5f) : new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.1f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 450;
        var emission = ps.emission; emission.rateOverTime = ground ? 55f : 26f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = ground ? 75f : 12f; shape.radius = ground ? 1f : 0.12f * scale;
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(0.65f, 0.61f, 0.54f), 0f), new GradientColorKey(new Color(0.3f, 0.36f, 0.4f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.12f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, ground ? 8f : 5f));
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.65f; noise.frequency = 0.35f; noise.scrollSpeed = 0.3f;
        var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 0.7f; velocity.y = ground ? 0.4f : 0.2f;
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

    private static void SetFlames(ParticleSystem[] systems, bool on)
    {
        if (systems == null) return;
        foreach (ParticleSystem system in systems)
        {
            if (system == null) continue;
            if (on && !system.isPlaying) system.Play(true);
            else if (!on && system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private sealed class FallingStage : MonoBehaviour
    {
        private Vector3 velocity, spin;
        public void Initialize(Vector3 initialVelocity, Vector3 initialSpin) { velocity = initialVelocity; spin = initialSpin; }
        private void Update()
        {
            velocity += Vector3.down * 2.2f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(spin * Time.deltaTime, Space.Self);
            if (transform.position.y < -25f) Destroy(gameObject);
        }
    }
}
