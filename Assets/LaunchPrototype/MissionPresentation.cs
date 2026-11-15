using System;
using Artemis;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class LaunchDemo
{
    public Mision Simulation { get; private set; }
    public bool CloseCamera { get; private set; }
    public bool Muted { get; private set; }
    public float EffectiveTimeRate { get; private set; }
    RepositorioDatos missionData;
    Transform icpsModule, serviceModule, crewCabin, lasTower;
    bool icpsReleased, serviceReleased, lasReleased;
    GameObject parachutes, moon, reentryGlow, ocean;
    Renderer earthRenderer;
    Light fillLight;
    void InitializeMission()
    {
        missionData=MisionDemoRunner.CargarDatos();
        NewMission(2026,1);
    }
    void NewMission(int seed,int number)
    {
        Simulation=new Mision(missionData,new GeneradorParametros(missionData).Generar(seed,number));
        fuel=100;
        // Telemetría de eventos, útil para verificar el mismo flujo que consume el HUD.
        Simulation.OnEvento+=e=>Debug.Log("[Artemis] "+e);
    }
    public void GenerateMission(int seed,int number)
    {
        if (State != FlightState.Preparing && Simulation.Resultado == null) return;
        ResetDemo(); NewMission(seed,number);
    }
    public void ToggleCamera() { CloseCamera=!CloseCamera; }
    public void ToggleAudio() { Muted=!Muted; if(engineAudio!=null) engineAudio.mute=Muted; }
    public void ChooseProfile(int index)
    {
        if(State!=FlightState.Preparing) return;
        var c=missionData.Combinaciones[index]; Simulation.Seleccionar(c.material,c.combustible,c.capa_termica);
    }
    public void CycleMaterial()
    {
        if(State!=FlightState.Preparing) return;
        var ids=new System.Collections.Generic.List<string>(missionData.Materiales.Keys); var c=Simulation.Nave;
        Simulation.Seleccionar(ids[(ids.IndexOf(c.MaterialId)+1)%ids.Count],c.CombustibleId,c.CapaId);
    }
    public void CycleFuel()
    {
        if(State!=FlightState.Preparing) return;
        var ids=new System.Collections.Generic.List<string>(missionData.Combustibles.Keys); var c=Simulation.Nave;
        Simulation.Seleccionar(c.MaterialId,ids[(ids.IndexOf(c.CombustibleId)+1)%ids.Count],c.CapaId);
    }
    public void CycleShield()
    {
        if(State!=FlightState.Preparing) return;
        var ids=new System.Collections.Generic.List<string>(missionData.Capas.Keys); var c=Simulation.Nave;
        Simulation.Seleccionar(c.MaterialId,c.CombustibleId,ids[(ids.IndexOf(c.CapaId)+1)%ids.Count]);
    }
    void AdvanceMission(float dt)
    {
        var m=Simulation;
        string rate=m.Fase==Fase.Recorrido?"velocidad_tiempo_crucero":m.Fase==Fase.Regreso?"velocidad_tiempo_reingreso":m.Fase==Fase.Aterrizaje?"velocidad_tiempo_descenso":"velocidad_tiempo_ascenso";
        EffectiveTimeRate=(float)m.Datos.Juego[rate]*SpeedMultiplier;
        if(State!=FlightState.Preparing && State!=FlightState.Countdown) m.Avanzar(Mathf.Min(dt,0.1f)*EffectiveTimeRate);
        fuel=(float)m.PropelentePorcentaje; Speed=(float)m.Velocidad; launchElapsed=(float)m.Tiempo;
        // Escala visual comprimida y continua; las magnitudes del HUD siempre conservan SI.
        rocketHeight=(float)(300*Math.Log10(1+m.Altura/2000));
        if(m.SrbSeparado && !boostersSeparated) SeparateBoosters();
        if(m.CoreSeparado && !firstStageSeparated) SeparateFirstStage();
        if(m.LasSeparado && !lasReleased) { lasReleased=true; ReleaseModule(lasTower,new Vector3(0,4,0),new Vector3(0,0,16)); }
        if(m.IcpsSeparado && !icpsReleased) { icpsReleased=true; ReleaseModule(icpsModule,new Vector3(-1,-2,0),new Vector3(0,0,13)); }
        if(m.Capsula.ServicioSeparado && !serviceReleased) { serviceReleased=true; ReleaseModule(serviceModule,new Vector3(1.2f,-1,0),new Vector3(0,0,-14)); }
        if(m.Resultado!=null) State=m.Resultado.Tipo==TipoResultado.Exito?FlightState.Complete:FlightState.Failed;
        else if(m.Fase==Fase.Recorrido || m.Fase==Fase.Regreso || m.Fase==Fase.Aterrizaje) State=FlightState.Coasting;
        if(m.Resultado?.Tipo==TipoResultado.AbortoSeguro && !firstStageSeparated && m.Fase!=Fase.Abordaje)
        { SeparateFirstStage(); if(!boostersSeparated) SeparateBoosters(); }
    }
    void BuildUpperModules(Transform source)
    {
        icpsReleased=serviceReleased=lasReleased=false;
        string[] names={"ICPS","ServiceModule","CrewCabin"}; Transform[] parts=new Transform[3];
        var filter=source.GetComponentInChildren<MeshFilter>();
        if(filter==null) return;
        for(int i=0;i<3;i++)
        {
            Mesh mesh=Resources.Load<Mesh>("StageParts/"+names[i]);
            if(mesh==null) { Debug.LogError("Falta malla de etapa: "+names[i]); return; }
            var go=new GameObject(names[i]); go.transform.SetParent(filter.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>(); ApplyRealisticRocketFinish(go.transform,names[i],i==0?new Color(0.42f,0.55f,0.65f):new Color(0.84f,0.88f,0.91f),0.65f,0.62f);
            parts[i]=go.transform;
        }
        filter.GetComponent<Renderer>().enabled=false;
        icpsModule=parts[0]; serviceModule=parts[1]; crewCabin=parts[2];
        // Pivote de seguimiento en el centro geométrico, manteniendo intacta la malla recortada.
        crewCabin=PivotAtBounds(crewCabin,"Orion crew cabin");
        icpsModule=PivotAtBounds(icpsModule,"ICPS separation pivot");
        serviceModule=PivotAtBounds(serviceModule,"Service separation pivot");
        lasTower=Part(rocket,"Launch abort tower",PrimitiveType.Cylinder,new Vector3(0,6.27f,0),new Vector3(0.055f,0.27f,0.055f),new Color(0.82f,0.86f,0.9f)).transform;
        Part(lasTower,"LAS tip",PrimitiveType.Sphere,new Vector3(0,1,0),new Vector3(1.5f,0.1f,1.5f),Cyan,true);
        var window=Part(crewCabin,"Crew observation window",PrimitiveType.Sphere,Vector3.zero,new Vector3(0.35f,0.18f,0.06f),new Color(0.05f,0.34f,0.53f),true);
        // El pivote está en unidades de mundo para que accesorios no hereden la escala FBX ×100.
        window.transform.localPosition=new Vector3(0,0,-0.32f);
        reentryGlow=Part(crewCabin,"Heat shield plasma",PrimitiveType.Sphere,new Vector3(0,-0.30f,0),new Vector3(0.7f,0.08f,0.7f),new Color(1,0.18f,0.025f),true);
        reentryGlow.SetActive(false);
        var shield=Part(crewCabin,"Orion heat shield",PrimitiveType.Cylinder,new Vector3(0,-0.32f,0),new Vector3(0.63f,0.045f,0.63f),new Color(0.1f,0.09f,0.09f));
        shield.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.On;
    }
    Transform PivotAtBounds(Transform mesh,string name)
    {
        var pivot=new GameObject(name).transform; pivot.SetParent(rocket,false);
        pivot.position=mesh.GetComponent<Renderer>().bounds.center; mesh.SetParent(pivot,true); return pivot;
    }
    void ReleaseModule(Transform part,Vector3 velocity,Vector3 spin)
    {
        if(part==null) return;
        part.SetParent(transform,true); separatedStages.Add(part);
        part.gameObject.AddComponent<FallingStage>().Initialize(velocity,spin,this);
    }
    void BuildMissionScenery()
    {
        earthRenderer=transform.Find("Earth horizon").GetComponent<Renderer>();
        var tex=new Texture2D(1024,512,TextureFormat.RGBA32,true) { name="Earth oceans, continents and clouds",wrapMode=TextureWrapMode.Repeat };
        var pixels=new Color[1024*512];
        for(int y=0;y<512;y++) for(int x=0;x<1024;x++)
        {
            float u=x/1024f,v=y/512f;
            float a=Mathf.PerlinNoise(u*8.5f+32,v*5.5f+11)+0.35f*Mathf.PerlinNoise(u*22,v*19);
            Color col=a>0.77f ? Color.Lerp(new Color(0.09f,0.21f,0.11f),new Color(0.53f,0.43f,0.27f),Mathf.InverseLerp(0.77f,1.25f,a)) : Color.Lerp(new Color(0.015f,0.045f,0.13f),new Color(0.015f,0.22f,0.38f),Mathf.InverseLerp(0.3f,0.77f,a));
            float cloud=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.62f,0.86f,Mathf.PerlinNoise(u*30+Mathf.Sin(v*15)*2,v*14)))*0.7f;
            col=Color.Lerp(col,new Color(0.82f,0.9f,0.96f),cloud);
            if(v<0.06f || v>0.94f) col=Color.Lerp(col,new Color(0.8f,0.9f,0.95f),0.8f);
            pixels[y*1024+x]=col;
        }
        tex.SetPixels(pixels); tex.Apply(); generatedResources.Add(tex);
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetTexture("_BaseMap",tex); material.SetFloat("_Smoothness",0.3f);
        material.EnableKeyword("_EMISSION"); material.SetTexture("_EmissionMap",tex); material.SetColor("_EmissionColor",new Color(0.18f,0.25f,0.4f));
        generatedResources.Add(material); earthRenderer.sharedMaterial=material;
        moon=Make("Lunar destination",PrimitiveType.Sphere,new Vector3(300,1200,1000),Vector3.one*95,new Color(0.56f,0.59f,0.63f));
        var moonTexture=new Texture2D(256,128,TextureFormat.RGBA32,true);
        var moonPixels=new Color[256*128];
        for(int y=0;y<128;y++) for(int x=0;x<256;x++) { float n=Mathf.PerlinNoise(x*0.14f,y*0.14f)*0.3f+Mathf.PerlinNoise(x*0.035f,y*0.035f)*0.6f; moonPixels[y*256+x]=Color.Lerp(new Color(0.14f,0.17f,0.22f),new Color(0.82f,0.81f,0.76f),n); }
        moonTexture.SetPixels(moonPixels); moonTexture.Apply(); generatedResources.Add(moonTexture);
        var moonMat=new Material(material); moonMat.SetTexture("_BaseMap",moonTexture); moonMat.SetTexture("_EmissionMap",moonTexture); generatedResources.Add(moonMat); moon.GetComponent<Renderer>().sharedMaterial=moonMat;
        fillLight=new GameObject("Earth reflected blue light").AddComponent<Light>(); fillLight.transform.SetParent(transform,false);
        fillLight.type=LightType.Directional; fillLight.color=new Color(0.33f,0.57f,1f); fillLight.intensity=0.65f; fillLight.transform.rotation=Quaternion.Euler(-25,145,0);
        ocean=Make("Pacific splashdown",PrimitiveType.Plane,new Vector3(0,3.8f,0),new Vector3(80,1,80),new Color(0.02f,0.19f,0.29f)); ocean.SetActive(false);
    }
    void UpdateMissionScenery()
    {
        if(Simulation==null) return;
        bool descent=Simulation.Fase==Fase.Aterrizaje;
        if(ocean!=null) ocean.SetActive(descent);
        if(earthRenderer!=null)
        {
            bool orbit=Simulation.Altura>80000;
            earthRenderer.transform.position=orbit?launchCamera.transform.position+launchCamera.transform.forward*2100-launchCamera.transform.up*1600:new Vector3(0,-2500,0);
            earthRenderer.transform.localScale=Vector3.one*(orbit?3000:5000);
        }
        if(moon!=null)
        {
            moon.SetActive(Simulation.Altura>80000);
            moon.transform.position=launchCamera.transform.position+launchCamera.transform.forward*1400+launchCamera.transform.right*340+launchCamera.transform.up*150;
        }
        if(reentryGlow!=null)
        {
            reentryGlow.SetActive(Simulation.Fase==Fase.Regreso && Simulation.Calor>100000);
            reentryGlow.transform.localScale=new Vector3(0.8f,0.12f+Mathf.PingPong(Time.time*0.4f,0.12f),0.8f);
        }
        if((Simulation.Drogues || Simulation.Principales) && parachutes==null) BuildParachutes();
        if(parachutes!=null) parachutes.transform.localScale=Vector3.one*(Simulation.Principales?1f:0.35f);
    }
    void BuildParachutes()
    {
        parachutes=new GameObject("Orion recovery parachutes"); parachutes.transform.SetParent(crewCabin,false);
        for(int i=0;i<3;i++)
        {
            Vector3 center=new Vector3((i-1)*1.5f,3.6f+(i%2)*0.5f,0.1f);
            Part(parachutes.transform,"Orange recovery canopy",PrimitiveType.Sphere,center,new Vector3(1.75f,0.55f,1.75f),new Color(0.98f,0.34f,0.08f));
            for(int line=0;line<4;line++)
            {
                float a=line*Mathf.PI/2; var end=center+new Vector3(Mathf.Cos(a)*0.7f,-0.15f,Mathf.Sin(a)*0.7f);
                var rope=new GameObject("Parachute suspension").AddComponent<LineRenderer>(); rope.transform.SetParent(parachutes.transform,false);
                rope.useWorldSpace=false; rope.positionCount=2; rope.SetPosition(0,Vector3.zero); rope.SetPosition(1,end); rope.startWidth=rope.endWidth=0.012f;
                var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.color=new Color(0.8f,0.86f,0.9f); generatedResources.Add(mat); rope.sharedMaterial=mat;
            }
        }
    }
}
