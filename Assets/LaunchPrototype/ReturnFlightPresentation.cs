using Artemis;
using UnityEngine;

public sealed partial class LaunchDemo
{
    public bool WelcomeVisible { get; private set; } = true;
    public float VisualFlightAngle { get; private set; } = -6f;
    public Vector3 VisualFlightDirection { get; private set; } = Vector3.down;
    float downrange, previousVisualHeight;
    double previousPoseTime;
    bool returning;
    LineRenderer entryVector;

    public void BeginMission()
    {
        WelcomeVisible = false;
        hud.ShowWelcome(false);
    }

    public void ShowWelcome()
    {
        WelcomeVisible = true;
        hud.ShowWelcome(true);
    }

    public void TogglePause()
    {
        if (!WelcomeVisible && Simulation.Resultado == null) Simulation.Pausar();
    }

    void ResetFlightPresentation()
    {
        downrange = previousVisualHeight = 0;
        previousPoseTime = 0;
        returning = false;
        VisualFlightAngle = -6;
        entryVector = null; // The previous line belongs to the destroyed rocket.
    }

    void UpdateVehiclePose()
    {
        var m = Simulation;
        bool entry = m.Fase == Fase.Regreso;
        bool landing = m.Fase == Fase.Aterrizaje;
        bool moving = !WelcomeVisible && !m.Pausada && m.Resultado == null;
        bool preview = m.ListaReentrada && m.Capsula.ServicioSeparado;
        float gamma = (float)(m.AnguloEntrada + m.CorreccionEntrada);
        if ((entry || landing) && m.EstadoReingreso != null)
            gamma = (float)m.EstadoReingreso[2] * Mathf.Rad2Deg;

        if (landing)
        {
            // Keep the entry direction at the handoff, then hang vertically below the canopies.
            float h0 = (float)m.Datos.Fisica["paracaidas"][0]["h_max"];
            float deploy = Mathf.SmoothStep(0, 1, 1 - (float)m.Altura / h0);
            if (m.Drogues) deploy = Mathf.Max(deploy, .85f);
            if (m.Principales) deploy = 1;
            gamma = Mathf.Lerp(gamma, -90, deploy);
        }
        VisualFlightAngle = gamma;
        float radians = gamma * Mathf.Deg2Rad;
        VisualFlightDirection = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0).normalized;

        if (entry || landing)
        {
            if (!returning)
            {
                returning = true;
                previousVisualHeight = rocketHeight;
                previousPoseTime = m.Tiempo;
            }
            if (moving && m.Tiempo > previousPoseTime)
            {
                // Equal compression on both axes preserves gamma in the visible trajectory.
                float dy = rocketHeight - previousVisualHeight;
                if (Mathf.Abs(Mathf.Sin(radians)) > .015f)
                    downrange += dy * Mathf.Cos(radians) / Mathf.Sin(radians);
                else
                    downrange += (float)(m.Velocidad * (m.Tiempo - previousPoseTime)) * 300f / (Mathf.Log(10) * (2000 + (float)m.Altura));
            }
        }
        previousVisualHeight = rocketHeight;
        previousPoseTime = m.Tiempo;
        rocket.position = new Vector3(downrange, 4.2f + rocketHeight, 0);

        if (crewCabin != null && (preview || entry || landing))
        {
            // The base/heat shield is local -Y. Bank rotates around the flight vector.
            Quaternion attitude = Quaternion.FromToRotation(Vector3.down, VisualFlightDirection)
                * Quaternion.AngleAxis((float)m.Alabeo, Vector3.up);
            if (!m.Pausada && !WelcomeVisible)
                crewCabin.rotation = Quaternion.Slerp(crewCabin.rotation, attitude, 1 - Mathf.Exp(-Time.deltaTime * 4f));
            if (entryVector == null) BuildEntryVector();
            entryVector.gameObject.SetActive((preview || entry) && m.Resultado == null);
            Vector3 start = crewCabin.position + Vector3.forward * .5f;
            Vector3 end = start + VisualFlightDirection * 4f;
            Vector3 side = new Vector3(-VisualFlightDirection.y, VisualFlightDirection.x, 0) * .25f;
            entryVector.SetPositions(new[] { start - VisualFlightDirection * 2f, end,
                end - VisualFlightDirection * .6f + side, end,
                end - VisualFlightDirection * .6f - side });
        }
        if (parachutes != null)
        {
            // Canopies stay above the cabin while its attitude stabilizes.
            parachutes.transform.rotation = Quaternion.identity;
            float size = m.Principales ? 1 : .35f;
            if (moving) parachutes.transform.localScale = Vector3.MoveTowards(parachutes.transform.localScale, Vector3.one * size, Time.deltaTime * 1.2f);
        }
    }

    void BuildEntryVector()
    {
        entryVector = new GameObject("Entry direction indicator").AddComponent<LineRenderer>();
        entryVector.transform.SetParent(rocket, false);
        entryVector.useWorldSpace = true;
        entryVector.positionCount = 5;
        entryVector.startWidth = entryVector.endWidth = .025f;
        entryVector.numCornerVertices = 3;
        if (!materials.TryGetValue(Cyan, out Material lineMaterial))
        {
            lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lineMaterial.color = Cyan;
            generatedResources.Add(lineMaterial);
            materials[Cyan] = lineMaterial;
        }
        entryVector.sharedMaterial = lineMaterial;
    }

    void BuildCabinShell()
    {
        // The source's cropped tip is extremely narrow; give the crew compartment a closed shell.
        foreach (Renderer original in crewCabin.GetComponentsInChildren<Renderer>()) original.enabled = false;
        const int sides = 64;
        Vector2[] profile = { new Vector2(-.28f,.305f), new Vector2(-.22f,.32f),
            new Vector2(.08f,.20f), new Vector2(.22f,.115f), new Vector2(.26f,.11f), new Vector2(.27f,0) };
        var vertices = new Vector3[(sides+1)*profile.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[(profile.Length-1)*sides*6];
        int index = 0;
        for (int ring=0; ring<profile.Length; ring++) for (int side=0; side<=sides; side++)
        {
            int i=ring*(sides+1)+side;
            float angle=side*Mathf.PI*2/sides;
            vertices[i]=new Vector3(Mathf.Cos(angle)*profile[ring].y,profile[ring].x,Mathf.Sin(angle)*profile[ring].y);
            uv[i]=new Vector2(side/(float)sides,ring/(float)(profile.Length-1));
            if(ring==profile.Length-1 || side==sides) continue;
            int next=i+sides+1;
            triangles[index++]=i; triangles[index++]=next; triangles[index++]=i+1;
            triangles[index++]=i+1; triangles[index++]=next; triangles[index++]=next+1;
        }
        var mesh=new Mesh { name="Orion crew pressure shell",vertices=vertices,uv=uv,triangles=triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); generatedResources.Add(mesh);
        var body=new GameObject("Orion ceramic pressure shell",typeof(MeshFilter),typeof(MeshRenderer));
        body.transform.SetParent(crewCabin,false); body.GetComponent<MeshFilter>().sharedMesh=mesh;
        ApplyRealisticRocketFinish(body.transform,"Crew shell",new Color(.88f,.9f,.92f),.24f,.45f);
    }
}
