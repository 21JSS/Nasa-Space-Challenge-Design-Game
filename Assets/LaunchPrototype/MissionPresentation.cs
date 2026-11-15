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
        if(!WelcomeVisible && State!=FlightState.Preparing && State!=FlightState.Countdown) m.Avanzar(Mathf.Min(dt,0.1f)*EffectiveTimeRate);
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
        BuildCabinShell();
        icpsModule=PivotAtBounds(icpsModule,"ICPS separation pivot");
        serviceModule=PivotAtBounds(serviceModule,"Service separation pivot");
        lasTower=Part(rocket,"Launch abort tower",PrimitiveType.Cylinder,new Vector3(0,6.27f,0),new Vector3(0.055f,0.27f,0.055f),new Color(0.82f,0.86f,0.9f)).transform;
        Part(lasTower,"LAS tip",PrimitiveType.Sphere,new Vector3(0,1,0),new Vector3(1.5f,0.1f,1.5f),Cyan,true);
        var window=Part(crewCabin,"Crew observation window",PrimitiveType.Sphere,Vector3.zero,new Vector3(0.17f,0.11f,0.025f),new Color(0.025f,0.15f,0.24f));
        // El pivote está en unidades de mundo para que accesorios no hereden la escala FBX ×100.
        window.transform.localPosition=new Vector3(0,.06f,-.215f);
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
        BuildCelestialBodies();
        fillLight=new GameObject("Earth reflected blue light").AddComponent<Light>(); fillLight.transform.SetParent(transform,false);
        fillLight.type=LightType.Directional; fillLight.color=new Color(0.33f,0.57f,1f); fillLight.intensity=0.65f; fillLight.transform.rotation=Quaternion.Euler(-25,145,0);
        ocean=Make("Pacific splashdown",PrimitiveType.Plane,new Vector3(0,3.8f,0),new Vector3(80,1,80),new Color(0.02f,0.19f,0.29f)); ocean.SetActive(false);
    }
    void UpdateMissionScenery()
    {
        if(Simulation==null) return;
        bool descent=Simulation.Fase==Fase.Aterrizaje;
        if(ocean!=null)
        {
            ocean.SetActive(descent && !WelcomeVisible);
            ocean.transform.position=new Vector3(rocket.position.x,4.2f+crewCabin.localPosition.y-.33f,0);
        }
        if(earthRenderer!=null)
        {
            bool orbit=Simulation.Altura>80000;
            bool entering=Simulation.Fase==Fase.Regreso;
            earthRenderer.gameObject.SetActive(orbit || entering || WelcomeVisible);
            float approach=entering?1-Mathf.Clamp01(Altitude/120000f):0;
            earthRenderer.transform.position=WelcomeVisible
                ? launchCamera.transform.position+launchCamera.transform.forward*2400+launchCamera.transform.right*750
                : launchCamera.transform.position+launchCamera.transform.forward*2100-launchCamera.transform.up*(1600-approach*450)
                    -launchCamera.transform.right*(entering?Mathf.Clamp(downrange*.015f,0,350):0);
            earthRenderer.transform.localScale=Vector3.one*(WelcomeVisible?1300:3000);
            if(WelcomeVisible || !Simulation.Pausada && Simulation.Resultado==null)
                earthRenderer.transform.Rotate(Vector3.up,Time.deltaTime*.65f,Space.Self);
        }
        if(moon!=null)
        {
            moon.SetActive(Simulation.Altura>80000 || WelcomeVisible);
            moon.transform.position=launchCamera.transform.position+launchCamera.transform.forward*1500+launchCamera.transform.right*(WelcomeVisible?630:340)+launchCamera.transform.up*(WelcomeVisible?370:200);
            moon.transform.localScale=Vector3.one*(WelcomeVisible?180:145);
        }
        if(reentryGlow!=null)
        {
            reentryGlow.SetActive(Simulation.Fase==Fase.Regreso && Simulation.Calor>100000);
            reentryGlow.transform.localScale=new Vector3(0.8f,0.12f+Mathf.PingPong(Time.time*0.4f,0.12f),0.8f);
        }
        if((Simulation.Drogues || Simulation.Principales) && parachutes==null) BuildParachutes();
    }
    void BuildParachutes()
    {
        parachutes=new GameObject("Orion recovery parachutes"); parachutes.transform.SetParent(crewCabin,false);
        parachutes.transform.localScale=Vector3.one*.02f;
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
